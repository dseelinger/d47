using D47.Core.Journal;
using D47.Core.Storage;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Journal;

public class ErasedPilotDataClearsTheTravelLogTests
{
    private static string Commander(string fid) =>
        "{\"timestamp\":\"2026-10-01T09:00:00Z\",\"event\":\"Commander\",\"FID\":\"" + fid + "\"}";

    private static string Erase(string fid, string time) =>
        "{\"timestamp\":\"" + time + "\",\"event\":\"NewCommander\",\"FID\":\"" + fid + "\",\"Name\":\"X\"}";

    private static string Jump(string time, long address, string system) =>
        "{\"timestamp\":\"" + time + "\",\"event\":\"FSDJump\",\"StarSystem\":\"" + system
        + "\",\"SystemAddress\":" + address + ",\"StarPos\":[1,2,3]}";

    private static string[] Lines(string fid = "F") =>
    [
        Commander(fid),
        Jump("2026-10-01T10:00:00Z", 1, "Alpha"),
        Jump("2026-10-01T11:00:00Z", 2, "Beta"),
        Erase(fid, "2026-10-01T12:00:00Z"),
        Jump("2026-10-01T13:00:00Z", 3, "Gamma"),
    ];

    private static TravelLog Log() => new(new MemoryFileSystem(), NullLogger.Instance);

    private static JournalEvent Event(string line)
    {
        Assert.True(JournalEvent.TryParse(line, NullLogger.Instance, out var parsed));
        return parsed!;
    }

    [Fact]
    public void ArrivalsBeforeANewCommanderAreDroppedFromTheHistoryWalk()
    {
        var log = Log();
        log.Fold(Lines());

        Assert.Equal("Gamma", Assert.Single(log.Steps("F")).System);
    }

    [Fact]
    public void ArrivalsBeforeANewCommanderAreDroppedFromLiveEvents()
    {
        var log = Log();
        log.Apply([.. Lines().Select(Event)]);

        Assert.Equal("Gamma", Assert.Single(log.Steps("F")).System);
    }

    [Fact]
    public void AnotherCommandersStepsSurviveANewCommander()
    {
        var log = Log();
        log.Fold(
        [
            Commander("G"),
            Jump("2026-10-01T09:30:00Z", 9, "Delta"),
            .. Lines(),
        ]);

        Assert.Equal("Delta", Assert.Single(log.Steps("G")).System);
        Assert.Equal("Gamma", Assert.Single(log.Steps("F")).System);
    }
}
