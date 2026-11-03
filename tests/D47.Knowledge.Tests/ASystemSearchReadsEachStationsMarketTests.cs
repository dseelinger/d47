using System.Text.Json;
using Xunit;

namespace D47.Knowledge.Tests;

/// <summary>A system search result carries its stations, each with its type and whether it has a market (#2095).</summary>
public class ASystemSearchReadsEachStationsMarketTests
{
    [Fact]
    public void EachStationKeepsItsTypeMarketAndDistanceFromTheStar()
    {
        var body = """
            {"results":[{"name":"X","stations":[
              {"name":"Nordic Light","type":"Coriolis Starport","has_market":true,"distance_to_arrival":286.1},
              {"name":"Iida Enterprise","type":"Outpost","has_market":null,"distance_to_arrival":2.7},
              {"type":"Outpost"}
            ]}],"count":1}
            """;

        var system = Assert.Single(SpanshResponse.ReadSearch(JsonDocument.Parse(body)).Systems);

        Assert.Equal(3, system.StationCount);
        Assert.Equal(2, system.Stations.Count);
        Assert.Equal("Nordic Light", system.Stations[0].Name);
        Assert.Equal("Coriolis Starport", system.Stations[0].Type);
        Assert.True(system.Stations[0].HasMarket);
        Assert.Equal(286.1, system.Stations[0].DistanceToArrival);
        Assert.False(system.Stations[1].HasMarket);
    }

    [Fact]
    public void ASystemWithNoStationListHasNoStations()
    {
        var system = Assert.Single(
            SpanshResponse.ReadSearch(JsonDocument.Parse("""{"results":[{"name":"X"}],"count":1}""")).Systems);

        Assert.Empty(system.Stations);
    }
}
