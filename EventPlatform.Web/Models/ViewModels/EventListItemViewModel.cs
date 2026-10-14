using EventPlatform.Web.Models.Entities;

namespace EventPlatform.Web.Models.ViewModels;

public class EventListItemViewModel
{
    public int Eventid { get; set; }
    public string Eventname { get; set; } = "";
    public DateTime Date { get; set; }
    public string Location { get; set; } = "";
    public string Category { get; set; } = "";

}


