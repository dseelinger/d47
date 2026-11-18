using D47.Core.Knowledge;
using Xunit;

namespace D47.Knowledge.Tests;

public sealed class AStationsSearchRowKeepsEveryFieldItSendsTests
{
    private static StationSummary Row(string name)
    {
        using var document = Fixture.Json("spansh-stations-search-sol-full.json");
        return Assert.Single(SpanshResponse.ReadStations(document).Stations, station => station.Name == name);
    }

    [Fact]
    public void ARowKeepsItsMarketWithEachCommoditysPriceAndRarity()
    {
        var market = Row("Webb Holdings").Market!;

        Assert.Equal(DateTimeOffset.Parse("2026-11-15T16:13:01Z"), market.ReportedAt);
        var commodity = market.Commodities.First(item => item.Supply > 0 && item.BuyPrice > 0);
        Assert.False(string.IsNullOrEmpty(commodity.Category));
        Assert.Contains(Row("Wellman's Folly").Market!.Commodities, item => item.IsRare == true);
    }

    [Fact]
    public void ARowKeepsItsOutfittingWithPriceAndMount()
    {
        var outfitting = Row("Webb Holdings").Outfitting!;

        var cannon = Assert.Single(outfitting.Modules, module => module.Symbol == "Hpt_ATMultiCannon_Fixed_Medium");
        Assert.Equal("AX Multi-Cannon", cannon.Name);
        Assert.Equal(379000, cannon.Price);
        Assert.Equal("fixed", cannon.Mount);
        Assert.Equal(2, cannon.Class);
        Assert.Equal("E", cannon.Rating);
        Assert.Equal(DateTimeOffset.Parse("2026-11-15T16:13:01Z"), outfitting.ReportedAt);
    }

    [Fact]
    public void ARowKeepsItsShipyardWithPrices()
    {
        var shipyard = Row("Webb Holdings").Shipyard!;

        Assert.All(shipyard.Ships, ship => Assert.False(string.IsNullOrEmpty(ship.Symbol)));
        Assert.Contains(shipyard.Ships, ship => ship.Price > 0);
        Assert.Equal(DateTimeOffset.Parse("2026-11-15T16:13:01Z"), shipyard.ReportedAt);
    }

    [Fact]
    public void ARowKeepsItsBodyAndSurfacePosition()
    {
        var station = Row("Webb Holdings");

        Assert.Equal("Wolf 424 A 1", station.Body);
        Assert.Equal("Planet", station.BodyType);
        Assert.Equal("High metal content world", station.BodySubtype);
        Assert.Equal(252228638366311849, station.BodyAddress);
        Assert.Equal(0.28999, station.BodyGravity!.Value, 4);
        Assert.False(station.BodyHasAtmosphere);
        Assert.False(station.BodyHasRings);
        Assert.True(station.IsPlanetary);
        Assert.Equal(-3.259011, station.Latitude!.Value, 5);
        Assert.Equal(-116.32592, station.Longitude!.Value, 5);
    }

    [Fact]
    public void ARowKeepsItsFactionEconomyAndSystemFields()
    {
        var station = Row("Webb Holdings");

        Assert.Equal("Federation", station.Allegiance);
        Assert.Equal("Democracy", station.Government);
        Assert.Equal("Refinery", station.PrimaryEconomy);
        Assert.Equal(new EconomyShare("Refinery", 100), Assert.Single(station.Economies));
        Assert.Equal("Boom", station.ControllingFactionState);
        Assert.Equal(0.51503, station.ControllingFactionInfluence!.Value, 5);
        Assert.True(station.HasMarket);
        Assert.Equal(24449914, station.SystemPopulation);
        Assert.Equal("Industrial", station.SystemPrimaryEconomy);
        Assert.Equal("Extraction", station.SystemSecondaryEconomy);
        Assert.Equal(6, station.SystemPowers.Count);
        Assert.Contains("Narcotics", station.ProhibitedCommodities);
        Assert.False(station.SystemIsColonised);
        Assert.False(station.SystemIsBeingColonised);
    }

    [Fact]
    public void ARowWithoutABodyLeavesItNull()
    {
        var station = Row("Donner Beacon");

        Assert.Null(station.Body);
        Assert.Null(station.BodyAddress);
        Assert.Null(station.Latitude);
    }
}
