using System.Security.Claims;
using EventPlatform.Web.Helpers;
using EventPlatform.Web.Models;
using EventPlatform.Web.Models.Api;
using EventPlatform.Web.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EventPlatform.Web.Controllers.Api;

[ApiController]
[Route("api/v1/events")]
[Produces("application/json")]
[Tags("Events")]
public class EventsApiController : ControllerBase
{
    private const int DefaultPageSize = 20;
    private const int MaxPageSize = 50;
    private const int MaxSearchLength = 100;

    private readonly AppDbContext _context;
    private readonly NotificationService _notifications;

    public EventsApiController(AppDbContext context, NotificationService notifications)
    {
        _context = context;
        _notifications = notifications;
    }

    // Yalnızca korumalı uç noktalarda anlamlı; kimlik JWT'den gelir, istekten asla alınmaz.
    private int CurrentUserId =>
        int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : 0;

    private static DateTime NowForDb() =>
        DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified);

    // Veritabanında UTC saklanır (Kind = Unspecified); JSON'da "Z" ile UTC olarak gitsin.
    private static DateTime AsUtc(DateTime value) =>
        DateTime.SpecifyKind(value, DateTimeKind.Utc);

    private static string EscapeLike(string value) =>
        value.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_");

    private static string? NormalizeId(string? publicId)
    {
        var id = publicId?.Trim().ToLowerInvariant();
        return PublicIdGenerator.IsValid(id) ? id : null;
    }

    private string? PageUrl(string name, string publicId) =>
        Url.Action("Details", "Events", new { slugId = SlugHelper.Compose(name, publicId) }, Request.Scheme);

    private ObjectResult EventNotFound() =>
        Problem(statusCode: StatusCodes.Status404NotFound, title: "Event not found.");

    /// <summary>Lists upcoming published events.</summary>
    [HttpGet]
    [EndpointSummary("List upcoming events")]
    [EndpointDescription("Returns upcoming published events, soonest first. Optional search in name and location, category filter (a value from GET /api/v1/categories, e.g. Gaming; case-insensitive) and paging (pageSize 1-50).")]
    [ProducesResponseType<PagedResult<EventSummaryDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<PagedResult<EventSummaryDto>>> List(
        [FromQuery] string? q,
        [FromQuery] string? category,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = DefaultPageSize)
    {
        if (page < 1 || pageSize < 1 || pageSize > MaxPageSize)
        {
            return Problem(statusCode: StatusCodes.Status400BadRequest, title: "Invalid paging.",
                detail: $"page must be at least 1 and pageSize between 1 and {MaxPageSize}.");
        }

        var now = NowForDb();
        var query = _context.Events
            .AsNoTracking()
            .Where(e => e.Isapproved && e.Date >= now);

        if (!string.IsNullOrWhiteSpace(category))
        {
            // Kategoriler veritabanında İngilizce saklanır (Gaming, Music…); ekranda Türkçe gösterilir.
            // API sabit değerleri kullanır; bilinmeyen bir değer sessizce boş liste döndürmek yerine 400 verir.
            var valid = await _context.Interests
                .AsNoTracking()
                .OrderBy(i => i.Interestname)
                .Select(i => i.Interestname)
                .ToListAsync();

            var match = valid.FirstOrDefault(v => string.Equals(v, category.Trim(), StringComparison.OrdinalIgnoreCase));
            if (match == null)
            {
                return Problem(statusCode: StatusCodes.Status400BadRequest, title: "Unknown category.",
                    detail: $"'{category.Trim()}' is not a category. Valid values: {string.Join(", ", valid)}.");
            }

            query = query.Where(e => e.Category == match);
        }

        q = q?.Trim();
        if (q is { Length: > MaxSearchLength })
            q = q[..MaxSearchLength];

        if (!string.IsNullOrEmpty(q))
        {
            var pattern = "%" + EscapeLike(q) + "%";
            query = query.Where(e =>
                EF.Functions.ILike(e.Eventname, pattern, "\\") ||
                EF.Functions.ILike(e.Location, pattern, "\\"));
        }

        var total = await query.CountAsync();

        var rows = await query
            .OrderBy(e => e.Date)
            .ThenBy(e => e.Eventid)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(e => new
            {
                e.Publicid,
                e.Eventname,
                e.Category,
                e.Date,
                e.Duration,
                e.Location,
                ParticipantCount = e.Users.Count(u => u.Userid != e.Createdbyuserid) + 1
            })
            .ToListAsync();

        var items = rows
            .Select(r => new EventSummaryDto(r.Publicid, r.Eventname, r.Category, AsUtc(r.Date), r.Duration,
                r.Location, r.ParticipantCount, PageUrl(r.Eventname, r.Publicid)))
            .ToList();

        return Ok(new PagedResult<EventSummaryDto>(items, page, pageSize, total));
    }

    /// <summary>Gets one published event.</summary>
    [HttpGet("{publicId}")]
    [EndpointSummary("Get an event")]
    [EndpointDescription("Returns a published event by its 8-character public id (the code at the end of the event page URL).")]
    [ProducesResponseType<EventDetailDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<EventDetailDto>> Get(string publicId)
    {
        var id = NormalizeId(publicId);
        if (id == null)
            return EventNotFound();

        var e = await _context.Events
            .AsNoTracking()
            .Where(x => x.Publicid == id && x.Isapproved)
            .Select(x => new
            {
                x.Publicid,
                x.Eventname,
                x.Description,
                x.Category,
                x.Date,
                x.Duration,
                x.Location,
                x.Latitude,
                x.Longitude,
                ParticipantCount = x.Users.Count(u => u.Userid != x.Createdbyuserid) + 1,
                CreatorUsername = x.Createdbyuser.Username,
                CreatorFirstname = x.Createdbyuser.Firstname,
                CreatorLastname = x.Createdbyuser.Lastname
            })
            .FirstOrDefaultAsync();

        if (e == null)
            return EventNotFound();

        return Ok(new EventDetailDto(
            e.Publicid, e.Eventname, e.Description, e.Category, AsUtc(e.Date), e.Duration, e.Location,
            e.Latitude, e.Longitude, e.ParticipantCount,
            new EventCreatorDto(e.CreatorUsername, DisplayNameHelper.Short(e.CreatorFirstname, e.CreatorLastname)),
            PageUrl(e.Eventname, e.Publicid)));
    }

    /// <summary>Joins an event as the signed-in user.</summary>
    [HttpPost("{publicId}/participation")]
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    [EndpointSummary("Join an event")]
    [EndpointDescription("Adds the signed-in user to the event's participants. Requires a bearer token. The event's creator is notified in real time.")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Join(string publicId)
    {
        var id = NormalizeId(publicId);
        if (id == null)
            return EventNotFound();

        var userId = CurrentUserId;

        var ev = await _context.Events
            .Include(e => e.Users.Where(u => u.Userid == userId))
            .FirstOrDefaultAsync(e => e.Publicid == id && e.Isapproved);

        if (ev == null)
            return EventNotFound();

        if (ev.Date < NowForDb())
            return Problem(statusCode: StatusCodes.Status409Conflict, title: "This event has already ended.");

        if (ev.Createdbyuserid == userId || ev.Users.Count > 0)
            return Problem(statusCode: StatusCodes.Status409Conflict, title: "You are already part of this event.");

        var user = await _context.Users.FindAsync(userId);
        if (user == null)
            return Unauthorized();

        ev.Users.Add(user);

        try
        {
            await _context.SaveChangesAsync();
        }
        catch (DbUpdateException ex) when (ex.InnerException is Npgsql.PostgresException { SqlState: "23505" })
        {
            // Aynı anda iki istek geldi: birincil anahtar ikinci kaydı engelledi.
            return Problem(statusCode: StatusCodes.Status409Conflict, title: "You are already part of this event.");
        }

        await _notifications.NotifyAsync(ev.Createdbyuserid, NotificationTypes.EventJoined, userId, ev.Eventid);
        return NoContent();
    }

    /// <summary>Leaves an event the signed-in user has joined.</summary>
    [HttpDelete("{publicId}/participation")]
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    [EndpointSummary("Leave an event")]
    [EndpointDescription("Removes the signed-in user from the event's participants. Requires a bearer token.")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Leave(string publicId)
    {
        var id = NormalizeId(publicId);
        if (id == null)
            return EventNotFound();

        var userId = CurrentUserId;

        var ev = await _context.Events
            .Include(e => e.Users.Where(u => u.Userid == userId))
            .FirstOrDefaultAsync(e => e.Publicid == id && e.Isapproved);

        if (ev == null)
            return EventNotFound();

        var me = ev.Users.FirstOrDefault();
        if (me == null)
            return Problem(statusCode: StatusCodes.Status409Conflict, title: "You have not joined this event.");

        ev.Users.Remove(me);
        await _context.SaveChangesAsync();

        await _notifications.RemoveAsync(ev.Createdbyuserid, NotificationTypes.EventJoined, userId, ev.Eventid);
        return NoContent();
    }
}
