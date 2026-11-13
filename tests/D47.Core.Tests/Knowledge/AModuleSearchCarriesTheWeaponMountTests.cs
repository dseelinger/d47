using D47.Core.Capabilities.Builtin;
using D47.Core.Knowledge;
using Xunit;

namespace D47.Core.Tests.Knowledge;

public class AModuleSearchCarriesTheWeaponMountTests
{
    private static OutfittingItem Item(string text)
    {
        Assert.True(OutfittingItem.TryParse(text, out var item, out var failure), failure);

        return item;
    }

    [Theory]
    [InlineData("2F Multi-Cannon gimballed")]
    [InlineData("gimballed 2F multi-cannon")]
    [InlineData("2F gimballed Multi-Cannon")]
    [InlineData("2F multi-cannon gimbal")]
    public void AMountIsReadAsTheFirstOrLastWord(string text)
    {
        var item = Item(text);

        Assert.Equal("Multi-Cannon", item.Name);
        Assert.Equal(2, item.Class);
        Assert.Equal("F", item.Rating);
        Assert.Equal("gimballed", item.Mount);
        Assert.Equal("2F Multi-Cannon gimballed", item.Display);
    }

    [Fact]
    public void AMountIsReadWithNoSizeOrRating()
    {
        var item = Item("turret multi-cannon");

        Assert.Equal("turreted", item.Mount);
        Assert.Null(item.Class);
        Assert.Equal("Multi-Cannon turreted", item.Display);
    }

    [Fact]
    public void AMountTheTableDoesNotListIsRefusedNamingTheOnesItDoes()
    {
        Assert.False(OutfittingItem.TryParse("2F Multi-Cannon fixed", out _, out var failure));
        Assert.Contains("gimballed or turreted", failure, StringComparison.Ordinal);
    }

    [Fact]
    public void AMountOnAModuleWithNoneIsRefused() =>
        Assert.False(OutfittingItem.TryParse("5A Frame Shift Drive gimballed", out _, out _));

    [Fact]
    public void TheMountIsCheckedOnlyWhereTheTableListsMoreThanOne()
    {
        Assert.True(Item("2F Multi-Cannon gimballed").MountChecked);
        Assert.False(Item("2E Multi-Cannon fixed").MountChecked);
        Assert.False(Item("Heat Sink Launcher turreted").MountChecked);
        Assert.False(Item("2F Multi-Cannon").MountChecked);
    }

    [Fact]
    public void TheTypeAheadOffersAWeaponOncePerMount()
    {
        var displays = OutfittingItem.All.Select(item => item.Display).ToList();

        Assert.Contains("2F Multi-Cannon gimballed", displays);
        Assert.Contains("2F Multi-Cannon turreted", displays);
        Assert.DoesNotContain("2F Multi-Cannon fixed", displays);
        Assert.DoesNotContain("2E Multi-Cannon fixed", displays);
    }

    [Fact]
    public void AnItemKeepsItsMountThroughTheItemsText()
    {
        var search = new OutfittingSearch { ReferenceSystem = "Sol", Items = "2F Multi-Cannon gimballed, 6A Fuel Scoop" };

        Assert.True(StationQuery.TryParse(search, out var query, out var failure), failure);
        Assert.Equal(new ModuleRequirement("Multi-Cannon", "2", "F", "gimballed"), query.Modules[0]);
        Assert.True(query.Modules[0].MountChecked);
        Assert.False(query.Modules[1].MountChecked);
    }

    [Fact]
    public void ANearestStationSearchTakesAMount()
    {
        Assert.True(
            StationQuery.TryParse("Sol", "multi-cannon", "2", "F", null, false, 50, 5, out var query, out var failure, "turreted"),
            failure);
        Assert.Equal("turreted", query.Modules[0].Mount);
        Assert.Equal("a 2F Multi-Cannon, turreted", query.Modules[0].Said);
    }

    [Fact]
    public void AMountSaidInTheModuleNameIsKept()
    {
        Assert.True(
            StationQuery.TryParse("Sol", "gimballed multi-cannon", "2", "F", null, false, 50, 5, out var query, out var failure),
            failure);
        Assert.Equal(new ModuleRequirement("Multi-Cannon", "2", "F", "gimballed"), query.Modules[0]);
    }

    [Theory]
    [InlineData("multi-cannon", "2", "F", "fixed")]
    [InlineData("frame shift drive", "5", "A", "gimballed")]
    [InlineData(null, null, null, "gimballed")]
    [InlineData("multi-cannon", null, null, "sideways")]
    public void AStationSearchRefusesAMountItCannotUse(string? module, string? size, string? rating, string mount) =>
        Assert.False(StationQuery.TryParse(
            new StationSearch { Module = module, ModuleClass = size, ModuleRating = rating, ModuleMount = mount, MinPad = "Large" },
            out _,
            out _));

    [Fact]
    public void AStationSellsACheckedMountOnlyWhereARowListsIt()
    {
        var gimballed = Item("2F Multi-Cannon gimballed");

        Assert.True(Stock(new StockedModule("Multi-Cannon", 2, "F", "gimballed")).Sells(gimballed));
        Assert.False(Stock(new StockedModule("Multi-Cannon", 2, "F", "turreted")).Sells(gimballed));
        Assert.False(Stock(new StockedModule("Multi-Cannon", 2, "F")).Sells(gimballed));
        Assert.True(Stock(new StockedModule("Multi-Cannon", 2, "F", "turreted")).Sells(Item("2F Multi-Cannon")));
    }

    [Fact]
    public void AnUncheckedMountIsSoldWhereTheRowHasNone() =>
        Assert.True(Stock(new StockedModule("Heat Sink Launcher", 0, "I")).Sells(Item("Heat Sink Launcher turreted")));

    [Fact]
    public void AnUncountedAnswerNamesTheMountAndStatesNoTotal()
    {
        Assert.True(StationQuery.TryParse(
            new OutfittingSearch { ReferenceSystem = "Sol", Items = "2F Multi-Cannon gimballed" }, out var query, out _));

        var result = new StationSearchResult("Sol", 40, [Station("Galileo"), Station("Daedalus")]) { Uncounted = true, More = true };

        var said = GalaxyCapability.Describe(result, query);

        Assert.StartsWith("Stations 1 to 2 of those listing a 2F Multi-Cannon, gimballed", said, StringComparison.Ordinal);
        Assert.DoesNotContain("40", said, StringComparison.Ordinal);
    }

    [Fact]
    public void AnUncountedStationSearchStatesNoTotal()
    {
        Assert.True(StationQuery.TryParse(
            new StationSearch { ReferenceSystem = "Sol", Module = "multi-cannon", ModuleMount = "gimballed" }, out var query, out var failure),
            failure);

        var result = new StationSearchResult("Sol", 40, [Station("Galileo")]) { Uncounted = true };

        var said = GalaxyCapability.DescribeStations(result, query);

        Assert.StartsWith("Station 1 of those listing a Multi-Cannon, gimballed, measured from Sol.", said, StringComparison.Ordinal);
        Assert.DoesNotContain("40", said, StringComparison.Ordinal);
    }

    [Fact]
    public void AnUncountedAnswerWithNoStationSaysOnlyWhatItChecked()
    {
        Assert.True(StationQuery.TryParse(
            new OutfittingSearch { ReferenceSystem = "Sol", Items = "2F Multi-Cannon gimballed" }, out var outfitting, out _));
        Assert.True(StationQuery.TryParse(
            new StationSearch { ReferenceSystem = "Sol", Module = "multi-cannon", ModuleMount = "gimballed" }, out var stations, out _));

        var result = new StationSearchResult("Sol", 0, []) { Uncounted = true };

        Assert.Equal("No station I checked lists a 2F Multi-Cannon, gimballed.", GalaxyCapability.Describe(result, outfitting));
        Assert.Equal("No station I checked lists a Multi-Cannon, gimballed.", GalaxyCapability.DescribeStations(result, stations));
    }

    private static StationStock Stock(params StockedModule[] modules) => new(modules, []);

    private static StationSummary Station(string name) => new() { Name = name, SystemName = "Sol", Distance = 1 };
}
