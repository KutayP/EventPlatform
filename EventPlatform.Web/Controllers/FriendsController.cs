using System.Security.Claims;
using EventPlatform.Web.Helpers;
using EventPlatform.Web.Models;
using EventPlatform.Web.Models.Entities;
using EventPlatform.Web.Models.ViewModels;
using EventPlatform.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EventPlatform.Web.Controllers;

[Authorize]
public class FriendsController : Controller
{
    private const int MinSearchLength = 2;
    private const int MaxSearchLength = 30;
    private const double MinSimilarity = 0.4;    // yakın sonuç eşiği (0-1, yüksek = daha sıkı)
    private const int SearchResultLimit = 10;
    private const int MaxPendingRequests = 50;   // istek yağdırmayı sınırlar

    private readonly AppDbContext _context;
    private readonly NotificationService _notifications;

    public FriendsController(AppDbContext context, NotificationService notifications)
    {
        _context = context;
        _notifications = notifications;
    }

    // Kimlik her zaman cookie'den gelir; başkası adına istek göndermek mümkün değil.
    private int CurrentUserId =>
        int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : 0;

    // Kullanıcı adından numarayı bulur; yoksa 0.
    private Task<int> FindUserIdAsync(string username) =>
        _context.Users
            .AsNoTracking()
            .Where(u => u.Username == username)
            .Select(u => u.Userid)
            .FirstOrDefaultAsync();

    // İşlemden sonra geldiği sayfaya dön (yalnızca site içi adresler), yoksa arkadaşlar sayfasına.
    private IActionResult Back(string? returnUrl)
    {
        // Bildirim menüsü gibi JavaScript çağrılarında sayfa değişmez; bir sonraki sayfada eski mesaj çıkmasın.
        if (Request.Headers.XRequestedWith == "fetch")
        {
            TempData.Remove("SuccessMessage");
            TempData.Remove("ErrorMessage");
            return NoContent();
        }

        if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
            return LocalRedirect(returnUrl);

        return RedirectToAction(nameof(Index));
    }

    // ---------- SAYFA ----------

    [HttpGet("arkadaslar")]
    public async Task<IActionResult> Index(string? q)
    {
        var me = CurrentUserId;
        var vm = new FriendsIndexViewModel();

        var incoming = await _context.Friendships
            .AsNoTracking()
            .Where(f => f.Addresseeid == me && f.Status == FriendshipStatus.Pending)
            .OrderByDescending(f => f.Createdat)
            .Select(f => new { f.Requester.Username, f.Requester.Firstname, f.Requester.Lastname })
            .ToListAsync();

        var outgoing = await _context.Friendships
            .AsNoTracking()
            .Where(f => f.Requesterid == me && f.Status == FriendshipStatus.Pending)
            .OrderByDescending(f => f.Createdat)
            .Select(f => new { f.Addressee.Username, f.Addressee.Firstname, f.Addressee.Lastname })
            .ToListAsync();

        var friendsA = await _context.Friendships
            .AsNoTracking()
            .Where(f => f.Requesterid == me && f.Status == FriendshipStatus.Accepted)
            .Select(f => new { f.Addressee.Username, f.Addressee.Firstname, f.Addressee.Lastname })
            .ToListAsync();

        var friendsB = await _context.Friendships
            .AsNoTracking()
            .Where(f => f.Addresseeid == me && f.Status == FriendshipStatus.Accepted)
            .Select(f => new { f.Requester.Username, f.Requester.Firstname, f.Requester.Lastname })
            .ToListAsync();

        vm.Incoming = incoming
            .Select(x => new FriendRow(x.Username, DisplayNameHelper.Short(x.Firstname, x.Lastname), FriendRelation.RequestReceived))
            .ToList();

        vm.Outgoing = outgoing
            .Select(x => new FriendRow(x.Username, DisplayNameHelper.Short(x.Firstname, x.Lastname), FriendRelation.RequestSent))
            .ToList();

        vm.Friends = friendsA.Concat(friendsB)
            .OrderBy(x => x.Username, StringComparer.OrdinalIgnoreCase)
            .Select(x => new FriendRow(x.Username, DisplayNameHelper.Short(x.Firstname, x.Lastname), FriendRelation.Friends))
            .ToList();

        // Kullanıcı arama: kullanıcı adı + ad + soyadın baş harfi üzerinde (başkalarına gösterdiğimiz bilgi kadar).
        // "kaan y", "kaan_y", "@kaan_y" aynı sonucu verir; Türkçe harfler ASCII'ye çevrilir.
        // Birebir eşleşmeler önce, yazım hatalı/yakın sonuçlar (trigram benzerliği) arkadan gelir.
        q = q?.Trim();
        if (q is { Length: > MaxSearchLength })
            q = q[..MaxSearchLength];

        vm.Query = q;

        var normalized = SearchText.Normalize(q);

        if (!string.IsNullOrEmpty(q))
        {
            if (normalized.Length < MinSearchLength)
            {
                vm.QueryTooShort = true;
            }
            else
            {
                // Kullanıcı girdisi yalnızca parametre olarak gider (SQL'e metin olarak eklenmez).
                // strpos: "içeriyor mu" kontrolü; LIKE olmadığı için % ve _ karakterleri sorun çıkarmaz.
                var found = await _context.Database.SqlQuery<UserSearchHit>($"""
                    WITH c AS (
                        SELECT u.userid, u.username, u.firstname, u.lastname,
                               trim(regexp_replace(
                                   translate(
                                       lower(translate(u.username || ' ' || u.firstname || ' ' || left(u.lastname, 1),
                                                       'İIÇĞÖŞÜÂÎÛ', 'iicgosuaiu')),
                                       'çğıöşüâîû', 'cgiosuaiu'),
                                   '[^a-z0-9]+', ' ', 'g')) AS h
                        FROM users u
                        WHERE u.userid <> {me}
                    )
                    SELECT c.userid AS "Userid", c.username AS "Username",
                           c.firstname AS "Firstname", c.lastname AS "Lastname",
                           GREATEST(word_similarity({normalized}, c.h),
                                    CASE WHEN strpos(c.h, {normalized}) > 0 THEN 1.0 ELSE 0.0 END)::float8 AS "Score"
                    FROM c
                    WHERE strpos(c.h, {normalized}) > 0
                       OR word_similarity({normalized}, c.h) >= {MinSimilarity}
                    ORDER BY "Score" DESC,
                             CASE WHEN strpos(c.h, {normalized}) > 0 THEN strpos(c.h, {normalized}) ELSE 1000 END,
                             c.username
                    LIMIT {SearchResultLimit}
                    """).ToListAsync();

                var ids = found.Select(u => u.Userid).ToList();

                var relations = await _context.Friendships
                    .AsNoTracking()
                    .Where(f => (f.Requesterid == me && ids.Contains(f.Addresseeid)) ||
                                (f.Addresseeid == me && ids.Contains(f.Requesterid)))
                    .Select(f => new { f.Requesterid, f.Addresseeid, f.Status })
                    .ToListAsync();

                vm.Results = found.Select(u =>
                {
                    var r = relations.FirstOrDefault(x => x.Requesterid == u.Userid || x.Addresseeid == u.Userid);

                    var relation = r == null ? FriendRelation.None
                        : r.Status == FriendshipStatus.Accepted ? FriendRelation.Friends
                        : r.Requesterid == me ? FriendRelation.RequestSent
                        : FriendRelation.RequestReceived;

                    return new FriendRow(u.Username, DisplayNameHelper.Short(u.Firstname, u.Lastname), relation);
                }).ToList();
            }
        }

        return View(vm);
    }

    // ---------- İSTEK GÖNDER ----------

    [HttpPost("arkadaslar/istek/{username}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SendRequest(string username, string? returnUrl)
    {
        var me = CurrentUserId;
        var targetId = await FindUserIdAsync(username);

        if (targetId == 0)
            return NotFound();

        if (targetId == me)
        {
            TempData["ErrorMessage"] = "Kendine arkadaşlık isteği gönderemezsin.";
            return Back(returnUrl);
        }

        var existing = await _context.Friendships
            .Between(me, targetId)
            .Select(f => new { f.Requesterid, f.Status })
            .FirstOrDefaultAsync();

        if (existing != null)
        {
            if (existing.Status == FriendshipStatus.Pending && existing.Requesterid == targetId)
            {
                // O da bana istek göndermiş: iki taraf da istiyor, doğrudan arkadaş yap.
                await AcceptAsync(requesterId: targetId, addresseeId: me);
                TempData["SuccessMessage"] = $"{username} ile arkadaş oldunuz.";
            }
            else if (existing.Status == FriendshipStatus.Accepted)
            {
                TempData["ErrorMessage"] = "Zaten arkadaşsınız.";
            }
            else
            {
                TempData["ErrorMessage"] = "Bu kullanıcıya zaten bir istek gönderdin.";
            }

            return Back(returnUrl);
        }

        var pendingCount = await _context.Friendships
            .CountAsync(f => f.Requesterid == me && f.Status == FriendshipStatus.Pending);

        if (pendingCount >= MaxPendingRequests)
        {
            TempData["ErrorMessage"] = "Çok fazla bekleyen isteğin var. Önce bazılarının yanıtlanmasını bekle ya da iptal et.";
            return Back(returnUrl);
        }

        _context.Friendships.Add(new Friendship
        {
            Requesterid = me,
            Addresseeid = targetId,
            Status = FriendshipStatus.Pending
        });

        try
        {
            await _context.SaveChangesAsync();
            TempData["SuccessMessage"] = $"{username} kullanıcısına arkadaşlık isteği gönderildi.";
            await _notifications.NotifyAsync(targetId, NotificationTypes.FriendRequest, me);
        }
        catch (DbUpdateException ex) when (ex.InnerException is Npgsql.PostgresException { SqlState: "23505" })
        {
            // Aynı anda iki istek geldi: benzersiz kısıt ikincisini engelledi.
            TempData["ErrorMessage"] = "Bu kullanıcıyla zaten bir arkadaşlık kaydı var.";
        }

        return Back(returnUrl);
    }

    // ---------- KABUL / RED / İPTAL / ÇIKAR ----------

    // Yalnızca isteğin muhatabı kabul edebilir: sorgu, "addressee = ben" şartını içerir.
    private async Task<int> AcceptAsync(int requesterId, int addresseeId)
    {
        var updated = await _context.Friendships
            .Where(f => f.Requesterid == requesterId &&
                        f.Addresseeid == addresseeId &&
                        f.Status == FriendshipStatus.Pending)
            .ExecuteUpdateAsync(s => s.SetProperty(f => f.Status, FriendshipStatus.Accepted));

        if (updated > 0)
        {
            // Kabul eden kişinin "istek geldi" bildirimi artık geçersiz; isteği gönderene "kabul etti" haberi gider.
            await _notifications.RemoveAsync(addresseeId, NotificationTypes.FriendRequest, requesterId);
            await _notifications.NotifyAsync(requesterId, NotificationTypes.FriendAccepted, addresseeId);
        }

        return updated;
    }

    [HttpPost("arkadaslar/kabul/{username}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Accept(string username, string? returnUrl)
    {
        var requesterId = await FindUserIdAsync(username);
        var updated = requesterId == 0 ? 0 : await AcceptAsync(requesterId, CurrentUserId);

        TempData[updated > 0 ? "SuccessMessage" : "ErrorMessage"] = updated > 0
            ? $"{username} ile arkadaş oldunuz."
            : "Bekleyen bir istek bulunamadı.";

        return Back(returnUrl);
    }

    [HttpPost("arkadaslar/reddet/{username}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Decline(string username, string? returnUrl)
    {
        var me = CurrentUserId;
        var requesterId = await FindUserIdAsync(username);

        var deleted = requesterId == 0 ? 0 : await _context.Friendships
            .Where(f => f.Requesterid == requesterId &&
                        f.Addresseeid == me &&
                        f.Status == FriendshipStatus.Pending)
            .ExecuteDeleteAsync();

        if (deleted > 0)
            await _notifications.RemoveAsync(me, NotificationTypes.FriendRequest, requesterId);

        TempData[deleted > 0 ? "SuccessMessage" : "ErrorMessage"] = deleted > 0
            ? "İstek reddedildi."
            : "Bekleyen bir istek bulunamadı.";

        return Back(returnUrl);
    }

    [HttpPost("arkadaslar/iptal/{username}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Cancel(string username, string? returnUrl)
    {
        var me = CurrentUserId;
        var targetId = await FindUserIdAsync(username);

        var deleted = targetId == 0 ? 0 : await _context.Friendships
            .Where(f => f.Requesterid == me &&
                        f.Addresseeid == targetId &&
                        f.Status == FriendshipStatus.Pending)
            .ExecuteDeleteAsync();

        if (deleted > 0)
            await _notifications.RemoveAsync(targetId, NotificationTypes.FriendRequest, me);

        TempData[deleted > 0 ? "SuccessMessage" : "ErrorMessage"] = deleted > 0
            ? "İstek iptal edildi."
            : "Bekleyen bir istek bulunamadı.";

        return Back(returnUrl);
    }

    [HttpPost("arkadaslar/cikar/{username}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Remove(string username, string? returnUrl)
    {
        var me = CurrentUserId;
        var targetId = await FindUserIdAsync(username);

        var deleted = targetId == 0 ? 0 : await _context.Friendships
            .Between(me, targetId)
            .Where(f => f.Status == FriendshipStatus.Accepted)
            .ExecuteDeleteAsync();

        TempData[deleted > 0 ? "SuccessMessage" : "ErrorMessage"] = deleted > 0
            ? $"{username} arkadaşlıktan çıkarıldı."
            : "Bu kullanıcıyla arkadaş değilsiniz.";

        return Back(returnUrl);
    }
}
