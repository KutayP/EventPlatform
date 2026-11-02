using System;

namespace EventPlatform.Web.Models.Entities;

public partial class Notification
{
    public int Notificationid { get; set; }

    // Bildirimi alan kullanıcı
    public int Userid { get; set; }

    public string Type { get; set; } = null!;

    // İşlemi yapan kullanıcı (örn. katılan ya da istek gönderen)
    public int? Actorid { get; set; }

    public int? Eventid { get; set; }

    public DateTime Createdat { get; set; }

    public DateTime? Readat { get; set; }

    public virtual User User { get; set; } = null!;

    public virtual User? Actor { get; set; }

    public virtual Event? Event { get; set; }
}
