namespace EventPlatform.Web.Models.ViewModels;

public class HomeViewModel
{
    public int UpcomingEventCount { get; set; }
    public int MemberCount { get; set; }
    public List<EventListItemViewModel> Upcoming { get; set; } = new();
}