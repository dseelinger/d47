using System.Globalization;
using D47.Core.Journal;
using D47.Core.Storage;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Journal;

/// <summary>System factions and conflicts recovered from earlier journals, with the live readings laid over them (#2256).</summary>
public sealed class SystemStandingsSurviveARestartTests
{
    private const string Here = "Col 285 Sector AB-C d1";

    private const string Ours = "Diamond Frogs";

    private static readonly DateTimeOffset Tick = new(2026, 10, 11, 0, 30, 0, TimeSpan.Zero);

    private static CancellationToken Cancel => TestContext.Current.CancellationToken;

    private static string Jump(string time, double ours, string system = Here) =>
        "{\"timestamp\":\"" + time + "Z\",\"event\":\"FSDJump\",\"StarSystem\":\"" + system + "\",\"SystemAddress\":1,"
        + "\"Factions\":[{\"Name\":\"" + Ours + "\",\"Influence\":" + ours.ToString(CultureInfo.InvariantCulture)
        + "},{\"Name\":\"Rivals\",\"Influence\":" + (1 - ours).ToString(CultureInfo.InvariantCulture) + "}]}";

    private static string Login(string time, string fid = "F1") =>
        "{\"timestamp\":\"" + time + "Z\",\"event\":\"LoadGame\",\"FID\":\"" + fid + "\",\"Commander\":\"Jameson\"}";

    private static SystemStandings Folded(params string[] lines) =>
        lines.Aggregate(SystemStandings.Empty, (standings, line) => standings.Apply(Event(line)));

    private static JournalEvent Event(string line)
    {
        Assert.True(JournalEvent.TryParse(line, NullLogger.Instance, out var parsed));
        return parsed!;
    }

    [Fact]
    public void ANewerLiveReadingIsTheLatest()
    {
        var recovered = Folded(Jump("2026-10-10T20:00:00", 0.40));
        var live = Folded(Jump("2026-10-10T22:00:00", 0.45));

        Assert.Equal(0.45, recovered.With(live).Latest(Here)!.InfluenceOf(Ours));
    }

    [Fact]
    public void ARecoveredEarlierDayReadingGivesTheChange()
    {
        var recovered = Folded(Jump("2026-10-10T20:00:00", 0.40));
        var live = Folded(Jump("2026-10-11T09:00:00", 0.43));

        Assert.Equal(0.03, recovered.With(live).InfluenceChange(Here, Ours)!.Value, 6);
    }

    [Fact]
    public void InfluenceAcrossTheTickTakesItsBeforeFromTheRecoveredSide()
    {
        var recovered = Folded(Jump("2026-10-10T20:00:00", 0.40));
        var live = Folded(Jump("2026-10-11T09:00:00", 0.43));

        Assert.Null(live.InfluenceAcross(Here, Ours, Tick));

        var across = recovered.With(live).InfluenceAcross(Here, Ours, Tick);

        Assert.Equal(0.40, across!.Value.Before);
        Assert.Equal(0.43, across.Value.After);
    }

    [Fact]
    public void AReadingInBothIsKeptOnce()
    {
        var lines = new[] { Jump("2026-10-10T20:00:00", 0.40), Jump("2026-10-11T09:00:00", 0.43) };
        var once = Folded(lines);

        var merged = Folded(lines).With(Folded(lines[1]));

        Assert.Equal(once.InfluenceChange(Here, Ours), merged.InfluenceChange(Here, Ours));
        Assert.Equal(
            once.InfluenceAcross(Here, Ours, Tick),
            merged.InfluenceAcross(Here, Ours, Tick));
    }

    [Fact]
    public void ASystemOnlyLiveIsTakenAsItIs()
    {
        var recovered = Folded(Jump("2026-10-10T20:00:00", 0.40, "Other"));
        var live = Folded(Jump("2026-10-11T09:00:00", 0.43));

        var merged = recovered.With(live);

        Assert.Equal(0.40, merged.Latest("Other")!.InfluenceOf(Ours));
        Assert.Equal(0.43, merged.Latest(Here)!.InfluenceOf(Ours));
    }

    [Fact]
    public void TheWalkFoldsEachCommandersReadings()
    {
        var files = new MemoryFileSystem();
        files.WriteLines("j/Journal.2026-10-10T000000.01.log", [Login("2026-10-10T19:00:00"), Jump("2026-10-10T20:00:00", 0.40)]);
        files.WriteLines("j/Journal.2026-10-11T000000.01.log", [Login("2026-10-11T08:00:00", "G2"), Jump("2026-10-11T09:00:00", 0.43)]);

        var found = SystemStandingsBackfill.FromHistory(
            files,
            ["j/Journal.2026-10-10T000000.01.log", "j/Journal.2026-10-11T000000.01.log"],
            NullLogger.Instance,
            Cancel);

        Assert.Equal(0.40, found["F1"].Latest(Here)!.InfluenceOf(Ours));
        Assert.Equal(0.43, found["G2"].Latest(Here)!.InfluenceOf(Ours));
    }

    [Fact]
    public void TheWalkSkipsALocationInsideAnOperation()
    {
        var files = new MemoryFileSystem();
        var location = "{\"timestamp\":\"2026-10-10T20:05:00Z\",\"event\":\"Location\",\"StarSystem\":\"" + Here
            + "\",\"SystemAddress\":1,\"Factions\":[{\"Name\":\"" + Ours + "\",\"Influence\":0.9}]}";
        files.WriteLines(
            "j/Journal.2026-10-10T000000.01.log",
            [
                Login("2026-10-10T19:00:00"),
                Jump("2026-10-10T20:00:00", 0.40),
                Login("2026-10-10T20:04:00"),
                "{\"timestamp\":\"2026-10-10T20:04:30Z\",\"event\":\"GameModeChange\",\"GameMode\":\"Operation\"}",
                location,
            ]);

        var found = SystemStandingsBackfill.FromHistory(files, ["j/Journal.2026-10-10T000000.01.log"], NullLogger.Instance, Cancel);

        Assert.Equal(0.40, found["F1"].Latest(Here)!.InfluenceOf(Ours));
    }

    [Fact]
    public void ACommanderWithNoReadingIsLeftOut()
    {
        var files = new MemoryFileSystem();
        files.WriteLines("j/Journal.2026-10-10T000000.01.log", [Login("2026-10-10T19:00:00")]);

        Assert.Empty(SystemStandingsBackfill.FromHistory(files, ["j/Journal.2026-10-10T000000.01.log"], NullLogger.Instance, Cancel));
    }
}
