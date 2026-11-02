using System.Diagnostics.CodeAnalysis;

namespace EventPlatform.Web.Models.ViewModels;

// users.profilevisibility sütununun izin verilen değerleri (veritabanında da CHECK kısıtı var).
public static class ProfileVisibilities
{
    public const string Public = "public";
    public const string Friends = "friends";

    public static bool IsValid([NotNullWhen(true)] string? value) =>
        value is Public or Friends;
}
