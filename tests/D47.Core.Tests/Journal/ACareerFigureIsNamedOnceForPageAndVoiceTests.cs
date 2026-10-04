using D47.Core.Journal;
using Xunit;

namespace D47.Core.Tests.Journal;

/// <summary>The label and unit of a career figure, shared by the Statistics page and <c>get_commander_statistics</c> (#553).</summary>
public class ACareerFigureIsNamedOnceForPageAndVoiceTests
{
    [Theory]
    [InlineData("Bounties_Claimed", "Bounties Claimed")]
    [InlineData("Search_And_Rescue", "Search And Rescue")]
    [InlineData("NpcCrew_Hired", "Npc Crew Hired")]
    [InlineData("FLEETCARRIER", "Fleet carrier")]
    [InlineData("Material_Trader_Stats", "Material trader")]
    public void AKeyReadsAsWords(string key, string label) => Assert.Equal(label, CareerStatistics.Label(key));

    [Theory]
    [InlineData("MercCoins_Current", 46, "46 Merc Coins")]
    [InlineData("Greatest_Distance_From_Start", 21345.678, "21345.68 ly")]
    [InlineData("Multicrew_Time_Total", 7260, "2h 1m")]
    [InlineData("Time_Played", 60, "1 minute")]
    [InlineData("Market_Profits", 902440000, "902,440,000 cr")]
    [InlineData("Bounties_Claimed", 1842, "1,842")]
    public void AFigureTakesTheUnitItsKeyNames(string key, double value, string shown) =>
        Assert.Equal(shown, CareerStatistics.Format(key, value));
}
