namespace EventPlatform.Web.Models.Entities;

// Kullanıcı başına ayarlar. Satır yoksa varsayılanlar geçerlidir.
public partial class Usersetting
{
    public int Userid { get; set; }

    public bool NotifyEventJoined { get; set; } = true;

    public bool NotifyFriends { get; set; } = true;

    public bool NotifyRecommended { get; set; }

    public int RadiusKm { get; set; } = 50;

    public string Theme { get; set; } = "system";

    public virtual User User { get; set; } = null!;
}
