using D47.Core.Knowledge;
using Xunit;

namespace D47.Core.Tests.Knowledge;

public class ACodexVariantIsSearchedAsItsSpeciesTests
{
    [Theory]
    [InlineData("Frutexa Flammasis - Red", "Frutexa Flammasis")]
    [InlineData("Bacterium Cerbrus", "Bacterium Cerbrus")]
    [InlineData("Proto-Lagrange Cloud", "Proto-Lagrange Cloud")]
    [InlineData("Q09-Type Anomaly", "Q09-Type Anomaly")]
    [InlineData("  Fumarole  ", "Fumarole")]
    public void TheVariantIsStrippedAndAHyphenatedNameIsNot(string name, string species) =>
        Assert.Equal(species, CodexSpecies.Of(name));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public void ABlankNameIsNoFilter(string? name) =>
        Assert.Null(CodexSpecies.Of(name));

    [Fact]
    public void ACodexEntryAloneIsASearch()
    {
        Assert.True(BodyQuery.TryParse(new BodyRequest { CodexEntry = "Frutexa Flammasis - Red" }, out var query, out var failure), failure);
        Assert.Equal("Frutexa Flammasis", query.CodexEntry);
    }
}
