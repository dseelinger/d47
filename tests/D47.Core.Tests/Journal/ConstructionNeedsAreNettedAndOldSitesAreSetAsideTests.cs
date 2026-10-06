using D47.Core.Capabilities;
using D47.Core.Capabilities.Builtin;
using D47.Core.Configuration;
using D47.Core.Journal;
using D47.Core.Knowledge;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Journal;

/// <summary>
/// A site is current for 14 days after it was last seen, and what it needs is netted against the hold
/// and a reconciled carrier ledger (#800).
/// </summary>
public class ConstructionNeedsAreNettedAndOldSitesAreSetAsideTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 30, 12, 0, 0, TimeSpan.Zero);

    private const string Sign = "BNH-T2F";
    private const long CarrierId = 3715429376;

    private static JournalEvent Event(string json) =>
        JournalEvent.TryParse(json, NullLogger.Instance, out var parsed)
            ? parsed!
            : throw new InvalidOperationException("Fixture JSON did not parse.");

    private static string Depot(long marketId, DateTimeOffset at, string symbol, int required, int provided) =>
        $$"""{"timestamp":"{{at:yyyy-MM-ddTHH:mm:ssZ}}","event":"ColonisationConstructionDepot","MarketID":{{marketId}},"ConstructionProgress":0.2,"ConstructionComplete":false,"ConstructionFailed":false,"ResourcesRequired":[{"Name":"${{symbol}}_name;","Name_Localised":"{{symbol}}","RequiredAmount":{{required}},"ProvidedAmount":{{provided}},"Payment":100}]}""";

    private static ColonisationSites Sites(params (long Id, int DaysAgo, string Symbol, int Required)[] sites)
    {
        var known = ColonisationSites.Empty;

        foreach (var (id, daysAgo, symbol, required) in sites)
        {
            known = known.Apply(Event(Depot(id, Now.AddDays(-daysAgo), symbol, required, 0)), $"System {id}", $"Site {id}");
        }

        return known;
    }

    private static CargoHold Hold(string symbol, int tonnes) => new()
    {
        Vessel = "Ship",
        Items = [new CargoItem(symbol, tonnes)],
        Count = tonnes,
        ReadAt = Now,
    };

    private static CarrierState Carrier(int steel, int cargoTotal) => CarrierState.None
        .Apply(Event($$"""{"timestamp":"2026-09-29T10:00:00Z","event":"CarrierBuy","CarrierID":{{CarrierId}},"Callsign":"{{Sign}}","Location":"Meene"}"""))
        .Apply(Event($$"""{"timestamp":"2026-09-29T10:01:00Z","event":"Docked","StationName":"{{Sign}}","StationType":"FleetCarrier","MarketID":{{CarrierId}}}"""))
        .Apply(Event($$"""{"timestamp":"2026-09-29T10:02:00Z","event":"CargoTransfer","Transfers":[{"Type":"steel","Count":{{steel}},"Direction":"tocarrier"}]}"""))
        .Apply(Event($$$"""{"timestamp":"2026-09-29T10:03:00Z","event":"CarrierStats","CarrierID":{{{CarrierId}}},"Callsign":"{{{Sign}}}","Name":"Sacred Fire","SpaceUsage":{"TotalCapacity":25000,"Cargo":{{{cargoTotal}}},"FreeSpace":{{{25000 - cargoTotal}}}}}"""));

    [Fact]
    public void ASiteSeenThirteenDaysAgoIsCurrentAndOneSeenFifteenDaysAgoIsNot()
    {
        var sites = Sites((1, 13, "steel", 100), (2, 15, "steel", 100));

        Assert.Equal([1L], sites.Current(Now).Select(site => site.MarketId));
        Assert.Equal([2L], sites.NotSeenSince(Now).Select(site => site.MarketId));
    }

    [Fact]
    public void AReconciledLedgerAndTheHoldAreTakenOffWhatIsToBuy()
    {
        var site = Sites((1, 1, "steel", 1000)).ById(1)!;

        var need = Assert.Single(ConstructionNeeds.For(site, Hold("steel", 300), Carrier(500, 500)));

        Assert.Equal(1000, need.Remaining);
        Assert.Equal(300, need.InHold);
        Assert.Equal(500, need.OnCarrier);
        Assert.Equal(200, need.ToBuy);
    }

    [Fact]
    public void AnUnreconciledLedgerIsNotCountedAtAll()
    {
        var site = Sites((1, 1, "steel", 1000)).ById(1)!;

        var need = Assert.Single(ConstructionNeeds.For(site, Hold("steel", 300), Carrier(500, 900)));

        Assert.Null(need.OnCarrier);
        Assert.Equal(700, need.ToBuy);
    }

    [Fact]
    public void WhatIsAlreadyEnoughLeavesNothingToBuy()
    {
        var site = Sites((1, 1, "steel", 400)).ById(1)!;

        var need = Assert.Single(ConstructionNeeds.For(site, Hold("steel", 300), Carrier(500, 500)));

        Assert.Equal(0, need.ToBuy);
    }

    private static GameStateStore Store(ColonisationSites sites)
    {
        var gameState = new GameStateStore();
        gameState.Apply(Event("""{"timestamp":"2026-09-01T09:00:00Z","event":"Commander","FID":"F1","Name":"Fixture"}"""));
        gameState.Active!.Colonisation = sites;

        return gameState;
    }

    private static Task<ToolResult> Ask(GameStateStore gameState, string tool, string json = "{}") =>
        CapabilityRegistry
            .Build([ColonisationCapability.Create(() => gameState.Active, now: () => Now)])
            .InvokeAsync(tool, ToolArguments.FromJson(json), TestContext.Current.CancellationToken);

    [Fact]
    public async Task WithOneCurrentSiteAndFourOldOnesNeedsAreAboutTheCurrentOne()
    {
        var gameState = Store(Sites(
            (1, 2, "steel", 100),
            (2, 40, "steel", 100),
            (3, 60, "steel", 100),
            (4, 90, "steel", 100),
            (5, 120, "steel", 100)));

        var result = await Ask(gameState, "get_construction_needs");

        Assert.Contains("Site 1", result.Content, StringComparison.Ordinal);
        Assert.DoesNotContain("Site 2", result.Content, StringComparison.Ordinal);
    }

    [Fact]
    public async Task WithNoCurrentSiteTheNewestOldOneIsNamedWithItsDate()
    {
        var gameState = Store(Sites((2, 40, "steel", 100), (3, 60, "steel", 100)));

        var result = await Ask(gameState, "get_construction_needs");

        Assert.Contains("No construction site is current", result.Content, StringComparison.Ordinal);
        Assert.Contains("Site 2", result.Content, StringComparison.Ordinal);
        Assert.DoesNotContain("Site 3", result.Content, StringComparison.Ordinal);
    }

    [Fact]
    public async Task TheSiteListSaysHowManyAreOldAndWhenTheNewestWasSeen()
    {
        var gameState = Store(Sites((1, 2, "steel", 100), (2, 40, "steel", 100), (3, 60, "steel", 100)));

        var result = await Ask(gameState, "get_construction_sites");

        Assert.Contains("Site 1", result.Content, StringComparison.Ordinal);
        Assert.DoesNotContain("Site 2", result.Content, StringComparison.Ordinal);
        Assert.Contains("2 more not seen in over 14 days", result.Content, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ASharedCommodityIsSaidToBeSharedWithTheOtherCurrentSite()
    {
        var gameState = Store(Sites((1, 2, "steel", 100), (2, 3, "steel", 100)));
        gameState.Active!.Hold = Hold("steel", 50);

        var result = await Ask(gameState, "get_construction_needs", """{"site":"Site 1"}""");

        Assert.Contains("shared with Site 2", result.Content, StringComparison.Ordinal);
    }

    private sealed class FakeTrade : ITradePlanService
    {
        public SourcingSearch? Last { get; private set; }

        public Task<TradeRoute?> PlanAsync(TradeQuery query, CancellationToken cancellationToken) =>
            Task.FromResult<TradeRoute?>(null);

        public Task<CommodityAnswer> FindCommodityAsync(CommoditySearch search, CancellationToken cancellationToken) =>
            Task.FromResult(CommodityAnswer.Empty);

        public Task<SourcingAnswer> SourceConstructionAsync(SourcingSearch search, CancellationToken cancellationToken)
        {
            Last = search;

            return Task.FromResult(SourcingAnswer.Empty);
        }

        public Task<StationQuote?> QuoteAsync(long marketId, string commodity, CancellationToken cancellationToken) =>
            Task.FromResult<StationQuote?>(null);

        public Task<BestCargoAnswer?> BestCargoAsync(BestCargoSearch search, CancellationToken cancellationToken) =>
            Task.FromResult<BestCargoAnswer?>(null);
    }

    [Fact]
    public async Task TheSourcingSearchAsksForWhatIsLeftToBuy()
    {
        using var install = new TempInstall();

        var gameState = Store(Sites((1, 1, "steel", 1000)));
        gameState.Active!.Hold = Hold("steel", 300);
        gameState.Active!.Carrier = Carrier(500, 500);
        gameState.Apply(Event(
            """{"timestamp":"2026-09-29T11:00:00Z","event":"Location","StarSystem":"Ratraii","StarPos":[0,0,0],"Docked":false}"""));

        var trade = new FakeTrade();
        var store = new SettingsStore(install.Paths, NullLogger<SettingsStore>.Instance);

        var settings = new SettingsService(
            store,
            new SecretStore(install.Paths, new ReversibleProtector(), NullLogger<SecretStore>.Instance),
            store.Load(),
            NullLogger<SettingsService>.Instance);

        settings.Replace(
            "the test",
            current => current with { Knowledge = current.Knowledge with { GalaxySearch = true } });

        var registry = CapabilityRegistry.Build(
            [ColonisationCapability.Create(() => gameState.Active, null, settings, trade, null, () => Now)]);

        await registry.InvokeAsync(
            "get_construction_needs",
            ToolArguments.FromJson("""{"where_to_buy":true}"""),
            TestContext.Current.CancellationToken);

        var asked = Assert.Single(trade.Last!.Outstanding);

        Assert.Equal("steel", asked.Symbol);
        Assert.Equal(200, asked.Remaining);
    }
}
