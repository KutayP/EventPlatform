using System.Security.Claims;
using EventPlatform.Web.Models;
using EventPlatform.Web.Models.Entities;
using EventPlatform.Web.Models.ViewModels;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EventPlatform.Web.Controllers;

public class AccountController : Controller
{
    private readonly AppDbContext _context;

    public AccountController(AppDbContext context)
    {
        _context = context;
    }

    // Sütun "timestamp without time zone" olduğu için Kind'ı Unspecified yapıyoruz.
    private static DateTime NowForDb() =>
        DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified);

    // ---------- KAYIT ----------

    [HttpGet]
    public async Task<IActionResult> Register()
    {
        var model = new RegisterViewModel();
        await LoadInterestsAsync(model);
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Register(RegisterViewModel model)
    {
        if (!ModelState.IsValid)
        {
            await LoadInterestsAsync(model);
            return View(model);
        }

        var username = model.Username.Trim();
        var usernameLower = username.ToLowerInvariant();
        var email = model.Email.Trim().ToLowerInvariant();

        // Kullanıcı adı büyük/küçük harf duyarsız benzersiz olmalı.
        if (await _context.Users.AnyAsync(u => u.Username.ToLower() == usernameLower))
            ModelState.AddModelError(nameof(model.Username), "Bu kullanıcı adı zaten alınmış.");

        if (await _context.Users.AnyAsync(u => u.Email == email))
            ModelState.AddModelError(nameof(model.Email), "Bu e-posta adresi zaten kayıtlı.");

        if (model.BirthDate.Date > DateTime.Today || model.BirthDate.Year < 1900)
            ModelState.AddModelError(nameof(model.BirthDate), "Geçerli bir doğum tarihi girin.");

        // Seçilen ilgi alanları gerçekten tabloda var mı? (istemciye güvenme)
        var validInterestIds = await _context.Interests
            .AsNoTracking()
            .Select(i => i.Interestid)
            .ToListAsync();

        var selectedIds = model.SelectedInterestIds
            .Distinct()
            .ToList();

        if (selectedIds.Any(id => !validInterestIds.Contains(id)))
            ModelState.AddModelError(nameof(model.SelectedInterestIds), "Geçersiz ilgi alanı seçildi.");

        if (!ModelState.IsValid)
        {
            await LoadInterestsAsync(model);
            return View(model);
        }

        var user = new User
        {
            Username = username,
            Email = email,
            Password = BCrypt.Net.BCrypt.HashPassword(model.Password),
            Firstname = model.FirstName.Trim(),
            Lastname = model.LastName.Trim(),
            Birthdate = model.BirthDate,
            Gender = model.Gender,
            Role = "User" // rol asla formdan gelmez
        };

        // Kullanıcı ve ilgi alanları tek SaveChanges içinde, tek işlemde kaydedilir.
        foreach (var interestId in selectedIds)
        {
            user.Userinterests.Add(new Userinterest
            {
                Interestid = interestId,
                Time = NowForDb()
            });
        }

        _context.Users.Add(user);

        try
        {
            await _context.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            // Aynı anda gelen iki kayıt isteğinde UNIQUE kısıtlaması son savunma hattı.
            ModelState.AddModelError(string.Empty, "Kayıt oluşturulamadı. Kullanıcı adı veya e-posta kullanımda olabilir.");
            await LoadInterestsAsync(model);
            return View(model);
        }

        // Kayıt sonrası kullanıcıyı doğrudan giriş yapmış say.
        await SignInUserAsync(user, isPersistent: false);

        return RedirectToAction("Index", "Home");
    }

    // ---------- GİRİŞ / ÇIKIŞ ----------

    [HttpGet]
    public IActionResult Login(string? returnUrl = null)
    {
        ViewData["ReturnUrl"] = returnUrl;
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginViewModel model, string? returnUrl = null)
    {
        ViewData["ReturnUrl"] = returnUrl;

        if (!ModelState.IsValid)
            return View(model);

        // Kullanıcı adı ve e-posta büyük/küçük harf duyarsız, şifre duyarlı.
        var identifierLower = model.Identifier.Trim().ToLowerInvariant();

        var user = await _context.Users
            .FirstOrDefaultAsync(u => u.Username.ToLower() == identifierLower || u.Email == identifierLower);

        // Kullanıcı yok / şifre yanlış için aynı mesaj: hesap varlığı sızdırılmaz.
        if (user == null || !BCrypt.Net.BCrypt.Verify(model.Password, user.Password))
        {
            ModelState.AddModelError(string.Empty, "Kullanıcı adı/e-posta veya şifre hatalı.");
            return View(model);
        }

        await SignInUserAsync(user, model.RememberMe);

        // Sadece site içi adreslere yönlendir (open redirect koruması).
        if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
            return LocalRedirect(returnUrl);

        return RedirectToAction("Index", "Home");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return RedirectToAction("Index", "Home");
    }

    [HttpGet]
    public IActionResult AccessDenied() => View();

    // ---------- YARDIMCI ----------

    // Formdaki ilgi alanı kutucuklarını veritabanından doldurur.
    private async Task LoadInterestsAsync(RegisterViewModel model)
    {
        var selected = model.SelectedInterestIds;

        model.Interests = await _context.Interests
            .AsNoTracking()
            .OrderBy(i => i.Interestname)
            .Select(i => new InterestOption
            {
                Interestid = i.Interestid,
                Interestname = i.Interestname,
                Selected = selected.Contains(i.Interestid)
            })
            .ToListAsync();
    }

    // Giriş ve kayıt aynı cookie oluşturma kodunu kullanır.
    private async Task SignInUserAsync(User user, bool isPersistent)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Userid.ToString()),
            new(ClaimTypes.Name, user.Username),
            new(ClaimTypes.Role, user.Role)
        };

        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);

        var props = new AuthenticationProperties { IsPersistent = isPersistent };
        if (isPersistent)
            props.ExpiresUtc = DateTimeOffset.UtcNow.AddDays(30);

        await HttpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            new ClaimsPrincipal(identity),
            props);
    }
}