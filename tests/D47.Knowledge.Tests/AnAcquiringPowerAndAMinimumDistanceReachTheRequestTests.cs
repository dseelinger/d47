using System.Text.Json;
using D47.Core.Knowledge;
using Xunit;

namespace D47.Knowledge.Tests;

/// <summary>acquiring_power is Spansh's power field, and a station minimum distance is written as the min (#2093).</summary>
public class AnAcquiringPowerAndAMinimumDistanceReachTheRequestTests
{
    private static readonly Dictionary<string, string> Asked = new(StringComparer.Ordinal)
    {
        ["acquiring_power"] = "Aisling Duval",
        ["power_state"] = "Unoccupied",
    };

    [Fact]
    public void SystemsWritePower()
    {
        Assert.True(GalaxyQuery.TryParse("Sol", Asked, 5, out var query, out var failure), failure);

        var filters = JsonDocument.Parse(SpanshRequest.Search(query)).RootElement.GetProperty("filters");

        Assert.Equal("Aisling Duval", filters.GetProperty("power").GetProperty("value")[0].GetString());
        Assert.Equal("Unoccupied", filters.GetProperty("power_state").GetProperty("value")[0].GetString());
        Assert.False(filters.TryGetProperty("controlling_power", out _));
    }

    [Fact]
    public void StationsWriteSystemPowerAndTheMinimumDistance()
    {
        Assert.True(
            StationQuery.TryParse(
                new StationSearch { ReferenceSystem = "Sol", MinDistance = 200, Filters = Asked },
                out var query,
                out var failure),
            failure);

        var filters = JsonDocument.Parse(SpanshRequest.Stations(query)).RootElement.GetProperty("filters");

        Assert.Equal("Aisling Duval", filters.GetProperty("system_power").GetProperty("value")[0].GetString());
        Assert.Equal("200", filters.GetProperty("distance").GetProperty("min").GetString());
        Assert.NotEqual("50", filters.GetProperty("distance").GetProperty("max").GetString());
    }

    [Fact]
    public void AMinimumAtOrPastTheMaximumIsRefused()
    {
        Assert.False(StationQuery.TryParse(
            new StationSearch { ReferenceSystem = "Sol", MinDistance = 200, MaxDistance = 100, Filters = Asked },
            out _,
            out var failure));

        Assert.Contains("min_distance", failure);
    }

    [Fact]
    public void ASystemReadsItsAcquiringPowersAndProgress()
    {
        var body = """{"results":[{"name":"X","power_conflict_progress":[{"power":"Aisling Duval","progress":0.25}]}],"count":1}""";

        var result = SpanshResponse.ReadSearch(JsonDocument.Parse(body));

        var conflict = Assert.Single(Assert.Single(result.Systems).Acquiring);
        Assert.Equal("Aisling Duval", conflict.Power);
        Assert.Equal(0.25, conflict.Progress);
    }
}
