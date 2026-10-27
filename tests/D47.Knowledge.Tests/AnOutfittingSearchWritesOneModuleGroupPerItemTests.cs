using System.Text.Json;
using D47.Core.Knowledge;
using Xunit;

namespace D47.Knowledge.Tests;

/// <summary>search_outfitting's request body: every module is its own group, shapes probed on 2026-10-18.</summary>
public class AnOutfittingSearchWritesOneModuleGroupPerItemTests
{
    private static string Body(OutfittingSearch search)
    {
        Assert.True(
            StationQuery.TryParse(search with { ReferenceSystem = "Sol" }, out var query, out var failure),
            failure);

        return SpanshRequest.Stations(query);
    }

    private static JsonElement Filters(OutfittingSearch search) =>
        JsonDocument.Parse(Body(search)).RootElement.GetProperty("filters");

    private static string[] Values(JsonElement filter) =>
        [.. filter.GetProperty("value").EnumerateArray().Select(value => value.GetString()!)];

    [Fact]
    public void TwoModulesAreAnArrayOfTwoGroupsWrittenOnTheString()
    {
        var body = Body(new OutfittingSearch { Items = "5A Frame Shift Drive (SCO), 0F Shutdown Field Neutraliser" });

        Assert.Contains(
            "\"modules\":[{\"name\":{\"value\":[\"Frame Shift Drive (SCO)\"]},\"class\":{\"value\":[\"5\"]},"
            + "\"rating\":{\"value\":[\"A\"]}},{\"name\":{\"value\":[\"Shutdown Field Neutraliser\"]},"
            + "\"class\":{\"value\":[\"0\"]},\"rating\":{\"value\":[\"F\"]}}]",
            body,
            StringComparison.Ordinal);
    }

    [Fact]
    public void AModuleSpansListsUnderTwoNamesIsSearchedForUnderBoth()
    {
        var module = Filters(new OutfittingSearch { Items = "Detailed Surface Scanner" }).GetProperty("modules");

        Assert.Equal(
            ["Detailed Surface Scanner", "Long Range Detailed Surface Scanner"],
            Values(module.GetProperty("name")));
    }

    [Fact]
    public void ANameSaidOutLoudUnderTheOtherNameIsSearchedForUnderBoth()
    {
        var module = Filters(new OutfittingSearch { Items = "long range mining laser" }).GetProperty("modules");

        Assert.Equal(["Mining Laser", "Long Range Mining Laser"], Values(module.GetProperty("name")));
    }

    [Fact]
    public void AnAnacondaMkIIIsSaidToBeSoldAtNoShipyard()
    {
        Assert.False(StationQuery.TryParse(
            new OutfittingSearch { Items = "Anaconda Mk II", ReferenceSystem = "Sol" }, out _, out var failure));
        Assert.Equal("No shipyard in the station index sells the Anaconda Mk II.", failure);

        Assert.False(StationQuery.TryParse(
            new StationSearch { Ship = "Anaconda Mk II", ReferenceSystem = "Sol" }, out _, out var searched));
        Assert.Equal(failure, searched);
    }

    [Fact]
    public void AShipAndModulesAreAllRequired()
    {
        var filters = Filters(new OutfittingSearch { Items = "Python MkII, Fuel Scoop, 5A Frame Shift Drive (SCO)" });

        Assert.Equal(["Python MkII"], Values(filters.GetProperty("ships")));
        Assert.Equal(2, filters.GetProperty("modules").GetArrayLength());
    }

    [Fact]
    public void WithoutSurfaceStationsNoSurfaceTypeIsSent()
    {
        var types = Values(Filters(new OutfittingSearch { Items = "Fuel Scoop", SurfaceStations = false })
            .GetProperty("type"));

        Assert.NotEmpty(types);
        Assert.DoesNotContain(types, type => MarketSnapshot.IsSurfaceType(type));
        Assert.DoesNotContain(types, type => MarketSnapshot.IsCarrierType(type));
    }

    [Fact]
    public void CarriersAreLeftOutUnlessAskedFor()
    {
        var without = Values(Filters(new OutfittingSearch { Items = "Fuel Scoop" }).GetProperty("type"));
        var with = Values(Filters(new OutfittingSearch { Items = "Fuel Scoop", IncludeCarriers = true })
            .GetProperty("type"));

        Assert.DoesNotContain(without, type => MarketSnapshot.IsCarrierType(type));
        Assert.Contains(with, type => MarketSnapshot.IsCarrierType(type));
        Assert.Contains(with, type => MarketSnapshot.IsSurfaceType(type));
    }
}
