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

/// <summary>Pressing a Stations or Bodies search result opens its system on the section it came from.</summary>
public sealed class AStationOrBodyResultOpensItsSystemOnItsSectionTests
{
    private const string Fid = "F1";
    private const long Shinrarta = 3932277478106;
    private const long Alioth = 1109989017963;

    private static readonly DateTimeOffset AskedAt = new(2026, 10, 10, 19, 42, 0, TimeSpan.Zero);

    private static readonly StationSearchResult Stations = new(
        "LTT 7786",
        3,
        [
            new StationSummary { Name = "Jameson Memorial", SystemName = "Shinrarta Dezhra", SystemAddress = Shinrarta, Type = "Orbis Starport" },
            new StationSummary { Name = "Ray Gateway", SystemName = "Alioth", SystemAddress = Alioth, Type = "Outpost" },
            new StationSummary { Name = "Nowhere Base", SystemName = "Unknown", Type = "Outpost" },
        ]);

    private static readonly BodySearchResult Bodies = new(
        "LTT 7786",
        3,
        [
            new BodySummary { Name = "Shinrarta Dezhra 4 a", SystemName = "Shinrarta Dezhra", SystemAddress = Shinrarta, Subtype = "Rocky body" },
            new BodySummary { Name = "Alioth 1", SystemName = "Alioth", SystemAddress = Alioth, Subtype = "High metal content world" },
            new BodySummary { Name = "Lost 1", SystemName = "Unknown", Subtype = "Rocky body" },
        ]);

    private sealed class Systems : StubStarSystemService
    {
        public override Task<StarSystemProfile?> ProfileAsync(long systemAddress, CancellationToken cancellationToken) =>
            Task.FromResult<StarSystemProfile?>(new StarSystemProfile { Name = $"System {systemAddress}", SystemAddress = systemAddress });

        public override Task<IReadOnlyList<SystemNameMatch>> MatchNamesAsync(string typed, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<SystemNameMatch>>([]);

        public override Task<IReadOnlyList<SystemNameMatch>> PrefixNamesAsync(string typed, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<SystemNameMatch>>([]);

        public override Task<PowerplayNeighbourhood> PowerplayNearAsync(string system, double lightYears, CancellationToken cancellationToken) =>
            Task.FromResult(new PowerplayNeighbourhood(0, []));
    }

    private sealed class Galaxy : StubGalaxyService
    {

    }

    private static (PanelView Panel, Window Window) Shown(string root, bool headset = false)
    {
        var settings = TestSurface.Settings();
        var store = new SavedSearchStore("saved-searches.json", new MemoryFileSystem(), NullLogger<SavedSearchStore>.Instance);
        var board = new GalaxySearchBoard();
        var registry = CapabilityRegistry.Build([GalaxyCapability.Create(new Galaxy(), () => "LTT 7786", settings, searches: board)]);

        board.Post(new GalaxySearchPosting(GalaxySearchKind.Stations, new Dictionary<string, string>(StringComparer.Ordinal), Stations.Reference, Stations, "Stations matched.", AskedAt));
        board.Post(new GalaxySearchPosting(GalaxySearchKind.Bodies, new Dictionary<string, string>(StringComparer.Ordinal), Bodies.Reference, Bodies, "Bodies matched.", AskedAt));

        var panel = new PanelView { DataContext = new PanelViewModel() };

        if (headset)
        {
            panel.Classes.Add("headset");
        }

        panel.EnableStarSystem(new StarSystemSurface(
            new Systems(),
            () => new JournalLocation("LTT 7786", null, false, null))
        {
            Searches = new GalaxySearchSurface(registry, board, store, () => Fid, () => [])
            {
                PrefixNames = new Systems().PrefixNamesAsync,
                SuggestionPause = TimeSpan.Zero,
            },
        });

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
        Dispatcher.UIThread.RunJobs();
    }

    private static Button Row(PanelView panel, string name) =>
        panel.GetVisualDescendants().OfType<Button>().Single(button => button.Classes.Contains(GalaxySearchPage.RowClass) && (string?)button.Tag == name);

    private static int Section(PanelView panel) =>
        panel.GetVisualDescendants().OfType<Segment>().Single(segment => Avalonia.Automation.AutomationProperties.GetName(segment) == "Sections").SelectedIndex;

    private static string LastKey(PanelView panel) => panel.Nav.TrailOf(PanelTab.Search)[^1].Key;

    [AvaloniaFact]
    public void AStationResultOpensItsSystemOnStations()
    {
        var (panel, window) = Shown(StationSearchPage.RootKey);

        Press(Row(panel, "Jameson Memorial"));

        Assert.Equal(SearchFurnishing.FoundKeyPrefix + Shinrarta + SearchFurnishing.StationsSuffix, LastKey(panel));
        Assert.Equal(1, Section(panel));

        window.Close();
    }

    [AvaloniaFact]
    public void ABodyResultOpensItsSystemOnBodies()
    {
        var (panel, window) = Shown(BodySearchPage.RootKey);

        Press(Row(panel, "Alioth 1"));

        Assert.Equal(SearchFurnishing.FoundKeyPrefix + Alioth + SearchFurnishing.BodiesSuffix, LastKey(panel));
        Assert.Equal(2, Section(panel));

        window.Close();
    }

    [AvaloniaFact]
    public void ASecondPressReplacesTheOpenSystemRatherThanStackingAnother()
    {
        var (panel, window) = Shown(StationSearchPage.RootKey);

        Press(Row(panel, "Jameson Memorial"));
        panel.Nav.SelectRoot(StationSearchPage.RootKey);
        Dispatcher.UIThread.RunJobs();
        Press(Row(panel, "Ray Gateway"));

        Assert.Equal(2, panel.Nav.TrailOf(PanelTab.Search).Count);
        Assert.Equal(SearchFurnishing.FoundKeyPrefix + Alioth + SearchFurnishing.StationsSuffix, LastKey(panel));

        window.Close();
    }

    [AvaloniaFact]
    public void AResultWithNoSystemAddressOpensNothingOnTheHeadsetOrTheDesktop()
    {
        foreach (var headset in new[] { false, true })
        {
            var (panel, window) = Shown(StationSearchPage.RootKey, headset);

            Assert.DoesNotContain(panel.GetVisualDescendants().OfType<Button>(), button => (string?)button.Tag == "Nowhere Base");
            Press(Row(panel, "Ray Gateway"));
            Assert.Equal(SearchFurnishing.FoundKeyPrefix + Alioth + SearchFurnishing.StationsSuffix, LastKey(panel));

            window.Close();
        }

        var (bodies, bodiesWindow) = Shown(BodySearchPage.RootKey);
        Assert.DoesNotContain(bodies.GetVisualDescendants().OfType<Button>(), button => (string?)button.Tag == "Lost 1");
        bodiesWindow.Close();
    }
}
