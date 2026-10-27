using D47.Core.Knowledge;
using Xunit;

namespace D47.Core.Tests.Knowledge;

/// <summary>The names Spansh lists a module or ship under besides the catalogue's.</summary>
public class AnotherSpanshNameMapsToOneCatalogueEntryTests
{
    private static IEnumerable<(string Catalogue, string Other)> Pairs() =>
        OutfittingCatalogue.OtherNames.Concat(OutfittingCatalogue.OtherShipNames)
            .SelectMany(pair => pair.Value.Select(other => (pair.Key, other)));

    [Fact]
    public void EveryNameIsOtherThanACatalogueEntryItBelongsToExactlyOne()
    {
        var all = OutfittingCatalogue.Ships.Concat(OutfittingCatalogue.Modules).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var pairs = Pairs().ToList();

        Assert.NotEmpty(pairs);

        foreach (var (catalogue, other) in pairs)
        {
            Assert.Contains(catalogue, all);
            Assert.DoesNotContain(other, all);
            Assert.Single(pairs, pair => string.Equals(pair.Other, other, StringComparison.OrdinalIgnoreCase));
        }
    }

    [Fact]
    public void ANameSaidAloudUnderTheOtherNameResolvesToTheCatalogueModule()
    {
        foreach (var (catalogue, other) in OutfittingCatalogue.OtherNames.SelectMany(
                     pair => pair.Value.Select(other => (pair.Key, other))))
        {
            Assert.Equal(catalogue, OutfittingCatalogue.MatchModule(other));
            Assert.Equal(catalogue, OutfittingCatalogue.MatchModule(other.ToLowerInvariant()));
        }

        Assert.Equal("Detailed Surface Scanner", OutfittingCatalogue.MatchModule("long range detailed surface scanner"));
        Assert.Equal("Mining Laser", OutfittingCatalogue.MatchModule("long range mining laser"));
    }

    [Fact]
    public void TheCatalogueNameComesFirstInTheNamesASearchSends()
    {
        Assert.Equal(
            ["Detailed Surface Scanner", "Long Range Detailed Surface Scanner"],
            OutfittingCatalogue.ModuleNames("Detailed Surface Scanner"));
        Assert.Equal(["Fuel Scoop"], OutfittingCatalogue.ModuleNames("Fuel Scoop"));
        Assert.Equal(["Python MkII"], OutfittingCatalogue.ShipNames("Python MkII"));
    }

    [Fact]
    public void EveryUnlistedShipIsInTheCatalogue() =>
        Assert.All(OutfittingCatalogue.UnlistedShips, ship => Assert.Contains(ship, OutfittingCatalogue.Ships));
}
