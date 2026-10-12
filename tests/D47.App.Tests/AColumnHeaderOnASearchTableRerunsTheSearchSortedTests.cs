using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using D47.App.Controls;
using D47.App.Panel;
using D47.Core.Capabilities;
using D47.Core.Capabilities.Builtin;
using D47.Core.Configuration;
using D47.Core.Interface;
using D47.Core.Journal;
using D47.Core.Knowledge;
using D47.Core.Storage;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.App.Tests;

/// <summary>Pressing a System or Station column header runs the search again sorted by the service on that column (#1113).</summary>
public sealed class AColumnHeaderOnASearchTableRerunsTheSearchSortedTests
{
    private static readonly GalaxySearchResult TwoSystems = new(
        "LTT 7786",
        2,
        [
            new SystemSummary { Name = "Alioth", Distance = 3, Population = 1_000 },
            new SystemSummary { Name = "Sol", Distance = 9, Population = 22_000_000_000 },
        ]);

    private static readonly StationSearchResult TwoStations = new(
        "LTT 7786",
        2,
        [
            new StationSummary { Name = "Jameson Memorial", SystemName = "Shinrarta Dezhra", Distance = 4.2 },
            new StationSummary { Name = "Daedalus", SystemName = "Sol", Distance = 40 },
        ]);

    private sealed class Systems : IStarSystemService
    {
        public Task<StarSystemProfile?> ProfileAsync(long systemAddress, CancellationToken cancellationToken) =>
            Task.FromResult<StarSystemProfile?>(null);

        public Task<IReadOnlyList<SystemNameMatch>> MatchNamesAsync(string typed, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<SystemNameMatch>>([]);

        public Task<IReadOnlyList<SystemNameMatch>> PrefixNamesAsync(string typed, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<SystemNameMatch>>([]);

        public Task<PowerplayNeighbourhood> PowerplayNearAsync(string system, double lightYears, CancellationToken cancellationToken) =>
            Task.FromResult(new PowerplayNeighbourhood(0, []));
    }

    private sealed class Galaxy : IGalaxyService
    {
        public List<GalaxyQuery> Systems { get; } = [];

        public List<StationQuery> Stations { get; } = [];

        public Task<GalaxySearchResult> SearchAsync(GalaxyQuery query, CancellationToken cancellationToken)
        {
            Systems.Add(query);
            return Task.FromResult(TwoSystems);
        }

        public Task<StationSearchResult> FindStationsAsync(StationQuery query, CancellationToken cancellationToken)
        {
            Stations.Add(query);
            return Task.FromResult(TwoStations);
        }

        public Task<double?> DistanceAsync(string from, string to, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<BodySearchResult> FindBodiesAsync(BodyQuery query, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<ColonisationScan> ScanForColonisationAsync(ColonisationQuery query, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<MissionStationScan> FindMissionStationsAsync(MissionStationQuery query, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<SystemBiology> SystemBiologyAsync(long systemAddress, CancellationToken cancellationToken) => throw new NotSupportedException();
    }

    private static (PanelView Panel, Window Window, Galaxy Galaxy) Shown(string root)
    {
        var settings = TestSurface.Settings();
        settings.Apply(GalaxyCapability.EnabledKey, "true", SettingsCaller.Panel);

        var galaxy = new Galaxy();
        var board = new GalaxySearchBoard();
        var history = new SearchHistory("search-history.json", new MemoryFileSystem(), NullLogger<SearchHistory>.Instance);
        var registry = CapabilityRegistry.Build([GalaxyCapability.Create(galaxy, () => "LTT 7786", settings, searches: board, history: history)]);
        var store = new SavedSearchStore("saved-searches.json", new MemoryFileSystem(), NullLogger<SavedSearchStore>.Instance);

        var panel = new PanelView { DataContext = new PanelViewModel() };

        panel.EnableStarSystem(new StarSystemSurface(
            new Systems(),
            () => new JournalLocation("LTT 7786", null, false, null),
            () => settings.Current.Knowledge.GalaxySearch,
            () => { })
        {
            Searches = new GalaxySearchSurface(registry, board, store, () => "F1", () => [])
            {
                PrefixNames = new Systems().PrefixNamesAsync,
                SuggestionPause = TimeSpan.Zero,
                History = history,
            },
        });

        var window = new Window { Content = panel, Width = 1280, Height = 960 };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        panel.Tab = PanelTab.Search;
        panel.Nav.SelectRoot(root);
        Dispatcher.UIThread.RunJobs();

        return (panel, window, galaxy);
    }

    private static Button Named(Control root, string name) =>
        root.GetVisualDescendants().OfType<Button>().First(button => button.Name == name);

    private static void Press(Button button)
    {
        button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();
    }

    private static string? Words(Control root) =>
        root.GetVisualDescendants().OfType<TextBlock>().FirstOrDefault(block => block.Name == GalaxySearchPage.SortedName)?.Text;

    private static async Task Ask(PanelView panel, IReadOnlyDictionary<string, string> arguments)
    {
        await panel.GetVisualDescendants().OfType<GalaxySearchPage>().Single().RunAsync(arguments);
        Dispatcher.UIThread.RunJobs();
    }

    [AvaloniaFact]
    public async Task PressingPopulationAsksForTheMostPopulousThenTheLeastThenNearestAgain()
    {
        var (panel, window, galaxy) = Shown(SystemSearchPage.RootKey);

        await Ask(panel, new Dictionary<string, string> { ["allegiance"] = "Federation" });

        Assert.Null(Assert.Single(galaxy.Systems).Sort);
        Assert.Null(Words(panel));

        Press(Named(panel, "SystemHead.Population"));
        Assert.Equal("population", galaxy.Systems[^1].Sort!.Field.Name);
        Assert.True(galaxy.Systems[^1].Sort!.Descending);
        Assert.Equal("· MOST POPULOUS", Words(panel));

        Press(Named(panel, "SystemHead.Population"));
        Assert.False(galaxy.Systems[^1].Sort!.Descending);
        Assert.Equal("· LEAST POPULOUS", Words(panel));

        Press(Named(panel, "SystemHead.Population"));
        Assert.Null(galaxy.Systems[^1].Sort);
        Assert.Null(Words(panel));

        window.Close();
    }

    [AvaloniaFact]
    public async Task RefreshingASortedSearchKeepsItsSortAndTheFormDoesNotCarryOne()
    {
        var (panel, window, galaxy) = Shown(SystemSearchPage.RootKey);

        await Ask(panel, new Dictionary<string, string> { ["allegiance"] = "Federation" });
        Press(Named(panel, "SystemHead.Population"));
        Press(Named(panel, GalaxySearchPage.RefreshName));

        Assert.Equal("population", galaxy.Systems[^1].Sort!.Field.Name);

        var page = panel.GetVisualDescendants().OfType<SystemSearchPage>().Single();
        Assert.DoesNotContain("order_by", page.FormArguments().Keys);
        Assert.DoesNotContain("order", page.FormArguments().Keys);

        window.Close();
    }

    [AvaloniaFact]
    public async Task ASearchSortedByPopulationOpenedFromHistoryIsStillSortedByPopulation()
    {
        var (panel, window, galaxy) = Shown(SystemSearchPage.RootKey);

        await Ask(panel, new Dictionary<string, string> { ["allegiance"] = "Federation" });
        Press(Named(panel, "SystemHead.Population"));
        await Ask(panel, new Dictionary<string, string> { ["allegiance"] = "Empire" });

        Assert.Null(Words(panel));

        Press(Named(panel, HistoryTile.Name));

        var picker = panel.GetVisualDescendants().OfType<PickerPage>().Single();
        var list = picker.GetControl<ListBox>("Choices");
        // Newest first, so the first Federation entry is the one asked after the sort was pressed.
        var sorted = list.ItemsSource!.Cast<PickerChoice>().First(choice => choice.Text.Contains("Federation", StringComparison.Ordinal));

        list.SelectedItem = sorted;
        Dispatcher.UIThread.RunJobs();
        Press(picker.GetControl<Button>("AcceptButton"));

        Assert.Equal("· MOST POPULOUS", Words(panel));

        Press(Named(panel, GalaxySearchPage.RefreshName));

        Assert.Equal("population", galaxy.Systems[^1].Sort!.Field.Name);

        window.Close();
    }

    [AvaloniaFact]
    public async Task PressingLyOnStationsAsksForTheFarthestFirstThenNearestAgain()
    {
        var (panel, window, galaxy) = Shown(StationSearchPage.RootKey);

        await Ask(panel, new Dictionary<string, string> { ["ship"] = "Cobra Mk III" });

        Assert.Null(Assert.Single(galaxy.Stations).Sort);

        Press(Named(panel, "StationHead.LY"));

        Assert.True(galaxy.Stations[^1].Sort!.Descending);
        Assert.Equal("distance", galaxy.Stations[^1].Sort!.Field.Name);
        Assert.Equal("· FARTHEST FIRST", Words(panel));

        Press(Named(panel, "StationHead.LY"));

        Assert.Null(galaxy.Stations[^1].Sort);
        Assert.Null(Words(panel));

        window.Close();
    }

    [AvaloniaFact]
    public async Task AColumnTheServiceCannotSortIsNotAButton()
    {
        var (panel, window, _) = Shown(StationSearchPage.RootKey);

        await Ask(panel, new Dictionary<string, string> { ["ship"] = "Cobra Mk III" });

        var buttons = panel.GetVisualDescendants().OfType<Button>().Select(button => button.Name).ToList();

        Assert.Contains("StationHead.LY", buttons);
        Assert.DoesNotContain("StationHead.Pad", buttons);
        Assert.DoesNotContain("StationHead.Askedfor", buttons);
        Assert.Contains(panel.GetVisualDescendants().OfType<Control>(), control => control.Name == "StationHead.Pad");

        window.Close();
    }
}
