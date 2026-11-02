using System.Security.Claims;
using EventPlatform.Web.Services;
using Microsoft.AspNetCore.Mvc;

namespace EventPlatform.Web.ViewComponents;

// Navbar'daki zil: okunmamış sayısı rozeti + son bildirimler menüsü.
public class NotificationBellViewComponent : ViewComponent
{
    private readonly NotificationService _notifications;

    public NotificationBellViewComponent(NotificationService notifications)
    {
        _notifications = notifications;
    }

    public async Task<IViewComponentResult> InvokeAsync()
    {
        var principal = HttpContext.User;
        if (principal.Identity?.IsAuthenticated != true ||
            !int.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier), out var userId))
        {
            return Content(string.Empty);
        }

        return View(await _notifications.GetPanelAsync(userId, 8, unreadOnly: true));
    }
}
