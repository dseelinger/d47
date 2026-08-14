using D47.Core.Capabilities;
using D47.Core.Capabilities.Builtin;
using D47.Core.Configuration;
using D47.Core.Journal;
using D47.Core.Knowledge;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Knowledge;

/// <summary>
/// The model's view of route planning: what it can ask for, what it is filled in with from the
/// journal, and — the one that matters — what it is not allowed to fill in.
/// </summary>
public class RouteCapabilityTests
{
    private sealed class FakeRoutes : IRouteService
    {
        public RouteQuery? LastRoute { get; private set; }

        public RichesQuery? LastRiches { get; private set; }

        public TradeQuery? LastTrade { get; private set; }

        public PlottedRoute? Route { get; set; } =
            new("Sol", "Colonia", 22_000, 168, [new RouteWaypoint("PSR J1752-2806", 10, 21_629, true)]);

        public RichesRoute? Riches { get; set; } =
            new([new RichesStop("Aries Dark Region FW-W d1-59", 3, [new RichesBody("A 4", "Water world")])]);

        public TradeRoute? Trade { get; set; } =
            new([new TradeHop("Sol", "Abraham Lincoln", "RR Caeli", "Diaz Chemical Holdings")]);

        public Exception? Throws { get; set; }

        public Task<PlottedRoute?> PlotAsync(RouteQuery query, CancellationToken cancellationToken)
        {
            LastRoute = query;
            return Throws is not null ? Task.FromException<PlottedRoute?>(Throws) : Task.FromResult(Route);
        }

        public Task<RichesRoute?> PlotRichesAsync(RichesQuery query, CancellationToken cancellationToken)
        {
            LastRiches = query;
            return Throws is not null ? Task.FromException<RichesRoute?>(Throws) : Task.FromResult(Riches);
        }

        public Task<TradeRoute?> PlotTradeAsync(TradeQuery query, CancellationToken cancellationToken)
        {
            LastTrade = query;
            return Throws is not null ? Task.FromException<TradeRoute?>(Throws) : Task.FromResult(Trade);
        }
    }

    private static void Apply(GameStateStore gameState, string json)
    {
        Assert.True(JournalEvent.TryParse(json, NullLogger.Instance, out var parsed));
        gameState.Apply(parsed!);
    }

    private static (CapabilityRegistry Registry, FakeRoutes Routes) Build(
        TempInstall install,
        bool enabled = true,
        bool docked = true)
    {
        var gameState = new GameStateStore();
        Apply(gameState, """{"timestamp":"2026-01-01T00:00:00Z","event":"Commander","FID":"F1","Name":"Fixture"}""");
        Apply(
            gameState,
            """{"timestamp":"2026-01-01T00:00:01Z","event":"Loadout","Ship":"anaconda","MaxJumpRange":52.31,"CargoCapacity":384}""");

        Apply(
            gameState,
            docked
                ? """{"timestamp":"2026-01-01T00:00:02Z","event":"Docked","StarSystem":"Sol","StationName":"Abraham Lincoln"}"""
                : """{"timestamp":"2026-01-01T00:00:02Z","event":"FSDJump","StarSystem":"Sol"}""");

        var routes = new FakeRoutes();
        var settings = TestSurface.For(install).Settings;

        settings.Apply(GalaxyCapability.EnabledKey, enabled ? "true" : "false", SettingsCaller.Panel);

        return (
            CapabilityRegistry.Build([RouteCapability.Create(routes, () => gameState.Active, settings)]),
            routes);
    }

    private static ToolArguments Args(params (string Name, string Value)[] values) =>
        new(values.ToDictionary(v => v.Name, v => v.Value, StringComparer.Ordinal));

    [Fact]
    public async Task TheShipsOwnJumpRangeAndPositionFillThemselvesIn()
    {
        // Nobody says "plot me a route to Colonia from Sol at 52.31 light years a jump".
        using var install = new TempInstall();
        var (registry, routes) = Build(install);

        var result = await registry.InvokeAsync(
            "plot_route",
            Args(("to", "Colonia")),
            TestContext.Current.CancellationToken);

        Assert.False(result.IsError);
        Assert.Equal("Sol", routes.LastRoute?.From);
        Assert.Equal(52.31, routes.LastRoute?.JumpRange);
    }

    [Fact]
    public async Task AnEfficiencyOfOneHundredIsNeverSentBecauseTheServiceCannotRouteWithIt()
    {
        // Accepted and then failed to route, measured Sol to Colonia on 2026-08-14. Clamping it
        // here turns a mystifying "no route exists" into a route.
        using var install = new TempInstall();
        var (registry, routes) = Build(install);

        await registry.InvokeAsync(
            "plot_route",
            Args(("to", "Colonia"), ("efficiency", "100")),
            TestContext.Current.CancellationToken);

        Assert.Equal(99, routes.LastRoute?.Efficiency);
    }

    [Fact]
    public async Task AShipTooShortRangedToPlotForIsToldSoRatherThanRefusedByTheService()
    {
        using var install = new TempInstall();
        var (registry, routes) = Build(install);

        var result = await registry.InvokeAsync(
            "plot_route",
            Args(("to", "Colonia"), ("jump_range", "8")),
            TestContext.Current.CancellationToken);

        Assert.True(result.IsError);
        Assert.Contains("over 10 light years", result.Content, StringComparison.Ordinal);
        Assert.Null(routes.LastRoute);
    }

    [Fact]
    public async Task ALongRouteSaysHowManyWaypointsThereAreRatherThanReadingThemAllOut()
    {
        using var install = new TempInstall();
        var (registry, routes) = Build(install);

        routes.Route = new PlottedRoute(
            "Sol",
            "Colonia",
            22_000,
            168,
            [.. Enumerable.Range(0, 131).Select(i => new RouteWaypoint($"Waypoint {i}", 1, 100, i == 0))]);

        var result = await registry.InvokeAsync(
            "plot_route",
            Args(("to", "Colonia")),
            TestContext.Current.CancellationToken);

        Assert.Contains("131 waypoints", result.Content, StringComparison.Ordinal);
        Assert.Contains("126 more after that", result.Content, StringComparison.Ordinal);

        // The neutron flag is the only line that changes what the Commander does on arrival.
        Assert.Contains("supercharge here", result.Content, StringComparison.Ordinal);
        Assert.DoesNotContain("Waypoint 100", result.Content, StringComparison.Ordinal);
    }

    [Fact]
    public async Task NoRouteIsAnAnswerRatherThanAFailure()
    {
        using var install = new TempInstall();
        var (registry, routes) = Build(install);

        routes.Route = null;

        var result = await registry.InvokeAsync(
            "plot_route",
            Args(("to", "Colonia")),
            TestContext.Current.CancellationToken);

        // The service reports this as a completed job whose status is "failed", and telling the
        // Commander to try again would send them at something that fails identically every time.
        Assert.False(result.IsError);
        Assert.Contains("No route", result.Content, StringComparison.Ordinal);
        Assert.Contains("lower efficiency", result.Content, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ATradeRouteWillNotInferTheCommandersBalance()
    {
        // The one figure here that is about the Commander rather than their ship. It is in the
        // journal and it does not leave without them saying a number.
        using var install = new TempInstall();
        var (registry, routes) = Build(install);

        // Required in the schema, so the model is stopped before a turn is spent on it…
        var omitted = await registry.InvokeAsync(
            "plot_trade_route",
            ToolArguments.Empty,
            TestContext.Current.CancellationToken);

        Assert.True(omitted.IsError);
        Assert.Contains("requires 'capital'", omitted.Content, StringComparison.Ordinal);

        // …and refused again below that, where the sentence says why rather than merely that.
        var zero = await registry.InvokeAsync(
            "plot_trade_route",
            Args(("capital", "0")),
            TestContext.Current.CancellationToken);

        Assert.True(zero.IsError);
        Assert.Contains("balance", zero.Content, StringComparison.Ordinal);
        Assert.Null(routes.LastTrade);
    }

    [Fact]
    public async Task ATradeRouteDoesFillInTheHoldFromTheShip()
    {
        using var install = new TempInstall();
        var (registry, routes) = Build(install);

        await registry.InvokeAsync(
            "plot_trade_route",
            Args(("capital", "50000000")),
            TestContext.Current.CancellationToken);

        // A property of the hull, and the plot means nothing without it.
        Assert.Equal(384, routes.LastTrade?.CargoCapacity);
        Assert.Equal("Abraham Lincoln", routes.LastTrade?.Station);
    }

    [Fact]
    public async Task ATradeRouteCannotBePlottedFromSupercruise()
    {
        using var install = new TempInstall();
        var (registry, routes) = Build(install, docked: false);

        var result = await registry.InvokeAsync(
            "plot_trade_route",
            Args(("capital", "50000000")),
            TestContext.Current.CancellationToken);

        // The service keys the whole plot on a starting station id, so there is no version of
        // this question that can be asked in flight.
        Assert.True(result.IsError);
        Assert.Contains("not docked", result.Content, StringComparison.Ordinal);
        Assert.Null(routes.LastTrade);
    }

    [Fact]
    public async Task ARichesRouteReportsWhatItIsWorthAndHowFarItGoes()
    {
        using var install = new TempInstall();
        var (registry, routes) = Build(install);

        routes.Riches = new RichesRoute(
        [
            new RichesStop(
                "Aries Dark Region FW-W d1-59",
                3,
                [
                    new RichesBody("A 4", "Water world")
                    {
                        MappingValue = 971_799, DistanceToArrival = 759, Terraformable = true,
                    },
                    new RichesBody("A 3", "High metal content world") { MappingValue = 578_960 },
                ]),
        ]);

        var result = await registry.InvokeAsync(
            "plot_exploration_route",
            Args(("radius", "500")),
            TestContext.Current.CancellationToken);

        Assert.False(result.IsError);
        Assert.Contains("1,550,759 credits", result.Content, StringComparison.Ordinal);
        Assert.Contains("terraformable", result.Content, StringComparison.Ordinal);

        // Worth most first: it is the number that decides whether the stop is worth making.
        Assert.True(
            result.Content.IndexOf("A 4", StringComparison.Ordinal)
            < result.Content.IndexOf("A 3", StringComparison.Ordinal));
    }

    [Fact]
    public async Task PlottingIsOffWithTheGalaxySearchAndSaysWhichSettingItIs()
    {
        using var install = new TempInstall();
        var (registry, routes) = Build(install, enabled: false);

        var result = await registry.InvokeAsync(
            "plot_route",
            Args(("to", "Colonia")),
            TestContext.Current.CancellationToken);

        Assert.True(result.IsError);
        Assert.Contains("galaxy search setting", result.Content, StringComparison.Ordinal);
        Assert.Null(routes.LastRoute);
    }

    [Fact]
    public async Task AnUnreachablePlotterIsAnErrorResultNotAnException()
    {
        using var install = new TempInstall();
        var (registry, routes) = Build(install);

        routes.Throws = new GalaxyUnavailableException("The route plotter is still working after 90 seconds.");

        var result = await registry.InvokeAsync(
            "plot_route",
            Args(("to", "Colonia")),
            TestContext.Current.CancellationToken);

        Assert.True(result.IsError);
        Assert.Contains("still working", result.Content, StringComparison.Ordinal);
    }
}
