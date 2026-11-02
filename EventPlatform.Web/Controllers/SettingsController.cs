using System.Security.Claims;
using EventPlatform.Web.Models;
using EventPlatform.Web.Models.Entities;
using EventPlatform.Web.Models.ViewModels;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EventPlatform.Web.Controllers;

// Ayarlar: /ayarlar/profil, /ayarlar/ilgi-alanlari, /ayarlar/gizlilik, /ayarlar/guvenlik
[Authorize]
[Route("ayarlar")]
public class SettingsController : Controller
{
    private readonly AppDbContext _context;

    public SettingsController(AppDbContext context)
    {
        _context = context;
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

    [HttpGet("profil")]
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

    [HttpPost("profil")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ProfileInfo(SettingsProfileViewModel input)
    {
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

    [HttpGet("ilgi-alanlari")]
    public async Task<IActionResult> Interests()
    {
        return View(await BuildInterestsAsync(CurrentUserId));
    }

    [HttpPost("ilgi-alanlari")]
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

    [HttpGet("gizlilik")]
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

    [HttpPost("gizlilik")]
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

    // ---------- GÜVENLİK (ŞİFRE) ----------

    [HttpGet("guvenlik")]
    public IActionResult Security() => View(new ChangePasswordInput());

    [HttpPost("guvenlik")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Security(ChangePasswordInput input)
    {
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
