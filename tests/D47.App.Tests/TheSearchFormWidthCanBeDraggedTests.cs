using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using D47.App.Panel;
using D47.App.Theming;
using D47.Core;
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

/// <summary>The boundary between the Search form and its answer can be dragged and is remembered (#1543).</summary>
[Trait("Category", "Integration")]
public sealed class TheSearchFormWidthCanBeDraggedTests
{
    private const string Here = "LTT 7786";

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
        public Task<BodySearchResult> FindBodiesAsync(BodyQuery query, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<StationSearchResult> FindStationsAsync(StationQuery query, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<GalaxySearchResult> SearchAsync(GalaxyQuery query, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<double?> DistanceAsync(string from, string to, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<ColonisationScan> ScanForColonisationAsync(ColonisationQuery query, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<MissionStationScan> FindMissionStationsAsync(MissionStationQuery query, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<IReadOnlyList<BodySummary>> SystemBodiesAsync(long systemAddress, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<SystemBiology> SystemBiologyAsync(long systemAddress, CancellationToken cancellationToken) => throw new NotSupportedException();
    }

    private static (PanelView Panel, Window Window, ViewStateStore Store) Shown(string root, bool headset, bool granted, double? remembered = null)
    {
        var settings = TestSurface.Settings();
        settings.Apply(GalaxyCapability.EnabledKey, "true", SettingsCaller.Panel);
        var board = new GalaxySearchBoard();
        var registry = CapabilityRegistry.Build([GalaxyCapability.Create(new Galaxy(), () => Here, settings, searches: board)]);

        var surface = new StarSystemSurface(
            new Systems(),
            () => new JournalLocation(Here, null, false, null),
            () => settings.Current.Knowledge.GalaxySearch,
            () => { })
        {
            Searches = new GalaxySearchSurface(
                registry,
                board,
                new SavedSearchStore("saved-searches.json", new MemoryFileSystem(), NullLogger<SavedSearchStore>.Instance),
                () => "F1",
                () => [])
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

        var paths = new AppPaths(TempFolders.Create("d47-search-form-width"));
        var store = new ViewStateStore(paths, new DiskFileSystem(), NullLogger<ViewStateStore>.Instance);

        if (remembered is { } width)
        {
            store.Save(new ViewState { SearchFormWidth = width });
        }

        if (granted)
        {
            panel.EnableDraggablePanes(new PaneWidthMemory(store));
        }

        panel.EnableStarSystem(surface);

        var window = new Window { Content = panel, Width = 1280, Height = 960 };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        panel.Tab = PanelTab.Search;
        panel.Nav.SelectRoot(root);
        Dispatcher.UIThread.RunJobs();

        return (panel, window, store);
    }

    private static double FormColumn(PanelView panel) =>
        panel.GetVisualDescendants().OfType<GalaxySearchPage>().Single()
            .GetVisualDescendants().OfType<Grid>()
            .First(grid => grid.ColumnDefinitions.Count >= 2 && grid.Margin.Top == 16)
            .ColumnDefinitions[0].ActualWidth;

    private static IReadOnlyList<GridSplitter> Splitters(PanelView panel) =>
        [.. panel.GetVisualDescendants().OfType<GalaxySearchPage>().Single().GetVisualDescendants().OfType<GridSplitter>()];

    [AvaloniaFact]
    public void AGrantedSearchPageHasOneSplitterAndAFormOfThreeHundredAndSixty()
    {
        using var look = AppLook.Put();

        var (panel, window, _) = Shown(SystemSearchPage.RootKey, headset: false, granted: true);

        Assert.Single(Splitters(panel));
        Assert.Equal(360, FormColumn(panel));

        window.Close();
    }

    [AvaloniaTheory]
    [InlineData(false, false)]
    [InlineData(true, true)]
    public void WithoutAGrantOnTheHeadsetTheFormIsFixedWithNoSplitter(bool headset, bool granted)
    {
        using var look = AppLook.Put();

        var (panel, window, _) = Shown(SystemSearchPage.RootKey, headset, granted);

        Assert.Empty(Splitters(panel));

        if (!headset)
        {
            var grid = panel.GetVisualDescendants().OfType<GalaxySearchPage>().Single()
                .GetVisualDescendants().OfType<Grid>().First(grid => grid.ColumnDefinitions.Count >= 2 && grid.Margin.Top == 16);
            Assert.Equal(new GridLength(360), grid.ColumnDefinitions[0].Width);
        }

        window.Close();
    }

    [AvaloniaTheory]
    [InlineData(SystemSearchPage.RootKey)]
    [InlineData(SignalSearchPage.RootKey)]
    public void ARememberedWidthLaysTheFormOutAtIt(string root)
    {
        using var look = AppLook.Put();

        var (panel, window, _) = Shown(root, headset: false, granted: true, remembered: 500);

        Assert.Equal(500, FormColumn(panel));

        window.Close();
    }

    [AvaloniaFact]
    public void AWidthPastTheLimitIsHeldAtTheMaximum()
    {
        using var look = AppLook.Put();

        var (panel, window, _) = Shown(SystemSearchPage.RootKey, headset: false, granted: true, remembered: 5000);

        Assert.Equal(720, FormColumn(panel));

        window.Close();
    }

    [Theory]
    [InlineData(5000, 720)]
    [InlineData(double.NaN, 360)]
    [InlineData(-1, 360)]
    [InlineData(0, 360)]
    [InlineData(double.PositiveInfinity, 360)]
    [InlineData(100, 300)]
    public void AnUnusableWidthFallsBackOrIsClamped(double remembered, double expected) =>
        Assert.Equal(expected, GalaxySearchPage.FormWidth(remembered));

    [Fact]
    public void NoRememberedWidthIsTheDefault() =>
        Assert.Equal(360, GalaxySearchPage.FormWidth(null));

    [AvaloniaFact]
    public void ACompletedDragRecordsTheFormWidth()
    {
        using var look = AppLook.Put();

        var (panel, window, store) = Shown(SystemSearchPage.RootKey, headset: false, granted: true);
        var splitter = Splitters(panel).Single();
        var column = panel.GetVisualDescendants().OfType<GalaxySearchPage>().Single()
            .GetVisualDescendants().OfType<Grid>().First(grid => grid.ColumnDefinitions.Count == 3 && grid.Margin.Top == 16).ColumnDefinitions[0];

        column.Width = new GridLength(450);
        Dispatcher.UIThread.RunJobs();
        splitter.RaiseEvent(new VectorEventArgs { RoutedEvent = Thumb.DragCompletedEvent, Vector = default });

        Assert.Equal(450, store.Load().SearchFormWidth);

        window.Close();
    }

    [AvaloniaTheory]
    [InlineData(SystemSearchPage.RootKey)]
    [InlineData(StationSearchPage.RootKey)]
    [InlineData(OutfittingSearchPage.RootKey)]
    [InlineData(BodySearchPage.RootKey)]
    [InlineData(SignalSearchPage.RootKey)]
    public void NoFormControlClipsAtTheMinimumWidth(string root)
    {
        using var look = AppLook.Put();

        var (panel, window, _) = Shown(root, headset: false, granted: true, remembered: 300);
        var page = panel.GetVisualDescendants().OfType<GalaxySearchPage>().Single();
        var grid = page.GetVisualDescendants().OfType<Grid>().First(grid => grid.ColumnDefinitions.Count == 3 && grid.Margin.Top == 16);
        var form = grid.Children[0];

        Assert.Equal(300, grid.ColumnDefinitions[0].ActualWidth);

        foreach (var control in form.GetVisualDescendants().OfType<Control>().Where(control => control.IsVisible && control.Bounds.Width > 0))
        {
            var right = control.TranslatePoint(new Point(control.Bounds.Width, 0), grid)?.X ?? 0;
            Assert.True(right <= 300.5, $"{control.GetType().Name} {control.Name} ends at {right}");
        }

        window.Close();
    }
}
