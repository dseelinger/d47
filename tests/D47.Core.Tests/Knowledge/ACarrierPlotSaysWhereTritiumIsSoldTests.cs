using D47.Core.Capabilities;
using D47.Core.Capabilities.Builtin;
using D47.Core.Configuration;
using D47.Core.Journal;
using D47.Core.Knowledge;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Knowledge;

[Trait("Category", "Integration")]
public class ACarrierPlotSaysWhereTritiumIsSoldTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 7, 12, 0, 0, TimeSpan.Zero);

    private sealed class OneJump : IRouteService
    {
        public Task<PlottedRoute?> PlotAsync(RouteQuery query, CancellationToken cancellationToken) =>
            Task.FromResult<PlottedRoute?>(null);

        public Task<RichesRoute?> PlotRichesAsync(RichesQuery query, CancellationToken cancellationToken) =>
            Task.FromResult<RichesRoute?>(null);

        public Task<ExobiologyRoute?> PlotExobiologyAsync(ExobiologyQuery query, CancellationToken cancellationToken) =>
            Task.FromResult<ExobiologyRoute?>(null);

        public Task<CarrierRoute?> PlotCarrierAsync(CarrierRouteQuery query, CancellationToken cancellationToken) =>
            Task.FromResult<CarrierRoute?>(new CarrierRoute(
                [new CarrierWaypoint("Colonia", 500, 0, 1_000, 20, false, 0, 0, false, false, true)]));
    }

    private sealed class Market(Func<CommoditySearch, CommodityAnswer> answer) : ITradePlanService
    {
        public List<CommoditySearch> Searches { get; } = [];

        public Task<TradeRoute?> PlanAsync(TradeQuery query, CancellationToken cancellationToken) =>
            Task.FromResult<TradeRoute?>(null);

        public Task<CommodityAnswer> FindCommodityAsync(CommoditySearch search, CancellationToken cancellationToken)
        {
            Searches.Add(search);

            return Task.FromResult(answer(search));
        }

        public Task<SourcingAnswer> SourceConstructionAsync(SourcingSearch search, CancellationToken cancellationToken) =>
            Task.FromResult(SourcingAnswer.Empty);

        public Task<StationQuote?> QuoteAsync(long marketId, string commodity, CancellationToken cancellationToken) =>
            Task.FromResult<StationQuote?>(null);

        public Task<BestCargoAnswer?> BestCargoAsync(BestCargoSearch search, CancellationToken cancellationToken) =>
            Task.FromResult<BestCargoAnswer?>(null);
    }

    private sealed class SilentGalaxy : IGalaxyService
    {
        public Task<GalaxySearchResult> SearchAsync(GalaxyQuery query, CancellationToken cancellationToken) =>
            Task.FromResult(new GalaxySearchResult("Colonia", 0, []));

        public Task<double?> DistanceAsync(string from, string to, CancellationToken cancellationToken) =>
            Task.FromResult<double?>(null);

        public Task<StationSearchResult> FindStationsAsync(StationQuery query, CancellationToken cancellationToken) =>
            Task.FromResult(new StationSearchResult("Colonia", 0, []));

        public Task<BodySearchResult> FindBodiesAsync(BodyQuery query, CancellationToken cancellationToken) =>
            Task.FromResult(new BodySearchResult("Colonia", 0, []));

        public Task<ColonisationScan> ScanForColonisationAsync(ColonisationQuery query, CancellationToken cancellationToken) =>
            Task.FromResult(new ColonisationScan("Colonia", 0, []));

        public Task<SystemBiology> SystemBiologyAsync(long systemAddress, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }

    private static CommodityOffer Offer(string station, string type, DateTimeOffset reported, double distance) =>
        new(
            new MarketSnapshot
            {
                Station = station,
                System = "Colonia",
                Type = type,
                UpdatedAt = reported,
                Quotes = new Dictionary<string, MarketQuote>(StringComparer.OrdinalIgnoreCase)
                {
                    ["Tritium"] = new("Tritium") { BuyPrice = 50_000, Supply = 900 },
                },
            },
            50_000,
            900,
            distance);

    private static CommodityAnswer Answer(params CommodityOffer[] offers) => new(offers, offers.Length, 0, true);

    private static async Task<string> PlotAsync(Market market, bool returnTrip)
    {
        using var install = new TempInstall();
        var gameState = new GameStateStore();

        foreach (var json in (string[])
                 [
                     """{"timestamp":"2026-09-01T00:00:00Z","event":"Commander","FID":"F1","Name":"Fixture"}""",
                     """{"timestamp":"2026-09-05T12:00:00Z","event":"CarrierStats","CarrierID":3700000000,"Callsign":"K7Q-B4X","Name":"Sacred Fire","CarrierType":"FleetCarrier","FuelLevel":0,"SpaceUsage":{"TotalCapacity":25000,"Crew":0,"Cargo":0,"CargoSpaceReserved":0,"ShipPacks":0,"ModulePacks":0,"FreeSpace":25000},"Crew":[]}""",
                 ])
        {
            Assert.True(JournalEvent.TryParse(json, NullLogger.Instance, out var parsed));
            gameState.Apply(parsed!);
        }

        var settings = TestSurface.For(install).Settings;
        settings.Apply(GalaxyCapability.EnabledKey, "true", SettingsCaller.Panel);

        var registry = CapabilityRegistry.Build(
        [
            RouteCapability.Create(
                new OneJump(),
                market,
                () => gameState.Active,
                settings,
                new RoutePlanBook(Path.Combine(install.Root, "data", "route-plans.json"), NullLogger<RoutePlanBook>.Instance),
                () => Now),
        ]);

        var result = await registry.InvokeAsync(
            "plot_carrier_route",
            new ToolArguments(new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["from"] = "Sol",
                ["to"] = "Colonia",
                ["return_trip"] = returnTrip ? "true" : "false",
            }),
            TestContext.Current.CancellationToken);

        Assert.False(result.IsError, result.Content);

        return result.Content;
    }

    [Fact]
    public async Task AStationSellerIsNamedWithTheAgeOfItsReport()
    {
        var market = new Market(_ => Answer(
            Offer("Carrier K1A-22B", "FleetCarrier", Now.AddHours(-2), 10),
            Offer("Jaques Station", "Coriolis Starport", Now.AddDays(-3), 20)));

        var said = await PlotAsync(market, returnTrip: false);

        Assert.Contains("Tritium is sold at Jaques Station (Colonia), reported 3 days ago.", said, StringComparison.Ordinal);
        Assert.DoesNotContain("round trip", said, StringComparison.Ordinal);

        var asked = Assert.Single(market.Searches);

        Assert.Equal("Colonia", asked.System);
        Assert.Equal(500, asked.Query.MaxDistance);
        Assert.True(asked.Query.IncludeCarriers);
    }

    [Fact]
    public async Task CarriersOnlyAreSaidToBeCarriersAtCarrierPrices()
    {
        var market = new Market(_ => Answer(
            Offer("Carrier A", "FleetCarrier", Now.AddDays(-1), 5),
            Offer("Carrier B", "FleetCarrier", Now.AddDays(-1), 6),
            Offer("Carrier C", "FleetCarrier", Now.AddDays(-1), 7),
            Offer("Carrier D", "FleetCarrier", Now.AddDays(-1), 8)));

        var said = await PlotAsync(market, returnTrip: true);

        Assert.Contains(
            "No station within one jump of Colonia sells tritium; 4 carriers list it, at carrier prices, and those listings move.",
            said,
            StringComparison.Ordinal);
        Assert.DoesNotContain("round trip", said, StringComparison.Ordinal);
    }

    [Fact]
    public async Task NoSupplyOnAOneWayPlotSaysToPlotARoundTrip()
    {
        var said = await PlotAsync(new Market(_ => Answer()), returnTrip: false);

        Assert.Contains("Nothing within one jump of Colonia sells tritium. Plot it as a round trip and carry the whole total.", said, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AnIncompleteAnswerClaimsNothing()
    {
        var said = await PlotAsync(new Market(_ => Answer() with { Complete = false }), returnTrip: false);

        Assert.DoesNotContain("Nothing within one jump", said, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AStationSearchWithNoStationRunsAgainWithCarriersAndSaysSo()
    {
        using var install = new TempInstall();
        var surface = TestSurface.For(install);
        surface.Settings.Apply(GalaxyCapability.EnabledKey, "true", SettingsCaller.Panel);

        var market = new Market(search => search.Query.IncludeCarriers
            ? Answer(Offer("Carrier A", "FleetCarrier", Now.AddDays(-1), 5))
            : Answer());

        var registry = CapabilityRegistry.Build(
        [
            GalaxyCapability.Create(new SilentGalaxy(), () => "Colonia", surface.Settings, market),
        ]);

        var result = await registry.InvokeAsync(
            "find_nearest_station",
            new ToolArguments(new Dictionary<string, string>(StringComparer.Ordinal) { ["commodity"] = "Tritium" }),
            TestContext.Current.CancellationToken);

        Assert.False(result.IsError, result.Content);
        Assert.Contains("carriers only, at carrier prices", result.Content, StringComparison.Ordinal);
        Assert.Contains("Carrier A", result.Content, StringComparison.Ordinal);
        Assert.Equal([false, true], market.Searches.Select(s => s.Query.IncludeCarriers));
    }

    [Fact]
    public void TheEgressTextNamesTheCarrierPlotsTritiumQuery()
    {
        using var install = new TempInstall();
        var settings = TestSurface.For(install).Settings;
        settings.Apply(GalaxyCapability.EnabledKey, "true", SettingsCaller.Panel);

        var entry = EgressDisclosure.Entry(EgressDisclosure.GalaxySearch, settings.Current, llmKeyPresent: false);

        Assert.Contains("where tritium is sold around the destination", entry.What, StringComparison.Ordinal);
    }
}
