using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media.Imaging;
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

/// <summary>A star or planet pressed on Search › System opens a page of its own, under the page it was pressed on.</summary>
public sealed class ABodyOpensItsOwnPageUnderItsSystemTests
{
    private const string Fid = "F1";
    private const long Address = 7786;
    private const string Placeholders = @"C:\d47\codex-placeholders";

    private static readonly DateTimeOffset AskedAt = new(2026, 10, 10, 19, 42, 0, TimeSpan.Zero);

    private static readonly StarSystemProfile Profile = new()
    {
        Name = "LTT 7786",
        SystemAddress = Address,
        Bodies =
        [
            new BodyProfile
            {
                BodyId = 0,
                Name = "LTT 7786",
                Type = "Star",
                SubType = "K (Yellow-Orange) Star",
                SpectralClass = "K7",
                Luminosity = "V",
                IsMainStar = true,
                RotationalPeriod = 30.5,
            },
            new BodyProfile
            {
                BodyId = 5,
                Name = "LTT 7786 3",
                Type = "Planet",
                SubType = "High metal content world",
                ParentId = 0,
                DistanceToArrival = 288,
                IsLandable = true,
                OrbitalPeriod = 200,
            },
            new BodyProfile
            {
                BodyId = 6,
                Name = "LTT 7786 3 a",
                Type = "Planet",
                SubType = "Rocky body",
                ParentId = 5,
                DistanceToArrival = 290,
            },
        ],
        Stations = [new StationProfile { Name = "Smith Port", Body = "LTT 7786 3", Kind = StationKind.SurfacePort }],
    };

    private static readonly BodySearchResult Bodies = new(
        "LTT 7786",
        1,
        [new BodySummary { Name = "LTT 7786 3", SystemName = "LTT 7786", SystemAddress = Address, BodyId = 5, Subtype = "High metal content world" }]);

    private sealed class Systems : StubStarSystemService
    {
        public int Asked { get; private set; }

        public override Task<StarSystemProfile?> ProfileAsync(long systemAddress, CancellationToken cancellationToken)
        {
            Asked++;

            return Task.FromResult<StarSystemProfile?>(systemAddress == Address ? Profile : null);
        }

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

    private static (PanelView Panel, Window Window, Systems Systems) Shown(string root, bool headset = false)
    {
        var settings = TestSurface.Settings();
        var store = new SavedSearchStore("saved-searches.json", new MemoryFileSystem(), NullLogger<SavedSearchStore>.Instance);
        var board = new GalaxySearchBoard();
        var registry = CapabilityRegistry.Build([GalaxyCapability.Create(new Galaxy(), () => "LTT 7786", settings, searches: board)]);

        board.Post(new GalaxySearchPosting(GalaxySearchKind.Bodies, new Dictionary<string, string>(StringComparer.Ordinal), Bodies.Reference, Bodies, "Bodies matched.", AskedAt));

        var panel = new PanelView { DataContext = new PanelViewModel() };

        if (headset)
        {
            panel.Classes.Add("headset");
        }

        var systems = new Systems();

        panel.EnableStarSystem(new StarSystemSurface(
            systems,
            () => new JournalLocation("LTT 7786", null, false, null) { SystemAddress = Address })
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

    /// <summary>Runs the dispatcher until <paramref name="done"/> holds, the fetches being on the pool.</summary>
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

    private static Border Named(Control within, string name) =>
        within.GetVisualDescendants().OfType<Border>()
            .Single(border => border.Classes.Contains(D47.App.Theming.ListRow.Class) && Avalonia.Automation.AutomationProperties.GetName(border) == name);

    private static IReadOnlyList<string> Keys(PanelView panel) => [.. panel.Nav.TrailOf(PanelTab.Search).Select(crumb => crumb.Key)];

    private static SystemBodyPage Page(PanelView panel) => panel.GetVisualDescendants().OfType<SystemBodyPage>().Single();

    [AvaloniaFact]
    public void ABodyPressedOnTheSystemsBodiesDrillsItsPageUnderTheSystemOnTheHeadsetAndTheDesktop()
    {
        foreach (var headset in new[] { false, true })
        {
            var (panel, window, systems) = Shown(StarSystemPage.RootKey, headset);

            Until(() => panel.GetVisualDescendants().OfType<Segment>().Any());
            Choose(Sections(panel), 2);

            Enter(Named(panel, "LTT 7786 3"));
            Until(() => panel.GetVisualDescendants().OfType<SystemBodyPage>().Any(page => page.Body is not null));

            Assert.Equal([StarSystemPage.RootKey, SystemBodyPage.Key(Address, 5)], Keys(panel));
            Assert.Equal("LTT 7786 3", Page(panel).Body!.Name);
            Assert.Equal(1, systems.Asked);

            window.Close();
        }
    }

    [AvaloniaFact]
    public void ABodySearchResultOpensItsSystemWithTheBodysPageUnderIt()
    {
        var (panel, window, _) = Shown(BodySearchPage.RootKey);

        var row = panel.GetVisualDescendants().OfType<Button>()
            .Single(button => button.Classes.Contains(GalaxySearchPage.RowClass) && (string?)button.Tag == "LTT 7786 3");
        row.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(
            [BodySearchPage.RootKey, SearchFurnishing.FoundKeyPrefix + Address + SearchFurnishing.BodiesSuffix, SystemBodyPage.Key(Address, 5)],
            Keys(panel));

        Until(() => panel.GetVisualDescendants().OfType<SystemBodyPage>().Any(page => page.Body is not null));
        Assert.Equal("LTT 7786 3", Page(panel).Body!.Name);

        window.Close();
    }

    [AvaloniaFact]
    public void AMoonPressedOnAPlanetsPageDrillsUnderThePlanet()
    {
        var (panel, window, _) = Shown(StarSystemPage.RootKey);

        Until(() => panel.GetVisualDescendants().OfType<Segment>().Any());
        Choose(Sections(panel), 2);
        Enter(Named(panel, "LTT 7786 3"));
        Until(() => panel.GetVisualDescendants().OfType<SystemBodyPage>().Any(page => page.Body is not null));

        var planet = Page(panel);
        Assert.Equal(
            [SystemBodyPage.Section.Overview, SystemBodyPage.Section.Orbit, SystemBodyPage.Section.Orbiting],
            planet.Sections);

        planet.Show(SystemBodyPage.Section.Orbiting);
        Dispatcher.UIThread.RunJobs();
        Assert.NotNull(Named(planet, "Smith Port"));
        Enter(Named(planet, "LTT 7786 3 a"));
        Until(() => panel.GetVisualDescendants().OfType<SystemBodyPage>().Any(page => page.Body?.BodyId == 6));

        Assert.Equal([StarSystemPage.RootKey, SystemBodyPage.Key(Address, 5), SystemBodyPage.Key(Address, 6)], Keys(panel));

        window.Close();
    }

    [AvaloniaFact]
    public void AStarShowsOverviewOrbitAndOrbitingAndItsClassLine()
    {
        var page = new SystemBodyPage(Surface(), Address, 0, "LTT 7786", Profile);
        var window = new Window { Content = page, Width = 1280, Height = 960 };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        var segment = Sections(page);
        Assert.Equal(["Overview", "Orbit", "Orbiting 1"], segment.ItemsSource!.Cast<string>());
        Assert.Contains(page.GetVisualDescendants().OfType<TextBlock>(), block => block.Text == "STAR · MAIN STAR");

        var texts = page.GetVisualDescendants().OfType<TextBlock>().Where(block => block.Name is { Length: > 0 }).ToDictionary(block => block.Name!, block => block.Text);
        Assert.Equal("K7 V", texts["StarClass"]);
        Assert.Equal("Orange main-sequence star", texts["StarClassName"]);
        Assert.Contains("V: main sequence", texts["StarClassMeaning"], StringComparison.Ordinal);

        window.Close();
    }

    [AvaloniaFact]
    public void ABodyTypeWithNoCodexEntryShowsItsFamilyPlaceholderAndOpensNothing()
    {
        var files = new MemoryFileSystem();
        files.WriteBytes(Path.Combine(Placeholders, "terrestrial-planets.jpg"), Png(8));
        var store = new CodexPictureStore(files, @"C:\d47\data", () => true, NullLogger.Instance, new HttpClientHandlerStub(), commander: () => Fid);
        var opened = new List<CodexEntry>();
        var codex = new BodyCodex(CodexCatalogue.Table, new CodexPictureSet(store, () => true, files, Placeholders), opened.Add);

        Assert.Null(codex.EntryOf(Profile.Bodies[1]));

        var page = new SystemBodyPage(Surface(), Address, 5, "LTT 7786 3", Profile, codex);
        var window = new Window { Content = page, Width = 1280, Height = 960 };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        var tile = page.GetVisualDescendants().OfType<Control>().First(control => control.Name == BodyCodex.TileName);

        Assert.IsType<Border>(tile);
        Assert.Contains(tile.GetVisualDescendants().OfType<Image>(), image => image.Name == BodyCodex.PlaceholderName);
        Assert.DoesNotContain(page.GetVisualDescendants().OfType<Button>(), button => button.Name == BodyCodex.TileName);
        Assert.Empty(opened);

        window.Close();
    }

    private sealed class HttpClientHandlerStub : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.NotFound));
    }

    private static StarSystemSurface Surface() => new(new Systems(), () => null);

    private static byte[] Png(int side)
    {
        using var bitmap = new WriteableBitmap(
            new PixelSize(side, side), new Vector(96, 96), Avalonia.Platform.PixelFormat.Bgra8888, Avalonia.Platform.AlphaFormat.Premul);
        using var stream = new MemoryStream();
        bitmap.Save(stream, new PngBitmapEncoderOptions());

        return stream.ToArray();
    }
}
