using System.Security.Claims;

namespace EventPlatform.Web.Helpers;

// Herkese açık demo hesabı (README'de kullanıcı adı ve şifresi verilir).
// Bu hesapla şifre, kişisel bilgiler ve etkinlik düzenleme/silme kapalıdır; böylece ziyaretçiler
// hesabı ele geçirip başkalarını dışarıda bırakamaz ya da örnek veriyi bozamaz.
public static class DemoAccount
{
    public const string Username = "demo";

    public const string BlockedMessage = "Bu işlem demo hesabında kapalı.";

    public static bool IsDemo(ClaimsPrincipal? user) =>
        user?.Identity?.IsAuthenticated == true &&
        string.Equals(user.Identity.Name, Username, StringComparison.OrdinalIgnoreCase);
}
