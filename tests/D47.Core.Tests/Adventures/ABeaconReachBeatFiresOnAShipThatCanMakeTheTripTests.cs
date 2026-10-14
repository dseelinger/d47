using D47.Core.Adventures;
using Xunit;
using static D47.Core.Tests.Adventures.AdventureFixtures;

namespace D47.Core.Tests.Adventures;

/// <summary>A reach objective is met by a Loadout with the jump range and a fuel scoop, or by buying a fleet carrier.</summary>
public sealed class ABeaconReachBeatFiresOnAShipThatCanMakeTheTripTests
{
    private static readonly AdventureTrigger Trigger = new() { Kind = TriggerKind.Reach, System = "Col 359 Sector XX-X c4-12", JumpRange = 30 };

    private static bool Fires(string json) => AdventureFold.Matches(Trigger, Event(json));

    private static string Loadout(string range, bool scoop) =>
        $$"""{ "timestamp":"2026-10-14T10:00:00Z", "event":"Loadout", "Ship":"cobra_mk_iii", "ShipID":3{{range}}, "Modules":[{ "Slot":"FrameShiftDrive", "Item":"int_hyperdrive_size4_class5" }{{(scoop ? ", { \"Slot\":\"Slot02\", \"Item\":\"int_fuelscoop_size4_class5\" }" : string.Empty)}}] }""";

    [Fact]
    public void ALoadoutBelowTheRangeDoesNotFireItEvenWithAScoop() =>
        Assert.False(Fires(Loadout(", \"MaxJumpRange\":29.9", scoop: true)));

    [Fact]
    public void ALoadoutAtTheRangeWithNoScoopDoesNotFire() =>
        Assert.False(Fires(Loadout(", \"MaxJumpRange\":30.0", scoop: false)));

    [Fact]
    public void ALoadoutAtTheRangeWithAScoopFires() =>
        Assert.True(Fires(Loadout(", \"MaxJumpRange\":30.0", scoop: true)));

    [Fact]
    public void ALoadoutWithNoRangeFieldDoesNotFire() =>
        Assert.False(Fires(Loadout(string.Empty, scoop: true)));

    [Fact]
    public void BuyingAFleetCarrierFires() =>
        Assert.True(Fires("""{ "timestamp":"2026-10-14T10:00:00Z", "event":"CarrierBuy", "CarrierType":"FleetCarrier" }"""));

    [Fact]
    public void ABeatWithNoRangeIsNotResolved() =>
        Assert.False(AdventureFold.Matches(Trigger with { JumpRange = null }, Event(Loadout(", \"MaxJumpRange\":50", scoop: true))));

    [Fact]
    public void TheBeatSaysWhereTheBeaconIs() =>
        Assert.Equal("Next: fit out a ship that can reach the beacon in Col 359 Sector XX-X c4-12.", Trigger.HandOff());
}
