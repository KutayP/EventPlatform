using EventPlatform.Web.Helpers;

namespace EventPlatform.Tests;

public class GeoHelperTests
{
    [Fact]
    public void DistanceKm_IsZeroForSamePoint()
    {
        Assert.Equal(0, GeoHelper.DistanceKm(40.7654, 29.9408, 40.7654, 29.9408), precision: 6);
    }

    [Fact]
    public void DistanceKm_OneDegreeOfLatitudeIsAbout111Km()
    {
        Assert.InRange(GeoHelper.DistanceKm(0, 0, 1, 0), 111.0, 111.4);
    }

    [Fact]
    public void DistanceKm_IstanbulToAnkaraIsAbout350Km()
    {
        var km = GeoHelper.DistanceKm(41.0082, 28.9784, 39.9334, 32.8597);

        Assert.InRange(km, 345, 355);
    }

    [Fact]
    public void DistanceKm_IsSymmetric()
    {
        var a = GeoHelper.DistanceKm(41.0082, 28.9784, 39.9334, 32.8597);
        var b = GeoHelper.DistanceKm(39.9334, 32.8597, 41.0082, 28.9784);

        Assert.Equal(a, b, precision: 9);
    }
}
