namespace EventPlatform.Web.Models;

// notifications.type sütununun alabileceği değerler (veritabanındaki CHECK ile aynı liste).
public static class NotificationTypes
{
    public const string EventJoined = "event_joined";
    public const string FriendRequest = "friend_request";
    public const string FriendAccepted = "friend_accepted";
    public const string EventRecommended = "event_recommended";
}
