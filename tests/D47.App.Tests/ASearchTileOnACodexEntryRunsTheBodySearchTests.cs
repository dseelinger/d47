using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
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

/// <summary>A codex entry page's Search tile opens Search › Bodies with that entry filled in and the search running (#1822).</summary>
[Trait("Category", "Integration")]
public sealed class ASearchTileOnACodexEntryRunsTheBodySearchTests
{
    private const long Listed = 2440402;

    private const string ListedName = "Frutexa Flammasis - Lime";

    private const long Unlisted = 99999999;

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
        public List<BodyQuery> Asked { get; } = [];

        public Task<BodySearchResult> FindBodiesAsync(BodyQuery query, CancellationToken cancellationToken)
        {
            Asked.Add(query);

            return Task.FromResult(new BodySearchResult("LTT 7786", 0, []));
        }

        public Task<StationSearchResult> FindStationsAsync(StationQuery query, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<GalaxySearchResult> SearchAsync(GalaxyQuery query, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<double?> DistanceAsync(string from, string to, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<ColonisationScan> ScanForColonisationAsync(ColonisationQuery query, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<MissionStationScan> FindMissionStationsAsync(MissionStationQuery query, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<IReadOnlyList<BodySummary>> SystemBodiesAsync(long systemAddress, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<SystemBiology> SystemBiologyAsync(long systemAddress, CancellationToken cancellationToken) => throw new NotSupportedException();
    }

    private static (PanelView Panel, Window Window, Galaxy Galaxy) Shown(bool lookups, long entry, string name, bool headset = false)
    {
        var settings = TestSurface.Settings();
        settings.Apply(GalaxyCapability.EnabledKey, lookups ? "true" : "false", SettingsCaller.Panel);
        var board = new GalaxySearchBoard();
        var galaxy = new Galaxy();
        var store = new SavedSearchStore("saved-searches.json", new MemoryFileSystem(), NullLogger<SavedSearchStore>.Instance);
        var registry = CapabilityRegistry.Build([GalaxyCapability.Create(galaxy, () => "LTT 7786", settings, searches: board)]);

        var surface = new StarSystemSurface(
            new Systems(),
            () => new JournalLocation("LTT 7786", null, false, null),
            () => settings.Current.Knowledge.GalaxySearch,
            () => { })
        {
            Searches = new GalaxySearchSurface(registry, board, store, () => "F1", () => [])
            {
                PrefixNames = new Systems().PrefixNamesAsync,
                SuggestionPause = TimeSpan.Zero,
            },
        };

        var panel = new PanelView { DataContext = new PanelViewModel() };

        if (headset)
        {
            panel.Classes.Add("headset");
        }

        panel.EnableStarSystem(surface);
        panel.EnableCodex(() => null, () => HistoryState.Done, () => DateTimeOffset.UtcNow);

        var window = new Window { Content = panel, Width = 1280, Height = 960 };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        panel.Tab = PanelTab.Commander;
        Assert.True(panel.Nav.GoTo(
            PanelTab.Commander,
            LedgersPage.Root,
            new NavCrumb(CodexPage.PageKey, "Codex"),
            CodexPage.RegionCrumb(1),
            CodexPage.EntryCrumb(1, entry, name)));
        Dispatcher.UIThread.RunJobs();

        return (panel, window, galaxy);
    }

    private static Button? Tile(Control root) =>
        root.GetVisualDescendants().OfType<Button>().SingleOrDefault(button => button.Name == "CodexSearch");

    [AvaloniaTheory]
    [InlineData(false)]
    [InlineData(true)]
    public void PressingSearchOpensBodiesWithTheEntryFilledAndTheSearchRun(bool headset)
    {
        var (panel, window, galaxy) = Shown(lookups: true, Listed, ListedName, headset);

        var tile = Assert.IsType<Button>(Tile(panel));
        Assert.True(tile.IsEnabled);
        tile.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();

        var asked = Assert.Single(galaxy.Asked);
        Assert.Equal("Frutexa Flammasis", asked.CodexEntry);
        Assert.Equal(PanelTab.Search, panel.Tab);
        Assert.Equal(BodySearchPage.RootKey, panel.Nav.Root.Key);

        var page = panel.GetVisualDescendants().OfType<BodySearchPage>().Single();
        var form = page.FormArguments();
        Assert.Equal(ListedName, form["codex_entry"]);
        Assert.False(form.TryGetValue("near", out var near) && !string.IsNullOrEmpty(near));

        window.Close();
    }

    [AvaloniaFact]
    public void WithLookupsOffTheTileIsDisabledAndSaysWhy()
    {
        var (panel, window, galaxy) = Shown(lookups: false, Listed, ListedName);

        var tile = Assert.IsType<Button>(Tile(panel));
        Assert.False(tile.IsEnabled);
        Assert.Equal(GalaxySearchPage.LookupsOffTip, ToolTip.GetTip(tile));
        Assert.Empty(galaxy.Asked);

        window.Close();
    }

    [AvaloniaFact]
    public void AnEntryTheCodexTableDoesNotListHasNoSearchTile()
    {
        var (panel, window, _) = Shown(lookups: true, Unlisted, "Unknown entry");

        Assert.Null(Tile(panel));

        window.Close();
    }
}
