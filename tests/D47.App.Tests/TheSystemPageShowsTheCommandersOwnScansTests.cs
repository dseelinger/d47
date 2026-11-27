using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using D47.App.Controls;
using D47.App.Panel;
using D47.Core.Interface;
using D47.Core.Journal;
using D47.Core.Knowledge;
using Xunit;

namespace D47.App.Tests;

/// <summary>Search › System joins the Commander's own scans to Spansh's record, and shows them alone where Spansh has none.</summary>
public sealed class TheSystemPageShowsTheCommandersOwnScansTests
{
    private const long Address = 4471;
    private const string Name = "Oochost QX-U d3-21";

    private static readonly DateTimeOffset ReportedAt = new(2026, 9, 2, 10, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset ScannedAt = new(2026, 11, 20, 21, 5, 0, TimeSpan.Zero);

    private static readonly BodyProfile Star = new()
    {
        BodyId = 0,
        Name = Name,
        Type = "Star",
        SubType = "M (Red dwarf) Star",
        IsMainStar = true,
        ReportedAt = ReportedAt,
    };

    private static readonly StarSystemProfile Record = new()
    {
        Name = Name,
        SystemAddress = Address,
        Bodies = [Star],
        ReportedAt = ReportedAt,
    };

    private static OwnSystem Own(params int[] planets) => new(Address)
    {
        Name = Name,
        Bodies =
        [
            Star with { ReportedAt = ScannedAt },
            .. planets.Select(id => new BodyProfile
            {
                BodyId = id,
                Name = $"{Name} {id}",
                Type = "Planet",
                SubType = "Icy body",
                ParentId = 0,
                DistanceToArrival = 400 + id,
                ReportedAt = ScannedAt,
            }),
        ],
        Own = new[] { 0 }.Concat(planets).ToDictionary(id => id, _ => new OwnBody(ScannedAt)),
        LatestAt = ScannedAt,
    };

    private sealed class Systems(StarSystemProfile? record) : StubStarSystemService
    {
        private int _asked;

        public int Asked => Volatile.Read(ref _asked);

        public override Task<StarSystemProfile?> ProfileAsync(long systemAddress, CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref _asked);

            return Task.FromResult(systemAddress == Address ? record : null);
        }

        public override Task<IReadOnlyList<SystemNameMatch>> MatchNamesAsync(string typed, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<SystemNameMatch>>([]);

        public override Task<IReadOnlyList<SystemNameMatch>> PrefixNamesAsync(string typed, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<SystemNameMatch>>([]);

        public override Task<PowerplayNeighbourhood> PowerplayNearAsync(string system, double lightYears, CancellationToken cancellationToken) =>
            Task.FromResult(new PowerplayNeighbourhood(0, []));
    }

    private sealed class Scans(OwnSystem? own)
    {
        private int _reads;
        private OwnSystem? _own = own;

        public int Reads => Volatile.Read(ref _reads);

        public int Version { get; set; }

        public OwnSystem? Current
        {
            get => Volatile.Read(ref _own);
            set => Volatile.Write(ref _own, value);
        }

        public OwnSystem? Read(long address, CancellationToken cancellation)
        {
            Interlocked.Increment(ref _reads);

            return address == Address ? Current : null;
        }
    }

    private static StarSystemSurface Surface(Systems systems, Scans scans, TimeProvider? clock = null) =>
        new(systems, () => new JournalLocation(Name, null, false, null) { SystemAddress = Address })
        {
            OwnScans = scans.Read,
            OwnScanVersion = address => address == Address ? scans.Version : 0,
            Clock = clock ?? TimeProvider.System,
        };

    private static (PanelView Panel, Window Window) Shown(StarSystemSurface surface)
    {
        var panel = new PanelView { DataContext = new PanelViewModel() };
        panel.EnableStarSystem(surface);

        var window = new Window { Content = panel, Width = 1280, Height = 960 };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        panel.Tab = PanelTab.Search;
        panel.Nav.SelectRoot(StarSystemPage.RootKey);
        Dispatcher.UIThread.RunJobs();

        return (panel, window);
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

    private static StarSystemPage SystemPage(Control within) => within.GetVisualDescendants().OfType<StarSystemPage>().Single();

    private static void ShowBodies(Control within)
    {
        Until(() => within.GetVisualDescendants().OfType<Segment>().Any());
        var sections = within.GetVisualDescendants().OfType<Segment>()
            .Single(segment => Avalonia.Automation.AutomationProperties.GetName(segment) == "Sections");
        sections.GetVisualDescendants().OfType<RadioButton>().ElementAt(2).IsChecked = true;
        Dispatcher.UIThread.RunJobs();
    }

    private static Border? Row(Control within, string name) =>
        within.GetVisualDescendants().OfType<Border>()
            .SingleOrDefault(border => border.Classes.Contains(D47.App.Theming.ListRow.Class) && Avalonia.Automation.AutomationProperties.GetName(border) == name);

    private static IReadOnlyList<string> Texts(Control within) =>
        [.. within.GetVisualDescendants().OfType<TextBlock>().Select(block => block.Text ?? string.Empty)];

    [AvaloniaFact]
    public void WithNoSpanshRecordTheBodiesAreTheCommandersAndTheCaptionSaysFromYourScans()
    {
        var scans = new Scans(Own(3));
        var (panel, window) = Shown(Surface(new Systems(null), scans));

        ShowBodies(panel);

        Assert.NotNull(Row(panel, $"{Name} 3"));
        Assert.Contains(StarSystemPage.FromYourScans.ToUpperInvariant(), Texts(panel));
        Assert.DoesNotContain("LAST REPORTED", Texts(panel));
        Assert.Contains(StarSystemPage.Stamp(ScannedAt), Texts(panel));

        window.Close();
    }

    [AvaloniaFact]
    public void ABodyOnlyTheCommanderScannedIsListedBesideSpanshsAndOpensItsPage()
    {
        var systems = new Systems(Record);
        var (panel, window) = Shown(Surface(systems, new Scans(Own(4))));

        ShowBodies(panel);

        Assert.NotNull(Row(panel, Name));
        Assert.Contains("LAST REPORTED", Texts(panel));

        var row = Row(panel, $"{Name} 4");
        Assert.NotNull(row);

        row.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Enter, Source = row });
        Until(() => panel.GetVisualDescendants().OfType<SystemBodyPage>().Any(page => page.Body is not null));

        Assert.Equal($"{Name} 4", panel.GetVisualDescendants().OfType<SystemBodyPage>().Single().Body!.Name);
        Assert.Equal(1, systems.Asked);

        window.Close();
    }

    [AvaloniaFact]
    public void WithNeitherRecordTheSystemIsUnrecorded()
    {
        var scans = new Scans(null);
        var (panel, window) = Shown(Surface(new Systems(null), scans));

        Until(() => Texts(panel).Contains(StarSystemPage.Unrecorded));
        Assert.Equal(1, scans.Reads);

        window.Close();
    }

    [AvaloniaFact]
    public void ANewScanRereadsOwnScansWithoutAskingSpanshAgain()
    {
        var clock = new AnOrbitMarkerStaysOnItsDrawnPathTests.FakeClock(ScannedAt);
        var systems = new Systems(Record);
        var scans = new Scans(Own(3));
        var (panel, window) = Shown(Surface(systems, scans, clock));

        ShowBodies(panel);
        Assert.NotNull(Row(panel, $"{Name} 3"));
        Assert.Null(Row(panel, $"{Name} 5"));

        scans.Current = Own(3, 5);
        scans.Version = 1;
        SystemPage(panel).Tick();
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(1, scans.Reads);

        clock.Advance(StarSystemPage.OwnScanPause);
        SystemPage(panel).Tick();
        Until(() => Row(panel, $"{Name} 5") is not null);

        Assert.Equal(2, scans.Reads);
        Assert.Equal(1, systems.Asked);

        SystemPage(panel).Tick();
        Dispatcher.UIThread.RunJobs();
        Assert.Equal(2, scans.Reads);

        window.Close();
    }

    [AvaloniaFact]
    public void ABodyPageOpenedWithoutTheRecordFindsABodyOnlyTheCommanderScanned()
    {
        var page = new SystemBodyPage(Surface(new Systems(Record), new Scans(Own(6))), Address, 6, $"{Name} 6");
        var window = new Window { Content = page, Width = 1280, Height = 960 };
        window.Show();

        Until(() => page.Body is not null);
        Assert.Equal($"{Name} 6", page.Body!.Name);

        window.Close();
    }
}
