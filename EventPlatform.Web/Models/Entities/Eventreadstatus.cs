using System;
using System.Collections.Generic;

namespace EventPlatform.Web.Models.Entities;

public partial class Eventreadstatus
{
    public int Userid { get; set; }

    public int Eventid { get; set; }

    public DateTime? Lastreadat { get; set; }

    public virtual Event Event { get; set; } = null!;

    public virtual User User { get; set; } = null!;
}
