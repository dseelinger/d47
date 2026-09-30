using D47.Core.Callouts;
using D47.Core.Journal;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Callouts;

/// <summary>A ship scene opens on an interdiction, a site or an attack in normal space, and hears kills and deaths in it.</summary>
public class AShipSceneOpensOnTheFightTests
{
    private const string Exit =
        """{"timestamp":"2026-09-29T14:40:00Z","event":"SupercruiseExit","StarSystem":"LTT 7786","Body":"LTT 7786 1"}""";

    private const string Attack = """{"timestamp":"2026-09-29T14:42:05Z","event":"UnderAttack","Target":"You"}""";

    private static JournalEvent Event(string json)
    {
        Assert.True(JournalEvent.TryParse(json, NullLogger.Instance, out var parsed));
        return parsed!;
    }

    private static SceneTracker Folded(params string[] lines)
    {
        var tracker = new SceneTracker();

        foreach (var line in lines)
        {
            tracker.Fold(Event(line));
        }

        return tracker;
    }

    [Fact]
    public void AnNpcInterdictionOpensASceneNamingTheInterdictorAndFaction()
    {
        var snapshot = Folded(
            """{ "timestamp":"2026-08-29T00:00:32Z", "event":"Interdicted", "Submitted":true, "Interdictor":"Ramona Starburst", "IsPlayer":false, "Faction":"Pirates of Kuwemaki" }""")
            .Snapshot;

        Assert.True(snapshot.InShip);

        var beat = snapshot.Last!;
        Assert.Equal(SceneBeatKind.Interdicted, beat.Kind);
        Assert.Equal("Ramona Starburst", beat.Interdictor);
        Assert.Equal("Pirates of Kuwemaki", beat.Faction);
        Assert.True(beat.Submitted);

        var brief = NpcChatter.SceneInstruction(beat, "Running guns for the resistance.");
        Assert.Contains("Pilots flying for Pirates of Kuwemaki", brief, StringComparison.Ordinal);
        Assert.Contains("Ramona Starburst has just pulled a ship", brief, StringComparison.Ordinal);
    }

    [Fact]
    public void APlayerInterdictionOpensNoScene()
    {
        var snapshot = Folded(
            """{ "timestamp":"2026-08-29T00:00:32Z", "event":"Interdicted", "Submitted":false, "Interdictor":"Jameson", "IsPlayer":true }""")
            .Snapshot;

        Assert.Equal(SceneSnapshot.None, snapshot);
    }

    [Fact]
    public void AShipBountyIsAShipDownWithThePilotsName()
    {
        var beat = Folded(
            Exit,
            Attack,
            """{ "timestamp":"2026-09-29T14:42:16Z", "event":"Bounty", "Rewards":[ { "Faction":"Marquis du SPOCS 399", "Reward":46332 }, { "Faction":"LTT 7786 Major Group", "Reward":71148 } ], "PilotName":"$npc_name_decorate:#name=Gyorgy Ligeti;", "PilotName_Localised":"Gyorgy Ligeti", "Target":"empire_eagle", "Target_Localised":"Imperial Eagle", "TotalReward":117480, "VictimFaction":"LTT 7786 Silver Partnership" }""")
            .Snapshot.Last!;

        Assert.Equal(SceneBeatKind.ShipDown, beat.Kind);
        Assert.Equal("Gyorgy Ligeti", beat.Victim);
        Assert.Equal("Imperial Eagle", beat.Ship);
        Assert.Equal("LTT 7786 Silver Partnership", beat.Faction);

        var brief = NpcChatter.SceneInstruction(beat with { Merged = 1 }, "Running guns for the resistance.");
        Assert.Contains("Gyorgy Ligeti's Imperial Eagle flying for LTT 7786 Silver Partnership has just been destroyed.", brief, StringComparison.Ordinal);
    }

    [Fact]
    public void AKillBondIsAShipDownWithNoPilotName()
    {
        var beat = Folded(
            Exit,
            Attack,
            """{ "timestamp":"2026-09-29T14:48:17Z", "event":"FactionKillBond", "Reward":38018, "AwardingFaction":"LTT 7786 Labour", "VictimFaction":"LTT 7786 Major Group" }""")
            .Snapshot.Last!;

        Assert.Equal(SceneBeatKind.ShipDown, beat.Kind);
        Assert.Null(beat.Victim);
        Assert.Equal(1, beat.Kills);
    }

    [Fact]
    public void AShipKillerMakesACommanderDown()
    {
        var tracker = Folded(
            Exit,
            Attack,
            """{ "timestamp":"2026-09-29T14:43:00Z", "event":"Died", "KillerName":"Martin Caspersson", "KillerShip":"empire_eagle", "KillerRank":"Master" }""");

        var beat = tracker.Snapshot.Last!;
        Assert.Equal(SceneBeatKind.CommanderDown, beat.Kind);
        Assert.Equal(ScenePlace.Ship, beat.Place);
        Assert.Equal("Martin Caspersson", beat.Killer);
        Assert.Equal("Imperial Eagle", beat.Ship);
        Assert.False(tracker.Snapshot.Open);
    }

    [Fact]
    public void ASuitKillerIsLeftToTheSceneOnFoot()
    {
        var tracker = Folded(
            Exit,
            Attack,
            """{ "timestamp":"2026-09-29T14:43:00Z", "event":"Died", "KillerName":"Stevie Preston", "KillerShip":"rangedsuitai_class3", "KillerRank":"Harmless" }""");

        Assert.Equal(SceneBeatKind.Engaged, tracker.Snapshot.Last!.Kind);
        Assert.False(tracker.Snapshot.Open);
    }

    [Fact]
    public void AnAttackInSupercruiseOrDockedOpensNoScene()
    {
        Assert.Equal(
            SceneSnapshot.None,
            Folded(
                """{"timestamp":"2026-09-29T14:40:00Z","event":"SupercruiseEntry","StarSystem":"LTT 7786"}""",
                Attack).Snapshot);

        Assert.Equal(
            SceneSnapshot.None,
            Folded(
                """{"timestamp":"2026-09-29T14:40:00Z","event":"Docked","StationName":"Parise Gateway"}""",
                Attack).Snapshot);
    }

    [Fact]
    public void ADropAtAStationOpensNoScene()
    {
        var snapshot = Folded(
            """{ "timestamp":"2026-09-29T15:01:08Z", "event":"SupercruiseDestinationDrop", "Type":"Parise Gateway", "Threat":0, "MarketID":3225929984 }""")
            .Snapshot;

        Assert.Equal(SceneSnapshot.None, snapshot);
    }

    [Fact]
    public void LeavingWithoutAFightIsNotHeard()
    {
        var tracker = Folded(
            """{ "timestamp":"2026-09-29T14:31:25Z", "event":"SupercruiseDestinationDrop", "Type":"$MULTIPLAYER_SCENARIO14_TITLE;", "Type_Localised":"Resource Extraction Site", "Threat":3 }""",
            """{"timestamp":"2026-09-29T14:33:00Z","event":"SupercruiseEntry","StarSystem":"LTT 7786"}""");

        Assert.Equal(SceneBeatKind.Site, tracker.Snapshot.Last!.Kind);
        Assert.False(tracker.Snapshot.Open);
    }
}
