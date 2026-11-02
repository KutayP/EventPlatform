using System.Text;

namespace EventPlatform.Web.Helpers;

// Arama metnini sadeleştirir: "@Kaan_Y.", "kaan y", "KAAN-Y" -> "kaan y".
// Türkçe harfler ASCII'ye çevrilir (Çağrı -> cagri), diğer her şey boşluk sayılır.
// Veritabanındaki arama sorgusu da aynı kuralla (translate/regexp_replace) metni sadeleştirir.
public static class SearchText
{
    public static string Normalize(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return "";

        var sb = new StringBuilder(text.Length);
        var pendingSpace = false;

        foreach (var ch in text)
        {
            var c = Fold(ch);

            if (c is >= 'a' and <= 'z' or >= '0' and <= '9')
            {
                if (pendingSpace && sb.Length > 0)
                    sb.Append(' ');

                pendingSpace = false;
                sb.Append(c);
            }
            else
            {
                pendingSpace = true;
            }
        }

        return sb.ToString();
    }

    private static char Fold(char c) => c switch
    {
        'ç' or 'Ç' => 'c',
        'ğ' or 'Ğ' => 'g',
        'ı' or 'İ' or 'I' => 'i',
        'ö' or 'Ö' => 'o',
        'ş' or 'Ş' => 's',
        'ü' or 'Ü' => 'u',
        'â' or 'Â' => 'a',
        'î' or 'Î' => 'i',
        'û' or 'Û' => 'u',
        _ => char.ToLowerInvariant(c)
    };
}
