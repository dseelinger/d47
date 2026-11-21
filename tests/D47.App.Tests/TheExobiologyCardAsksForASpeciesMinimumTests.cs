using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml.Styling;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using D47.App.Panel;
using D47.App.Theming;
using D47.Core.Capabilities;
using D47.Core.Capabilities.Builtin;
using D47.Core.Interface;
using D47.Core.Journal;
using D47.Core.Knowledge;
using D47.Core.Storage;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.App.Tests;

/// <summary>The Exobiology card chooses stops by the least a species is worth, and nothing else (#2628).</summary>
[Trait("Category", "Integration")]
public class TheExobiologyCardAsksForASpeciesMinimumTests
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

    private static (Window Window, PanelView Panel, RecordedRoutes Routes) Open()
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

        new ThemeManager(Application.Current!, NullLogger<ThemeManager>.Instance).Apply(ThemeCatalog.Elite);
        Application.Current!.Styles.Add(new StyleInclude((Uri?)null) { Source = new Uri("avares://d47/Theming/ControlKitTheme.axaml") });

        var panel = new PanelView { DataContext = new PanelViewModel() };

        panel.EnableRouting(new RoutingSurface(
            () => NavRoute.None,
            () => gameState.Active?.Location.StarSystem,
            registry,
            plans,
            Commander: () => gameState.Active));

        var window = new Window
        {
            Content = panel,
            Width = 1280,
            Height = 860,
            Background = (IBrush?)Application.Current.Resources[ThemeManager.BgKey],
        };
        window.Show();

        panel.Tab = PanelTab.Astrometrics;
        panel.Nav.SelectRoot(RoutingPages.PlanRoot);
        Dispatcher.UIThread.RunJobs();

        return (window, panel, routes);
    }

    private static TextBox Minimum(PanelView panel) =>
        panel.GetVisualDescendants()
            .OfType<TextBox>()
            .Single(box => (Avalonia.Automation.AutomationProperties.GetName(box) ?? string.Empty)
                .StartsWith("Least a species is worth", StringComparison.Ordinal));

    private static Button Plot(PanelView panel) =>
        Minimum(panel).GetVisualAncestors()
            .First(ancestor => ancestor.GetVisualDescendants().OfType<Button>().Any(button => button.Content as string == "Plot"))
            .GetVisualDescendants()
            .OfType<Button>()
            .Single(button => button.Content as string == "Plot");

    private static void PlotExobiology(PanelView panel)
    {
        var plot = Plot(panel);

        plot.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();
    }

    [AvaloniaFact]
    public void TheCardHasNoCreditsPerScanCheckbox()
    {
        var (window, panel, _) = Open();

        Plot(panel).BringIntoView();
        Dispatcher.UIThread.RunJobs();

        using (var frame = window.CaptureRenderedFrame()!)
        {
            frame.SaveCapture("exobiology-card.png");
        }

        Assert.DoesNotContain(
            panel.GetVisualDescendants().OfType<CheckBox>(),
            box => (box.Content as TextBlock)?.Text == "Most credits per scan");
    }

    [AvaloniaFact]
    public void AnEmptyMinimumSaysNoMinimumAndSendsNone()
    {
        var (_, panel, routes) = Open();

        Assert.Equal("no minimum", Minimum(panel).PlaceholderText);

        PlotExobiology(panel);

        Assert.Equal(0, routes.Last?.MinimumValue);
    }

    [AvaloniaFact]
    public void AMinimumTypedInTheCardIsSent()
    {
        var (_, panel, routes) = Open();

        Minimum(panel).Text = "10000000";
        PlotExobiology(panel);

        Assert.Equal(10_000_000, routes.Last?.MinimumValue);
    }
}
