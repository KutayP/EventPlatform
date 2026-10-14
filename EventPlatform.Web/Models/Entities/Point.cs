using System;
using System.Collections.Generic;

namespace EventPlatform.Web.Models.Entities;

public partial class Point
{
    public int Pointid { get; set; }

    public int Userid { get; set; }

    public int Amount { get; set; }

    public string Reason { get; set; } = null!;

    public int? Eventid { get; set; }

    public DateTime Earnedat { get; set; }

    public virtual Event? Event { get; set; }

    public virtual User User { get; set; } = null!;
}
