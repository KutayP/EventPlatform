namespace EventPlatform.Web.Models.ViewModels;

// friendships.status sütununun değerleri (veritabanında CHECK kısıtı da var).
public static class FriendshipStatus
{
    public const string Pending = "pending";
    public const string Accepted = "accepted";
}

// Bir ziyaretçinin, baktığı kullanıcıyla ilişkisi.
public enum FriendRelation
{
    Anonymous,       // giriş yapmamış
    Self,            // kendi profili
    None,            // hiçbir kayıt yok
    RequestSent,     // ben istek gönderdim, bekliyor
    RequestReceived, // bana istek gönderdi, bekliyor
    Friends
}

// _FriendAction bölümünün modeli: hangi kullanıcıya, hangi durumda hangi düğmeler gösterilsin.
public record FriendActionModel(string Username, FriendRelation Relation);

// Arkadaşlar sayfasındaki bir satır.
public record FriendRow(string Username, string DisplayName, FriendRelation Relation);

public class FriendsIndexViewModel
{
    public List<FriendRow> Incoming { get; set; } = new();
    public List<FriendRow> Outgoing { get; set; } = new();
    public List<FriendRow> Friends { get; set; } = new();

    public string? Query { get; set; }
    public bool QueryTooShort { get; set; }

    // null: arama yapılmadı
    public List<FriendRow>? Results { get; set; }
}

// Kullanıcı aramasının ham sonucu (SQL sütun adlarıyla birebir eşleşir).
public class UserSearchHit
{
    public int Userid { get; set; }
    public string Username { get; set; } = "";
    public string Firstname { get; set; } = "";
    public string Lastname { get; set; } = "";
    public double Score { get; set; }
}
