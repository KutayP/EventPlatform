using EventPlatform.Web.Helpers;

namespace EventPlatform.Tests;

public class PublicIdGeneratorTests
{
    [Fact]
    public void New_ReturnsValidEightCharacterCode()
    {
        var id = PublicIdGenerator.New();

        Assert.Equal(PublicIdGenerator.CodeLength, id.Length);
        Assert.True(PublicIdGenerator.IsValid(id));
    }

    [Fact]
    public void New_ProducesDifferentCodes()
    {
        // 36^8 olasılıkta 1000 kodun çakışma ihtimali yok denecek kadar küçük.
        var ids = Enumerable.Range(0, 1000).Select(_ => PublicIdGenerator.New()).ToHashSet();

        Assert.Equal(1000, ids.Count);
    }

    [Theory]
    [InlineData("k7x2m9ab")]
    [InlineData("00000000")]
    [InlineData("zzzzzzzz")]
    public void IsValid_AcceptsLowercaseLettersAndDigits(string value)
    {
        Assert.True(PublicIdGenerator.IsValid(value));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("abc")]
    [InlineData("abcdefghi")]
    [InlineData("ABCDEFGH")]
    [InlineData("abcd-efg")]
    [InlineData("abcdéfgh")]
    public void IsValid_RejectsEverythingElse(string? value)
    {
        Assert.False(PublicIdGenerator.IsValid(value));
    }
}
