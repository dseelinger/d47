using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using D47.Core.Interface;
using D47.App.Panel;
using D47.Core.Capabilities;
using D47.Core.Capabilities.Builtin;
using D47.Core.Journal;
using D47.Core.Knowledge;
using D47.Core.Storage;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.App.Tests;

/// <summary>
/// Navigation › Plan's Exobiology and Road to Riches cards take a jump range and a farthest-from-arrival
/// distance, and send them only when filled in (#1799).
/// </summary>
[Trait("Category", "Integration")]
public class TheBiologyAndRichesCardsSendJumpRangeAndFarthestTests
{
    private const string ExobiologyMinimum = "Least a species is worth (cr)";

    private const string RichesMinimum = "Least worth stopping for (cr)";

    private const string JumpRange = "Jump range (ly), optional, filled from your ship";

    private const string Farthest = "Farthest from arrival (ls)";

    private sealed class RecordingRoutes : IRouteService
    {
        public RichesQuery? Riches { get; private set; }

        public ExobiologyQuery? Exobiology { get; private set; }

        public Task<PlottedRoute?> PlotAsync(RouteQuery query, CancellationToken cancellationToken) =>
            Task.FromResult<PlottedRoute?>(null);

        public Task<RichesRoute?> PlotRichesAsync(RichesQuery query, CancellationToken cancellationToken)
        {
            Riches = query;
            return Task.FromResult<RichesRoute?>(null);
        }

        public Task<ExobiologyRoute?> PlotExobiologyAsync(ExobiologyQuery query, CancellationToken cancellationToken)
        {
            Exobiology = query;
            return Task.FromResult<ExobiologyRoute?>(null);
        }

        public Task<CarrierRoute?> PlotCarrierAsync(CarrierRouteQuery query, CancellationToken cancellationToken) =>
            Task.FromResult<CarrierRoute?>(null);
    }

    private static (PanelView Panel, RecordingRoutes Routes) Open()
    {
        var folder = TempFolders.Create("d47-biology-card");
        var settings = TestSurface.Settings();
        settings.Apply(GalaxyCapability.EnabledKey, "true", D47.Core.Configuration.SettingsCaller.Panel);

        var gameState = new GameStateStore();

        foreach (var json in (string[])
                 [
                     """{"timestamp":"2026-09-01T00:00:00Z","event":"Commander","FID":"F1","Name":"Fixture"}""",
                     """{"timestamp":"2026-09-01T00:00:01Z","event":"FSDJump","StarSystem":"Sol"}""",
                 ])
        {
            Assert.True(JournalEvent.TryParse(json, NullLogger.Instance, out var parsed));
            gameState.Apply(parsed!);
        }

        var routes = new RecordingRoutes();
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
            () => true,
            Commander: () => gameState.Active));

        var window = new Window { Content = panel, Width = 1280, Height = 1400 };
        window.Show();

        panel.Tab = PanelTab.Navigation;
        panel.Nav.SelectRoot(RoutingPages.PlanRoot);
        Dispatcher.UIThread.RunJobs();

        return (panel, routes);
    }

    /// <summary>The smallest container around the box named <paramref name="marker"/> that holds a Plot button.</summary>
    private static Visual Card(PanelView panel, string marker)
    {
        var box = panel.GetVisualDescendants()
            .OfType<TextBox>()
            .Single(candidate => AutomationProperties.GetName(candidate) == marker);

        return box.GetVisualAncestors()
            .First(ancestor => ancestor.GetVisualDescendants().OfType<Button>().Any(button => button.Content as string == "Plot"));
    }

    private static TextBox Box(Visual card, string name) =>
        card.GetVisualDescendants().OfType<TextBox>().Single(box => AutomationProperties.GetName(box) == name);

    private static void Plot(Visual card)
    {
        card.GetVisualDescendants()
            .OfType<Button>()
            .Single(button => button.Content as string == "Plot")
            .RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();
    }

    [AvaloniaFact]
    public void TheExobiologyMinimumReadsPerSpecies()
    {
        var (panel, _) = Open();

        Assert.NotNull(Card(panel, ExobiologyMinimum));
    }

    [AvaloniaFact]
    public void TheExobiologyCardSendsWhatIsTyped()
    {
        var (panel, routes) = Open();
        var card = Card(panel, ExobiologyMinimum);

        Box(card, JumpRange).Text = "31.5";
        Box(card, Farthest).Text = "50,000";
        Plot(card);

        Assert.Equal(31.5, routes.Exobiology!.JumpRange);
        Assert.Equal(50_000, routes.Exobiology.MaxDistanceToArrival);
    }

    [AvaloniaFact]
    public void TheExobiologyCardSendsNeitherWhenBothAreEmpty()
    {
        var (panel, routes) = Open();

        Plot(Card(panel, ExobiologyMinimum));

        Assert.Equal(50, routes.Exobiology!.JumpRange);
        Assert.Equal(10_000, routes.Exobiology.MaxDistanceToArrival);
    }

    [AvaloniaFact]
    public void TheRoadToRichesCardSendsWhatIsTyped()
    {
        var (panel, routes) = Open();
        var card = Card(panel, RichesMinimum);

        Box(card, JumpRange).Text = "31.5";
        Box(card, Farthest).Text = "50,000";
        Plot(card);

        Assert.Equal(31.5, routes.Riches!.JumpRange);
        Assert.Equal(50_000, routes.Riches.MaxDistanceToArrival);
    }

    [AvaloniaFact]
    public void TheRoadToRichesCardSendsNeitherWhenBothAreEmpty()
    {
        var (panel, routes) = Open();

        Plot(Card(panel, RichesMinimum));

        Assert.Equal(10_000, routes.Riches!.MaxDistanceToArrival);
        Assert.NotEqual(31.5, routes.Riches.JumpRange);
    }
}
