using System.Text;

namespace EventPlatform.Web.Helpers;

// "Kadıköy Sahil Fotoğraf Yürüyüşü" -> "kadikoy-sahil-fotograf-yuruyusu"
// Adres: /etkinlik/{slug}-{kısa kod}. Etkinliği bulan kısım sondaki koddur, slug yalnızca okunabilirlik içindir.
public static class SlugHelper
{
    private const string Fallback = "etkinlik";
    private const int MaxSlugLength = 60;

    public static string Slugify(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return Fallback;

        var sb = new StringBuilder(text.Length);
        var pendingDash = false;

        foreach (var ch in text)
        {
            // Normalize() kullanmıyoruz: ICU'nun kurulu olmadığı ortamlarda da aynı sonucu versin.
            var mapped = Transliterate(ch);
            var c = mapped ?? char.ToLowerInvariant(ch);

            if (c is >= 'a' and <= 'z' or >= '0' and <= '9')
            {
                if (pendingDash && sb.Length > 0)
                    sb.Append('-');

                pendingDash = false;
                sb.Append(c);
            }
            else
            {
                pendingDash = true;
            }
        }

        var slug = sb.ToString();
        if (slug.Length > MaxSlugLength)
            slug = slug[..MaxSlugLength].TrimEnd('-');

        return slug.Length == 0 ? Fallback : slug;
    }

    // Adrese yazılacak değer: "kadikoy-sahil-fotograf-yuruyusu-k7x2m9ab"
    public static string Compose(string? eventName, string publicId) =>
        $"{Slugify(eventName)}-{publicId}";

    // Adresten slug ve kısa kodu ayırır. Kod geçersizse PublicId null döner.
    public static (string Slug, string? PublicId) Split(string? slugId)
    {
        if (string.IsNullOrWhiteSpace(slugId))
            return ("", null);

        var value = slugId.Trim().ToLowerInvariant();
        var dash = value.LastIndexOf('-');

        var slug = dash >= 0 ? value[..dash] : "";
        var id = dash >= 0 ? value[(dash + 1)..] : value;

        return (slug, PublicIdGenerator.IsValid(id) ? id : null);
    }

    private static char? Transliterate(char c) => c switch
    {
        'ç' or 'Ç' => 'c',
        'ğ' or 'Ğ' => 'g',
        'ı' or 'İ' => 'i',
        'ö' or 'Ö' => 'o',
        'ş' or 'Ş' => 's',
        'ü' or 'Ü' => 'u',
        'â' or 'Â' => 'a',
        'î' or 'Î' => 'i',
        'û' or 'Û' => 'u',
        _ => null
    };
}
