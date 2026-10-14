using System;
using System.Collections.Generic;

namespace EventPlatform.Web.Models.Entities;

public partial class Event
{
    public int Eventid { get; set; }

    public int Createdbyuserid { get; set; }

    public string Eventname { get; set; } = null!;

    public string Description { get; set; } = null!;

    public DateTime Date { get; set; }

    public int Duration { get; set; }

    public string Location { get; set; } = null!;

    public string Category { get; set; } = null!;

    public double Latitude { get; set; }

    public double Longitude { get; set; }

    public bool Isapproved { get; set; }

    public virtual User Createdbyuser { get; set; } = null!;

    public virtual ICollection<Eventparticipationrequest> Eventparticipationrequests { get; set; } = new List<Eventparticipationrequest>();

    public virtual ICollection<Eventreadstatus> Eventreadstatuses { get; set; } = new List<Eventreadstatus>();

    public virtual ICollection<Message> Messages { get; set; } = new List<Message>();

    public virtual ICollection<Point> Points { get; set; } = new List<Point>();

    public virtual ICollection<User> Users { get; set; } = new List<User>();
}
