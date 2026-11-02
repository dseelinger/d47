using D47.Core.Callouts;
using D47.Core.Journal;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Callouts;

/// <summary>
/// Elite changing a route without rewriting <c>NavRoute.json</c>: the route callouts follow its targets, not the
/// file (#2061).
/// </summary>
public class ARouteElitePlotsAgainIsSpokenFromItsTargetsTests
{
    /// <summary>The file both fixtures plot; the diverging one flies a different route after the first target.</summary>
    private static readonly NavRoute Plotted = new()
    {
        Hops =
        [
            new RouteHop("Pareco", "K") { Position = (0, 0, 0) },
            new RouteHop("Shui Wei Sector DB-X c1-18", "K") { Position = (10, 0, 0) },
            new RouteHop("Van Maanen's Star", "DZ") { Position = (20, 0, 0) },
            new RouteHop("Jackson's Lighthouse", "N") { Position = (30, 0, 0) },
            new RouteHop("Anlave", "G") { Position = (40, 0, 0) },
        ],
        ReadAt = DateTimeOffset.Parse("2026-11-01T15:05:38.4Z"),
    };

    private sealed record Tick(JournalEvent Event, bool Changed, IReadOnlyList<Announcement> Said);

    /// <summary>One event per tick, folded as the journal tick folds it, with the file read on the NavRoute event.</summary>
    private static List<Tick> Replay(string fixture, bool checkedAgainstTargets = true)
    {
        var store = new GameStateStore();
        var live = new LiveRoute();
        var route = new RouteCallout { EveryNJumps = 1 };
        var fuel = new FuelCallout();
        var file = NavRoute.None;
        var ticks = new List<Tick>();

        foreach (var line in EmbeddedFixture.Lines($"routes.{fixture}.log"))
        {
            Assert.True(JournalEvent.TryParse(line, NullLogger.Instance, out var parsed));
            var journalEvent = parsed!;

            store.Apply(journalEvent);

            if (journalEvent.Kind == "NavRoute")
            {
                file = Plotted;
            }

            live.Fold([journalEvent], file, store.InGame?.Location);

            var context = new CalloutContext(
                journalEvent.Timestamp, false, store.Active, GameStatus.Unknown, file, [journalEvent])
            {
                LiveRoute = checkedAgainstTargets ? live : null,
            };

            ticks.Add(new Tick(journalEvent, live.Changed, [.. route.Examine(context), .. fuel.Examine(context)]));
        }

        return ticks;
    }

    private static IEnumerable<Announcement> AtArrivals(List<Tick> ticks) =>
        ticks.Where(tick => tick.Event.Kind == "FSDJump").SelectMany(tick => tick.Said);

    [Fact]
    public void TargetsThatLeaveTheFileAreSpokenFromTheTarget()
    {
        var ticks = Replay("diverging");
        var arrivals = ticks.Where(tick => tick.Event.Kind == "FSDJump").ToArray();

        var first = Assert.Single(arrivals[0].Said);
        Assert.Equal("route.progress", first.Key);
        Assert.Equal("4 jumps remaining. Next is LP 988-175, scoopable.", first.Text);

        // The target's own class sets the hazard and the scoopability.
        Assert.Equal(
            [
                "route.hazard: Next jump is a white dwarf at LHS 4058. Throttle down on arrival.",
                "route.progress: 3 jumps remaining. Next is LHS 4058, a white dwarf — not scoopable.",
            ],
            arrivals[1].Said.Select(announcement => $"{announcement.Key}: {announcement.Text}"));
    }

    [Fact]
    public void TheFilesStarsAreNotWarnedOfOnceTheTargetsLeaveIt()
    {
        var said = Replay("diverging").SelectMany(tick => tick.Said).ToArray();

        Assert.DoesNotContain(said, announcement => announcement.Key == "route.ahead");
        Assert.DoesNotContain(said, announcement => announcement.Key.StartsWith("fuel.", StringComparison.Ordinal));
        Assert.DoesNotContain(said, announcement => announcement.Text.Contains("Van Maanen", StringComparison.Ordinal));
    }

    [Fact]
    public void ACountThatMovesUnderTheSameNameMarksTheRouteChanged()
    {
        var ticks = Replay("diverging");

        // The plot's target matches the file; the supercruise target names the same system one jump further out.
        var bumped = ticks.FindIndex(tick =>
            tick.Event.Kind == "FSDTarget" && tick.Event.Int("RemainingJumpsInRoute") == 5);

        Assert.All(ticks.Take(bumped), tick => Assert.False(tick.Changed, tick.Event.Kind));
        Assert.All(ticks.Skip(bumped), tick => Assert.True(tick.Changed, tick.Event.Kind));
    }

    [Fact]
    public void TargetsThatMatchTheFileChangeNothing()
    {
        var checkedAgainstTargets = Replay("matching");
        var fileOnly = Replay("matching", checkedAgainstTargets: false);

        string[] Said(List<Tick> ticks) =>
            [.. ticks.SelectMany(tick => tick.Said).Select(announcement => $"{announcement.Key}: {announcement.Text}")];

        Assert.Equal(Said(fileOnly), Said(checkedAgainstTargets));

        // The file is what is spoken: the white dwarf, the neutron star after it, and the fuel line.
        var keys = AtArrivals(checkedAgainstTargets).Select(announcement => announcement.Key).ToArray();
        Assert.Contains("route.hazard", keys);
        Assert.Contains("route.ahead", keys);
        Assert.Contains("fuel.route.unscoopable", keys);
    }

    [Fact]
    public void TheTargetSetInTheTunnelNeverMarksTheRouteChanged()
    {
        var ticks = Replay("matching");

        Assert.Contains(ticks, tick => tick.Event.Kind == "StartJump" && tick.Event.String("JumpType") == "Hyperspace");
        Assert.All(ticks, tick => Assert.False(tick.Changed, $"{tick.Event.Kind} at {tick.Event.Timestamp:T}"));
    }
}
