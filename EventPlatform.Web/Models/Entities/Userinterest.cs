using System;
using System.Collections.Generic;

namespace EventPlatform.Web.Models.Entities;

public partial class Userinterest
{
    public int Userid { get; set; }

    public int Interestid { get; set; }

    public DateTime Time { get; set; }

    public virtual Interest Interest { get; set; } = null!;

    public virtual User User { get; set; } = null!;
}
