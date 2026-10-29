using System.Security.Claims;
using EventPlatform.Web.Models;
using EventPlatform.Web.Models.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EventPlatform.Web.ViewComponents;

// Navbar'daki "Mesajlar" menüsü: okunmamış mesajı olan etkinlikler ve sayıları.
public class UnreadMessagesViewComponent : ViewComponent
{
    private readonly AppDbContext _context;

    public UnreadMessagesViewComponent(AppDbContext context)
    {
        _context = context;
    }

    public async Task<IViewComponentResult> InvokeAsync()
    {
        var principal = HttpContext.User;
        if (principal.Identity?.IsAuthenticated != true ||
            !int.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier), out var userId))
        {
            return Content(string.Empty);
        }

        // Okunmamış = başkasının yazdığı, sahibi olduğum/katıldığım onaylı etkinlikteki,
        // son okuma zamanımdan sonra gelen mesajlar. Okuma kaydı hiç yoksa hepsi okunmamış sayılır.
        var items = await _context.Messages
            .AsNoTracking()
            .Where(m => m.Senderid != userId
                && m.Event.Isapproved
                && (m.Event.Createdbyuserid == userId || m.Event.Users.Any(u => u.Userid == userId))
                && !_context.Eventreadstatuses.Any(r =>
                    r.Userid == userId && r.Eventid == m.Eventid && r.Lastreadat >= m.Senttime))
            .GroupBy(m => new { m.Eventid, m.Event.Publicid, m.Event.Eventname })
            .Select(g => new UnreadEventItem
            {
                Eventid = g.Key.Eventid,
                Publicid = g.Key.Publicid,
                Eventname = g.Key.Eventname,
                Count = g.Count()
            })
            .OrderByDescending(x => x.Count)
            .ToListAsync();

        return View(items);
    }
}