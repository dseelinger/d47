using D47.Core.Knowledge;
using Xunit;

namespace D47.Core.Tests.Knowledge;

public class ABrokerInAColonisedSystemIsHumanTests
{
    [Theory]
    [InlineData(true, true, "Guardian", "Human")]
    [InlineData(true, true, "Human", "Human")]
    [InlineData(true, true, null, "Human")]
    [InlineData(false, true, "Guardian", null)]
    [InlineData(false, true, null, null)]
    [InlineData(true, false, "Guardian", "Guardian")]
    [InlineData(true, false, null, null)]
    [InlineData(true, null, "Guardian", "Guardian")]
    [InlineData(true, null, null, null)]
    public void TheTypeFollowsTheColonisedFlag(bool hasBroker, bool? colonised, string? reported, string? expected) =>
        Assert.Equal(expected, StationQuery.BrokerTypeAt(hasBroker, colonised, reported));
}
