using System;
using System.Collections.Generic;

namespace EventPlatform.Web.Models.Entities;

public partial class User
{
    public int Userid { get; set; }

    public string Username { get; set; } = null!;

    public string Password { get; set; } = null!;

    public string Email { get; set; } = null!;

    public string? Location { get; set; }

    public string Firstname { get; set; } = null!;

    public string Lastname { get; set; } = null!;

    public DateTime Birthdate { get; set; }

    public string Gender { get; set; } = null!;

    public string? Phonenumber { get; set; }

    public string? Profilepicture { get; set; }

    public string Role { get; set; } = null!;

    public double? Latitude { get; set; }

    public double? Longitude { get; set; }

    public virtual ICollection<Eventparticipationrequest> EventparticipationrequestForuserNavigations { get; set; } = new List<Eventparticipationrequest>();

    public virtual ICollection<Eventparticipationrequest> EventparticipationrequestInvitedbyNavigations { get; set; } = new List<Eventparticipationrequest>();

    public virtual ICollection<Eventreadstatus> Eventreadstatuses { get; set; } = new List<Eventreadstatus>();

    public virtual ICollection<Event> Events { get; set; } = new List<Event>();

    public virtual ICollection<Message> Messages { get; set; } = new List<Message>();

    public virtual ICollection<Point> Points { get; set; } = new List<Point>();

    public virtual ICollection<Userinterest> Userinterests { get; set; } = new List<Userinterest>();

    public virtual ICollection<Event> EventsNavigation { get; set; } = new List<Event>();
}
