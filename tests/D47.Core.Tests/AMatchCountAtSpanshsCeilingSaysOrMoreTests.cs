using Xunit;
using D47.Core.Capabilities.Builtin;
using D47.Core.Knowledge;

namespace D47.Core.Tests;

public sealed class AMatchCountAtSpanshsCeilingSaysOrMoreTests
{
    private static StationSearchResult Stations(int total) => new(
        "Sol",
        total,
        [.. Enumerable.Range(0, 25).Select(i => new StationSummary { Name = $"Station {i}", SystemName = "Sol" })]);

    private static StationQuery StationSearch()
    {
        Assert.True(StationQuery.TryParse(new StationSearch { MinPad = "Large" }, out var query, out var failure), failure);
        return query;
    }

    private static StationQuery SellSearch()
    {
        Assert.True(StationQuery.TryParse("Sol", "Frame Shift Drive", "5", "A", null, false, 50, 25, out var query, out var failure), failure);
        return query;
    }

    [Fact]
    public void AStationSearchAtTheCeilingSaysTenThousandOrMore() =>
        Assert.StartsWith("10,000 or more stations matched;", GalaxyCapability.DescribeStations(Stations(10_000), StationSearch()));

    [Fact]
    public void AStationSearchBelowTheCeilingSaysTheExactCount() =>
        Assert.StartsWith("445 stations matched;", GalaxyCapability.DescribeStations(Stations(445), StationSearch()));

    [Fact]
    public void ASellSearchAtTheCeilingSaysTenThousandOrMore() =>
        Assert.Contains("10,000 or more stations sell", GalaxyCapability.Describe(Stations(10_000), SellSearch()));

    [Fact]
    public void ASellSearchBelowTheCeilingSaysTheExactCount() =>
        Assert.Contains("445 stations sell", GalaxyCapability.Describe(Stations(445), SellSearch()));

    [Fact]
    public void ABodySearchAtTheCeilingSaysTenThousandOrMore()
    {
        Assert.True(BodyQuery.TryParse(new BodyRequest { Landable = true }, out var query, out var failure), failure);
        var bodies = Enumerable.Range(0, 25).Select(i => new BodySummary { Name = $"Body {i}", SystemName = "Sol" }).ToList();

        Assert.StartsWith("10,000 or more bodies matched;", GalaxyCapability.Describe(new BodySearchResult("Sol", 10_000, bodies), query));
        Assert.StartsWith("445 bodies matched;", GalaxyCapability.Describe(new BodySearchResult("Sol", 445, bodies), query));
    }

    [Fact]
    public void ASystemSearchAtTheCeilingSaysTenThousandOrMore()
    {
        Assert.True(GalaxyQuery.TryParse("Sol", new Dictionary<string, string>(), 25, out var query, out var failure), failure);
        var systems = Enumerable.Range(0, 25).Select(i => new SystemSummary { Name = $"System {i}" }).ToList();

        Assert.StartsWith("10,000 or more systems matched;", GalaxyCapability.Describe(new GalaxySearchResult("Sol", 10_000, systems), query));
        Assert.StartsWith("445 systems matched;", GalaxyCapability.Describe(new GalaxySearchResult("Sol", 445, systems), query));
    }
}
