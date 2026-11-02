using System.Security.Claims;
using EventPlatform.Web.Models;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EventPlatform.Web.Controllers;

// Kısayol adresleri. Asıl iş başka yerde:
//   /Profile       -> kendi profil sayfam (/u/kullanici)
//   /Profile/Edit  -> ayarlar (/ayarlar)
[Authorize]
public class ProfileController : Controller
{
    private readonly AppDbContext _context;

    public ProfileController(AppDbContext context)
    {
        _context = context;
    }

    private int CurrentUserId =>
        int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : 0;

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var userId = CurrentUserId;
        var username = await _context.Users
            .AsNoTracking()
            .Where(u => u.Userid == userId)
            .Select(u => u.Username)
            .FirstOrDefaultAsync();

        if (username == null)
        {
            // Silinmiş bir kullanıcının cookie'si kalmışsa oturumu kapat.
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return RedirectToAction("Login", "Account");
        }

        return RedirectToAction("Profile", "Users", new { username });
    }

    [HttpGet]
    public IActionResult Edit() => RedirectToAction("Index", "Settings");
}
