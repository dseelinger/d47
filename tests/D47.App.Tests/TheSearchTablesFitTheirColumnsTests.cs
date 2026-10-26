using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using D47.App.Panel;
using D47.App.Theming;
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

/// <summary>The System and Station search tables are drawn through a TableLayout (#1616).</summary>
[Trait("Category", "Integration")]
public sealed class TheSearchTablesFitTheirColumnsTests
{
    private static readonly GalaxySearchResult TwoSystems = new(
        "LTT 7786",
        2,
        [
            new SystemSummary { Name = "Alioth", Distance = 3, Population = 1_000, Allegiance = "Federation" },
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

        public Task<IReadOnlyList<BodySummary>> SystemBodiesAsync(long systemAddress, CancellationToken cancellationToken) => throw new NotSupportedException();
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

    private static async Task<List<TableGrid>> Drawn(PanelView panel, IReadOnlyDictionary<string, string> arguments)
    {
        await panel.GetVisualDescendants().OfType<GalaxySearchPage>().Single().RunAsync(arguments);
        Dispatcher.UIThread.RunJobs();

        return [.. panel.GetVisualDescendants().OfType<TableGrid>()];
    }

    private static void AssertShared(List<TableGrid> grids, int columns)
    {
        Assert.Equal(3, grids.Count);

        foreach (var grid in grids)
        {
            Assert.Equal(columns, grid.ColumnDefinitions.Count);
            Assert.Equal(
                grids[0].ColumnDefinitions.Select(definition => definition.ActualWidth),
                grid.ColumnDefinitions.Select(definition => definition.ActualWidth));
        }
    }

    [AvaloniaFact]
    public async Task TheSystemTableSharesColumnEdgesAndDoesNotStretchItsFigureColumns()
    {
        var (panel, window, _) = Shown(SystemSearchPage.RootKey);

        var grids = await Drawn(panel, new Dictionary<string, string> { ["allegiance"] = "Federation" });

        AssertShared(grids, 7);

        var head = grids.Single(grid => grid.IsHeader);
        var populationNeed = grids.Where(grid => !grid.IsHeader)
            .SelectMany(grid => grid.Children.OfType<Control>().Where(child => Grid.GetColumn(child) == 4))
            .Max(cell => cell.DesiredSize.Width);
        var label = head.Children.OfType<Control>().Single(child => child is not ColumnGrip && Grid.GetColumn(child) == 4);
        label.Measure(new Avalonia.Size(double.PositiveInfinity, double.PositiveInfinity));
        var populationLabel = label.DesiredSize.Width;

        Assert.True(head.Layout.Widths[4] <= Math.Max(populationNeed, populationLabel) + 1);
        Assert.True(head.Layout.Widths[0] > head.Layout.Widths[1]);

        window.Close();
    }

    [AvaloniaFact]
    public async Task TheStationTableSharesColumnEdges()
    {
        var (panel, window, _) = Shown(StationSearchPage.RootKey);

        var grids = await Drawn(panel, new Dictionary<string, string> { ["services"] = "Refuel" });

        AssertShared(grids, 7);

        window.Close();
    }
}
