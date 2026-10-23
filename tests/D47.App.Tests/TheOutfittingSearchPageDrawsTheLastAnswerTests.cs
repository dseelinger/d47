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

/// <summary>Search › Outfitting adds items through a type-ahead, runs search_outfitting and draws the last answer (#1301).</summary>
public sealed class TheOutfittingSearchPageDrawsTheLastAnswerTests
{
    private const long Shinrarta = 3932277478106;

    private static readonly DateTimeOffset AskedAt = new(2026, 10, 18, 19, 42, 0, TimeSpan.Zero);

    private static readonly StationSearchResult TwoStations = new(
        "LTT 7786",
        2,
        [
            new StationSummary
            {
                Name = "Jameson Memorial",
                SystemName = "Shinrarta Dezhra",
                SystemAddress = Shinrarta,
                Type = "Orbis Starport",
                Distance = 4.2,
                DistanceToArrival = 1200,
                LargePads = 2,
                UpdatedAt = AskedAt,
            },
            new StationSummary
            {
                Name = "Ray Gateway",
                SystemName = "LTT 7786",
                SystemAddress = 633608311522,
                Type = "Outpost",
                Distance = 9.9,
                DistanceToArrival = 40,
                MediumPads = 2,
                UpdatedAt = AskedAt,
            },
        ]);

    private sealed class Systems : IStarSystemService
    {
        public Task<StarSystemProfile?> ProfileAsync(long systemAddress, CancellationToken cancellationToken) =>
            Task.FromResult<StarSystemProfile?>(new StarSystemProfile { Name = $"System {systemAddress}", SystemAddress = systemAddress });

        public Task<IReadOnlyList<SystemNameMatch>> MatchNamesAsync(string typed, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<SystemNameMatch>>([]);

        public Task<IReadOnlyList<SystemNameMatch>> PrefixNamesAsync(string typed, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<SystemNameMatch>>([]);

        public Task<PowerplayNeighbourhood> PowerplayNearAsync(string system, double lightYears, CancellationToken cancellationToken) =>
            Task.FromResult(new PowerplayNeighbourhood(0, []));
    }

    private sealed class Galaxy : IGalaxyService
    {
        public Task<StationSearchResult> FindStationsAsync(StationQuery query, CancellationToken cancellationToken) =>
            Task.FromResult(TwoStations);

        public Task<GalaxySearchResult> SearchAsync(GalaxyQuery query, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<double?> DistanceAsync(string from, string to, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<BodySearchResult> FindBodiesAsync(BodyQuery query, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<ColonisationScan> ScanForColonisationAsync(ColonisationQuery query, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<MissionStationScan> FindMissionStationsAsync(MissionStationQuery query, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<IReadOnlyList<BodySummary>> SystemBodiesAsync(long systemAddress, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<SystemBiology> SystemBiologyAsync(long systemAddress, CancellationToken cancellationToken) => throw new NotSupportedException();
    }

    private sealed class Clipboard : IClipboard
    {
        public Task<bool> SetTextAsync(string text, CancellationToken cancellationToken = default) => Task.FromResult(true);
    }

    private sealed class World
    {
        public World()
        {
            Settings = TestSurface.Settings();
            Settings.Apply(GalaxyCapability.EnabledKey, "true", SettingsCaller.Panel);
            Registry = CapabilityRegistry.Build([GalaxyCapability.Create(new Galaxy(), () => "LTT 7786", Settings, searches: Board)]);
        }

        public SettingsService Settings { get; }

        public GalaxySearchBoard Board { get; } = new();

        public CapabilityRegistry Registry { get; }

        public PanelRouting Routing { get; } = new(() => false, () => []);

        public StarSystemSurface Surface => new(
            new Systems(),
            () => new JournalLocation("LTT 7786", null, false, null) { SystemAddress = 633608311522 },
            () => Settings.Current.Knowledge.GalaxySearch,
            () => { })
        {
            Searches = new GalaxySearchSurface(
                Registry,
                Board,
                new SavedSearchStore("saved-searches.json", new MemoryFileSystem(), NullLogger<SavedSearchStore>.Instance),
                () => "F1",
                () => [])
            {
                PrefixNames = new Systems().PrefixNamesAsync,
                SuggestionPause = TimeSpan.Zero,
            },
        };

        public async Task<ToolResult> AskAsync(string tool, params (string Name, string Value)[] values)
        {
            var result = await Registry.InvokeAsync(
                tool,
                new ToolArguments(values.ToDictionary(v => v.Name, v => v.Value, StringComparer.Ordinal)),
                TestContext.Current.CancellationToken);

            if (result.Page is { } page)
            {
                Routing.Open(page);
            }

            Dispatcher.UIThread.RunJobs();

            return result;
        }
    }

    private static (PanelView Panel, Window Window) Shown(World world, bool headset = false, bool openRoot = true)
    {
        var panel = new PanelView { DataContext = new PanelViewModel() };

        if (headset)
        {
            panel.Classes.Add("headset");
        }

        panel.EnableCopy(new Clipboard());
        panel.EnableStarSystem(world.Surface);
        world.Routing.RouteNavigation(panel.Nav, action => action());

        var window = new Window { Content = panel, Width = 1280, Height = 960 };
        window.Bind(Window.BackgroundProperty, Avalonia.Application.Current!.Resources.GetResourceObservable(D47.App.Theming.ThemeManager.BgKey));
        window.Show();
        Dispatcher.UIThread.RunJobs();

        if (openRoot)
        {
            panel.Tab = PanelTab.Search;
            panel.Nav.SelectRoot(OutfittingSearchPage.RootKey);
            Dispatcher.UIThread.RunJobs();
        }

        return (panel, window);
    }

    private static OutfittingSearchPage Page(Control root) => root.GetVisualDescendants().OfType<OutfittingSearchPage>().Single();

    private static void Press(Button button)
    {
        button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();
    }

    private static Button Named(Control root, string name) =>
        root.GetVisualDescendants().OfType<Button>().Single(button => button.Name == name);

    private static List<Button> Tiles(Control root) =>
        [.. root.GetVisualDescendants().OfType<Button>().Where(button => button.Classes.Contains(OutfittingSearchPage.ItemClass))];

    private static List<Control> Rows(Control root) =>
        [.. root.GetVisualDescendants().OfType<Control>().Where(control => control.Classes.Contains(GalaxySearchPage.RowClass))];

    private static List<string> TextOf(Control root) =>
        [.. root.GetVisualDescendants().OfType<TextBlock>().Where(text => text.IsEffectivelyVisible).Select(text => text.Text ?? string.Empty)];

    private static void Typed(OutfittingSearchPage page, string typed)
    {
        page.Type(typed);
        Dispatcher.UIThread.RunJobs();
        Thread.Sleep(50);
        Dispatcher.UIThread.RunJobs();
    }

    private static void Choose(OutfittingSearchPage page, string typed, string offered)
    {
        Typed(page, typed);
        Assert.Contains(page.Suggestions.Shown, item => item.Display == offered);
        page.Suggestions.Choose(page.Suggestions.Shown.First(item => item.Display == offered));
        Dispatcher.UIThread.RunJobs();
    }

    private static void Save(Window window, string name)
    {
        Dispatcher.UIThread.RunJobs();

        using var frame = window.CaptureRenderedFrame()!;
        frame.SaveCapture(name);
    }

    [AvaloniaFact]
    public void SearchOffersOutfittingAfterStationsAndBeforeBodies()
    {
        var (panel, window) = Shown(new World());

        Assert.Equal(
            [StarSystemPage.RootKey, SystemSearchPage.RootKey, StationSearchPage.RootKey, OutfittingSearchPage.RootKey, BodySearchPage.RootKey, SignalSearchPage.RootKey],
            panel.Nav.Roots(PanelTab.Search).Select(root => root.Key));

        window.Close();
    }

    [AvaloniaFact]
    public void TypingAbbreviatedWordsOffersTheModuleAndTwoTilesAreSent()
    {
        var world = new World();
        var (panel, window) = Shown(world);
        var page = Page(panel);

        Typed(page, "5a fsd sco");
        Assert.Contains(page.Suggestions.Shown, item => item.Display == "5A Frame Shift Drive (SCO)");

        Choose(page, "5a fsd sco", "5A Frame Shift Drive (SCO)");
        Choose(page, "6a fuel scoop", "6A Fuel Scoop");

        Assert.Equal(2, Tiles(panel).Count);
        Press(Named(panel, GalaxySearchPage.RunName));

        Assert.Equal("5A Frame Shift Drive (SCO), 6A Fuel Scoop", world.Board.Last(GalaxySearchKind.Outfitting)!.Arguments["items"]);

        window.Close();
    }

    [AvaloniaFact]
    public void TheSpelledOutNameOffersTheSameModule()
    {
        var (panel, window) = Shown(new World());
        var page = Page(panel);

        Typed(page, "frame shift drive sco");

        Assert.Contains(page.Suggestions.Shown, item => item.Display == "4A Frame Shift Drive (SCO)");

        window.Close();
    }

    [AvaloniaFact]
    public void PressingATileRemovesItFromTheNextSearch()
    {
        var world = new World();
        var (panel, window) = Shown(world);
        var page = Page(panel);

        Choose(page, "5a fsd sco", "5A Frame Shift Drive (SCO)");
        Choose(page, "6a fuel scoop", "6A Fuel Scoop");

        Press(Tiles(panel).First(tile => (string?)tile.Tag == "5A Frame Shift Drive (SCO)"));
        Press(Named(panel, GalaxySearchPage.RunName));

        Assert.Equal("6A Fuel Scoop", world.Board.Last(GalaxySearchKind.Outfitting)!.Arguments["items"]);

        window.Close();
    }

    [AvaloniaFact]
    public void WhileAShipTileIsPresentNoShipIsOffered()
    {
        var (panel, window) = Shown(new World());
        var page = Page(panel);

        Typed(page, "python");
        Assert.Contains(page.Suggestions.Shown, item => item.IsShip);

        Choose(page, "python mkii", "Python MkII");

        Typed(page, "python");
        Assert.DoesNotContain(page.Suggestions.Shown, item => item.IsShip);

        window.Close();
    }

    [AvaloniaFact]
    public void TextThatMatchesNothingSaysSo()
    {
        var (panel, window) = Shown(new World());
        var page = Page(panel);

        Typed(page, "zzzzqq");

        Assert.Contains(OutfittingSearchPage.NothingText, TextOf(panel));

        window.Close();
    }

    [AvaloniaFact]
    public void UntickingSurfaceStationsSendsItFalse()
    {
        var world = new World();
        var (panel, window) = Shown(world);
        var page = Page(panel);

        Choose(page, "6a fuel scoop", "6A Fuel Scoop");
        page.TickSurface(false);
        page.TickCarriers(true);
        page.Pad("Large");
        Press(Named(panel, GalaxySearchPage.RunName));

        var arguments = world.Board.Last(GalaxySearchKind.Outfitting)!.Arguments;
        Assert.Equal("false", arguments["surface_stations"]);
        Assert.Equal("true", arguments["include_carriers"]);
        Assert.Equal("Large", arguments["min_pad"]);

        window.Close();
    }

    [AvaloniaFact]
    public async Task ASpokenSearchRedrawsAnOpenPageAndOpensTheRootOnEverySurface()
    {
        var world = new World();
        var (open, openHost) = Shown(world);
        var (elsewhere, elsewhereHost) = Shown(world, openRoot: false);
        var (headset, headsetHost) = Shown(world, headset: true, openRoot: false);

        await world.AskAsync("search_outfitting", ("items", "6A Fuel Scoop"));

        Assert.All([open, elsewhere, headset], panel =>
        {
            Assert.Equal(PanelTab.Search, panel.Tab);
            Assert.Equal(OutfittingSearchPage.RootKey, panel.Nav.RootKeyOf(PanelTab.Search));
            Assert.Equal(GalaxySearchView.Answer, Page(panel).View);
            Assert.Equal(2, Rows(panel).Count);
        });

        Assert.Equal("6A Fuel Scoop", Assert.Single(Page(open).Items).Display);

        openHost.Close();
        elsewhereHost.Close();
        headsetHost.Close();
    }

    [AvaloniaFact]
    public async Task APressedRowOpensTheStarSystemPageOnItsSystem()
    {
        var world = new World();
        var (panel, window) = Shown(world);

        await world.AskAsync("search_outfitting", ("items", "6A Fuel Scoop"));

        var row = Rows(panel).OfType<Button>().First(button => (string?)button.Tag == "Shinrarta Dezhra");
        Press(row);

        Assert.Equal(
            SearchFurnishing.FoundKeyPrefix + Shinrarta,
            panel.Nav.TrailOf(PanelTab.Search)[^1].Key);

        window.Close();
    }

    [AvaloniaFact]
    public async Task EverySystemNameCarriesACopyGlyph()
    {
        var world = new World();
        var (panel, window) = Shown(world);

        await world.AskAsync("search_outfitting", ("items", "6A Fuel Scoop"));

        var copies = panel.GetVisualDescendants().OfType<Button>().Select(CopyGlyph.GetCopies).OfType<string>().ToList();
        Assert.Equal(["Shinrarta Dezhra", "LTT 7786"], copies);

        window.Close();
    }

    [AvaloniaTheory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task EveryPressableControlIsAtLeastFortyFourPixelsTall(bool headset)
    {
        var world = new World();
        var (panel, window) = Shown(world, headset);
        var page = Page(panel);

        await world.AskAsync("search_outfitting", ("items", "6A Fuel Scoop"));

        if (headset)
        {
            Press(Named(panel, GalaxySearchPage.EditName));
        }

        Typed(page, "6a fuel");

        var pressable = page.GetVisualDescendants()
            .Where(control => control is Button or CheckBox or RadioButton && control.IsEffectivelyVisible && control.Bounds.Height > 0)
            .ToList();

        Assert.NotEmpty(pressable);
        Assert.All(pressable, control => Assert.True(control.Bounds.Height >= 44, $"{control.GetType().Name} {control.Name} is {control.Bounds.Height}px tall."));

        window.Close();
    }

    [AvaloniaFact]
    public async Task ThePageDrawsATwoItemAnswer()
    {
        var world = new World();
        var (panel, window) = Shown(world);
        var page = Page(panel);

        Choose(page, "5a fsd sco", "5A Frame Shift Drive (SCO)");
        Choose(page, "6a fuel scoop", "6A Fuel Scoop");
        Press(Named(panel, GalaxySearchPage.RunName));
        await Task.Yield();
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(GalaxySearchView.Answer, page.View);
        Assert.Contains("2 STATIONS", TextOf(panel));

        if (TestSurface.CapturesWanted)
        {
            Save(window, "outfitting-answered-two-items.png");
        }

        window.Close();
    }

    [AvaloniaFact]
    public async Task TheTypeAheadListsItsRowsInThePageBeneathTheField()
    {
        var world = new World();
        var (panel, window) = Shown(world);
        var page = Page(panel);

        Choose(page, "python mkii", "Python MkII");
        Typed(page, "6a fuel");

        Assert.All(page.Suggestions.Shown, item => Assert.False(item.IsShip));
        Assert.Contains(page.Suggestions.Shown, item => item.Display == "6A Fuel Scoop");
        Assert.Equal("ADD A MODULE", TextOf(panel).Single(text => text.StartsWith("ADD A", StringComparison.Ordinal)));
        Assert.Equal("Type a module", panel.GetVisualDescendants().OfType<TextBox>().Single(box => box.Name == OutfittingSearchPage.AddName).PlaceholderText);
        Assert.Contains("MODULE", TextOf(panel));

        if (TestSurface.CapturesWanted)
        {
            Save(window, "outfitting-type-ahead-open-ship-added.png");
        }

        window.Close();

        var (headset, headsetWindow) = Shown(world, headset: true);
        await world.AskAsync("search_outfitting", ("items", "6A Fuel Scoop"));

        if (TestSurface.CapturesWanted)
        {
            Save(headsetWindow, "outfitting-headset-answer.png");
            Press(Named(headset, GalaxySearchPage.EditName));
            Save(headsetWindow, "outfitting-headset-form.png");
        }

        headsetWindow.Close();
    }

    [AvaloniaFact]
    public void AnAnswerOfOneUsesTheSingularNoun()
    {
        var world = new World();
        world.Board.Post(new GalaxySearchPosting(GalaxySearchKind.Outfitting, new Dictionary<string, string>(StringComparer.Ordinal), TwoStations.Reference, TwoStations with { Total = 1, Stations = [TwoStations.Stations[0]] }, "Stations matched.", new DateTimeOffset(2026, 10, 10, 19, 42, 0, TimeSpan.Zero)));
        var (panel, window) = Shown(world);

        Assert.Contains("1 STATION", TextOf(panel));
        Assert.DoesNotContain("1 STATIONS", TextOf(panel));

        window.Close();
    }
}
