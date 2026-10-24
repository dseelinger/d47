using D47.Core.Adventures;
using Xunit;
using static D47.Core.Tests.Adventures.AdventureFixtures;

namespace D47.Core.Tests.Adventures;

/// <summary>
/// A reach objective is met by a Loadout with the jump range, a fuel scoop and a laser or railgun, by arriving in the beacon's system or by buying
/// a fleet carrier; arriving and buying do not meet one written for a ship with no laser or railgun.
/// </summary>
public sealed class ABeaconReachBeatFiresOnAShipThatCanMakeTheTripTests
{
    private static readonly AdventureTrigger Trigger = new() { Kind = TriggerKind.Reach, System = "Col 359 Sector XX-X c4-12", JumpRange = 30, Unarmed = false };

    private static bool Fires(string json) => AdventureFold.Matches(Trigger, Event(json));

    private static string Loadout(string range, bool scoop, string? weapon = "hpt_pulselaser_fixed_small") =>
        $$"""{ "timestamp":"2026-10-14T10:00:00Z", "event":"Loadout", "Ship":"cobra_mk_iii", "ShipID":3{{range}}, "Modules":[{ "Slot":"FrameShiftDrive", "Item":"int_hyperdrive_size4_class5" }{{(scoop ? ", { \"Slot\":\"Slot02\", \"Item\":\"int_fuelscoop_size4_class5\" }" : string.Empty)}}{{(weapon is null ? string.Empty : $", {{ \"Slot\":\"SmallHardpoint1\", \"Item\":\"{weapon}\" }}")}}] }""";

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
    public void ArrivingInTheBeaconSystemInAShortRangeShipFires() =>
        Assert.True(Fires("""{ "timestamp":"2026-10-14T10:00:00Z", "event":"FSDJump", "StarSystem":"Col 359 Sector XX-X c4-12", "SystemAddress":1 }"""));

    [Fact]
    public void StartingTheGameInTheBeaconSystemFires() =>
        Assert.True(Fires("""{ "timestamp":"2026-10-14T10:00:00Z", "event":"Location", "StarSystem":"col 359 sector xx-x c4-12", "SystemAddress":1 }"""));

    [Fact]
    public void ArrivingInAnotherSystemDoesNotFire() =>
        Assert.False(Fires("""{ "timestamp":"2026-10-14T10:00:00Z", "event":"FSDJump", "StarSystem":"Col 359 Sector XX-X c4-13", "SystemAddress":2 }"""));

    [Fact]
    public void ABeatWithNoRangeIsNotResolved() =>
        Assert.False(AdventureFold.Matches(Trigger with { JumpRange = null }, Event(Loadout(", \"MaxJumpRange\":50", scoop: true))));

    [Fact]
    public void ALoadoutWithRangeAndAScoopButNoLaserOrRailgunDoesNotFire() =>
        Assert.False(Fires(Loadout(", \"MaxJumpRange\":30.0", scoop: true, weapon: null)));

    [Theory]
    [InlineData("hpt_multicannon_fixed_medium")]
    [InlineData("hpt_mininglaser_fixed_small")]
    public void AnotherWeaponDoesNotCountAsALaser(string weapon) =>
        Assert.False(Fires(Loadout(", \"MaxJumpRange\":30.0", scoop: true, weapon)));

    [Fact]
    public void ABeatWrittenForAShipWithNoWeaponFiresOnceOneIsFitted()
    {
        var unarmed = Trigger with { Unarmed = true };

        Assert.False(AdventureFold.Matches(unarmed, Event(Loadout(", \"MaxJumpRange\":30.0", scoop: true, weapon: null))));
        Assert.True(AdventureFold.Matches(unarmed, Event(Loadout(", \"MaxJumpRange\":30.0", scoop: true, "hpt_railgun_fixed_medium"))));
    }

    [Fact]
    public void ABeatWrittenForAShipWithNoWeaponIsNotMetByArrivingOrBuyingACarrier()
    {
        var unarmed = Trigger with { Unarmed = true };

        Assert.False(AdventureFold.Matches(unarmed, Event("""{ "timestamp":"2026-10-14T10:00:00Z", "event":"FSDJump", "StarSystem":"Col 359 Sector XX-X c4-12", "SystemAddress":1 }""")));
        Assert.False(AdventureFold.Matches(unarmed, Event("""{ "timestamp":"2026-10-14T10:00:00Z", "event":"CarrierBuy", "CarrierType":"FleetCarrier" }""")));
    }

    [Fact]
    public void ACarrierOwnersBeatNamesNoRangeAndIsMetByALaserWhateverTheRangeAndScoop()
    {
        var carrier = new AdventureTrigger { Kind = TriggerKind.Reach, System = "Col 359 Sector XX-X c4-12", Carrier = true, Unarmed = true };

        Assert.True(carrier.IsResolved);
        Assert.True(AdventureFold.Matches(carrier, Event(Loadout(", \"MaxJumpRange\":8.0", scoop: false))));
        Assert.False(AdventureFold.Matches(carrier, Event(Loadout(", \"MaxJumpRange\":50.0", scoop: true, weapon: null))));
        Assert.False(AdventureFold.Matches(carrier, Event("""{ "timestamp":"2026-10-14T10:00:00Z", "event":"FSDJump", "StarSystem":"Col 359 Sector XX-X c4-12", "SystemAddress":1 }""")));
    }

    [Fact]
    public void ABeatSavedBeforeTheWeaponWasAskedForBehavesAsItDid()
    {
        var saved = Trigger with { Unarmed = null };

        Assert.True(AdventureFold.Matches(saved, Event(Loadout(", \"MaxJumpRange\":30.0", scoop: true, weapon: null))));
        Assert.True(AdventureFold.Matches(saved, Event("""{ "timestamp":"2026-10-14T10:00:00Z", "event":"CarrierBuy", "CarrierType":"FleetCarrier" }""")));
        Assert.Equal(
            "Next: fit out a ship that can reach the beacon in Col 359 Sector XX-X c4-12: a jump range of at least 30.0 light years and a fuel scoop. Arriving there in any ship also counts.",
            saved.HandOff());
    }

    [Fact]
    public void TheBeatSaysTheWeaponTheShipNeeds() =>
        Assert.Equal(
            "Next: fit out a ship that can reach the beacon in Col 359 Sector XX-X c4-12: a jump range of at least 30.0 light years, a fuel scoop and a laser or railgun. Arriving there in any ship also counts.",
            Trigger.HandOff());

    [Fact]
    public void TheBeatForAShipWithNoWeaponDoesNotSayArrivingCounts() =>
        Assert.Equal(
            "Next: fit out a ship that can reach the beacon in Col 359 Sector XX-X c4-12: a jump range of at least 30.0 light years, a fuel scoop and a laser or railgun.",
            (Trigger with { Unarmed = true }).HandOff());

    [Fact]
    public void TheBeatForACarrierOwnerAsksForTheWeaponAlone() =>
        Assert.Equal(
            "Next: fit a laser or railgun to the ship you take to the beacon in Col 359 Sector XX-X c4-12. Its pylons are charged by weapon fire before it can be scanned.",
            (Trigger with { JumpRange = null, Carrier = true, Unarmed = true }).HandOff());
}
