namespace EventPlatform.Web.Models.ViewModels;

// Kayıt ve profil formlarının ortak karakter kuralları.
// Aynı desen hem sunucuda (.NET) hem tarayıcıda (JS) çalışır; bu yüzden \p{L} yerine açık aralıklar kullanılır.
public static class ValidationPatterns
{
    // A-Z, a-z ve Latin harfler (ç, ğ, ı, İ, ö, ş, ü, é, ñ ...). × ve ÷ hariç.
    private const string Letter = @"A-Za-z\u00C0-\u00D6\u00D8-\u00F6\u00F8-\u024F";

    // Ad/soyad: harf bloklarından oluşur, bloklar boşluk, ' , ’ veya - ile ayrılabilir.
    // Örn: "Ali Veli", "O'Brien", "Anne-Marie". Baştaki/sondaki boşluk serbest (sunucu Trim eder).
    public const string PersonName = @"^ *[" + Letter + @"]+(?:[ '\u2019-][" + Letter + @"]+)* *$";

    public const string PersonNameMessage =
        "Sadece harfler ve belirtilen özel karakterler kullanılabilir. İzin verilen özel karakterler: boşluk, kesme işareti (') ve tire (-).";

    // Kullanıcı adı: harf/rakamla başlar; harf, rakam, nokta, alt çizgi ve tire içerebilir.
    public const string Username = @"^ *[A-Za-z0-9][A-Za-z0-9._-]* *$";

    public const string UsernameMessage =
        "Sadece harfler (a-z), rakamlar ve belirtilen özel karakterler kullanılabilir. İzin verilen özel karakterler: nokta (.), alt çizgi (_) ve tire (-). Harf veya rakamla başlamalı.";
}