using D47.Core.Adventures;
using Xunit;
using static D47.Core.Tests.Adventures.AdventureFixtures;

namespace D47.Core.Tests.Adventures;

public class AnOnFootBondObjectiveCountsOnlyBondsEarnedOnFootTests
{
    private const string Bond = """{ "timestamp":"@", "event":"FactionKillBond", "Reward":2911, "AwardingFaction":"Nationals of Ossen", "VictimFaction":"Ossen Labour" }""";

    private const string Disembark = """{ "timestamp":"@", "event":"Disembark", "SRV":false, "Taxi":false, "Multicrew":false, "SystemAddress":1, "BodyID":4, "OnStation":false, "OnPlanet":true }""";

    private const string StationDisembark = """{ "timestamp":"@", "event":"Disembark", "SRV":false, "Taxi":false, "Multicrew":false, "SystemAddress":1, "BodyID":9, "OnStation":true, "OnPlanet":false }""";

    private const string Embark = """{ "timestamp":"@", "event":"Embark", "SRV":false, "Taxi":false, "Multicrew":false, "SystemAddress":1, "BodyID":4, "OnStation":false, "OnPlanet":true }""";

    private const string BoardDropship = """{ "timestamp":"@", "event":"Embark", "SRV":false, "Taxi":true, "Multicrew":false, "SystemAddress":1, "BodyID":9, "OnStation":true, "OnPlanet":false }""";

    private const string Deploy = """{ "timestamp":"@", "event":"DropshipDeploy", "SystemAddress":1, "Body":"Ossen 4", "BodyID":4, "OnStation":false, "OnPlanet":true }""";

    private const string LaunchSrv = """{ "timestamp":"@", "event":"LaunchSRV", "SRVType":"testbuggy", "Loadout":"starter", "ID":53, "PlayerControlled":true }""";

    private const string LeaveSrv = """{ "timestamp":"@", "event":"Disembark", "SRV":true, "Taxi":false, "Multicrew":false, "SystemAddress":1, "BodyID":4, "OnStation":false, "OnPlanet":true }""";

    private const string BoardSrv = """{ "timestamp":"@", "event":"Embark", "SRV":true, "Taxi":false, "Multicrew":false, "SystemAddress":1, "BodyID":4, "OnStation":false, "OnPlanet":true }""";

    private static readonly string LoadOnFoot = $$"""{ "timestamp":"@", "event":"Location", "Docked":false, "OnFoot":true, "StarSystem":"Ossen's Lantern", "SystemAddress":{{Lantern}} }""";

    private static readonly string LoadInShip = $$"""{ "timestamp":"@", "event":"Location", "Docked":false, "StarSystem":"Ossen's Lantern", "SystemAddress":{{Lantern}} }""";

    private static AdventureStanding Fold(int count, params string[] lines)
    {
        var adventure = LanternRoute(Accepted) with
        {
            Beats =
            [
                Beat("The Lantern", "setup", new AdventureTrigger { Kind = TriggerKind.Arrive, SystemAddress = Lantern, System = "Ossen's Lantern" }, "Go."),
                Beat("The Line", "catalyst", new AdventureTrigger { Kind = TriggerKind.OnFootBond, Count = count }, "Done."),
            ],
        };

        var standing = AdventureFold.Start(adventure);
        var world = AdventureWorld.Empty;
        var minute = 0;

        foreach (var line in lines.Prepend($$"""{ "timestamp":"@", "event":"FSDJump", "SystemAddress":{{Lantern}}, "StarSystem":"Ossen's Lantern" }"""))
        {
            var journalEvent = Event(line.Replace("@", Stamp(Accepted.AddMinutes(++minute)), StringComparison.Ordinal));
            world = world.Apply(journalEvent);
            standing = AdventureFold.Apply(standing, journalEvent, world);
        }

        return standing;
    }

    [Fact]
    public void ABondEarnedAfterADisembarkCounts() =>
        Assert.Equal(2, Fold(5, Disembark, Bond, Bond).Counted);

    [Fact]
    public void ABondEarnedInTheShipDoesNotCount() =>
        Assert.Equal(0, Fold(5, Bond, Bond).Counted);

    [Fact]
    public void ABondEarnedAfterEmbarkingAgainDoesNotCount() =>
        Assert.Equal(1, Fold(5, Disembark, Bond, Embark, Bond).Counted);

    [Fact]
    public void ABondEarnedAfterADropshipDeployCounts() =>
        Assert.True(Fold(2, StationDisembark, BoardDropship, Deploy, Bond, Bond).IsDone);

    [Fact]
    public void ABondEarnedOnTheDropshipBeforeItDeploysDoesNotCount() =>
        Assert.Equal(0, Fold(5, StationDisembark, BoardDropship, Bond).Counted);

    [Fact]
    public void ABondEarnedInTheSrvDoesNotCount() =>
        Assert.Equal(0, Fold(5, LaunchSrv, Bond, LeaveSrv, BoardSrv, Bond).Counted);

    [Fact]
    public void ALocationSaysWhetherTheCommanderLoadedOnFoot()
    {
        Assert.Equal(1, Fold(5, LoadOnFoot, Bond).Counted);
        Assert.Equal(0, Fold(5, Disembark, LoadInShip, Bond).Counted);
    }

    [Fact]
    public void AnOnFootBondObjectiveIsCountedAndSaysSo()
    {
        var trigger = new AdventureTrigger { Kind = TriggerKind.OnFootBond, Count = 8 };

        Assert.True(trigger.IsCounted);
        Assert.Equal("Kill bonds on foot: 0 of 8", trigger.Progress(0));
        Assert.Equal("earn 8 kill bonds on foot", trigger.Describe());
        Assert.Equal("onfootbond", RefusedActivities.Key(TriggerKind.OnFootBond, null));
    }
}
