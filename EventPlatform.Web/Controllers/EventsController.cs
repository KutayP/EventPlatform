using System.Globalization;
using System.Security.Claims;
using EventPlatform.Web.Helpers;
using EventPlatform.Web.Models;
using EventPlatform.Web.Models.Entities;
using EventPlatform.Web.Models.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EventPlatform.Web.Controllers;

public class EventsController : Controller
{
    private const int ChatHistoryLimit = 50;
    private const int MaxSearchLength = 100;

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

    // Kategoriler = ilgi alanları tablosu (kayıt ve profildeki seçimle aynı kaynak).
    private Task<List<string>> GetCategoriesAsync() =>
        _context.Interests
            .AsNoTracking()
            .OrderBy(i => i.Interestname)
            .Select(i => i.Interestname)
            .ToListAsync();

    // LIKE desenindeki özel karakterleri (% _ \) kaçırır: kullanıcı girdisi desen olarak yorumlanmaz.
    private static string EscapeLike(string value) =>
        value.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_");

    // NOT: Event.Isapproved sütunu artık "yayında mı" anlamına gelir.
    // Etkinlikler oluşturulunca hemen yayınlanır; admin sonradan yayından kaldırabilir.

    // ---------- LİSTE ----------

    public async Task<IActionResult> Index(string? q, string? category)
    {
        var now = NowForDb();

        q = q?.Trim();
        if (q is { Length: > MaxSearchLength })
            q = q[..MaxSearchLength];

        var query = _context.Events
            .AsNoTracking()
            .Where(e => e.Isapproved && e.Date >= now);

        if (!string.IsNullOrEmpty(q))
        {
            var pattern = "%" + EscapeLike(q) + "%";
            query = query.Where(e =>
                EF.Functions.ILike(e.Eventname, pattern) ||
                EF.Functions.ILike(e.Location, pattern));
        }

        if (!string.IsNullOrEmpty(category))
            query = query.Where(e => e.Category == category);

        var events = await query
            .OrderBy(e => e.Date)
            .Select(e => new EventListItemViewModel
            {
                Eventid = e.Eventid,
                Publicid = e.Publicid,
                Eventname = e.Eventname,
                Date = e.Date,
                Location = e.Location,
                Category = e.Category,
                ParticipantCount = e.Users.Count(u => u.Userid != e.Createdbyuserid) + 1
            })
            .ToListAsync();

        return View(new EventIndexViewModel
        {
            Events = events,
            Query = q,
            Category = category,
            Categories = await GetCategoriesAsync()
        });
    }

    // ---------- BENİM ETKİNLİKLERİM ----------

    [Authorize]
    public async Task<IActionResult> My()
    {
        var userId = CurrentUserId;
        var now = NowForDb();

        var created = await _context.Events
            .AsNoTracking()
            .Where(e => e.Createdbyuserid == userId)
            .OrderByDescending(e => e.Date)
            .Select(e => new MyEventItemViewModel
            {
                Eventid = e.Eventid,
                Publicid = e.Publicid,
                Eventname = e.Eventname,
                Date = e.Date,
                Location = e.Location,
                Isapproved = e.Isapproved,
                ParticipantCount = e.Users.Count(u => u.Userid != e.Createdbyuserid) + 1
            })
            .ToListAsync();

        var joined = await _context.Events
            .AsNoTracking()
            .Where(e => e.Isapproved && e.Users.Any(u => u.Userid == userId))
            .OrderBy(e => e.Date)
            .Select(e => new MyEventItemViewModel
            {
                Eventid = e.Eventid,
                Publicid = e.Publicid,
                Eventname = e.Eventname,
                Date = e.Date,
                Location = e.Location,
                Isapproved = e.Isapproved,
                ParticipantCount = e.Users.Count(u => u.Userid != e.Createdbyuserid) + 1
            })
            .ToListAsync();

        foreach (var e in created) e.IsPast = e.Date < now;
        foreach (var e in joined) e.IsPast = e.Date < now;

        return View(new MyEventsViewModel { Created = created, Joined = joined });
    }

    // ---------- DETAY ----------

    // Kullanıcının görebileceği etkinlikler: yayında olanlar + (oluşturan, katılımcı, admin için) yayında olmayanlar.
    private IQueryable<Event> VisibleEvents(int userId, bool isAdmin) =>
        _context.Events.Where(e =>
            e.Isapproved || e.Createdbyuserid == userId || isAdmin ||
            e.Users.Any(u => u.Userid == userId));

    // Adres: /etkinlik/kadikoy-sahil-fotograf-yuruyusu-k7x2m9ab
    // Etkinliği bulan kısım sondaki kısa koddur; baştaki yazı (slug) yalnızca okunabilirlik içindir.
    // Slug eskiyse ya da eksikse (ad değişmiş olabilir) güncel adrese yönlendirilir.
    [HttpGet("etkinlik/{slugId}")]
    public async Task<IActionResult> Details(string slugId)
    {
        var (slug, publicId) = SlugHelper.Split(slugId);
        if (publicId == null)
            return NotFound();

        var userId = CurrentUserId;
        var isAdmin = User.IsInRole("Admin");

        // Yayından kaldırılmış etkinliği yalnızca oluşturan, katılımcılar ve admin görebilir.
        var vm = await VisibleEvents(userId, isAdmin)
            .AsNoTracking()
            .Where(e => e.Publicid == publicId)
            .Select(e => new EventDetailsViewModel
            {
                Eventid = e.Eventid,
                Publicid = e.Publicid,
                Eventname = e.Eventname,
                Description = e.Description,
                Date = e.Date,
                Duration = e.Duration,
                Location = e.Location,
                Category = e.Category,
                Latitude = e.Latitude,
                Longitude = e.Longitude,
                CreatorUsername = e.Createdbyuser.Username,
                ParticipantCount = e.Users.Count(u => u.Userid != e.Createdbyuserid) + 1,
                IsApproved = e.Isapproved,
                IsCreator = e.Createdbyuserid == userId,
                IsParticipant = e.Users.Any(u => u.Userid == userId)
            })
            .FirstOrDefaultAsync();

        if (vm == null)
            return NotFound();

        if (slug != SlugHelper.Slugify(vm.Eventname))
        {
            return RedirectToAction(nameof(Details),
                new { slugId = SlugHelper.Compose(vm.Eventname, vm.Publicid) });
        }

        vm.IsAdmin = isAdmin;
        vm.IsPast = vm.Date < NowForDb();
        vm.CurrentUserId = userId;

        // Sohbet geçmişi sadece etkinliğin sahibine ve katılımcılarına gösterilir.
        if (vm.CanChat)
        {
            var latest = await _context.Messages
                .AsNoTracking()
                .Where(m => m.Eventid == vm.Eventid)
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

            // Sohbet ekrana geldi: bu etkinlik için okundu işaretle (navbar rozeti buna bakar).
            await MarkReadAsync(vm.Eventid, userId);
        }

        return View(vm);
    }

    // (userid, eventid) için "en son ne zaman okudu" değerini yazar (yoksa ekler).
    private async Task MarkReadAsync(int eventId, int userId)
    {
        var now = NowForDb();

        var updated = await _context.Eventreadstatuses
            .Where(r => r.Userid == userId && r.Eventid == eventId)
            .ExecuteUpdateAsync(s => s.SetProperty(r => r.Lastreadat, now));

        if (updated == 0)
        {
            _context.Eventreadstatuses.Add(new Eventreadstatus
            {
                Userid = userId,
                Eventid = eventId,
                Lastreadat = now
            });

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateException)
            {
                // Aynı anda başka bir istek satırı eklediyse sorun değil.
            }
        }
    }

    // ---------- OLUŞTUR ----------

    [Authorize]
    [HttpGet]
    public async Task<IActionResult> Create()
    {
        await FillFormViewDataAsync();
        return View(new EventCreateViewModel());
    }

    [Authorize]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(EventCreateViewModel model)
    {
        var categories = await GetCategoriesAsync();
        var (lat, lng, dateUtc) = ValidateEventForm(model, categories);

        if (!ModelState.IsValid)
        {
            await FillFormViewDataAsync(categories);
            return View(model);
        }

        Event BuildEvent() => new Event
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
            Isapproved = true // hemen yayınlanır; uygunsuz içeriği admin sonradan kaldırır
        };

        // Kısa kod rastgele üretilir; veritabanında benzersiz olduğu için çok nadir çakışırsa yeni kodla tekrar denenir.
        for (var attempt = 0; ; attempt++)
        {
            var ev = BuildEvent();
            ev.Publicid = PublicIdGenerator.New();
            _context.Events.Add(ev);

            try
            {
                await _context.SaveChangesAsync();

                TempData["SuccessMessage"] = "Etkinliğiniz oluşturuldu ve yayınlandı.";
                return RedirectToAction(nameof(Details),
                    new { slugId = SlugHelper.Compose(ev.Eventname, ev.Publicid) });
            }
            catch (DbUpdateException ex) when (attempt < 4 && IsPublicIdCollision(ex))
            {
                _context.Entry(ev).State = EntityState.Detached;
            }
        }
    }

    // ---------- DÜZENLE ----------

    [Authorize]
    [HttpGet("etkinlik/{publicId:length(8)}/duzenle")]
    public async Task<IActionResult> Edit(string publicId)
    {
        // Yalnızca oluşturan kişi düzenleyebilir.
        var ev = await _context.Events
            .AsNoTracking()
            .FirstOrDefaultAsync(e => e.Publicid == publicId && e.Createdbyuserid == CurrentUserId);

        if (ev == null)
            return NotFound();

        if (ev.Date < NowForDb())
        {
            TempData["ErrorMessage"] = "Sona ermiş bir etkinlik düzenlenemez.";
            return RedirectToAction(nameof(My));
        }

        var model = new EventEditViewModel
        {
            Publicid = ev.Publicid,
            Eventname = ev.Eventname,
            Description = ev.Description,
            Date = ev.Date, // UTC; tarayıcı yerel saate çevirir
            OriginalDateUtc = ev.Date.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'"),
            Duration = ev.Duration,
            Category = ev.Category,
            Location = ev.Location,
            Latitude = FormattableString.Invariant($"{ev.Latitude:F6}"),
            Longitude = FormattableString.Invariant($"{ev.Longitude:F6}")
        };

        await FillFormViewDataAsync();
        return View(model);
    }

    [Authorize]
    [HttpPost("etkinlik/{publicId:length(8)}/duzenle")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(string publicId, EventEditViewModel model)
    {
        var categories = await GetCategoriesAsync();
        var (lat, lng, dateUtc) = ValidateEventForm(model, categories);

        if (!ModelState.IsValid)
        {
            model.Publicid = publicId;
            model.OriginalDateUtc = null;
            await FillFormViewDataAsync(categories);
            return View(model);
        }

        var ev = await _context.Events
            .FirstOrDefaultAsync(e => e.Publicid == publicId && e.Createdbyuserid == CurrentUserId);

        if (ev == null)
            return NotFound();

        if (ev.Date < NowForDb())
        {
            TempData["ErrorMessage"] = "Sona ermiş bir etkinlik düzenlenemez.";
            return RedirectToAction(nameof(My));
        }

        ev.Eventname = model.Eventname.Trim();
        ev.Description = model.Description.Trim();
        ev.Date = dateUtc;
        ev.Duration = model.Duration;
        ev.Location = model.Location!.Trim();
        ev.Category = model.Category;
        ev.Latitude = lat;
        ev.Longitude = lng;

        await _context.SaveChangesAsync();

        TempData["SuccessMessage"] = "Etkinlik güncellendi.";
        return RedirectToAction(nameof(Details),
            new { slugId = SlugHelper.Compose(ev.Eventname, ev.Publicid) });
    }

    // ---------- SİL ----------

    [Authorize]
    [HttpPost("etkinlik/{publicId:length(8)}/sil")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(string publicId)
    {
        var userId = CurrentUserId;
        var isAdmin = User.IsInRole("Admin");

        try
        {
            // Oluşturan ya da admin silebilir. Mesajlar, katılımcılar ve okuma kayıtları
            // veritabanındaki cascade kuralıyla birlikte silinir.
            var deleted = await _context.Events
                .Where(e => e.Publicid == publicId && (e.Createdbyuserid == userId || isAdmin))
                .ExecuteDeleteAsync();

            if (deleted == 0)
                return NotFound();
        }
        catch (Exception ex) when (ex is DbUpdateException or Npgsql.PostgresException)
        {
            TempData["ErrorMessage"] = "Etkinlik silinemedi. Lütfen daha sonra tekrar deneyin.";
            // Slug'sız adres de çalışır: Details güncel adrese yönlendirir.
            return RedirectToAction(nameof(Details), new { slugId = publicId });
        }

        TempData["SuccessMessage"] = "Etkinlik silindi.";
        return RedirectToAction(isAdmin ? nameof(Index) : nameof(My));
    }

    // ---------- ORTAK YARDIMCILAR ----------

    // Oluşturma ve düzenleme formunun ortak doğrulaması. Hataları ModelState'e ekler.
    private (double Lat, double Lng, DateTime DateUtc) ValidateEventForm(
        EventCreateViewModel model, List<string> categories)
    {
        if (!categories.Contains(model.Category))
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

        return (lat, lng, dateUtc);
    }

    // events.publicid benzersiz kısıtı (uq_events_publicid) ihlal edildi mi?
    private static bool IsPublicIdCollision(DbUpdateException ex) =>
        ex.InnerException is Npgsql.PostgresException { SqlState: "23505", ConstraintName: "uq_events_publicid" };

    private async Task FillFormViewDataAsync(List<string>? categories = null)
    {
        ViewData["Categories"] = categories ?? await GetCategoriesAsync();
        ViewData["MapsApiKey"] = _configuration["GoogleMaps:ApiKey"];
    }

    // ---------- KATIL / AYRIL ----------

    [Authorize]
    [HttpPost("etkinlik/{publicId:length(8)}/katil")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Join(string publicId)
    {
        var userId = CurrentUserId;

        // Etkinliği, sadece bu kullanıcının katılımcı kaydıyla birlikte yükle.
        var ev = await _context.Events
            .Include(e => e.Users.Where(u => u.Userid == userId))
            .FirstOrDefaultAsync(e => e.Publicid == publicId && e.Isapproved);

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

        return RedirectToAction(nameof(Details),
            new { slugId = SlugHelper.Compose(ev.Eventname, ev.Publicid) });
    }

    [Authorize]
    [HttpPost("etkinlik/{publicId:length(8)}/ayril")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Leave(string publicId)
    {
        var userId = CurrentUserId;

        var ev = await _context.Events
            .Include(e => e.Users.Where(u => u.Userid == userId))
            .FirstOrDefaultAsync(e => e.Publicid == publicId && e.Isapproved);

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

        return RedirectToAction(nameof(Details),
            new { slugId = SlugHelper.Compose(ev.Eventname, ev.Publicid) });
    }
}
