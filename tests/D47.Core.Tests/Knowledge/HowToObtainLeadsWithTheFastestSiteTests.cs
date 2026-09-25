using D47.Core.Knowledge;
using Xunit;

namespace D47.Core.Tests.Knowledge;

/// <summary>The material dialog's How to obtain steps, best first, ending in the table's other origins (#472).</summary>
public class HowToObtainLeadsWithTheFastestSiteTests
{
    private static IReadOnlyList<string> Steps(string symbol) =>
        FarmingAdvice.HowToObtain(MaterialCatalogue.Find(symbol)!);

    [Fact]
    public void PoloniumLeadsWithItsShardSite()
    {
        var steps = Steps("polonium");

        Assert.StartsWith("Shards — HIP 36601 C 1 a, -57.4599, 126.9543.", steps[0], StringComparison.Ordinal);
        Assert.Contains("Longest jump on the confirmed route: 45.74 ly.", steps[0], StringComparison.Ordinal);
    }

    [Fact]
    public void SeleniumLeadsWithItsBrainTrees()
    {
        Assert.StartsWith("Brain trees — HR 3230 3 a a", Steps("selenium")[0], StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("polonium")]
    [InlineData("ruthenium")]
    [InlineData("tellurium")]
    [InlineData("technetium")]
    [InlineData("yttrium")]
    [InlineData("antimony")]
    [InlineData("selenium")]
    public void EveryGradeFourRawLeadsWithAFarmingSite(string symbol)
    {
        var material = MaterialCatalogue.Find(symbol)!;
        var site = FarmingSites.FastestFor(material.Line)!;

        Assert.Equal(symbol, site.MaterialSymbol);
        Assert.Contains($"{site.System} {site.Body}", Steps(symbol)[0], StringComparison.Ordinal);
    }

    [Fact]
    public void ALowerRawNamesTheSiteThenTheTradeDown()
    {
        var steps = Steps("mercury");

        Assert.Contains("HIP 36601 C 1 a", steps[0], StringComparison.Ordinal);
        Assert.StartsWith("Trade down: ", steps[1], StringComparison.Ordinal);
        Assert.Contains("Polonium for", steps[1], StringComparison.Ordinal);
        Assert.EndsWith("Mercury.", steps[1], StringComparison.Ordinal);
    }

    [Fact]
    public void AManufacturedMaterialLeadsWithWhatToSearchFor()
    {
        Assert.Equal(
            "High Grade Emission — search for Independent · War or Civil War · population over 1,000,000.",
            Steps("militarygradealloys")[0]);
    }

    [Fact]
    public void DavsHopeComesAfterTheTradeDown()
    {
        var lower = MaterialCatalogue.InLine(MaterialCatalogue.Find("militarygradealloys")!.Line)
            .First(entry => entry.Grade == 3);

        var steps = Steps(lower.Symbol);

        Assert.StartsWith("High Grade Emission", steps[0], StringComparison.Ordinal);
        Assert.StartsWith("Trade down: ", steps[1], StringComparison.Ordinal);
        Assert.StartsWith("Dav's Hope — Hyades Sector DR-V c2-23 A 5", steps[2], StringComparison.Ordinal);
    }

    [Fact]
    public void AManufacturedLineNoEmissionCarriesHasNoEmissionStep()
    {
        var uncarried = MaterialCatalogue.All
            .Where(entry => entry.Category == "Manufactured" && entry.Line is not null)
            .GroupBy(entry => entry.Line)
            .First(line => EmissionRules.Holding(line.OrderByDescending(entry => entry.Grade).First().Symbol) is null)
            .First();

        Assert.DoesNotContain(Steps(uncarried.Symbol), step => step.Contains("High Grade Emission", StringComparison.Ordinal));
    }

    [Fact]
    public void EveryEncodedMaterialLeadsWithJamesonCrashSite()
    {
        var encoded = MaterialCatalogue.All.Where(entry => entry.Category == "Encoded" && entry.Line is not null);

        Assert.All(encoded, entry =>
            Assert.StartsWith("Jameson crash site — HIP 12099 1 b", Steps(entry.Symbol)[0], StringComparison.Ordinal));
    }

    [Fact]
    public void TheTablesOtherOriginsComeLast()
    {
        Assert.Equal("Otherwise: Surface prospecting, Mission reward", Steps("polonium")[^1]);
    }
}
