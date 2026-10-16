namespace EventPlatform.Web.Models.ViewModels;

public class ChatMessageViewModel
{
    public int Messageid { get; set; }
    public int SenderId { get; set; }
    public string SenderUsername { get; set; } = "";
    public string Text { get; set; } = "";
    public DateTime SentAt { get; set; } // UTC
}