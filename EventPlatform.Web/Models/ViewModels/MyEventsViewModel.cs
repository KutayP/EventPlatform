namespace EventPlatform.Web.Models.ViewModels;

public class MyEventItemViewModel
{
    public int Eventid { get; set; }
    public string Publicid { get; set; } = "";
    public string Eventname { get; set; } = "";
    public DateTime Date { get; set; }
    public string Location { get; set; } = "";
    public bool Isapproved { get; set; }
    public int ParticipantCount { get; set; }
    public bool IsPast { get; set; }
}

public class MyEventsViewModel
{
    public List<MyEventItemViewModel> Created { get; set; } = new();
    public List<MyEventItemViewModel> Joined { get; set; } = new();
}