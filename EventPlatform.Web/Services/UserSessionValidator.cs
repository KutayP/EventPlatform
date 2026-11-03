using System.Security.Claims;
using EventPlatform.Web.Models;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace EventPlatform.Web.Services;

// Cookie'deki kullanıcı hâlâ veritabanında var mı? Yoksa (hesap silindi ya da demo verisi sıfırlandı)
// oturum kapatılır. Sonuç bir dakika önbellekte tutulur; her istekte veritabanına gidilmez.
public static class UserSessionValidator
{
    private static readonly TimeSpan CacheFor = TimeSpan.FromMinutes(1);

    public static async Task ValidateAsync(CookieValidatePrincipalContext context)
    {
        var principal = context.Principal;
        var idText = principal?.FindFirstValue(ClaimTypes.NameIdentifier);
        var username = principal?.Identity?.Name;

        if (!int.TryParse(idText, out var userId) || string.IsNullOrEmpty(username))
        {
            await RejectAsync(context);
            return;
        }

        var services = context.HttpContext.RequestServices;
        var cache = services.GetRequiredService<IMemoryCache>();
        var key = $"session-user:{userId}:{username}";

        if (!cache.TryGetValue(key, out bool exists))
        {
            var db = services.GetRequiredService<AppDbContext>();

            // Numara ve kullanıcı adı birlikte kontrol edilir: numara bir şekilde başka birine geçse bile eşleşmez.
            exists = await db.Users
                .AsNoTracking()
                .AnyAsync(u => u.Userid == userId && u.Username == username);

            cache.Set(key, exists, CacheFor);
        }

        if (!exists)
            await RejectAsync(context);
    }

    private static async Task RejectAsync(CookieValidatePrincipalContext context)
    {
        context.RejectPrincipal();
        await context.HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
    }
}
