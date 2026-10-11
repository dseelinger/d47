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

/// <summary>HISTORY on the Search pages opens the Commander's past searches in the full-page picker (#1086).</summary>
public sealed class PastSearchesOpenFromTheHistoryPickerTests
{
    private const string Fid = "F1";

    private const long Ltt7786 = 633608311522;

    private static readonly StationSearchResult OneStation = new(
        "Sol",
        4,
        [new StationSummary { Name = "Jameson Memorial", SystemName = "Shinrarta Dezhra", Type = "Orbis Starport", Distance = 4.2, Services = ["Market", "Black Market"] }]);

    private static readonly GalaxySearchResult TwoSystems = new(
        "LTT 7786",
        7,
        [new SystemSummary { Name = "LTT 7786", SystemAddress = Ltt7786, Distance = 0, Government = "Anarchy" }]);

    private sealed class Systems : IStarSystemService
    {
        public Task<StarSystemProfile?> ProfileAsync(long systemAddress, CancellationToken cancellationToken) =>
            Task.FromResult<StarSystemProfile?>(new StarSystemProfile { Name = $"System {systemAddress}", SystemAddress = systemAddress });

        public Task<IReadOnlyList<SystemNameMatch>> MatchNamesAsync(string typed, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<SystemNameMatch>>([]);

        public Task<PowerplayNeighbourhood> PowerplayNearAsync(string system, double lightYears, CancellationToken cancellationToken) =>
            Task.FromResult(new PowerplayNeighbourhood(0, []));
    }

    private sealed class Galaxy : IGalaxyService
    {
        public List<StationQuery> Asked { get; } = [];

        public Task<StationSearchResult> FindStationsAsync(StationQuery query, CancellationToken cancellationToken)
        {
            Asked.Add(query);
            return Task.FromResult(OneStation);
        }

        public Task<GalaxySearchResult> SearchAsync(GalaxyQuery query, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<double?> DistanceAsync(string from, string to, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<BodySearchResult> FindBodiesAsync(BodyQuery query, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<ColonisationScan> ScanForColonisationAsync(ColonisationQuery query, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<MissionStationScan> FindMissionStationsAsync(MissionStationQuery query, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<SystemBiology> SystemBiologyAsync(long systemAddress, CancellationToken cancellationToken) => throw new NotSupportedException();
    }

    private sealed class World
    {
        public World(bool entries = true)
        {
            Settings = TestSurface.Settings();
            Settings.Apply(GalaxyCapability.EnabledKey, "true", SettingsCaller.Panel);
            Registry = CapabilityRegistry.Build([GalaxyCapability.Create(Galaxy, () => "LTT 7786", Settings, searches: Board)]);

            if (!entries)
            {
                return;
            }

            var now = DateTimeOffset.UtcNow;
            SystemsSearch = Add("search_systems", new Dictionary<string, string> { ["government"] = "Anarchy" }, "LTT 7786", null, TwoSystems, now.AddHours(-3));
            StationsSearch = Add("search_stations", new Dictionary<string, string> { ["services"] = "Black Market" }, "Sol", null, OneStation, now.AddHours(-1));
            SystemEntry = Add("describe_system", new Dictionary<string, string> { ["system"] = "LTT 7786" }, "LTT 7786", Ltt7786, null, now.AddMinutes(-10));
        }

        public SettingsService Settings { get; }

        public CapabilityRegistry Registry { get; }

        public GalaxySearchBoard Board { get; } = new();

        public Galaxy Galaxy { get; } = new();

        public SearchHistory History { get; } = new("search-history.json", new MemoryFileSystem(), NullLogger<SearchHistory>.Instance);

        public SearchHistoryEntry SystemsSearch { get; } = null!;

        public SearchHistoryEntry StationsSearch { get; } = null!;

        public SearchHistoryEntry SystemEntry { get; } = null!;

        public StarSystemSurface Surface => new(
            new Systems(),
            () => new JournalLocation("LTT 7786", null, false, null) { SystemAddress = Ltt7786 },
            () => Settings.Current.Knowledge.GalaxySearch,
            () => { })
        {
            Searches = new GalaxySearchSurface(
                Registry,
                Board,
                new SavedSearchStore("saved-searches.json", new MemoryFileSystem(), NullLogger<SavedSearchStore>.Instance),
                () => Fid,
                () => [])
            {
                History = History,
            },
        };

        private SearchHistoryEntry Add(
            string tool, Dictionary<string, string> arguments, string reference, long? address, object? result, DateTimeOffset at)
        {
            var entry = SearchHistoryEntry.Of(tool, arguments, reference, address, result, at);
            History.Add(Fid, entry);

            return entry;
        }
    }

    private static (PanelView Panel, Window Window) Shown(World world, string root, bool headset = false)
    {
        var panel = new PanelView { DataContext = new PanelViewModel() };

        if (headset)
        {
            panel.Classes.Add("headset");
        }

        panel.EnableStarSystem(world.Surface);

        var window = new Window { Content = panel, Width = 1280, Height = 960 };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        panel.Tab = PanelTab.Search;
        panel.Nav.SelectRoot(root);
        Dispatcher.UIThread.RunJobs();

        return (panel, window);
    }

    private static void Press(Button button)
    {
        button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();
    }

    private static Button Named(Control root, string name) =>
        root.GetVisualDescendants().OfType<Button>().Single(button => button.Name == name);

    private static PickerPage OpenPicker(PanelView panel)
    {
        Press(Named(panel, HistoryTile.Name));

        return panel.GetVisualDescendants().OfType<PickerPage>().Single();
    }

    private static List<PickerChoice> Listed(PickerPage picker) =>
        [.. picker.GetControl<ListBox>("Choices").ItemsSource!.Cast<PickerChoice>()];

    private static void Choose(PickerPage picker, string id)
    {
        picker.GetControl<ListBox>("Choices").SelectedItem = Listed(picker).Single(choice => choice.Value == id);
        Dispatcher.UIThread.RunJobs();
        Press(picker.GetControl<Button>("AcceptButton"));
    }

    [AvaloniaTheory]
    [InlineData(SystemSearchPage.RootKey)]
    [InlineData(StarSystemPage.RootKey)]
    public void HistoryListsTheEntriesNewestFirstWithTheirLinesAndStatuses(string root)
    {
        var world = new World();
        var (panel, window) = Shown(world, root);

        var picker = OpenPicker(panel);
        var rows = Listed(picker);

        Assert.Equal([world.SystemEntry.Id, world.StationsSearch.Id, world.SystemsSearch.Id], rows.Select(row => row.Value));
        Assert.Equal(
            ["System · LTT 7786", "Stations · Black Market · near Sol", "Systems · Anarchy · near LTT 7786"],
            rows.Select(row => row.Text));
        Assert.Equal(["10 MINUTES AGO", "4 FOUND · 1 HOUR AGO", "7 FOUND · 3 HOURS AGO"], rows.Select(row => row.Status));
        Assert.Equal(SearchHistoryPicker.Help, picker.GetControl<TextBlock>("HelpText").Text);
        Assert.Equal(SearchHistoryPicker.OpenText, picker.GetControl<Button>("AcceptButton").Content);

        window.Close();
    }

    [AvaloniaFact]
    public void TheKindFacetSetToStationsShowsOnlyStationSearches()
    {
        var world = new World();
        var (panel, window) = Shown(world, SystemSearchPage.RootKey);
        var picker = OpenPicker(panel);

        picker.GetControl<Segment>("FacetBox").GetVisualDescendants().OfType<RadioButton>().ElementAt(3).IsChecked = true;
        Dispatcher.UIThread.RunJobs();

        Assert.Equal([world.StationsSearch.Id], Listed(picker).Select(row => row.Value));

        window.Close();
    }

    [AvaloniaFact]
    public void ChoosingAStationSearchDrawsItsStoredAnswerAndAgeWithoutAskingAgain()
    {
        var world = new World();
        var (panel, window) = Shown(world, SystemSearchPage.RootKey);

        Choose(OpenPicker(panel), world.StationsSearch.Id);

        var page = panel.GetVisualDescendants().OfType<StationSearchPage>().Single();
        Assert.Equal(StationSearchPage.RootKey, panel.Nav.RootKeyOf(PanelTab.Search));
        Assert.Equal(world.StationsSearch.Id, page.HistoryId);
        Assert.Equal(GalaxySearchView.Answer, page.View);
        Assert.Single(panel.GetVisualDescendants().OfType<Control>(), control => control.Classes.Contains(GalaxySearchPage.RowClass));
        Assert.Equal("ASKED 1 HOUR AGO", panel.GetVisualDescendants().OfType<TextBlock>().Single(text => text.Name == GalaxySearchPage.AskedName).Text);
        Assert.Empty(world.Galaxy.Asked);

        window.Close();
    }

    [AvaloniaFact]
    public void ChoosingASystemOpensTheSystemPageOnThatAddress()
    {
        var world = new World();
        var (panel, window) = Shown(world, StationSearchPage.RootKey);

        Choose(OpenPicker(panel), world.SystemEntry.Id);

        Assert.Equal(StarSystemPage.RootKey, panel.Nav.RootKeyOf(PanelTab.Search));
        Assert.Equal(Ltt7786, panel.GetVisualDescendants().OfType<StarSystemPage>().Single().ShownAddress);

        window.Close();
    }

    [AvaloniaFact]
    public void RefreshOnAnEntryThatLeftNearOutAsksWithTheStoredReference()
    {
        var world = new World();
        var (panel, window) = Shown(world, StationSearchPage.RootKey);

        Choose(OpenPicker(panel), world.StationsSearch.Id);
        Press(Named(panel, GalaxySearchPage.RefreshName));

        Assert.Equal("Sol", Assert.Single(world.Galaxy.Asked).ReferenceSystem);

        window.Close();
    }

    [AvaloniaFact]
    public void BackAndTheBreadcrumbLeaveThePickerWithoutOpeningAnything()
    {
        var world = new World();
        var (panel, window) = Shown(world, SystemSearchPage.RootKey);

        Press(OpenPicker(panel).GetControl<Button>("BackButton"));
        Assert.Empty(panel.GetVisualDescendants().OfType<PickerPage>());
        Assert.Equal(SystemSearchPage.RootKey, panel.Nav.RootKeyOf(PanelTab.Search));

        OpenPicker(panel);
        panel.Nav.ToRoot();
        Dispatcher.UIThread.RunJobs();

        Assert.Empty(panel.GetVisualDescendants().OfType<PickerPage>());
        Assert.Equal(SystemSearchPage.RootKey, panel.Nav.RootKeyOf(PanelTab.Search));
        Assert.Null(panel.GetVisualDescendants().OfType<SystemSearchPage>().Single().HistoryId);

        window.Close();
    }

    [AvaloniaFact]
    public void HistoryIsDisabledWhileThereAreNoEntries()
    {
        var world = new World(entries: false);
        var (panel, window) = Shown(world, SystemSearchPage.RootKey);

        Assert.False(Named(panel, HistoryTile.Name).IsEnabled);

        world.History.Add(Fid, SearchHistoryEntry.Of("describe_system", new Dictionary<string, string>(), "LTT 7786", Ltt7786, null, DateTimeOffset.UtcNow));
        Dispatcher.UIThread.RunJobs();

        Assert.True(Named(panel, HistoryTile.Name).IsEnabled);

        window.Close();
    }

    [AvaloniaFact]
    public void TheHeadsetShowsHistoryAndOpensTheSamePicker()
    {
        var world = new World();
        var (panel, window) = Shown(world, SystemSearchPage.RootKey, headset: true);

        Assert.True(Named(panel, HistoryTile.Name).Height >= 44);
        Assert.Equal(3, Listed(OpenPicker(panel)).Count);

        window.Close();
    }
}
