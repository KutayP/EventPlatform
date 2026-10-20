namespace EventPlatform.Web.Models.ViewModels;

// Kategori adı veritabanında İngilizce anahtar olarak durur; ekranda Türkçe etiket ve sabit renk gösterilir.
public static class CategoryStyle
{
    private static readonly Dictionary<string, (string Label, int Color)> Known =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["Music"] = ("Müzik", 0),
            ["Sports"] = ("Spor", 1),
            ["Reading"] = ("Okuma", 2),
            ["Art"] = ("Sanat", 3),
            ["Technology"] = ("Teknoloji", 4),
            ["Gaming"] = ("Oyun", 5),
            ["Travel"] = ("Gezi", 6),
            ["Cooking"] = ("Yemek", 7),
            ["Photography"] = ("Fotoğrafçılık", 8),
            ["Fitness"] = ("Fitness", 9)
        };

    // Ekranda gösterilecek ad. Tanımsız kategoriler olduğu gibi gösterilir.
    public static string Label(string? category) =>
        category != null && Known.TryGetValue(category, out var k) ? k.Label : category ?? "";

    // Rozet rengi (site.css içindeki cat-0 ... cat-9).
    public static string CssClass(string? category)
    {
        if (!string.IsNullOrEmpty(category) && Known.TryGetValue(category, out var k))
            return "cat-" + k.Color;

        // Sonradan eklenen kategoriler için kararlı bir renk (string.GetHashCode süreçten sürece değiştiği için kullanılmaz).
        var hash = 17;
        foreach (var c in (category ?? "").ToLowerInvariant())
            hash = unchecked(hash * 31 + c);

        return "cat-" + Math.Abs(hash % 10);
    }
}