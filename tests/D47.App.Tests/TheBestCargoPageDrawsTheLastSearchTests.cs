using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Avalonia.VisualTree;
using D47.App.Panel;
using D47.Core.Capabilities;
using D47.Core.Capabilities.Builtin;
using D47.Core.Configuration;
using D47.Core.Interface;
using D47.Core.Journal;
using D47.Core.Knowledge;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.App.Tests;

/// <summary>Navigation › Best cargo draws the last best cargo search, by voice or from the page (#849).</summary>
public class TheBestCargoPageDrawsTheLastSearchTests
{
    private static readonly DateTimeOffset AskedAt = new(2026, 10, 3, 19, 42, 0, TimeSpan.Zero);

    private static readonly BestCargoAnswer SixPicks = new(
    [
        new CargoPick("Superconductors", "Ray Gateway", 9_412, 256, CargoLimit.Hold),
        new CargoPick("Power Generators", "Ray Gateway", 4_880, 256, CargoLimit.Hold),
        new CargoPick("Robotics", "Fraser Hub", 3_210, 180, CargoLimit.Supply),
        new CargoPick("Computer Components", "Ray Gateway", 1_904, 256, CargoLimit.Hold),
        new CargoPick("Titanium", "Ross Terminal", 1_120, 212, CargoLimit.Demand),
        new CargoPick("Aluminium", "Ray Gateway", 410, 256, CargoLimit.Hold),
    ], 256);

    private sealed class FakeTrade : ITradePlanService
    {
        public BestCargoAnswer? Answer { get; set; } = SixPicks;

        public Task<TradeRoute?> PlanAsync(TradeQuery query, CancellationToken cancellationToken) =>
            Task.FromResult<TradeRoute?>(null);

        public Task<CommodityAnswer> FindCommodityAsync(CommoditySearch search, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<SourcingAnswer> SourceConstructionAsync(SourcingSearch search, CancellationToken cancellationToken) =>
            Task.FromResult(SourcingAnswer.Empty);

        public Task<StationQuote?> QuoteAsync(long marketId, string commodity, CancellationToken cancellationToken) =>
            Task.FromResult<StationQuote?>(null);

        public Task<BestCargoAnswer?> BestCargoAsync(BestCargoSearch search, CancellationToken cancellationToken) =>
            Task.FromResult(Answer);
    }

    private sealed record Opened(Window Window, PanelView Panel, BestCargoBoard Board, FakeTrade Trade, CapabilityRegistry Registry, string Folder)
        : IDisposable
    {
        public RouteBestCargoPage Page => Panel.GetVisualDescendants().OfType<RouteBestCargoPage>().Single();

        public void Dispose()
        {
            Window.Close();
            Directory.Delete(Folder, recursive: true);
        }
    }

    private static void Apply(GameStateStore gameState, string json)
    {
        Assert.True(JournalEvent.TryParse(json, NullLogger.Instance, out var parsed));
        gameState.Apply(parsed!);
    }

    private static Opened Open(
        bool docked = true,
        bool lookups = true,
        NavRoute? route = null,
        double width = 1280,
        bool trade = true)
    {
        var folder = Path.Combine(Path.GetTempPath(), "d47-best-cargo-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);

        var settings = TestSurface.Settings();
        settings.Apply(GalaxyCapability.EnabledKey, lookups ? "true" : "false", SettingsCaller.Panel);
        settings.Replace("test", current => current with
        {
            Trade = current.Trade with
            {
                LargePadOnly = true,
                MaxPriceAgeHours = 24,
                MaxStationDistance = 5_000,
            },
        });

        var gameState = new GameStateStore();
        Apply(gameState, """{"timestamp":"2026-10-03T19:00:00Z","event":"Commander","FID":"F1","Name":"Fixture"}""");
        Apply(gameState, """{"timestamp":"2026-10-03T19:00:01Z","event":"Loadout","Ship":"type9","CargoCapacity":256}""");
        Apply(
            gameState,
            docked
                ? """{"timestamp":"2026-10-03T19:00:02Z","event":"Docked","StarSystem":"LTT 7786","StationName":"Parise Gateway"}"""
                : """{"timestamp":"2026-10-03T19:00:02Z","event":"FSDJump","StarSystem":"LTT 7786"}""");

        var fake = new FakeTrade();
        var board = new BestCargoBoard();
        var plans = new RoutePlanBook(Path.Combine(folder, "route-plans.json"), NullLogger<RoutePlanBook>.Instance);

        var registry = CapabilityRegistry.Build(
        [
            RouteCapability.Create(null, fake, () => gameState.Active, settings, plans, () => AskedAt, cargo: board),
        ]);

        var panel = new PanelView { DataContext = new PanelViewModel() };

        panel.EnableRouting(new RoutingSurface(
            () => route ?? NavRoute.None,
            () => gameState.Active?.Location.StarSystem,
            registry,
            plans,
            () => settings.Current.Knowledge.GalaxySearch,
            () => { },
            new CommodityBoard(),
            Settings: settings,
            Commander: () => gameState.Active,
            Cargo: trade ? board : null));

        var window = new Window { Content = panel, Width = width, Height = 860 };
        window.Bind(Window.BackgroundProperty, Avalonia.Application.Current!.Resources.GetResourceObservable(D47.App.Theming.ThemeManager.BgKey));
        window.Show();
        Dispatcher.UIThread.RunJobs();

        panel.Tab = PanelTab.Navigation;

        if (trade)
        {
            panel.Nav.SelectRoot(RoutingPages.BestCargoRoot);
        }

        Dispatcher.UIThread.RunJobs();

        return new Opened(window, panel, board, fake, registry, folder);
    }

    private static void Search(RouteBestCargoPage page, string system)
    {
        page.SellIn = system;
        page.SearchAsync().GetAwaiter().GetResult();
        Dispatcher.UIThread.RunJobs();
    }

    private static void Save(Window window, string name)
    {
        Dispatcher.UIThread.RunJobs();

        using var frame = window.CaptureRenderedFrame()!;
        frame.Save(Path.Combine(TestSurface.CaptureDirectory, name), new PngBitmapEncoderOptions());
    }

    [AvaloniaFact]
    public void BestCargoSitsBetweenMarketAndTradeRoute()
    {
        using var opened = Open();

        var words = opened.Panel.Nav.Roots(PanelTab.Navigation).Select(root => root.Word).ToList();
        var market = words.IndexOf("Market");

        Assert.Equal(["Market", "Best cargo", "Trade route"], words.Skip(market).Take(3));
    }

    [AvaloniaFact]
    public void APanelWithoutTheBoardHasNoBestCargo()
    {
        using var opened = Open(trade: false);

        Assert.DoesNotContain(opened.Panel.Nav.Roots(PanelTab.Navigation), root => root.Key == RoutingPages.BestCargoRoot);
    }

    [AvaloniaFact]
    public void ASearchFromThePagePostsToTheBoardAndDrawsEveryPick()
    {
        using var opened = Open();

        Assert.Equal(BestCargoView.Unasked, opened.Page.View);

        Search(opened.Page, "Diaguandri");

        Assert.Equal("Diaguandri", opened.Board.Last?.Search.Destination);
        Assert.Equal(BestCargoView.Picks, opened.Page.View);
        Assert.Equal(6, opened.Page.Rows.Count);
        Assert.Equal(["HOLD", "HOLD", "SUPPLY", "HOLD", "DEMAND", "HOLD"], opened.Page.Rows.Select(row => RouteBestCargoPage.Limit(row.Limit)));
    }

    [AvaloniaFact]
    public void ASearchByVoiceDrawsOnThePage()
    {
        using var opened = Open();

        opened.Registry
            .InvokeAsync(RouteBestCargoPage.Tool, new ToolArguments(new Dictionary<string, string> { ["system"] = "Sothis" }), CancellationToken.None)
            .GetAwaiter()
            .GetResult();
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(BestCargoView.Picks, opened.Page.View);
        Assert.Equal("Sothis", opened.Page.SellIn);
    }

    [AvaloniaFact]
    public void NotDockedDisablesFindCargo()
    {
        using var opened = Open(docked: false);

        Assert.Equal(BestCargoView.NotDocked, opened.Page.View);
        Assert.False(opened.Page.CanFind);
    }

    [AvaloniaFact]
    public void LookupsOffDisablesFindCargo()
    {
        using var opened = Open(lookups: false);

        Assert.Equal(BestCargoView.LookupsOff, opened.Page.View);
        Assert.False(opened.Page.CanFind);
    }

    [AvaloniaFact]
    public void ANullAnswerSaysTheMarketsPricesAreUnknown()
    {
        using var opened = Open();
        opened.Trade.Answer = null;

        Search(opened.Page, "Diaguandri");

        Assert.Equal(BestCargoView.MarketUnseen, opened.Page.View);
        Assert.Empty(opened.Page.Rows);
    }

    [AvaloniaFact]
    public void AnEmptyAnswerSaysNothingSellsAtAProfit()
    {
        using var opened = Open();
        opened.Trade.Answer = new BestCargoAnswer([], 256);

        Search(opened.Page, "Shinrarta Dezhra");

        Assert.Equal(BestCargoView.NoProfit, opened.Page.View);
        Assert.Empty(opened.Page.Rows);
    }

    [AvaloniaFact]
    public void RouteEndIsAbsentWithNoRoutePlotted()
    {
        using var opened = Open();

        Assert.Null(opened.Page.RouteEnd);
        Assert.DoesNotContain(
            opened.Page.GetVisualDescendants().OfType<Button>(),
            button => (button.Content as string)?.StartsWith("Route end", StringComparison.Ordinal) == true);
    }

    [AvaloniaFact]
    public void RouteEndFillsTheFieldWithTheLastHop()
    {
        using var opened = Open(route: new NavRoute { Hops = [new RouteHop("LTT 7786", "M"), new RouteHop("Diaguandri", "K")] });

        Assert.Equal("Diaguandri", opened.Page.RouteEnd);

        opened.Page.UseRouteEnd();

        Assert.Equal("Diaguandri", opened.Page.SellIn);
    }

    [AvaloniaFact]
    public void TheFiltersLineNamesTheSavedTradeFilters()
    {
        var trade = new TradeSettings { LargePadOnly = true, MaxPriceAgeHours = 24, MaxStationDistance = 5_000 };

        Assert.Equal(
            ["large pad only", "no surface ports", "prices under 24 h", "stations within 5,000 ls"],
            RouteBestCargoPage.Filters(trade));
    }

    /// <summary>Captures 01 and 03–06 of the handoff, 01 in all four themes, for a look by eye.</summary>
    [AvaloniaFact]
    public void EachStateIsCaptured()
    {
        foreach (var theme in new[] { ThemeCatalog.Elite, ThemeCatalog.Dark, ThemeCatalog.Light })
        {
            using var look = AppLook.Put(theme);
            using var picks = Open(route: new NavRoute { Hops = [new RouteHop("LTT 7786", "M"), new RouteHop("Diaguandri", "K")] });

            Search(picks.Page, "Diaguandri");
            Save(picks.Window, $"best-cargo-01-picks-{theme}.png");
        }

        using var elite = AppLook.Put();

        using (var notDocked = Open(docked: false))
        {
            Save(notDocked.Window, "best-cargo-03-not-docked.png");
        }

        using (var off = Open(lookups: false))
        {
            off.Page.SellIn = "Diaguandri";
            Save(off.Window, "best-cargo-04-lookups-off.png");
        }

        using (var unseen = Open())
        {
            unseen.Trade.Answer = null;
            Search(unseen.Page, "Diaguandri");
            Save(unseen.Window, "best-cargo-05-market-unseen.png");
        }

        using (var none = Open())
        {
            none.Trade.Answer = new BestCargoAnswer([], 256);
            Search(none.Page, "Shinrarta Dezhra");
            Save(none.Window, "best-cargo-06-no-profit.png");
        }
    }
}
