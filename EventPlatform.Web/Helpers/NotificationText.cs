using EventPlatform.Web.Models;
using EventPlatform.Web.Models.ViewModels;

namespace EventPlatform.Web.Helpers;

// Bildirimin ekranda görünen metni ve simgesi. Metin kaydedilmez, her gösterimde türden üretilir.
public static class NotificationText
{
    public static string Text(NotificationItem n) => n.Type switch
    {
        NotificationTypes.EventJoined => $"{n.ActorName}, “{n.EventName}” etkinliğine katıldı.",
        NotificationTypes.FriendRequest => $"{n.ActorName} sana arkadaşlık isteği gönderdi.",
        NotificationTypes.FriendAccepted => $"{n.ActorName} arkadaşlık isteğini kabul etti.",
        NotificationTypes.EventRecommended => $"İlgi alanına uygun yeni etkinlik: “{n.EventName}”",
        _ => "Yeni bildirim"
    };

    public static string Icon(string type) => type switch
    {
        NotificationTypes.EventJoined => "bi-person-plus",
        NotificationTypes.FriendRequest => "bi-person-heart",
        NotificationTypes.FriendAccepted => "bi-people",
        NotificationTypes.EventRecommended => "bi-stars",
        _ => "bi-bell"
    };

    // "5 dk önce" gibi. Sunucuda hesaplanır; panel her açıldığında yenilendiği için güncel kalır.
    public static string Ago(DateTime utc)
    {
        var diff = DateTime.UtcNow - DateTime.SpecifyKind(utc, DateTimeKind.Utc);
        if (diff.TotalMinutes < 1) return "şimdi";
        if (diff.TotalMinutes < 60) return $"{(int)diff.TotalMinutes} dk önce";
        if (diff.TotalHours < 24) return $"{(int)diff.TotalHours} sa önce";
        if (diff.TotalDays < 7) return $"{(int)diff.TotalDays} gün önce";
        return utc.ToString("dd.MM.yyyy");
    }
}
