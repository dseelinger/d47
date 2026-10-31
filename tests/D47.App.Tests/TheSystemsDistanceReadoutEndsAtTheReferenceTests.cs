using D47.App.Panel;
using Xunit;

namespace D47.App.Tests;

/// <summary>The Systems distance readout names the range and the reference system and says nothing about order (#1994).</summary>
public sealed class TheSystemsDistanceReadoutEndsAtTheReferenceTests
{
    [Theory]
    [InlineData(10.0, 50.0, "Systems 10–50 LY from ")]
    [InlineData(null, 50.0, "Systems within 50 LY of ")]
    [InlineData(10.0, null, "Systems beyond 10 LY of ")]
    public void EveryRangeEndsWithAFullStopAfterTheReference(double? low, double? high, string lead)
    {
        var words = RangeReadout.Distance(low, high, "Anlave");

        Assert.NotNull(words);
        Assert.Equal(lead, words.Value.Lead);
        Assert.Equal("ANLAVE", words.Value.Reference);
        Assert.Equal(".", words.Value.Tail);
    }

    [Fact]
    public void NoRangeGivesNoReadout() => Assert.Null(RangeReadout.Distance(null, null, "Sol"));
}
