using D47.Core.Callouts;
using D47.Core.Capabilities;
using D47.Core.Capabilities.Builtin;
using D47.Core.Journal;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Callouts;

/// <summary>
/// Elite plotting a route without rewriting <c>NavRoute.json</c>: the fuel callouts, the situation digest and
/// <c>get_location</c> stop reading the file's hops (#2062).
/// </summary>
public class FuelAndLocationAnswersHonourAChangedRouteTests
{
    private static readonly DateTimeOffset Start = DateTimeOffset.Parse("3311-01-01T00:00:00Z");

    private const string Scoop = "int_fuelscoop_size6_class5";

    private const string CargoRack = "int_cargorack_size6_class1";

    private const string AJump =
        """{"timestamp":"3311-01-01T00:00:01Z","event":"FSDJump","StarSystem":"Here","JumpDist":40,"FuelUsed":2}""";

    private static readonly GameStatus LowTank = new() { Flags = StatusFlags.InMainShip, FuelMain = 1, ReadAt = Start };

    private static JournalEvent Event(string json)
    {
        Assert.True(JournalEvent.TryParse(json, NullLogger.Instance, out var parsed));
        return parsed!;
    }

    private static GameStateStore Store(string module)
    {
        var store = new GameStateStore();
        store.Apply(Event("""{"timestamp":"3311-01-01T00:00:00Z","event":"Commander","FID":"F1","Name":"Jameson"}"""));
        store.Apply(Event(
            $$"""{"timestamp":"3311-01-01T00:00:00Z","event":"Loadout","Ship":"Anaconda","ShipID":1,"MaxJumpRange":50,"FuelCapacity":{"Main":32},"Modules":[{"Slot":"Slot03_Size6","Item":"{{module}}","On":true,"Health":1.0}]}"""));
        store.Apply(Event(AJump));

        return store;
    }

    private static NavRoute Route(params string[] classesAhead) => new()
    {
        Hops =
        [
            new RouteHop("Here", "K"),
            .. classesAhead.Select((starClass, index) => new RouteHop($"Hop {index + 1}", starClass)),
        ],
        ReadAt = Start,
    };

    private static LiveRoute Live(NavRoute file, string target, string starClass, int jumps)
    {
        var live = new LiveRoute();

        live.Fold(
            [Event($$"""{"timestamp":"3311-01-01T00:00:02Z","event":"FSDTarget","Name":"{{target}}","StarClass":"{{starClass}}","RemainingJumpsInRoute":{{jumps}}}""")],
            file,
            "Here",
            null);

        return live;
    }

    /// <summary>The target is a system the file does not name.</summary>
    private static LiveRoute Changed(NavRoute file, string starClass = "DA") => Live(file, "Elsewhere", starClass, 2);

    /// <summary>The target is the file's own next hop with its count.</summary>
    private static LiveRoute Unchanged(NavRoute file, string starClass) => Live(file, "Hop 1", starClass, 3);

    private static CalloutContext Context(
        CommanderGameState state,
        NavRoute file,
        LiveRoute live,
        GameStatus? status = null,
        params string[] events) =>
        new(Start, IsPriming: false, state, status ?? GameStatus.Unknown, file, [.. events.Select(Event)])
        {
            LiveRoute = live,
        };

    [Fact]
    public void AChangedRouteWarnsOnlyOfTheTargetsStar()
    {
        var file = Route("K", "N", "G");
        var live = Changed(file);
        Assert.True(live.Changed);

        var said = Assert.Single(new FuelCallout().Examine(Context(Store(Scoop).Active!, file, live)));

        Assert.Equal("fuel.route.unscoopable", said.Key);
        Assert.Contains("Elsewhere", said.Text, StringComparison.Ordinal);
        Assert.DoesNotContain("Hop", said.Text, StringComparison.Ordinal);
    }

    [Fact]
    public void AChangedRouteWhoseTargetIsScoopableSaysNothingEvenIfTheFileHasAWhiteDwarf()
    {
        var file = Route("DA", "N", "G");
        var live = Changed(file, starClass: "K");
        Assert.True(live.Changed);

        Assert.Empty(new FuelCallout().Examine(Context(Store(Scoop).Active!, file, live)));
    }

    [Fact]
    public void AChangedRouteNeverEstimatesTheJumpBeyondTheTarget()
    {
        // From the file's legs this would be a strand warning.
        var file = new NavRoute
        {
            Hops =
            [
                new RouteHop("Here", "K") { Position = (0, 0, 0) },
                new RouteHop("Hop 1", "K") { Position = (500, 0, 0) },
                new RouteHop("Hop 2", "K") { Position = (1000, 0, 0) },
            ],
            ReadAt = Start,
        };
        var live = Changed(file);
        Assert.True(live.Changed);

        var said = Assert.Single(new FuelCallout().Examine(Context(Store(Scoop).Active!, file, live)));

        Assert.Equal("fuel.route.unscoopable", said.Key);
    }

    [Fact]
    public void AnUnchangedRouteIsStillWarnedOfFromTheFile()
    {
        var file = Route("DA", "K", "K");
        var live = Unchanged(file, "DA");
        Assert.False(live.Changed);

        var said = Assert.Single(new FuelCallout().Examine(Context(Store(Scoop).Active!, file, live)));

        Assert.Contains("Hop 1", said.Text, StringComparison.Ordinal);
    }

    [Fact]
    public void AChangedRouteIsNotMeasuredAgainstTheFilesHopsForFuelReach()
    {
        var file = Route("T", "T", "T");
        var live = Changed(file);
        Assert.True(live.Changed);

        Assert.Empty(new FuelReachCallout().Examine(Context(Store(CargoRack).Active!, file, live, LowTank, AJump)));
    }

    [Fact]
    public void AnUnchangedRouteIsStillMeasuredForFuelReach()
    {
        var file = Route("T", "T", "T");
        var live = Unchanged(file, "T");
        Assert.False(live.Changed);

        Assert.Single(new FuelReachCallout().Examine(Context(Store(CargoRack).Active!, file, live, LowTank, AJump)));
    }

    [Fact]
    public void AChangedRouteIsDescribedFromTheTargetInTheSituationDigest()
    {
        var state = Store(Scoop).Active!;
        var file = Route("K", "N", "G");

        var changed = Situation.Describe(state, now: Start, route: file, liveRoute: Changed(file))!;

        Assert.Contains("Next jump: Elsewhere, star class DA; 2 jumps left on the route", changed, StringComparison.Ordinal);
        Assert.DoesNotContain("Hop 1", changed, StringComparison.Ordinal);

        var unchanged = Situation.Describe(state, now: Start, route: file, liveRoute: Unchanged(file, "K"))!;

        Assert.Contains("Next jump: Hop 1, star class K; 3 jumps left on the route", unchanged, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AChangedRouteIsDescribedFromTheTargetByGetLocation()
    {
        var file = Route("K", "N", "G");
        var live = Changed(file);

        var registry = CapabilityRegistry.Build(
            [JournalCapability.Create(Store(Scoop), route: () => file, liveRoute: () => live)]);
        var result = await registry.InvokeAsync("get_location", ToolArguments.Empty, TestContext.Current.CancellationToken);

        Assert.Contains("Next jump: Elsewhere (class DA). 2 jumps left on the route.", result.Content, StringComparison.Ordinal);
        Assert.DoesNotContain("Hop 1", result.Content, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AnUnchangedRouteIsDescribedFromTheFileByGetLocation()
    {
        var file = Route("K", "N", "G");
        var live = Unchanged(file, "K");

        var registry = CapabilityRegistry.Build(
            [JournalCapability.Create(Store(Scoop), route: () => file, liveRoute: () => live)]);
        var result = await registry.InvokeAsync("get_location", ToolArguments.Empty, TestContext.Current.CancellationToken);

        Assert.Contains("Next jump: Hop 1 (class K). 3 jumps left on the route.", result.Content, StringComparison.Ordinal);
    }
}
