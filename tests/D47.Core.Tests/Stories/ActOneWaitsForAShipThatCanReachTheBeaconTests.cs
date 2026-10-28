using D47.Core.Adventures;
using D47.Core.Journal;
using D47.Core.Stories;
using D47.Core.Tests.Conversation;
using Xunit;
using static D47.Core.Tests.Stories.StoryFixtures;

namespace D47.Core.Tests.Stories;

/// <summary>
/// An act-one chapter ends at the beacon only when the ship the Commander is in can reach it: a laser or railgun fitted, and a fleet carrier
/// or a fuel scoop, whatever its jump range. Otherwise the chapter works toward a ship that can; a short range only makes the trip easier.
/// </summary>
public sealed class ActOneWaitsForAShipThatCanReachTheBeaconTests
{
    [Fact]
    public void AStockSidewinderWithNoScoopIsOutOfReach()
    {
        var reach = BeaconReach.Of(BeyondTheBeacon(686), Flying(7.6, scoop: false).Ship, carrier: false);

        Assert.Equal(Beacon, reach.System);
        Assert.Equal(686, reach.LightYears, 0.5);
        Assert.False(reach.InReach);
        Assert.Equal("no fuel scoop is fitted", reach.Why);
    }

    [Fact]
    public void AScoopAndAWeaponAreEnoughAtSidewinderRange()
    {
        var reach = BeaconReach.Of(BeyondTheBeacon(686), Flying(7.6, scoop: true).Ship, carrier: false);

        Assert.True(reach.InReach);
        Assert.Null(reach.Why);
    }

    [Fact]
    public void ThirtyFiveLightYearsAndAScoopReach686() =>
        Assert.True(BeaconReach.Of(BeyondTheBeacon(686), Flying(35, scoop: true).Ship, carrier: false).InReach);

    [Fact]
    public void TwentyEightLightYearsAndAScoopReach686() =>
        Assert.True(BeaconReach.Of(BeyondTheBeacon(686), Flying(28, scoop: true).Ship, carrier: false).InReach);

    [Fact]
    public void JustUnderTwentyEightLightYearsStillReaches686AndSuggestsTwentyEight()
    {
        var reach = BeaconReach.Of(BeyondTheBeacon(686), Flying(27.9, scoop: true).Ship, carrier: false);

        Assert.True(reach.InReach);
        Assert.Null(reach.Why);
        Assert.Equal(28, reach.EasierWith);
    }

    [Fact]
    public void ARangeThatIsEnoughSuggestsNothing() =>
        Assert.Null(BeaconReach.Of(BeyondTheBeacon(686), Flying(35, scoop: true).Ship, carrier: false).EasierWith);

    [Fact]
    public void AMissingScoopNeverStatesTheShipsRange()
    {
        var reach = BeaconReach.Of(BeyondTheBeacon(686), Flying(7.6, scoop: false, hardpoint: null).Ship, carrier: false);

        Assert.DoesNotContain("jump range", reach.Why);
        Assert.DoesNotContain("light years", reach.Why);
    }

    [Fact]
    public void ANearerBeaconAsksForLessThanTwentyEight() =>
        Assert.Equal(20, BeaconReach.RangeNeeded(400), 3);

    [Fact]
    public void FortySevenLightYearsAndAScoopReach584() =>
        Assert.True(BeaconReach.Of(BeyondTheBeacon(584), Flying(47.7, scoop: true).Ship, carrier: false).InReach);

    [Fact]
    public void ACarrierOwnerWithALaserIsInReachWhateverTheRangeAndScoop() =>
        Assert.True(BeaconReach.Of(BeyondTheBeacon(686), Flying(7.6, scoop: false).Ship, carrier: true).InReach);

    [Fact]
    public void ACarrierOwnerWithNoLaserOrRailgunIsOutOfReachAndTheReasonIsOnlyTheWeapon()
    {
        var reach = BeaconReach.Of(BeyondTheBeacon(686), Flying(7.6, scoop: false, hardpoint: null).Ship, carrier: true);

        Assert.False(reach.InReach);
        Assert.Equal("no laser or railgun is fitted", reach.Why);
        Assert.True(reach.Unarmed);
        Assert.True(reach.Carrier);
    }

    [Fact]
    public void ARangeAndAScoopWithNoLaserOrRailgunIsOutOfReach()
    {
        var reach = BeaconReach.Of(BeyondTheBeacon(686), Flying(35, scoop: true, hardpoint: null).Ship, carrier: false);

        Assert.False(reach.InReach);
        Assert.Equal("no laser or railgun is fitted", reach.Why);
        Assert.True(reach.Unarmed);
        Assert.False(reach.Carrier);
    }

    [Fact]
    public void AMissingWeaponIsNamedBesideTheScoop()
    {
        var reach = BeaconReach.Of(BeyondTheBeacon(686), Flying(7.6, scoop: false, hardpoint: null).Ship, carrier: false);

        Assert.Equal("no fuel scoop is fitted and no laser or railgun is fitted", reach.Why);
    }

    [Theory]
    [InlineData("hpt_beamlaser_turret_large")]
    [InlineData("hpt_pulselaserburst_gimbal_medium")]
    [InlineData("hpt_railgun_fixed_medium")]
    public void ALaserOrRailgunInAnyMountIsInReach(string item) =>
        Assert.True(BeaconReach.Of(BeyondTheBeacon(686), Flying(35, scoop: true, hardpoint: item).Ship, carrier: false).InReach);

    [Theory]
    [InlineData("hpt_multicannon_fixed_medium")]
    [InlineData("hpt_mininglaser_fixed_small")]
    [InlineData("hpt_plasmaaccelerator_fixed_large")]
    [InlineData("hpt_guardian_gausscannon_fixed_medium")]
    public void AnyOtherWeaponIsOutOfReach(string item) =>
        Assert.False(BeaconReach.Of(BeyondTheBeacon(686), Flying(35, scoop: true, hardpoint: item).Ship, carrier: false).InReach);

    [Fact]
    public void ALoadoutThatListsNoModulesIsInReach()
    {
        var ship = new ShipLoadout().Apply(Tests.Adventures.AdventureFixtures.Event(
            """{ "timestamp":"2026-10-14T10:00:00Z", "event":"Loadout", "Ship":"sidewinder", "ShipID":1, "MaxJumpRange":35, "Modules":[] }"""));

        Assert.True(ship.IsKnown);
        Assert.True(BeaconReach.Of(BeyondTheBeacon(686), ship, carrier: false).InReach);
        Assert.True(BeaconReach.Of(BeyondTheBeacon(686), ship, carrier: true).InReach);
    }

    [Trait("Category", "Integration")]
    [Fact]
    public async Task TheDirectorReadsTheCarrierFromTheGameState()
    {
        using var fixtures = new StoryFixtures(new RoundScriptedLlmProvider(
            RoundScriptedLlmProvider.Saying(Spine), RoundScriptedLlmProvider.Saying(BeatsToTheBeacon)))
        {
            Here = BeyondTheBeacon(686),
        };

        fixtures.Director.Game = () => Flying(7.6, scoop: false, carrier: true);

        Assert.Null(await fixtures.Director.PickAsync("F1", Id, Now, CancellationToken.None));
        Assert.Equal(BeaconAddress, Assert.Single(fixtures.Asks).Story!.Beacon!.SystemAddress);
    }

    [Trait("Category", "Integration")]
    [Fact]
    public async Task ActOneRunsUntilTheShipHasAScoopAndAWeapon()
    {
        using var fixtures = new StoryFixtures(new RoundScriptedLlmProvider(
            RoundScriptedLlmProvider.Saying(Spine),
            RoundScriptedLlmProvider.Saying(BeatsElsewhere),
            RoundScriptedLlmProvider.Saying(NothingObjected),
            RoundScriptedLlmProvider.Saying(NextSpine),
            RoundScriptedLlmProvider.Saying(NextBeatsWithABeacon),
            RoundScriptedLlmProvider.Saying(NothingObjected)))
        {
            Here = BeyondTheBeacon(686),
        };

        var ship = Flying(7.6, scoop: false);
        fixtures.Director.Game = () => ship;

        Assert.Null(await fixtures.Director.PickAsync("F1", Id, Now, CancellationToken.None));

        var first = Assert.Single(fixtures.Asks);

        Assert.Null(first.Story!.Beacon);
        Assert.Equal(AdventureReach.Session, first.Reach);
        Assert.Equal(Beacon, first.Story.BeaconAway!.System);
        Assert.DoesNotContain(fixtures.Book.Store.For("F1").Single().Beats, beat => beat.Trigger.Kind == TriggerKind.Beacon);

        var brief = fixtures.Provider.Requests[0].Prompt.History[0].Text;

        Assert.Contains($"in {Beacon}, is 686 light years away and out of reach", brief);
        Assert.Contains("no fuel scoop is fitted", brief);
        Assert.DoesNotContain("jump range of", brief);
        Assert.DoesNotContain("its last beat is \"beacon\"", brief);
        Assert.DoesNotContain(Secret.Beats.BreakIntoTwo!, brief);
        Assert.Contains(Secret.Beats.Debate!, brief);

        fixtures.Finish("F1", fixtures.Stories.Current("F1")!.CurrentChapter!, Now);
        ship = Flying(7.6, scoop: true);

        Assert.Null(await fixtures.Director.WriteNextAsync("F1", Now.AddDays(1), CancellationToken.None));

        Assert.Equal(BeaconAddress, fixtures.Asks[^1].Story!.Beacon!.SystemAddress);
        Assert.Null(fixtures.Asks[^1].Story!.BeaconAway);

        var second = fixtures.Book.Store.Find("F1", fixtures.Stories.Current("F1")!.CurrentChapter!)!;

        Assert.Equal(TriggerKind.Beacon, second.Beats[^1].Trigger.Kind);
        Assert.Equal(28, second.Beats[^1].Trigger.JumpRange);
        Assert.Contains(Secret.Beats.BreakIntoTwo!, fixtures.Provider.Requests[3].Prompt.History[0].Text);
    }
}
