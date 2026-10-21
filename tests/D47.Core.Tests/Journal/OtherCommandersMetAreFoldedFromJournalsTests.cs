using D47.Core.Journal;
using D47.Core.Storage;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Journal;

public class OtherCommandersMetAreFoldedFromJournalsTests
{
    private const string Fid = "F100";

    private static string Line(string time, string kind, string rest = "") =>
        "{\"timestamp\":\"2026-10-01T" + time + "Z\",\"event\":\"" + kind + "\"" + rest + "}";

    private static string Load(string time, string fid = Fid) =>
        Line(time, "LoadGame", ",\"FID\":\"" + fid + "\"");

    private static string Jump(string time, string system) =>
        Line(time, "FSDJump", ",\"StarSystem\":\"" + system + "\"");

    private static string Target(string time, string name, string ship = "Python") =>
        Line(time, "ShipTargeted", ",\"TargetLocked\":true,\"Ship_Localised\":\"" + ship + "\",\"PilotName\":\"$cmdr_decorate:#name="
            + name + ";\",\"PilotName_Localised\":\"CMDR " + name + "\"");

    private static CommanderLog Folded(params string[] lines)
    {
        var log = new CommanderLog(new MemoryFileSystem(), NullLogger.Instance);
        log.Fold(lines);
        return log;
    }

    [Fact]
    public void ATargetThenACrimeByTheSameNameInOneSystemAndSessionIsOneEncounter()
    {
        var log = Folded(
            Load("10:00:00"), Jump("10:01:00", "Sol"),
            Target("10:02:00", "Ilse Bruhn"),
            Line("10:03:00", "CrimeVictim", ",\"Offender\":\"Ilse Bruhn\",\"CrimeType\":\"assault\""));

        var met = Assert.Single(log.Met(Fid));
        Assert.Equal("Ilse Bruhn", met.Name);

        var encounter = Assert.Single(log.Encounters(Fid, "CMDR ilse bruhn"));
        Assert.Equal(EncounterKind.Targeted | EncounterKind.Crime, encounter.Kinds);
        Assert.Equal("Sol", encounter.System);
        Assert.Equal("Python", encounter.Ship);
        Assert.Equal("assault", encounter.CrimeType);
    }

    [Fact]
    public void ALocalisedPrefixInFrontOfThePilotNameIsIgnored()
    {
        var log = Folded(
            Load("10:00:00"),
            Line("10:02:00", "ShipTargeted", ",\"PilotName\":\"$RolePanel2_unmanned; $cmdr_decorate:#name=Ada Ng;\",\"PilotName_Localised\":\"unmanned CMDR Ada Ng\""));

        Assert.Equal("Ada Ng", Assert.Single(log.Met(Fid)).Name);
    }

    [Fact]
    public void ANonPlayerTargetOrAnUnscannedOneAddsNothing()
    {
        var log = Folded(
            Load("10:00:00"),
            Line("10:01:00", "ShipTargeted", ",\"PilotName\":\"$npc_name_decorate:#name=Bot;\",\"PilotName_Localised\":\"Bot\""),
            Line("10:02:00", "ShipTargeted", ",\"TargetLocked\":true,\"ScanStage\":0,\"Ship\":\"python\""));

        Assert.Empty(log.Met(Fid));
    }

    [Fact]
    public void InterdictionsOfNonPlayersAddNothing()
    {
        var log = Folded(
            Load("10:00:00"),
            Line("10:01:00", "Interdicted", ",\"Interdictor\":\"Bot\",\"IsPlayer\":false"),
            Line("10:02:00", "Interdiction", ",\"Interdicted\":\"Bot\",\"IsPlayer\":false"),
            Line("10:03:00", "EscapeInterdiction", ",\"Interdictor\":\"Bot\",\"IsPlayer\":false"));

        Assert.Empty(log.Met(Fid));
    }

    [Fact]
    public void InterdictionsOfPlayersAreKeptByKind()
    {
        var log = Folded(
            Load("10:00:00"),
            Line("10:01:00", "Interdicted", ",\"Interdictor\":\"Cmdr Zed\",\"IsPlayer\":true"),
            Line("10:02:00", "Interdiction", ",\"Interdicted\":\"Zed\",\"IsPlayer\":true"),
            Line("10:03:00", "EscapeInterdiction", ",\"Interdictor\":\"Zed\",\"IsPlayer\":true"));

        var encounter = Assert.Single(log.Encounters(Fid, "Zed"));
        Assert.Equal(EncounterKind.InterdictedBy | EncounterKind.Interdicted | EncounterKind.EscapedInterdictionBy, encounter.Kinds);
    }

    [Fact]
    public void ADeathCountsOnlyWhenACommanderDidIt()
    {
        var log = Folded(
            Load("10:00:00"),
            Line("10:01:00", "Died", ",\"KillerName\":\"$ShipName_Police_Federation;\""),
            Line("10:02:00", "Died", ",\"Killers\":[{\"Name\":\"Cmdr Zed\",\"Ship\":\"viper\"},{\"Name\":\"Bot\"}]"),
            Line("10:03:00", "PVPKill", ",\"Victim\":\"Quin\",\"CombatRank\":5"));

        Assert.Equal(EncounterKind.KilledBy, Assert.Single(log.Encounters(Fid, "Zed")).Kinds);
        Assert.Equal(EncounterKind.Killed, Assert.Single(log.Encounters(Fid, "Quin")).Kinds);
        Assert.Equal(2, log.Met(Fid).Count);
    }

    [Fact]
    public void WingmatesAreKept()
    {
        var log = Folded(
            Load("10:00:00"),
            Line("10:01:00", "WingJoin", ",\"Others\":[\"Ada\",\"Bo\"]"),
            Line("10:02:00", "WingAdd", ",\"Name\":\"Cy\""));

        Assert.Equal(3, log.Met(Fid).Count);
    }

    [Fact]
    public void AMessageOnAPlayerChannelIsKeptWithoutItsText()
    {
        var log = Folded(
            Load("10:00:00"),
            Line("10:01:00", "ReceiveText", ",\"From\":\"Nearby\",\"Message\":\"hello\",\"Channel\":\"starsystem\""),
            Line("10:02:00", "ReceiveText", ",\"From\":\"Ada\",\"Message\":\"secret words\",\"Channel\":\"player\""));

        var encounter = Assert.Single(log.Encounters(Fid, "Ada"));
        Assert.Equal(EncounterKind.Messaged, encounter.Kinds);
        Assert.Single(log.Met(Fid));
        Assert.DoesNotContain("secret", encounter.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void TwoFidsStayApart()
    {
        var log = Folded(
            Load("10:00:00", "F1"), Target("10:01:00", "Ada"),
            Load("11:00:00", "F2"), Target("11:01:00", "Bo"));

        Assert.Equal("Ada", Assert.Single(log.Met("F1")).Name);
        Assert.Equal("Bo", Assert.Single(log.Met("F2")).Name);
    }

    [Fact]
    public void AnotherSessionIsAnotherEncounterAndIsBeforeThisSession()
    {
        var log = Folded(
            Load("10:00:00"), Target("10:01:00", "Ada"),
            Load("12:00:00"), Target("12:01:00", "Ada"));

        var encounters = log.Encounters(Fid, "Ada");
        Assert.Equal(2, encounters.Count);
        Assert.False(log.IsBeforeThisSession(encounters[0]));
        Assert.True(log.IsBeforeThisSession(encounters[1]));
        Assert.Equal("Ada", log.LastTargeted()?.Name);
    }

    [Fact]
    public void TheLastTargetIsNotOneFromAnEarlierSession()
    {
        var log = Folded(Load("10:00:00"), Target("10:01:00", "Ada"), Load("12:00:00"));

        Assert.Null(log.LastTargeted());
    }

    [Fact]
    public void FoldingTheSameLinesByHistoryAndByApplyChangesNothing()
    {
        string[] lines =
        [
            Load("10:00:00"), Jump("10:01:00", "Sol"), Target("10:02:00", "Ada"),
            Line("10:03:00", "CrimeVictim", ",\"Offender\":\"Ada\",\"CrimeType\":\"assault\""),
        ];

        var files = new MemoryFileSystem();
        files.WriteText("j.log", string.Join('\n', lines));

        var twice = new CommanderLog(files, NullLogger.Instance);
        twice.FoldHistory(["j.log"], TestContext.Current.CancellationToken);
        twice.Fold(lines);

        var once = Folded(lines);

        Assert.Equal(once.Encounters(Fid, "Ada"), twice.Encounters(Fid, "Ada"));
        Assert.Equal(once.Met(Fid), twice.Met(Fid));
    }

    [Fact]
    public void HistoryIsNotFoldedUntilFoldHistoryFinishes()
    {
        var log = new CommanderLog(new MemoryFileSystem(), NullLogger.Instance);
        Assert.False(log.HistoryFolded);

        log.Fold([Load("10:00:00")]);
        Assert.False(log.HistoryFolded);

        log.FoldHistory([], TestContext.Current.CancellationToken);
        Assert.True(log.HistoryFolded);
    }

    [Fact]
    public void TheLogWritesNoFile()
    {
        var files = new MemoryFileSystem();
        var log = new CommanderLog(files, NullLogger.Instance);
        log.Fold([Load("10:00:00"), Target("10:01:00", "Ada")]);
        log.FoldHistory([], TestContext.Current.CancellationToken);

        Assert.Empty(files.Enumerate("/data", "*", true));
    }
}
