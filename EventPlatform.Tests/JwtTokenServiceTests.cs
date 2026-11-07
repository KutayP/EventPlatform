using System.Security.Claims;
using System.Text;
using EventPlatform.Web.Services;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace EventPlatform.Tests;

public class JwtTokenServiceTests
{
    private static JwtSettings Settings(string key = "test-signing-key-that-is-long-enough-1234567890") =>
        new(JwtSettings.DefaultIssuer, JwtSettings.DefaultAudience,
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key)), TimeSpan.FromHours(1));

    [Fact]
    public async Task CreateToken_ProducesValidTokenWithUserClaims()
    {
        var settings = Settings();
        var (token, _) = new JwtTokenService(settings).CreateToken(42, "demo", "User");

        var result = await new JsonWebTokenHandler().ValidateTokenAsync(token, settings.ValidationParameters);

        Assert.True(result.IsValid);
        Assert.Equal("42", result.ClaimsIdentity.FindFirst(ClaimTypes.NameIdentifier)?.Value);
        Assert.Equal("demo", result.ClaimsIdentity.Name);
        Assert.Equal("User", result.ClaimsIdentity.FindFirst(ClaimTypes.Role)?.Value);
    }

    [Fact]
    public void CreateToken_ExpiresAfterConfiguredLifetime()
    {
        var before = DateTime.UtcNow;
        var (_, expiresAt) = new JwtTokenService(Settings()).CreateToken(1, "demo", "User");

        Assert.InRange(expiresAt, before.AddMinutes(59), DateTime.UtcNow.AddMinutes(61));
    }

    [Fact]
    public async Task Token_SignedWithAnotherKey_IsRejected()
    {
        var (token, _) = new JwtTokenService(Settings("another-signing-key-that-is-long-enough-0987654321"))
            .CreateToken(1, "demo", "User");

        var result = await new JsonWebTokenHandler().ValidateTokenAsync(token, Settings().ValidationParameters);

        Assert.False(result.IsValid);
    }

    [Fact]
    public async Task Token_ForAnotherAudience_IsRejected()
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes("test-signing-key-that-is-long-enough-1234567890"));
        var other = new JwtSettings(JwtSettings.DefaultIssuer, "SomeOtherApi", key, TimeSpan.FromHours(1));
        var (token, _) = new JwtTokenService(other).CreateToken(1, "demo", "User");

        var result = await new JsonWebTokenHandler().ValidateTokenAsync(token, Settings().ValidationParameters);

        Assert.False(result.IsValid);
    }
}
