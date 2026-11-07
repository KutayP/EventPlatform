using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.RateLimiting;
using EventPlatform.Web.Hubs;
using EventPlatform.Web.Models;
using EventPlatform.Web.Services;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");

builder.Services.AddControllersWithViews();

// Üretilen tüm bağlantılar küçük harfli olsun (/events, /account/login): adresler tutarlı görünür.
builder.Services.AddRouting(options => options.LowercaseUrls = true);
builder.Services.AddSignalR();
builder.Services.AddScoped<NotificationService>();
builder.Services.AddMemoryCache();

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(connectionString));

// Şifreleme anahtarları veritabanında: yeniden başlatmada oturumlar ve formlar bozulmaz.
builder.Services.AddDbContext<DataProtectionKeyContext>(options =>
    options.UseNpgsql(connectionString));

var dataProtection = builder.Services.AddDataProtection()
    .SetApplicationName("EventPlatform")
    .PersistKeysToDbContext<DataProtectionKeyContext>();

// Anahtarlar veritabanında sertifikayla şifreli saklanır; sertifika kodda değil yapılandırmada (secret).
var certBase64 = builder.Configuration["DataProtection:CertificateBase64"];
var certPassword = builder.Configuration["DataProtection:CertificatePassword"];
if (!string.IsNullOrWhiteSpace(certBase64))
{
    var certificate = X509CertificateLoader.LoadPkcs12(Convert.FromBase64String(certBase64), certPassword);
    dataProtection.ProtectKeysWithCertificate(certificate);
}

// Render gibi bir ters vekilin arkasında gerçek istemci adresi ve https bilgisi başlıklardan gelir.
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    // Platformun vekil adresleri sabit değil; konteynere sadece platformun vekili ulaşır.
    options.KnownIPNetworks.Clear();
    options.KnownProxies.Clear();
});

// ---------- REST API: JWT, OpenAPI belgesi, istek hızı sınırı ----------

// İmza anahtarı yapılandırmadan gelir (yerelde user-secrets, canlıda Jwt__SigningKey ortam değişkeni).
// Tanımlı değilse uygulama yine açılır ama her başlatmada rastgele bir anahtar üretilir:
// API anahtarları yeniden başlatmada geçersiz olur (yalnızca geliştirme için kabul edilebilir).
var jwtKeyText = builder.Configuration["Jwt:SigningKey"];
byte[] jwtKeyBytes;
if (!string.IsNullOrWhiteSpace(jwtKeyText) && jwtKeyText.Length >= 32)
{
    jwtKeyBytes = Encoding.UTF8.GetBytes(jwtKeyText);
}
else
{
    Console.WriteLine("UYARI: Jwt:SigningKey tanımlı değil ya da 32 karakterden kısa; geçici rastgele anahtar kullanılıyor.");
    jwtKeyBytes = RandomNumberGenerator.GetBytes(64);
}

var jwtSettings = new JwtSettings(
    JwtSettings.DefaultIssuer,
    JwtSettings.DefaultAudience,
    new SymmetricSecurityKey(jwtKeyBytes),
    TimeSpan.FromHours(1));

builder.Services.AddSingleton(jwtSettings);
builder.Services.AddSingleton<JwtTokenService>();

builder.Services.AddOpenApi(options =>
{
    // Belgede yalnızca /api altındaki uç noktalar yer alır (MVC sayfaları değil).
    options.ShouldInclude = description =>
        description.RelativePath?.StartsWith("api/", StringComparison.OrdinalIgnoreCase) == true;

    options.AddDocumentTransformer((document, context, cancellationToken) =>
    {
        document.Info = new OpenApiInfo
        {
            Title = "EventPlatform API",
            Version = "v1",
            Description = "Read events, sign in with a JWT and join or leave events. " +
                          "Get a token from POST /api/v1/auth/token (demo / Demo1234!) and paste it into the Bearer authentication field."
        };

        document.Components ??= new OpenApiComponents();
        document.Components.SecuritySchemes ??= new Dictionary<string, IOpenApiSecurityScheme>();
        document.Components.SecuritySchemes["Bearer"] = new OpenApiSecurityScheme
        {
            Type = SecuritySchemeType.Http,
            Scheme = "bearer",
            BearerFormat = "JWT",
            Description = "Paste the accessToken returned by POST /api/v1/auth/token."
        };

        return Task.CompletedTask;
    });

    // Kilit simgesi ve "kimlik doğrulama gerekli" bilgisi yalnızca gerçekten giriş isteyen uç noktalarda görünsün.
    options.AddOperationTransformer((operation, context, cancellationToken) =>
    {
        var metadata = context.Description.ActionDescriptor.EndpointMetadata;
        var requiresAuth = metadata.OfType<IAuthorizeData>().Any() && !metadata.OfType<IAllowAnonymous>().Any();

        if (requiresAuth)
        {
            operation.Security =
            [
                new OpenApiSecurityRequirement
                {
                    [new OpenApiSecuritySchemeReference("Bearer")] = []
                }
            ];
        }

        return Task.CompletedTask;
    });
});

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    // Şifre deneme sınırı: her IP adresi için dakikada 10 istek (kaba kuvvet denemelerini yavaşlatır).
    options.AddPolicy(ApiRateLimits.Auth, httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 10,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0
            }));
});

builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Account/Login";
        options.AccessDeniedPath = "/Account/AccessDenied";
        options.ExpireTimeSpan = TimeSpan.FromHours(2);
        options.SlidingExpiration = true;
        options.Cookie.HttpOnly = true;
        options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
        options.Cookie.SameSite = SameSiteMode.Lax;

        // Silinmiş kullanıcının (ör. demo verisi sıfırlandıktan sonra) eski oturumu geçersiz sayılır.
        options.Events.OnValidatePrincipal = UserSessionValidator.ValidateAsync;
    })
    // API yalnızca bu şemayı kabul eder: çerez API'de geçerli değildir, bu yüzden CSRF saldırısı API'ye işlemez.
    .AddJwtBearer(JwtBearerDefaults.AuthenticationScheme, options =>
    {
        options.MapInboundClaims = false;   // claim adları üretildiği gibi kalsın (NameIdentifier, Name, Role)
        options.TokenValidationParameters = jwtSettings.ValidationParameters;
    });

var app = builder.Build();

// En başta olmalı: sonraki ara katmanlar doğru şema (https) ve istemci adresini görsün.
app.UseForwardedHeaders();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

// 404 gibi gövdesiz yanıtlarda siteye uyan bir sayfa göster (durum kodu korunur).
// API isteklerinde değil: API'yi kullanan program HTML sayfası değil, durum kodu bekler.
app.UseWhen(
    context => !context.Request.Path.StartsWithSegments("/api"),
    branch => branch.UseStatusCodePagesWithReExecute("/Home/Status", "?code={0}"));

// Canlıda https yönlendirmesini platform kenarda yapıyor; burada sadece yerel geliştirmede gerekli.
if (app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}

app.UseRouting();

app.UseRateLimiter();

app.UseAuthentication();
app.UseAuthorization();

app.MapStaticAssets();

app.MapControllerRoute(
        name: "default",
        pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();

// API belgesi: /openapi/v1.json (makine için) ve /api/docs (okunabilir belge + deneme alanı, Scalar).
app.MapOpenApi();
app.MapScalarApiReference("/api/docs", options => options
    .WithTitle("EventPlatform API")
    .WithDefaultHttpClient(ScalarTarget.Shell, ScalarClient.Curl)
    .AddPreferredSecuritySchemes("Bearer"));

app.MapHub<EventChatHub>("/hubs/eventchat");
app.MapHub<NotificationHub>("/hubs/notifications");

// Sağlık kontrolü: veritabanına dokunmaz, yalnızca uygulamanın ayakta olduğunu söyler.
app.MapGet("/healthz", () => Results.Text("ok"));

app.Run();