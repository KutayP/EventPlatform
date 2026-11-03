using EventPlatform.Web.Helpers;
using EventPlatform.Web.Models;
using EventPlatform.Web.Models.ViewModels;

namespace EventPlatform.Tests;

public class NotificationTextTests
{
    private static NotificationItem Item(string type) => new()
    {
        Type = type,
        ActorName = "Deniz A.",
        EventName = "Sahil Yürüyüşü"
    };

    [Fact]
    public void Text_EventJoinedMentionsActorAndEvent()
    {
        var text = NotificationText.Text(Item(NotificationTypes.EventJoined));

        Assert.Contains("Deniz A.", text);
        Assert.Contains("Sahil Yürüyüşü", text);
        Assert.Contains("katıldı", text);
    }

    [Fact]
    public void Text_FriendRequestMentionsActor()
    {
        var text = NotificationText.Text(Item(NotificationTypes.FriendRequest));

        Assert.StartsWith("Deniz A.", text);
        Assert.Contains("arkadaşlık isteği", text);
    }

    [Fact]
    public void Text_RecommendedMentionsEvent()
    {
        Assert.Contains("Sahil Yürüyüşü", NotificationText.Text(Item(NotificationTypes.EventRecommended)));
    }

    [Fact]
    public void Text_UnknownTypeHasGenericText()
    {
        Assert.Equal("Yeni bildirim", NotificationText.Text(Item("something_else")));
    }

    [Fact]
    public void Icon_UnknownTypeFallsBackToBell()
    {
        Assert.Equal("bi-bell", NotificationText.Icon("something_else"));
    }

    [Fact]
    public void Ago_FormatsRelativeTime()
    {
        var now = DateTime.UtcNow;

        Assert.Equal("şimdi", NotificationText.Ago(now));
        Assert.Equal("5 dk önce", NotificationText.Ago(now.AddMinutes(-5).AddSeconds(-10)));
        Assert.Equal("3 sa önce", NotificationText.Ago(now.AddHours(-3).AddMinutes(-1)));
        Assert.Equal("2 gün önce", NotificationText.Ago(now.AddDays(-2).AddMinutes(-1)));
    }

    [Fact]
    public void Ago_ShowsDateForOlderThanAWeek()
    {
        var old = new DateTime(2025, 3, 14, 10, 0, 0, DateTimeKind.Unspecified);

        Assert.Equal("14.03.2025", NotificationText.Ago(old));
    }
}
