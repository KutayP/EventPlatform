using System.Security.Cryptography.X509Certificates;
using EventPlatform.Web.Hubs;
using EventPlatform.Web.Models;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");

builder.Services.AddControllersWithViews();
builder.Services.AddSignalR();

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
    options.KnownNetworks.Clear();
    options.KnownProxies.Clear();
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
app.UseStatusCodePagesWithReExecute("/Home/Status", "?code={0}");

// Canlıda https yönlendirmesini platform kenarda yapıyor; burada sadece yerel geliştirmede gerekli.
if (app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapStaticAssets();

app.MapControllerRoute(
        name: "default",
        pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();

app.MapHub<EventChatHub>("/hubs/eventchat");

// Sağlık kontrolü: veritabanına dokunmaz, yalnızca uygulamanın ayakta olduğunu söyler.
app.MapGet("/healthz", () => Results.Text("ok"));

app.Run();