using System.Security.Claims;
using EventPlatform.Web.Models;
using EventPlatform.Web.Models.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EventPlatform.Web.ViewComponents;

// Navbar'daki "Arkadaşlar" bağlantısı ve bekleyen istek sayısı rozeti.
public class FriendRequestsViewComponent : ViewComponent
{
    private readonly AppDbContext _context;

    public FriendRequestsViewComponent(AppDbContext context)
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

        var pending = await _context.Friendships
            .AsNoTracking()
            .CountAsync(f => f.Addresseeid == userId && f.Status == FriendshipStatus.Pending);

        return View(pending);
    }
}
