using System.Text.Json;
using D47.Core.Knowledge;
using D47.Knowledge;
using Xunit;

namespace D47.Knowledge.Tests;

public class AStoryStationSearchAsksForFixedStationsOnlyTests
{
    private static string[] Types(StationQuery query) =>
    [
        .. JsonDocument.Parse(SpanshRequest.Stations(query)).RootElement
            .GetProperty("filters").GetProperty("type").GetProperty("value")
            .EnumerateArray().Select(value => value.GetString()!),
    ];

    [Fact]
    public void WithinNamesEveryTypeButCarriersMegashipsAndConstructionDepots()
    {
        var types = Types(StationQuery.Within("Sol", "Jameson Memorial"));

        Assert.DoesNotContain("Drake-Class Carrier", types);
        Assert.DoesNotContain("Mega ship", types);
        Assert.DoesNotContain("Planetary Construction Depot", types);
        Assert.DoesNotContain("Space Construction Depot", types);
        Assert.Contains("Surface Settlement", types);
        Assert.Contains("Settlement", types);
        Assert.Contains("Coriolis Starport", types);
        Assert.Equal(10, types.Length);
    }

    [Fact]
    public void NearWritesTheSameList()
    {
        Assert.Equal(Types(StationQuery.Within("Sol", "Jameson Memorial")), Types(StationQuery.Near("Sol", 80, 20)));
    }

    [Fact]
    public void WithinWritesTheStationNameFilter()
    {
        var name = JsonDocument.Parse(SpanshRequest.Stations(StationQuery.Within("Eravate", "Cleve Hub")))
            .RootElement.GetProperty("filters").GetProperty("name").GetProperty("value").GetString();

        Assert.Equal("Cleve Hub", name);
    }

    [Fact]
    public void ASearchWithoutAStationNameWritesNoNameFilter()
    {
        var filters = JsonDocument.Parse(SpanshRequest.Stations(StationQuery.Near("Eravate", 50, 5))).RootElement.GetProperty("filters");

        Assert.False(filters.TryGetProperty("name", out _));
    }
}
