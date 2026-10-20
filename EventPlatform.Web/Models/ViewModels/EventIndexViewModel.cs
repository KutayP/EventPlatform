namespace EventPlatform.Web.Models.ViewModels;

public class EventIndexViewModel
{
    public List<EventListItemViewModel> Events { get; set; } = new();
    public string? Query { get; set; }
    public string? Category { get; set; }
    public List<string> Categories { get; set; } = new();

    public bool HasFilter =>
        !string.IsNullOrWhiteSpace(Query) || !string.IsNullOrWhiteSpace(Category);
}