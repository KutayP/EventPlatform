using System.Security.Claims;

namespace EventPlatform.Web.Helpers;

// Herkese açık demo hesabı (README'de kullanıcı adı ve şifresi verilir).
// Bu hesapla şifre ve kişisel bilgiler değiştirilemez, örnek veriden gelen etkinlikler düzenlenemez ve silinemez;
// böylece ziyaretçiler hesabı ele geçirip başkalarını dışarıda bırakamaz ya da örnek veriyi bozamaz.
// Ziyaretçinin kendi oluşturduğu etkinlikler serbestçe düzenlenip silinebilir.
public static class DemoAccount
{
    public const string Username = "demo";

    public const string BlockedMessage = "Bu işlem demo hesabında kapalı.";

    public const string SampleEventMessage =
        "Örnek etkinlikler demo hesabında düzenlenemez ve silinemez. Kendi oluşturduğun etkinlikleri düzenleyip silebilirsin.";

    public static bool IsDemo(ClaimsPrincipal? user) =>
        user?.Identity?.IsAuthenticated == true &&
        string.Equals(user.Identity.Name, Username, StringComparison.OrdinalIgnoreCase);
}
