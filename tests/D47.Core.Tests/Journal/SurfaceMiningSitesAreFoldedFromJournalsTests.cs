using D47.Core.Journal;
using D47.Core.Storage;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Journal;

public class SurfaceMiningSitesAreFoldedFromJournalsTests
{
    private const string Fid = "F1";

    private const long System = 1234;

    private const int BodyId = 7;

    private static string Line(string time, string kind, string fields = "") =>
        "{ \"timestamp\":\"2025-07-10T" + time + "Z\", \"event\":\"" + kind + "\"" + (fields.Length > 0 ? ", " + fields : "") + " }";

    private static string AtBody(int bodyId = BodyId) =>
        $"\"StarSystem\":\"Hyades Sector AB-C d1\", \"SystemAddress\":{System}, \"Body\":\"Hyades Sector AB-C d1 {bodyId}\", \"BodyID\":{bodyId}";

    private static string Refine(string time, string type = "platinum", string name = "Platinum") =>
        Line(time, "MiningRefined", $"\"Type\":\"${type}_name;\", \"Type_Localised\":\"{name}\"");

    private static string Login(string time) => Line(time, "LoadGame", $"\"FID\":\"{Fid}\", \"Commander\":\"Jameson\"");

    private static readonly string[] OneRun =
    [
        Login("09:00:00"),
        Line("09:50:00", "ApproachBody", AtBody()),
        Line("09:55:00", "Touchdown", AtBody() + ", \"Latitude\":12.5, \"Longitude\":-40.25, \"PlayerControlled\":true"),
        Line("10:00:00", "LaunchSRV", "\"SRVType\":\"mev_rhino\", \"PlayerControlled\":true"),
        Refine("10:01:00"),
        Refine("10:02:00", "osmium", "Osmium"),
        Refine("10:03:00"),
        Line("10:10:00", "DockSRV", "\"SRVType\":\"mev_rhino\""),
    ];

    private static readonly string[] ARestartMidRun =
    [
        Login("09:00:00"),
        Line("09:50:00", "ApproachBody", AtBody()),
        Line("10:00:00", "LaunchSRV", "\"PlayerControlled\":true"),
        Refine("10:01:00"),
        Line("10:05:00", "Shutdown"),
        Login("11:00:00"),
        Line("11:00:05", "Location", AtBody() + ", \"InSRV\":true, \"Latitude\":-3.5, \"Longitude\":101.75"),
        Refine("11:01:00"),
        Line("11:10:00", "DockSRV"),
    ];

    private static JournalEvent Event(string json)
    {
        Assert.True(JournalEvent.TryParse(json, NullLogger.Instance, out var parsed));
        return parsed!;
    }

    private static SurfaceMiningSites Fold(IEnumerable<string> lines) =>
        lines.Aggregate(SurfaceMiningSites.None, (sites, line) => sites.Apply(Event(line)));

    private static SurfaceMiningSites Walk(params string[][] journals)
    {
        var files = new MemoryFileSystem();
        var paths = new List<string>();

        for (var i = 0; i < journals.Length; i++)
        {
            paths.Add($"j/Journal.2025-07-{10 + i:00}T000000.01.log");
            files.WriteLines(paths[^1], journals[i]);
        }

        return SurfaceMiningBackfill.FromHistory(files, paths, NullLogger.Instance, TestContext.Current.CancellationToken)[Fid];
    }

    [Fact]
    public void ARhinoRunGivesOneSiteWithItsTonnesAndTheTouchdownPosition()
    {
        var site = Assert.Single(Fold(OneRun).Sites);
        var run = Assert.Single(site.Runs);

        Assert.Equal("Hyades Sector AB-C d1 7", site.Body);
        Assert.Equal("Hyades Sector AB-C d1", site.StarSystem);
        Assert.Equal(2, run.Refined["platinum"].Tonnes);
        Assert.Equal("Osmium", run.Refined["osmium"].Name);
        Assert.Equal(1, run.Refined["osmium"].Tonnes);
        Assert.Equal(new SurfaceFix(12.5, -40.25, 0), run.At);
        Assert.Equal(DateTimeOffset.Parse("2025-07-10T10:01:00Z"), run.OpenedAt);
        Assert.Equal(DateTimeOffset.Parse("2025-07-10T10:10:00Z"), run.ClosedAt);
    }

    [Fact]
    public void ARestartInTheSrvGivesTwoRunsTheSecondAtTheLocationPosition()
    {
        var runs = Fold(ARestartMidRun).At(System, BodyId)!.Runs;

        Assert.Equal(2, runs.Count);
        Assert.Null(runs[0].At);
        Assert.Equal(DateTimeOffset.Parse("2025-07-10T10:05:00Z"), runs[0].ClosedAt);
        Assert.Equal(new SurfaceFix(-3.5, 101.75, 0), runs[1].At);
        Assert.Equal(1, runs[1].TonnesRefined);
    }

    [Fact]
    public void ARunWithNoTouchdownOrLocationOnItsBodyHasNoPosition()
    {
        var run = Assert.Single(Fold(
        [
            Line("09:40:00", "ApproachBody", AtBody(3)),
            Line("09:41:00", "Touchdown", AtBody(3) + ", \"Latitude\":1.0, \"Longitude\":2.0"),
            Line("09:45:00", "LeaveBody", AtBody(3)),
            Line("09:50:00", "ApproachBody", AtBody()),
            Line("10:00:00", "LaunchSRV"),
            Refine("10:01:00"),
            Line("10:10:00", "DockSRV"),
        ]).At(System, BodyId)!.Runs);

        Assert.Null(run.At);
    }

    [Fact]
    public void AnSrvLaunchThatRefinesNothingIsNoSite() =>
        Assert.Empty(Fold([Line("09:50:00", "ApproachBody", AtBody()), Line("10:00:00", "LaunchSRV"), Line("10:10:00", "DockSRV")]).Sites);

    [Fact]
    public void ARefineWithNoKnownBodyIsNotRecorded() =>
        Assert.Empty(Fold([Line("10:00:00", "LaunchSRV"), Refine("10:01:00"), Line("10:10:00", "DockSRV")]).Sites);

    [Fact]
    public void RefiningOutsideAnSrvIsNotSurfaceMining() =>
        Assert.Empty(Fold([Line("09:50:00", "ApproachBody", AtBody()), Refine("10:01:00")]).Sites);

    [Fact]
    public void AMergeCountsARunInTheWalkAndTheLiveJournalOnce()
    {
        var walk = Fold(ARestartMidRun);
        var live = Fold(ARestartMidRun[5..]);

        var merged = walk.With(live);

        Assert.Equal(2, merged.At(System, BodyId)!.Runs.Count);
        Assert.Equal(2, walk.With(merged).At(System, BodyId)!.Runs.Count);
        Assert.True(merged.IsWhole);
    }

    [Fact]
    public void ALiveRunStillOpenInTheWalkTakesTheLiveTonnes()
    {
        var walk = Fold(OneRun[..5]);
        var live = Fold(OneRun);

        var run = Assert.Single(walk.With(live).At(System, BodyId)!.Runs);

        Assert.Equal(3, run.TonnesRefined);
        Assert.NotNull(run.ClosedAt);
    }

    [Fact]
    public void TheWalkFindsWhatTheLiveFoldFinds()
    {
        var walked = Walk(ARestartMidRun[..5], ARestartMidRun[5..]).At(System, BodyId)!.Runs;
        var folded = Fold(ARestartMidRun).At(System, BodyId)!.Runs;

        Assert.Equal(folded, walked, (a, b) => a.OpenedAt == b.OpenedAt && a.ClosedAt == b.ClosedAt && a.At == b.At && a.TonnesRefined == b.TonnesRefined);
    }

    [Fact]
    public void ANewCommanderClearsTheSitesLiveAndInTheWalk()
    {
        var erase = Line("12:00:00", "NewCommander", $"\"FID\":\"{Fid}\", \"Name\":\"Jameson\", \"Package\":\"Default\"");
        var store = new GameStateStore();

        foreach (var line in OneRun)
        {
            store.Apply(Event(line));
        }

        Assert.True(store.Active!.SurfaceMining.IsKnown);

        store.Apply(Event(erase));

        Assert.False(store.Active!.SurfaceMining.IsKnown);
        Assert.Empty(Walk([.. OneRun, erase, Login("13:00:00")]).Sites);
    }

    [Fact]
    public void TheStoreMergesTheRestoredSitesWhenTheWalkFinishesLate()
    {
        var walked = Walk(ARestartMidRun[..5], ARestartMidRun[5..]);
        var ready = false;
        var store = new GameStateStore { RestoreSurfaceMining = _ => ready ? walked : null };

        foreach (var line in ARestartMidRun[5..])
        {
            store.Apply(Event(line));
        }

        Assert.Single(store.Active!.SurfaceMining.At(System, BodyId)!.Runs);

        ready = true;
        store.RestoreLate();

        Assert.Equal(2, store.Active!.SurfaceMining.At(System, BodyId)!.Runs.Count);
        Assert.True(store.Active!.SurfaceMining.IsWhole);
    }

    [Fact]
    public void ACommanderMetAfterTheWalkIsAdmittedWithTheRestoredSites()
    {
        var walked = Walk(OneRun);
        var store = new GameStateStore { RestoreSurfaceMining = _ => walked };

        store.Apply(Event(Login("12:00:00")));

        Assert.Single(store.Active!.SurfaceMining.At(System, BodyId)!.Runs);
        Assert.True(store.Active!.SurfaceMining.IsWhole);
    }

    [Fact]
    public void ApproachingABodyIsNoChangeToTheSites()
    {
        var store = new GameStateStore();
        store.Apply(Event(Login("09:00:00")));

        var receipt = store.Apply(Event(Line("09:50:00", "ApproachBody", AtBody())));

        Assert.DoesNotContain(receipt.Changes, change => change.Part == nameof(CommanderGameState.SurfaceMining));
    }
}
