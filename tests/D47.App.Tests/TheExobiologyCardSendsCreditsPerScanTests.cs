using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using D47.App.Panel;
using D47.Core.Capabilities;
using D47.Core.Capabilities.Builtin;
using D47.Core.Interface;
using D47.Core.Journal;
using D47.Core.Knowledge;
using D47.Core.Storage;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.App.Tests;

/// <summary>The Exobiology card's "Most credits per scan" checkbox sends credits_per_scan (#2316).</summary>
[Trait("Category", "Integration")]
public class TheExobiologyCardSendsCreditsPerScanTests
{
    private sealed class RecordedRoutes : IRouteService
    {
        public ExobiologyQuery? Last { get; private set; }

        public Task<PlottedRoute?> PlotAsync(RouteQuery query, CancellationToken cancellationToken) =>
            Task.FromResult<PlottedRoute?>(null);

        public Task<RichesRoute?> PlotRichesAsync(RichesQuery query, CancellationToken cancellationToken) =>
            Task.FromResult<RichesRoute?>(null);

        public Task<ExobiologyRoute?> PlotExobiologyAsync(ExobiologyQuery query, CancellationToken cancellationToken)
        {
            Last = query;

            return Task.FromResult<ExobiologyRoute?>(null);
        }

        public Task<CarrierRoute?> PlotCarrierAsync(CarrierRouteQuery query, CancellationToken cancellationToken) =>
            Task.FromResult<CarrierRoute?>(null);
    }

    private static (PanelView Panel, RecordedRoutes Routes) Open()
    {
        var folder = TempFolders.Create("d47-exobiology-card");
        var settings = TestSurface.Settings();
        var gameState = new GameStateStore();

        foreach (var json in (string[])
                 [
                     """{"timestamp":"2026-09-01T00:00:00Z","event":"Commander","FID":"F1","Name":"Fixture"}""",
                     """{"timestamp":"2026-09-01T00:00:01Z","event":"FSDJump","StarSystem":"Alpha Centauri"}""",
                 ])
        {
            Assert.True(JournalEvent.TryParse(json, NullLogger.Instance, out var parsed));
            gameState.Apply(parsed!);
        }

        var routes = new RecordedRoutes();
        var plans = new RoutePlanBook(Path.Combine(folder, "route-plans.json"), new MemoryFileSystem(), NullLogger<RoutePlanBook>.Instance);

        var registry = CapabilityRegistry.Build(
        [
            RouteCapability.Create(routes, null, () => gameState.Active, settings, plans),
            ExobiologyCapability.Create(routes, () => gameState.Active, settings, null, plans),
        ]);

        var panel = new PanelView { DataContext = new PanelViewModel() };

        panel.EnableRouting(new RoutingSurface(
            () => NavRoute.None,
            () => gameState.Active?.Location.StarSystem,
            registry,
            plans,
            Commander: () => gameState.Active));

        var window = new Window { Content = panel, Width = 1280, Height = 860 };
        window.Show();

        panel.Tab = PanelTab.Navigation;
        panel.Nav.SelectRoot(RoutingPages.PlanRoot);
        Dispatcher.UIThread.RunJobs();

        return (panel, routes);
    }

    private static CheckBox PerScan(PanelView panel) =>
        panel.GetVisualDescendants()
            .OfType<CheckBox>()
            .Single(box => (box.Content as TextBlock)?.Text == "Most credits per scan");

    private static void PlotExobiology(PanelView panel)
    {
        var plot = PerScan(panel).GetVisualAncestors()
            .First(ancestor => ancestor.GetVisualDescendants().OfType<Button>().Any(button => button.Content as string == "Plot"))
            .GetVisualDescendants()
            .OfType<Button>()
            .Single(button => button.Content as string == "Plot");

        plot.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();
    }

    [AvaloniaFact]
    public void TheCheckboxStartsUnchecked()
    {
        var (panel, _) = Open();

        Assert.NotEqual(true, PerScan(panel).IsChecked);
    }

    [AvaloniaFact]
    public void CheckedTheCardAsksForCreditsPerScan()
    {
        var (panel, routes) = Open();

        PerScan(panel).IsChecked = true;
        PlotExobiology(panel);

        Assert.Equal(30, routes.Last?.MaxResults);
    }

    [AvaloniaFact]
    public void UncheckedTheCardAsksForTheServicesOwnRoute()
    {
        var (panel, routes) = Open();

        PlotExobiology(panel);

        Assert.Equal(10, routes.Last?.MaxResults);
    }
}
