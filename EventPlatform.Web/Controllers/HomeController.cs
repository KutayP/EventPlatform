using System.Diagnostics;
using System.Security.Claims;
using EventPlatform.Web.Helpers;
using EventPlatform.Web.Models;
using EventPlatform.Web.Models.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EventPlatform.Web.Controllers;

public class HomeController : Controller
{
    private const int FriendsEventsShown = 3;

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
                    Publicid = e.Publicid,
                    Eventname = e.Eventname,
                    Date = e.Date,
                    Location = e.Location,
                    Category = e.Category,
                    ParticipantCount = e.Users.Count(u => u.Userid != e.Createdbyuserid) + 1
                })
                .ToListAsync()
        };

        // Giriş yapmış kullanıcıya, keşif için: arkadaşlarının oluşturduğu ve katıldığı yaklaşan etkinlikler.
        // Kendi oluşturduğun ve zaten katıldığın etkinlikler burada tekrar gösterilmez.
        if (int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId))
        {
            vm.IsAuthenticated = true;

            var friendIds = _context.Friendships.FriendIdsOf(userId);

            var candidates = _context.Events
                .AsNoTracking()
                .Where(e => e.Isapproved && e.Date >= now &&
                            e.Createdbyuserid != userId &&
                            !e.Users.Any(u => u.Userid == userId));

            vm.FriendsCreated = await candidates
                .Where(e => friendIds.Contains(e.Createdbyuserid))
                .OrderBy(e => e.Date)
                .Take(FriendsEventsShown)
                .Select(e => new EventListItemViewModel
                {
                    Eventid = e.Eventid,
                    Publicid = e.Publicid,
                    Eventname = e.Eventname,
                    Date = e.Date,
                    Location = e.Location,
                    Category = e.Category,
                    ParticipantCount = e.Users.Count(u => u.Userid != e.Createdbyuserid) + 1,
                    Note = e.Createdbyuser.Username + " oluşturdu"
                })
                .ToListAsync();

            // Bir etkinlik iki listede birden çıkmasın: arkadaşın oluşturduğu etkinlikler yalnızca yukarıdaki listede.
            vm.FriendsJoined = await candidates
                .Where(e => !friendIds.Contains(e.Createdbyuserid) &&
                            e.Users.Any(u => friendIds.Contains(u.Userid)))
                .OrderBy(e => e.Date)
                .Take(FriendsEventsShown)
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

            if (vm.FriendsJoined.Count > 0)
            {
                // Kartın altına "Katılan: deniz_a, kaan_y +1" notu: hangi arkadaşların katıldığını gösterir.
                var joinedIds = vm.FriendsJoined.Select(x => x.Eventid).ToList();

                var friendParticipants = await _context.Events
                    .AsNoTracking()
                    .Where(e => joinedIds.Contains(e.Eventid))
                    .SelectMany(
                        e => e.Users.Where(u => friendIds.Contains(u.Userid)),
                        (e, u) => new { e.Eventid, u.Username })
                    .ToListAsync();

                foreach (var item in vm.FriendsJoined)
                {
                    var names = friendParticipants
                        .Where(x => x.Eventid == item.Eventid)
                        .Select(x => x.Username)
                        .OrderBy(n => n, StringComparer.OrdinalIgnoreCase)
                        .ToList();

                    if (names.Count > 0)
                    {
                        item.Note = names.Count <= 2
                            ? "Katılan: " + string.Join(", ", names)
                            : $"Katılan: {names[0]}, {names[1]} +{names.Count - 2}";
                    }
                }
            }
        }

        return View(vm);
    }

    // 404 gibi durum kodlarında Program.cs'deki UseStatusCodePagesWithReExecute buraya yönlendirir.
    // Durum kodu orijinal hâliyle korunur, burada değiştirmiyoruz.
    public IActionResult Status(int code)
    {
        return View(code);
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}