using System.Globalization;
using System.Security.Claims;
using EventPlatform.Web.Models;
using EventPlatform.Web.Models.Entities;
using EventPlatform.Web.Models.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EventPlatform.Web.Controllers;

public class EventsController : Controller
{
    // Veritabanındaki interests tablosuyla aynı değerler.
    // TODO: profil/ilgi alanı modülünde tablodan okunacak.
    private static readonly string[] Categories =
    {
        "Music", "Sports", "Art", "Technology", "Travel",
        "Cooking", "Reading", "Gaming", "Fitness", "Photography"
    };

    private const int ChatHistoryLimit = 50;

    private readonly AppDbContext _context;
    private readonly IConfiguration _configuration;

    public EventsController(AppDbContext context, IConfiguration configuration)
    {
        _context = context;
        _configuration = configuration;
    }

    // Giriş yapmamışsa 0 döner (veritabanında 0 id'li kullanıcı yok).
    private int CurrentUserId =>
        int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : 0;

    // Sütun "timestamp without time zone" olduğu için Kind'ı Unspecified yapıyoruz.
    private static DateTime NowForDb() =>
        DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified);

    // ---------- LİSTE ----------

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

    // ---------- DETAY ----------

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
        vm.CurrentUserId = userId;

        // Sohbet geçmişi sadece etkinliğin sahibine ve katılımcılarına gösterilir.
        if (vm.CanChat)
        {
            var latest = await _context.Messages
                .AsNoTracking()
                .Where(m => m.Eventid == id)
                .OrderByDescending(m => m.Senttime)
                .ThenByDescending(m => m.Messageid)
                .Take(ChatHistoryLimit)
                .Select(m => new ChatMessageViewModel
                {
                    Messageid = m.Messageid,
                    SenderId = m.Senderid,
                    SenderUsername = m.Sender.Username,
                    Text = m.Messagetext,
                    SentAt = m.Senttime ?? DateTime.MinValue
                })
                .ToListAsync();

            latest.Reverse(); // ekranda eskiden yeniye
            vm.Messages = latest;
        }

        return View(vm);
    }

    // ---------- OLUŞTUR ----------

    [Authorize]
    [HttpGet]
    public IActionResult Create()
    {
        FillCreateViewData();
        return View(new EventCreateViewModel());
    }

    [Authorize]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(EventCreateViewModel model)
    {
        if (!Categories.Contains(model.Category))
            ModelState.AddModelError(nameof(model.Category), "Geçerli bir kategori seçin.");

        // Koordinatlar: nokta ondalık ayracıyla gelir, kültürden bağımsız ayrıştır.
        double lat = 0, lng = 0;
        var coordsOk =
            double.TryParse(model.Latitude, NumberStyles.Float, CultureInfo.InvariantCulture, out lat) &&
            double.TryParse(model.Longitude, NumberStyles.Float, CultureInfo.InvariantCulture, out lng) &&
            lat is >= -90 and <= 90 &&
            lng is >= -180 and <= 180;

        if (!coordsOk)
            ModelState.AddModelError(nameof(model.Location), "Haritadan bir konum seçin.");

        // Yerel saat -> UTC. getTimezoneOffset() = UTC - yerel (dk), yani UTC = yerel + offset.
        DateTime dateUtc = default;
        if (model.Date.HasValue)
        {
            dateUtc = DateTime.SpecifyKind(
                model.Date.Value.AddMinutes(model.TimezoneOffsetMinutes),
                DateTimeKind.Unspecified);

            if (dateUtc <= NowForDb())
                ModelState.AddModelError(nameof(model.Date), "Etkinlik tarihi gelecekte olmalı.");
        }

        if (!ModelState.IsValid)
        {
            FillCreateViewData();
            return View(model);
        }

        var ev = new Event
        {
            Createdbyuserid = CurrentUserId,
            Eventname = model.Eventname.Trim(),
            Description = model.Description.Trim(),
            Date = dateUtc,
            Duration = model.Duration,
            Location = model.Location!.Trim(),
            Category = model.Category,
            Latitude = lat,
            Longitude = lng,
            Isapproved = false // admin onayından sonra yayınlanır
        };

        _context.Events.Add(ev);
        await _context.SaveChangesAsync();

        TempData["SuccessMessage"] = "Etkinliğiniz oluşturuldu. Admin onayından sonra yayınlanacak.";
        return RedirectToAction(nameof(Index));
    }

    private void FillCreateViewData()
    {
        ViewData["Categories"] = Categories;
        ViewData["MapsApiKey"] = _configuration["GoogleMaps:ApiKey"];
    }

    // ---------- KATIL / AYRIL ----------

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