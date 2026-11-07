using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace EventPlatform.Web.Models.ViewModels;

public class NotificationItem
{
    public int Id { get; set; }
    public string Type { get; set; } = "";
    public string? ActorUsername { get; set; }
    public string ActorName { get; set; } = "Biri";
    public string? EventName { get; set; }
    public string? EventPublicid { get; set; }
    public DateTime CreatedAt { get; set; }     // UTC
    public bool IsRead { get; set; }
}

// Zil menüsü ve bildirimler sayfası için.
public class NotificationPanelViewModel
{
    public List<NotificationItem> Items { get; set; } = new();
    public int Unread { get; set; }

    // Okunmamış sohbet mesajları (etkinlik başına). Bildirim gibi saklanmaz, her seferinde hesaplanır.
    public List<UnreadEventItem> Chats { get; set; } = new();

    // Zildeki rozet: okunmamış bildirimler + okunmamış mesajı olan etkinlik sayısı.
    public int TotalUnread => Unread + Chats.Count;

    // Zil menüsü yalnızca okunmamışları gösterir; /notifications sayfası tüm geçmişi.
    public bool UnreadOnly { get; set; }

    public int Page { get; set; } = 1;
    public bool HasMore { get; set; }
}

// Ayarlar > Bildirimler
public class NotificationSettingsViewModel
{
    public bool NotifyEventJoined { get; set; } = true;
    public bool NotifyFriends { get; set; } = true;

    // İlgi alanıma uygun, yakınımdaki yeni etkinlikler (varsayılan kapalı)
    public bool NotifyRecommended { get; set; }

    // Sayfada uyarı göstermek için; formdan alınmaz.
    [BindNever]
    public bool HasLocation { get; set; }
}
