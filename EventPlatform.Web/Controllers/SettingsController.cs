using System.Globalization;
using System.Security.Claims;
using EventPlatform.Web.Helpers;
using EventPlatform.Web.Models;
using EventPlatform.Web.Models.Entities;
using EventPlatform.Web.Models.ViewModels;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EventPlatform.Web.Controllers;

// Ayarlar: /settings/profile, /interests, /privacy, /notifications, /location, /appearance, /security
[Authorize]
[Route("settings")]
public class SettingsController : Controller
{
    private readonly AppDbContext _context;
    private readonly IConfiguration _configuration;

    public SettingsController(AppDbContext context, IConfiguration configuration)
    {
        _context = context;
        _configuration = configuration;
    }

    // Kimlik her zaman cookie'den gelir; başka birinin ayarını değiştirmek mümkün değil.
    private int CurrentUserId =>
        int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : 0;

    private static DateTime NowForDb() =>
        DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified);

    // Silinmiş bir kullanıcının cookie'si kalmışsa oturumu kapatıp girişe yönlendirir.
    private async Task<IActionResult> SignOutAsync()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return RedirectToAction("Login", "Account");
    }

    [HttpGet("")]
    public IActionResult Index() => RedirectToAction(nameof(ProfileInfo));

    // ---------- PROFİL BİLGİLERİ ----------

    [HttpGet("profile")]
    public async Task<IActionResult> ProfileInfo()
    {
        var vm = await _context.Users
            .AsNoTracking()
            .Where(u => u.Userid == CurrentUserId)
            .Select(u => new SettingsProfileViewModel
            {
                Username = u.Username,
                Email = u.Email,
                Firstname = u.Firstname,
                Lastname = u.Lastname,
                Birthdate = u.Birthdate,
                Gender = u.Gender
            })
            .FirstOrDefaultAsync();

        return vm == null ? await SignOutAsync() : View(vm);
    }

    [HttpPost("profile")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ProfileInfo(SettingsProfileViewModel input)
    {
        if (DemoAccount.IsDemo(User))
        {
            TempData["ErrorMessage"] = DemoAccount.BlockedMessage;
            return RedirectToAction(nameof(ProfileInfo));
        }

        var userId = CurrentUserId;

        if (input.Birthdate.Date > DateTime.Today || input.Birthdate.Year < 1900)
            ModelState.AddModelError(nameof(input.Birthdate), "Geçerli bir doğum tarihi girin.");

        if (!ModelState.IsValid)
        {
            // Kullanıcı adı ve e-posta formdan alınmaz; sayfa yeniden çizilirken veritabanından doldurulur.
            var fixedFields = await _context.Users
                .AsNoTracking()
                .Where(u => u.Userid == userId)
                .Select(u => new { u.Username, u.Email })
                .FirstOrDefaultAsync();

            if (fixedFields == null)
                return await SignOutAsync();

            input.Username = fixedFields.Username;
            input.Email = fixedFields.Email;
            return View(input);
        }

        var user = await _context.Users.FindAsync(userId);
        if (user == null)
            return await SignOutAsync();

        user.Firstname = input.Firstname.Trim();
        user.Lastname = input.Lastname.Trim();
        user.Birthdate = input.Birthdate;
        user.Gender = input.Gender;

        await _context.SaveChangesAsync();

        TempData["SuccessMessage"] = "Bilgilerin güncellendi.";
        return RedirectToAction(nameof(ProfileInfo));
    }

    // ---------- İLGİ ALANLARI ----------

    private async Task<InterestsSettingsViewModel> BuildInterestsAsync(int userId, List<int>? selectedIds = null)
    {
        selectedIds ??= await _context.Userinterests
            .AsNoTracking()
            .Where(ui => ui.Userid == userId)
            .Select(ui => ui.Interestid)
            .ToListAsync();

        var interests = await _context.Interests
            .AsNoTracking()
            .OrderBy(i => i.Interestname)
            .Select(i => new InterestOption
            {
                Interestid = i.Interestid,
                Interestname = i.Interestname,
                Selected = selectedIds.Contains(i.Interestid)
            })
            .ToListAsync();

        return new InterestsSettingsViewModel { Interests = interests };
    }

    [HttpGet("interests")]
    public async Task<IActionResult> Interests()
    {
        return View(await BuildInterestsAsync(CurrentUserId));
    }

    [HttpPost("interests")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Interests(List<int>? selectedInterestIds)
    {
        var userId = CurrentUserId;
        var requested = (selectedInterestIds ?? new List<int>()).Distinct().ToList();

        // Sadece tabloda gerçekten var olan ilgi alanları kabul edilir.
        var validIds = await _context.Interests
            .AsNoTracking()
            .Select(i => i.Interestid)
            .ToListAsync();

        if (requested.Any(id => !validIds.Contains(id)))
        {
            ModelState.AddModelError(string.Empty, "Geçersiz ilgi alanı seçildi.");
            return View(await BuildInterestsAsync(userId, requested.Where(validIds.Contains).ToList()));
        }

        var current = await _context.Userinterests
            .Where(ui => ui.Userid == userId)
            .ToListAsync();

        // Seçimden çıkarılanları sil, yeni seçilenleri ekle.
        _context.Userinterests.RemoveRange(current.Where(ui => !requested.Contains(ui.Interestid)));

        var alreadyHave = current.Select(ui => ui.Interestid).ToHashSet();
        foreach (var interestId in requested.Where(id => !alreadyHave.Contains(id)))
        {
            _context.Userinterests.Add(new Userinterest
            {
                Userid = userId,
                Interestid = interestId,
                Time = NowForDb()
            });
        }

        await _context.SaveChangesAsync();

        TempData["SuccessMessage"] = "İlgi alanların güncellendi.";
        return RedirectToAction(nameof(Interests));
    }

    // ---------- GİZLİLİK ----------

    [HttpGet("privacy")]
    public async Task<IActionResult> Privacy()
    {
        var vm = await _context.Users
            .AsNoTracking()
            .Where(u => u.Userid == CurrentUserId)
            .Select(u => new PrivacySettingsViewModel
            {
                Username = u.Username,
                ProfileVisibility = u.Profilevisibility
            })
            .FirstOrDefaultAsync();

        return vm == null ? await SignOutAsync() : View(vm);
    }

    [HttpPost("privacy")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Privacy(string? visibility)
    {
        // Yalnızca bilinen değerler kabul edilir (formu elle değiştirip başka değer göndermek işe yaramaz).
        if (!ProfileVisibilities.IsValid(visibility))
        {
            TempData["ErrorMessage"] = "Geçersiz görünürlük seçeneği.";
            return RedirectToAction(nameof(Privacy));
        }

        var userId = CurrentUserId;
        var updated = await _context.Users
            .Where(u => u.Userid == userId)
            .ExecuteUpdateAsync(s => s.SetProperty(u => u.Profilevisibility, visibility));

        if (updated == 0)
            return await SignOutAsync();

        TempData["SuccessMessage"] = visibility == ProfileVisibilities.Public
            ? "Profilin artık herkese açık."
            : "Profilin artık yalnızca arkadaşlarına açık.";
        return RedirectToAction(nameof(Privacy));
    }

    // Ayar satırı yoksa oluşturur, varsa günceller. İki istek aynı anda satır eklemeye çalışırsa yeniden dener.
    private async Task SaveSettingsAsync(int userId, Action<Usersetting> apply)
    {
        for (var attempt = 0; ; attempt++)
        {
            var settings = await _context.Usersettings.FirstOrDefaultAsync(x => x.Userid == userId);
            if (settings == null)
            {
                settings = new Usersetting { Userid = userId };
                _context.Usersettings.Add(settings);
            }

            apply(settings);

            try
            {
                await _context.SaveChangesAsync();
                return;
            }
            catch (DbUpdateException ex) when (attempt < 2 && ex.InnerException is Npgsql.PostgresException { SqlState: "23505" })
            {
                _context.ChangeTracker.Clear();
            }
        }
    }

    // ---------- BİLDİRİMLER ----------

    [HttpGet("notifications")]
    public async Task<IActionResult> Notifications()
    {
        var userId = CurrentUserId;

        var s = await _context.Usersettings
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Userid == userId);

        var hasLocation = await _context.Users
            .AsNoTracking()
            .AnyAsync(u => u.Userid == userId && u.Latitude != null && u.Longitude != null);

        return View(new NotificationSettingsViewModel
        {
            NotifyEventJoined = s?.NotifyEventJoined ?? true,
            NotifyFriends = s?.NotifyFriends ?? true,
            NotifyRecommended = s?.NotifyRecommended ?? false,
            HasLocation = hasLocation
        });
    }

    [HttpPost("notifications")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Notifications(NotificationSettingsViewModel input)
    {
        var userId = CurrentUserId;

        if (!await _context.Users.AnyAsync(u => u.Userid == userId))
            return await SignOutAsync();

        await SaveSettingsAsync(userId, s =>
        {
            s.NotifyEventJoined = input.NotifyEventJoined;
            s.NotifyFriends = input.NotifyFriends;
            s.NotifyRecommended = input.NotifyRecommended;
        });

        TempData["SuccessMessage"] = "Bildirim ayarların güncellendi.";
        return RedirectToAction(nameof(Notifications));
    }

    // ---------- KONUM ----------

    private static readonly int[] RadiusOptions = { 10, 25, 50, 100, 250 };
    private const int MaxLocationLabelLength = 100;
    private const int NearbyShown = 3;

    // Konum yaklaşık 1 km hassasiyetinde saklanır (2 ondalık) ve başkalarına hiçbir yerde gösterilmez.
    [HttpGet("location")]
    public async Task<IActionResult> Location()
    {
        var userId = CurrentUserId;

        var u = await _context.Users
            .AsNoTracking()
            .Where(x => x.Userid == userId)
            .Select(x => new { x.Location, x.Latitude, x.Longitude })
            .FirstOrDefaultAsync();

        if (u == null)
            return await SignOutAsync();

        var radius = await _context.Usersettings
            .AsNoTracking()
            .Where(x => x.Userid == userId)
            .Select(x => (int?)x.RadiusKm)
            .FirstOrDefaultAsync() ?? 50;

        var vm = new LocationSettingsViewModel
        {
            Label = u.Location,
            Latitude = u.Latitude,
            Longitude = u.Longitude,
            RadiusKm = RadiusOptions.Contains(radius) ? radius : 50,
            MapsApiKey = _configuration["GoogleMaps:ApiKey"]
        };

        if (vm.HasLocation)
            vm.Nearby = await GetNearbyAsync(vm.Latitude!.Value, vm.Longitude!.Value, vm.RadiusKm);

        return View(vm);
    }

    [HttpPost("location")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Location(string? latitude, string? longitude, string? label, int radiusKm)
    {
        if (!RadiusOptions.Contains(radiusKm))
        {
            TempData["ErrorMessage"] = "Geçersiz mesafe seçildi.";
            return RedirectToAction(nameof(Location));
        }

        // Koordinatlar tarayıcıdan "40.765400" biçiminde (nokta ile) gelir. Sunucu Türkçe kültürde çalışınca
        // nokta binlik ayracı sayılır; bu yüzden kültürden bağımsız (InvariantCulture) okunur.
        if (!double.TryParse(latitude, NumberStyles.Float, CultureInfo.InvariantCulture, out var lat) ||
            !double.TryParse(longitude, NumberStyles.Float, CultureInfo.InvariantCulture, out var lng) ||
            double.IsNaN(lat) || double.IsNaN(lng) ||
            lat is < -90 or > 90 || lng is < -180 or > 180)
        {
            TempData["ErrorMessage"] = "Arama kutusundan ya da haritadan bir konum seç.";
            return RedirectToAction(nameof(Location));
        }

        lat = Math.Round(lat, 2);
        lng = Math.Round(lng, 2);

        label = (label ?? "").Trim();
        if (label.Length == 0)
            label = "Seçilen konum";
        if (label.Length > MaxLocationLabelLength)
            label = label[..MaxLocationLabelLength];

        var userId = CurrentUserId;
        var updated = await _context.Users
            .Where(u => u.Userid == userId)
            .ExecuteUpdateAsync(s => s
                .SetProperty(u => u.Location, label)
                .SetProperty(u => u.Latitude, (double?)lat)
                .SetProperty(u => u.Longitude, (double?)lng));

        if (updated == 0)
            return await SignOutAsync();

        await SaveSettingsAsync(userId, s => s.RadiusKm = radiusKm);

        TempData["SuccessMessage"] = "Konum ayarın kaydedildi.";
        return RedirectToAction(nameof(Location));
    }

    [HttpPost("location/delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteLocation()
    {
        var userId = CurrentUserId;

        await _context.Users
            .Where(u => u.Userid == userId)
            .ExecuteUpdateAsync(s => s
                .SetProperty(u => u.Location, (string?)null)
                .SetProperty(u => u.Latitude, (double?)null)
                .SetProperty(u => u.Longitude, (double?)null));

        TempData["SuccessMessage"] = "Konumun silindi. Konuma göre öneri bildirimi almayacaksın.";
        return RedirectToAction(nameof(Location));
    }

    // Seçili konumun çevresindeki yaklaşan etkinlikler: önce kaba bir kutuyla veritabanında daralt,
    // sonra gerçek mesafeyi hesaplayıp en yakın birkaçını al.
    private async Task<List<EventListItemViewModel>> GetNearbyAsync(double lat, double lng, int radiusKm)
    {
        var now = NowForDb();
        var dLat = radiusKm / 111.0;
        var dLng = radiusKm / (111.0 * Math.Max(0.1, Math.Cos(lat * Math.PI / 180.0)));

        var candidates = await _context.Events
            .AsNoTracking()
            .Where(e => e.Isapproved && e.Date >= now &&
                        e.Latitude >= lat - dLat && e.Latitude <= lat + dLat &&
                        e.Longitude >= lng - dLng && e.Longitude <= lng + dLng)
            .OrderBy(e => e.Date)
            .Take(60)
            .Select(e => new
            {
                e.Eventid,
                e.Publicid,
                e.Eventname,
                e.Date,
                e.Location,
                e.Category,
                e.Latitude,
                e.Longitude,
                ParticipantCount = e.Users.Count(u => u.Userid != e.Createdbyuserid) + 1
            })
            .ToListAsync();

        var tr = CultureInfo.GetCultureInfo("tr-TR");

        return candidates
            .Select(e => new { Event = e, Km = GeoHelper.DistanceKm(lat, lng, e.Latitude, e.Longitude) })
            .Where(x => x.Km <= radiusKm)
            .OrderBy(x => x.Km)
            .Take(NearbyShown)
            .Select(x => new EventListItemViewModel
            {
                Eventid = x.Event.Eventid,
                Publicid = x.Event.Publicid,
                Eventname = x.Event.Eventname,
                Date = x.Event.Date,
                Location = x.Event.Location,
                Category = x.Event.Category,
                ParticipantCount = x.Event.ParticipantCount,
                Note = x.Km < 1 ? "1 km'den yakın" : $"{x.Km.ToString("0.#", tr)} km uzakta"
            })
            .ToList();
    }

    // ---------- GÖRÜNÜM (TEMA) ----------

    // Tema ve palet bu cihaza özeldir (çerez); hesap bilgisi değildir.
    private const string ThemeCookie = "ep-theme";
    private const string PaletteCookie = "ep-palette";
    private const string LightCookie = "ep-light";

    private static readonly string[] Palettes = { "default", "slate", "soft", "graphite" };
    private static readonly string[] LightPalettes = { "default", "cream", "mint", "sky" };

    [HttpGet("appearance")]
    public IActionResult Appearance()
    {
        var theme = Request.Cookies[ThemeCookie];
        var palette = Request.Cookies[PaletteCookie];
        var light = Request.Cookies[LightCookie];

        return View(new ThemeSettingsViewModel
        {
            Theme = theme is "light" or "dark" ? theme : "system",
            Palette = palette != null && Palettes.Contains(palette) ? palette : "default",
            LightPalette = light != null && LightPalettes.Contains(light) ? light : "default"
        });
    }

    [HttpPost("appearance")]
    [ValidateAntiForgeryToken]
    public IActionResult Appearance(string? theme, string? palette, string? lightPalette)
    {
        if (theme is not ("system" or "light" or "dark") ||
            palette == null || !Palettes.Contains(palette) ||
            lightPalette == null || !LightPalettes.Contains(lightPalette))
        {
            TempData["ErrorMessage"] = "Geçersiz görünüm seçeneği.";
            return RedirectToAction(nameof(Appearance));
        }

        SetOrClearCookie(ThemeCookie, theme, clear: theme == "system");
        SetOrClearCookie(PaletteCookie, palette, clear: palette == "default");
        SetOrClearCookie(LightCookie, lightPalette, clear: lightPalette == "default");

        TempData["SuccessMessage"] = "Görünüm ayarın kaydedildi.";
        return RedirectToAction(nameof(Appearance));
    }

    private void SetOrClearCookie(string name, string value, bool clear)
    {
        if (clear)
        {
            Response.Cookies.Delete(name, new CookieOptions { Path = "/" });
            return;
        }

        Response.Cookies.Append(name, value, new CookieOptions
        {
            Path = "/",
            MaxAge = TimeSpan.FromDays(365),
            HttpOnly = true,
            Secure = Request.IsHttps,
            SameSite = SameSiteMode.Lax,
            IsEssential = true
        });
    }

    // ---------- GÜVENLİK (ŞİFRE) ----------

    [HttpGet("security")]
    public IActionResult Security() => View(new ChangePasswordInput());

    [HttpPost("security")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Security(ChangePasswordInput input)
    {
        if (DemoAccount.IsDemo(User))
        {
            TempData["ErrorMessage"] = DemoAccount.BlockedMessage;
            return RedirectToAction(nameof(Security));
        }

        var user = await _context.Users.FindAsync(CurrentUserId);
        if (user == null)
            return await SignOutAsync();

        if (ModelState.IsValid)
        {
            if (!BCrypt.Net.BCrypt.Verify(input.CurrentPassword, user.Password))
            {
                ModelState.AddModelError(nameof(input.CurrentPassword), "Mevcut şifre hatalı.");
            }
            // Mevcut şifre doğrulandıktan sonra bakılır: yanlış şifre girene ek bilgi verilmez.
            else if (input.NewPassword == input.CurrentPassword)
            {
                ModelState.AddModelError(nameof(input.NewPassword), "Yeni şifre mevcut şifrenle aynı olamaz.");
            }
        }

        if (!ModelState.IsValid)
            return View(new ChangePasswordInput());   // şifre alanları geri doldurulmaz

        user.Password = BCrypt.Net.BCrypt.HashPassword(input.NewPassword);
        await _context.SaveChangesAsync();

        TempData["SuccessMessage"] = "Şifren değiştirildi.";
        return RedirectToAction(nameof(Security));
    }
}
