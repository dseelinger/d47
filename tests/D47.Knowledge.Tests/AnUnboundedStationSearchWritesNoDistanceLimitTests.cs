using System.Text.Json;
using D47.Core.Knowledge;
using Xunit;

namespace D47.Knowledge.Tests;

/// <summary>A station search can ask for the whole galaxy, and every other search keeps its radius (#602).</summary>
public class AnUnboundedStationSearchWritesNoDistanceLimitTests
{
    private static StationQuery Query()
    {
        Assert.True(StationQuery.TryParse(
            "Sol", "Enhanced Performance Thrusters", null, null, null, false, 50, 1, out var query, out _));
        return query;
    }

    private static string DistanceMax(StationQuery query)
    {
        using var body = JsonDocument.Parse(SpanshRequest.Stations(query));

        return body.RootElement.GetProperty("filters").GetProperty("distance").GetProperty("max").GetString()!;
    }

    [Fact]
    public void AnUnboundedQueryWritesTheUnboundedMaximum()
    {
        var max = double.Parse(DistanceMax(Query() with { Unbounded = true }), System.Globalization.CultureInfo.InvariantCulture);

        Assert.True(max >= 1_000_000_000);
    }

    [Fact]
    public void ABoundedQueryStillWritesItsRadius()
    {
        Assert.Equal("50", DistanceMax(Query()));
    }
}
