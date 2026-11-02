using System.Security.Claims;
using EventPlatform.Web.Helpers;
using EventPlatform.Web.Models;
using EventPlatform.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EventPlatform.Web.Controllers;

[Authorize]
[Route("bildirimler")]
public class NotificationsController : Controller
{
    private const int PanelSize = 8;
    private const int PageSize = 30;

    private readonly AppDbContext _context;
    private readonly NotificationService _notifications;

    public NotificationsController(AppDbContext context, NotificationService notifications)
    {
        _context = context;
        _notifications = notifications;
    }

    // Kimlik her zaman cookie'den gelir; yalnızca kendi bildirimlerime erişirim.
    private int CurrentUserId =>
        int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : 0;

    private static DateTime NowForDb() =>
        DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified);

    // Tüm geçmiş: yeniden eskiye, sayfa sayfa.
    [HttpGet("")]
    public async Task<IActionResult> Index(int sayfa = 1)
    {
        sayfa = Math.Clamp(sayfa, 1, 10_000);

        var vm = await _notifications.GetPanelAsync(CurrentUserId, PageSize, unreadOnly: false, skip: (sayfa - 1) * PageSize);
        vm.Page = sayfa;
        return View(vm);
    }

    // Zil menüsünün içeriği (JavaScript bunu çekip yeniler).
    [HttpGet("panel")]
    public async Task<IActionResult> Panel()
    {
        Response.Headers.CacheControl = "no-store";
        return PartialView("_NotificationList", await _notifications.GetPanelAsync(CurrentUserId, PanelSize, unreadOnly: true));
    }

    // Bildirime tıklayınca: okundu işaretle, ilgili sayfaya git.
    [HttpGet("{id:int}/git")]
    public async Task<IActionResult> Open(int id)
    {
        var userId = CurrentUserId;

        var n = await _context.Notifications
            .AsNoTracking()
            .Where(x => x.Notificationid == id && x.Userid == userId)
            .Select(x => new
            {
                x.Type,
                ActorUsername = x.Actor != null ? x.Actor.Username : null,
                EventName = x.Event != null ? x.Event.Eventname : null,
                EventPublicid = x.Event != null ? x.Event.Publicid : null
            })
            .FirstOrDefaultAsync();

        if (n == null)
            return RedirectToAction(nameof(Index));

        var now = NowForDb();
        await _context.Notifications
            .Where(x => x.Notificationid == id && x.Userid == userId && x.Readat == null)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.Readat, now));

        string? target = n.Type switch
        {
            NotificationTypes.EventJoined or NotificationTypes.EventRecommended when n.EventPublicid != null
                => Url.EventUrl(n.EventName ?? "", n.EventPublicid),
            NotificationTypes.FriendRequest => Url.Action("Index", "Friends"),
            NotificationTypes.FriendAccepted when n.ActorUsername != null
                => Url.Action("Profile", "Users", new { username = n.ActorUsername }),
            _ => null
        };

        return target != null && Url.IsLocalUrl(target)
            ? LocalRedirect(target)
            : RedirectToAction(nameof(Index));
    }

    [HttpPost("tumunu-oku")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> MarkAllRead()
    {
        var userId = CurrentUserId;
        var now = NowForDb();

        await _context.Notifications
            .Where(x => x.Userid == userId && x.Readat == null)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.Readat, now));

        // JavaScript çağırdıysa sadece "tamam" de; normal form gönderimiyse sayfaya dön.
        if (Request.Headers.XRequestedWith == "fetch")
            return NoContent();

        return RedirectToAction(nameof(Index));
    }
}
