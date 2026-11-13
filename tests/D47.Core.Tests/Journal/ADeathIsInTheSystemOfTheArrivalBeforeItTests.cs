using D47.Core.Journal;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Journal;

public class ADeathIsInTheSystemOfTheArrivalBeforeItTests
{
    private static TravelLog Log() => new(new MemoryInstall().Files, NullLogger.Instance);

    private static string Jump(string time, string system, long address) =>
        "{\"timestamp\":\"" + time + "\",\"event\":\"FSDJump\",\"StarSystem\":\"" + system + "\",\"SystemAddress\":" + address + ",\"StarPos\":[" + address + ",0,0]}";

    private static string Located(string time, string system, long address) =>
        "{\"timestamp\":\"" + time + "\",\"event\":\"Location\",\"StarSystem\":\"" + system + "\",\"SystemAddress\":" + address + ",\"StarPos\":[" + address + ",0,0]}";

    private static string Died(string time) =>
        "{\"timestamp\":\"" + time + "\",\"event\":\"Died\",\"KillerName\":\"Pirate\"}";

    private const string Load = "{\"timestamp\":\"2026-10-01T09:00:00Z\",\"event\":\"LoadGame\",\"FID\":\"F1\"}";

    [Fact]
    public void ADeathAfterARespawnInAnotherSystemIsInTheSystemOfTheJumpBeforeIt()
    {
        var log = Log();

        log.Fold(
        [
            Load,
            Jump("2026-10-01T09:05:00Z", "Alpha", 1),
            Died("2026-10-01T09:10:00Z"),
            Located("2026-10-01T09:15:00Z", "Beta", 2),
            Jump("2026-10-01T09:20:00Z", "Gamma", 3),
            Died("2026-10-01T09:25:00Z"),
        ]);

        Assert.Equal(["Alpha", "Gamma"], log.Deaths("F1").Select(place => place.System));
        Assert.All(log.Deaths("F1"), place => Assert.Equal(1, place.Count));
    }

    [Fact]
    public void ADeathBeforeAnyArrivalDrawsNoMark()
    {
        var log = Log();

        log.Fold([Load, Died("2026-10-01T09:01:00Z"), Jump("2026-10-01T09:05:00Z", "Alpha", 1)]);

        Assert.Empty(log.Deaths("F1"));
    }

    [Fact]
    public void TwoDeathsInOneSystemAreOnePlaceWithACountOfTwo()
    {
        var log = Log();

        log.Fold([Load, Jump("2026-10-01T09:05:00Z", "Alpha", 1), Died("2026-10-01T09:10:00Z"), Died("2026-10-01T09:20:00Z")]);

        var place = Assert.Single(log.Deaths("F1"));
        Assert.Equal(2, place.Count);
        Assert.Equal(2, log.DeathCount("F1"));
    }

    private static JournalEvent Live(string line)
    {
        Assert.True(JournalEvent.TryParse(line, NullLogger.Instance, out var parsed));

        return parsed!;
    }

    [Fact]
    public void ALiveDeathIsPlacedOnTheNextRead()
    {
        var log = Log();

        log.Apply([Live(Load), Live(Jump("2026-10-01T09:05:00Z", "Alpha", 1))]);
        Assert.Empty(log.Deaths("F1"));
        Assert.Equal(0, log.DeathCount("F1"));

        log.Apply([Live(Died("2026-10-01T09:10:00Z"))]);

        Assert.Equal(["Alpha"], log.Deaths("F1").Select(place => place.System));
        Assert.Equal(1, log.DeathCount("F1"));
    }
}
