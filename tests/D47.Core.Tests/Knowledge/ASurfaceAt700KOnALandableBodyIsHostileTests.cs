using D47.Core.Knowledge;
using Xunit;

namespace D47.Core.Tests.Knowledge;

public class ASurfaceAt700KOnALandableBodyIsHostileTests
{
    [Theory]
    [InlineData(699.0, true, false)]
    [InlineData(700.0, true, true)]
    [InlineData(1315.0, false, false)]
    [InlineData(null, true, false)]
    public void OnlyALandableBodyAtOrAboveTheThresholdIsHostile(double? kelvin, bool landable, bool hostile)
    {
        Assert.Equal(hostile, SurfaceHeat.IsHostile(kelvin, landable));
    }
}
