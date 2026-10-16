using System.Security.Claims;
using EventPlatform.Web.Models;
using EventPlatform.Web.Models.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EventPlatform.Web.Controllers;

public class EventsController : Controller
{
    private readonly AppDbContext _context;

    public EventsController(AppDbContext context)
    {
        _context = context;
    }

    // Giriş yapmamışsa 0 döner (veritabanında 0 id'li kullanıcı yok).
    private int CurrentUserId =>
        int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : 0;

    // Sütun "timestamp without time zone" olduğu için Kind'ı Unspecified yapıyoruz.
    private static DateTime NowForDb() =>
        DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified);

    public async Task<IActionResult> Index()
    {
        var now = NowForDb();

        var events = await _context.Events
            .AsNoTracking()
            .Where(e => e.Isapproved && e.Date >= now)
            .OrderBy(e => e.Date)
            .Select(e => new EventListItemViewModel
            {
                Eventid = e.Eventid,
                Eventname = e.Eventname,
                Date = e.Date,
                Location = e.Location,
                Category = e.Category
            })
            .ToListAsync();

        return View(events);
    }

    public async Task<IActionResult> Details(int id)
    {
        var userId = CurrentUserId;

        var vm = await _context.Events
            .AsNoTracking()
            .Where(e => e.Eventid == id && e.Isapproved)
            .Select(e => new EventDetailsViewModel
            {
                Eventid = e.Eventid,
                Eventname = e.Eventname,
                Description = e.Description,
                Date = e.Date,
                Duration = e.Duration,
                Location = e.Location,
                Category = e.Category,
                CreatorUsername = e.Createdbyuser.Username,
                ParticipantCount = e.Users.Count,
                IsCreator = e.Createdbyuserid == userId,
                IsParticipant = e.Users.Any(u => u.Userid == userId)
            })
            .FirstOrDefaultAsync();

        if (vm == null)
            return NotFound();

        vm.IsPast = vm.Date < NowForDb();
        return View(vm);
    }

    [Authorize]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Join(int id)
    {
        var userId = CurrentUserId;

        // Etkinliği, sadece bu kullanıcının katılımcı kaydıyla birlikte yükle.
        var ev = await _context.Events
            .Include(e => e.Users.Where(u => u.Userid == userId))
            .FirstOrDefaultAsync(e => e.Eventid == id && e.Isapproved);

        if (ev == null)
            return NotFound();

        if (ev.Date < NowForDb())
        {
            TempData["ErrorMessage"] = "Bu etkinlik sona erdi.";
        }
        else if (ev.Createdbyuserid == userId || ev.Users.Count > 0)
        {
            TempData["ErrorMessage"] = "Bu etkinliğe zaten katılıyorsunuz.";
        }
        else
        {
            var user = await _context.Users.FindAsync(userId);
            if (user == null)
                return Forbid();

            ev.Users.Add(user);
            await _context.SaveChangesAsync();
            TempData["SuccessMessage"] = "Etkinliğe katıldınız.";
        }

        return RedirectToAction(nameof(Details), new { id });
    }

    [Authorize]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Leave(int id)
    {
        var userId = CurrentUserId;

        var ev = await _context.Events
            .Include(e => e.Users.Where(u => u.Userid == userId))
            .FirstOrDefaultAsync(e => e.Eventid == id && e.Isapproved);

        if (ev == null)
            return NotFound();

        var me = ev.Users.FirstOrDefault();
        if (me == null)
        {
            TempData["ErrorMessage"] = "Bu etkinliğe katılmıyorsunuz.";
        }
        else
        {
            ev.Users.Remove(me);
            await _context.SaveChangesAsync();
            TempData["SuccessMessage"] = "Etkinlikten ayrıldınız.";
        }

        return RedirectToAction(nameof(Details), new { id });
    }
}