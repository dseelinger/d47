using System.Text.Json;
using D47.Core.Knowledge;
using D47.Knowledge;
using Xunit;

namespace D47.Knowledge.Tests;

/// <summary>A galaxy search is sorted by the service on the field it was asked for, and nearest first when it was asked for none (#1113).</summary>
public class ASortedSearchAsksTheServiceForThatSortTests
{
    private static (string Field, string Direction) SortOf(string body)
    {
        var sort = JsonDocument.Parse(body).RootElement.GetProperty("sort");
        var only = Assert.Single(sort.EnumerateArray()).EnumerateObject().Single();

        return (only.Name, only.Value.GetProperty("direction").GetString()!);
    }

    private static string Systems(string? orderBy, string? order)
    {
        Assert.True(GalaxyQuery.TryParse(
            "Sol",
            new Dictionary<string, string> { ["allegiance"] = "Federation" },
            5,
            out var query,
            out var failure,
            orderBy,
            order), failure);

        return SpanshRequest.Search(query);
    }

    private static string Stations(string? orderBy, string? order)
    {
        Assert.True(StationQuery.TryParse(
            new StationSearch { ReferenceSystem = "Sol", Ship = "Cobra Mk III", Size = 5, OrderBy = orderBy, Order = order },
            out var query,
            out var failure), failure);

        return SpanshRequest.Stations(query);
    }

    private static string Bodies(string? orderBy, string? order)
    {
        Assert.True(BodyQuery.TryParse(
            new BodyRequest { ReferenceSystem = "Sol", Subtype = "Earth-like world", Material = "Polonium", Size = 5, OrderBy = orderBy, Order = order },
            out var query,
            out var failure), failure);

        return SpanshRequest.Bodies(query);
    }

    [Fact]
    public void ASystemSearchWithNoSortIsNearestFirst()
    {
        Assert.Equal(("distance", "asc"), SortOf(Systems(null, null)));
    }

    [Fact]
    public void ASystemSearchSortedByPopulationIsMostPopulousFirstUnlessReversed()
    {
        Assert.Equal(("population", "desc"), SortOf(Systems("population", null)));
        Assert.Equal(("population", "asc"), SortOf(Systems("population", "asc")));
    }

    [Fact]
    public void AToolNameIsSentAsTheServicesOwnField()
    {
        Assert.Equal(("primary_economy", "asc"), SortOf(Systems("economy", null)));
        Assert.Equal(("controlling_power", "desc"), SortOf(Systems("power", "desc")));
        Assert.Equal(("updated_at", "desc"), SortOf(Systems("updated", null)));
    }

    [Fact]
    public void AStationSearchSortsByDistanceFromItsStarOrFurthestFirst()
    {
        Assert.Equal(("distance", "asc"), SortOf(Stations(null, null)));
        Assert.Equal(("distance", "desc"), SortOf(Stations("distance", "desc")));
        Assert.Equal(("distance_to_arrival", "asc"), SortOf(Stations("from_star", null)));
        Assert.Equal(("system_name", "asc"), SortOf(Stations("system", null)));
    }

    [Fact]
    public void ABodySearchSortsByGravityHighestFirst()
    {
        Assert.Equal(("distance", "asc"), SortOf(Bodies(null, null)));
        Assert.Equal(("gravity", "desc"), SortOf(Bodies("gravity", null)));
    }

    [Fact]
    public void ARichestInMaterialSearchAsksNearestFirstAndSortsTheShareItself()
    {
        Assert.Equal(("distance", "asc"), SortOf(Bodies("material", null)));
    }

    [Fact]
    public void ASortTheServiceDoesNotDoIsRefusedWithTheNamesItDoes()
    {
        Assert.False(GalaxyQuery.TryParse("Sol", new Dictionary<string, string> { ["allegiance"] = "Federation" }, 5, out _, out var failure, "landable", null));
        Assert.Contains("population", failure, StringComparison.Ordinal);
    }

    [Fact]
    public void AnOrderWithNoFieldAndAWrongOrderAreRefused()
    {
        Assert.False(SearchSorts.TryParse(GalaxySearchKind.Systems, null, "desc", out _, out _));
        Assert.False(SearchSorts.TryParse(GalaxySearchKind.Systems, "population", "sideways", out _, out _));
    }

    [Fact]
    public void RichestFirstGoesOneWay()
    {
        Assert.False(SearchSorts.TryParse(GalaxySearchKind.Bodies, "material", "asc", out _, out var failure));
        Assert.Contains("desc", failure, StringComparison.Ordinal);
    }
}
