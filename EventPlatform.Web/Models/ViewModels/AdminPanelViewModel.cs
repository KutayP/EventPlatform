namespace EventPlatform.Web.Models.ViewModels;

public class AdminUserRow
{
    public int Userid { get; set; }
    public string Username { get; set; } = null!;
    public string Email { get; set; } = null!;
    public string Role { get; set; } = null!;
}

public class AdminEventRow
{
    public int Eventid { get; set; }
    public string Publicid { get; set; } = null!;
    public string Eventname { get; set; } = null!;
    public string Description { get; set; } = null!;
    public DateTime Date { get; set; }
    public bool Isapproved { get; set; }
    public string CreatorUsername { get; set; } = null!;
}

public class AdminPanelViewModel
{
    public List<AdminUserRow> Users { get; set; } = new();
    public List<AdminEventRow> Events { get; set; } = new();
}