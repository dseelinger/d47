using D47.Core.Capabilities;
using D47.Core.Capabilities.Builtin;
using D47.Core.Configuration;
using D47.Core.Journal;
using D47.Core.Knowledge;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Journal;

public class CollectMissionsSayWhereToSourceTheirCargoTests
{
    private sealed class FakeTrade : ITradePlanService
    {
        public SourcingSearch? Last { get; private set; }

        public SourcingAnswer Answer { get; set; } = SourcingAnswer.Empty;

        public Task<TradeRoute?> PlanAsync(TradeQuery query, CancellationToken cancellationToken) =>
            Task.FromResult<TradeRoute?>(null);

        public Task<CommodityAnswer> FindCommodityAsync(CommoditySearch search, CancellationToken cancellationToken) =>
            Task.FromResult(CommodityAnswer.Empty);

        public Task<SourcingAnswer> SourceConstructionAsync(SourcingSearch search, CancellationToken cancellationToken)
        {
            Last = search;

            return Task.FromResult(Answer);
        }

        public Task<StationQuote?> QuoteAsync(long marketId, string commodity, CancellationToken cancellationToken) =>
            Task.FromResult<StationQuote?>(null);

        public Task<BestCargoAnswer?> BestCargoAsync(BestCargoSearch search, CancellationToken cancellationToken) =>
            Task.FromResult<BestCargoAnswer?>(null);
    }

    private static GameStateStore Store(params string[] missions)
    {
        var store = new GameStateStore();

        string[] lines =
        [
            """{"timestamp":"2026-09-30T09:00:00Z","event":"LoadGame","FID":"F1","Commander":"Fixture"}""",
            """{"timestamp":"2026-09-30T09:01:00Z","event":"Loadout","Ship":"cobramkiii","ShipID":1,"CargoCapacity":16,"Modules":[]}""",
            """{"timestamp":"2026-09-30T09:02:00Z","event":"Location","StarSystem":"Home","StarPos":[0,0,0],"Docked":false}""",
            .. missions,
        ];

        foreach (var line in lines)
        {
            Assert.True(JournalEvent.TryParse(line, NullLogger.Instance, out var parsed));
            store.Apply(parsed!);
        }

        return store;
    }

    private static string Collect(int id, string name, string commodity, int count, string system, string station) =>
        $$"""{"timestamp":"2026-09-30T09:30:00Z","event":"MissionAccepted","Faction":"F","Name":"{{name}}","LocalisedName":"Need {{commodity}}","Commodity":"${{commodity}}_Name;","Commodity_Localised":"{{commodity}}","Count":{{count}},"DestinationSystem":"{{system}}","DestinationStation":"{{station}}","Expiry":"2026-10-30T10:00:00Z","MissionID":{{id}}}""";

    private static MarketSnapshot Market(string station, string system, double x, params (string Commodity, int Buy, int Supply)[] quotes) =>
        new()
        {
            Station = station,
            System = system,
            X = x,
            HasLargePad = true,
            UpdatedAt = DateTimeOffset.UnixEpoch,
            Quotes = quotes.ToDictionary(
                quote => quote.Commodity,
                quote => new MarketQuote(quote.Commodity) { BuyPrice = quote.Buy, Supply = quote.Supply },
                StringComparer.OrdinalIgnoreCase),
        };

    private static SettingsService Settings(MemoryInstall install, bool lookups)
    {
        var store = new SettingsStore(install.Paths, install.Files, NullLogger<SettingsStore>.Instance);
        var settings = new SettingsService(
            store,
            new SecretStore(install.Paths, new ReversibleProtector(), install.Files, NullLogger<SecretStore>.Instance),
            store.Load(),
            NullLogger<SettingsService>.Instance);

        settings.Replace(
            "the test",
            current => current with { Knowledge = current.Knowledge with { GalaxySearch = lookups } });

        return settings;
    }

    private static async Task<string> AskAsync(GameStateStore store, FakeTrade trade, bool lookups = true)
    {
        var registry = CapabilityRegistry.Build(
            [MissionsCapability.Create(() => store.Active, () => DateTimeOffset.MinValue, trade, Settings(new MemoryInstall(), lookups))]);

        var result = await registry.InvokeAsync(
            MissionsCapability.Tool,
            ToolArguments.FromJson("""{"where_to_buy":true}"""),
            TestContext.Current.CancellationToken);

        return result.Content;
    }

    [Fact]
    public async Task TheBuyStopsComeBeforeEachHandInAndAreSearchedNearestFirst()
    {
        var store = Store(
            Collect(1, "Mission_Collect", "Algae", 20, "Alpha", "Alpha Port"),
            Collect(2, "Mission_Collect_RankFed", "Algae", 10, "Beta", "Beta Hub"));

        var trade = new FakeTrade
        {
            Answer = new SourcingAnswer(
                new SourcingPlan(
                    [new SourcingStop(Market("Shop", "Alpha", 5, ("Algae", 100, 500)), [new SourcingLot("Algae", "algae", 30, 100)], 0)],
                    [],
                    new Dictionary<string, int>()),
                2,
                0,
                true,
                new Dictionary<string, (double, double, double)> { ["Alpha"] = (10, 0, 0), ["Beta"] = (40, 0, 0) }),
        };

        var said = await AskAsync(store, trade);

        Assert.Equal(SourcingRank.Nearest, trade.Last!.Rank);
        Assert.Equal("Alpha", trade.Last.System);
        Assert.Equal(["Beta"], trade.Last.AlsoAround);
        Assert.Equal(30, Assert.Single(trade.Last.Outstanding).Tonnes);
        Assert.Contains("1. Buy at Shop (Alpha), 5 ly: 30 tonnes Algae at 100 cr.", said);
        Assert.Contains("2. Hand in at Alpha Port (Alpha), 5 ly.", said);
        Assert.Contains("3. Hand in at Beta Hub (Beta), 30 ly.", said);
        Assert.Contains("The whole round is 40 ly.", said);
        Assert.Contains("At 16 tonnes a run, that is 2 full loads.", said);
    }

    [Fact]
    public async Task OnFootAndDeepSpaceCollectMissionsAreNotSourced()
    {
        var store = Store(
            Collect(1, "Mission_OnFoot_Collect", "Algae", 20, "Alpha", "Alpha Port"),
            Collect(2, "Mission_DS_Collect", "Algae", 20, "Alpha", "Alpha Port"));

        var trade = new FakeTrade();
        var said = await AskAsync(store, trade);

        Assert.Null(trade.Last);
        Assert.Contains("no collect missions", said);
    }

    [Fact]
    public async Task ASwitchedOffGalaxySearchSendsNothing()
    {
        var store = Store(Collect(1, "Mission_Collect", "Algae", 20, "Alpha", "Alpha Port"));
        var trade = new FakeTrade();
        var said = await AskAsync(store, trade, lookups: false);

        Assert.Null(trade.Last);
        Assert.Contains("switched off", said);
    }

    [Fact]
    public async Task WhatCouldNotBeFoundIsSaid()
    {
        var store = Store(Collect(1, "Mission_Collect", "Algae", 20, "Alpha", "Alpha Port"));

        var trade = new FakeTrade
        {
            Answer = new SourcingAnswer(
                new SourcingPlan([], ["Algae"], new Dictionary<string, int>()),
                0,
                0,
                true),
        };

        var said = await AskAsync(store, trade);

        Assert.Contains("Nothing in range prices: Algae.", said);
    }
}
