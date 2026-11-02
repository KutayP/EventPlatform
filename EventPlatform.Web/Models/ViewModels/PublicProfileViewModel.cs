namespace EventPlatform.Web.Models.ViewModels;

// /u/{kullanici} sayfası. Burada e-posta, doğum tarihi, cinsiyet gibi özel bilgiler bilerek YOK.
public class PublicProfileViewModel
{
    public string Username { get; set; } = "";

    // "Deniz A." biçiminde: ad + soyadın baş harfi.
    public string DisplayName { get; set; } = "";

    // Profil içeriği bu ziyaretçiye gösterilsin mi?
    public bool IsVisible { get; set; }

    public bool IsOwner { get; set; }
    public bool IsAdminViewing { get; set; }
    public bool IsFriendsOnly { get; set; }

    // Ziyaretçinin bu kullanıcıyla arkadaşlık durumu (düğmeleri belirler).
    public FriendRelation Relation { get; set; } = FriendRelation.Anonymous;

    public int CreatedCount { get; set; }
    public int JoinedCount { get; set; }

    public List<string> Interests { get; set; } = new();
    public List<EventListItemViewModel> Created { get; set; } = new();
    public List<EventListItemViewModel> Joined { get; set; } = new();
}
