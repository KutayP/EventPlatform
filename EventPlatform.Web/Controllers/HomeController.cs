using System.Diagnostics;
using EventPlatform.Web.Models;
using EventPlatform.Web.Models.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EventPlatform.Web.Controllers;

public class HomeController : Controller
{
    private readonly AppDbContext _context;

    public HomeController(AppDbContext context)
    {
        _context = context;
    }

    public async Task<IActionResult> Index()
    {
        // Sütun "timestamp without time zone": Kind Unspecified olmalı.
        var now = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified);

        var upcoming = _context.Events
            .AsNoTracking()
            .Where(e => e.Isapproved && e.Date >= now);

        var vm = new HomeViewModel
        {
            UpcomingEventCount = await upcoming.CountAsync(),
            MemberCount = await _context.Users.CountAsync(),
            Upcoming = await upcoming
                .OrderBy(e => e.Date)
                .Take(3)
                .Select(e => new EventListItemViewModel
                {
                    Eventid = e.Eventid,
                    Eventname = e.Eventname,
                    Date = e.Date,
                    Location = e.Location,
                    Category = e.Category,
                    ParticipantCount = e.Users.Count
                })
                .ToListAsync()
        };

        return View(vm);
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}