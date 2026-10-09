using System.Net;
using System.Text;
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
using D47.App.Panel;
using D47.Core.Interface;
using D47.Core.Journal;
using D47.Core.Knowledge;
using D47.Knowledge;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.App.Tests;

/// <summary>
/// SEARCH › SYSTEM › POWERPLAY draws the system's own figures, then its Powerplay neighbours from one request
/// made when the section first opens, filtered by state; a row opens that system here (#827).
/// </summary>
[Trait("Category", "Integration")]
public sealed class TheSystemPageListsNearbyPowerplayTests
{
    private const long Ltt7786 = 633608311522;
    private const long Neighbour = 99;

    private sealed class Systems(StarSystemProfile profile, PowerplayNeighbourhood near) : IStarSystemService
    {
        public List<(string System, double Reach)> Asked { get; } = [];

        public Exception? Fails { get; set; }

        public Task<StarSystemProfile?> ProfileAsync(long systemAddress, CancellationToken cancellationToken) =>
            Task.FromResult<StarSystemProfile?>(
                systemAddress == Ltt7786 ? profile : profile with { SystemAddress = systemAddress, Name = near.Systems[0].Name, Powerplay = null });

        public Task<IReadOnlyList<SystemNameMatch>> MatchNamesAsync(string typed, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<SystemNameMatch>>(
                typed == near.Systems[0].Name ? [new SystemNameMatch(typed, Neighbour, new StarPosition(0, 0, 0))] : []);

        public Task<PowerplayNeighbourhood> PowerplayNearAsync(string system, double lightYears, CancellationToken cancellationToken)
        {
            lock (Asked)
            {
                Asked.Add((system, lightYears));
            }

            return Fails is { } failure ? Task.FromException<PowerplayNeighbourhood>(failure) : Task.FromResult(near);
        }
    }

    private sealed class Answer(string body) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json"),
            });
    }

    private static PowerplayNeighbourhood Ltt7786Neighbours()
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);

        while (root is not null && !File.Exists(Path.Combine(root.FullName, "d47.slnx")))
        {
            root = root.Parent;
        }

        Assert.NotNull(root);

        var body = File.ReadAllText(Path.Combine(
            root.FullName, "tests", "D47.Knowledge.Tests", "Fixtures", "spansh-powerplay-near-ltt-7786.json"));

        using var service = new SpanshStarSystemService(
            NullLogger<SpanshStarSystemService>.Instance,
            new HttpClient(new Answer(body)));

        return service.PowerplayNearAsync("LTT 7786", 20, CancellationToken.None).GetAwaiter().GetResult();
    }

    private static (PanelView Panel, Window Window, Systems Systems) Shown(
        Func<StarSystemProfile, StarSystemProfile>? change = null,
        Exception? fails = null)
    {
        var profile = TheSystemPageListsItsStationsTests.Ltt7786Profile();
        var systems = new Systems(change?.Invoke(profile) ?? profile, Ltt7786Neighbours()) { Fails = fails };
        var panel = new PanelView { DataContext = new PanelViewModel() };
        panel.EnableSearch();
        panel.EnableStarSystem(new StarSystemSurface(
            systems,
            () => new JournalLocation("LTT 7786", null, false, null) { SystemAddress = Ltt7786 },
            () => true));

        var window = new Window { Content = panel, Width = 1280, Height = 1180 };
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

    private static void OpenPowerplay(PanelView panel)
    {
        TheSystemPageListsItsStationsTests.Section(panel, "Powerplay");
        Until(() => !Shows(panel, "Asking Spansh for Powerplay systems near LTT 7786."));
    }

    private static List<Border> Rows(Control root) =>
        [.. root.GetVisualDescendants().OfType<Border>()
            .Where(border => border.Child is Grid { ColumnDefinitions.Count: 5 } && AutomationProperties.GetName(border) is { Length: > 0 })];

    private static void Filter(Control root, string label)
    {
        var segment = root.GetVisualDescendants().OfType<Segment>().Single(s => AutomationProperties.GetName(s) == "Powerplay state");
        segment.GetLogicalDescendants().OfType<RadioButton>()
            .Single(button => (button.Content as string)?.StartsWith(label, StringComparison.Ordinal) == true)
            .IsChecked = true;
        Dispatcher.UIThread.RunJobs();
    }

    private static List<string> Filters(Control root) =>
        [.. root.GetVisualDescendants().OfType<Segment>().Single(s => AutomationProperties.GetName(s) == "Powerplay state")
            .GetLogicalDescendants().OfType<RadioButton>().Select(button => (string)button.Content!)];

    private static void Press(Control row) =>
        row.RaiseEvent(new PointerPressedEventArgs(
            row,
            new Pointer(0, PointerType.Mouse, true),
            row,
            default,
            0,
            new PointerPointProperties(RawInputModifiers.LeftMouseButton, PointerUpdateKind.LeftButtonPressed),
            KeyModifiers.None));

    [AvaloniaFact]
    public void OpeningPowerplayAsksOnceAtTwentyLightYearsForAFortifiedSystem()
    {
        var (panel, window, systems) = Shown();

        Assert.Equal("Fortified", TheSystemPageListsItsStationsTests.Ltt7786Profile().Powerplay?.State);
        Assert.Empty(systems.Asked);

        OpenPowerplay(panel);
        TheSystemPageListsItsStationsTests.Section(panel, "Overview");
        OpenPowerplay(panel);

        Assert.Equal([("LTT 7786", 20d)], systems.Asked);

        window.Close();
    }

    [AvaloniaFact]
    public void AStrongholdReachesThirtyLightYears()
    {
        var (panel, window, systems) = Shown(profile => profile with { Powerplay = profile.Powerplay! with { State = "Stronghold" } });

        OpenPowerplay(panel);

        Assert.Equal([("LTT 7786", 30d)], systems.Asked);
        Assert.True(Shows(panel, "Within 30 ly, nearest first. Choose one to open it here."));

        window.Close();
    }

    [AvaloniaFact]
    public void TheStateCountsAndFilterReadTheLtt7786Capture()
    {
        var (panel, window, _) = Shown();
        OpenPowerplay(panel);

        Assert.Equal(["All 49", "Exploited 38", "Fortified 8", "Stronghold 3"], Filters(panel));
        Assert.Equal(49, Rows(panel).Count);
        Assert.Equal("LHS 480", AutomationProperties.GetName(Rows(panel)[0]));
        Assert.True(Shows(panel, "Showing 49 of 49. At most 100 are listed.".ToUpperInvariant()));

        Filter(panel, "Fortified");
        Assert.Equal(8, Rows(panel).Count);
        Assert.True(Shows(panel, "Showing 8 of 49. At most 100 are listed.".ToUpperInvariant()));

        Filter(panel, "Stronghold");
        Assert.Equal(3, Rows(panel).Count);

        window.Close();
    }

    [AvaloniaFact]
    public void TheFiguresDrawFromTheSystemsOwnStanding()
    {
        var standing = TheSystemPageListsItsStationsTests.Ltt7786Profile().Powerplay!;
        var (panel, window, _) = Shown();
        OpenPowerplay(panel);

        Assert.Equal("Jerome Archer", standing.ControllingPower);
        Assert.True(Shows(panel, "Jerome Archer"));
        Assert.True(Shows(panel, "67.4%"));
        Assert.True(Shows(panel, "187,320"));
        Assert.True(Shows(panel, "83,042"));
        Assert.True(Shows(panel, "6.12 ly"));

        window.Close();
    }

    [AvaloniaFact]
    public void ASystemWithNoPowerplaySaysSoInOneLine()
    {
        var (panel, window, _) = Shown(profile => profile with { Powerplay = null });
        OpenPowerplay(panel);

        Assert.True(Shows(panel, StarSystemPage.NoPowerplay));
        Assert.False(Shows(panel, "CONTROL PROGRESS") && Shows(panel, "67.4%"));

        window.Close();
    }

    [AvaloniaFact]
    public void ChoosingARowOpensThatSystemAtOverviewAndStopsFollowing()
    {
        var (panel, window, systems) = Shown();
        OpenPowerplay(panel);

        var page = panel.GetVisualDescendants().OfType<StarSystemPage>().Single();
        Assert.True(page.Following);

        var first = Rows(panel)[0];
        var name = AutomationProperties.GetName(first)!;
        Press(first);
        Until(() => !page.Following && Rows(panel).Count == 0);

        Assert.False(page.Following);
        Assert.True(Shows(panel, "Star system".ToUpperInvariant()) || Shows(panel, "Star system"));

        OpenPowerplay2(panel, name);
        Assert.Equal(("LTT 7786", 20d), systems.Asked[0]);
        Assert.Equal((name, 20d), systems.Asked[1]);

        window.Close();
    }

    private static void OpenPowerplay2(PanelView panel, string name)
    {
        TheSystemPageListsItsStationsTests.Section(panel, "Powerplay");
        Until(() => !Shows(panel, $"Asking Spansh for Powerplay systems near {name}."));
    }

    [AvaloniaFact]
    public void AFailedNeighbourRequestKeepsTheFiguresAndNoticesInTheGroupOnly()
    {
        var (panel, window, _) = Shown(fails: new GalaxyUnavailableException("Spansh did not answer in time."));
        OpenPowerplay(panel);

        Assert.True(Shows(panel, "Jerome Archer"));
        Assert.True(Shows(panel, "Spansh did not answer in time."));
        Assert.Single(panel.GetVisualDescendants().OfType<Notice>(), notice => notice.Text == "Spansh did not answer in time.");
        Assert.Empty(Rows(panel));

        window.Close();
    }

    [AvaloniaFact]
    public void PowerplayIsCaptured()
    {
        using var look = AppLook.Put();

        var (panel, window, _) = Shown();
        OpenPowerplay(panel);

        using var frame = window.CaptureRenderedFrame()!;
        frame.SaveCapture("system-powerplay.png");

        window.Width = 1024;
        window.Height = 640;
        Dispatcher.UIThread.RunJobs();

        using var small = window.CaptureRenderedFrame()!;
        small.SaveCapture("system-powerplay-1024.png");

        window.Close();
    }
}
