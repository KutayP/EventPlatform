using System;
using System.Collections.Generic;

namespace EventPlatform.Web.Models.Entities;

public partial class Message
{
    public int Messageid { get; set; }

    public int Senderid { get; set; }

    public int Eventid { get; set; }

    public string Messagetext { get; set; } = null!;

    public DateTime? Senttime { get; set; }

    public virtual Event Event { get; set; } = null!;

    public virtual User Sender { get; set; } = null!;
}
