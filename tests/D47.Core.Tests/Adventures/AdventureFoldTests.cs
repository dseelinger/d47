using D47.Core.Adventures;
using Xunit;
using static D47.Core.Tests.Adventures.AdventureFixtures;

namespace D47.Core.Tests.Adventures;

/// <summary>
/// Progress is derived, and an adventure counts forward only (list.md Phase 47).
/// <para>
/// The assertion is the list's own sentence: fires at each place in order, once, and at nothing
/// before the stamp.
/// </para>
/// </summary>
public class AdventureFoldTests
{
    private static AdventureStanding Run(Adventure adventure, IEnumerable<D47.Core.Journal.JournalEvent> events) =>
        events.Aggregate(AdventureFold.Start(adventure), AdventureFold.Apply);

    [Fact]
    public void TheWholeRouteFiresEveryBeatInOrder()
    {
        var standing = Run(LanternRoute(Accepted), WholeRoute(Accepted));

        Assert.True(standing.IsDone);
        Assert.Equal(5, standing.Fired.Count);
        Assert.Equal(Accepted.AddMinutes(5), standing.FinishedAt);
        Assert.Equal("finished", standing.Place());
    }

    [Fact]
    public void NothingBeforeTheStampCounts()
    {
        // The Commander flew the whole route last week. Adventures start when accepted and mine no
        // history, so the first beat is a beginning rather than an audit.
        var standing = Run(LanternRoute(Accepted), WholeRoute(Accepted.AddDays(-7)));

        Assert.Empty(standing.Fired);
        Assert.Equal("The Lantern", standing.Place());
    }

    [Fact]
    public void OnlyTheCurrentBeatCanMatch()
    {
        // Home first, then the Lantern. Beat 5's place reached early is not banked.
        var standing = Run(
            LanternRoute(Accepted),
            [Jump(Home, Accepted.AddMinutes(1)), Jump(Lantern, Accepted.AddMinutes(2))]);

        Assert.Single(standing.Fired);
        Assert.Equal("The Survey", standing.Place());
    }

    [Fact]
    public void RevisitingAnEarlierPlaceDoesNothing()
    {
        var standing = Run(
            LanternRoute(Accepted),
            [Jump(Lantern, Accepted.AddMinutes(1)), Jump(Lantern, Accepted.AddMinutes(2)), Jump(Lantern, Accepted.AddMinutes(3))]);

        Assert.Single(standing.Fired);
    }

    [Fact]
    public void AnUnbegunAdventureNeverMoves()
    {
        var standing = Run(LanternRoute(acceptedAt: null), WholeRoute(Accepted));

        Assert.Empty(standing.Fired);
        Assert.Equal("waiting for your yes", standing.Place());
    }

    [Fact]
    public void TheFoldStopsAtAbandonment()
    {
        var adventure = LanternRoute(Accepted) with { AbandonedAt = Accepted.AddMinutes(2).AddSeconds(30) };
        var standing = Run(adventure, WholeRoute(Accepted));

        Assert.Equal(2, standing.Fired.Count);
        Assert.Equal("abandoned at The Anchorage", standing.Place());
    }

    [Fact]
    public void ArrivalIsAnyOfTheThreeEventsThatPutYouInASystem()
    {
        var trigger = new AdventureTrigger { Kind = TriggerKind.Arrive, SystemAddress = Lantern };

        Assert.True(AdventureFold.Matches(trigger, Jump(Lantern, Accepted)));
        Assert.True(AdventureFold.Matches(trigger, Location(Lantern, Accepted)));
        Assert.True(AdventureFold.Matches(
            trigger,
            Event($$"""{ "timestamp":"{{Stamp(Accepted)}}", "event":"CarrierJump", "Docked":true, "StarSystem":"X", "SystemAddress":{{Lantern}} }""")));
        Assert.False(AdventureFold.Matches(trigger, Jump(Home, Accepted)));
    }

    [Fact]
    public void ABodyBeatNeedsBothTheSystemAndTheBody()
    {
        var trigger = new AdventureTrigger { Kind = TriggerKind.Land, SystemAddress = Veyl, BodyId = 9 };

        Assert.True(AdventureFold.Matches(trigger, Touchdown(Veyl, 9, Accepted)));
        Assert.False(AdventureFold.Matches(trigger, Touchdown(Veyl, 8, Accepted)));
        Assert.False(AdventureFold.Matches(trigger, Touchdown(Home, 9, Accepted)));
        Assert.False(AdventureFold.Matches(trigger, Scan(Veyl, 9, Accepted)));
    }

    [Fact]
    public void ARankBeatFiresOnReachingOrPassingTheRank()
    {
        var trigger = new AdventureTrigger { Kind = TriggerKind.Rank, Career = "Explore", Rank = 6 };

        Assert.True(AdventureFold.Matches(trigger, Promotion("Explore", 6, Accepted)));
        Assert.True(AdventureFold.Matches(trigger, Promotion("Explore", 7, Accepted)));
        Assert.False(AdventureFold.Matches(trigger, Promotion("Explore", 5, Accepted)));
        Assert.False(AdventureFold.Matches(trigger, Promotion("Combat", 6, Accepted)));
    }

    [Fact]
    public void AnUnresolvedTriggerNeverMatchesAnything()
    {
        var named = new AdventureTrigger { Kind = TriggerKind.Arrive, System = "Ossen's Lantern" };

        Assert.False(named.IsResolved);
        Assert.False(AdventureFold.Matches(named, Jump(Lantern, Accepted, "Ossen's Lantern")));
    }

    [Fact]
    public void NothingMovingReturnsTheSameInstance()
    {
        var start = AdventureFold.Start(LanternRoute(Accepted));

        Assert.Same(start, AdventureFold.Apply(start, Jump(Home, Accepted.AddMinutes(1))));
    }

    [Fact]
    public void TheTurnIsReachedWhenItsBeatFires()
    {
        var route = WholeRoute(Accepted);

        Assert.False(Run(LanternRoute(Accepted), route.Take(2)).TurnReached);
        Assert.True(Run(LanternRoute(Accepted), route.Take(3)).TurnReached);
    }
}
