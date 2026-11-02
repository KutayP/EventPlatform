namespace EventPlatform.Web.Helpers;

public static class DisplayNameHelper
{
    // "Deniz Akay" -> "Deniz A." (başkalarına tam soyad göstermiyoruz)
    public static string Short(string firstName, string lastName)
    {
        var first = firstName.Trim();
        var last = lastName.Trim();
        return last.Length == 0 ? first : $"{first} {char.ToUpperInvariant(last[0])}.";
    }
}
