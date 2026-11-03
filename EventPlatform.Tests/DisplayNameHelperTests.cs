using EventPlatform.Web.Helpers;

namespace EventPlatform.Tests;

public class DisplayNameHelperTests
{
    [Theory]
    [InlineData("Deniz", "Akay", "Deniz A.")]
    [InlineData("  Deniz ", "  akay", "Deniz A.")]
    [InlineData("Ömer", "şahin", "Ömer Ş.")]
    [InlineData("Deniz", "", "Deniz")]
    public void Short_ShowsFirstNameAndLastInitialOnly(string first, string last, string expected)
    {
        Assert.Equal(expected, DisplayNameHelper.Short(first, last));
    }
}
