using D47.Core.Checklists;
using D47.Core.Engineers;
using D47.Core.Journal;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Journal;

/// <summary>Elite sometimes writes no EngineerProgress roster; the last one in earlier journals stands in (#2413).</summary>
[Trait("Category", "Integration")]
public class EngineerStandingsSurviveASessionWithoutARosterTests
{
    private const string Fid = "F1234567";
    private const int DidiVatermann = 300000;

    [Fact]
    public void AnEngineerUnlockedInAnOlderJournalIsUnlockedWhenTheSessionHasNoRoster()
    {
        var store = RestoredStore(OlderRosterThenSessionWithoutOne());

        var didi = store.Active!.Engineers.For(DidiVatermann);

        Assert.NotNull(didi);
        Assert.True(didi.IsUnlocked);
        Assert.Equal(5, didi.Rank);
        Assert.True(store.Active.Engineers.IsKnown);
    }

    [Fact]
    public void ASingleEngineerChangeInTheSessionChangesOnlyThatEngineer()
    {
        var store = RestoredStore(OlderRosterThenSessionWithoutOne());

        store.Apply(Event(
            """{"timestamp":"2026-09-06T10:30:00Z","event":"EngineerProgress","Engineer":"Felicity Farseer","EngineerID":300100,"Progress":"Unlocked","Rank":2,"RankProgress":0}"""));

        var engineers = store.Active!.Engineers;

        Assert.Equal(2, engineers.For(300100)!.Rank);
        Assert.Equal(5, engineers.For(DidiVatermann)!.Rank);
        Assert.Equal(DateTimeOffset.Parse("2026-09-06T10:30:00Z", System.Globalization.CultureInfo.InvariantCulture), engineers.TakenAt);
    }

    [Fact]
    public void AGradeFiveBlueprintIsNotBlockedByTheRestoredStandings()
    {
        var store = RestoredStore(OlderRosterThenSessionWithoutOne());
        var items = EngineeringPlan.Items(
            ChecklistScope.Ship(12),
            "anaconda",
            [new BuildRequest("Slot1", "Resistance Augmented", 5, Module: "Shield Booster")]);

        var costing = EngineeringPlan.Cost(items, store.Active!);

        Assert.Empty(costing.Blocks);
    }

    [Fact]
    public void ThePlannerSeesTheRestoredEngineerAsUnlockedAndLeavesThemOffTheRoute()
    {
        var store = RestoredStore(OlderRosterThenSessionWithoutOne());

        var report = UnlockPlanner.Of([], [], store.Active);

        Assert.True(report.ProgressKnown);
        Assert.Equal(EngineerReach.Unlocked, report.Directory.Single(entry => entry.Engineer.Id == DidiVatermann).Reach);
        Assert.DoesNotContain(report.Route, candidate => candidate.Engineer.Id == DidiVatermann);
    }

    [Fact]
    public void ABlockedRowSaysProgressWasNotReadWhenNoRosterWasEverRead()
    {
        var store = new GameStateStore();
        store.Apply(Event(LoadGame("09-06")));
        var items = EngineeringPlan.Items(
            ChecklistScope.Ship(12),
            "anaconda",
            [new BuildRequest("Slot1", "Resistance Augmented", 5, Module: "Shield Booster")]);

        var block = Assert.Single(EngineeringPlan.Cost(items, store.Active!).Blocks);

        Assert.False(store.Active!.Engineers.IsKnown);
        Assert.DoesNotContain("not unlocked", block.Reach, StringComparison.Ordinal);
        Assert.Contains("not read", block.Reach, StringComparison.Ordinal);
    }

    [Fact]
    public void RestoredStandingsBelongToTheirOwnCommander()
    {
        var install = OlderRosterThenSessionWithoutOne();
        var store = RestoredStore(install);

        store.Apply(Event(LoadGame("09-07", "F9999999")));

        Assert.False(store.Active!.Engineers.IsKnown);
    }

    private static MemoryInstall OlderRosterThenSessionWithoutOne()
    {
        var install = new MemoryInstall();
        Write(
            install,
            "Journal.2026-09-01T100000.01.log",
            LoadGame("09-01"),
            """{"timestamp":"2026-09-01T10:01:00Z","event":"EngineerProgress","Engineers":[{"Engineer":"Didi Vatermann","EngineerID":300000,"Progress":"Unlocked","RankProgress":0,"Rank":5},{"Engineer":"Felicity Farseer","EngineerID":300100,"Progress":"Unlocked","RankProgress":0,"Rank":1}]}""");
        Write(install, "Journal.2026-09-06T100000.01.log", LoadGame("09-06"));
        return install;
    }

    private static GameStateStore RestoredStore(MemoryInstall install)
    {
        var backfill = new HistoryBackfill
        {
            Directory = install.Root,
            FileSystem = install.Files,
            Loggers = NullLoggerFactory.Instance,
        };
        backfill.Run(TestContext.Current.CancellationToken);

        var store = new GameStateStore { RestoreEngineers = fid => backfill.Engineers?.GetValueOrDefault(fid) };
        store.Apply(Event(LoadGame("09-06")));
        return store;
    }

    private static string LoadGame(string day, string fid = Fid) =>
        $$"""{"timestamp":"2026-{{day}}T10:00:00Z","event":"LoadGame","FID":"{{fid}}","Commander":"Fixture"}""";

    private static JournalEvent Event(string json)
    {
        Assert.True(JournalEvent.TryParse(json, NullLogger.Instance, out var parsed));
        return parsed!;
    }

    private static void Write(MemoryInstall install, string name, params string[] lines) =>
        install.Files.WriteLines(Path.Combine(install.Root, name), lines);
}
