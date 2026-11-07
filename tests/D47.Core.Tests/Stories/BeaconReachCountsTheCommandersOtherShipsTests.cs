using D47.Core.Adventures;
using D47.Core.Journal;
using D47.Core.Stories;
using D47.Core.Tests.Adventures;
using D47.Core.Tests.Conversation;
using Xunit;
using static D47.Core.Tests.Stories.StoryFixtures;

namespace D47.Core.Tests.Stories;

/// <summary>The beacon is in reach when a stored ship with a remembered loadout can make the trip, and the chapter writer is told which.</summary>
public sealed class BeaconReachCountsTheCommandersOtherShipsTests
{
    private static ShipLoadout Stored(bool scoop, bool weapon, string cargo = ", \"CargoCapacity\":4")
    {
        var modules =
            """[{"Slot":"FrameShiftDrive","Item":"int_hyperdrive_size2_class5","On":true}"""
            + (scoop ? """,{"Slot":"Slot01_Size2","Item":"int_fuelscoop_size2_class5","On":true}""" : string.Empty)
            + (weapon ? """,{"Slot":"SmallHardpoint1","Item":"hpt_pulselaser_fixed_small","On":true}""" : string.Empty)
            + "]";

        return new ShipLoadout().Apply(AdventureFixtures.Event(
            $$"""{ "timestamp":"{{AdventureFixtures.Stamp(Now)}}", "event":"Loadout", "Ship":"python", "ShipID":2, "MaxJumpRange":20{{cargo}}, "Modules":{{modules}} }"""));
    }

    private static FleetRegistry Fleet(bool inTransit = false) => new()
    {
        TakenAt = Now,
        Ships = [new StoredShip(2, "python", "Blue Jay", "Sol") { StationName = "Galileo", InTransit = inTransit }],
    };

    private static ShipLoadouts Remembered(ShipLoadout? ship) =>
        ship is null ? ShipLoadouts.Empty : ShipLoadouts.Empty.Remember(ship, Now);

    private static BeaconReach Reach(FleetRegistry fleet, ShipLoadouts loadouts) =>
        BeaconReach.Of(BeyondTheBeacon(686), Flying(7.6, scoop: false, hardpoint: null).Ship, carrier: false, fleet, loadouts);

    [Fact]
    public void AStoredShipThatCanMakeTheTripPutsTheBeaconInReach()
    {
        var reach = Reach(Fleet(), Remembered(Stored(scoop: true, weapon: true)));

        Assert.True(reach.InReach);
        Assert.Null(reach.Why);
        Assert.Contains("Blue Jay", reach.SwapTo);
        Assert.Contains("Galileo in Sol", reach.SwapTo);
    }

    [Fact]
    public void AStoredShipWithNoRememberedLoadoutDoesNotCount()
    {
        var reach = Reach(Fleet(), Remembered(null));

        Assert.False(reach.InReach);
        Assert.Null(reach.SwapTo);
        Assert.Equal(BeaconReach.Of(BeyondTheBeacon(686), Flying(7.6, scoop: false, hardpoint: null).Ship, carrier: false).Why, reach.Why);
    }

    [Fact]
    public void AStoredShipInTransitDoesNotCount()
    {
        var reach = Reach(Fleet(inTransit: true), Remembered(Stored(scoop: true, weapon: true)));

        Assert.False(reach.InReach);
        Assert.Null(reach.SwapTo);
    }

    [Fact]
    public void AStoredShipThatCannotMakeTheTripDoesNotCount() =>
        Assert.False(Reach(Fleet(), Remembered(Stored(scoop: true, weapon: false))).InReach);

    [Fact]
    public void ACommanderWhoseOnlyCapableShipIsTheOneFlownGetsTheSamePrompt()
    {
        var flown = Flying(35, scoop: true).Ship;
        var reach = BeaconReach.Of(BeyondTheBeacon(686), flown, carrier: false, Fleet(), Remembered(Stored(scoop: false, weapon: false)));

        Assert.Equal(BeaconReach.Of(BeyondTheBeacon(686), flown, carrier: false), reach);
        Assert.Null(reach.SwapTo);
    }

    [Trait("Category", "Integration")]
    [Fact]
    public async Task TheDirectorTellsTheChapterWriterWhichStoredShipToSwapInto()
    {
        using var fixtures = new StoryFixtures(new RoundScriptedLlmProvider(
            RoundScriptedLlmProvider.Saying(Lines())))
        {
            Here = BeyondTheBeacon(686),
        };

        var game = Flying(7.6, scoop: false, hardpoint: null);
        game.Fleet = Fleet();
        game.Loadouts = Remembered(Stored(scoop: true, weapon: true));
        fixtures.Director.Game = () => game;

        Assert.Null(await fixtures.Director.PickAsync("F1", Id, Now, CancellationToken.None));

        var beacon = Assert.Single(fixtures.Asks).Story!.Beacon!;

        Assert.Equal(BeaconAddress, beacon.SystemAddress);
        Assert.Contains("Blue Jay", beacon.SwapTo);
    }

    [Fact]
    public void AStoredShipWithNoCargoRackDoesNotCount()
    {
        var reach = Reach(Fleet(), Remembered(Stored(scoop: true, weapon: true, cargo: ", \"CargoCapacity\":0")));

        Assert.False(reach.InReach);
        Assert.Null(reach.SwapTo);
    }

    [Fact]
    public void AStoredShipWithNoReportedCargoCapacityDoesNotCount()
    {
        var reach = Reach(Fleet(), Remembered(Stored(scoop: true, weapon: true, cargo: string.Empty)));

        Assert.False(reach.InReach);
        Assert.Null(reach.SwapTo);
    }
}
