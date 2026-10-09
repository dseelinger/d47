using D47.Core.Callouts;
using D47.Core.Journal;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Callouts;

/// <summary>A conflict zone, replayed from the journal, emits one ship scene beat per moment, spaced.</summary>
public class AConflictZonePlaysAsShipScenesTests
{
    private static IReadOnlyList<JournalEvent> Fight()
    {
        return [.. EmbeddedFixture.Lines("scenes.Journal.2026-09-29T144511.01.log").Select(line =>
        {
            Assert.True(JournalEvent.TryParse(line, NullLogger.Instance, out var parsed));
            return parsed!;
        })];
    }

    /// <summary>Ticks once a second across the fixture, folding each event on the tick it falls in.</summary>
    private static List<(DateTimeOffset At, SceneBeat Beat)> Replay(
        Func<bool>? enabled = null, string? scenario = "Holding the line for LTT 7786 Labour.")
    {
        var events = Fight();
        var tracker = new SceneTracker();
        var callout = new SceneCallout(tracker)
        {
            Enabled = enabled ?? (() => true),
            Scenario = () => scenario,
        };

        var heard = new List<(DateTimeOffset, SceneBeat)>();
        var start = events[0].Timestamp;
        var end = events[^1].Timestamp.AddMinutes(2);

        Tick(new CalloutContext(start.AddSeconds(-1), true, null, GameStatus.Unknown, NavRoute.None, []));

        var next = 0;

        for (var now = start; now <= end; now = now.AddSeconds(1))
        {
            var due = new List<JournalEvent>();

            while (next < events.Count && events[next].Timestamp <= now)
            {
                tracker.Fold(events[next]);
                due.Add(events[next++]);
            }

            Tick(new CalloutContext(now, false, null, GameStatus.Unknown, NavRoute.None, due));
        }

        return heard;

        void Tick(CalloutContext context)
        {
            foreach (var marker in callout.Examine(context))
            {
                Assert.Equal(SceneCallout.Key, marker.Key);
                heard.Add((context.Now, marker.Scene!));
            }
        }
    }

    [Fact]
    public void TheFightIsHeardAsSiteEngagedOneMergedKillAndGone()
    {
        var beats = Replay().Select(heard => heard.Beat).ToList();

        Assert.Equal(
            [SceneBeatKind.Site, SceneBeatKind.Engaged, SceneBeatKind.ShipDown, SceneBeatKind.Gone],
            beats.Select(beat => beat.Kind));

        Assert.All(beats, beat => Assert.Equal(ScenePlace.Ship, beat.Place));
        Assert.Equal("Conflict Zone [Low Intensity]", beats[0].Site);
        Assert.Equal("LTT 7786", beats[1].System);

        var down = beats[2];
        Assert.Equal((3, 3), (down.Kills, down.Merged));
        Assert.Null(down.Victim);
        Assert.Equal("LTT 7786 Major Group", down.Faction);
        Assert.Equal(["LTT 7786 Labour", "LTT 7786 Major Group"], down.Sides!);
    }

    [Fact]
    public void TheBriefNamesBothSidesOfTheConflictZone()
    {
        var down = Replay().Single(heard => heard.Beat.Kind == SceneBeatKind.ShipDown).Beat;

        var brief = NpcChatter.SceneInstruction(down, "Holding the line for LTT 7786 Labour.");

        Assert.Contains("at a Conflict Zone [Low Intensity] in LTT 7786", brief, StringComparison.Ordinal);
        Assert.Contains("for LTT 7786 Labour or for LTT 7786 Major Group", brief, StringComparison.Ordinal);
        Assert.Contains("3 ships flying for LTT 7786 Major Group have just been destroyed.", brief, StringComparison.Ordinal);
        Assert.Contains("which this fight is part of", brief, StringComparison.Ordinal);
    }

    [Fact]
    public void NoBeatFallsWithinTheSpacingOfTheOneBefore()
    {
        var heard = Replay();

        for (var i = 1; i < heard.Count; i++)
        {
            Assert.True(heard[i].At - heard[i - 1].At >= SceneCallout.Spacing, $"{heard[i].Beat.Kind} came too soon");
        }
    }

    [Fact]
    public void WithNoScenarioSetNothingIsEmitted() => Assert.Empty(Replay(scenario: null));

    [Fact]
    public void WithScenesOffNothingIsEmitted() => Assert.Empty(Replay(enabled: () => false));
}
