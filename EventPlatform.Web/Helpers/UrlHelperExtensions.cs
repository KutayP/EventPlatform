using Microsoft.AspNetCore.Mvc;

namespace EventPlatform.Web.Helpers;

public static class UrlHelperExtensions
{
    // Razor'da: <a href="@Url.EventUrl(Model.Eventname, Model.Publicid)">
    public static string EventUrl(this IUrlHelper url, string eventName, string publicId) =>
        url.Action("Details", "Events", new { slugId = SlugHelper.Compose(eventName, publicId) }) ?? "#";
}
