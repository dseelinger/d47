using D47.Core.Callouts;
using D47.Core.Journal;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Callouts;

/// <summary>A raid on foot at a settlement, replayed from the journal, emits one scene beat per moment, spaced.</summary>
public class ASettlementRaidPlaysAsScenesTests
{
    private static IReadOnlyList<JournalEvent> Raid()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "d47.slnx")))
        {
            directory = directory.Parent;
        }

        Assert.NotNull(directory);

        var path = Path.Combine(directory.FullName, "tests", "fixtures", "scenes", "Journal.2026-09-30T004823.01.log");

        return [.. File.ReadAllLines(path).Where(line => line.Length > 0).Select(line =>
        {
            Assert.True(JournalEvent.TryParse(line, NullLogger.Instance, out var parsed));
            return parsed!;
        })];
    }

    /// <summary>Ticks once a second across the fixture, folding each event on the tick it falls in.</summary>
    private static List<(DateTimeOffset At, SceneBeat Beat)> Replay(
        Func<bool>? enabled = null, string? scenario = "Recovering stolen data from a pirate den.", bool primeAll = false)
    {
        var events = Raid();
        var tracker = new SceneTracker();
        var callout = new SceneCallout(tracker)
        {
            Enabled = enabled ?? (() => true),
            Scenario = () => scenario,
        };

        var heard = new List<(DateTimeOffset, SceneBeat)>();
        var start = events[0].Timestamp;
        var end = events[^1].Timestamp.AddMinutes(2);

        var primed = primeAll ? events : [];

        foreach (var journalEvent in primed)
        {
            tracker.Fold(journalEvent);
        }

        Tick(new CalloutContext(start.AddSeconds(-1), true, null, GameStatus.Unknown, NavRoute.None, primed));

        var next = primeAll ? events.Count : 0;

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
                Assert.Equal(string.Empty, marker.Text);
                heard.Add((context.Now, marker.Scene!));
            }
        }
    }

    [Fact]
    public void TheRaidIsHeardInOrder()
    {
        var beats = Replay().Select(heard => heard.Beat).ToList();

        Assert.Equal(
            [
                SceneBeatKind.Arrived,
                SceneBeatKind.Spotted,
                SceneBeatKind.Down,
                SceneBeatKind.Down,
                SceneBeatKind.Gone,
                SceneBeatKind.Arrived,
                SceneBeatKind.CommanderDown,
            ],
            beats.Select(beat => beat.Kind));

        Assert.Equal("Archambeau's Serenity", beats[0].Settlement);
        Assert.Equal("LTT 7786 Silver Partnership", beats[0].Faction);
        Assert.Equal("Anarchy", beats[0].Government);
        Assert.Equal("LTT 7786 6 a", beats[0].Body);

        Assert.Equal("Hugh Juarez", beats[2].Victim);
        Assert.Equal((1, 1), (beats[2].Kills, beats[2].Merged));

        Assert.Equal("Sarai Prifti", beats[3].Victim);
        Assert.Equal((3, 2), (beats[3].Kills, beats[3].Merged));
        Assert.True(beats[3].Seen);

        Assert.Equal("Stevie Preston", beats[6].Killer);
    }

    [Fact]
    public void NoBeatFallsWithinTheSpacingOfTheOneBeforeExceptTheCommandersDeath()
    {
        var heard = Replay();

        for (var i = 1; i < heard.Count; i++)
        {
            if (heard[i].Beat.Kind != SceneBeatKind.CommanderDown)
            {
                Assert.True(heard[i].At - heard[i - 1].At >= SceneCallout.Spacing, $"{heard[i].Beat.Kind} came too soon");
            }
        }

        var death = heard.Single(h => h.Beat.Kind == SceneBeatKind.CommanderDown);
        var before = heard[heard.IndexOf(death) - 1];

        Assert.True(death.At - before.At < SceneCallout.Spacing);
    }

    [Fact]
    public void WithNoScenarioSetNothingIsEmitted() => Assert.Empty(Replay(scenario: " "));

    [Fact]
    public void WithScenesOffNothingIsEmitted() => Assert.Empty(Replay(enabled: () => false));

    [Fact]
    public void NothingIsEmittedForWhatWasPrimed() => Assert.Empty(Replay(primeAll: true));
}
