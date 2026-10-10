using System.Reflection;
using D47.Core.Callouts;
using D47.Core.Configuration;
using D47.Core.Journal;
using D47.Core.Knowledge;
using D47.Core.Stories;
using D47.Core.Tests.Adventures;
using Xunit;

namespace D47.Core.Tests.Callouts;

/// <summary>
/// <see cref="ShippedCallouts.Connect"/> leaves no callout of the shipped catalogue on the default of a
/// service built after it.
/// </summary>
public sealed class EveryLateServiceReachesItsCalloutTests
{
    private static readonly DateTimeOffset Start = new(3311, 1, 1, 12, 0, 0, TimeSpan.Zero);

    private sealed class FakeTrade : ITradePlanService
    {
        public Task<TradeRoute?> PlanAsync(TradeQuery query, CancellationToken cancellationToken) =>
            Task.FromResult<TradeRoute?>(null);

        public Task<CommodityAnswer> FindCommodityAsync(CommoditySearch search, CancellationToken cancellationToken) =>
            Task.FromResult(CommodityAnswer.Empty);

        public Task<SourcingAnswer> SourceConstructionAsync(SourcingSearch search, CancellationToken cancellationToken) =>
            Task.FromResult(SourcingAnswer.Empty);

        public Task<StationQuote?> QuoteAsync(long marketId, string commodity, CancellationToken cancellationToken) =>
            Task.FromResult<StationQuote?>(null);

        public Task<BestCargoAnswer?> BestCargoAsync(BestCargoSearch search, CancellationToken cancellationToken) =>
            Task.FromResult<BestCargoAnswer?>(null);
    }

    private sealed class Wired
    {
        public required CalloutEngine Engine { get; init; }

        public required SettingsService Settings { get; init; }

        public required ShippedCallouts.Services Services { get; init; }

        public bool StockCore { get; set; }

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
            ComposeRecap = before =>
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

    private static void GalaxySearch(SettingsService settings, bool on) =>
        settings.Replace("test", current => current with { Knowledge = current.Knowledge with { GalaxySearch = on } });

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
    public void TheGalaxyAndTradeServicesFollowTheGalaxySearchSwitch()
    {
        var wired = Connected();
        var missions = wired.Shipped<MissionCallout>();
        var surveyed = wired.Shipped<SurveyedBiologyCallout>();
        var trading = wired.Shipped<TradingModeCallout>();

        GalaxySearch(wired.Settings, on: true);
        Assert.Same(wired.Services.Galaxy, missions.Galaxy());
        Assert.Same(wired.Services.Galaxy, surveyed.Galaxy());
        Assert.Same(wired.Services.Trade, trading.Trade());

        GalaxySearch(wired.Settings, on: false);
        Assert.Null(missions.Galaxy());
        Assert.Null(surveyed.Galaxy());
        Assert.Null(trading.Trade());

        GalaxySearch(wired.Settings, on: true);
        Assert.Same(wired.Services.Galaxy, missions.Galaxy());
        Assert.Same(wired.Services.Galaxy, surveyed.Galaxy());
        Assert.Same(wired.Services.Trade, trading.Trade());
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
