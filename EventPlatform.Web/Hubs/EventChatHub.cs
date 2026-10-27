using System.Security.Claims;
using EventPlatform.Web.Models;
using EventPlatform.Web.Models.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace EventPlatform.Web.Hubs;

[Authorize]
public class EventChatHub : Hub
{
    private const int MaxMessageLength = 1000;

    // Hub metotlarının istemciye döndürdüğü sonuçlar. "Erişim yok" bir hata değil, beklenen bir durumdur
    // (ör. etkinlik yayından kaldırıldı), bu yüzden istisna fırlatmak yerine sonuç döndürülür.
    private const string Ok = "ok";
    private const string NoAccess = "no-access";
    private const string Invalid = "invalid";

    private readonly AppDbContext _context;

    public EventChatHub(AppDbContext context)
    {
        _context = context;
    }

    // Kimlik cookie'deki imzalı claim'den gelir, istemciden asla alınmaz.
    private int CurrentUserId =>
        int.Parse(Context.User!.FindFirstValue(ClaimTypes.NameIdentifier)!);

    private string CurrentUsername => Context.User!.Identity!.Name ?? "";

    private static string GroupName(int eventId) => $"event-{eventId}";

    private static DateTime NowForDb() =>
        DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified);

    // Sadece yayındaki etkinliğin sahibi ve katılımcıları sohbete girebilir.
    private Task<bool> CanChatAsync(int eventId, int userId) =>
        _context.Events.AnyAsync(e =>
            e.Eventid == eventId &&
            e.Isapproved &&
            (e.Createdbyuserid == userId || e.Users.Any(u => u.Userid == userId)));

    public async Task<string> JoinEvent(int eventId)
    {
        var userId = CurrentUserId;
        if (!await CanChatAsync(eventId, userId))
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, GroupName(eventId));
            return NoAccess;
        }

        await Groups.AddToGroupAsync(Context.ConnectionId, GroupName(eventId));
        await MarkReadInternalAsync(eventId, userId);
        return Ok;
    }

    public async Task<string> SendMessage(int eventId, string text)
    {
        text = (text ?? "").Trim();
        if (text.Length == 0 || text.Length > MaxMessageLength)
            return Invalid;

        var userId = CurrentUserId;
        if (!await CanChatAsync(eventId, userId))
        {
            // Erişimi kapanan bağlantı, canlı mesaj almaya devam etmesin.
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, GroupName(eventId));
            return NoAccess;
        }

        var sentAt = NowForDb();
        var message = new Message
        {
            Senderid = userId,
            Eventid = eventId,
            Messagetext = text,
            Senttime = sentAt
        };

        _context.Messages.Add(message);
        await _context.SaveChangesAsync();

        // Gönderen kendi mesajını okumuş sayılır.
        await MarkReadInternalAsync(eventId, userId);

        await Clients.Group(GroupName(eventId)).SendAsync("ReceiveMessage", new
        {
            id = message.Messageid,
            senderId = userId,
            sender = CurrentUsername,
            text = message.Messagetext,
            sentAt = DateTime.SpecifyKind(sentAt, DateTimeKind.Utc)
        });

        return Ok;
    }

    // İstemci sayfa açıkken yeni mesaj gelince okundu bilgisini günceller.
    public async Task<string> MarkRead(int eventId)
    {
        var userId = CurrentUserId;
        if (!await CanChatAsync(eventId, userId))
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, GroupName(eventId));
            return NoAccess;
        }

        await MarkReadInternalAsync(eventId, userId);
        return Ok;
    }

    // (userid, eventid) için "en son ne zaman okudu" değerini yazar (yoksa ekler).
    private async Task MarkReadInternalAsync(int eventId, int userId)
    {
        var now = NowForDb();

        var updated = await _context.Eventreadstatuses
            .Where(r => r.Userid == userId && r.Eventid == eventId)
            .ExecuteUpdateAsync(s => s.SetProperty(r => r.Lastreadat, now));

        if (updated == 0)
        {
            _context.Eventreadstatuses.Add(new Eventreadstatus
            {
                Userid = userId,
                Eventid = eventId,
                Lastreadat = now
            });

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateException)
            {
                // İki istek aynı anda satır eklemeye çalıştıysa diğeri kazanmıştır, sorun değil.
            }
        }
    }
}