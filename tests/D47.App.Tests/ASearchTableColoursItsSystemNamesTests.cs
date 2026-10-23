using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
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

/// <summary>The Stations and Bodies tables draw the current system in cyan and every other system in the accent colour (#1540).</summary>
public sealed class ASearchTableColoursItsSystemNamesTests
{
    private const string Here = "LTT 7786";

    private static readonly DateTimeOffset AskedAt = new(2026, 10, 23, 19, 42, 0, TimeSpan.Zero);

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

    private static (PanelView Panel, Window Window) Shown(GalaxySearchPosting posting, string root, bool headset)
    {
        var settings = TestSurface.Settings();
        settings.Apply(GalaxyCapability.EnabledKey, "true", SettingsCaller.Panel);
        var board = new GalaxySearchBoard();
        var registry = CapabilityRegistry.Build([GalaxyCapability.Create(new Galaxy(), () => Here, settings, searches: board)]);
        board.Post(posting);

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

        panel.EnableStarSystem(surface);

        var window = new Window { Content = panel, Width = 1280, Height = 960 };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        panel.Tab = PanelTab.Search;
        panel.Nav.SelectRoot(root);
        Dispatcher.UIThread.RunJobs();

        return (panel, window);
    }

    private static Color Published(string key) => ((ISolidColorBrush)Application.Current!.Resources[key]!).Color;

    private static Color Ink(TextBlock text) => ((ISolidColorBrush)text.Foreground!).Color;

    private static TextBlock Text(Control root, string text) =>
        root.GetVisualDescendants().OfType<TextBlock>().Single(block => block.Text == text);

    private static StationSummary Station(string name, string system) =>
        new() { Name = name, SystemName = system, Type = "Outpost", ControllingFaction = "Faction", Distance = 5, MediumPads = 1, UpdatedAt = AskedAt };

    private static BodySummary Body(string name, string system) =>
        new() { Name = name, SystemName = system, Subtype = "Rocky body", Distance = 5, DistanceToArrival = 100 };

    [AvaloniaTheory]
    [InlineData(false)]
    [InlineData(true)]
    public void TheStationsTableColoursItsSystemNames(bool headset)
    {
        using var look = AppLook.Put();

        var result = new StationSearchResult(Here, 2, [Station("Ray Gateway", "ltt 7786"), Station("Jameson Memorial", "Shinrarta Dezhra")]);
        var (panel, window) = Shown(
            new GalaxySearchPosting(GalaxySearchKind.Stations, new Dictionary<string, string>(StringComparer.Ordinal), Here, result, "Stations matched.", AskedAt),
            StationSearchPage.RootKey,
            headset);

        Assert.Equal(Published(ThemeManager.CyanKey), Ink(Text(panel, "ltt 7786")));
        Assert.Equal(Published(ThemeManager.AKey), Ink(Text(panel, "Shinrarta Dezhra")));

        window.Close();
    }

    [AvaloniaTheory]
    [InlineData(false)]
    [InlineData(true)]
    public void TheBodiesTableColoursItsSystemNames(bool headset)
    {
        using var look = AppLook.Put();

        var result = new BodySearchResult(Here, 2, [Body("LTT 7786 1", "ltt 7786"), Body("Alioth 1", "Alioth")]);
        var (panel, window) = Shown(
            new GalaxySearchPosting(GalaxySearchKind.Bodies, new Dictionary<string, string>(StringComparer.Ordinal), Here, result, "Bodies matched.", AskedAt),
            BodySearchPage.RootKey,
            headset);

        Assert.Equal(Published(ThemeManager.CyanKey), Ink(Text(panel, "ltt 7786")));
        Assert.Equal(Published(ThemeManager.AKey), Ink(Text(panel, "Alioth")));

        window.Close();
    }
}
