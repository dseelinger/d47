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

        public List<int?> Ages { get; } = [];

        public Task<IReadOnlyList<OutfitterStation>> FindOutfittersAsync(
            IReadOnlyList<OutfittingItem> items, string referenceSystem, double maxDistance, PadSize? minPad, int? maxReportAgeHours, CancellationToken cancellationToken)
        {
            Asked.Add((items, referenceSystem));
            Ages.Add(maxReportAgeHours);

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
        ItemPage Page, PanelNavigator Nav, Outfitters Galaxy, RoutePlanBook Plans, string BuildId, ShipsMode Mode, OutfittingPlotter Plotter);

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

        return new Bench(new ItemPage(mode, nav, build.Id), nav, galaxy, plans, build.Id, mode, plotter);
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
        Assert.Equal([72], bench.Galaxy.Ages);
        Assert.Equal(72, bench.Plans.Last(RoutePlanKind.Outfitting)!.Outfitting!.MaxReportAgeHours);
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

    private static OutfittingRoute SeedRoute(int? age) => new(
        [new OutfittingStop("Eravate", "Ackerman Market", 12.4, ["6A Fuel Scoop"])
        {
            StockLastSeen = new DateTimeOffset(2026, 10, 12, 8, 0, 0, TimeSpan.Zero),
        }],
        ["1H Guardian FSD Booster"])
    {
        Reference = "LHS 3447",
        MaxDistance = 50,
        MaxReportAgeHours = age,
        StorageChecked = true,
    };

    private static List<string> Texts(Control root) =>
        [.. root.GetVisualDescendants().OfType<TextBlock>().Select(block => block.Text ?? string.Empty)];

    private static string PickerValue(Control root) =>
        string.Concat(root.GetVisualDescendants().OfType<D47.App.Controls.InlinePicker>().Single()
            .GetVisualDescendants().OfType<TextBlock>().Single(block => block.Name == "InlinePickerValue")
            .Inlines!.OfType<Avalonia.Controls.Documents.Run>().Select(run => run.Text));

    private static Button? SearchButton(Control root) =>
        root.GetVisualDescendants().OfType<Button>().SingleOrDefault(button => button.Content as string == "Search");

    private static void Click(Button button) => button.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));

    private static RoutePlanResultPage ResultPage(Bench bench) =>
        new(bench.Plans.Last(RoutePlanKind.Outfitting)!, plans: bench.Plans, outfittingPlotter: bench.Plotter);

    [AvaloniaFact]
    public void TheOutfittingResultShowsItsAgeASearchButtonAndEachStopsReportDate()
    {
        using var kit = AppLook.ControlKit();
        var bench = Set(lookups: true);
        bench.Plans.Record(SeedRoute(72), "1 stop for 1 of 2 items", new DateTimeOffset(2026, 10, 28, 19, 42, 0, TimeSpan.Zero));

        var page = ResultPage(bench);
        var window = Show(page);

        using (var frame = window.CaptureRenderedFrame()!)
        {
            frame.SaveCapture("outfitting-result-with-search.png");
        }

        var texts = Texts(page);

        Assert.NotNull(SearchButton(page));
        Assert.Equal("3 days", PickerValue(page));
        Assert.Contains("outfitting reported 12 Oct 2026", texts);
        Assert.Contains("No station within 50 ly of LHS 3447 reported in the last 3 days sells these.", texts);
    }

    [AvaloniaFact]
    public void AStopWithNoReportDateSaysSo()
    {
        using var kit = AppLook.ControlKit();
        var bench = Set(lookups: true);
        var route = new OutfittingRoute([new OutfittingStop("Eravate", "Ackerman Market", 12.4, ["6A Fuel Scoop"])], []);
        bench.Plans.Record(route, "1 stop", default);

        var page = ResultPage(bench);
        Show(page);

        Assert.Contains("report date unknown", Texts(page));
    }

    [AvaloniaFact]
    public void AnOutfittingPlanStoredWithNoAgeShowsAnyInThePicker()
    {
        using var kit = AppLook.ControlKit();
        var bench = Set(lookups: true);
        bench.Plans.Record(SeedRoute(null), "1 stop", default);

        var page = ResultPage(bench);
        Show(page);

        Assert.Equal("Any", PickerValue(page));
    }

    [AvaloniaFact]
    public void OtherKindsOfPlanShowNeitherThePickerNorSearch()
    {
        using var kit = AppLook.ControlKit();
        var bench = Set(lookups: true);
        var plan = new StoredRoutePlan { Kind = RoutePlanKind.Trade, Headline = "Trade" };

        var page = new RoutePlanResultPage(plan, plans: bench.Plans, outfittingPlotter: bench.Plotter);
        Show(page);

        Assert.Null(SearchButton(page));
        Assert.Empty(page.GetVisualDescendants().OfType<D47.App.Controls.InlinePicker>());
    }

    [AvaloniaFact]
    public async Task SearchPlotsTheSamePlanAgainWithThePickedAgeAndRedraws()
    {
        using var kit = AppLook.ControlKit();
        var bench = Set(lookups: true);
        bench.Plans.Record(SeedRoute(72), "old", new DateTimeOffset(2026, 10, 28, 19, 42, 0, TimeSpan.Zero));
        var before = bench.Plans.Last(RoutePlanKind.Outfitting);

        var page = ResultPage(bench);
        Show(page);

        var picker = page.GetVisualDescendants().OfType<D47.App.Controls.InlinePicker>().Single();
        Click(picker.GetVisualDescendants().OfType<Button>().Single(button => button.Name == D47.App.Controls.InlinePicker.ButtonName));
        Dispatcher.UIThread.RunJobs();
        Click(picker.GetVisualDescendants().OfType<Button>().Single(button =>
            button.Classes.Contains(D47.App.Controls.InlinePicker.OptionClass)
            && Avalonia.Automation.AutomationProperties.GetName(button) == "Any"));
        Click(SearchButton(page)!);

        for (var wait = 0; wait < 200 && bench.Galaxy.Ages.Count == 0; wait++)
        {
            Dispatcher.UIThread.RunJobs();
            await Task.Delay(10, TestContext.Current.CancellationToken);
        }

        Assert.Equal<int?[]>([null], [.. bench.Galaxy.Ages]);

        Assert.True(page.Refresh());
        Dispatcher.UIThread.RunJobs();

        Assert.Contains("report date unknown", Texts(page));
        Assert.DoesNotContain("outfitting reported 12 Oct 2026", Texts(page));
        Assert.Equal("Any", PickerValue(page));

        var asked = Assert.Single(bench.Galaxy.Asked);
        Assert.Equal("LHS 3447", asked.Near);
        Assert.Equal(["6A Fuel Scoop", "1H Guardian FSD Booster"], asked.Items.Select(item => item.Display));

        var after = bench.Plans.Last(RoutePlanKind.Outfitting);
        Assert.NotSame(before, after);
        Assert.Null(after!.Outfitting!.MaxReportAgeHours);
        Assert.Equal(50, after.Outfitting.MaxDistance);
        Assert.True(after.Outfitting.StorageChecked);
    }

    [AvaloniaFact]
    public void WithLookupsOffSearchIsDisabledAndSaysWhy()
    {
        using var kit = AppLook.ControlKit();
        var bench = Set(lookups: false);
        bench.Plans.Record(SeedRoute(72), "old", default);

        var page = ResultPage(bench);
        Show(page);

        var search = SearchButton(page)!;

        Assert.False(search.IsEnabled);
        Assert.Equal(OutfittingPlotter.SwitchedOff, ToolTip.GetTip(search));
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
