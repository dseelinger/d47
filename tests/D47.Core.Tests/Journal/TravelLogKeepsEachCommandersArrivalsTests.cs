using D47.Core.Journal;
using D47.Core.Storage;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Journal;

public class TravelLogKeepsEachCommandersArrivalsTests
{
    private static string Commander(string fid) =>
        "{\"timestamp\":\"2026-10-01T09:00:00Z\",\"event\":\"LoadGame\",\"FID\":\"" + fid + "\"}";

    private static string Arrive(string kind, string time, long address, string system, double x = 1, bool position = true) =>
        "{\"timestamp\":\"" + time + "\",\"event\":\"" + kind + "\",\"StarSystem\":\"" + system
        + "\",\"SystemAddress\":" + address + (position ? ",\"StarPos\":[" + x + ",2,3]" : string.Empty) + "}";

    private static TravelLog Log() => new(new MemoryFileSystem(), NullLogger.Instance);

    private static TravelLog Folded(params string[] lines)
    {
        var log = Log();
        log.Fold(lines);
        return log;
    }

    private static JournalEvent Event(string line)
    {
        Assert.True(JournalEvent.TryParse(line, NullLogger.Instance, out var parsed));
        return parsed!;
    }

    [Theory]
    [InlineData("FSDJump", TravelArrival.Jump)]
    [InlineData("CarrierJump", TravelArrival.CarrierJump)]
    [InlineData("Location", TravelArrival.Location)]
    public void EachArrivalEventBecomesAStepWithItsHow(string kind, TravelArrival how)
    {
        var step = Assert.Single(Folded(Commander("F1"), Arrive(kind, "2026-10-01T10:00:00Z", 5, "Alpha", 7)).Steps("F1"));

        Assert.Equal(how, step.How);
        Assert.Equal(5, step.SystemAddress);
        Assert.Equal("Alpha", step.System);
        Assert.Equal(new StarPosition(7, 2, 3), step.Position);
    }

    [Fact]
    public void AnArrivalWithoutAStarPosIsSkipped() =>
        Assert.Empty(Folded(Commander("F1"), Arrive("FSDJump", "2026-10-01T10:00:00Z", 5, "Alpha", position: false)).Steps("F1"));

    [Fact]
    public void TwoCommandersStepsAreKeptApartByFid()
    {
        var log = Folded(
            Commander("F1"),
            Arrive("FSDJump", "2026-10-01T10:00:00Z", 1, "Alpha"),
            Commander("F2"),
            Arrive("FSDJump", "2026-10-01T11:00:00Z", 2, "Beta"));

        Assert.Equal("Alpha", Assert.Single(log.Steps("F1")).System);
        Assert.Equal("Beta", Assert.Single(log.Steps("F2")).System);
        Assert.Empty(log.Steps("F3"));
    }

    [Fact]
    public void AStepBeforeAnyFidInItsFileIsDropped() =>
        Assert.Empty(Folded(Arrive("FSDJump", "2026-10-01T10:00:00Z", 1, "Alpha"), Commander("F1")).Steps("F1"));

    [Fact]
    public void TheSameStepFromFoldAndApplyIsKeptOnceAndInOrder()
    {
        var log = Log();
        log.Apply([Event(Commander("F1")), Event(Arrive("FSDJump", "2026-10-01T12:00:00Z", 3, "Gamma"))]);
        log.Fold(
        [
            Commander("F1"),
            Arrive("FSDJump", "2026-10-01T10:00:00Z", 1, "Alpha"),
            Arrive("FSDJump", "2026-10-01T11:00:00Z", 2, "Beta"),
            Arrive("FSDJump", "2026-10-01T12:00:00Z", 3, "Gamma"),
        ]);

        Assert.Equal(["Alpha", "Beta", "Gamma"], log.Steps("F1").Select(step => step.System));
    }

    [Fact]
    public void PositionOfIsTheLatestVisitWithoutRegardToCase()
    {
        var log = Folded(
            Commander("F1"),
            Arrive("FSDJump", "2026-10-01T10:00:00Z", 1, "Alpha", 1),
            Arrive("FSDJump", "2026-10-01T11:00:00Z", 1, "Alpha", 9),
            Arrive("FSDJump", "2026-10-01T12:00:00Z", 2, "Beta", 4));

        Assert.Equal(new StarPosition(9, 2, 3), log.PositionOf("F1", "ALPHA"));
        Assert.Null(log.PositionOf("F1", "Nowhere"));
        Assert.Null(log.PositionOf("F2", "Alpha"));
    }

    [Fact]
    public void StepsIsACopy()
    {
        var log = Folded(Commander("F1"), Arrive("FSDJump", "2026-10-01T10:00:00Z", 1, "Alpha"));
        var before = log.Steps("F1");

        log.Apply([Event(Commander("F1")), Event(Arrive("FSDJump", "2026-10-01T11:00:00Z", 2, "Beta"))]);

        Assert.Single(before);
        Assert.Equal(2, log.Steps("F1").Count);
    }
}
