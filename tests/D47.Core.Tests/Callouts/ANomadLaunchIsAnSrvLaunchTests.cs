using D47.Core.Callouts;
using D47.Core.Journal;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Callouts;

/// <summary>A player-controlled LaunchVessel, the Nomad leaving a Mk II Vessel Hangar, is read as LaunchSRV.</summary>
public class ANomadLaunchIsAnSrvLaunchTests
{
    private const string Exit =
        """{"timestamp":"2026-10-26T14:40:00Z","event":"SupercruiseExit","StarSystem":"LTT 7786","Body":"LTT 7786 1"}""";

    private const string Attack = """{"timestamp":"2026-10-26T14:42:05Z","event":"UnderAttack","Target":"You"}""";

    private static string Launch(bool playerControlled) =>
        $$"""{"timestamp":"2026-10-26T14:41:00Z","event":"LaunchVessel","VesselType":"lander01","VesselType_Localised":"Nomad","PlayerControlled":{{(playerControlled ? "true" : "false")}}}""";

    private static JournalEvent Event(string json)
    {
        Assert.True(JournalEvent.TryParse(json, NullLogger.Instance, out var parsed));
        return parsed!;
    }

    private static SceneSnapshot Folded(params string[] lines)
    {
        var tracker = new SceneTracker();

        foreach (var line in lines)
        {
            tracker.Fold(Event(line));
        }

        return tracker.Snapshot;
    }

    [Fact]
    public void ANomadLaunchLeavesNormalSpaceThroughDockSrvAndBack()
    {
        Assert.Equal(SceneSnapshot.None, Folded(Exit, Launch(true), Attack));

        var docked = Folded(
            Exit,
            Launch(true),
            """{"timestamp":"2026-10-26T14:41:10Z","event":"Disembark","SRV":true,"OnPlanet":true}""",
            """{"timestamp":"2026-10-26T14:41:20Z","event":"Embark","SRV":false}""",
            """{"timestamp":"2026-10-26T14:41:30Z","event":"DockSRV","SRVType":"lander01"}""",
            Attack);

        Assert.True(docked.InShip);
    }

    [Fact]
    public void ALaunchVesselNobodyFliesChangesNoSceneState()
    {
        Assert.True(Folded(Exit, Launch(false), Attack).InShip);
    }
}
