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

[Authorize]
public class ProfileController : Controller
{
    private readonly AppDbContext _context;

    public ProfileController(AppDbContext context)
    {
        _context = context;
    }

    // Kimlik her zaman cookie'den gelir; başka birinin profilini düzenlemek mümkün değil.
    private int CurrentUserId =>
        int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : 0;

    private static DateTime NowForDb() =>
        DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified);

    // ---------- PROFİL ----------

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var vm = await BuildViewModelAsync(CurrentUserId);
        if (vm == null)
            return await SignOutAsync();

        return View(vm);
    }

    // ---------- BİLGİLERİ GÜNCELLE ----------

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateInfo(
        [Bind(Prefix = nameof(ProfileViewModel.InfoForm))] ProfileInfoInput input)
    {
        var userId = CurrentUserId;

        if (input.Birthdate.Date > DateTime.Today || input.Birthdate.Year < 1900)
        {
            ModelState.AddModelError(
                $"{nameof(ProfileViewModel.InfoForm)}.{nameof(input.Birthdate)}",
                "Geçerli bir doğum tarihi girin.");
        }

        if (!ModelState.IsValid)
        {
            var invalidVm = await BuildViewModelAsync(userId, info: input);
            return invalidVm == null ? await SignOutAsync() : View("Index", invalidVm);
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
        return RedirectToAction(nameof(Index));
    }

    // ---------- İLGİ ALANLARINI GÜNCELLE ----------

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateInterests(List<int>? selectedInterestIds)
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
            var invalidVm = await BuildViewModelAsync(userId, selectedIds: requested.Where(validIds.Contains).ToList());
            return invalidVm == null ? await SignOutAsync() : View("Index", invalidVm);
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
        return RedirectToAction(nameof(Index));
    }

    // ---------- ŞİFRE DEĞİŞTİR ----------

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ChangePassword(
        [Bind(Prefix = nameof(ProfileViewModel.PasswordForm))] ChangePasswordInput input)
    {
        var userId = CurrentUserId;

        var user = await _context.Users.FindAsync(userId);
        if (user == null)
            return await SignOutAsync();

        if (ModelState.IsValid)
        {
            if (!BCrypt.Net.BCrypt.Verify(input.CurrentPassword, user.Password))
            {
                ModelState.AddModelError(
                    $"{nameof(ProfileViewModel.PasswordForm)}.{nameof(input.CurrentPassword)}",
                    "Mevcut şifre hatalı.");
            }
            // Mevcut şifre doğrulandıktan sonra bakılır: yanlış şifre girene ek bilgi verilmez.
            else if (input.NewPassword == input.CurrentPassword)
            {
                ModelState.AddModelError(
                    $"{nameof(ProfileViewModel.PasswordForm)}.{nameof(input.NewPassword)}",
                    "Yeni şifre mevcut şifrenle aynı olamaz.");
            }
        }

        if (!ModelState.IsValid)
        {
            var invalidVm = await BuildViewModelAsync(userId);
            return invalidVm == null ? await SignOutAsync() : View("Index", invalidVm);
        }

        user.Password = BCrypt.Net.BCrypt.HashPassword(input.NewPassword);
        await _context.SaveChangesAsync();

        TempData["SuccessMessage"] = "Şifren değiştirildi.";
        return RedirectToAction(nameof(Index));
    }

    // ---------- YARDIMCI ----------

    // Silinmiş bir kullanıcının cookie'si kalmışsa oturumu kapatıp girişe yönlendirir.
    private async Task<IActionResult> SignOutAsync()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return RedirectToAction("Login", "Account");
    }

    // Sayfa modelini veritabanından kurar. Doğrulama hatasında kullanıcının yazdıkları (info/selectedIds) korunur.
    private async Task<ProfileViewModel?> BuildViewModelAsync(
        int userId, ProfileInfoInput? info = null, List<int>? selectedIds = null)
    {
        var u = await _context.Users
            .AsNoTracking()
            .Where(x => x.Userid == userId)
            .Select(x => new { x.Username, x.Email, x.Firstname, x.Lastname, x.Birthdate, x.Gender })
            .FirstOrDefaultAsync();

        if (u == null)
            return null;

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

        return new ProfileViewModel
        {
            Username = u.Username,
            Email = u.Email,
            CreatedCount = await _context.Events.CountAsync(e => e.Createdbyuserid == userId),
            JoinedCount = await _context.Events.CountAsync(e => e.Users.Any(p => p.Userid == userId)),
            InfoForm = info ?? new ProfileInfoInput
            {
                Firstname = u.Firstname,
                Lastname = u.Lastname,
                Birthdate = u.Birthdate,
                Gender = u.Gender
            },
            SelectedInterestIds = selectedIds,
            Interests = interests
        };
    }
}