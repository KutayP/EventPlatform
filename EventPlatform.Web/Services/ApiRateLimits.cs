namespace EventPlatform.Web.Services;

// İstek hızı sınırı politikalarının adları (Program.cs'te tanımlanır).
public static class ApiRateLimits
{
    // Şifre deneme: IP başına dakikada 10 istek.
    public const string Auth = "auth";
}
