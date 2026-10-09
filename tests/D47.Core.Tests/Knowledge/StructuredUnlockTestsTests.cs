using D47.Core.Knowledge;
using Xunit;

namespace D47.Core.Tests.Knowledge;

/// <summary>
/// The <c>meeting_test</c> and <c>unlock_test</c> columns <c>tools/gen-engineers.py</c> derives from
/// Frontier's prose (#180), and <see cref="UnlockTest.Parse"/>, which reads them.
/// </summary>
public class StructuredUnlockTestsTests
{
    [Fact]
    public void ThirtyFourEngineersCarryAMeetingTest()
    {
        Assert.Equal(34, EngineerDirectory.All.Count(engineer => engineer.MeetingTest is not null));
    }

    [Fact]
    public void ThirtyTwoEngineersCarryAnUnlockTest()
    {
        Assert.Equal(32, EngineerDirectory.All.Count(engineer => engineer.UnlockTest is not null));
    }

    [Theory]
    [InlineData("Jude Navarro", UnlockTest.TallyKind.Missions, 10, "Mission_OnFoot_Reboot", null)]
    [InlineData("Terra Velasquez", UnlockTest.TallyKind.Missions, 6, "Mission_OnFoot_Heist_Covert", null)]
    [InlineData("Eleanor Bresa", UnlockTest.TallyKind.Visits, 5, "", "Colonia")]
    [InlineData("Rosa Dayette", UnlockTest.TallyKind.Sales, 10, "culinaryrecipes,cocktailrecipes", "Colonia")]
    [InlineData("Kit Fowler", UnlockTest.TallyKind.Sales, 5, "opinionpolls", null)]
    [InlineData("Yarden Bond", UnlockTest.TallyKind.Sales, 5, "smearcampaignplans", null)]
    [InlineData("Wellington Beck", UnlockTest.TallyKind.Sales, 15,
        "multimediaentertainment,classicentertainment,catmedia", null)]
    [InlineData("Oden Geiger", UnlockTest.TallyKind.Sales, 20,
        "geneticsample,employeegeneticdata,geneticresearch", null)]
    public void EightOdysseyMeetingsAreTallies(
        string name, UnlockTest.TallyKind kind, long atLeast, string names, string? system)
    {
        var tally = Assert.IsType<UnlockTest.Tally>(EngineerDirectory.ByName(name)?.MeetingTest);

        Assert.Equal(kind, tally.Kind);
        Assert.Equal(atLeast, tally.AtLeast);
        Assert.Equal(names, string.Join(',', tally.Names));
        Assert.Equal(system, tally.StarSystem);
    }

    [Fact]
    public void YiShenHasNoMeetingTest()
    {
        Assert.Null(EngineerDirectory.ByName("Yi Shen")?.MeetingTest);
    }

    [Theory]
    [InlineData("tally")]
    [InlineData("tally missions")]
    [InlineData("tally missions ten Mission_OnFoot_Reboot")]
    [InlineData("tally parcels 10 Mission_OnFoot_Reboot")]
    [InlineData("tally missions 10 -")]
    [InlineData("tally visits 5 Colonia")]
    [InlineData("tally sales 5 opinionpolls Colonia extra")]
    public void AMalformedTallyCellParsesToNothing(string cell)
    {
        Assert.Null(UnlockTest.Parse(cell));
    }

    [Fact]
    public void LoriJamesonsMeetingIsACombatRank()
    {
        var lori = EngineerDirectory.ByName("Lori Jameson");

        Assert.NotNull(lori);
        Assert.Equal(new UnlockTest.Rank("Combat", 6), lori.MeetingTest);
    }

    [Fact]
    public void ColonelBrisDekkersUnlockIsAStatedBondOverride()
    {
        var dekker = EngineerDirectory.ByName("Colonel Bris Dekker");

        Assert.NotNull(dekker);
        Assert.Equal(new UnlockTest.Contribution("Bond", null, 1000000), dekker.UnlockTest);
    }

    [Fact]
    public void UmaLaszlosMeetingIsAReputationCeiling()
    {
        var uma = EngineerDirectory.ByName("Uma Laszlo");

        Assert.NotNull(uma);
        Assert.Equal(
            new UnlockTest.Reputation("Sirius Corporation", ReputationBand.Unfriendly, AtMost: true),
            uma.MeetingTest);
    }

    [Fact]
    public void JuriIshmaaksMeetingIsAStatisticFloor()
    {
        var juri = EngineerDirectory.ByName("Juri Ishmaak");

        Assert.NotNull(juri);
        Assert.Equal(new UnlockTest.Statistic("Combat.Combat_Bonds", 51), juri.MeetingTest);
    }

    [Fact]
    public void HeroFerrarisMeetingIsASurfaceConflictZoneFloor()
    {
        var hero = EngineerDirectory.ByName("Hero Ferrari");

        Assert.NotNull(hero);
        Assert.Equal(new UnlockTest.Statistic("Combat.ConflictZone_Low_Wins", 10), hero.MeetingTest);
    }

    [Fact]
    public void EveryContributionSymbolIsAMaterialsTableSymbol()
    {
        var symbols = MaterialCatalogue.All.Select(material => material.Symbol).ToHashSet(StringComparer.Ordinal);

        var contributions = EngineerDirectory.All
            .SelectMany(engineer => new[] { engineer.MeetingTest, engineer.UnlockTest })
            .OfType<UnlockTest.Contribution>()
            .Where(contribution => contribution.Symbol is not null);

        Assert.NotEmpty(contributions);
        Assert.All(contributions, contribution => Assert.Contains(contribution.Symbol!, symbols));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("gain Combat 3")]
    [InlineData("rank Combat")]
    [InlineData("rank Combat six")]
    [InlineData("reputation sideways friendly Empire")]
    [InlineData("reputation at-least sideways Empire")]
    [InlineData("statistic Combat.Combat_Bonds")]
    [InlineData("statistic Combat.Combat_Bonds fifty")]
    [InlineData("contribution")]
    [InlineData("contribution Commodity")]
    public void AMalformedOrUnknownCellParsesToNull(string? cell)
    {
        Assert.Null(UnlockTest.Parse(cell));
    }

    [Trait("Category", "Integration")]
    [Fact]
    public void NoNonEmptyShippedCellFailsToParse()
    {
        var path = Path.Combine(RepositoryRoot(), "src", "D47.Core", "Knowledge", "Engineers.tsv");
        var lines = File.ReadAllLines(path).Where(line => line.Length > 0 && line[0] != '#').Skip(1);

        var unparsed = new List<string>();

        foreach (var cells in lines.Select(line => line.Split('\t')))
        {
            foreach (var index in new[] { 15, 16 })
            {
                if (index < cells.Length && cells[index].Length > 0 && UnlockTest.Parse(cells[index]) is null)
                {
                    unparsed.Add($"{cells[1]} column {index}: {cells[index]}");
                }
            }
        }

        Assert.Empty(unparsed);
    }

    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "d47.slnx")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName
               ?? throw new InvalidOperationException("no repository root above the test binary");
    }
}
