using System.Linq.Expressions;
using System.Security.Claims;
using EventPlatform.Web.Helpers;
using EventPlatform.Web.Models;
using EventPlatform.Web.Models.Entities;
using EventPlatform.Web.Models.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EventPlatform.Web.Controllers;

// Profil sayfaları: /u/{kullanici}
public class UsersController : Controller
{
    private const int EventsShown = 6;

    private readonly AppDbContext _context;

    public UsersController(AppDbContext context)
    {
        _context = context;
    }

    private int CurrentUserId =>
        int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : 0;

    private static DateTime NowForDb() =>
        DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified);

    // Etkinlik kartı için ortak projeksiyon (listelerdeki ile aynı alanlar).
    private static readonly Expression<Func<Event, EventListItemViewModel>> ToListItem = e => new EventListItemViewModel
    {
        Eventid = e.Eventid,
        Publicid = e.Publicid,
        Eventname = e.Eventname,
        Date = e.Date,
        Location = e.Location,
        Category = e.Category,
        ParticipantCount = e.Users.Count(u => u.Userid != e.Createdbyuserid) + 1
    };

    [HttpGet("u/{username}")]
    public async Task<IActionResult> Profile(string username)
    {
        var user = await _context.Users
            .AsNoTracking()
            .Where(u => u.Username == username)
            .Select(u => new { u.Userid, u.Username, u.Firstname, u.Lastname, u.Profilevisibility })
            .FirstOrDefaultAsync();

        if (user == null)
            return NotFound();

        var viewerId = CurrentUserId;
        var isOwner = viewerId == user.Userid;
        var isAdmin = User.IsInRole("Admin");
        var isPublic = user.Profilevisibility == ProfileVisibilities.Public;

        // Ziyaretçinin bu kullanıcıyla arkadaşlık durumu.
        FriendRelation relation;
        if (viewerId == 0)
        {
            relation = FriendRelation.Anonymous;
        }
        else if (isOwner)
        {
            relation = FriendRelation.Self;
        }
        else
        {
            var f = await _context.Friendships
                .AsNoTracking()
                .Between(viewerId, user.Userid)
                .Select(x => new { x.Requesterid, x.Status })
                .FirstOrDefaultAsync();

            relation = f == null ? FriendRelation.None
                : f.Status == FriendshipStatus.Accepted ? FriendRelation.Friends
                : f.Requesterid == viewerId ? FriendRelation.RequestSent
                : FriendRelation.RequestReceived;
        }

        var isFriend = relation == FriendRelation.Friends;

        // İçeriği kim görür? Herkese açıksa herkes; "yalnızca arkadaşlar" ise arkadaşlar, sahibi ve yönetici.
        // Özel bilgiler (e-posta, doğum tarihi, cinsiyet) hiçbir durumda gösterilmez.
        var vm = new PublicProfileViewModel
        {
            Username = user.Username,
            DisplayName = DisplayNameHelper.Short(user.Firstname, user.Lastname),
            IsOwner = isOwner,
            IsFriendsOnly = !isPublic,
            IsVisible = isPublic || isOwner || isAdmin || isFriend,
            IsAdminViewing = !isOwner && isAdmin && !isPublic && !isFriend,
            Relation = relation
        };

        if (!vm.IsVisible)
            return View(vm);

        var userId = user.Userid;
        var now = NowForDb();

        // Yalnızca yayında olan etkinlikler profilde görünür.
        vm.CreatedCount = await _context.Events
            .CountAsync(e => e.Createdbyuserid == userId && e.Isapproved);

        vm.JoinedCount = await _context.Events
            .CountAsync(e => e.Isapproved && e.Users.Any(u => u.Userid == userId));

        vm.Interests = await _context.Userinterests
            .AsNoTracking()
            .Where(ui => ui.Userid == userId)
            .Select(ui => ui.Interest.Interestname)
            .OrderBy(n => n)
            .ToListAsync();

        vm.Created = await _context.Events
            .AsNoTracking()
            .Where(e => e.Createdbyuserid == userId && e.Isapproved && e.Date >= now)
            .OrderBy(e => e.Date)
            .Take(EventsShown)
            .Select(ToListItem)
            .ToListAsync();

        vm.Joined = await _context.Events
            .AsNoTracking()
            .Where(e => e.Isapproved && e.Date >= now && e.Users.Any(u => u.Userid == userId))
            .OrderBy(e => e.Date)
            .Take(EventsShown)
            .Select(ToListItem)
            .ToListAsync();

        return View(vm);
    }
}
