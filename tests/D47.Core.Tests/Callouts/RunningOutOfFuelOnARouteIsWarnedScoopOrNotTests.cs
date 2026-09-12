using D47.Core.Callouts;
using D47.Core.Journal;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Callouts;

/// <summary>The fuel-reach warning, for a ship with a Fuel Scoop, one without, and one whose loadout is unread.</summary>
public class RunningOutOfFuelOnARouteIsWarnedScoopOrNotTests
{
    private static readonly DateTimeOffset Start = DateTimeOffset.Parse("3311-01-01T00:00:00Z");

    private const string Scoop = "int_fuelscoop_size6_class5";

    private const string CargoRack = "int_cargorack_size6_class1";

    /// <summary>Two jumps at 2 t each: 5 t in the tank is fuel for 2 more.</summary>
    private static readonly string[] TwoJumps =
    [
        Jump("Before", fuelUsed: 2),
        Jump("Here", fuelUsed: 2),
    ];

    private static string Jump(string system, double fuelUsed) =>
        $$"""{"timestamp":"3311-01-01T00:00:01Z","event":"FSDJump","StarSystem":"{{system}}","JumpDist":40,"FuelUsed":{{fuelUsed}}}""";

    private static string Loadout(string module, int shipId = 1) =>
        $$"""{"timestamp":"3311-01-01T00:00:00Z","event":"Loadout","Ship":"Anaconda","ShipID":{{shipId}},"MaxJumpRange":50,"FuelCapacity":{"Main":32},"Modules":[{"Slot":"Slot03_Size6","Item":"{{module}}","On":true,"Health":1.0}]}""";

    private static JournalEvent Event(string json)
    {
        Assert.True(JournalEvent.TryParse(json, NullLogger.Instance, out var parsed));
        return parsed!;
    }

    private static GameStateStore Store(string? module)
    {
        var store = new GameStateStore();
        store.Apply(Event("""{"timestamp":"3311-01-01T00:00:00Z","event":"Commander","FID":"F1","Name":"Jameson"}"""));

        if (module is not null)
        {
            store.Apply(Event(Loadout(module)));
        }

        store.Apply(Event(Jump("Here", fuelUsed: 2)));

        return store;
    }

    private static CommanderGameState Ship(string? module) => Store(module).Active!;

    private static NavRoute Route(params string[] classesAhead) => new()
    {
        Hops =
        [
            new RouteHop("Here", "K"),
            .. classesAhead.Select((starClass, index) => new RouteHop($"Hop {index + 1}", starClass)),
        ],
        ReadAt = Start,
    };

    private static IReadOnlyList<Announcement> Examine(
        FuelReachCallout callout,
        CommanderGameState state,
        NavRoute route,
        double fuel,
        IEnumerable<string>? events = null,
        int atSecond = 0) =>
        [
            .. callout.Examine(new CalloutContext(
                Start.AddSeconds(atSecond),
                IsPriming: false,
                state,
                new GameStatus { Flags = StatusFlags.InMainShip, FuelMain = fuel, ReadAt = Start },
                route,
                [.. (events ?? TwoJumps).Select(Event)])),
        ];

    [Fact]
    public void AShipWithNoScoopHearsThatItCannotFinishTheRouteWithNoWordOnScooping()
    {
        var warning = Assert.Single(
            Examine(new FuelReachCallout(), Ship(CargoRack), Route("K", "G", "T", "K"), fuel: 5));

        Assert.Equal(FuelReachCallout.Key, warning.Key);
        Assert.Equal(CalloutUrgency.Urgent, warning.Urgency);
        Assert.Contains("fuel for 2 jumps", warning.Text, StringComparison.Ordinal);
        Assert.Contains("4 jumps left", warning.Text, StringComparison.Ordinal);
        Assert.DoesNotContain("scoop", warning.Text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("class", warning.Text, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void AShipWithNoScoopThatCanFinishTheRouteHearsNothing()
    {
        Assert.Empty(Examine(new FuelReachCallout(), Ship(CargoRack), Route("T", "T", "T", "T"), fuel: 8));
    }

    [Fact]
    public void AScoopShipWithAScoopableStarInReachHearsNothing()
    {
        Assert.Empty(Examine(new FuelReachCallout(), Ship(Scoop), Route("T", "K", "T", "T"), fuel: 5));
    }

    [Fact]
    public void AScoopShipWhoseNearestScoopableStarIsOutOfReachIsTold()
    {
        var warning = Assert.Single(
            Examine(new FuelReachCallout(), Ship(Scoop), Route("T", "T", "K", "T"), fuel: 5));

        Assert.Contains("nearest scoopable star on the route is 3 jumps away", warning.Text, StringComparison.Ordinal);
    }

    [Fact]
    public void AnUnreadLoadoutIsNotAShipWithoutAScoopAndStillHearsTheWarning()
    {
        var warning = Assert.Single(
            Examine(new FuelReachCallout(), Ship(module: null), Route("T", "T", "T"), fuel: 1));

        Assert.Contains("not enough fuel for another jump", warning.Text, StringComparison.Ordinal);
        Assert.Contains("known to be scoopable", warning.Text, StringComparison.Ordinal);
    }

    [Fact]
    public void NothingIsSaidBeforeAJumpHasShownWhatAJumpCosts()
    {
        Assert.Empty(Examine(new FuelReachCallout(), Ship(CargoRack), Route("K", "K", "K"), fuel: 0.5, events: []));
    }

    [Fact]
    public void TheWarningIsWorkedOutOncePerSystemAndAgainForANewRoute()
    {
        var callout = new FuelReachCallout();
        var state = Ship(CargoRack);

        Assert.NotEmpty(Examine(callout, state, Route("K", "K", "K"), fuel: 3));
        Assert.Empty(Examine(callout, state, Route("K", "K", "K"), fuel: 3, events: [], atSecond: 1));

        var replotted = new NavRoute
        {
            Hops = [.. Route("K", "K", "K", "K").Hops.Select((hop, index) => index == 4 ? hop with { StarSystem = "Elsewhere" } : hop)],
            ReadAt = Start,
        };

        Assert.NotEmpty(Examine(callout, state, replotted, fuel: 3, events: [], atSecond: 2));
    }

    /// <summary>
    /// Said on the first jump that finds the shortfall, not on every jump after it, and said again for the last
    /// jump the fuel covers.
    /// </summary>
    [Fact]
    public void AStandingShortfallIsSaidOnceAndAgainForTheLastJump()
    {
        var callout = new FuelReachCallout();
        var store = Store(CargoRack);
        var route = Route("K", "K", "K", "K", "K");

        Assert.NotEmpty(Examine(callout, store.Active!, route, fuel: 6));

        store.Apply(Event(Jump("Hop 1", fuelUsed: 2)));
        Assert.Empty(Examine(callout, store.Active!, route, fuel: 4, events: [Jump("Hop 1", fuelUsed: 2)], atSecond: 30));

        store.Apply(Event(Jump("Hop 2", fuelUsed: 2)));
        var last = Assert.Single(
            Examine(callout, store.Active!, route, fuel: 2, events: [Jump("Hop 2", fuelUsed: 2)], atSecond: 60));

        Assert.Contains("fuel for 1 jump,", last.Text, StringComparison.Ordinal);
    }

    /// <summary>The fuel a jump costs is measured on the ship being flown, not averaged across a swap.</summary>
    [Fact]
    public void AnotherShipStartsItsOwnFuelAverage()
    {
        var callout = new FuelReachCallout();
        var store = Store(CargoRack);
        var route = Route("K", "K", "K", "K");

        Assert.Empty(Examine(callout, store.Active!, route, fuel: 8));

        store.Apply(Event(Loadout(CargoRack, shipId: 2)));
        store.Apply(Event(Jump("Hop 1", fuelUsed: 6)));

        // 11 t at 6 t a jump covers 1 of the 3 jumps left. Averaged with the first ship's 2 t jumps it would cover 3.
        var warning = Assert.Single(
            Examine(callout, store.Active!, route, fuel: 11, events: [Jump("Hop 1", fuelUsed: 6)], atSecond: 30));

        Assert.Contains("At 6 tonnes a jump", warning.Text, StringComparison.Ordinal);
    }
}
