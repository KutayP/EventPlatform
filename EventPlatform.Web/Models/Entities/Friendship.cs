using System;
using System.Collections.Generic;

namespace EventPlatform.Web.Models.Entities;

public partial class Friendship
{
    public int Friendshipid { get; set; }

    public int Requesterid { get; set; }

    public int Addresseeid { get; set; }

    public string Status { get; set; } = null!;

    public DateTime Createdat { get; set; }

    public virtual User Addressee { get; set; } = null!;

    public virtual User Requester { get; set; } = null!;
}
