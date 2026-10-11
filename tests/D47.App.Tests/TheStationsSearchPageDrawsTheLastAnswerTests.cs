using Avalonia.Controls;
using Avalonia.Headless;
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

/// <summary>Search › Stations runs search_stations from its form and draws the last answer, however it was asked (#1081).</summary>
public sealed class TheStationsSearchPageDrawsTheLastAnswerTests
{
    private const string Fid = "F1";

    private static readonly DateTimeOffset AskedAt = new(2026, 10, 10, 19, 42, 0, TimeSpan.Zero);

    private static readonly StationSearchResult TwoStations = new(
        "LTT 7786",
        2,
        [
            new StationSummary
            {
                Name = "Jameson Memorial",
                SystemName = "Shinrarta Dezhra",
                Type = "Orbis Starport",
                Distance = 4.2,
                DistanceToArrival = 1200,
                LargePads = 2,
                MediumPads = 4,
                ControllingFaction = "Pilots Federation Local Branch",
                Services = ["Market", "Black Market", "Interstellar Factors Contact", "Shipyard"],
                UpdatedAt = AskedAt,
            },
            new StationSummary
            {
                Name = "Ray Gateway",
                SystemName = "Alioth",
                Type = "Outpost",
                Distance = 9.9,
                DistanceToArrival = 40,
                MediumPads = 2,
                Services = ["Market", "Refuel", "Black Market"],
            },
        ]);

    private sealed class Systems : IStarSystemService
    {
        public Task<StarSystemProfile?> ProfileAsync(long systemAddress, CancellationToken cancellationToken) =>
            Task.FromResult<StarSystemProfile?>(null);

        public Task<IReadOnlyList<SystemNameMatch>> MatchNamesAsync(string typed, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<SystemNameMatch>>([]);

        public Task<PowerplayNeighbourhood> PowerplayNearAsync(string system, double lightYears, CancellationToken cancellationToken) =>
            Task.FromResult(new PowerplayNeighbourhood(0, []));
    }

    /// <summary>The service behind the real search_stations tool, recording the query it was given.</summary>
    private sealed class Galaxy : IGalaxyService
    {
        public List<StationQuery> Asked { get; } = [];

        public Task<StationSearchResult> FindStationsAsync(StationQuery query, CancellationToken cancellationToken)
        {
            Asked.Add(query);
            return Task.FromResult(TwoStations);
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
        public World(bool lookups)
        {
            Settings = TestSurface.Settings();
            Settings.Apply(GalaxyCapability.EnabledKey, lookups ? "true" : "false", SettingsCaller.Panel);
            Store = new SavedSearchStore("saved-searches.json", new MemoryFileSystem(), NullLogger<SavedSearchStore>.Instance);

            Registry = CapabilityRegistry.Build([GalaxyCapability.Create(Galaxy, () => "LTT 7786", Settings, searches: Board)]);
        }

        public SettingsService Settings { get; }

        public SavedSearchStore Store { get; }

        public CapabilityRegistry Registry { get; }

        public GalaxySearchBoard Board { get; } = new();

        public Galaxy Galaxy { get; } = new();

        public StarSystemSurface Surface => new(
            new Systems(),
            () => new JournalLocation("LTT 7786", null, false, null),
            () => Settings.Current.Knowledge.GalaxySearch,
            () => { })
        {
            Searches = new GalaxySearchSurface(Registry, Board, Store, () => Fid, () => []),
        };
    }

    private static GalaxySearchPosting Posting(IReadOnlyDictionary<string, string> arguments, StationSearchResult result) =>
        new(
            GalaxySearchKind.Stations,
            new Dictionary<string, string>(arguments, StringComparer.Ordinal),
            result.Reference,
            result,
            "Stations matched.",
            AskedAt);

    private static (PanelView Panel, Window Window) Shown(World world)
    {
        var panel = new PanelView { DataContext = new PanelViewModel() };

        panel.EnableStarSystem(world.Surface);

        var window = new Window { Content = panel, Width = 1280, Height = 960 };
        window.Bind(Window.BackgroundProperty, Avalonia.Application.Current!.Resources.GetResourceObservable(D47.App.Theming.ThemeManager.BgKey));
        window.Show();
        Dispatcher.UIThread.RunJobs();

        panel.Tab = PanelTab.Search;
        panel.Nav.SelectRoot(StationSearchPage.RootKey);
        Dispatcher.UIThread.RunJobs();

        return (panel, window);
    }

    private static StationSearchPage Page(Control root) => root.GetVisualDescendants().OfType<StationSearchPage>().Single();

    private static void Press(Button button)
    {
        button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();
    }

    private static Button Named(Control root, string name) =>
        root.GetVisualDescendants().OfType<Button>().Single(button => button.Name == name);

    private static List<Control> Rows(Control root) =>
        [.. root.GetVisualDescendants().OfType<Control>().Where(control => control.Classes.Contains(GalaxySearchPage.RowClass))];

    private static List<string> TextOf(Control root) =>
        [.. root.GetVisualDescendants().OfType<TextBlock>().Where(text => text.IsEffectivelyVisible).Select(text => text.Text ?? string.Empty)];

    /// <summary>Ticks rows on the open services picker and takes them.</summary>
    private static void TakeServices(Control panel, params string[] services)
    {
        foreach (var service in services)
        {
            panel.GetVisualDescendants().OfType<CheckBox>()
                .Single(box => box.Content is TextBlock { Text: { } text } && text == service)
                .IsChecked = true;
        }

        Dispatcher.UIThread.RunJobs();
        Press(panel.GetVisualDescendants().OfType<Button>().Single(button => button.Name == "AcceptButton"));
    }

    [AvaloniaFact]
    public void SearchOffersStationsAfterSystems()
    {
        var world = new World(lookups: true);
        var (panel, window) = Shown(world);

        Assert.Equal(
            [StarSystemPage.RootKey, SystemSearchPage.RootKey, StationSearchPage.RootKey],
            panel.Nav.Roots(PanelTab.Search).Select(root => root.Key));

        window.Close();
    }

    [AvaloniaFact]
    public void ChoosingTwoServicesShowsTwoTilesAndRunSearchSendsThem()
    {
        var world = new World(lookups: true);
        var (panel, window) = Shown(world);
        var page = Page(panel);

        Press(Named(panel, StationSearchPage.ChooseServicesName));

        var picker = panel.GetVisualDescendants().OfType<PickerPage>().Single();
        Assert.Contains(StationSearchPage.ServicesHelp, TextOf(picker));

        TakeServices(panel, "Interstellar Factors", "Black Market");

        var tiles = page.GetVisualDescendants().OfType<Button>().Where(button => button.Classes.Contains(StationSearchPage.ServiceClass)).ToList();
        Assert.Equal(["Black Market", "Interstellar Factors"], tiles.Select(tile => (string)tile.Tag!));

        Press(Named(panel, GalaxySearchPage.RunName));

        var asked = Assert.Single(world.Galaxy.Asked);
        Assert.Equal(["Black Market", "Interstellar Factors Contact"], asked.Services);
        Assert.Equal("Black Market, Interstellar Factors", page.FormArguments()["services"]);

        Press(tiles[0]);

        Assert.Equal(["Interstellar Factors"], page.Services);
        Assert.Equal("Interstellar Factors", page.FormArguments()["services"]);

        window.Close();
    }

    [AvaloniaFact]
    public void NoTypeTickedSendsNoTypeAndTheAnswerLeavesOutCarriers()
    {
        var world = new World(lookups: true);
        var (panel, window) = Shown(world);
        var page = Page(panel);

        page.ChooseServices(["Black Market"]);
        Press(Named(panel, GalaxySearchPage.RunName));

        Assert.False(page.FormArguments().ContainsKey("station_type"));
        var kinds = Assert.Single(world.Galaxy.Asked).Kinds!;
        Assert.DoesNotContain(StationKind.FleetCarrier, kinds);
        Assert.Contains(StationKind.Starport, kinds);

        page.TickType("Fleet carrier", true);
        Press(Named(panel, GalaxySearchPage.RunName));

        Assert.Equal("Fleet carrier", page.FormArguments()["station_type"]);
        Assert.Equal([StationKind.FleetCarrier], world.Galaxy.Asked[1].Kinds);

        window.Close();
    }

    [AvaloniaFact]
    public void TheAskedForColumnListsOnlyTheAskedForServicesEachStationHas()
    {
        var world = new World(lookups: true);
        world.Board.Post(Posting(new Dictionary<string, string> { ["services"] = "Black Market, Interstellar Factors" }, TwoStations));
        var (panel, window) = Shown(world);

        var rows = Rows(panel);
        Assert.Equal(2, rows.Count);
        Assert.Contains("Black Market, Interstellar Factors", TextOf(rows[0]));
        Assert.DoesNotContain(TextOf(rows[0]), text => text.Contains("Shipyard", StringComparison.Ordinal));
        Assert.Contains("Black Market", TextOf(rows[1]));
        Assert.DoesNotContain(TextOf(rows[1]), text => text.Contains("Refuel", StringComparison.Ordinal));
        Assert.Contains("2 STATIONS", TextOf(panel));

        window.Close();
    }

    [AvaloniaFact]
    public async Task AVoicePostingRedrawsAnOpenPage()
    {
        var world = new World(lookups: true);
        var (panel, window) = Shown(world);

        Assert.Empty(Rows(panel));

        await Task.Run(() => world.Board.Post(Posting(new Dictionary<string, string> { ["material_trader"] = "Raw", ["station_type"] = "Outpost, Starport" }, TwoStations)), TestContext.Current.CancellationToken);
        Dispatcher.UIThread.RunJobs();

        var page = Page(panel);
        Assert.Equal(2, Rows(panel).Count);
        Assert.Equal("Raw", page.FormArguments()["material_trader"]);
        Assert.Equal("Starport, Outpost", page.FormArguments()["station_type"]);

        window.Close();
    }

    [AvaloniaFact]
    public void TheFormSendsEveryParameterTheToolTakes()
    {
        var world = new World(lookups: true);
        var (panel, window) = Shown(world);
        var page = Page(panel);

        page.Type("near", "Sol");
        page.Type("max_station_distance", "500");
        page.Type("module", "Frame Shift Drive");
        page.Type("ship", "Cobra Mk III");
        page.Type("controlling_faction", "Diamond Frogs");
        page.Type("population from", "1000");
        page.TickType("Starport", true);
        page.ChooseServices(["Market"]);

        foreach (var name in (string[])["allegiance", "government", "station_economy", "station_state", "state", "primary_economy", "power", "module_class", "module_rating"])
        {
            page.Pick(name, name is "module_class" ? "5" : name is "module_rating" ? "A" : GalaxyFilters.Find(name)!.Choices[0]);
        }

        foreach (var stepper in page.GetVisualDescendants().OfType<Stepper>())
        {
            stepper.SelectedIndex = 1;
        }

        page.GetVisualDescendants().OfType<CheckBox>().Single(box => box.Content as string == "Only colonised systems").IsChecked = true;

        var amounts = page.GetVisualDescendants().OfType<Amount>().ToList();
        amounts[0].Value = 80;
        amounts[1].Value = 8;

        var tool = GalaxyCapability.Create(null, () => null, world.Settings).Tools.Single(t => t.Name == "search_stations");

        Assert.Equal(
            tool.Parameters.Select(parameter => parameter.Name).Order(StringComparer.Ordinal),
            page.FormArguments().Keys.Order(StringComparer.Ordinal));

        Press(Named(panel, GalaxySearchPage.ClearName));

        Assert.Empty(page.FormArguments());

        window.Close();
    }

    [AvaloniaFact]
    public void EveryPressableControlIsAtLeastFortyFourTall()
    {
        var world = new World(lookups: true);
        world.Board.Post(Posting(new Dictionary<string, string> { ["services"] = "Black Market" }, TwoStations));
        var (panel, window) = Shown(world);
        var page = Page(panel);
        page.ChooseServices(["Black Market", "Shipyard"]);
        Dispatcher.UIThread.RunJobs();

        var pressable = page.GetVisualDescendants()
            .OfType<Control>()
            .Where(control => control.IsEffectivelyVisible && control is Button or CheckBox or TextBox)
            .Where(control => !control.GetVisualAncestors().OfType<Avalonia.Controls.Primitives.ScrollBar>().Any())
            .ToList();

        Assert.NotEmpty(pressable);
        Assert.All(pressable, control => Assert.True(
            control.Bounds.Height >= 44 - 0.5,
            $"{control.GetType().Name} '{control.Name ?? (control as ContentControl)?.Content?.ToString()}' is {control.Bounds.Height} tall"));

        window.Close();
    }

    [AvaloniaFact]
    public void TheStationsPageIsCaptured()
    {
        using var look = AppLook.Put();

        var world = new World(lookups: true);
        world.Board.Post(Posting(new Dictionary<string, string> { ["services"] = "Black Market, Interstellar Factors" }, TwoStations));
        var (_, window) = Shown(world);

        using var frame = window.CaptureRenderedFrame()!;
        frame.SaveCapture("search-stations.png");

        window.Close();
    }
}
