using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
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

/// <summary>A station pressed on Search › System, or found by a Stations search, opens a page of its own under its system.</summary>
public sealed class AStationOpensItsOwnPageUnderItsSystemTests
{
    private const string Fid = "F1";
    private const long Address = 7786;
    private const long Market = 3700001;

    private static readonly DateTimeOffset AskedAt = new(2026, 10, 10, 19, 42, 0, TimeSpan.Zero);

    private static readonly StarSystemProfile Profile = new()
    {
        Name = "LTT 7786",
        SystemAddress = Address,
        Stations =
        [
            new StationProfile
            {
                Name = "Smith Port",
                MarketId = Market,
                Kind = StationKind.Starport,
                Type = "Orbis Starport",
                DistanceToArrival = 288,
                Services = ["Market", "Outfitting"],
                Market = new StationMarket(
                    [
                        new MarketCommodity("Gold", "gold", "Metals", 9000, 0, 12, 0),
                        new MarketCommodity("Algae", "algae", "Foods", 100, 80, 500, 40),
                    ],
                    AskedAt),
                Outfitting = new StationOutfitting([new StationModule("Frame Shift Drive", "fsd", "Core internal", 5, "A", null)], AskedAt),
            },
        ],
    };

    private static readonly StationSearchResult Found = new(
        "LTT 7786",
        1,
        [new StationSummary { Name = "Smith Port", SystemName = "LTT 7786", SystemAddress = Address, MarketId = Market, Type = "Orbis Starport" }]);

    private sealed class Systems : IStarSystemService
    {
        public int Asked { get; private set; }

        public Task<StarSystemProfile?> ProfileAsync(long systemAddress, CancellationToken cancellationToken)
        {
            Asked++;

            return Task.FromResult<StarSystemProfile?>(systemAddress == Address ? Profile : null);
        }

        public Task<IReadOnlyList<SystemNameMatch>> MatchNamesAsync(string typed, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<SystemNameMatch>>([]);

        public Task<IReadOnlyList<SystemNameMatch>> PrefixNamesAsync(string typed, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<SystemNameMatch>>([]);

        public Task<PowerplayNeighbourhood> PowerplayNearAsync(string system, double lightYears, CancellationToken cancellationToken) =>
            Task.FromResult(new PowerplayNeighbourhood(0, []));
    }

    private sealed class Galaxy : IGalaxyService
    {
        public Task<StationSearchResult> FindStationsAsync(StationQuery query, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<GalaxySearchResult> SearchAsync(GalaxyQuery query, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<double?> DistanceAsync(string from, string to, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<BodySearchResult> FindBodiesAsync(BodyQuery query, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<ColonisationScan> ScanForColonisationAsync(ColonisationQuery query, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<MissionStationScan> FindMissionStationsAsync(MissionStationQuery query, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<IReadOnlyList<BodySummary>> SystemBodiesAsync(long systemAddress, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<SystemBiology> SystemBiologyAsync(long systemAddress, CancellationToken cancellationToken) => throw new NotSupportedException();
    }

    private static (PanelView Panel, Window Window, Systems Systems) Shown(string root, bool headset = false)
    {
        var settings = TestSurface.Settings();
        settings.Apply(GalaxyCapability.EnabledKey, "true", SettingsCaller.Panel);
        var store = new SavedSearchStore("saved-searches.json", new MemoryFileSystem(), NullLogger<SavedSearchStore>.Instance);
        var board = new GalaxySearchBoard();
        var registry = CapabilityRegistry.Build([GalaxyCapability.Create(new Galaxy(), () => "LTT 7786", settings, searches: board)]);

        board.Post(new GalaxySearchPosting(GalaxySearchKind.Stations, new Dictionary<string, string>(StringComparer.Ordinal), Found.Reference, Found, "Stations matched.", AskedAt));

        var panel = new PanelView { DataContext = new PanelViewModel() };

        if (headset)
        {
            panel.Classes.Add("headset");
        }

        var systems = new Systems();

        panel.EnableStarSystem(new StarSystemSurface(
            systems,
            () => new JournalLocation("LTT 7786", null, false, null) { SystemAddress = Address },
            () => settings.Current.Knowledge.GalaxySearch,
            () => { })
        {
            Searches = new GalaxySearchSurface(registry, board, store, () => Fid, () => [])
            {
                PrefixNames = systems.PrefixNamesAsync,
                SuggestionPause = TimeSpan.Zero,
            },
        });

        var window = new Window { Content = panel, Width = 1280, Height = 960 };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        panel.Tab = PanelTab.Search;
        panel.Nav.SelectRoot(root);
        Dispatcher.UIThread.RunJobs();

        return (panel, window, systems);
    }

    private static void Until(Func<bool> done)
    {
        var stop = DateTime.UtcNow.AddSeconds(10);

        while (!done() && DateTime.UtcNow < stop)
        {
            Dispatcher.UIThread.RunJobs();
            Thread.Sleep(5);
        }

        Dispatcher.UIThread.RunJobs();
        Assert.True(done());
    }

    private static void Enter(Control row)
    {
        row.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Enter, Source = row });
        Dispatcher.UIThread.RunJobs();
    }

    private static Segment Sections(Control within) =>
        within.GetVisualDescendants().OfType<Segment>().Single(segment => Avalonia.Automation.AutomationProperties.GetName(segment) == "Sections");

    private static void Choose(Segment segment, int index)
    {
        segment.GetVisualDescendants().OfType<RadioButton>().ElementAt(index).IsChecked = true;
        Dispatcher.UIThread.RunJobs();
    }

    private static Border Block(Control within, string name) =>
        within.GetVisualDescendants().OfType<Border>().Single(border => border.Name == name);

    private static IReadOnlyList<string> Keys(PanelView panel) => [.. panel.Nav.TrailOf(PanelTab.Search).Select(crumb => crumb.Key)];

    private static SystemStationPage Page(PanelView panel) => panel.GetVisualDescendants().OfType<SystemStationPage>().Single();

    private static StarSystemSurface Surface(Systems? systems = null) => new(systems ?? new Systems(), () => null, () => true);

    private static SystemStationPage Drawn(out Window window, StarSystemProfile? profile = null)
    {
        var page = new SystemStationPage(Surface(), Address, Market, "Smith Port", profile ?? Profile);
        window = new Window { Content = page, Width = 1280, Height = 960 };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        return page;
    }

    [AvaloniaFact]
    public void AStationPressedOnTheSystemsStationsDrillsItsPageUnderTheSystemOnTheHeadsetAndTheDesktop()
    {
        foreach (var headset in new[] { false, true })
        {
            var (panel, window, systems) = Shown(StarSystemPage.RootKey, headset);

            Until(() => panel.GetVisualDescendants().OfType<Segment>().Any());
            Choose(Sections(panel), 1);

            var row = panel.GetVisualDescendants().OfType<Border>()
                .Single(border => border.Classes.Contains(StarSystemPage.StationRowClass)
                    && Avalonia.Automation.AutomationProperties.GetName(border) == "Smith Port");
            Assert.True(row.MinHeight >= 44);
            Enter(row);
            Until(() => panel.GetVisualDescendants().OfType<SystemStationPage>().Any(page => page.Station is not null));

            Assert.Equal([StarSystemPage.RootKey, SystemStationPage.Key(Address, Market)], Keys(panel));
            Assert.Equal("Smith Port", Page(panel).Station!.Name);
            Assert.Equal(1, systems.Asked);

            window.Close();
        }
    }

    [AvaloniaFact]
    public void AStationSearchResultOpensItsSystemWithTheStationsPageUnderIt()
    {
        var (panel, window, _) = Shown(StationSearchPage.RootKey);

        var row = panel.GetVisualDescendants().OfType<Button>()
            .Single(button => button.Classes.Contains(GalaxySearchPage.RowClass) && (string?)button.Tag == "Smith Port");
        row.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(
            [StationSearchPage.RootKey, SearchFurnishing.FoundKeyPrefix + Address + SearchFurnishing.StationsSuffix, SystemStationPage.Key(Address, Market)],
            Keys(panel));

        Until(() => panel.GetVisualDescendants().OfType<SystemStationPage>().Any(page => page.Station is not null));

        window.Close();
    }

    [AvaloniaFact]
    public void PressingTheMarketBlockOnTheOverviewShowsTheMarketSection()
    {
        var page = Drawn(out var window);

        Assert.Equal(["Overview", "Market 2", "Outfitting 1", "Shipyard 0"], Sections(page).ItemsSource!.Cast<string>());
        Assert.Equal(SystemStationPage.Section.Overview, page.Showing);

        var market = Block(page, "TradeMarket");
        Assert.Contains(D47.App.Theming.ListRow.OpensClass, market.Classes);
        Assert.True(market.MinHeight >= 44);
        Enter(market);

        Assert.Equal(SystemStationPage.Section.Market, page.Showing);
        Assert.Contains(
            page.GetVisualDescendants().OfType<TextBlock>(),
            block => block.Name == "MarketReported" && block.Text!.StartsWith("Reported 2026-10-10", StringComparison.Ordinal));
        Assert.Contains(page.GetVisualDescendants().OfType<TextBlock>(), block => block.Text!.Contains(SystemStationPage.PricesText, StringComparison.Ordinal));

        window.Close();
    }

    [AvaloniaFact]
    public void AStationWithNoShipyardDrawsAReadOnlyBlockAndASentenceInItsSection()
    {
        var page = Drawn(out var window);

        var shipyard = Block(page, "TradeShipyard");
        Assert.DoesNotContain(D47.App.Theming.ListRow.OpensClass, shipyard.Classes);
        Assert.Contains(shipyard.GetVisualDescendants().OfType<TextBlock>(), block => block.Text == SystemStationPage.NoneReported);

        page.Show(SystemStationPage.Section.Shipyard);
        Dispatcher.UIThread.RunJobs();

        Assert.Contains(page.GetVisualDescendants().OfType<TextBlock>(), block => block.Text == SystemStationPage.NoneText("shipyard", "Smith Port"));

        window.Close();
    }

    [AvaloniaFact]
    public void AStationPageBuiltFromItsKeyAloneFetchesTheSystem()
    {
        var systems = new Systems();
        var page = new SystemStationPage(Surface(systems), Address, Market, "Smith Port");
        var window = new Window { Content = page, Width = 1280, Height = 960 };
        window.Show();

        Until(() => page.Station is not null);
        Assert.Equal(1, systems.Asked);

        window.Close();
    }
}
