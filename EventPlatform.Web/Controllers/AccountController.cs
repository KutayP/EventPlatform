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

    [HttpGet]
    public IActionResult Register() => View();

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Register(RegisterViewModel model)
    {
        if (!ModelState.IsValid)
            return View(model);

        var email = model.Email.Trim().ToLowerInvariant();

        if (await _context.Users.AnyAsync(u => u.Username == model.Username))
        {
            ModelState.AddModelError(nameof(model.Username), "Bu kullanıcı adı alınmış.");
            return View(model);
        }

        if (await _context.Users.AnyAsync(u => u.Email == email))
        {
            ModelState.AddModelError(nameof(model.Email), "Bu e-posta zaten kayıtlı.");
            return View(model);
        }

        var user = new User
        {
            Username = model.Username,
            Email = email,
            Password = BCrypt.Net.BCrypt.HashPassword(model.Password),
            Firstname = model.FirstName,
            Lastname = model.LastName,
            Birthdate = model.BirthDate,
            Gender = model.Gender,
            Role = "User"
        };

        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        return RedirectToAction("Login");
    }

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

        var identifier = model.Identifier.Trim();
        var emailCandidate = identifier.ToLowerInvariant();

        var user = await _context.Users.FirstOrDefaultAsync(u =>
            u.Username == identifier || u.Email == emailCandidate);

        // Kullanıcı yok ya da şifre yanlış: aynı mesaj, hangisinin olduğunu belli etmiyoruz.
        if (user == null || !BCrypt.Net.BCrypt.Verify(model.Password, user.Password))
        {
            ModelState.AddModelError(string.Empty, "Kullanıcı adı/e-posta veya şifre hatalı.");
            return View(model);
        }

        var claims = new List<Claim>
    {
        new(ClaimTypes.NameIdentifier, user.Userid.ToString()),
        new(ClaimTypes.Name, user.Username),
        new(ClaimTypes.Role, user.Role)
    };

        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);

        var authProperties = new AuthenticationProperties { IsPersistent = model.RememberMe };
        if (model.RememberMe)
            authProperties.ExpiresUtc = DateTimeOffset.UtcNow.AddDays(30);

        await HttpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            new ClaimsPrincipal(identity),
            authProperties);

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
}