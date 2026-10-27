using System.Security.Claims;
using EventPlatform.Web.Models;
using EventPlatform.Web.Models.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EventPlatform.Web.Controllers;

// Sınıf seviyesinde: bu controller'a eklenecek her action otomatik olarak sadece Admin'e açık.
[Authorize(Roles = "Admin")]
public class AdminController : Controller
{
    private readonly AppDbContext _context;

    public AdminController(AppDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<IActionResult> AdminPanel(string? tab = null)
    {
        ViewData["ActiveTab"] = tab == "events" ? "events" : "users";

        var model = new AdminPanelViewModel
        {
            Users = await _context.Users
                .AsNoTracking()
                .OrderBy(u => u.Username)
                .Select(u => new AdminUserRow
                {
                    Userid = u.Userid,
                    Username = u.Username,
                    Email = u.Email,
                    Role = u.Role
                })
                .ToListAsync(),

            Events = await _context.Events
                .AsNoTracking()
                .OrderByDescending(e => e.Date)
                .Select(e => new AdminEventRow
                {
                    Eventid = e.Eventid,
                    Eventname = e.Eventname,
                    Description = e.Description,
                    Date = e.Date,
                    Isapproved = e.Isapproved, // "yayında mı"
                    CreatorUsername = e.Createdbyuser.Username
                })
                .ToListAsync()
        };

        return View(model);
    }

    // Etkinlikler hemen yayınlanır; admin uygunsuz olanı yayından kaldırır ya da geri yayınlar.
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SetEventVisibility(int id, bool visible)
    {
        var ev = await _context.Events.FindAsync(id);

        if (ev == null)
        {
            TempData["ErrorMessage"] = "Etkinlik bulunamadı.";
        }
        else if (ev.Isapproved == visible)
        {
            TempData["ErrorMessage"] = visible ? "Etkinlik zaten yayında." : "Etkinlik zaten yayında değil.";
        }
        else
        {
            ev.Isapproved = visible;
            await _context.SaveChangesAsync();
            TempData["SuccessMessage"] = visible
                ? $"\"{ev.Eventname}\" yeniden yayına alındı."
                : $"\"{ev.Eventname}\" yayından kaldırıldı.";
        }

        return RedirectToAction(nameof(AdminPanel), new { tab = "events" });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteUser(int id)
    {
        // Admin kendi hesabını silip panelden kilitlenmesin.
        if (User.FindFirstValue(ClaimTypes.NameIdentifier) == id.ToString())
        {
            TempData["ErrorMessage"] = "Kendi hesabınızı silemezsiniz.";
            return RedirectToAction(nameof(AdminPanel));
        }

        if (await _context.Events.AnyAsync(e => e.Createdbyuserid == id))
        {
            TempData["ErrorMessage"] = "Bu kullanıcının oluşturduğu etkinlikler var. Önce etkinlikleri silin.";
            return RedirectToAction(nameof(AdminPanel));
        }

        try
        {
            // Bağlı kayıtları (puan, mesaj, katılım...) veritabanı ON DELETE CASCADE ile siliyor.
            int deleted = await _context.Users.Where(u => u.Userid == id).ExecuteDeleteAsync();

            if (deleted > 0)
                TempData["SuccessMessage"] = "Kullanıcı silindi.";
            else
                TempData["ErrorMessage"] = "Kullanıcı bulunamadı.";
        }
        catch (DbUpdateException)
        {
            TempData["ErrorMessage"] = "Kullanıcı silinemedi: bağlı kayıtlar var.";
        }

        return RedirectToAction(nameof(AdminPanel));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteEvent(int id)
    {
        try
        {
            int deleted = await _context.Events.Where(e => e.Eventid == id).ExecuteDeleteAsync();

            if (deleted > 0)
                TempData["SuccessMessage"] = "Etkinlik silindi.";
            else
                TempData["ErrorMessage"] = "Etkinlik bulunamadı.";
        }
        catch (DbUpdateException)
        {
            TempData["ErrorMessage"] = "Etkinlik silinemedi: bağlı kayıtlar var.";
        }

        return RedirectToAction(nameof(AdminPanel), new { tab = "events" });
    }
}