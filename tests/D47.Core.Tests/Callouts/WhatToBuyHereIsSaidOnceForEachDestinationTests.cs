using D47.Core.Callouts;
using D47.Core.Configuration;
using D47.Core.Journal;
using D47.Core.Knowledge;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Callouts;

/// <summary>Docked with a route plotted: what to buy here for the system at the end of it.</summary>
public class WhatToBuyHereIsSaidOnceForEachDestinationTests
{
    private const string Home = "Fixture";
    private const string Pad = "Home Dock";
    private const string Far = "Sothis";

    private sealed class FakeTrade(BestCargoAnswer? answer = null) : ITradePlanService
    {
        public List<BestCargoSearch> Asked { get; } = [];

        public Exception? Throws { get; set; }

        public Task<BestCargoAnswer?> BestCargoAsync(
            BestCargoSearch search,
            CancellationToken cancellationToken)
        {
            Asked.Add(search);

            if (Throws is not null)
            {
                return Task.FromException<BestCargoAnswer?>(Throws);
            }

            return Task.FromResult<BestCargoAnswer?>(answer ?? new BestCargoAnswer(
                [new CargoPick("Gold", "Newholm Station", 8_204, 280)], search.Hold));
        }

        public Task<TradeRoute?> PlanAsync(TradeQuery query, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<CommodityAnswer> FindCommodityAsync(
            CommoditySearch search,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<SourcingAnswer> SourceConstructionAsync(
            SourcingSearch search,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<StationQuote?> QuoteAsync(
            long marketId,
            string commodity,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }

    private static (TradingModeCallout Callout, FakeTrade Trade, List<Func<Task>> Dispatched) Build(
        BestCargoAnswer? answer = null,
        bool galaxySearch = true,
        Exception? throws = null)
    {
        var trade = new FakeTrade(answer) { Throws = throws };
        var dispatched = new List<Func<Task>>();

        var callout = new TradingModeCallout(NullLogger<TradingModeCallout>.Instance)
        {
            Trade = () => galaxySearch ? trade : null,
            Dispatch = dispatched.Add,
        };

        return (callout, trade, dispatched);
    }

    private static async Task RunDispatched(List<Func<Task>> dispatched)
    {
        foreach (var work in dispatched)
        {
            await work();
        }

        dispatched.Clear();
    }

    private static NavRoute Route(params string[] systems) =>
        new() { Hops = [.. systems.Select(system => new RouteHop(system, "K"))] };

    private static CalloutContext Context(
        CommanderGameState state,
        NavRoute route,
        bool priming = false,
        params string[] lines) =>
        new(
            DateTimeOffset.UnixEpoch,
            IsPriming: priming,
            State: state,
            Status: GameStatus.Unknown,
            Route: route,
            Events: [.. lines.Select(Parse)]);

    /// <summary>A Commander docked at <paramref name="station"/> in a ship carrying limpets.</summary>
    private static CommanderGameState Commander(int cargoCapacity = 280, int limpets = 0, string? station = Pad)
    {
        var install = new TempInstall();

        File.WriteAllLines(
            Path.Combine(install.Root, "Journal.2026-09-19T090000.01.log"),
            [
                """{"timestamp":"2026-09-19T09:00:00Z","event":"Commander","FID":"F1","Name":"Fixture"}""",
                $$"""{"timestamp":"2026-09-19T09:00:01Z","event":"Loadout","Ship":"type9","ShipID":1,"CargoCapacity":{{cargoCapacity}},"Modules":[]}""",
                station is null
                    ? $$"""{"timestamp":"2026-09-19T09:00:02Z","event":"Location","StarSystem":"{{Home}}","SystemAddress":1001,"Docked":false}"""
                    : $$"""{"timestamp":"2026-09-19T09:00:02Z","event":"Location","StarSystem":"{{Home}}","SystemAddress":1001,"Docked":true,"StationName":"{{station}}","StationType":"Coriolis"}""",
            ]);

        var inventory = limpets == 0
            ? ""
            : $$"""{ "Name":"drones", "Name_Localised":"Limpet", "Count":{{limpets}}, "Stolen":0 }""";

        File.WriteAllText(
            Path.Combine(install.Root, CargoManifestReader.ManifestFile),
            $$"""{ "timestamp":"2026-09-19T09:00:03Z", "event":"Cargo", "Vessel":"Ship", "Count":{{limpets}}, "Inventory":[ {{inventory}} ] }""");

        var store = new GameStateStore();
        new JournalSpine(install.Root, store, NullLoggerFactory.Instance).Poll();

        return store.Active!;
    }

    private static JournalEvent Parse(string json)
    {
        Assert.True(JournalEvent.TryParse(json, NullLogger.Instance, out var parsed));
        return parsed!;
    }

    private const string Plotted = """
        {"timestamp":"2026-09-19T09:05:00Z","event":"NavRoute"}
        """;

    private const string Opened = """
        {"timestamp":"2026-09-19T09:05:00Z","event":"Market","MarketID":1,"StationName":"Home Dock",
         "StarSystem":"Fixture"}
        """;

    [Fact]
    public async Task PlottingARouteWhileDockedNamesWhatToBuyForTheLastSystemOnIt()
    {
        var (callout, trade, dispatched) = Build();
        var state = Commander();
        var route = Route(Home, "Waypoint", Far);

        Assert.Empty(callout.Examine(Context(state, route, lines: Plotted)));
        await RunDispatched(dispatched);

        // The end of the route, not the next jump.
        Assert.Equal(Far, Assert.Single(trade.Asked).Destination);

        var said = Assert.Single(callout.Examine(Context(state, route)));

        Assert.Equal(
            "Best cargo for Sothis: Gold to Newholm Station, 8,204 Cr a tonne, 2.3 million for 280 tonnes.",
            said.Text);
    }

    [Fact]
    public async Task ASecondPlotToTheSameSystemFromTheSamePadAsksNothingMore()
    {
        var (callout, trade, dispatched) = Build();
        var state = Commander();
        var route = Route(Home, Far);

        Assert.Empty(callout.Examine(Context(state, route, lines: Plotted)));
        await RunDispatched(dispatched);
        Assert.Single(callout.Examine(Context(state, route)));

        Assert.Empty(callout.Examine(Context(state, route, lines: Plotted)));

        Assert.Empty(dispatched);
        Assert.Single(trade.Asked);
    }

    [Fact]
    public async Task AnotherDestinationFromTheSamePadIsAskedAgain()
    {
        var (callout, trade, dispatched) = Build();
        var state = Commander();

        Assert.Empty(callout.Examine(Context(state, Route(Home, Far), lines: Plotted)));
        await RunDispatched(dispatched);
        Assert.Single(callout.Examine(Context(state, Route(Home, Far))));

        var elsewhere = Route(Home, "Ceos");

        Assert.Empty(callout.Examine(Context(state, elsewhere, lines: Plotted)));
        await RunDispatched(dispatched);
        Assert.Single(callout.Examine(Context(state, elsewhere)));

        Assert.Equal([Far, "Ceos"], trade.Asked.Select(search => search.Destination));
    }

    [Fact]
    public void AHoldUnderTheThresholdAsksNothing()
    {
        var (callout, trade, dispatched) = Build();
        callout.MinHold = () => 25;

        Assert.Empty(callout.Examine(
            Context(Commander(cargoCapacity: 24), Route(Home, Far), lines: Plotted)));

        Assert.Empty(dispatched);
        Assert.Empty(trade.Asked);
    }

    [Fact]
    public async Task TheLimpetsAboardComeOffTheHoldBeforeTheThresholdIsApplied()
    {
        var (callout, trade, dispatched) = Build();
        callout.MinHold = () => 25;

        Assert.Empty(callout.Examine(
            Context(Commander(cargoCapacity: 30, limpets: 8), Route(Home, Far), lines: Plotted)));

        Assert.Empty(dispatched);
        Assert.Empty(trade.Asked);

        Assert.Empty(callout.Examine(
            Context(Commander(cargoCapacity: 300, limpets: 8), Route(Home, Far), lines: Plotted)));
        await RunDispatched(dispatched);

        Assert.Equal(292, Assert.Single(trade.Asked).Hold);
    }

    [Fact]
    public void NotDockedAsksNothing()
    {
        var (callout, trade, dispatched) = Build();

        Assert.Empty(callout.Examine(
            Context(Commander(station: null), Route(Home, Far), lines: Plotted)));

        Assert.Empty(dispatched);
        Assert.Empty(trade.Asked);
    }

    [Fact]
    public void WithNoRoutePlottedAsksNothing()
    {
        var (callout, trade, dispatched) = Build();

        Assert.Empty(callout.Examine(Context(Commander(), NavRoute.None, lines: Opened)));

        Assert.Empty(dispatched);
        Assert.Empty(trade.Asked);
    }

    [Fact]
    public void ARouteThatEndsWhereYouAreStandingAsksNothing()
    {
        var (callout, trade, dispatched) = Build();

        Assert.Empty(callout.Examine(Context(Commander(), Route("Waypoint", Home), lines: Plotted)));

        Assert.Empty(dispatched);
        Assert.Empty(trade.Asked);
    }

    [Fact]
    public void WithGalaxySearchOffNoCallIsMade()
    {
        var (callout, trade, dispatched) = Build(galaxySearch: false);

        Assert.Empty(callout.Examine(Context(Commander(), Route(Home, Far), lines: Plotted)));

        Assert.Empty(dispatched);
        Assert.Empty(trade.Asked);
    }

    [Fact]
    public void WhilePrimingNoCallIsMade()
    {
        var (callout, trade, dispatched) = Build();

        Assert.Empty(callout.Examine(
            Context(Commander(), Route(Home, Far), priming: true, lines: Plotted)));

        Assert.Empty(dispatched);
        Assert.Empty(trade.Asked);
    }

    [Fact]
    public void WithTheToggleOffNoCallIsMade()
    {
        var (callout, _, dispatched) = Build();
        var engine = new CalloutEngine(NullLogger<CalloutEngine>.Instance).Add(callout);

        engine.SetEnabled("trading-mode", false);
        engine.Tick(Context(Commander(), Route(Home, Far), lines: Plotted));

        Assert.Empty(dispatched);
    }

    [Fact]
    public void WithNoEventThisTickNoCallIsMade()
    {
        var (callout, trade, dispatched) = Build();

        Assert.Empty(callout.Examine(Context(Commander(), Route(Home, Far))));

        Assert.Empty(dispatched);
        Assert.Empty(trade.Asked);
    }

    [Fact]
    public async Task ADestinationWhereNothingPaysSaysSo()
    {
        var (callout, _, dispatched) = Build(new BestCargoAnswer([], 280));
        var state = Commander();
        var route = Route(Home, Far);

        Assert.Empty(callout.Examine(Context(state, route, lines: Plotted)));
        await RunDispatched(dispatched);

        Assert.Equal(
            "Nothing here sells at a profit in Sothis.",
            Assert.Single(callout.Examine(Context(state, route))).Text);
    }

    [Fact]
    public async Task AnAnswerThatArrivesAfterTheRouteChangedIsNotSaid()
    {
        var (callout, _, dispatched) = Build();
        var state = Commander();

        Assert.Empty(callout.Examine(Context(state, Route(Home, Far), lines: Plotted)));
        await RunDispatched(dispatched);

        Assert.Empty(callout.Examine(Context(state, Route(Home, "Ceos"))));
    }

    [Fact]
    public async Task AFailedLookupIsNotSpoken()
    {
        var (callout, _, dispatched) = Build(throws: new GalaxyUnavailableException("Spansh is down."));
        var state = Commander();
        var route = Route(Home, Far);

        Assert.Empty(callout.Examine(Context(state, route, lines: Plotted)));
        await RunDispatched(dispatched);

        Assert.Empty(callout.Examine(Context(state, route)));
    }

    [Fact]
    public async Task APairWhoseLookupFailedIsAskedAgainOnTheNextPlot()
    {
        var (callout, trade, dispatched) = Build(throws: new GalaxyUnavailableException("Spansh is down."));
        var state = Commander();
        var route = Route(Home, Far);

        Assert.Empty(callout.Examine(Context(state, route, lines: Plotted)));
        await RunDispatched(dispatched);
        Assert.Empty(callout.Examine(Context(state, route)));

        trade.Throws = null;

        Assert.Empty(callout.Examine(Context(state, route, lines: Plotted)));
        await RunDispatched(dispatched);

        Assert.Equal(2, trade.Asked.Count);
        Assert.Single(callout.Examine(Context(state, route)));
    }

    [Fact]
    public void APairWithALookupStillOutIsNotAskedTwice()
    {
        var (callout, trade, dispatched) = Build();
        var state = Commander();
        var route = Route(Home, Far);

        Assert.Empty(callout.Examine(Context(state, route, lines: Plotted)));
        Assert.Empty(callout.Examine(Context(state, route, lines: Plotted)));

        Assert.Single(dispatched);
        Assert.Empty(trade.Asked);
    }

    [Fact]
    public async Task TheSavedTradeFiltersReachTheSearch()
    {
        var (callout, trade, dispatched) = Build();

        callout.Filters = search => search with
        {
            MaxPriceAge = 48,
            LargePadOnly = true,
            Planetary = true,
            MaxStationDistance = 250,
        };

        Assert.Empty(callout.Examine(Context(Commander(), Route(Home, Far), lines: Plotted)));
        await RunDispatched(dispatched);

        var asked = Assert.Single(trade.Asked);

        Assert.Equal(48, asked.MaxPriceAge);
        Assert.True(asked.LargePadOnly);
        Assert.True(asked.Planetary);
        Assert.Equal(250, asked.MaxStationDistance);
    }

    [Fact]
    public void TheGalaxySearchDisclosureNamesTheDestinationLookup()
    {
        var settings = new D47Settings { Knowledge = new KnowledgeSettings { GalaxySearch = true } };
        var trading = settings with { Callouts = settings.Callouts with { TradingMode = true } };

        var on = EgressDisclosure.Entry(EgressDisclosure.GalaxySearch, trading, llmKeyPresent: false);
        var off = EgressDisclosure.Entry(EgressDisclosure.GalaxySearch, settings, llmKeyPresent: false);

        Assert.Contains(
            "With Trading Mode on, plotting a route while docked sends the destination system's name "
            + "to spansh.co.uk; switching Trading Mode off stops it.",
            on.What);

        Assert.DoesNotContain("Trading Mode", off.What);
    }
}
