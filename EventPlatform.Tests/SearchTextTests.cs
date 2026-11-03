using EventPlatform.Web.Helpers;

namespace EventPlatform.Tests;

public class SearchTextTests
{
    [Theory]
    [InlineData("@Kaan_Y.", "kaan y")]
    [InlineData("kaan y", "kaan y")]
    [InlineData("KAAN-Y", "kaan y")]
    [InlineData("Çağrı", "cagri")]
    [InlineData("İSTANBUL", "istanbul")]
    [InlineData("ışık", "isik")]
    [InlineData("a__b--c", "a b c")]
    [InlineData("  boşluklu   arama  ", "bosluklu arama")]
    public void Normalize_FoldsTurkishAndPunctuation(string input, string expected)
    {
        Assert.Equal(expected, SearchText.Normalize(input));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("@@@ ___")]
    public void Normalize_ReturnsEmptyForNoSearchableText(string? input)
    {
        Assert.Equal("", SearchText.Normalize(input));
    }
}
