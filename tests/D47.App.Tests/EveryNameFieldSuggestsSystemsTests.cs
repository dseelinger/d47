using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using D47.App.Controls;
using D47.App.Panel;
using D47.Core.Capabilities;
using D47.Core.Interface;
using D47.Core.Journal;
using D47.Core.Knowledge;
using D47.Core.Storage;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.App.Tests;

/// <summary>
/// The Near, Destination, From, To and Sell in fields list the systems that start with what is typed, and a
/// pick only fills the field (#1089). The pause before asking is zero, so nothing here waits for it.
/// </summary>
[Trait("Category", "Integration")]
public sealed class EveryNameFieldSuggestsSystemsTests
{
    private static readonly string[] Names = ["Diaguandri", "Dian", "Diabozo"];

    private sealed class Systems : IStarSystemService
    {
        private readonly List<string> _prefixes = [];

        public string[] Prefixes()
        {
            lock (_prefixes)
            {
                return [.. _prefixes];
            }
        }

        public Task<StarSystemProfile?> ProfileAsync(long systemAddress, CancellationToken cancellationToken) =>
            Task.FromResult<StarSystemProfile?>(null);

        public Task<IReadOnlyList<SystemNameMatch>> MatchNamesAsync(string typed, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<SystemNameMatch>>([]);

        public Task<IReadOnlyList<SystemNameMatch>> PrefixNamesAsync(string typed, CancellationToken cancellationToken)
        {
            lock (_prefixes)
            {
                _prefixes.Add(typed);
            }

            return Task.FromResult<IReadOnlyList<SystemNameMatch>>(
                [.. Names
                    .Where(name => name.StartsWith(typed, StringComparison.OrdinalIgnoreCase))
                    .Select(name => new SystemNameMatch(name, name.Length, new StarPosition(0, 0, 0)))]);
        }

        public Task<PowerplayNeighbourhood> PowerplayNearAsync(string system, double lightYears, CancellationToken cancellationToken) =>
            Task.FromResult(new PowerplayNeighbourhood(0, []));
    }

    private sealed record Shown(PanelView Panel, Window Window, Systems Systems, GalaxySearchBoard Board, BestCargoBoard Cargo)
    {
        public TextBox Box(string name) =>
            Panel.GetVisualDescendants().OfType<TextBox>().Last(box => AutomationProperties.GetName(box) == name);

        public string[] Listed =>
        [
            .. Panel.GetVisualDescendants()
                .OfType<Border>()
                .Where(border => border.Name == SystemNameSuggestions.ListName && border.IsEffectivelyVisible)
                .SelectMany(border => border.GetVisualDescendants().OfType<Button>())
                .Select(button => AutomationProperties.GetName(button) ?? string.Empty),
        ];

        public void Pick(string name) =>
            Panel.GetVisualDescendants()
                .OfType<Border>()
                .Where(border => border.Name == SystemNameSuggestions.ListName && border.IsEffectivelyVisible)
                .SelectMany(border => border.GetVisualDescendants().OfType<Button>())
                .Single(button => AutomationProperties.GetName(button) == name)
                .RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
    }

    private static Shown Open(PanelTab tab, string root, bool lookups = true)
    {
        var systems = new Systems();
        var board = new GalaxySearchBoard();
        var cargo = new BestCargoBoard();
        var registry = CapabilityRegistry.Build([]);
        var plans = new RoutePlanBook(Path.Combine("plans", "route-plans.json"), new MemoryFileSystem(), NullLogger<RoutePlanBook>.Instance);

        var panel = new PanelView { DataContext = new PanelViewModel() };

        panel.EnableStarSystem(new StarSystemSurface(
            systems,
            () => new JournalLocation("LTT 7786", null, false, null),
            () => lookups,
            () => { })
        {
            Searches = new GalaxySearchSurface(registry, board, null, () => string.Empty, () => [])
            {
                PrefixNames = systems.PrefixNamesAsync,
                SuggestionPause = TimeSpan.Zero,
            },
        });

        panel.EnableRouting(new RoutingSurface(
            () => NavRoute.None,
            () => "LTT 7786",
            registry,
            plans,
            () => lookups,
            Cargo: cargo)
        {
            PrefixNames = systems.PrefixNamesAsync,
            SuggestionPause = TimeSpan.Zero,
        });

        var window = new Window { Content = panel, Width = 1280, Height = 960 };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        panel.Tab = tab;
        panel.Nav.SelectRoot(root);
        Dispatcher.UIThread.RunJobs();

        return new Shown(panel, window, systems, board, cargo);
    }

    private static void Until(Func<bool> done)
    {
        var deadline = DateTime.UtcNow.AddSeconds(5);

        while (!done())
        {
            Assert.True(DateTime.UtcNow < deadline, "the page did not settle in time");
            Dispatcher.UIThread.RunJobs();
            Thread.Sleep(5);
        }

        Dispatcher.UIThread.RunJobs();
    }

    private static void Settle()
    {
        Dispatcher.UIThread.RunJobs();
        Thread.Sleep(50);
        Dispatcher.UIThread.RunJobs();
    }

    private static void TypeAndPick(Shown shown, string field)
    {
        shown.Box(field).Text = "Dia";
        Until(() => shown.Listed.Length > 0);

        Assert.Contains("Diaguandri", shown.Listed);
        Assert.Contains("Dian", shown.Listed);

        shown.Pick("Dian");
        Dispatcher.UIThread.RunJobs();

        Assert.Equal("Dian", shown.Box(field).Text);
        Assert.Empty(shown.Listed);
        Assert.Equal(["Dia"], shown.Systems.Prefixes());
    }

    [AvaloniaFact]
    public void PickingANameFillsNearOnTheSystemsSearch()
    {
        var shown = Open(PanelTab.Search, SystemSearchPage.RootKey);

        TypeAndPick(shown, "near");

        Assert.Null(shown.Board.Last(GalaxySearchKind.Systems));
        shown.Window.Close();
    }

    [AvaloniaFact]
    public void PickingANameFillsNearOnTheStationsSearch()
    {
        var shown = Open(PanelTab.Search, StationSearchPage.RootKey);

        TypeAndPick(shown, "near");

        Assert.Null(shown.Board.Last(GalaxySearchKind.Stations));
        shown.Window.Close();
    }

    [AvaloniaFact]
    public void PickingANameFillsNearOnTheBodiesSearch()
    {
        var shown = Open(PanelTab.Search, BodySearchPage.RootKey);

        TypeAndPick(shown, "near");

        Assert.Null(shown.Board.Last(GalaxySearchKind.Bodies));
        shown.Window.Close();
    }

    [AvaloniaFact]
    public void PickingANameFillsDestinationOnThePlan()
    {
        var shown = Open(PanelTab.Navigation, RoutingPages.PlanRoot);

        TypeAndPick(shown, "Destination, required");

        shown.Window.Close();
    }

    [AvaloniaFact]
    public void PickingANameFillsFromOnThePlan()
    {
        var shown = Open(PanelTab.Navigation, RoutingPages.PlanRoot);

        TypeAndPick(shown, "From, optional, filled from your ship");

        shown.Window.Close();
    }

    [AvaloniaFact]
    public void PickingANameFillsToOnTheCarrierRoute()
    {
        var shown = Open(PanelTab.Navigation, RoutingPages.PlanRoot);

        TypeAndPick(shown, "To, required");

        shown.Window.Close();
    }

    [AvaloniaFact]
    public void PickingANameFillsFromOnTheCarrierRoute()
    {
        var shown = Open(PanelTab.Navigation, RoutingPages.PlanRoot);

        TypeAndPick(shown, "From, optional, filled from your carrier");

        shown.Window.Close();
    }

    [AvaloniaFact]
    public void ACarrierStartFilledFromFleetOpensNoList()
    {
        var shown = Open(PanelTab.Navigation, RoutingPages.PlanRoot);

        typeof(PanelView)
            .GetMethod("PlanCarrierRoute", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
            .Invoke(shown.Panel, ["Diaguandri"]);
        Settle();

        Assert.Equal("Diaguandri", shown.Box("From, optional, filled from your carrier").Text);
        Assert.Empty(shown.Listed);
        Assert.Empty(shown.Systems.Prefixes());
        shown.Window.Close();
    }

    [AvaloniaFact]
    public void PickingANameFillsSellInOnBestCargo()
    {
        var shown = Open(PanelTab.Navigation, RoutingPages.BestCargoRoot);

        TypeAndPick(shown, "Sell in");

        Assert.Null(shown.Cargo.Last);
        shown.Window.Close();
    }

    [AvaloniaFact]
    public void ASearchFilledByAPostingOpensNoList()
    {
        var shown = Open(PanelTab.Search, SystemSearchPage.RootKey);

        shown.Board.Post(new GalaxySearchPosting(
            GalaxySearchKind.Systems,
            new Dictionary<string, string>(StringComparer.Ordinal) { ["near"] = "Diaguandri" },
            "Diaguandri",
            new GalaxySearchResult("Diaguandri", 0, []),
            "Nothing matched that search.",
            DateTimeOffset.UtcNow));
        Settle();

        Assert.Equal("Diaguandri", shown.Box("near").Text);
        Assert.Empty(shown.Listed);
        Assert.Empty(shown.Systems.Prefixes());
        shown.Window.Close();
    }

    [AvaloniaFact]
    public void TheListsAreDrawnUnderTheirFields()
    {
        new D47.App.Theming.ThemeManager(Avalonia.Application.Current!, NullLogger<D47.App.Theming.ThemeManager>.Instance).Apply(D47.Core.Interface.ThemeCatalog.Elite);
        Avalonia.Application.Current!.Styles.Add(new Avalonia.Markup.Xaml.Styling.StyleInclude((Uri?)null)
        {
            Source = new Uri("avares://d47/Theming/ControlKitTheme.axaml"),
        });

        foreach (var (tab, root, field, file) in new[]
        {
            (PanelTab.Search, SystemSearchPage.RootKey, "near", "name-list-near.png"),
            (PanelTab.Navigation, RoutingPages.PlanRoot, "Destination, required", "name-list-plan.png"),
            (PanelTab.Navigation, RoutingPages.BestCargoRoot, "Sell in", "name-list-sell-in.png"),
        })
        {
            var shown = Open(tab, root);
            shown.Box(field).Text = "Dia";
            Until(() => shown.Listed.Length > 0);

            using var frame = shown.Window.CaptureRenderedFrame()!;
            frame.SaveCapture(file);
            shown.Window.Close();
        }
    }

    [AvaloniaFact]
    public void WithGalaxySearchOffTheFieldsListNothingAndSendNothing()
    {
        var shown = Open(PanelTab.Search, SystemSearchPage.RootKey, lookups: false);

        shown.Box("near").Text = "Dia";
        Settle();

        Assert.Empty(shown.Listed);
        Assert.Empty(shown.Systems.Prefixes());
        shown.Window.Close();
    }
}
