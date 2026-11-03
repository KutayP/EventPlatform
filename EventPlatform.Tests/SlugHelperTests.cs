using EventPlatform.Web.Helpers;

namespace EventPlatform.Tests;

public class SlugHelperTests
{
    [Theory]
    [InlineData("Kadıköy Sahil Fotoğraf Yürüyüşü", "kadikoy-sahil-fotograf-yuruyusu")]
    [InlineData("ÇĞİÖŞÜ çğıöşü", "cgiosu-cgiosu")]
    [InlineData("  Hello,   World!! ", "hello-world")]
    [InlineData("C# & .NET 10", "c-net-10")]
    [InlineData("Âşık Veysel Gecesi", "asik-veysel-gecesi")]
    public void Slugify_ProducesReadableAsciiSlug(string input, string expected)
    {
        Assert.Equal(expected, SlugHelper.Slugify(input));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("!!! ???")]
    public void Slugify_FallsBackWhenNothingUsable(string? input)
    {
        Assert.Equal("etkinlik", SlugHelper.Slugify(input));
    }

    [Fact]
    public void Slugify_LimitsLengthWithoutTrailingDash()
    {
        var longName = string.Join(" ", Enumerable.Repeat("uzun kelime", 20));

        var slug = SlugHelper.Slugify(longName);

        Assert.True(slug.Length <= 60);
        Assert.False(slug.EndsWith('-'));
    }

    [Fact]
    public void Compose_AppendsPublicId()
    {
        Assert.Equal("kadikoy-sahil-k7x2m9ab", SlugHelper.Compose("Kadıköy Sahil", "k7x2m9ab"));
    }

    [Fact]
    public void Split_SeparatesSlugAndPublicId()
    {
        var (slug, publicId) = SlugHelper.Split("kadikoy-sahil-k7x2m9ab");

        Assert.Equal("kadikoy-sahil", slug);
        Assert.Equal("k7x2m9ab", publicId);
    }

    [Fact]
    public void Split_AcceptsCodeWithoutSlugAndIgnoresCase()
    {
        var (slug, publicId) = SlugHelper.Split("K7X2M9AB");

        Assert.Equal("", slug);
        Assert.Equal("k7x2m9ab", publicId);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("etkinlik-kisa")]       // kod 8 karakter değil
    [InlineData("etkinlik-abc_defg")]   // geçersiz karakter
    public void Split_ReturnsNullPublicIdForInvalidInput(string? input)
    {
        Assert.Null(SlugHelper.Split(input).PublicId);
    }

    [Fact]
    public void ComposeThenSplit_RoundTripsPublicId()
    {
        var id = PublicIdGenerator.New();

        var (_, publicId) = SlugHelper.Split(SlugHelper.Compose("Sahilde yürüyüş - 2. hafta", id));

        Assert.Equal(id, publicId);
    }
}
