using System.Text.Json;
using D47.Core.Knowledge;
using Xunit;

namespace D47.Knowledge.Tests;

/// <summary>The station searches send any positive radius as asked (#1564).</summary>
public class AStationSearchRadiusHasNoCeilingTests
{
    private static StationQuery Nearest(double? maxDistance)
    {
        Assert.True(StationQuery.TryParse(
            "Sol", "Enhanced Performance Thrusters", null, null, null, false, maxDistance, 1, out var query, out _));
        return query;
    }

    private static StationQuery Outfitting(double? maxDistance)
    {
        Assert.True(StationQuery.TryParse(
            new OutfittingSearch { ReferenceSystem = "Sol", Items = "5A Frame Shift Drive", MaxDistance = maxDistance, Size = 1 },
            out var query,
            out _));
        return query;
    }

    private static StationQuery Stations(double? maxDistance)
    {
        Assert.True(StationQuery.TryParse(
            new StationSearch { ReferenceSystem = "Sol", StationTypes = "Starport", MaxDistance = maxDistance, Size = 1 },
            out var query,
            out _));
        return query;
    }

    public static TheoryData<Func<double?, StationQuery>> Overloads => new() { Nearest, Outfitting, Stations };

    [Theory]
    [MemberData(nameof(Overloads))]
    public void ARadiusOfTwentyThousandIsKept(Func<double?, StationQuery> parse) =>
        Assert.Equal(20000, parse(20000).MaxDistance);

    [Theory]
    [MemberData(nameof(Overloads))]
    public void ZeroOrNoRadiusBecomesFifty(Func<double?, StationQuery> parse)
    {
        Assert.Equal(50, parse(0).MaxDistance);
        Assert.Equal(50, parse(null).MaxDistance);
    }

    [Fact]
    public void TheRequestWritesTheRadiusAsAsked()
    {
        using var body = JsonDocument.Parse(SpanshRequest.Stations(Stations(20000)));

        Assert.Equal("20000", body.RootElement.GetProperty("filters").GetProperty("distance").GetProperty("max").GetString());
    }
}
