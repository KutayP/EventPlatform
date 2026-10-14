using System;
using System.Collections.Generic;

namespace EventPlatform.Web.Models.Entities;

public partial class Interest
{
    public int Interestid { get; set; }

    public string Interestname { get; set; } = null!;

    public virtual ICollection<Userinterest> Userinterests { get; set; } = new List<Userinterest>();
}
