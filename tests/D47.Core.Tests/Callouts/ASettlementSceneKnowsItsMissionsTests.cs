using D47.Core.Callouts;
using D47.Core.Journal;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Callouts;

/// <summary>
/// A settlement scene's beat carries the live missions whose destination is the settlement or whose target is
/// its faction, and its brief names them.
/// </summary>
public class ASettlementSceneKnowsItsMissionsTests
{
    private const string Scenario = "Recovering stolen data from a pirate den.";

    private static JournalEvent Event(string json)
    {
        Assert.True(JournalEvent.TryParse(json, NullLogger.Instance, out var parsed));
        return parsed!;
    }

    private static IReadOnlyList<JournalEvent> Raid()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "d47.slnx")))
        {
            directory = directory.Parent;
        }

        Assert.NotNull(directory);

        return [.. File.ReadAllLines(Path.Combine(directory.FullName, "tests", "fixtures", "scenes", "Journal.2026-09-30T004823.01.log"))
            .Where(line => line.Length > 0)
            .Select(line =>
            {
                Assert.True(JournalEvent.TryParse(line, NullLogger.Instance, out var parsed));
                return parsed!;
            })];
    }

    private static SceneBeat Arrival(string accepted)
    {
        var state = new CommanderGameState(new CommanderIdentity("F1", "Fixture"));
        state.Apply(Event(accepted));

        var tracker = new SceneTracker();
        var callout = new SceneCallout(tracker) { Scenario = () => Scenario };

        callout.Examine(new CalloutContext(DateTimeOffset.MinValue, true, state, GameStatus.Unknown, NavRoute.None, [])).ToList();

        foreach (var journalEvent in Raid())
        {
            tracker.Fold(journalEvent);

            var markers = callout.Examine(
                new CalloutContext(journalEvent.Timestamp, false, state, GameStatus.Unknown, NavRoute.None, [journalEvent])).ToList();

            if (markers.Count > 0)
            {
                return markers[0].Scene!;
            }
        }

        throw new InvalidOperationException("The raid made no beat.");
    }

    [Fact]
    public void AMissionToTheSettlementIsInTheSceneAndItsBrief()
    {
        var beat = Arrival(
            """{ "timestamp":"2026-09-30T00:40:00Z", "event":"MissionAccepted", "Faction":"LTT 7786 Blue Council", "Name":"Mission_OnFoot_Heist", "LocalisedName":"Recover the stolen data", "TargetFaction":"Pirates of LTT 7786", "DestinationSystem":"LTT 7786", "DestinationSettlement":"Archambeau's Serenity", "MissionID":42 }""");

        Assert.Equal(SceneBeatKind.Arrived, beat.Kind);
        Assert.Equal(42, Assert.Single(beat.Missions!).Id);

        var brief = NpcChatter.SceneInstruction(beat, Scenario);

        Assert.Contains(
            "Recover the stolen data for LTT 7786 Blue Council, against Pirates of LTT 7786, to Archambeau's Serenity, LTT 7786",
            brief,
            StringComparison.Ordinal);
    }

    [Fact]
    public void AMissionAgainstTheSettlementsFactionIsInTheScene()
    {
        var beat = Arrival(
            """{ "timestamp":"2026-09-30T00:40:00Z", "event":"MissionAccepted", "Faction":"LTT 7786 Blue Council", "Name":"Mission_Assassinate", "LocalisedName":"Kill their boss", "TargetFaction":"LTT 7786 Silver Partnership", "DestinationSystem":"LTT 7786", "MissionID":43 }""");

        Assert.Equal(43, Assert.Single(beat.Missions!).Id);
    }

    [Fact]
    public void AnUnrelatedMissionIsNotInTheScene()
    {
        var beat = Arrival(
            """{ "timestamp":"2026-09-30T00:40:00Z", "event":"MissionAccepted", "Faction":"Federal Congress", "Name":"Mission_Courier", "LocalisedName":"Deliver a report", "TargetFaction":"Earth Defense Fleet", "DestinationSystem":"Sol", "DestinationStation":"Abraham Lincoln", "MissionID":44 }""");

        Assert.Null(beat.Missions);
        Assert.DoesNotContain("missions concerning", NpcChatter.SceneInstruction(beat, Scenario), StringComparison.Ordinal);
    }
}
