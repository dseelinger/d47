using D47.Core.Callouts;
using D47.Core.Journal;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Callouts;

/// <summary><see cref="RebuyCallout"/> (#484).</summary>
public class TheShipSaysWhenTheBalanceCannotCoverTheRebuyTests
{
    private static readonly DateTimeOffset Start = DateTimeOffset.Parse("3311-01-01T00:00:00Z");

    private static JournalEvent Event(string json)
    {
        Assert.True(JournalEvent.TryParse(json, NullLogger.Instance, out var parsed));
        return parsed!;
    }

    private static string Loadout(int shipId, long rebuy) =>
        $$"""{"timestamp":"3311-01-01T00:00:01Z","event":"Loadout","Ship":"cobramkiii","ShipID":{{shipId}},"Rebuy":{{rebuy}}}""";

    private static CommanderGameState StateFrom(params string[] lines)
    {
        var store = new GameStateStore();
        store.Apply(Event("""{"timestamp":"3311-01-01T00:00:00Z","event":"Commander","FID":"F1","Name":"Jameson"}"""));

        foreach (var line in lines)
        {
            store.Apply(Event(line));
        }

        return store.Active!;
    }

    private static GameStatus Status(long? balance, StatusFlags2 flags2 = StatusFlags2.None) => new()
    {
        Flags = StatusFlags.InMainShip,
        Flags2 = (uint)flags2,
        Balance = balance,
        ReadAt = Start,
    };

    private static CalloutContext Context(CommanderGameState state, GameStatus status, bool priming = false) =>
        new(Start, priming, state, status, NavRoute.None, []);

    [Fact]
    public void ABalanceBelowTheRebuyIsSaidWithBothFiguresBanded()
    {
        var state = StateFrom(Loadout(5, 3_200_000));

        var announced = Assert.Single(new RebuyCallout().Examine(Context(state, Status(1_100_000))));

        Assert.Equal("rebuy.short", announced.Key);
        Assert.Equal("Rebuy on this ship is 3.2 million credits. You have 1.1 million.", announced.Text);
        Assert.True(announced.Cooldown >= TimeSpan.FromMinutes(1));
    }

    [Fact]
    public void ABalanceThatCoversTheRebuyIsSilent()
    {
        var callout = new RebuyCallout();

        Assert.Empty(callout.Examine(Context(StateFrom(Loadout(5, 3_200_000)), Status(3_200_000))));

        // A Loadout that moved the rebuy, still covered.
        Assert.Empty(callout.Examine(Context(StateFrom(Loadout(5, 3_000_000)), Status(3_200_000))));
    }

    [Fact]
    public void ANullBalanceIsSilent() =>
        Assert.Empty(new RebuyCallout().Examine(Context(StateFrom(Loadout(5, 3_200_000)), Status(null))));

    [Fact]
    public void AnUnknownStatusIsSilent() =>
        Assert.Empty(new RebuyCallout().Examine(Context(StateFrom(Loadout(5, 3_200_000)), GameStatus.Unknown)));

    [Fact]
    public void AnUnknownShipIsSilent()
    {
        var state = StateFrom();

        Assert.False(state.Ship.IsKnown);
        Assert.Empty(new RebuyCallout().Examine(Context(state, Status(1))));
    }

    [Fact]
    public void ARebuyOfZeroIsSilent() =>
        Assert.Empty(new RebuyCallout().Examine(Context(StateFrom(Loadout(5, 0)), Status(0))));

    [Fact]
    public void NothingIsSaidWhilePrimingAndTheFirstTickAfterSaysIt()
    {
        var callout = new RebuyCallout();
        var state = StateFrom(Loadout(5, 3_200_000));

        Assert.Empty(callout.Examine(Context(state, Status(1_100_000), priming: true)));
        Assert.Single(callout.Examine(Context(state, Status(1_100_000))));
    }

    [Fact]
    public void AConditionThatHoldsIsSaidOnce()
    {
        var callout = new RebuyCallout();
        var state = StateFrom(Loadout(5, 3_200_000));

        var spoken = Enumerable.Range(0, 50)
            .SelectMany(_ => callout.Examine(Context(state, Status(1_100_000))))
            .ToList();

        Assert.Single(spoken);
    }

    [Fact]
    public void ARebuyThatMovesWhileShortIsNotSaidAgain()
    {
        var callout = new RebuyCallout();

        Assert.Single(callout.Examine(Context(StateFrom(Loadout(5, 3_200_000)), Status(1_100_000))));
        Assert.Empty(callout.Examine(Context(StateFrom(Loadout(5, 4_000_000)), Status(1_100_000))));
    }

    [Fact]
    public void ClearingTheConditionAndEnteringItAgainSaysItAgain()
    {
        var callout = new RebuyCallout();
        var state = StateFrom(Loadout(5, 3_200_000));

        Assert.Single(callout.Examine(Context(state, Status(1_100_000))));
        Assert.Empty(callout.Examine(Context(state, Status(5_000_000))));
        Assert.Single(callout.Examine(Context(state, Status(1_100_000))));
    }

    [Fact]
    public void AShipChangeWhileShortSaysItAgain()
    {
        var callout = new RebuyCallout();

        Assert.Single(callout.Examine(Context(StateFrom(Loadout(5, 3_200_000)), Status(1_100_000))));
        Assert.Single(callout.Examine(Context(StateFrom(Loadout(6, 2_000_000)), Status(1_100_000))));
    }

    [Theory]
    [InlineData(StatusFlags2.InMulticrew)]
    [InlineData(StatusFlags2.InTaxi)]
    public void SomebodyElsesShipIsSilent(StatusFlags2 flag) =>
        Assert.Empty(new RebuyCallout().Examine(Context(StateFrom(Loadout(5, 3_200_000)), Status(1_100_000, flag))));
}
