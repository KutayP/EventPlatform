namespace EventPlatform.Web.Models.ViewModels;

public class EventDetailsViewModel
{
    public int Eventid { get; set; }
    public string Eventname { get; set; } = "";
    public string Description { get; set; } = "";
    public DateTime Date { get; set; }
    public int Duration { get; set; }
    public string Location { get; set; } = "";
    public string Category { get; set; } = "";
    public string CreatorUsername { get; set; } = "";
    public int ParticipantCount { get; set; }

    public bool IsCreator { get; set; }
    public bool IsParticipant { get; set; }
    public bool IsPast { get; set; }

    public int CurrentUserId { get; set; }
    public bool CanChat => IsCreator || IsParticipant;
    public List<ChatMessageViewModel> Messages { get; set; } = new();
}