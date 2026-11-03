using EventPlatform.Web.Hubs;
using EventPlatform.Web.Models;
using EventPlatform.Web.Models.Entities;
using EventPlatform.Web.Models.ViewModels;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace EventPlatform.Web.Services;

// Bildirim oluşturma, silme ve listeleme. Bildirim üretmek asıl işlemi (katılma, arkadaşlık isteği)
// asla bozmamalı: oluşturma/silme hataları yutulur ve günlüğe yazılır.
public class NotificationService
{
    private readonly AppDbContext _context;
    private readonly IHubContext<NotificationHub> _hub;
    private readonly ILogger<NotificationService> _logger;

    public NotificationService(AppDbContext context, IHubContext<NotificationHub> hub, ILogger<NotificationService> logger)
    {
        _context = context;
        _hub = hub;
        _logger = logger;
    }

    private static DateTime NowForDb() =>
        DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified);

    // Alıcının ayarına göre bu tür bildirim açık mı? Ayar satırı yoksa varsayılanlar geçerli.
    private static bool IsEnabled(string type, Usersetting? s) => type switch
    {
        NotificationTypes.EventJoined => s?.NotifyEventJoined ?? true,
        NotificationTypes.FriendRequest or NotificationTypes.FriendAccepted => s?.NotifyFriends ?? true,
        NotificationTypes.EventRecommended => s?.NotifyRecommended ?? false,
        _ => false
    };

    public async Task NotifyAsync(int recipientId, string type, int actorId, int? eventId = null)
    {
        if (recipientId == actorId)
            return;

        Notification? added = null;
        try
        {
            var settings = await _context.Usersettings
                .AsNoTracking()
                .FirstOrDefaultAsync(s => s.Userid == recipientId);

            if (!IsEnabled(type, settings))
                return;

            added = new Notification
            {
                Userid = recipientId,
                Type = type,
                Actorid = actorId,
                Eventid = eventId,
                Createdat = NowForDb()
            };
            _context.Notifications.Add(added);
            await _context.SaveChangesAsync();

            await PingAsync(recipientId);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Bildirim oluşturulamadı (tür: {Type}).", type);
            if (added != null)
                _context.Entry(added).State = EntityState.Detached;
        }
    }

    // Durumu geçersiz kalan bildirimleri siler (istek iptal/kabul edildi, etkinlikten ayrılındı).
    public async Task RemoveAsync(int recipientId, string type, int actorId, int? eventId = null)
    {
        try
        {
            var deleted = await _context.Notifications
                .Where(n => n.Userid == recipientId && n.Type == type && n.Actorid == actorId &&
                            (eventId == null || n.Eventid == eventId))
                .ExecuteDeleteAsync();

            if (deleted > 0)
                await PingAsync(recipientId);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Bildirim silinemedi (tür: {Type}).", type);
        }
    }

    // unreadOnly: zil menüsü için yalnızca okunmamışlar; false: tüm geçmiş (sayfalı).
    public async Task<NotificationPanelViewModel> GetPanelAsync(int userId, int take, bool unreadOnly = false, int skip = 0)
    {
        var query = _context.Notifications
            .AsNoTracking()
            .Where(n => n.Userid == userId);

        if (unreadOnly)
            query = query.Where(n => n.Readat == null);

        // Bir fazla çekip "devamı var mı" bilgisini çıkarıyoruz.
        var items = await query
            .OrderByDescending(n => n.Createdat)
            .ThenByDescending(n => n.Notificationid)
            .Skip(skip)
            .Take(take + 1)
            .Select(n => new NotificationItem
            {
                Id = n.Notificationid,
                Type = n.Type,
                ActorUsername = n.Actor != null ? n.Actor.Username : null,
                ActorName = n.Actor != null ? n.Actor.Firstname + " " + n.Actor.Lastname.Substring(0, 1) + "." : "Biri",
                EventName = n.Event != null ? n.Event.Eventname : null,
                EventPublicid = n.Event != null ? n.Event.Publicid : null,
                CreatedAt = n.Createdat,
                IsRead = n.Readat != null
            })
            .ToListAsync();

        var hasMore = items.Count > take;
        if (hasMore)
            items.RemoveAt(take);

        var unread = await _context.Notifications
            .AsNoTracking()
            .CountAsync(n => n.Userid == userId && n.Readat == null);

        return new NotificationPanelViewModel
        {
            Items = items,
            Unread = unread,
            UnreadOnly = unreadOnly,
            HasMore = hasMore,
            Chats = await GetUnreadChatsAsync(userId)
        };
    }

    // Okunmamış = başkasının yazdığı, sahibi olduğum/katıldığım yayındaki etkinlikteki,
    // son okuma zamanımdan sonra gelen mesajlar. Okuma kaydı hiç yoksa hepsi okunmamış sayılır.
    private Task<List<UnreadEventItem>> GetUnreadChatsAsync(int userId) =>
        _context.Messages
            .AsNoTracking()
            .Where(m => m.Senderid != userId
                && m.Event.Isapproved
                && (m.Event.Createdbyuserid == userId || m.Event.Users.Any(u => u.Userid == userId))
                && !_context.Eventreadstatuses.Any(r =>
                    r.Userid == userId && r.Eventid == m.Eventid && r.Lastreadat >= m.Senttime))
            .GroupBy(m => new { m.Eventid, m.Event.Publicid, m.Event.Eventname })
            .Select(g => new UnreadEventItem
            {
                Eventid = g.Key.Eventid,
                Publicid = g.Key.Publicid,
                Eventname = g.Key.Eventname,
                Count = g.Count()
            })
            .OrderByDescending(x => x.Count)
            .ToListAsync();

    private Task PingAsync(int userId) =>
        _hub.Clients.User(userId.ToString()).SendAsync("NotificationsChanged");
}
