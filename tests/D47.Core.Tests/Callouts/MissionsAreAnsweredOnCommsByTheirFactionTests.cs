using D47.Core.Callouts;
using D47.Core.Journal;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Callouts;

/// <summary>
/// Taking, finishing and losing missions, replayed from the journal, emit mission beats merged over a minute,
/// named from the event or, for a loss, from the board.
/// </summary>
[Trait("Category", "Integration")]
public class MissionsAreAnsweredOnCommsByTheirFactionTests
{
    private const string Strike = "Journal.2025-07-12T012607.01.log";
    private const string Burst = "Journal.2025-12-16T115443.01.log";

    private static IReadOnlyList<JournalEvent> Fixture(string name)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "d47.slnx")))
        {
            directory = directory.Parent;
        }

        Assert.NotNull(directory);

        return [.. File.ReadAllLines(Path.Combine(directory.FullName, "tests", "fixtures", "scenes", name))
            .Where(line => line.Length > 0)
            .Select(line =>
            {
                Assert.True(JournalEvent.TryParse(line, NullLogger.Instance, out var parsed));
                return parsed!;
            })];
    }

    private static JournalEvent Event(string json)
    {
        Assert.True(JournalEvent.TryParse(json, NullLogger.Instance, out var parsed));
        return parsed!;
    }

    private static List<JournalEvent> Congress(int count) =>
        [.. Fixture(Burst).Where(e => e.String("Faction") == "Federal Congress").Take(count)];

    /// <summary>Ticks once a second across the events and two minutes past them, applying each on its tick.</summary>
    private static List<SceneBeat> Replay(
        IReadOnlyList<JournalEvent> events,
        IReadOnlyList<JournalEvent>? primed = null,
        Func<bool>? enabled = null,
        string? scenario = "Winning the trust of the Federal Congress.")
    {
        var state = new CommanderGameState(new CommanderIdentity("F1", "Fixture"));
        var callout = new SceneCallout(new SceneTracker())
        {
            Enabled = enabled ?? (() => true),
            Scenario = () => scenario,
        };

        var heard = new List<SceneBeat>();
        var start = events[0].Timestamp;
        var end = events[^1].Timestamp.AddMinutes(2);

        foreach (var journalEvent in primed ?? [])
        {
            state.Apply(journalEvent);
        }

        Tick(new CalloutContext(start.AddSeconds(-1), true, state, GameStatus.Unknown, NavRoute.None, primed ?? []));

        var next = 0;

        for (var now = start; now <= end; now = now.AddSeconds(1))
        {
            var due = new List<JournalEvent>();

            while (next < events.Count && events[next].Timestamp <= now)
            {
                state.Apply(events[next]);
                due.Add(events[next++]);
            }

            Tick(new CalloutContext(now, false, state, GameStatus.Unknown, NavRoute.None, due));
        }

        return heard;

        void Tick(CalloutContext context)
        {
            foreach (var marker in callout.Examine(context))
            {
                Assert.Equal(SceneCallout.Key, marker.Key);
                Assert.Equal(string.Empty, marker.Text);
                heard.Add(marker.Scene!);
            }
        }
    }

    [Trait("Category", "Integration")]
    [Fact]
    public void ThreeTakenWithinAMinuteAreOneBeatNamingAllThree()
    {
        var beat = Assert.Single(Replay(Congress(3)));

        Assert.Equal(SceneBeatKind.MissionTaken, beat.Kind);
        Assert.Equal(ScenePlace.Mission, beat.Place);
        Assert.Equal(3, beat.Missions!.Count);
        Assert.All(beat.Missions, mission => Assert.Equal("Federal Congress", mission.Faction));
        Assert.Equal(["Sol Workers' Party", "Earth Defense Fleet", "Lung Dynamic Exchange"], beat.Missions.Select(m => m.TargetFaction));
        Assert.Equal(0, beat.MoreMissions);
    }

    [Trait("Category", "Integration")]
    [Fact]
    public void FiveTakenNameThreeAndCountTwoMore()
    {
        var beat = Assert.Single(Replay(Congress(5)));

        Assert.Equal(3, beat.Missions!.Count);
        Assert.Equal(2, beat.MoreMissions);
    }

    [Trait("Category", "Integration")]
    [Fact]
    public void ACompletedMissionIsDoneWithItsFactionAndName()
    {
        var beats = Replay(Fixture(Strike));

        Assert.Equal([SceneBeatKind.MissionTaken, SceneBeatKind.MissionDone], beats.Select(beat => beat.Kind));

        var done = Assert.Single(beats[1].Missions!);

        Assert.Equal("Federation Unite!", done.Faction);
        Assert.Equal("Federal Navy Strike Contract Authorised", done.LocalisedName);
        Assert.Equal("Screaming Bishops of The Kaha", done.TargetFaction);
    }

    [Trait("Category", "Integration")]
    [Fact]
    public void AnAbandonedMissionOnTheBoardIsLostWithItsFaction()
    {
        var events = Fixture(Burst);
        var abandoned = events[^1];

        // Accepted before the session, so only the board knows its faction.
        var beat = Assert.Single(Replay([abandoned], primed: [.. events.Take(events.Count - 1)]));

        Assert.Equal(SceneBeatKind.MissionLost, beat.Kind);

        var lost = Assert.Single(beat.Missions!);

        Assert.Equal("Sol Workers' Party", lost.Faction);
        Assert.Equal("Assassinate Known Pirate: James Matthew", lost.LocalisedName);
    }

    [Trait("Category", "Integration")]
    [Fact]
    public void AnAbandonedMissionNotOnTheBoardIsNotHeard() => Assert.Empty(Replay([Fixture(Burst)[^1]]));

    [Trait("Category", "Integration")]
    [Fact]
    public void WithNoScenarioNothingIsEmitted() => Assert.Empty(Replay(Fixture(Strike), scenario: " "));

    [Trait("Category", "Integration")]
    [Fact]
    public void WithScenesOrPersonalityOffNothingIsEmitted() =>
        Assert.Empty(Replay(Fixture(Strike), enabled: () => false));

    [Trait("Category", "Integration")]
    [Fact]
    public void NothingIsEmittedForWhatWasPrimed()
    {
        var events = Fixture(Strike);

        var music = Event("""{ "timestamp":"2025-07-12T01:46:00Z", "event":"Music", "MusicTrack":"Exploration" }""");

        Assert.Empty(Replay([music], primed: events));
    }

    [Trait("Category", "Integration")]
    [Fact]
    public void TheBriefNamesTheMissionsAndAsksForNothingWhenTheyDoNotBearOnTheScenario()
    {
        var beat = Assert.Single(Replay(Congress(5)));

        var brief = NpcChatter.SceneInstruction(beat, "Winning the trust of the Federal Congress.");

        Assert.Contains("People of Federal Congress", brief, StringComparison.Ordinal);
        Assert.Contains(
            "Expansion Data Couriering for Federal Congress, against Sol Workers' Party, to Miller Depot, Barnard's Star",
            brief,
            StringComparison.Ordinal);
        Assert.Contains("; and 2 more.", brief, StringComparison.Ordinal);
        Assert.Contains("Winning the trust of the Federal Congress.", brief, StringComparison.Ordinal);
        Assert.Contains(
            "If these missions do not bear on the scenario, reply with nothing at all", brief, StringComparison.Ordinal);
    }

    [Trait("Category", "Integration")]
    [Fact]
    public void AMissionBeatIsStillHappeningWithNoSceneOpen()
    {
        var beat = Assert.Single(Replay(Congress(1)));

        Assert.True(SceneCallout.IsStillHappening(beat, SceneSnapshot.None));
    }

    [Fact]
    public void AnExchangeWithNoLinesSpeaksNothing()
    {
        Assert.Empty(NpcChatter.Parse(null, NpcChatterKind.Scene));
        Assert.Empty(NpcChatter.Parse(string.Empty, NpcChatterKind.Scene));
    }
}
