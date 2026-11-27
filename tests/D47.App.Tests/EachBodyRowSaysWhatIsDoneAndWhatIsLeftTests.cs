using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using D47.App.Controls;
using D47.App.Panel;
using D47.Core.Interface;
using D47.Core.Journal;
using D47.Core.Knowledge;
using D47.Core.Storage;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.App.Tests;

/// <summary>
/// Search › System marks each body with what the Commander has mapped and sampled there and what is still worth doing,
/// from <see cref="OwnSystem"/>, <see cref="BiologyInSystem"/> and <see cref="SystemWorth"/>. Captures go to
/// <see cref="TestSurface.CaptureDirectory"/>.
/// </summary>
public sealed class EachBodyRowSaysWhatIsDoneAndWhatIsLeftTests
{
    private const long Here = 1000300;
    private const string Fid = "F1";
    private const string Journal = "j/Journal.2026-11-20T090000.01.log";

    private static string Body(int bodyId) => $"Fixture {bodyId}";

    private static readonly string[] Opening =
    [
        """{"timestamp":"2026-11-20T09:00:00Z","event":"Commander","FID":"F1","Name":"Fixture"}""",
        """{"timestamp":"2026-11-20T09:00:01Z","event":"LoadGame","FID":"F1","Commander":"Fixture","Credits":1}""",
        $$"""{"timestamp":"2026-11-20T09:00:02Z","event":"Location","StarSystem":"Fixture","SystemAddress":{{Here}},"StarPos":[0,0,0]}""",
    ];

    private static string Star() => $$"""
        {"timestamp":"2026-11-20T10:00:30Z","event":"Scan","ScanType":"AutoScan","StarSystem":"Fixture","SystemAddress":{{Here}},
         "BodyID":0,"BodyName":"Fixture","StarType":"K","Subclass":2,"StellarMass":0.8,"Radius":500000000.0,"Luminosity":"V",
         "SurfaceTemperature":4800.0,"DistanceFromArrivalLS":0.0,"WasDiscovered":true,"WasMapped":false}
        """;

    private static string Rocky(int bodyId) => $$"""
        {"timestamp":"2026-11-20T10:01:00Z","event":"Scan","ScanType":"Detailed","StarSystem":"Fixture","SystemAddress":{{Here}},
         "BodyID":{{bodyId}},"BodyName":"Fixture {{bodyId}}","PlanetClass":"Rocky body","Atmosphere":"thin carbon dioxide atmosphere",
         "Volcanism":"","SurfaceGravity":1.9,"SurfaceTemperature":175.0,"SurfacePressure":1200.0,"Landable":true,"MassEM":0.02,
         "TerraformState":"","DistanceFromArrivalLS":400.0,"WasDiscovered":true,"WasMapped":true}
        """;

    private static string Water(int bodyId) => $$"""
        {"timestamp":"2026-11-20T10:01:00Z","event":"Scan","ScanType":"Detailed","StarSystem":"Fixture","SystemAddress":{{Here}},
         "BodyID":{{bodyId}},"BodyName":"Fixture {{bodyId}}","PlanetClass":"Water world","Atmosphere":"","Landable":false,
         "MassEM":1.0,"TerraformState":"","DistanceFromArrivalLS":900.0,"WasDiscovered":true,"WasMapped":false}
        """;

    private static string Signals(int bodyId, string type, int count) => $$"""
        {"timestamp":"2026-11-20T10:02:00Z","event":"FSSBodySignals","BodyName":"Fixture {{bodyId}}","BodyID":{{bodyId}},"SystemAddress":{{Here}},
         "Signals":[{"Type":"$SAA_SignalType_{{type}};","Type_Localised":"{{type}}","Count":{{count}}}]}
        """;

    private static string Mapped(int bodyId) => $$"""
        {"timestamp":"2026-11-20T10:29:00Z","event":"SAAScanComplete","BodyName":"Fixture {{bodyId}}","SystemAddress":{{Here}},
         "BodyID":{{bodyId}},"ProbesUsed":4,"EfficiencyTarget":6}
        """;

    private static string Surveyed(int bodyId, string genus) => $$"""
        {"timestamp":"2026-11-20T10:30:00Z","event":"SAASignalsFound","BodyName":"Fixture {{bodyId}}","BodyID":{{bodyId}},"SystemAddress":{{Here}},
         "Signals":[{"Type":"$SAA_SignalType_Biological;","Type_Localised":"Biological","Count":1}],
         "Genuses":[{"Genus":"$Codex_Ent_{{genus}}_Genus_Name;","Genus_Localised":"{{genus}}"}]}
        """;

    private static IEnumerable<string> Completed(int bodyId, string genus) =>
        new[] { "Log", "Sample", "Sample" }.Select((scanType, i) => $$"""
            {"timestamp":"2026-11-20T11:0{{i}}:00Z","event":"ScanOrganic","ScanType":"{{scanType}}",
             "Genus":"$Codex_Ent_{{genus}}_Genus_Name;","Genus_Localised":"{{genus}}",
             "Species":"$Codex_Ent_{{genus}}_01_Name;","Species_Localised":"{{genus}} Tela",
             "SystemAddress":{{Here}},"Body":{{bodyId}}}
            """);

    /// <summary>A star; 2 a water world no one mapped; 4 mapped with a genus to sample; 5 with Guardian signals; 6 mapped and sampled.</summary>
    private static readonly string[] Scans =
    [
        Star(),
        Water(2),
        Rocky(4),
        Signals(4, "Biological", 1),
        Mapped(4),
        Surveyed(4, "Bacterium"),
        Rocky(5),
        Signals(5, "Guardian", 2),
        Rocky(6),
        Mapped(6),
        Surveyed(6, "Stratum"),
        .. Completed(6, "Stratum"),
    ];

    private static JournalEvent Parse(string json)
    {
        Assert.True(JournalEvent.TryParse(json.ReplaceLineEndings(string.Empty), NullLogger.Instance, out var parsed));
        return parsed!;
    }

    private static List<JournalEvent> Events(IEnumerable<string> lines) => [.. lines.Select(Parse)];

    /// <summary>The Commander's state, own scans and cartography as this session leaves them, or as a later session sees them.</summary>
    private sealed class World
    {
        public required CommanderGameState State { get; init; }

        public required OwnScanIndex Index { get; init; }

        public required CartographyLedger Ledger { get; init; }

        public static World ThisSession()
        {
            var events = Events([.. Opening, .. Scans]);
            var store = new GameStateStore();
            var index = new OwnScanIndex(new MemoryFileSystem(), NullLogger.Instance);

            foreach (var journalEvent in events)
            {
                store.Apply(journalEvent);
            }

            index.Apply(events);

            return new World { State = store.Active!, Index = index, Ledger = Folded(events) };
        }

        /// <summary>A new session that has not been to the system: the scans are on disk, and sampling and biology are restored.</summary>
        public static World LaterSession()
        {
            var before = ThisSession().State;
            var store = new GameStateStore
            {
                Restore = _ => before.Sampling,
                RestoreBiology = _ => before.Biology,
            };

            var files = new MemoryFileSystem();
            files.WriteText(Journal, string.Join("\n", Opening.Concat(Scans).Select(line => line.ReplaceLineEndings(string.Empty))));

            var index = new OwnScanIndex(files, NullLogger.Instance);
            index.FoldHistory([Journal], TestContext.Current.CancellationToken);

            foreach (var journalEvent in Events(Opening.Take(2)))
            {
                store.Apply(journalEvent);
                index.Apply([journalEvent]);
            }

            return new World { State = store.Active!, Index = index, Ledger = Folded(Events([.. Opening, .. Scans])) };
        }

        private static CartographyLedger Folded(IReadOnlyList<JournalEvent> events)
        {
            var ledger = new CartographyLedger(null, new MemoryFileSystem(), NullLogger.Instance);
            ledger.FoldHistory([], TestContext.Current.CancellationToken);
            ledger.Apply(events);

            return ledger;
        }

        public StarSystemSurface Surface(IStarSystemService systems) =>
            new(systems, () => new JournalLocation("Fixture", null, false, null) { SystemAddress = Here })
            {
                OwnScans = (address, token) => Index.Read(Fid, address, token),
                OwnScanVersion = address => Index.Version(Fid, address),
                Biology = (address, region, surveyed) => BiologyInSystem.For(State, address, region, surveyed, null),
                Worth = (address, position, own) => SystemWorth.For(State, address, position, Ledger, 500_000, [], own),
            };
    }

    private sealed class Systems(bool fails) : StubStarSystemService
    {
        public const string Failure = "The galaxy search took too long to answer.";

        public override Task<StarSystemProfile?> ProfileAsync(long systemAddress, CancellationToken cancellationToken) =>
            fails ? throw new GalaxyUnavailableException(Failure) : Task.FromResult<StarSystemProfile?>(null);

        public override Task<IReadOnlyList<SystemNameMatch>> MatchNamesAsync(string typed, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<SystemNameMatch>>([]);

        public override Task<IReadOnlyList<SystemNameMatch>> PrefixNamesAsync(string typed, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<SystemNameMatch>>([]);

        public override Task<PowerplayNeighbourhood> PowerplayNearAsync(string system, double lightYears, CancellationToken cancellationToken) =>
            Task.FromResult(new PowerplayNeighbourhood(0, []));
    }

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

    private static void Save(Window window, string name)
    {
        Dispatcher.UIThread.RunJobs();

        using var frame = window.CaptureRenderedFrame()!;
        frame.SaveCapture(name);
    }

    private static Segment Sections(Control within) =>
        within.GetVisualDescendants().OfType<Segment>()
            .Single(segment => Avalonia.Automation.AutomationProperties.GetName(segment) == "Sections");

    private static void ShowBodies(Control within)
    {
        Until(() => within.GetVisualDescendants().OfType<Segment>().Any());
        Sections(within).GetVisualDescendants().OfType<RadioButton>().ElementAt(2).IsChecked = true;
        Dispatcher.UIThread.RunJobs();
    }

    private static Border Row(Control within, string name) =>
        within.GetVisualDescendants().OfType<Border>()
            .Single(border => border.Classes.Contains(StarSystemPage.BodySlabClass) && Avalonia.Automation.AutomationProperties.GetName(border) == name);

    /// <summary>The words and values on a row's line of marks, in order.</summary>
    private static IReadOnlyList<string> Marks(Control within, string name) =>
        [.. Row(within, name).GetVisualDescendants().OfType<WrapPanel>()
            .Where(line => line.Classes.Contains(StarSystemPage.BodyMarksClass))
            .SelectMany(line => line.GetVisualDescendants().OfType<TextBlock>())
            .Select(block => block.Text ?? string.Empty)];

    private static IReadOnlyList<string> Texts(Control within) =>
        [.. within.GetVisualDescendants().OfType<TextBlock>().Select(block => block.Text ?? string.Empty)];

    private static readonly string[] Named = ["Fixture", Body(2), Body(4), Body(5), Body(6)];

    [AvaloniaFact]
    public void ABodyTheCommanderMappedShowsTheMappedMark()
    {
        var (panel, window) = Shown(World.ThisSession().Surface(new Systems(fails: false)));

        ShowBodies(panel);

        Assert.Equal(BodyRecord.MappedByYou.ToUpperInvariant(), Marks(panel, Body(4))[0]);
        Assert.Contains("BIOLOGY 0 OF 1", Marks(panel, Body(4)));
        Assert.Empty(Marks(panel, "Fixture"));

        window.Close();
    }

    [AvaloniaFact]
    public void ABodyWithEveryGenusDoneShowsItsBiologyAsDone()
    {
        var (panel, window) = Shown(World.ThisSession().Surface(new Systems(fails: false)));

        ShowBodies(panel);

        Assert.Equal([BodyRecord.MappedByYou.ToUpperInvariant(), "✓ BIOLOGY 1 OF 1"], Marks(panel, Body(6)));

        window.Close();
    }

    [AvaloniaFact]
    public void ABodySystemWorthListsShowsAsWorthDoing()
    {
        var (panel, window) = Shown(World.ThisSession().Surface(new Systems(fails: false)));

        ShowBodies(panel);

        Assert.Equal(["GUARDIAN SIGNALS"], Marks(panel, Body(5)));
        Save(window, "system-body-marks.png");

        var water = Marks(panel, Body(2));
        Assert.Equal("FIRST MAPPING", water[0]);
        Assert.EndsWith("M CR", water[1], StringComparison.Ordinal);

        window.Close();
    }

    [AvaloniaFact]
    public void ASystemFromAnEarlierSessionGivesTheSameMarksAsTheSameScansThisSession()
    {
        var now = World.ThisSession();
        var later = World.LaterSession();

        Assert.Empty(later.State.Scans.InSystem(Here));

        var (nowPanel, nowWindow) = Shown(now.Surface(new Systems(fails: false)));
        ShowBodies(nowPanel);
        var expected = Named.Select(name => string.Join(" | ", Marks(nowPanel, name))).ToList();
        nowWindow.Close();

        var (laterPanel, laterWindow) = Shown(later.Surface(new Systems(fails: false)));
        ShowBodies(laterPanel);
        var actual = Named.Select(name => string.Join(" | ", Marks(laterPanel, name))).ToList();
        laterWindow.Close();

        Assert.Contains(expected, marks => marks.Contains("GUARDIAN", StringComparison.Ordinal));
        Assert.Equal(expected, actual);
    }

    [AvaloniaFact]
    public void AFailedSearchShowsTheCommandersScansUnderItsNotice()
    {
        var (panel, window) = Shown(World.ThisSession().Surface(new Systems(fails: true)));

        Until(() => panel.GetVisualDescendants().OfType<Notice>().Any());
        Until(() => panel.GetVisualDescendants().OfType<Segment>().Any());

        var notice = panel.GetVisualDescendants().OfType<StarSystemPage>().Single().GetVisualDescendants().OfType<Notice>().Single();
        Assert.Equal($"{Systems.Failure} {StarSystemPage.OwnScansOnly} {StarSystemPage.AsksAgain}", notice.Text);
        Assert.Equal(3, Sections(panel).GetVisualDescendants().OfType<RadioButton>().Count());
        Assert.Contains(StarSystemPage.FromYourScans.ToUpperInvariant(), Texts(panel));
        Assert.Contains(StarSystemPage.ScannedBodiesNote.ToUpperInvariant(), Texts(panel));
        Assert.Equal(BodyRecord.MappedByYou.ToUpperInvariant(), Marks(panel, Body(4))[0]);
        Save(window, "system-failed-search-own-scans.png");

        window.Close();
    }

    [AvaloniaFact]
    public void TheBodyPageOpensWithTheCommandersRecord()
    {
        var (panel, window) = Shown(World.ThisSession().Surface(new Systems(fails: false)));

        ShowBodies(panel);

        var row = Row(panel, Body(4));
        row.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Enter, Source = row });
        Until(() => Texts(panel).Contains(BodyRecord.MappedByYou));

        var texts = Texts(panel);
        Assert.Contains("0 of 1 genera done", texts);
        Assert.Contains("BIOLOGY", texts);
        Assert.Contains(BodyRecord.RecordHint, texts);
        Save(window, "system-body-page-your-record.png");

        window.Close();
    }
}
