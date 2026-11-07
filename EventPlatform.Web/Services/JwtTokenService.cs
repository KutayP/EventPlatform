using System.Security.Claims;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace EventPlatform.Web.Services;

// API anahtarı (JWT) ayarları. İmza anahtarı yapılandırmadan (secret) gelir, kodda yok.
public sealed record JwtSettings(string Issuer, string Audience, SymmetricSecurityKey SigningKey, TimeSpan Lifetime)
{
    public const string DefaultIssuer = "EventPlatform";
    public const string DefaultAudience = "EventPlatform.Api";

    public TokenValidationParameters ValidationParameters => new()
    {
        ValidIssuer = Issuer,
        ValidAudience = Audience,
        IssuerSigningKey = SigningKey,
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateIssuerSigningKey = true,
        ValidateLifetime = true,
        ClockSkew = TimeSpan.FromMinutes(1),
        NameClaimType = ClaimTypes.Name,
        RoleClaimType = ClaimTypes.Role
    };
}

// API için kısa ömürlü erişim anahtarı üretir. Web sitesi çerez kullanır; API ise her istekte
// "Authorization: Bearer <anahtar>" başlığı bekler (bu yüzden API'de CSRF riski yoktur).
public sealed class JwtTokenService
{
    private readonly JwtSettings _settings;
    private readonly JsonWebTokenHandler _handler = new();

    public JwtTokenService(JwtSettings settings)
    {
        _settings = settings;
    }

    public (string Token, DateTime ExpiresAtUtc) CreateToken(int userId, string username, string role)
    {
        var now = DateTime.UtcNow;
        var expires = now.Add(_settings.Lifetime);

        var descriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(new[]
            {
                new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
                new Claim(ClaimTypes.Name, username),
                new Claim(ClaimTypes.Role, role)
            }),
            Issuer = _settings.Issuer,
            Audience = _settings.Audience,
            IssuedAt = now,
            NotBefore = now,
            Expires = expires,
            SigningCredentials = new SigningCredentials(_settings.SigningKey, SecurityAlgorithms.HmacSha256)
        };

        return (_handler.CreateToken(descriptor), expires);
    }
}
