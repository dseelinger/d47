using D47.Core.Callouts;
using D47.Core.Journal;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Callouts;

/// <summary>While a ship scene is heard, a kill is answered by the scene and not by the timed combat exchange.</summary>
[Trait("Category", "Integration")]
public class AShipSceneTakesTheKillFromTheCombatChatterTests
{
    private static readonly DateTimeOffset T0 = new(2026, 9, 29, 14, 40, 0, TimeSpan.Zero);

    private static JournalEvent Event(string json)
    {
        Assert.True(JournalEvent.TryParse(json, NullLogger.Instance, out var parsed));
        return parsed!;
    }

    private static readonly JournalEvent Exit =
        Event("""{"timestamp":"2026-09-29T14:40:00Z","event":"SupercruiseExit","StarSystem":"LTT 7786"}""");

    private static readonly JournalEvent Attack =
        Event("""{"timestamp":"2026-09-29T14:40:05Z","event":"UnderAttack","Target":"You"}""");

    private static readonly JournalEvent Kill = Event(
        """{ "timestamp":"2026-09-29T14:40:10Z", "event":"Bounty", "PilotName":"$npc_name_decorate:#name=Gyorgy Ligeti;", "PilotName_Localised":"Gyorgy Ligeti", "Target":"empire_eagle", "Target_Localised":"Imperial Eagle", "TotalReward":117480, "VictimFaction":"LTT 7786 Silver Partnership" }""");

    /// <summary>Folds an exit, an attack at 5 seconds and a kill at 10, ticking both callouts every second for two minutes.</summary>
    private static (List<string> Keys, List<SceneBeat> Beats) Replay(string? scenario)
    {
        var fight = new NearbyFight();
        var tracker = new SceneTracker();
        var scene = new SceneCallout(tracker) { Scenario = () => scenario };
        var chatter = new NpcChatterCallout(fight)
        {
            Interval = TimeSpan.FromMinutes(5),
            Longest = TimeSpan.FromMinutes(5),
            SceneHoldsTheFight = () => scene.HoldsTheFight,
        };

        var keys = new List<string>();
        var beats = new List<SceneBeat>();
        var status = GameStatus.Unknown with { Flags = StatusFlags.InMainShip };

        for (var second = 0; second <= 120; second++)
        {
            var now = T0.AddSeconds(second);
            var due = second switch { 0 => Exit, 5 => Attack, 10 => Kill, _ => null };

            if (due is not null)
            {
                fight.Fold(due, now, priming: false);
                tracker.Fold(due);
            }

            var context = new CalloutContext(now, false, null, status, NavRoute.None, due is null ? [] : [due]);

            foreach (var marker in chatter.Examine(context).Concat(scene.Examine(context)))
            {
                keys.Add(marker.Key);

                if (marker.Scene is { } beat)
                {
                    beats.Add(beat);
                }
            }
        }

        return (keys, beats);
    }

    [Fact]
    public void WithAScenarioTheKillIsAShipDownAndNoCombatExchange()
    {
        var (keys, beats) = Replay("Holding the line for LTT 7786 Labour.");

        Assert.DoesNotContain("npc.chatter.combat", keys);
        Assert.Contains(beats, beat => beat.Kind == SceneBeatKind.ShipDown && beat.Victim == "Gyorgy Ligeti");
    }

    [Fact]
    public void WithNoScenarioTheKillStillBringsTheCombatExchange()
    {
        var (keys, beats) = Replay(null);

        Assert.Contains("npc.chatter.combat", keys);
        Assert.Empty(beats);
    }
}
