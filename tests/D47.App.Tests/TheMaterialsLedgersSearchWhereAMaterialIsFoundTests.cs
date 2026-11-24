using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
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

/// <summary>Commander › Assets › Materials: a search tile under each High Grade Emission cell and each grade 4 raw's cell (#2735).</summary>
public sealed class TheMaterialsLedgersSearchWhereAMaterialIsFoundTests
{
    private sealed class Galaxy : StubGalaxyService
    {
        public override Task<GalaxySearchResult> SearchAsync(GalaxyQuery query, CancellationToken cancellationToken) =>
            Task.FromResult(new GalaxySearchResult(null, 0, []));
    }

    private sealed class Systems : StubStarSystemService
    {
        public override Task<IReadOnlyList<SystemNameMatch>> MatchNamesAsync(string typed, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<SystemNameMatch>>([]);

        public override Task<IReadOnlyList<SystemNameMatch>> PrefixNamesAsync(string typed, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<SystemNameMatch>>([]);
    }

    private static (PanelView Panel, Window Window) Open(bool withSearch, string? here = null)
    {
        var root = TestSurface.MemoryFolder("d47-materials-search-tile-tests");
        var store = new GameStateStore();

        void Apply(string line)
        {
            Assert.True(JournalEvent.TryParse(line, NullLogger.Instance, out var parsed));
            store.Apply(parsed!);
        }

        Apply("""{"timestamp":"2026-08-18T09:00:00Z","event":"Commander","FID":"F1","Name":"Jameson"}""");

        if (here is not null)
        {
            Apply($$"""{"timestamp":"2026-08-18T09:00:00Z","event":"Location","StarSystem":"{{here}}","SystemAddress":1,"StarPos":[0,0,0],"Docked":false}""");
        }

        CommanderGameState? State() => store.Active;

        var (checklists, _, _, ships, kit, _) = PlanBench.Over(root, State);

        var panel = new PanelView { DataContext = new PanelViewModel() };
        panel.EnableLoadout(ships, checklists, State, kit);

        if (withSearch)
        {
            var settings = TestSurface.Settings();
            var searches = new SavedSearchStore("saved-searches.json", new MemoryFileSystem(), NullLogger<SavedSearchStore>.Instance);
            var board = new GalaxySearchBoard();
            var registry = CapabilityRegistry.Build([GalaxyCapability.Create(new Galaxy(), () => "Sol", settings, searches: board)]);
            var systems = new Systems();

            panel.EnableStarSystem(new StarSystemSurface(systems, () => null)
            {
                Searches = new GalaxySearchSurface(registry, board, searches, () => "F1", () => [])
                {
                    PrefixNames = systems.PrefixNamesAsync,
                    SuggestionPause = TimeSpan.Zero,
                },
            });
        }

        var window = new Window { Content = panel, Width = 1280, Height = 1400 };
        window.Show();

        panel.Tab = PanelTab.Commander;
        panel.Nav.Show(PanelTabNames.AssetsRoot);
        Dispatcher.UIThread.RunJobs();
        Assert.True(panel.Nav.Show(LoadoutPages.GapRoot));
        Dispatcher.UIThread.RunJobs();

        return (panel, window);
    }

    private static void Select(PanelView panel, string view)
    {
        panel.GetVisualDescendants().OfType<MaterialsPage>().Single().Select(view);
        Dispatcher.UIThread.RunJobs();
    }

    private static IReadOnlyList<Border> Tiles(PanelView panel) =>
        [.. panel.GetVisualDescendants().OfType<Border>()
            .Where(border => AutomationProperties.GetName(border) is { } name
                && (name.StartsWith("Search HGE systems for ", StringComparison.Ordinal)
                    || name.Contains(" in Search, where ", StringComparison.Ordinal)))];

    private static Border Tile(PanelView panel, string name) => Tiles(panel).Single(tile => AutomationProperties.GetName(tile) == name);

    private static void Enter(Control target)
    {
        target.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Enter, Source = target });
        Dispatcher.UIThread.RunJobs();
    }

    [AvaloniaFact]
    public void ExactlyTheTenHighGradeEmissionCellsHaveATile()
    {
        var (panel, window) = Open(withSearch: true);
        Select(panel, "manufactured");

        var expected = EmissionRules.Groups.SelectMany(group => group.Materials).Count();
        Assert.Equal(10, expected);
        Assert.Equal(10, Tiles(panel).Count);
        Assert.All(Tiles(panel), tile => Assert.StartsWith("Search HGE systems for ", AutomationProperties.GetName(tile)));
        Assert.Contains(panel.GetVisualDescendants().OfType<TextBlock>(), block => block.Text == MaterialsPage.HgeLabel);

        window.Close();
    }

    [AvaloniaFact]
    public void ATileSitsTwoPixelsUnderItsCellInItsOwnColumnAndIsAtLeast44Tall()
    {
        var (panel, window) = Open(withSearch: true);
        Select(panel, "manufactured");

        var tile = Tile(panel, "Search HGE systems for Imperial Shielding");
        var cell = panel.GetVisualDescendants().OfType<Border>()
            .Single(border => AutomationProperties.GetName(border)?.StartsWith("Imperial Shielding, ", StringComparison.Ordinal) == true);

        var tileAt = tile.TranslatePoint(default, panel)!.Value;
        var cellAt = cell.TranslatePoint(default, panel)!.Value;

        Assert.Equal(cellAt.X, tileAt.X, 0.5);
        Assert.Equal(cell.Bounds.Width, tile.Bounds.Width, 0.5);
        Assert.Equal(cellAt.Y + cell.Bounds.Height + 2, tileAt.Y, 0.5);
        Assert.True(tile.Bounds.Height >= 44);

        window.Close();
    }

    [AvaloniaFact]
    public void TheGrade4RawCellsHaveATileNamingTheirSite()
    {
        var (panel, window) = Open(withSearch: true);
        Select(panel, "raw");

        Assert.Equal(7, Tiles(panel).Count);
        Tile(panel, "Open HR 3230 in Search, where Selenium is found");
        Tile(panel, "Open HIP 36601 in Search, where Ruthenium is found");
        Assert.Contains(panel.GetVisualDescendants().OfType<TextBlock>(), block => block.Text == "HR 3230");

        window.Close();
    }

    [AvaloniaFact]
    public void ABandWithNoTileHasNoTileRowAndANoSearchSurfaceDrawsNone()
    {
        var (panel, window) = Open(withSearch: false);
        Select(panel, "manufactured");
        Assert.Empty(Tiles(panel));

        Select(panel, "raw");
        Assert.Empty(Tiles(panel));

        window.Close();
    }

    [AvaloniaFact]
    public void PressingTheImperialShieldingTileRunsTheEmpireSearchInSearchSystems()
    {
        var (panel, window) = Open(withSearch: true);
        Select(panel, "manufactured");

        Enter(Tile(panel, "Search HGE systems for Imperial Shielding"));

        Assert.Equal(PanelTab.Search, panel.Tab);
        Assert.Equal(SystemSearchPage.RootKey, panel.Nav.RootKeyOf(PanelTab.Search));

        var form = panel.GetVisualDescendants().OfType<SystemSearchPage>().Single().FormArguments();
        Assert.Equal("Empire", form["allegiance"]);
        Assert.Equal("1000000-", form["population"]);
        Assert.Equal("500", form["distance"]);

        window.Close();
    }

    [AvaloniaFact]
    public void PressingTheSeleniumTileOpensSearchSystemOnHR3230()
    {
        var (panel, window) = Open(withSearch: true);
        Select(panel, "raw");

        Enter(Tile(panel, "Open HR 3230 in Search, where Selenium is found"));

        Assert.Equal(PanelTab.Search, panel.Tab);
        Assert.Equal(StarSystemPage.RootKey, panel.Nav.RootKeyOf(PanelTab.Search));
        Assert.Contains(panel.GetVisualDescendants().OfType<TextBox>(), box => box.Text == "HR 3230");

        window.Close();
    }
}
