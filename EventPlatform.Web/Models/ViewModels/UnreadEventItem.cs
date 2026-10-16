namespace EventPlatform.Web.Models.ViewModels;

public class UnreadEventItem
{
    public int Eventid { get; set; }
    public string Eventname { get; set; } = "";
    public int Count { get; set; }
}