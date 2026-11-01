using D47.Core.Knowledge;
using Xunit;

namespace D47.Core.Tests.Knowledge;

public sealed class HullsAreSpelledTheWayEliteSpellsThemTests
{
    [Theory]
    [InlineData("cobramkiii", "Cobra Mk III")]
    [InlineData("cobramkiv", "Cobra Mk IV")]
    [InlineData("cobramkv", "Cobra Mk V")]
    [InlineData("krait_mkii", "Krait Mk II")]
    [InlineData("python_nx", "Python Mk II")]
    [InlineData("panthermkii", "Panther Clipper Mk II")]
    [InlineData("viper", "Viper Mk III")]
    [InlineData("viper_mkiv", "Viper Mk IV")]
    public void TheTableNamesTheHullWithASpaceBeforeTheNumeral(string symbol, string name)
    {
        Assert.Equal(name, EliteSpecifications.HullName(symbol));
    }

    [Theory]
    [InlineData("Cobra MkV", "cobramkv")]
    [InlineData("Cobra Mk V", "cobramkv")]
    [InlineData("Krait MkII", "krait_mkii")]
    [InlineData("Krait Mk II", "krait_mkii")]
    [InlineData("Panther Clipper MkII", "panthermkii")]
    [InlineData("Panther Clipper Mk II", "panthermkii")]
    public void EitherSpellingResolvesToTheSameSymbol(string spoken, string symbol)
    {
        Assert.Equal(symbol, EliteSpecifications.HullSymbol(spoken));
    }

    [Fact]
    public void TheArmourRowsInheritTheSpelling()
    {
        Assert.DoesNotContain(
            EliteSpecifications.Modules,
            module => module.Name.StartsWith("Cobra Mk", StringComparison.Ordinal) && !module.Name.StartsWith("Cobra Mk ", StringComparison.Ordinal));
        Assert.Contains(EliteSpecifications.Modules, module => module.Name == "Cobra Mk V Lightweight Alloy");
    }

    [Fact]
    public void TheShipListKeepsTheSpellingSpanshFiltersOn()
    {
        // OutfittingCatalogue.Ships is written into Spansh's `ships` filter, and Spansh's shipyards list these hulls run together.
        Assert.Contains("Cobra MkIII", OutfittingCatalogue.Ships);
        Assert.Contains("Krait MkII", OutfittingCatalogue.Ships);
        Assert.Contains("Python MkII", OutfittingCatalogue.Ships);
        Assert.Contains("Panther Clipper MkII", OutfittingCatalogue.Ships);
    }
}
