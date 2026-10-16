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

    // ---------- KAYIT ----------

    [HttpGet]
    public IActionResult Register() => View(new RegisterViewModel());

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Register(RegisterViewModel model)
    {
        if (!ModelState.IsValid)
            return View(model);

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

        if (!ModelState.IsValid)
            return View(model);

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

        _context.Users.Add(user);

        try
        {
            await _context.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            // Aynı anda gelen iki kayıt isteğinde UNIQUE kısıtlaması son savunma hattı.
            ModelState.AddModelError(string.Empty, "Kayıt oluşturulamadı. Kullanıcı adı veya e-posta kullanımda olabilir.");
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