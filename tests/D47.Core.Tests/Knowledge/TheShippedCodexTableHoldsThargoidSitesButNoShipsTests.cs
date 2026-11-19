using D47.Core.Knowledge;
using Xunit;

namespace D47.Core.Tests.Knowledge;

/// <summary>Codex.tsv lists the Thargoid sites and the Thargoid barnacles, and none of the Thargoid ships.</summary>
public class TheShippedCodexTableHoldsThargoidSitesButNoShipsTests
{
    private static readonly CodexTable Table = CodexTable.Parse(CodexCatalogue.Text(CodexCatalogue.ResourceName));

    [Fact]
    public void TheTableHasEighteenThargoidObjectsAndThreeBarnacles()
    {
        Assert.Equal(18, Table.All.Count(entry => entry.SubCategory == "Thargoid objects"));
        Assert.Equal(3, Table.All.Count(entry => entry.Name.Contains("Thargoid Barnacle", StringComparison.Ordinal)
            && entry.SubCategory == "Organic structures"));
    }

    [Theory]
    [InlineData("Thargoid Interceptor")]
    [InlineData("Thargoid Hunter")]
    [InlineData("Thargoid Revenant")]
    [InlineData("Thargoid Scout")]
    public void NoThargoidShipIsListed(string name) =>
        Assert.DoesNotContain(Table.All, entry => entry.Name.Equals(name, StringComparison.OrdinalIgnoreCase)
            || entry.Name.StartsWith(name + " -", StringComparison.OrdinalIgnoreCase));

    [Fact]
    public void ACommonThargoidBarnacleIsJudgedAgainstItsRow()
    {
        var barnacle = Table.All.Single(entry => entry.Name == "Common Thargoid Barnacle");
        var organic = "$Codex_SubCategory_Organic_Structures;";

        Assert.Equal(CodexFirst.None, CodexFirsts.Judge(Table, barnacle.EntryId, 18, organic));
        Assert.Equal(CodexFirst.Region, CodexFirsts.Judge(Table, barnacle.EntryId, 3, organic));
    }
}
