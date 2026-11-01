using System.Text.Json;
using D47.Core.Knowledge;
using D47.Knowledge;
using Xunit;

namespace D47.Knowledge.Tests;

/// <summary>A search's page, counted from 1, is sent as the service's page counted from 0, at the same size.</summary>
public class APageIsAskedOfTheServiceFromZeroTests
{
    private static (int Page, int Size) PageOf(string body)
    {
        var root = JsonDocument.Parse(body).RootElement;

        return (root.GetProperty("page").GetInt32(), root.GetProperty("size").GetInt32());
    }

    [Fact]
    public void SystemsPageThreeOfTwentyFiveIsServicePageTwo()
    {
        Assert.True(GalaxyQuery.TryParse("Sol", new Dictionary<string, string>(), 25, out var query, out var failure), failure);

        Assert.Equal((2, 25), PageOf(SpanshRequest.Search(query with { Page = 3 })));
    }

    [Fact]
    public void AStationsPageIsSentAtItsSize()
    {
        Assert.True(StationQuery.TryParse(
            new StationSearch { ReferenceSystem = "Sol", Services = "Shipyard", Size = 10 },
            out var query,
            out var failure), failure);

        Assert.Equal((0, 10), PageOf(SpanshRequest.Stations(query)));
        Assert.Equal((1, 10), PageOf(SpanshRequest.Stations(query with { Page = 2 })));
    }

    [Fact]
    public void AnOutfittingPageTwoIsServicePageOne()
    {
        Assert.True(StationQuery.TryParse(
            new OutfittingSearch { ReferenceSystem = "Sol", Items = "6A Fuel Scoop", Size = 25 },
            out var query,
            out var failure), failure);

        Assert.Equal((1, 25), PageOf(SpanshRequest.Stations(query with { Page = 2 })));
    }

    [Fact]
    public void ABodiesPageIsSentAtItsSize()
    {
        Assert.True(BodyQuery.TryParse(
            new BodyRequest { ReferenceSystem = "Sol", Subtype = "Earth-like world", Size = 25, Page = 4 },
            out var query,
            out var failure), failure);

        Assert.Equal((3, 25), PageOf(SpanshRequest.Bodies(query)));
    }
}
