using System.Reflection;
using D47.Core.Callouts;
using D47.Core.Configuration;
using D47.Core.Journal;
using D47.Core.Knowledge;
using D47.Core.Stories;
using D47.Core.Tests.Adventures;
using D47.Core.Tests.Knowledge;
using Xunit;

namespace D47.Core.Tests.Callouts;

/// <summary>
/// <see cref="ShippedCallouts.Connect"/> leaves no callout of the shipped catalogue on the default of a
/// service built after it.
/// </summary>
public sealed class EveryLateServiceReachesItsCalloutTests
{
    private static readonly DateTimeOffset Start = new(3311, 1, 1, 12, 0, 0, TimeSpan.Zero);

    private sealed class FakeTrade : StubTradePlanService
    {
        public override Task<TradeRoute?> PlanAsync(TradeQuery query, CancellationToken cancellationToken) =>
            Task.FromResult<TradeRoute?>(null);

        public override Task<CommodityAnswer> FindCommodityAsync(CommoditySearch search, CancellationToken cancellationToken) =>
            Task.FromResult(CommodityAnswer.Empty);

        public override Task<SourcingAnswer> SourceConstructionAsync(SourcingSearch search, CancellationToken cancellationToken) =>
            Task.FromResult(SourcingAnswer.Empty);

        public override Task<StationQuote?> QuoteAsync(long marketId, string commodity, CancellationToken cancellationToken) =>
            Task.FromResult<StationQuote?>(null);

        public override Task<BestCargoAnswer?> BestCargoAsync(BestCargoSearch search, CancellationToken cancellationToken) =>
            Task.FromResult<BestCargoAnswer?>(null);
    }

    private sealed class Wired
    {
        public required CalloutEngine Engine { get; init; }

        public required SettingsService Settings { get; init; }

        public required ShippedCallouts.Services Services { get; init; }

        public bool StockCore { get; set; }

        public DateTimeOffset? BgsTick { get; set; }

        public IReadOnlyList<GalNetArticle> GalNetArrived { get; set; } = [];

        public GameServerState? ServerStatusAnnounce { get; set; }

        public IReadOnlyList<D47.Core.Watches.WatchNotice> WatchesReady { get; set; } = [];

        public List<DateTimeOffset> RecapAsked { get; } = [];

        public T Shipped<T>() => Engine.Callouts.OfType<T>().Single();
    }

    private static Wired Connected(string? recap = null)
    {
        var (engine, settings) = ShippedCatalogue.Build();
        Wired? wired = null;

        var services = new ShippedCallouts.Services
        {
            Settings = settings,
            Galaxy = new AdventureGeneratorTests.Galaxy(),
            Trade = new FakeTrade(),
            StoryRunning = () => true,
            StoryAsides = new StoryMissionAsides(),
            StockCoreAboard = () => wired!.StockCore,
            BgsLastTick = () => wired!.BgsTick,
            GalNetArrived = () => wired!.GalNetArrived,
            ServerStatusAnnounce = () => wired!.ServerStatusAnnounce,
            WatchesReady = () => wired!.WatchesReady,
            ComposeRecap = (before, _) =>
            {
                wired!.RecapAsked.Add(before);
                return recap;
            },
            Dispatch = work => work(),
        };

        wired = new Wired { Engine = engine, Settings = settings, Services = services };
        ShippedCallouts.Connect(engine, services);

        return wired;
    }

    [Fact]
    public void EveryCalloutThatAsksAfterAStockCoreIsToldWhetherOneIsAboard()
    {
        var wired = Connected();

        var asking = wired.Engine.Callouts
            .Select(callout => (Callout: callout, Property: callout.GetType().GetProperty("StockCoreAboard", BindingFlags.Public | BindingFlags.Instance)))
            .Where(pair => pair.Property is { } property
                && property.PropertyType == typeof(Func<bool>)
                && property.GetSetMethod() is not null)
            .ToList();

        Assert.Superset(
            new HashSet<Type> { typeof(AmbientCallout), typeof(ContinuityCallout), typeof(RecapCallout), typeof(SessionCallout), typeof(NarratorCallout) },
            asking.Select(pair => pair.Callout.GetType()).ToHashSet());

        foreach (var (callout, property) in asking)
        {
            var aboard = (Func<bool>)property!.GetValue(callout)!;

            wired.StockCore = true;
            Assert.True(aboard(), callout.Id);

            wired.StockCore = false;
            Assert.False(aboard(), callout.Id);
        }
    }

    [Fact]
    public void TheGalaxyAndTradeServicesReachTheirCallouts()
    {
        var wired = Connected();

        Assert.Same(wired.Services.Galaxy, wired.Shipped<MissionCallout>().Galaxy());
        Assert.Same(wired.Services.Galaxy, wired.Shipped<SurveyedBiologyCallout>().Galaxy());
        Assert.Same(wired.Services.Trade, wired.Shipped<TradingModeCallout>().Trade());
    }

    [Fact]
    public void TheBgsTickReachesItsCallout()
    {
        var wired = Connected();
        wired.BgsTick = Start;

        Assert.Equal(Start, wired.Shipped<BgsTickCallout>().LastTick());
    }

    [Fact]
    public void GalNetHeadlinesReachTheirCallout()
    {
        var wired = Connected();
        wired.GalNetArrived = [new GalNetArticle("a", "Title", ["Body."], Start, "3311")];

        Assert.Single(wired.Shipped<GalNetCallout>().Arrived());
    }

    [Fact]
    public void ServerStatusLinesReachTheirCallout()
    {
        var wired = Connected();
        wired.ServerStatusAnnounce = GameServerState.Maintenance;

        Assert.Equal(GameServerState.Maintenance, wired.Shipped<ServerStatusCallout>().Announce());
    }

    [Fact]
    public void WatchLinesReachTheirCallout()
    {
        var wired = Connected();
        wired.WatchesReady = [new D47.Core.Watches.WatchNotice(Guid.NewGuid(), "Gold: 1 in stock.")];

        Assert.Single(wired.Shipped<WatchCallout>().Ready());
    }

    [Fact]
    public void CastReactionsFollowTheirSwitch()
    {
        var wired = Connected();
        var reaction = wired.Shipped<StoryReactionCallout>();

        Assert.True(reaction.Enabled());

        wired.Settings.Replace("test", current => current with { Callouts = current.Callouts with { CastReactions = false } });

        Assert.False(reaction.Enabled());
    }

    [Fact]
    public void TheNarratorAndMissionsAreGivenTheStory()
    {
        var wired = Connected();
        var narrator = wired.Shipped<NarratorCallout>();

        Assert.Same(wired.Services.StoryRunning, narrator.StoryRunning);
        Assert.Same(wired.Services.StoryAsides, narrator.StoryAsides);
        Assert.Same(wired.Services.StoryAsides, wired.Shipped<MissionCallout>().StoryAsides);
    }

    [Fact]
    public void TheRecapSaysTheLineComposedForTheSessionBeforeThisOne()
    {
        const string Line = "Last session you finished docked at Garay Terminal, Deciat in the Python.";
        var wired = Connected(Line);
        var recap = wired.Shipped<RecapCallout>();

        Assert.Empty(recap.Examine(Tick(Start)));
        Assert.Equal([Start], wired.RecapAsked);

        var said = Assert.Single(recap.Examine(Tick(Start + recap.Settle)));
        Assert.Equal(RecapCallout.Key, said.Key);
        Assert.Equal(Line, said.Text);
    }

    private static CalloutContext Tick(DateTimeOffset now) =>
        new(now, false, null, GameStatus.Unknown, NavRoute.None, []);
}
