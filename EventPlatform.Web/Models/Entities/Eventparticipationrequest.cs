using System;
using System.Collections.Generic;

namespace EventPlatform.Web.Models.Entities;

public partial class Eventparticipationrequest
{
    public int Requestid { get; set; }

    public int Forevent { get; set; }

    public int Foruser { get; set; }

    public string Type { get; set; } = null!;

    public string Status { get; set; } = null!;

    public int? Invitedby { get; set; }

    public DateTime? Time { get; set; }

    public virtual Event ForeventNavigation { get; set; } = null!;

    public virtual User ForuserNavigation { get; set; } = null!;

    public virtual User? InvitedbyNavigation { get; set; }
}
