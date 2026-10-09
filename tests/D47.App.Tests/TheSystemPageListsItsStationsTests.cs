using System.Net;
using System.Text;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Avalonia.VisualTree;
using D47.App.Controls;
using D47.App.Headset;
using D47.App.Panel;
using D47.Core.Capabilities.Builtin;
using D47.Core.Configuration;
using D47.Core.Interface;
using D47.Core.Journal;
using D47.Core.Knowledge;
using D47.Knowledge;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.App.Tests;

/// <summary>
/// SEARCH › SYSTEM › STATIONS lists a system's stations nearest first, narrowed by one kind, every ticked service
/// and the title line's name field; the Overview's kind tiles open it on their kind (#825).
/// </summary>
[Trait("Category", "Integration")]
public sealed class TheSystemPageListsItsStationsTests
{
    private const long Ltt7786 = 633608311522;

    internal sealed class Systems(StarSystemProfile profile) : IStarSystemService
    {
        private int _asked;

        public int Asked => Volatile.Read(ref _asked);

        public Task<StarSystemProfile?> ProfileAsync(long systemAddress, CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref _asked);

            return Task.FromResult<StarSystemProfile?>(profile);
        }

        public Task<IReadOnlyList<SystemNameMatch>> MatchNamesAsync(string typed, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<SystemNameMatch>>([]);

        public Task<PowerplayNeighbourhood> PowerplayNearAsync(string system, double lightYears, CancellationToken cancellationToken) =>
            Task.FromResult(new PowerplayNeighbourhood(0, []));
    }

    private static StarSystemSurface Surface(Systems systems) =>
        new(systems, () => new JournalLocation("LTT 7786", null, false, null) { SystemAddress = Ltt7786 }, () => true);

    internal static (PanelView Panel, Window Window, Systems Systems) Shown(double width = 1280, double height = 1180)
    {
        var systems = new Systems(Ltt7786Profile());
        var panel = new PanelView { DataContext = new PanelViewModel() };
        panel.EnableSearch();
        panel.EnableStarSystem(Surface(systems));

        var window = new Window { Content = panel, Width = width, Height = height };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        panel.Tab = PanelTab.Search;
        Until(() => Shows(panel, "LAST REPORT TO SPANSH"));

        return (panel, window, systems);
    }

    private static void Until(Func<bool> done)
    {
        var deadline = DateTime.UtcNow.AddSeconds(5);

        while (!done())
        {
            Assert.True(DateTime.UtcNow < deadline, "the page did not settle in time");
            Dispatcher.UIThread.RunJobs();
            Thread.Sleep(5);
        }

        Dispatcher.UIThread.RunJobs();
    }

    private static bool Shows(Control root, string text) =>
        root.GetVisualDescendants().OfType<TextBlock>().Any(block => block.IsEffectivelyVisible && block.Text == text);

    internal static void Section(Control root, string label)
    {
        var sections = root.GetVisualDescendants().OfType<Segment>().Single(segment => AutomationProperties.GetName(segment) == "Sections");
        sections.GetLogicalDescendants().OfType<RadioButton>()
            .Single(button => (button.Content as string)?.StartsWith(label, StringComparison.Ordinal) == true)
            .IsChecked = true;
        Dispatcher.UIThread.RunJobs();
    }

    private static Sidebar Sidebar(Control root) => root.GetVisualDescendants().OfType<Sidebar>().Single();

    private static void Kind(Control root, string key)
    {
        var index = StationGroup.Kinds.Select(group => group.Key).ToList().IndexOf(key);
        var row = Sidebar(root).Items[index];

        row.RaiseEvent(new PointerPressedEventArgs(
            row,
            new Pointer(0, PointerType.Mouse, true),
            row,
            default,
            0,
            new PointerPointProperties(RawInputModifiers.LeftMouseButton, PointerUpdateKind.LeftButtonPressed),
            KeyModifiers.None));
        Dispatcher.UIThread.RunJobs();
    }

    private static CheckBox Service(Control root, string label) =>
        root.GetVisualDescendants().OfType<CheckBox>().Single(box => AutomationProperties.GetName(box) == label);

    private static void Tick(Control root, string label)
    {
        Service(root, label).IsChecked = true;
        Dispatcher.UIThread.RunJobs();
    }

    private static TextBox Field(PanelView panel) => panel.FindControl<TextBox>("SearchInput")!;

    private static void Type(PanelView panel, string text)
    {
        Field(panel).Text = text;
        Dispatcher.UIThread.RunJobs();
    }

    private static string Showing(Control root) =>
        AutomationProperties.GetName(root.GetVisualDescendants().OfType<StackPanel>()
            .Single(panel => AutomationProperties.GetName(panel)?.StartsWith("Showing", StringComparison.Ordinal) == true))!;

    private static List<string> Rows(Control root) =>
        [.. root.GetVisualDescendants().OfType<Border>()
            .Where(border => border.Child is Grid && AutomationProperties.GetName(border) is { Length: > 0 } && border.Padding == new Thickness(12, 0))
            .Select(border => AutomationProperties.GetName(border)!)];

    [AvaloniaFact]
    public void TheKindCountsReadTheFixture()
    {
        var profile = Ltt7786Profile();

        Assert.Equal(
            [102, 1, 2, 4, 84, 6, 5],
            StationGroup.Kinds.Select(group => profile.Stations.Count(group.Holds)));

        var (panel, window, _) = Shown();
        Section(panel, "Stations");

        Assert.Equal(
            ["102", "1", "2", "4", "84", "6", "5"],
            Sidebar(panel).Items.Select(row => row.GetVisualDescendants().OfType<TextBlock>().Last().Text));
        Assert.Equal("Showing 102 of 102", Showing(panel));

        window.Close();
    }

    [AvaloniaFact]
    public void TheServiceNamesMatchSpanshsSpellings()
    {
        var profile = Ltt7786Profile();

        Assert.Equal(
            [82, 1, 5, 9, 39, 0, 0, 8, 6, 82, 81, 18],
            StationService.All.Select(service => profile.Stations.Count(service.OfferedBy)));
    }

    [AvaloniaFact]
    public void AKindTwoServicesAndANameEachNarrowTheTable()
    {
        var (panel, window, _) = Shown();
        Section(panel, "Stations");

        Kind(panel, "settlement");
        Assert.Equal("Showing 84 of 102", Showing(panel));
        Assert.Equal(84, Rows(panel).Count);

        Tick(panel, "Commodity market");
        var markets = int.Parse(Showing(panel).Split(' ')[1], System.Globalization.CultureInfo.InvariantCulture);
        Assert.InRange(markets, 1, 83);

        Tick(panel, "Interstellar factors");
        var both = int.Parse(Showing(panel).Split(' ')[1], System.Globalization.CultureInfo.InvariantCulture);
        Assert.InRange(both, 1, markets - 1);
        Assert.Equal(both, Rows(panel).Count);

        var first = Rows(panel)[0];
        Type(panel, first[..4]);

        var named = Rows(panel);
        Assert.Contains(first, named);
        Assert.All(named, name => Assert.Contains(first[..4], name, StringComparison.OrdinalIgnoreCase));
        Assert.Equal($"Showing {named.Count} of 102", Showing(panel));

        Type(panel, "Qwertyuiop");
        Assert.Empty(Rows(panel));
        Assert.True(Shows(panel, StarSystemPage.NoStationMatches));

        window.Close();
    }

    [AvaloniaFact]
    public void TheTableIsNearestFirst()
    {
        var (panel, window, _) = Shown();
        Section(panel, "Stations");

        var profile = Ltt7786Profile();
        var expected = StationFilter.Nearest(profile.Stations).Select(station => station.Name).ToList();

        Assert.Equal(expected, Rows(panel));
        Assert.DoesNotContain("B9G-54K", Rows(panel).Take(90));
        Assert.Equal("Williams Vision", Rows(panel)[^1]);

        window.Close();
    }

    [AvaloniaFact]
    public void AServiceNoStationOffersCannotBeTicked()
    {
        var (panel, window, _) = Shown();
        Section(panel, "Stations");

        var trader = Service(panel, "Material trader");
        Assert.False(trader.IsEnabled);
        Assert.False(trader.IsEffectivelyEnabled);

        Assert.True(Service(panel, "Shipyard").IsEnabled);

        window.Close();
    }

    [AvaloniaFact]
    public void ATileOnTheOverviewOpensStationsOnItsKind()
    {
        var (panel, window, _) = Shown();

        Assert.True(Shows(panel, "STATIONS BY KIND"));

        var tile = panel.GetVisualDescendants().OfType<Button>().Single(button => AutomationProperties.GetName(button) == "Fleet carrier, 6");
        tile.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();

        Assert.Equal("carrier", Sidebar(panel).Selected);
        Assert.Equal("Showing 6 of 102", Showing(panel));

        window.Close();
    }

    [AvaloniaFact]
    public void SwitchingSectionsAsksSpanshNothing()
    {
        var (panel, window, systems) = Shown();
        Assert.Equal(1, systems.Asked);

        Section(panel, "Stations");
        Section(panel, "Overview");
        Section(panel, "Stations");
        Kind(panel, "outpost");
        Type(panel, "Dock");

        Assert.Equal(1, systems.Asked);

        window.Close();
    }

    [AvaloniaFact]
    public void TheNameFieldShowsOnlyOnStations()
    {
        var (panel, window, _) = Shown();

        Assert.False(Field(panel).IsVisible);

        Section(panel, "Stations");
        Assert.True(Field(panel).IsVisible);
        Assert.Equal("Find a station by name", Field(panel).PlaceholderText);

        Section(panel, "Overview");
        Assert.False(Field(panel).IsVisible);

        window.Close();
    }

    [AvaloniaFact]
    public void ARowReadsItsStation()
    {
        var profile = Ltt7786Profile();

        var gateway = profile.Stations.Single(station => station.Name == "Parise Gateway");
        Assert.Equal("Ocellus Starport", StarSystemPage.StationType(gateway));
        Assert.Equal("L", StarSystemPage.PadText(gateway.LargestPad));
        Assert.Equal("213 Ls", StarSystemPage.ArrivalText(gateway.DistanceToArrival));

        Assert.Equal("1,788 Ls", StarSystemPage.ArrivalText(1788.28));
        Assert.Equal("—", StarSystemPage.ArrivalText(null));
        Assert.Equal("—", StarSystemPage.ArrivalText(0));
        Assert.Equal("—", StarSystemPage.PadText(null));

        var untyped = profile.Stations.Single(station => station.Name == "Moore Installation");
        Assert.Equal("Other", StarSystemPage.StationType(untyped));

        var carrier = profile.Stations.First(station => station.Kind == StationKind.FleetCarrier);
        Assert.Equal("Fleet carrier", StarSystemPage.StationType(carrier));
        Assert.Equal("Fleet carrier", StarSystemPage.Faction(carrier));
    }

    [AvaloniaFact]
    public void StationsIsCaptured()
    {
        using var look = AppLook.Put();

        var (panel, window, _) = Shown();

        using (var overview = window.CaptureRenderedFrame()!)
        {
            overview.SaveCapture("system-overview-kinds.png");
        }

        Section(panel, "Stations");

        using var frame = window.CaptureRenderedFrame()!;
        frame.SaveCapture("system-stations.png");

        Kind(panel, "other");
        Tick(panel, "Shipyard");

        using var none = window.CaptureRenderedFrame()!;
        none.SaveCapture("system-stations-none.png");

        window.Close();
    }

    [AvaloniaFact]
    public void StationsIsCapturedInTheHeadset()
    {
        using var look = AppLook.Put();
        var (settings, _, _) = TestSurface.Create();
        settings.Apply(VrCapability.ModeKey, "full", SettingsCaller.Panel);

        var systems = new Systems(Ltt7786Profile());
        var headset = new VrPanelSurface(new PanelViewModel(), settings, _ => null, starSystem: Surface(systems));
        var view = (PanelView)typeof(VrPanelSurface)
            .GetField("_view", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
            .GetValue(headset)!;

        headset.Nav.Select(PanelTab.Search);
        Until(() =>
        {
            Serve(headset);
            return view.GetVisualDescendants().OfType<TextBlock>().Any(block => block.Text == "LAST REPORT TO SPANSH");
        });

        Section(view, "Stations");
        Serve(headset);

        var (width, height) = headset.Size;
        using var frame = new RenderTargetBitmap(new PixelSize(width, height));
        frame.Render(view);
        frame.SaveCapture("system-stations-headset.png");
    }

    /// <summary>One frame, into a buffer nobody reads, which lays the headset's panel out.</summary>
    private static void Serve(VrPanelSurface headset)
    {
        Dispatcher.UIThread.RunJobs();

        var (width, height) = headset.Size;
        var buffer = new byte[width * height * 4];

        unsafe
        {
            fixed (byte* pixels = buffer)
            {
                headset.Draw((IntPtr)pixels, width * 4);
                headset.Draw((IntPtr)pixels, width * 4);
            }
        }

        Dispatcher.UIThread.RunJobs();
    }

    /// <summary>LTT 7786 as Spansh had it on 2026-10-05, read through the real service.</summary>
    internal static StarSystemProfile Ltt7786Profile()
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);

        while (root is not null && !File.Exists(Path.Combine(root.FullName, "d47.slnx")))
        {
            root = root.Parent;
        }

        Assert.NotNull(root);

        var body = File.ReadAllText(Path.Combine(root.FullName, "tests", "D47.Knowledge.Tests", "Fixtures", "spansh-dump-ltt-7786.json"));

        using var service = new SpanshStarSystemService(
            NullLogger<SpanshStarSystemService>.Instance,
            new HttpClient(new Answer(body)));

        return service.ProfileAsync(Ltt7786, CancellationToken.None).GetAwaiter().GetResult()!;
    }

    private sealed class Answer(string body) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json"),
            });
    }
}
