using D47.Core.Knowledge;
using Xunit;

namespace D47.Core.Tests.Knowledge;

/// <summary>A codex entry is a region first, a galaxy first or neither, by its subcategory and the table's row.</summary>
public class AnEntryIsJudgedAgainstTheTableTests
{
    private const string Organic = "$Codex_SubCategory_Organic_Structures;";
    private const string Geology = "$Codex_SubCategory_Geology_and_Anomalies;";

    private static readonly CodexTable Table = CodexTable.Parse(
        "1\tListed\tBiology\tOrganic\t18:5\n"
        + "2\tNo Regions\tBiology\tOrganic\t\n"
        + "3\tGeology\tGeology\tGeology\t3:2\n");

    [Theory]
    [InlineData(1L, 18, CodexFirst.None)]
    [InlineData(1L, 4, CodexFirst.Region)]
    [InlineData(2L, 18, CodexFirst.Galaxy)]
    [InlineData(9L, 18, CodexFirst.Galaxy)]
    public void AnOrganicEntryIsJudgedByItsRow(long id, int region, CodexFirst expected) =>
        Assert.Equal(expected, CodexFirsts.Judge(Table, id, region, Organic));

    [Theory]
    [InlineData(3L, 3, CodexFirst.None)]
    [InlineData(3L, 18, CodexFirst.Region)]
    [InlineData(9L, 18, CodexFirst.None)]
    public void AGeologyEntryIsOnlyEverARegionFirst(long id, int region, CodexFirst expected) =>
        Assert.Equal(expected, CodexFirsts.Judge(Table, id, region, Geology));

    [Theory]
    [InlineData("$Codex_SubCategory_Guardian;", 3L, CodexFirst.Region)]
    [InlineData("$Codex_SubCategory_Gas_Giants;", 3L, CodexFirst.Region)]
    [InlineData("$Codex_SubCategory_Stars;", 3L, CodexFirst.None)]
    [InlineData("$Codex_SubCategory_Terrestrials;", 3L, CodexFirst.None)]
    [InlineData("$Codex_SubCategory_Stars;", 9L, CodexFirst.None)]
    [InlineData(null, 9L, CodexFirst.None)]
    public void ASubcategoryOutsideTheOrganicRuleIsJudgedAccordingly(string? sub, long id, CodexFirst expected) =>
        Assert.Equal(expected, CodexFirsts.Judge(Table, id, 18, sub));
}
