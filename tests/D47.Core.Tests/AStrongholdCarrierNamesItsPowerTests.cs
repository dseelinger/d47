using Xunit;
using D47.Core.Capabilities.Builtin;
using D47.Core.Knowledge;

namespace D47.Core.Tests;

public sealed class AStrongholdCarrierNamesItsPowerTests
{
    [Fact]
    public void AStationAnswerGivesAStrongholdCarriersPowerAfterItsSystem()
    {
        Assert.True(
            StationQuery.TryParse(new StationSearch { StationTypes = "Stronghold carrier" }, out var query, out var failure),
            failure);

        var result = new StationSearchResult(
            "Sol",
            1,
            [new StationSummary
            {
                Name = "Stronghold Carrier",
                SystemName = "Somewhere",
                ControllingPower = "Zemina Torval",
                PowerState = "Stronghold",
            }]);

        Assert.Contains(
            "Stronghold Carrier in Somewhere (Zemina Torval)",
            GalaxyCapability.DescribeStations(result, query),
            StringComparison.Ordinal);
    }
}
