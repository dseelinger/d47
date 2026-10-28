using D47.Core.Callouts;
using D47.Core.Configuration;
using D47.Core.Journal;
using D47.Core.Storage;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Journal;

public class SrvRefiningIsNotRingMiningTests
{
    private const string Fid = "F1";

    private const string DockedLine = """{ "timestamp":"2025-07-10T12:14:28Z", "event":"Docked", "StationName":"Jameson Memorial" }""";

    private static string Line(string time, string kind, string fields = "") =>
        "{ \"timestamp\":\"2025-07-10T" + time + "Z\", \"event\":\"" + kind + "\"" + (fields.Length > 0 ? ", " + fields : "") + " }";

    private static string Refine(string time) =>
        Line(time, "MiningRefined", "\"Type\":\"$platinum_name;\", \"Type_Localised\":\"Platinum\"");

    private static string Rock(string time) =>
        Line(time, "ProspectedAsteroid", "\"Materials\":[ { \"Name\":\"Platinum\", \"Proportion\":58.2 } ], \"Content\":\"$AsteroidMaterialContent_Low;\"");

    private static List<string> RhinoRun() =>
    [
        Line("10:00:00", "LaunchSRV", "\"SRVType\":\"mev_rhino\", \"PlayerControlled\":true"),
        Refine("10:01:00"),
        Refine("10:02:00"),
        Refine("10:03:00"),
        Line("10:10:00", "DockSRV", "\"SRVType\":\"mev_rhino\""),
        DockedLine,
    ];

    private static JournalEvent Event(string json)
    {
        Assert.True(JournalEvent.TryParse(json, NullLogger.Instance, out var parsed));
        return parsed!;
    }

    private static CommanderGameState StateFrom(IEnumerable<string> lines)
    {
        var store = new GameStateStore();
        store.Apply(Event($$"""{ "timestamp":"2025-07-10T06:23:44Z", "event":"Commander", "FID":"{{Fid}}", "Name":"Jameson" }"""));

        foreach (var line in lines)
        {
            store.Apply(Event(line));
        }

        return store.Active!;
    }

    [Fact]
    public void SurfaceRefiningOpensNoRun()
    {
        var mining = StateFrom(RhinoRun()).Mining;

        Assert.Null(mining.Open);
        Assert.Null(mining.Last);
    }

    [Fact]
    public void SurfaceRefiningIsNotSummarisedOnDocking()
    {
        var state = StateFrom(RhinoRun());

        var context = new CalloutContext(DateTimeOffset.UnixEpoch, false, state, GameStatus.Unknown, NavRoute.None, [Event(DockedLine)]);

        Assert.Empty(new MiningSummaryCallout().Examine(context));
    }

    [Fact]
    public void ARingRunAfterSurfaceRefiningCountsOnlyItsOwnOre()
    {
        var lines = RhinoRun()[..^1];
        lines.Add(Rock("11:00:00"));
        lines.Add(Refine("11:01:00"));
        lines.Add(Refine("11:02:00"));
        lines.Add(DockedLine);

        Assert.Equal(2, StateFrom(lines).Mining.Last!.TonnesRefined);
    }

    [Fact]
    public void ARunResumedInAnSrvAfterARestartIsNotRingMining()
    {
        var mining = StateFrom(
        [
            Line("10:00:00", "LoadGame"),
            Line("10:00:05", "Location", "\"InSRV\":true"),
            Refine("10:01:00"),
            Line("10:10:00", "DockSRV"),
            DockedLine,
        ]).Mining;

        Assert.Null(mining.Open);
        Assert.Null(mining.Last);
    }

    [Fact]
    public void RingMiningWithNoSrvEventStillOpensAndCloses()
    {
        var mining = StateFrom([Rock("11:00:00"), Refine("11:01:00"), DockedLine]).Mining;

        Assert.Null(mining.Open);
        Assert.Equal(1, mining.Last!.TonnesRefined);
    }

    [Fact]
    public void TheBackfillFindsNoRunInSurfaceRefining()
    {
        var files = new MemoryFileSystem();
        files.WriteLines("j/Journal.2026-10-10T000000.01.log", [Line("09:00:00", "LoadGame", $"\"FID\":\"{Fid}\""), .. RhinoRun()]);

        var runs = MiningBackfill.FromHistory(files, ["j/Journal.2026-10-10T000000.01.log"], NullLogger.Instance, TestContext.Current.CancellationToken);

        Assert.Empty(runs);
    }

    [Theory]
    [InlineData("LaunchSRV", "", true)]
    [InlineData("LaunchVessel", "\"PlayerControlled\":true", true)]
    [InlineData("LaunchVessel", "\"PlayerControlled\":false", null)]
    [InlineData("Embark", "\"SRV\":true", true)]
    [InlineData("Embark", "\"SRV\":false", false)]
    [InlineData("Location", "\"InSRV\":true", true)]
    [InlineData("Location", "\"Docked\":false", false)]
    [InlineData("LoadGame", "", false)]
    [InlineData("DockSRV", "", false)]
    [InlineData("SRVDestroyed", "", false)]
    [InlineData("Died", "", false)]
    [InlineData("Disembark", "", false)]
    [InlineData("FSDJump", "", null)]
    public void AnEventSaysWhetherTheCommanderIsInAnSrv(string kind, string fields, bool? expected) =>
        Assert.Equal(expected, Event(Line("10:00:00", kind, fields)).InSrvAfter);
}
