using D47.Core.Adventures;
using D47.Core.Journal;
using D47.Core.Stories;
using D47.Core.Tests.Conversation;
using Xunit;
using static D47.Core.Tests.Stories.StoryFixtures;

namespace D47.Core.Tests.Stories;

/// <summary>
/// An act-one chapter ends at the beacon only when the ship the Commander is in can reach it: a fleet carrier, or
/// twenty jumps or fewer with a fuel scoop. Otherwise the chapter works toward a ship that can.
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
        Assert.Contains("jump range of 7.6 light years", reach.Why);
        Assert.Contains("no fuel scoop", reach.Why);
    }

    [Fact]
    public void AScoopAloneIsNotEnoughAtSidewinderRange()
    {
        var reach = BeaconReach.Of(BeyondTheBeacon(686), Flying(7.6, scoop: true).Ship, carrier: false);

        Assert.False(reach.InReach);
        Assert.DoesNotContain("fuel scoop", reach.Why);
    }

    [Fact]
    public void ThirtyFiveLightYearsAndAScoopReach686() =>
        Assert.True(BeaconReach.Of(BeyondTheBeacon(686), Flying(35, scoop: true).Ship, carrier: false).InReach);

    [Fact]
    public void FortySevenLightYearsAndAScoopReach584() =>
        Assert.True(BeaconReach.Of(BeyondTheBeacon(584), Flying(47.7, scoop: true).Ship, carrier: false).InReach);

    [Fact]
    public void ACarrierOwnerIsAlwaysInReach() =>
        Assert.True(BeaconReach.Of(BeyondTheBeacon(686), Flying(7.6, scoop: false).Ship, carrier: true).InReach);

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

    [Fact]
    public async Task ActOneRunsUntilTheShipCanMakeTheTrip()
    {
        using var fixtures = new StoryFixtures(new RoundScriptedLlmProvider(
            RoundScriptedLlmProvider.Saying(Spine),
            RoundScriptedLlmProvider.Saying(BeatsElsewhere),
            RoundScriptedLlmProvider.Saying(NextSpine),
            RoundScriptedLlmProvider.Saying(NextBeatsWithABeacon)))
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
        Assert.Contains("jump range of 7.6 light years", brief);
        Assert.DoesNotContain("its last beat is \"beacon\"", brief);
        Assert.DoesNotContain(Secret.Beats.BreakIntoTwo!, brief);
        Assert.Contains(Secret.Beats.Debate!, brief);

        fixtures.Finish("F1", fixtures.Stories.Current("F1")!.CurrentChapter!, Now);
        ship = Flying(35, scoop: true);

        Assert.Null(await fixtures.Director.WriteNextAsync("F1", Now.AddDays(1), CancellationToken.None));

        Assert.Equal(BeaconAddress, fixtures.Asks[^1].Story!.Beacon!.SystemAddress);
        Assert.Null(fixtures.Asks[^1].Story!.BeaconAway);

        var second = fixtures.Book.Store.Find("F1", fixtures.Stories.Current("F1")!.CurrentChapter!)!;

        Assert.Equal(TriggerKind.Beacon, second.Beats[^1].Trigger.Kind);
        Assert.Contains(Secret.Beats.BreakIntoTwo!, fixtures.Provider.Requests[2].Prompt.History[0].Text);
    }
}
