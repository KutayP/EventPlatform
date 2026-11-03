using EventPlatform.Web.Models.ViewModels;

namespace EventPlatform.Tests;

public class ProfileVisibilitiesTests
{
    [Theory]
    [InlineData("public")]
    [InlineData("friends")]
    public void IsValid_AcceptsKnownValues(string value)
    {
        Assert.True(ProfileVisibilities.IsValid(value));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("Public")]
    [InlineData("private")]
    public void IsValid_RejectsUnknownValues(string? value)
    {
        Assert.False(ProfileVisibilities.IsValid(value));
    }
}
