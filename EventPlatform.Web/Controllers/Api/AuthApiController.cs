using EventPlatform.Web.Models;
using EventPlatform.Web.Models.Api;
using EventPlatform.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

namespace EventPlatform.Web.Controllers.Api;

[ApiController]
[Route("api/v1/auth")]
[Produces("application/json")]
[Tags("Auth")]
public class AuthApiController : ControllerBase
{
    // Kullanıcı bulunamasa da aynı süre harcansın diye sahte bir hash ile karşılaştırma yapılır
    // (yanıt süresinden kullanıcı adının var olup olmadığı anlaşılmasın).
    private static readonly Lazy<string> DummyHash = new(() => BCrypt.Net.BCrypt.HashPassword("dummy-password-for-timing"));

    private readonly AppDbContext _context;
    private readonly JwtTokenService _tokens;

    public AuthApiController(AppDbContext context, JwtTokenService tokens)
    {
        _context = context;
        _tokens = tokens;
    }

    /// <summary>Exchanges a username and password for a short-lived access token.</summary>
    [HttpPost("token")]
    [AllowAnonymous]
    [EnableRateLimiting(ApiRateLimits.Auth)]
    [EndpointSummary("Get an access token")]
    [EndpointDescription("Exchanges a username and password for a JWT access token valid for one hour. Send it as `Authorization: Bearer <token>`. Limited to 10 attempts per minute per IP address.")]
    [ProducesResponseType<TokenResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    public async Task<ActionResult<TokenResponse>> CreateToken(TokenRequest request)
    {
        var user = await _context.Users
            .AsNoTracking()
            .Where(u => u.Username == request.Username)
            .Select(u => new { u.Userid, u.Username, u.Password, u.Role })
            .FirstOrDefaultAsync();

        var passwordOk = BCrypt.Net.BCrypt.Verify(request.Password, user?.Password ?? DummyHash.Value);

        if (user == null || !passwordOk)
        {
            // Kullanıcı yok ile şifre yanlış aynı yanıtı verir.
            return Problem(statusCode: StatusCodes.Status401Unauthorized, title: "Invalid username or password.");
        }

        var (token, expiresAt) = _tokens.CreateToken(user.Userid, user.Username, user.Role);
        return Ok(new TokenResponse(token, "Bearer", expiresAt));
    }
}
