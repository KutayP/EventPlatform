using EventPlatform.Web.Models;
using EventPlatform.Web.Models.ViewModels;
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

    public async Task<IActionResult> Index()
    {
        // Sütun "timestamp without time zone" olduğu için Kind'ı Unspecified yapıyoruz.
        var now = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified);

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
}