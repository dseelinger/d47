using Avalonia.Headless;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using D47.App.Panel;
using D47.App.Theming;
using D47.Core.Capabilities;
using D47.Core.Capabilities.Builtin;
using D47.Core.Checklists;
using D47.Core.Configuration;
using D47.Core.Interface;
using D47.Core.Journal;
using D47.Core.Knowledge;
using D47.Core.Ships;
using D47.Core.Storage;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.App.Tests;

/// <summary>The To buy block on a ship page plots an outfitting route and opens it on Navigation › Plan (#1827).</summary>
[Trait("Category", "Integration")]
[Collection(nameof(ShipArtCollection))]
public class AShipPageRoutesToBuyWhatItsPlanStillNeedsTests
{
    private sealed class Outfitters : IGalaxyService
    {
        public List<(IReadOnlyList<OutfittingItem> Items, string Near)> Asked { get; } = [];

        public Task<IReadOnlyList<OutfitterStation>> FindOutfittersAsync(
            IReadOnlyList<OutfittingItem> items, string referenceSystem, double maxDistance, PadSize? minPad, CancellationToken cancellationToken)
        {
            Asked.Add((items, referenceSystem));

            Assert.True(OutfittingItem.TryParse("6A Fuel Scoop", out var scoop, out _));

            return Task.FromResult<IReadOnlyList<OutfitterStation>>(
            [
                new OutfitterStation(
                    new StationSummary { Name = "Ackerman Market", SystemName = "Eravate", MarketId = 1, Distance = 12.4 },
                    [scoop]),
            ]);
        }

        public Task<GalaxySearchResult> SearchAsync(GalaxyQuery query, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<double?> DistanceAsync(string from, string to, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<StationSearchResult> FindStationsAsync(StationQuery query, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<BodySearchResult> FindBodiesAsync(BodyQuery query, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<ColonisationScan> ScanForColonisationAsync(ColonisationQuery query, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<MissionStationScan> FindMissionStationsAsync(MissionStationQuery query, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<IReadOnlyList<BodySummary>> SystemBodiesAsync(long systemAddress, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<SystemBiology> SystemBiologyAsync(long systemAddress, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<PowerplayNeighbourhood> PowerplayNearAsync(string system, double lightYears, CancellationToken cancellationToken) => throw new NotSupportedException();
    }

    private sealed record Bench(
        ItemPage Page, PanelNavigator Nav, Outfitters Galaxy, RoutePlanBook Plans, string BuildId, ShipsMode Mode);

    private static Bench Set(bool lookups, bool twoScoops = false)
    {
        var folder = TempFolders.Create("d47-ship-to-buy");
        var settings = TestSurface.Settings();
        settings.Apply(GalaxyCapability.EnabledKey, lookups ? "true" : "false", SettingsCaller.Panel);

        var game = new GameStateStore();

        foreach (var json in (string[])
                 [
                     """{"timestamp":"2026-10-28T10:00:00Z","event":"Commander","FID":"F1","Name":"Fixture"}""",
                     """{"timestamp":"2026-10-28T10:00:01Z","event":"FSDJump","StarSystem":"LHS 3447","StarPos":[0,0,0]}""",
                 ])
        {
            Assert.True(JournalEvent.TryParse(json, NullLogger.Instance, out var parsed));
            game.Apply(parsed!);
        }

        var galaxy = new Outfitters();
        var plans = new RoutePlanBook(Path.Combine(folder, "route-plans.json"), new MemoryFileSystem(), NullLogger<RoutePlanBook>.Instance);

        var registry = CapabilityRegistry.Build(
            [RouteCapability.Create(null, null, () => game.Active, settings, plans, galaxy: galaxy)]);

        var checklists = new ChecklistService(
            new ChecklistStore(Path.Combine(folder, "checklist.json"), new MemoryFileSystem(), NullLogger<ChecklistStore>.Instance),
            new ChecklistProposalStore(
                Path.Combine(folder, "checklist-proposals.json"), new MemoryFileSystem(), NullLogger<ChecklistProposalStore>.Instance),
            () => game.Active);

        var ships = new ShipPlanService(
            new ShipBuildStore(Path.Combine(folder, "ships.json"), new MemoryFileSystem(), NullLogger<ShipBuildStore>.Instance),
            checklists,
            () => game.Active);

        var build = ships.Intend("Python")!;
        ships.Plan(build.Id, new SlotPlan("Slot01_Size6", Module: "Fuel Scoop") { Variant = "int_fuelscoop_size6_class5" });

        if (twoScoops)
        {
            ships.Plan(build.Id, new SlotPlan("Slot02_Size6", Module: "Fuel Scoop") { Variant = "int_fuelscoop_size6_class5" });
        }

        var nav = new PanelNavigator();
        nav.Register(PanelTab.Assets, new NavCrumb(LoadoutPages.FleetRoot, "Ships"));
        nav.Register(PanelTab.Navigation, new NavCrumb(RoutingPages.PlanRoot, "Plan"));

        var plotter = new OutfittingPlotter(registry, plans, () => lookups, () => game.Active?.Location.StarSystem);
        var mode = new ShipsMode(ships, checklists, () => game.Active, outfitting: plotter);

        return new Bench(new ItemPage(mode, nav, build.Id), nav, galaxy, plans, build.Id, mode);
    }

    private static Button Plot(Control root) =>
        root.GetVisualDescendants().OfType<Button>().Single(button => button.Name == "PlotToBuy");

    private static Window Show(Control content)
    {
        new ThemeManager(Avalonia.Application.Current!, NullLogger<ThemeManager>.Instance).Apply(ThemeCatalog.Elite);

        var window = new Window
        {
            Content = content,
            Width = 1100,
            Height = 900,
            Background = (Avalonia.Media.IBrush)Avalonia.Application.Current!.Resources[ThemeManager.BgKey]!,
        };

        window.Show();
        Dispatcher.UIThread.RunJobs();

        return window;
    }

    [AvaloniaFact]
    public async Task PressingPlotAsksForTheListedItemsAndOpensTheRouteOnThePlanPage()
    {
        using var kit = AppLook.ControlKit();
        var bench = Set(lookups: true);
        Show(bench.Page);

        var toBuy = bench.Mode.ToBuy(bench.BuildId)!;

        Assert.StartsWith("2 items to buy: Python and 6A Fuel Scoop.", toBuy.Summary, StringComparison.Ordinal);
        Assert.Contains("within 50 ly", toBuy.Hint, StringComparison.Ordinal);
        Assert.True(Plot(bench.Page).IsEnabled);

        Plot(bench.Page).RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));

        for (var wait = 0; wait < 200 && bench.Nav.Tab != PanelTab.Navigation; wait++)
        {
            Dispatcher.UIThread.RunJobs();
            await Task.Delay(10, TestContext.Current.CancellationToken);
        }

        var asked = Assert.Single(bench.Galaxy.Asked);

        Assert.Equal("LHS 3447", asked.Near);
        Assert.Equal(["Python", "6A Fuel Scoop"], asked.Items.Select(item => item.Display));
        Assert.Equal(PanelTab.Navigation, bench.Nav.Tab);
        Assert.Equal(RoutingPages.PlanRoot, bench.Nav.RootKeyOf(PanelTab.Navigation));
        Assert.Equal(
            [RoutingPages.PlanRoot, RoutingPages.ResultPrefix + RoutePlanKind.Outfitting],
            bench.Nav.TrailOf(PanelTab.Navigation).Select(crumb => crumb.Key));
        Assert.False(bench.Plans.Last(RoutePlanKind.Outfitting)!.Outfitting!.StorageChecked);
    }

    [AvaloniaFact]
    public async Task ARepeatedItemIsCountedOnceInTheSummaryAndAskedForOnce()
    {
        using var kit = AppLook.ControlKit();
        var bench = Set(lookups: true, twoScoops: true);
        Show(bench.Page);

        Assert.Equal("3 items to buy: Python and 2 × 6A Fuel Scoop.", bench.Mode.ToBuy(bench.BuildId)!.Summary);

        Plot(bench.Page).RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));

        for (var wait = 0; wait < 200 && bench.Nav.Tab != PanelTab.Navigation; wait++)
        {
            Dispatcher.UIThread.RunJobs();
            await Task.Delay(10, TestContext.Current.CancellationToken);
        }

        Assert.Equal(["Python", "6A Fuel Scoop"], Assert.Single(bench.Galaxy.Asked).Items.Select(item => item.Display));
    }

    [AvaloniaFact]
    public void WithLookupsOffTheButtonIsDisabledAndTheReasonIsShown()
    {
        using var kit = AppLook.ControlKit();
        var bench = Set(lookups: false);
        var window = Show(bench.Page);

        using (var frame = window.CaptureRenderedFrame()!)
        {
            frame.SaveCapture("to-buy-lookups-off.png");
        }

        Assert.False(Plot(bench.Page).IsEnabled);
        Assert.Equal(OutfittingPlotter.SwitchedOff, ToolTip.GetTip(Plot(bench.Page)));
        Assert.Contains(
            bench.Page.GetVisualDescendants().OfType<TextBlock>(),
            block => block.Text == OutfittingPlotter.SwitchedOff);
    }

    [AvaloniaFact]
    public void TheOutfittingResultDrawsStopsANoticeAndWhatWasNotFound()
    {
        using var kit = AppLook.ControlKit();

        Assert.True(OutfittingItem.TryParse("6A Fuel Scoop", out _, out _));

        var route = new OutfittingRoute(
            [new OutfittingStop("Eravate", "Ackerman Market", 12.4, ["6A Fuel Scoop", "5A Thrusters"])],
            ["1H Guardian FSD Booster"])
        {
            Reference = "LHS 3447",
            MaxDistance = 50,
            StorageChecked = false,
        };

        var book = new RoutePlanBook("plans.json", new MemoryFileSystem(), NullLogger<RoutePlanBook>.Instance);
        book.Record(route, "1 stop for 2 of 3 items", new DateTimeOffset(2026, 10, 28, 19, 42, 0, TimeSpan.Zero));

        var page = new RoutePlanResultPage(book.Last(RoutePlanKind.Outfitting)!, plans: book);
        var window = Show(page);

        var texts = page.GetVisualDescendants().OfType<TextBlock>().Select(block => block.Text ?? string.Empty).ToList();

        Assert.Contains(texts, text => text.StartsWith("1 stop for 2 of 3 items, 12 ly", StringComparison.Ordinal));
        Assert.Contains("Ackerman Market in Eravate", texts);
        Assert.Contains("buy 6A Fuel Scoop", texts);
        Assert.Contains("buy 5A Thrusters", texts);
        Assert.Contains("1H Guardian FSD Booster", texts);
        Assert.Contains(texts, text => text.StartsWith("Stored modules were not counted", StringComparison.Ordinal));
        Assert.Contains(texts, text => text == "No station within 50 ly of LHS 3447 sells these.");

        using var frame = window.CaptureRenderedFrame()!;
        frame.SaveCapture("outfitting-result.png");
    }

    [Fact]
    public void EveryItemNameSurvivesBeingJoinedWithCommasAndReadBack()
    {
        foreach (var item in OutfittingItem.All)
        {
            Assert.DoesNotContain(',', item.Display);
            Assert.True(OutfittingItem.TryParse(item.Display, out var again, out var failure), failure);
            Assert.Equal(item, again);
        }
    }
}
