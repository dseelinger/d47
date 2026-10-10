using D47.Core.Storage;
using D47.Core.Capabilities;
using D47.Core.Capabilities.Builtin;
using D47.Core.Configuration;
using D47.Core.Journal;
using D47.Core.Knowledge;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Knowledge;

/// <summary>
/// The model's view of route planning: what it can ask for, what it is filled in with from the journal,
/// and — the one that matters — what it is not allowed to fill in.
/// </summary>
public class RouteCapabilityTests
{
    private sealed class FakeRoutes : IRouteService
    {
        public RouteQuery? LastRoute { get; private set; }

        public RichesQuery? LastRiches { get; private set; }

        public PlottedRoute? Route { get; set; } =
            new("Sol", "Colonia", 22_000, 168, [new RouteWaypoint("PSR J1752-2806", 10, 21_629, true)]);

        public RichesRoute? Riches { get; set; } =
            new([new RichesStop("Aries Dark Region FW-W d1-59", 3, [new RichesBody("A 4", "Water world")])]);

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

        // The fifth plot type.
        public Task<ExobiologyRoute?> PlotExobiologyAsync(
            ExobiologyQuery query,
            CancellationToken cancellationToken) =>
            Task.FromResult<ExobiologyRoute?>(null);

        public Task<CarrierRoute?> PlotCarrierAsync(CarrierRouteQuery query, CancellationToken cancellationToken) =>
            Task.FromResult<CarrierRoute?>(null);
    }

    /// <summary>
    /// The trade planner, which is d47's own since Phase 36 and so a separate seam: lookups and
    /// arithmetic here, rather than a job somebody else queues.
    /// </summary>
    private sealed class FakeTrade : ITradePlanService
    {
        public TradeQuery? LastTrade { get; private set; }

        public TradeRoute? Trade { get; set; } =
            new([
                new TradeStop("Sol", "Abraham Lincoln")
                {
                    Buy = [new TradeLot("Gold", 384, 9_400)],
                    Credits = 46_390_400,
                },
                new TradeStop("RR Caeli", "Diaz Chemical Holdings")
                {
                    Distance = 20.9,
                    Sell = [new TradeLot("Gold", 384, 52_282)],
                    Credits = 66_466_688,
                },
            ])
            {
                Capital = 50_000_000,
                TotalProfit = 16_466_688,
                MarketsConsidered = 132,
            };

        public Exception? Throws { get; set; }

        public Task<TradeRoute?> PlanAsync(TradeQuery query, CancellationToken cancellationToken)
        {
            LastTrade = query;
            return Throws is not null ? Task.FromException<TradeRoute?>(Throws) : Task.FromResult(Trade);
        }

        public CommoditySearch? LastCommodity { get; private set; }

        public CommodityAnswer Commodity { get; set; } = CommodityAnswer.Empty;

        public Task<CommodityAnswer> FindCommodityAsync(
            CommoditySearch search,
            CancellationToken cancellationToken)
        {
            LastCommodity = search;

            return Throws is not null
                ? Task.FromException<CommodityAnswer>(Throws)
                : Task.FromResult(Commodity);
        }

        public SourcingSearch? LastSourcing { get; private set; }

        public SourcingAnswer Sourcing { get; set; } = SourcingAnswer.Empty;

        public Task<SourcingAnswer> SourceConstructionAsync(
            SourcingSearch search,
            CancellationToken cancellationToken)
        {
            LastSourcing = search;

            return Throws is not null
                ? Task.FromException<SourcingAnswer>(Throws)
                : Task.FromResult(Sourcing);
        }

        public Task<StationQuote?> QuoteAsync(
            long marketId,
            string commodity,
            CancellationToken cancellationToken) => Task.FromResult<StationQuote?>(null);

        public BestCargoSearch? LastBestCargo { get; private set; }

        public BestCargoAnswer? BestCargo { get; set; }

        public Task<BestCargoAnswer?> BestCargoAsync(
            BestCargoSearch search,
            CancellationToken cancellationToken)
        {
            LastBestCargo = search;
            return Task.FromResult(BestCargo);
        }
    }

    private static void Apply(GameStateStore gameState, string json)
    {
        Assert.True(JournalEvent.TryParse(json, NullLogger.Instance, out var parsed));
        gameState.Apply(parsed!);
    }

    private static (CapabilityRegistry Registry, FakeRoutes Routes, FakeTrade Trade, GameStateStore GameState) Build(
        MemoryInstall install,
        bool enabled = true,
        bool docked = true,
        RoutePlanBook? plans = null,
        NavigationSurface? navigation = null,

        // The Trade route page's own saved values (#311), seeded before the registry reads them.
        Action<SettingsService>? configureSettings = null,
        BestCargoBoard? cargo = null)
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
        var trade = new FakeTrade();
        var settings = TestSurface.For(install).Settings;

        settings.Apply(GalaxyCapability.EnabledKey, enabled ? "true" : "false", SettingsCaller.Panel);
        configureSettings?.Invoke(settings);

        return (
            CapabilityRegistry.Build(
            [
                RouteCapability.Create(
                    routes,
                    trade,
                    () => gameState.Active,
                    settings,
                    plans,
                    () => PlottedAt,
                    navigation,
                    cargo: cargo),
            ]),
            routes,
            trade,
            gameState);
    }

    /// <summary>A fixed instant, so a stored plan's timestamp is assertable.</summary>
    private static readonly DateTimeOffset PlottedAt = new(2026, 8, 20, 9, 0, 0, TimeSpan.Zero);

    private static ToolArguments Args(params (string Name, string Value)[] values) =>
        new(values.ToDictionary(v => v.Name, v => v.Value, StringComparer.Ordinal));

    [Fact]
    public async Task TheShipsOwnJumpRangeAndPositionFillThemselvesIn()
    {
        // Nobody says "plot me a route to Colonia from Sol at 52.31 light years a jump".
        var install = new MemoryInstall();
        var (registry, routes, trade, _) = Build(install);

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
        // Accepted and then failed to route, measured Sol to Colonia on 2026-08-14.
        var install = new MemoryInstall();
        var (registry, routes, trade, _) = Build(install);

        await registry.InvokeAsync(
            "plot_route",
            Args(("to", "Colonia"), ("efficiency", "100")),
            TestContext.Current.CancellationToken);

        Assert.Equal(99, routes.LastRoute?.Efficiency);
    }

    [Fact]
    public async Task AShipTooShortRangedToPlotForIsToldSoRatherThanRefusedByTheService()
    {
        var install = new MemoryInstall();
        var (registry, routes, trade, _) = Build(install);

        var result = await registry.InvokeAsync(
            "plot_route",
            Args(("to", "Colonia"), ("jump_range", "8")),
            TestContext.Current.CancellationToken);

        Assert.True(result.IsError);
        Assert.Contains("over 10 light years", result.Content, StringComparison.Ordinal);
        Assert.Null(routes.LastRoute);
    }

 /// <summary>The last waypoint does not announce zero light years left.</summary>
    [Fact]
    public async Task TheDestinationWaypointSaysNothingAboutDistanceLeft()
    {
        var install = new MemoryInstall();
        var (registry, routes, trade, _) = Build(install);

        routes.Route = new PlottedRoute(
            "CD-39 3269",
            "Meene",
            70,
            3,
            [
                new RouteWaypoint("Jackson's Lighthouse", 2, 55, true),

                // What the plotter reports for the destination, every time.
                new RouteWaypoint("Meene", 1, 0, false),
            ]);

        var result = await registry.InvokeAsync(
            "plot_route",
            Args(("to", "Meene")),
            TestContext.Current.CancellationToken);

        Assert.DoesNotContain("0 ly left", result.Content, StringComparison.Ordinal);

        // And the figure that does mean something is still said.
        Assert.Contains("55 ly left", result.Content, StringComparison.Ordinal);
        Assert.Contains("Meene — 1 jump", result.Content, StringComparison.Ordinal);
    }

    /// <summary>
    /// A part-light-year remainder prints as "0" too, so the test is what would be shown rather than
 /// what was measured.
    /// </summary>
    [Theory]
    [InlineData(0)]
    [InlineData(0.4)]
    public void ARemainderThatWouldPrintAsZeroIsNotWorthReporting(double left) =>
        Assert.Null(new RouteWaypoint("Meene", 1, left, false).DistanceLeftToReport);

    [Fact]
    public async Task ALongRouteSaysHowManyWaypointsThereAreRatherThanReadingThemAllOut()
    {
        var install = new MemoryInstall();
        var (registry, routes, trade, _) = Build(install);

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
        var install = new MemoryInstall();
        var (registry, routes, trade, _) = Build(install);

        routes.Route = null;

        var result = await registry.InvokeAsync(
            "plot_route",
            Args(("to", "Colonia")),
            TestContext.Current.CancellationToken);

        // The service reports this as a completed job whose status is "failed", and telling the Commander to
        // try again would send them at something that fails identically every time.
        Assert.False(result.IsError);
        Assert.Contains("No route", result.Content, StringComparison.Ordinal);
        Assert.Contains("lower efficiency", result.Content, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ATradeRouteWillNotInferTheCommandersBalance()
    {
        // The one figure here that is about the Commander rather than their ship.
        var install = new MemoryInstall();
        var (registry, routes, trade, _) = Build(install);

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
        Assert.Null(trade.LastTrade);
    }

    [Fact]
    public async Task ATradeRouteDoesFillInTheHoldFromTheShip()
    {
        var install = new MemoryInstall();
        var (registry, routes, trade, _) = Build(install);

        await registry.InvokeAsync(
            "plot_trade_route",
            Args(("capital", "50000000"), ("jump_range", "30")),
            TestContext.Current.CancellationToken);

        // A property of the hull, and the plot means nothing without it.
        Assert.Equal(384, trade.LastTrade?.CargoCapacity);
        Assert.Equal("Abraham Lincoln", trade.LastTrade?.Station);
    }

    /// <summary>A trade route never sells limpets, so they come off the default hold with no switch (#310).</summary>
    [Fact]
    public async Task TheDefaultHoldLeavesRoomForTheLimpetsAlreadyAboard()
    {
        var install = new MemoryInstall();

        install.Files.WriteLines(
            Path.Combine(install.Root, "Journal.2026-09-20T000000.01.log"),
            [
                """{"timestamp":"2026-09-20T00:00:00Z","event":"Commander","FID":"F1","Name":"Fixture"}""",
                """{"timestamp":"2026-09-20T00:00:01Z","event":"Loadout","Ship":"anaconda","MaxJumpRange":52.31,"CargoCapacity":256}""",
                """{"timestamp":"2026-09-20T00:00:02Z","event":"Docked","StarSystem":"Sol","StationName":"Abraham Lincoln"}""",
            ]);

        install.Files.WriteText(
            Path.Combine(install.Root, D47.Core.Journal.CargoManifestReader.ManifestFile),
            """{ "timestamp":"2026-09-20T00:00:03Z", "event":"Cargo", "Vessel":"Ship", "Count":8, "Inventory":[ { "Name":"drones", "Name_Localised":"Limpet", "Count":8, "Stolen":0 } ] }""");

        var gameState = new GameStateStore();
        new JournalSpine(install.Root, install.Files, gameState, NullLoggerFactory.Instance).Poll();

        var routes = new FakeRoutes();
        var trade = new FakeTrade();
        var settings = TestSurface.For(install).Settings;
        settings.Apply(GalaxyCapability.EnabledKey, "true", SettingsCaller.Panel);

        var registry = CapabilityRegistry.Build(
        [
            RouteCapability.Create(routes, trade, () => gameState.Active, settings),
        ]);

        var result = await registry.InvokeAsync(
            "plot_trade_route",
            Args(("capital", "50000000"), ("jump_range", "30")),
            TestContext.Current.CancellationToken);

        Assert.False(result.IsError);

        // 256 tonnes of racks, 8 limpets aboard: 248 tonnes to trade with.
        Assert.Equal(248, trade.LastTrade?.CargoCapacity);
        Assert.Contains("248 tonne hold", result.Content, StringComparison.Ordinal);
    }

    /// <summary>
    /// A stop behind a permit is one the Commander cannot fly to, so the switch is on unless they
    /// turn it off (#310).
    /// </summary>
    [Fact]
    public async Task PermitSystemsAreAvoidedUnlessTheCommanderSaysOtherwise()
    {
        var install = new MemoryInstall();
        var (registry, _, trade, _) = Build(install);

        await registry.InvokeAsync(
            "plot_trade_route",
            Args(("capital", "50000000"), ("jump_range", "30")),
            TestContext.Current.CancellationToken);

        Assert.True(trade.LastTrade?.AvoidPermitSystems);

        await registry.InvokeAsync(
            "plot_trade_route",
            Args(("capital", "50000000"), ("jump_range", "30"), ("avoid_permit_systems", "false")),
            TestContext.Current.CancellationToken);

        Assert.False(trade.LastTrade?.AvoidPermitSystems);
    }

    /// <summary>
    /// The Trade route page's own saved values (#311) fill in anything a call doesn't give — a voice
    /// plot that names only the credits runs with whatever the page last saved.
    /// </summary>
    [Fact]
    public async Task ATradeRouteUsesTheSavedSettingsForAnythingNotGiven()
    {
        var install = new MemoryInstall();
        var (registry, _, trade, _) = Build(
            install,
            configureSettings: settings => settings.Replace(
                "test",
                s => s with
                {
                    Trade = s.Trade with
                    {
                        Hops = 8,
                        MaxJumps = 4,
                        MaxStationDistance = 500,
                        MaxPriceAgeHours = 24,
                        Loop = true,
                        LargePadOnly = true,
                        Planetary = true,
                        AvoidPermitSystems = false,
                    },
                }));

        await registry.InvokeAsync(
            "plot_trade_route",
            Args(("capital", "50000000"), ("jump_range", "30")),
            TestContext.Current.CancellationToken);

        Assert.Equal(8, trade.LastTrade?.MaxHops);
        Assert.Equal(4, trade.LastTrade?.MaxJumps);
        Assert.Equal(500, trade.LastTrade?.MaxSystemDistance);
        Assert.Equal(24, trade.LastTrade?.MaxPriceAge);
        Assert.True(trade.LastTrade?.Loop);
        Assert.True(trade.LastTrade?.LargePadOnly);
        Assert.True(trade.LastTrade?.Planetary);
        Assert.False(trade.LastTrade?.AvoidPermitSystems);

        // An argument that is given still overrides the saved value.
        await registry.InvokeAsync(
            "plot_trade_route",
            Args(("capital", "50000000"), ("jump_range", "30"), ("hops", "3")),
            TestContext.Current.CancellationToken);

        Assert.Equal(3, trade.LastTrade?.MaxHops);
    }

    [Fact]
    public async Task ATradeRouteWithNoKnownJumpRangeAsksForOneRatherThanSendingAnything()
    {
        // The fixture's ship carries no modules, so d47 cannot work out a laden range for it, and none is
        // given here either.
        var install = new MemoryInstall();
        var (registry, routes, trade, _) = Build(install);

        var result = await registry.InvokeAsync(
            "plot_trade_route",
            Args(("capital", "50000000")),
            TestContext.Current.CancellationToken);

        Assert.True(result.IsError);
        Assert.Contains("laden jump range", result.Content, StringComparison.Ordinal);
        Assert.Null(trade.LastTrade);
    }

    /// <summary>The staleness bound is spelled `max_price_age_hours` here as well as on the commodity search,
    /// and the handler reads it under that name — a rename that reached the schema and not the handler would
    /// advertise a knob that silently does nothing.</summary>
    [Fact]
    public async Task TheStalenessBoundIsSpelledWithItsUnit()
    {
        var install = new MemoryInstall();
        var (registry, _, trade, _) = Build(install);

        await registry.InvokeAsync(
            "plot_trade_route",
            Args(("capital", "50000000"), ("jump_range", "30"), ("max_price_age_hours", "48")),
            TestContext.Current.CancellationToken);

        Assert.Equal(48, trade.LastTrade?.MaxPriceAge);
    }

    [Fact]
    public async Task ATradeRouteCannotBePlottedFromSupercruise()
    {
        var install = new MemoryInstall();
        var (registry, routes, trade, _) = Build(install, docked: false);

        var result = await registry.InvokeAsync(
            "plot_trade_route",
            Args(("capital", "50000000")),
            TestContext.Current.CancellationToken);

        // The service keys the whole plot on a starting station id, so there is no version of this question
        // that can be asked in flight.
        Assert.True(result.IsError);
        Assert.Contains("not docked", result.Content, StringComparison.Ordinal);
    }

    /// <summary>Asking the same question Trading Mode answers on its own, about any system (#313).</summary>
    [Fact]
    public async Task BestCommoditiesForSaysWhatToBuyHereForTheNamedSystem()
    {
        var install = new MemoryInstall();
        var (registry, _, trade, _) = Build(install);

        trade.BestCargo = new BestCargoAnswer([new CargoPick("Gold", "Newholm Station", 8_204, 280, CargoLimit.Demand)], 384);

        var result = await registry.InvokeAsync(
            "best_commodities_for",
            Args(("system", "Sothis")),
            TestContext.Current.CancellationToken);

        Assert.False(result.IsError);
        Assert.Equal(
            "Best cargo for Sothis: Gold to Newholm Station, 8,204 Cr a tonne, 2.3 million for 280 tonnes.",
            result.Content);

        Assert.Equal("Sol", trade.LastBestCargo?.System);
        Assert.Equal("Abraham Lincoln", trade.LastBestCargo?.Station);
        Assert.Equal("Sothis", trade.LastBestCargo?.Destination);
        Assert.Equal(384, trade.LastBestCargo?.Hold);
    }

    [Fact]
    public async Task BestCommoditiesForNeedsTheMarketYouAreDockedAt()
    {
        var install = new MemoryInstall();
        var (registry, _, trade, _) = Build(install, docked: false);

        var result = await registry.InvokeAsync(
            "best_commodities_for",
            Args(("system", "Sothis")),
            TestContext.Current.CancellationToken);

        Assert.True(result.IsError);
        Assert.Contains("docked", result.Content, StringComparison.Ordinal);
        Assert.Null(trade.LastBestCargo);
    }

    /// <summary>The page draws what the tool found, so the tool posts every search it finishes (#849).</summary>
    [Fact]
    public async Task BestCommoditiesForPostsTheSearchAndItsAnswerToTheBoard()
    {
        var install = new MemoryInstall();
        var board = new BestCargoBoard();
        var (registry, _, trade, _) = Build(install, cargo: board);
        var posted = 0;
        board.Posted += () => posted++;

        trade.BestCargo = new BestCargoAnswer(
        [
            new CargoPick("Gold", "Newholm Station", 8_204, 280, CargoLimit.Demand),
            new CargoPick("Silver", "Newholm Station", 4_000, 384, CargoLimit.Hold),
            new CargoPick("Tea", "Newholm Station", 900, 120, CargoLimit.Supply),
        ], 384);

        var result = await registry.InvokeAsync(
            "best_commodities_for",
            Args(("system", "Sothis")),
            TestContext.Current.CancellationToken);

        Assert.Equal(1, posted);
        Assert.Equal("Sothis", board.Last?.Search.Destination);
        Assert.Equal("Abraham Lincoln", board.Last?.Search.Station);
        Assert.Equal(PlottedAt, board.Last?.AskedAt);
        Assert.Equal(3, board.Last?.Answer?.Picks.Count);

        // The sentence still names two.
        Assert.DoesNotContain("Tea", result.Content, StringComparison.Ordinal);

        trade.BestCargo = null;

        await registry.InvokeAsync("best_commodities_for", Args(("system", "Sothis")), TestContext.Current.CancellationToken);

        Assert.Equal(2, posted);
        Assert.NotNull(board.Last);
        Assert.Null(board.Last.Answer);
    }

    [Fact]
    public async Task ARefusedBestCargoSearchPostsNothing()
    {
        var install = new MemoryInstall();
        var board = new BestCargoBoard();
        var (registry, _, _, _) = Build(install, docked: false, cargo: board);

        await registry.InvokeAsync("best_commodities_for", Args(("system", "Sothis")), TestContext.Current.CancellationToken);

        Assert.Null(board.Last);
    }

    [Fact]
    public async Task BestCommoditiesForSaysWhenNothingPays()
    {
        var install = new MemoryInstall();
        var (registry, _, trade, _) = Build(install);

        trade.BestCargo = new BestCargoAnswer([], 384);

        var result = await registry.InvokeAsync(
            "best_commodities_for",
            Args(("system", "Sothis")),
            TestContext.Current.CancellationToken);

        Assert.False(result.IsError);
        Assert.Equal("Nothing here sells at a profit in Sothis.", result.Content);
        Assert.Null(trade.LastTrade);
    }

    [Fact]
    public async Task ARichesRouteReportsWhatItIsWorthAndHowFarItGoes()
    {
        var install = new MemoryInstall();
        var (registry, routes, trade, _) = Build(install);

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
        var install = new MemoryInstall();
        var (registry, routes, trade, _) = Build(install, enabled: false);

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
        var install = new MemoryInstall();
        var (registry, routes, trade, _) = Build(install);

        routes.Throws = new GalaxyUnavailableException("The route plotter is still working after 90 seconds.");

        var result = await registry.InvokeAsync(
            "plot_route",
            Args(("to", "Colonia")),
            TestContext.Current.CancellationToken);

        Assert.True(result.IsError);
        Assert.Contains("still working", result.Content, StringComparison.Ordinal);
    }

 /// <summary>A route plotted by voice lands in the book the Routing tab reads.</summary>
    [Fact]
    public async Task APlotMadeByVoiceIsThereForASurfaceToDraw()
    {
        var install = new MemoryInstall();

        var plans = new RoutePlanBook(
            Path.Combine(install.Root, "data", "route-plans.json"),
            new MemoryFileSystem(), Microsoft.Extensions.Logging.Abstractions.NullLogger<RoutePlanBook>.Instance);

        var (registry, _, _, _) = Build(install, plans: plans);

        var result = await registry.InvokeAsync(
            "plot_route",
            Args(("to", "Colonia")),
            TestContext.Current.CancellationToken);

        Assert.False(result.IsError);

        var kept = plans.Last(RoutePlanKind.Jump);

        Assert.NotNull(kept);
        Assert.Equal(PlottedAt, kept.PlottedAt);
        Assert.Equal("Colonia", kept.Jump?.Destination);

        // The whole route, not the five waypoints the sentence carries.
        Assert.Equal(kept.Jump!.Waypoints.Count, kept.Jump.Waypoints.Count);
    }

    [Fact]
    public async Task APlotThatFoundNothingLeavesTheBookAlone()
    {
        var install = new MemoryInstall();

        var plans = new RoutePlanBook(
            Path.Combine(install.Root, "data", "route-plans.json"),
            new MemoryFileSystem(), Microsoft.Extensions.Logging.Abstractions.NullLogger<RoutePlanBook>.Instance);

        var (registry, routes, _, _) = Build(install, plans: plans);

        routes.Route = null;

        var result = await registry.InvokeAsync(
            "plot_route",
            Args(("to", "Colonia")),
            TestContext.Current.CancellationToken);

        Assert.False(result.IsError);
        Assert.Null(plans.Last(RoutePlanKind.Jump));
    }

    /// <summary>
    /// Plotting the next stop on a stored plan by voice (#211): the Neutron Plotter, Road to Riches
    /// and trade planners all share this tool, told apart by <c>kind</c>.
    /// </summary>
    private static RoutePlanBook PlanBook(MemoryInstall install) => new(
        Path.Combine(install.Root, "data", "route-plans.json"),
        new MemoryFileSystem(), NullLogger<RoutePlanBook>.Instance);

    /// <summary>A navigation surface whose clipboard works but never drives the galaxy map.</summary>
    private static NavigationSurface CopyOnlyNavigation() => new()
    {
        Clipboard = new RecordingClipboard(),
        Actions = ActionSurface.Inert,
        AutoPlotEnabled = () => false,
        WatchRoute = () => new FixedPlotWatch(null),
        AwaitGalaxyMap = (_, _) => Task.FromResult<bool?>(null),
    };

    /// <summary>Automatic plotting allowed, but with nothing bound to drive the map.</summary>
    private static NavigationSurface AutoPlotNavigation() => new()
    {
        Clipboard = new RecordingClipboard(),
        Actions = ActionSurface.Inert with { Enabled = () => true },
        AutoPlotEnabled = () => true,
        WatchRoute = () => new FixedPlotWatch(null),
        AwaitGalaxyMap = (_, _) => Task.FromResult<bool?>(null),
    };

    private static JournalEvent Arrival(string kind, string system, DateTimeOffset at) =>
        JournalEvent.TryParse(
            $$"""{"timestamp":"{{at:O}}","event":"{{kind}}","StarSystem":"{{system}}"}""",
            NullLogger.Instance,
            out var parsed)
            ? parsed!
            : throw new InvalidOperationException("bad journal fixture");

    private static PlottedRoute TwoWaypointJump() => new(
        "Procyon",
        "Byua Euq XQ-G c10-18",
        500,
        21,
        [
            new RouteWaypoint("PSR J1752-2806", 10, 400, true),
            new RouteWaypoint("Col 359 Sector NN-T e3-3", 11, 0, false),
        ]);

    [Fact]
    public async Task WithNoStoredPlanOfThatKindNothingIsPlottedAndItSaysSo()
    {
        var install = new MemoryInstall();
        var plans = PlanBook(install);
        plans.Record(TwoWaypointJump(), "Procyon to Byua Euq XQ-G c10-18", PlottedAt);

        var navigation = CopyOnlyNavigation();
        var (registry, _, _, _) = Build(install, plans: plans, navigation: navigation);

        // Trade was never asked for, only jump was stored.
        var result = await registry.InvokeAsync(
            "plot_next_stop",
            Args(("kind", "trade")),
            TestContext.Current.CancellationToken);

        Assert.False(result.IsError);
        Assert.Contains("No trade plan is stored", result.Content, StringComparison.Ordinal);
        Assert.Empty(((RecordingClipboard)navigation.Clipboard).Written);
    }

    [Fact]
    public async Task WithNothingReachedItPlotsTheFirstStop()
    {
        var install = new MemoryInstall();
        var plans = PlanBook(install);
        plans.Record(TwoWaypointJump(), "Procyon to Byua Euq XQ-G c10-18", PlottedAt);

        var navigation = CopyOnlyNavigation();
        var (registry, _, _, _) = Build(install, plans: plans, navigation: navigation);

        var result = await registry.InvokeAsync(
            "plot_next_stop",
            Args(("kind", "neutron")),
            TestContext.Current.CancellationToken);

        Assert.False(result.IsError);
        Assert.Equal("PSR J1752-2806", ((RecordingClipboard)navigation.Clipboard).Last);
        Assert.Contains("Stop 1 of 2", result.Content, StringComparison.Ordinal);
    }

    [Fact]
    public async Task WithAStopReachedItPlotsTheOneAfter()
    {
        var install = new MemoryInstall();
        var plans = PlanBook(install);
        plans.Record(TwoWaypointJump(), "Procyon to Byua Euq XQ-G c10-18", PlottedAt);
        plans.Apply([Arrival("FSDJump", "PSR J1752-2806", PlottedAt.AddMinutes(10))]);

        var navigation = CopyOnlyNavigation();
        var (registry, _, _, _) = Build(install, plans: plans, navigation: navigation);

        var result = await registry.InvokeAsync(
            "plot_next_stop",
            Args(("kind", "neutron")),
            TestContext.Current.CancellationToken);

        Assert.False(result.IsError);
        Assert.Equal("Col 359 Sector NN-T e3-3", ((RecordingClipboard)navigation.Clipboard).Last);
        Assert.Contains("Stop 2 of 2", result.Content, StringComparison.Ordinal);

        // Plotting reads the reached stop; only an arrival moves it.
        Assert.Equal(0, plans.Last(RoutePlanKind.Jump)?.Reached);
    }

    [Fact]
    public async Task WithTheLastStopReachedNothingIsPlottedAndItSaysSo()
    {
        var install = new MemoryInstall();
        var plans = PlanBook(install);
        plans.Record(TwoWaypointJump(), "Procyon to Byua Euq XQ-G c10-18", PlottedAt);
        plans.Apply(
        [
            Arrival("FSDJump", "PSR J1752-2806", PlottedAt.AddMinutes(10)),
            Arrival("FSDJump", "Col 359 Sector NN-T e3-3", PlottedAt.AddMinutes(20)),
        ]);

        var navigation = CopyOnlyNavigation();
        var (registry, _, _, _) = Build(install, plans: plans, navigation: navigation);

        var result = await registry.InvokeAsync(
            "plot_next_stop",
            Args(("kind", "neutron")),
            TestContext.Current.CancellationToken);

        Assert.False(result.IsError);
        Assert.Contains("last stop", result.Content, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(((RecordingClipboard)navigation.Clipboard).Written);
    }

    /// <summary>
    /// A trade plan's first stop is the station the Commander plotted from, so it is skipped rather than
    /// plotted back to — and its own kind's tool never reads another kind's book.
    /// </summary>
    [Fact]
    public async Task ATradeStopInTheCurrentSystemIsSkippedAndTheStationIsNamed()
    {
        var install = new MemoryInstall();
        var plans = PlanBook(install);
        plans.Record(TwoWaypointJump(), "Procyon to Byua Euq XQ-G c10-18", PlottedAt);

        var trade = new TradeRoute(
        [
            new TradeStop("Sol", "Abraham Lincoln"),
            new TradeStop("RR Caeli", "Diaz Chemical Holdings"),
        ])
        {
            Capital = 50_000_000,
            TotalProfit = 412_800,
        };

        // Build() docks the Commander at Sol, and the plan is not recorded with a current system, so
        // nothing is reached yet — the tool itself has to skip the first stop.
        plans.Record(trade, "2 stops from Abraham Lincoln", PlottedAt);

        var navigation = CopyOnlyNavigation();
        var (registry, _, _, _) = Build(install, plans: plans, navigation: navigation);

        var result = await registry.InvokeAsync(
            "plot_next_stop",
            Args(("kind", "trade")),
            TestContext.Current.CancellationToken);

        Assert.False(result.IsError);
        Assert.Equal("RR Caeli", ((RecordingClipboard)navigation.Clipboard).Last);
        Assert.Contains("Stop 2 of 2", result.Content, StringComparison.Ordinal);
        Assert.Contains("Diaz Chemical Holdings", result.Content, StringComparison.Ordinal);

        // Neutron is untouched by asking for trade.
        Assert.Null(plans.Last(RoutePlanKind.Jump)?.Reached);
    }

    [Fact]
    public async Task AKindArgumentInAnyCaseStillReachesTheRightPlan()
    {
        // The registry checks AllowedValues case-insensitively before the handler ever sees it.
        var install = new MemoryInstall();
        var plans = PlanBook(install);
        plans.Record(TwoWaypointJump(), "Procyon to Byua Euq XQ-G c10-18", PlottedAt);

        var navigation = CopyOnlyNavigation();
        var (registry, _, _, _) = Build(install, plans: plans, navigation: navigation);

        var result = await registry.InvokeAsync(
            "plot_next_stop",
            Args(("kind", "Neutron")),
            TestContext.Current.CancellationToken);

        Assert.False(result.IsError);
        Assert.Equal("PSR J1752-2806", ((RecordingClipboard)navigation.Clipboard).Last);
    }

    [Fact]
    public async Task AutomaticPlottingBothOnAndOffStillNamesTheStop()
    {
        var install = new MemoryInstall();
        var plans = PlanBook(install);
        plans.Record(TwoWaypointJump(), "Procyon to Byua Euq XQ-G c10-18", PlottedAt);

        var navigation = AutoPlotNavigation();
        var (registry, _, _, _) = Build(install, plans: plans, navigation: navigation);

        var result = await registry.InvokeAsync(
            "plot_next_stop",
            Args(("kind", "neutron")),
            TestContext.Current.CancellationToken);

        Assert.False(result.IsError);
        Assert.Contains("Stop 1 of 2", result.Content, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AnExobiologyPlanPlotsItsNextSystem()
    {
        var install = new MemoryInstall();
        var plans = PlanBook(install);
        plans.Record(
            new ExobiologyRoute([new ExobiologyStop("Opet", 3, []), new ExobiologyStop("Wregoe AC-D d12-8", 2, [])]),
            "2 stops from Sol",
            PlottedAt);
        plans.Apply([Arrival("FSDJump", "Opet", PlottedAt.AddMinutes(10))]);

        var navigation = CopyOnlyNavigation();
        var (registry, _, _, _) = Build(install, plans: plans, navigation: navigation);

        var result = await registry.InvokeAsync(
            "plot_next_stop",
            Args(("kind", "exobiology")),
            TestContext.Current.CancellationToken);

        Assert.False(result.IsError);
        Assert.Equal("Wregoe AC-D d12-8", ((RecordingClipboard)navigation.Clipboard).Last);
        Assert.Contains("Stop 2 of 2", result.Content, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("plot next exobiology stop")]
    [InlineData("plot the next exobiology stop")]
    [InlineData("next exobiology stop")]
    public void TheNextExobiologyStopIsReachedWithoutTheModel(string said)
    {
        var install = new MemoryInstall();

        var match = TestSurface.For(install).Router.MatchToolCommand(said);

        Assert.Equal("plot_next_stop", match?.ToolName);
        Assert.True(match!.Arguments.TryGetString("kind", out var kind));
        Assert.Equal("exobiology", kind);
    }
}
