using System.Security.Claims;
using EventPlatform.Web.Helpers;
using EventPlatform.Web.Models;
using EventPlatform.Web.Models.Api;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EventPlatform.Web.Controllers.Api;

[ApiController]
[Route("api/v1/me")]
[Produces("application/json")]
[Tags("Me")]
[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
public class MeApiController : ControllerBase
{
    private readonly AppDbContext _context;

    public MeApiController(AppDbContext context)
    {
        _context = context;
    }

    private int CurrentUserId =>
        int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : 0;

    /// <summary>Lists the signed-in user's upcoming events.</summary>
    [HttpGet("events")]
    [EndpointSummary("My upcoming events")]
    [EndpointDescription("Upcoming published events the signed-in user created or joined. Requires a bearer token.")]
    [ProducesResponseType<MyEventsDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<MyEventsDto>> MyEvents()
    {
        var userId = CurrentUserId;
        var now = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified);

        var rows = await _context.Events
            .AsNoTracking()
            .Where(e => e.Isapproved && e.Date >= now &&
                        (e.Createdbyuserid == userId || e.Users.Any(u => u.Userid == userId)))
            .OrderBy(e => e.Date)
            .Select(e => new
            {
                IsCreator = e.Createdbyuserid == userId,
                e.Publicid,
                e.Eventname,
                e.Category,
                e.Date,
                e.Duration,
                e.Location,
                ParticipantCount = e.Users.Count(u => u.Userid != e.Createdbyuserid) + 1
            })
            .ToListAsync();

        List<EventSummaryDto> Map(bool creator) => rows
            .Where(r => r.IsCreator == creator)
            .Select(r => new EventSummaryDto(r.Publicid, r.Eventname, r.Category,
                DateTime.SpecifyKind(r.Date, DateTimeKind.Utc), r.Duration, r.Location, r.ParticipantCount,
                Url.Action("Details", "Events", new { slugId = SlugHelper.Compose(r.Eventname, r.Publicid) }, Request.Scheme)))
            .ToList();

        return Ok(new MyEventsDto(Map(creator: true), Map(creator: false)));
    }
}
