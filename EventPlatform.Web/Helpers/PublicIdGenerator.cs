using System.Security.Cryptography;

namespace EventPlatform.Web.Helpers;

// Etkinliğin adreste görünen kısa kodu: 8 karakter, küçük harf ve rakam (örn. k7x2m9ab).
// Tahmin edilmesi zor olsun diye kriptografik rastgele sayı üreticisi kullanılır.
public static class PublicIdGenerator
{
    public const int CodeLength = 8;
    private const string Alphabet = "abcdefghijklmnopqrstuvwxyz0123456789";

    public static string New()
    {
        Span<char> chars = stackalloc char[CodeLength];
        for (var i = 0; i < chars.Length; i++)
            chars[i] = Alphabet[RandomNumberGenerator.GetInt32(Alphabet.Length)];

        return new string(chars);
    }

    public static bool IsValid(string? value)
    {
        if (value is not { Length: CodeLength })
            return false;

        foreach (var c in value)
        {
            if (!(c is >= 'a' and <= 'z' or >= '0' and <= '9'))
                return false;
        }

        return true;
    }
}
