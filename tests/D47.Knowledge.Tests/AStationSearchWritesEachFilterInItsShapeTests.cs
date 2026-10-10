using System.Text.Json;
using D47.Core.Knowledge;
using D47.Knowledge;
using Xunit;

namespace D47.Knowledge.Tests;

/// <summary>search_stations' request body, asserted against shapes probed on 2026-10-03 (#810).</summary>
public class AStationSearchWritesEachFilterInItsShapeTests
{
    private static JsonElement Filters(StationSearch search)
    {
        Assert.True(
            StationQuery.TryParse(search with { ReferenceSystem = "Sol" }, out var query, out var failure),
            failure);

        return JsonDocument.Parse(SpanshRequest.Stations(query)).RootElement.GetProperty("filters");
    }

    private static string[] Values(JsonElement filter) =>
        [.. filter.GetProperty("value").EnumerateArray().Select(value => value.GetString()!)];

    [Fact]
    public void ServicesAreOneObjectPerServiceSoAStationMustHaveEveryOne()
    {
        var services = Filters(new StationSearch { Services = "Material Trader, rearm, Interstellar Factors" })
            .GetProperty("services");

        Assert.Equal(JsonValueKind.Array, services.ValueKind);
        Assert.Equal(
            ["Material Trader", "Restock", "Interstellar Factors Contact"],
            services.EnumerateArray().Select(service => Assert.Single(Values(service.GetProperty("name")))));
    }

    [Fact]
    public void TheDefaultTypeListIsEveryTypeButTheCarrier()
    {
        var types = Values(Filters(new StationSearch { Services = "Refuel" }).GetProperty("type"));

        Assert.DoesNotContain("Drake-Class Carrier", types);
        Assert.Contains("Coriolis Starport", types);
        Assert.Contains("Planetary Port", types);
        Assert.Contains("Surface Settlement", types);
        Assert.Equal(13, types.Length);
    }

    [Fact]
    public void ASurfacePortIsBothPlanetaryTypesAndNothingElse()
    {
        var types = Values(Filters(new StationSearch { StationTypes = "Surface port" }).GetProperty("type"));

        Assert.Equal(["Planetary Outpost", "Planetary Port"], types);
    }

    [Fact]
    public void AFleetCarrierIsSentOnlyWhenNamed()
    {
        var types = Values(Filters(new StationSearch { StationTypes = "fleet carrier, Megaship" }).GetProperty("type"));

        Assert.Equal(["Drake-Class Carrier", "Mega ship"], types);
    }

    [Fact]
    public void ALargePadIsTheLargePadChoice()
    {
        var filters = Filters(new StationSearch { MinPad = "Large" });

        Assert.Equal(["true"], Values(filters.GetProperty("has_large_pad")));
        Assert.False(filters.TryGetProperty("medium_pads", out _));
    }

    [Fact]
    public void AMediumPadIsAComparisonFromOne()
    {
        var filters = Filters(new StationSearch { MinPad = "medium" });

        var medium = filters.GetProperty("medium_pads");

        Assert.Equal("1", Values(medium)[0]);
        Assert.Equal("<=>", medium.GetProperty("comparison").GetString());
        Assert.False(filters.TryGetProperty("has_large_pad", out _));
    }

    [Fact]
    public void TheArrivalDistanceIsAComparisonFromZero()
    {
        var arrival = Filters(new StationSearch { MaxStationDistance = 1000 }).GetProperty("distance_to_arrival");

        Assert.Equal(["0", "1000"], Values(arrival));
        Assert.Equal("<=>", arrival.GetProperty("comparison").GetString());
    }

    [Fact]
    public void TheTraderAndBrokerAreChoices()
    {
        var filters = Filters(new StationSearch { MaterialTrader = "raw", TechnologyBroker = "guardian" });

        Assert.Equal(["Raw"], Values(filters.GetProperty("material_trader")));
        Assert.Equal(["Guardian"], Values(filters.GetProperty("technology_broker")));
        Assert.False(filters.TryGetProperty("services", out _));
    }

    [Fact]
    public void AModuleAndAShipAreWrittenAsTheNearestStationSearchWritesThem()
    {
        var filters = Filters(new StationSearch
        {
            Module = "Frame Shift Drive", ModuleClass = "5", ModuleRating = "a", Ship = "Krait MkII",
        });

        var modules = filters.GetProperty("modules");

        Assert.Equal(["Frame Shift Drive"], Values(modules.GetProperty("name")));
        Assert.Equal(["5"], Values(modules.GetProperty("class")));
        Assert.Equal(["A"], Values(modules.GetProperty("rating")));
        Assert.Equal(["Krait MkII"], Values(filters.GetProperty("ships")));
    }

    [Theory]
    [InlineData("power", "Jerome Archer", "system_controlling_power")]
    [InlineData("power_state", "Fortified", "system_power_state")]
    [InlineData("primary_economy", "Industrial", "system_primary_economy")]
    [InlineData("allegiance", "Federation", "allegiance")]
    [InlineData("government", "Democracy", "government")]
    [InlineData("state", "Boom", "controlling_minor_faction_state")]
    [InlineData("controlling_faction", "Mother Gaia", "controlling_minor_faction")]
    public void AVocabularyChoiceIsSentUnderTheStationKey(string name, string value, string key)
    {
        var filters = Filters(new StationSearch
        {
            Filters = new Dictionary<string, string>(StringComparer.Ordinal) { [name] = value },
        });

        Assert.Equal([value], Values(filters.GetProperty(key)));
    }

    [Fact]
    public void PopulationIsAComparisonAndColonisedAFlag()
    {
        var filters = Filters(new StationSearch
        {
            Filters = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["population"] = "1000000-",
                ["colonised"] = "true",
            },
        });

        Assert.Equal("1000000", Values(filters.GetProperty("system_population"))[0]);
        Assert.Equal("<=>", filters.GetProperty("system_population").GetProperty("comparison").GetString());
        Assert.Equal(["true"], Values(filters.GetProperty("system_is_colonised")));
    }

    [Fact]
    public void TheNearestStationSearchStillSendsNoTypeFilter()
    {
        Assert.True(StationQuery.TryParse(
            "Sol", "Frame Shift Drive", null, null, null, largePadOnly: false, maxDistance: null, size: 5,
            out var query,
            out var failure), failure);

        var filters = JsonDocument.Parse(SpanshRequest.Stations(query)).RootElement.GetProperty("filters");

        Assert.False(filters.TryGetProperty("type", out _));
        Assert.False(filters.TryGetProperty("services", out _));
    }

    [Fact]
    public void AStationEconomyIsTheStationsOwnAndPrimaryEconomyStaysTheSystems()
    {
        var filters = Filters(new StationSearch
        {
            Filters = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["station_economy"] = "refinery",
                ["primary_economy"] = "Industrial",
            },
        });

        Assert.Equal(["Refinery"], Values(filters.GetProperty("primary_economy")));
        Assert.Equal(["Industrial"], Values(filters.GetProperty("system_primary_economy")));
        Assert.Equal(1, filters.EnumerateObject().Count(property => property.Name == "primary_economy"));
    }
}
