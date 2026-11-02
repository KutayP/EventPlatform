namespace EventPlatform.Web.Models.ViewModels;

public class HomeViewModel
{
    public int UpcomingEventCount { get; set; }
    public int MemberCount { get; set; }

    // Giriş yapmış kullanıcıya "arkadaşlarının etkinlikleri" bölümü gösterilir.
    public bool IsAuthenticated { get; set; }
    // Önümüzdeki günlerde oluşturduğum ya da katıldığım etkinlikler (hatırlatma).
    public List<EventListItemViewModel> MyUpcoming { get; set; } = new();

    public List<EventListItemViewModel> FriendsCreated { get; set; } = new();
    public List<EventListItemViewModel> FriendsJoined { get; set; } = new();
    public List<EventListItemViewModel> Upcoming { get; set; } = new();
}