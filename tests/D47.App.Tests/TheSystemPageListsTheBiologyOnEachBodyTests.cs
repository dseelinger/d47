using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using D47.App.Controls;
using D47.App.Panel;
using D47.App.Theming;
using D47.Core.Journal;
using D47.Core.Knowledge;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.App.Tests;

/// <summary>Search › System's Biology section lists each body's genera and how far the Commander has sampled them.</summary>
public sealed class TheSystemPageListsTheBiologyOnEachBodyTests
{
    private const long Here = 7_700_100;

    private const long Elsewhere = 7_700_200;

    private const string Region = "Inner Orion Spur";

    private static readonly DateTimeOffset Start = new(2026, 8, 16, 10, 0, 0, TimeSpan.Zero);

    private static int _second;

    private static StarPosition Position => GalacticRegions.LabelAt(GalacticRegions.IdOf(Region)!.Value)!.Value;

    private static string Time() =>
        Start.AddSeconds(Interlocked.Increment(ref _second)).ToString("yyyy-MM-ddTHH:mm:ssZ", System.Globalization.CultureInfo.InvariantCulture);

    private static string SystemName(long address) => address == Here ? "Vask Reach" : "Orrin Deep";

    private static string BodyName(long address, int bodyId) => $"{SystemName(address)} {bodyId}";

    private static StarSystemProfile Profile(long address) => new()
    {
        Name = SystemName(address),
        SystemAddress = address,
        Position = Position,
        Bodies =
        [
            new BodyProfile { BodyId = 0, Name = SystemName(address), Type = "Star", IsMainStar = true },
            new BodyProfile { BodyId = 3, Name = BodyName(address, 3), Type = "Planet", DistanceToArrival = 288, ParentId = 0 },
            new BodyProfile { BodyId = 4, Name = BodyName(address, 4), Type = "Planet", DistanceToArrival = 1_204, ParentId = 0 },
            new BodyProfile { BodyId = 5, Name = BodyName(address, 5), Type = "Planet", DistanceToArrival = 5_310, ParentId = 0 },
        ],
    };

    private sealed class Systems : StubStarSystemService
    {
        public override Task<StarSystemProfile?> ProfileAsync(long systemAddress, CancellationToken cancellationToken) =>
            Task.FromResult<StarSystemProfile?>(systemAddress is Here or Elsewhere ? Profile(systemAddress) : null);

        public override Task<IReadOnlyList<SystemNameMatch>> MatchNamesAsync(string typed, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<SystemNameMatch>>([]);

        public override Task<IReadOnlyList<SystemNameMatch>> PrefixNamesAsync(string typed, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<SystemNameMatch>>([]);

        public override Task<PowerplayNeighbourhood> PowerplayNearAsync(string system, double lightYears, CancellationToken cancellationToken) =>
            Task.FromResult(new PowerplayNeighbourhood(0, []));
    }

    private sealed class Galaxy(SystemBiology answer) : StubGalaxyService
    {
        private int _asked;

        public int Asked => _asked;

        public override Task<SystemBiology> SystemBiologyAsync(long systemAddress, CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref _asked);

            return Task.FromResult(answer);
        }
    }

    private static void Apply(GameStateStore store, string json)
    {
        Assert.True(JournalEvent.TryParse(json, NullLogger.Instance, out var parsed));
        store.Apply(parsed!);
    }

    private static GameStateStore Store()
    {
        var store = new GameStateStore();
        Apply(store, """{"timestamp":"2026-08-16T09:00:00Z","event":"Commander","FID":"F7","Name":"Tamsin Vey"}""");
        Apply(store, $$"""{"timestamp":"2026-08-16T09:30:00Z","event":"Location","StarSystem":"Vask Reach","SystemAddress":{{Here}},"Docked":false}""");

        return store;
    }

    private static void Scan(GameStateStore store, int bodyId) =>
        Apply(store, $$"""
            {"timestamp":"{{Time()}}","event":"Scan","ScanType":"Detailed","BodyName":"{{BodyName(Here, bodyId)}}","BodyID":{{bodyId}},"SystemAddress":{{Here}},
             "PlanetClass":"Rocky body","Atmosphere":"thin carbon dioxide atmosphere","Volcanism":"",
             "SurfaceGravity":2.5,"SurfaceTemperature":200.0,"SurfacePressure":2000.0}
            """);

    private static void Fss(GameStateStore store, int bodyId, int signals) =>
        Apply(store, $$"""
            {"timestamp":"{{Time()}}","event":"FSSBodySignals","BodyName":"{{BodyName(Here, bodyId)}}","BodyID":{{bodyId}},"SystemAddress":{{Here}},
             "Signals":[{"Type":"$SAA_SignalType_Biological;","Type_Localised":"Biological","Count":{{signals}}}]}
            """);

    private static void Mapped(GameStateStore store, int bodyId, params string[] genera) =>
        MappedIn(store, Here, bodyId, genera);

    private static void MappedIn(GameStateStore store, long address, int bodyId, params string[] genera)
    {
        var rows = string.Join(",", genera.Select(genus =>
            "{\"Genus\":\"$Codex_Ent_" + genus + "_Genus_Name;\",\"Genus_Localised\":\"" + genus + "\"}"));

        Apply(store, $$"""
            {"timestamp":"{{Time()}}","event":"SAASignalsFound","BodyName":"{{BodyName(address, bodyId)}}","BodyID":{{bodyId}},"SystemAddress":{{address}},
             "Signals":[{"Type":"$SAA_SignalType_Biological;","Type_Localised":"Biological","Count":{{genera.Length}}}],
             "Genuses":[{{rows}}]}
            """);
    }

    private static void Organic(GameStateStore store, int bodyId, string scanType, string species, string? variant = null)
    {
        var genus = species.Split(' ')[0];
        var variantFields = variant is null ? "" : $",\"Variant\":\"$Codex_Ent_{genus}_Variant;\",\"Variant_Localised\":\"{variant}\"";

        Apply(store, $$"""
            {"timestamp":"{{Time()}}","event":"ScanOrganic","ScanType":"{{scanType}}",
             "Genus":"$Codex_Ent_{{genus}}_Genus_Name;","Genus_Localised":"{{genus}}",
             "Species":"$Codex_Ent_{{genus}}_Name;","Species_Localised":"{{species}}"{{variantFields}},
             "SystemAddress":{{Here}},"Body":{{bodyId}}}
            """);
    }

    private static void Taken(GameStateStore store, int bodyId, string species, int count, string? variant = null)
    {
        for (var i = 0; i < count; i++)
        {
            Organic(store, bodyId, i == 0 ? "Log" : "Sample", species, variant);
        }
    }

    private static StarSystemSurface Surface(
        GameStateStore store,
        Func<JournalLocation?>? here = null,
        SurveyedBiology? surveyed = null,
        IGalaxyService? galaxy = null,
        BiologyHeads? heads = null,
        IReadOnlyList<EarlierFind>? earlier = null) =>
        new(new Systems(), here ?? (() => new JournalLocation("Vask Reach", null, false, null) { SystemAddress = Here }))
        {
            Biology = (address, region, held) => BiologyInSystem.For(store.Active!, address, region, held, earlier),
            Surveyed = surveyed ?? new SurveyedBiology(),
            AskSurveyed = galaxy is null ? null : galaxy.SystemBiologyAsync,
            Heads = heads ?? new BiologyHeads(),
        };

    private static (StarSystemPage Page, Window Window) Shown(StarSystemSurface surface, BodyCodex? codex = null, long? open = null)
    {
        new ThemeManager(Application.Current!, NullLogger<ThemeManager>.Instance).FollowSettings(TestSurface.Settings());

        var page = new StarSystemPage(surface) { Codex = codex ?? BodyCodex.None };
        var window = new Window { Content = page, Width = 1280, Height = 960 };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        if (open is { } address)
        {
            page.OpenSystem(address);
        }

        Until(() => page.GetVisualDescendants().OfType<Segment>().Any());

        return (page, window);
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

    private static Segment Sections(Control within) =>
        within.GetVisualDescendants().OfType<Segment>().Single(segment => Avalonia.Automation.AutomationProperties.GetName(segment) == "Sections");

    private static void ShowBiology(StarSystemPage page)
    {
        Sections(page).GetVisualDescendants().OfType<RadioButton>().ElementAt(4).IsChecked = true;
        Dispatcher.UIThread.RunJobs();
    }

    private static IReadOnlyList<string> Texts(Control within) =>
        [.. within.GetVisualDescendants().OfType<TextBlock>().Select(block => block.Text ?? string.Empty)];

    private static TextBlock Text(Control within, string text) =>
        within.GetVisualDescendants().OfType<TextBlock>().First(block => block.Text == text);

    private static string Valued(string species) =>
        $"{species} — {ExobiologyCatalogue.All.First(entry => entry.Species == species).Value.ToString("N0", System.Globalization.CultureInfo.CurrentCulture)} cr";

    private static Border Row(Control within, string genus) =>
        within.GetVisualDescendants().OfType<Border>()
            .Single(border => border.Classes.Contains(StarSystemPage.GenusRowClass) && Avalonia.Automation.AutomationProperties.GetName(border) == genus);

    private static Button Head(Control within, string body) =>
        within.GetVisualDescendants().OfType<Button>()
            .Single(button => button.Classes.Contains(StarSystemPage.BiologyHeadClass) && Avalonia.Automation.AutomationProperties.GetName(button) == body);

    private static bool IsOpen(Control within, string body) => ((StackPanel)Head(within, body).Parent!).Children.Count > 1;

    private static void Press(Button button)
    {
        button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();
    }

    private static IBrush? Ink(Visual within, string key) => within.FindResource(key) as IBrush;

    [AvaloniaFact]
    public void TheFifthSegmentIsBiologyCountingTheBodiesWithBiology()
    {
        var store = Store();
        Mapped(store, 3, "Bacterium");
        Fss(store, 4, 2);

        var (page, window) = Shown(Surface(store));

        Assert.Equal(["Overview", "Stations 0", "Bodies 4", "Powerplay", "Biology 2"], Sections(page).ItemsSource!.Cast<string>());

        ShowBiology(page);

        Assert.Equal([BodyName(Here, 3), BodyName(Here, 4)], page.GetVisualDescendants().OfType<Button>()
            .Where(button => button.Classes.Contains(StarSystemPage.BiologyHeadClass))
            .Select(button => Avalonia.Automation.AutomationProperties.GetName(button)));
        Assert.Contains("288 Ls", Texts(Head(page, BodyName(Here, 3))));
        Assert.Contains("1 GENUS · 0 DONE", Texts(Head(page, BodyName(Here, 3))));

        window.Close();
    }

    [AvaloniaFact]
    public void ASystemWithNoBiologyOnRecordSaysNothingIsKnown()
    {
        var (page, window) = Shown(Surface(Store()));

        Assert.Equal("Biology", Sections(page).ItemsSource!.Cast<string>().Last());

        ShowBiology(page);

        Assert.Contains(StarSystemPage.NothingKnown, Texts(page));
        Assert.DoesNotContain(page.GetVisualDescendants().OfType<Button>(), button => button.Classes.Contains(StarSystemPage.BiologyHeadClass));

        window.Close();
    }

    [AvaloniaFact]
    public void EachGenusStateIsWrittenInItsInk()
    {
        var store = Store();
        Mapped(store, 3, "Bacterium", "Stratum", "Fungoida", "Tussock");
        Taken(store, 3, "Bacterium Tela", 3);
        Taken(store, 3, "Fungoida Setisis", 1);
        Organic(store, 3, "Log", "Tussock Pennata");

        var (page, window) = Shown(Surface(store));
        ShowBiology(page);

        Assert.Equal(Ink(page, ThemeManager.BlueKey), Text(Row(page, "Bacterium"), "✓ Done · 3 / 3").Foreground);
        Assert.Equal(Ink(page, ThemeManager.CyanKey), Text(Row(page, "Tussock"), "Sampling · 1 / 3").Foreground);
        Assert.Equal(Ink(page, ThemeManager.GreyKey), Text(Row(page, "Stratum"), "Not started").Foreground);
        Assert.Equal(Ink(page, ThemeManager.WarnKey), Text(Row(page, "Fungoida"), "Abandoned · 1 / 3").Foreground);

        Assert.Equal(Ink(page, ThemeManager.WhiteKey), Text(Row(page, "Bacterium"), Valued("Bacterium Tela")).Foreground);
        Assert.Equal(Ink(page, ThemeManager.CyanKey), Text(Row(page, "Tussock"), Valued("Tussock Pennata")).Foreground);

        Assert.Contains("One sample lost when you logged a different species.", Texts(Row(page, "Fungoida")));
        Assert.DoesNotContain("One sample lost when you logged a different species.", Texts(Row(page, "Tussock")));

        using (var frame = window.CaptureRenderedFrame()!)
        {
            frame.SaveCapture("system-biology.png");
        }

        window.Close();
    }

    [AvaloniaFact]
    public void ReportedAndPossibleSpeciesAreGreyAndTagged()
    {
        Assert.Equal(Region, GalacticRegions.Find(Position));

        var store = Store();
        Scan(store, 3);
        Mapped(store, 3, "Bacterium");
        Mapped(store, 4, "Stratum");

        var surveyed = new SurveyedBiology();
        surveyed.Set(new SystemBiology(
            Here,
            [new SurveyedBody(BodyName(Here, 4), 4, 0, [new ExobiologySpecies("Stratum", "Stratum Tectonicas", 1, 19_010_800)])]));

        var (page, window) = Shown(Surface(store, surveyed: surveyed));
        ShowBiology(page);

        var reported = Row(page, "Stratum");
        Assert.Equal(Ink(page, ThemeManager.GreyKey), Text(reported, "Stratum Tectonicas — 19,010,800 cr").Foreground);
        Assert.Contains("REPORTED", Texts(reported));

        var possible = Row(page, "Bacterium");
        Assert.Contains("POSSIBLE", Texts(possible));
        Assert.DoesNotContain("REPORTED", Texts(possible));
        Assert.Contains(Texts(possible), text => text.StartsWith("Bacterium ", StringComparison.Ordinal));

        window.Close();
    }

    [AvaloniaFact]
    public void MoreThanThreeSpeciesAreCountedAfterTheThird()
    {
        var species = new[] { "A", "B", "C", "D", "E" }
            .Select(name => new SpeciesLine($"Bacterium {name}", SpeciesSource.Possible, null))
            .ToList();

        Assert.Equal("Bacterium A · Bacterium B · Bacterium C and 2 more", StarSystemPage.SpeciesText(species));
        Assert.Equal("Bacterium A · Bacterium B", StarSystemPage.SpeciesText(species[..2]));
        Assert.Equal("Two samples lost when you logged a different species.", StarSystemPage.LostText(2));
    }

    [AvaloniaFact]
    public void ABodyNotMappedSaysToMapIt()
    {
        var store = Store();
        Fss(store, 5, 2);

        var (page, window) = Shown(Surface(store));
        ShowBiology(page);

        var head = Head(page, BodyName(Here, 5));
        Assert.Contains("2 BIOLOGICAL SIGNALS · NOT MAPPED", Texts(head));
        Assert.True(IsOpen(page, BodyName(Here, 5)));
        Assert.Contains(StarSystemPage.NotMapped, Texts(page));

        window.Close();
    }

    [AvaloniaFact]
    public void BodiesWithWorkLeftOrTheCommanderOnThemStartOpenAndAPressedHeadStaysAsLeft()
    {
        var store = Store();
        Mapped(store, 3, "Bacterium");
        Taken(store, 3, "Bacterium Tela", 3);
        Mapped(store, 4, "Stratum");
        Mapped(store, 5, "Bacterium");
        Taken(store, 5, "Bacterium Tela", 3);

        var heads = new BiologyHeads();
        Func<JournalLocation?> onFive = () => new JournalLocation("Vask Reach", BodyName(Here, 5), false, null) { SystemAddress = Here, BodyId = 5 };

        var (page, window) = Shown(Surface(store, onFive, heads: heads));
        ShowBiology(page);

        Assert.False(IsOpen(page, BodyName(Here, 3)));
        Assert.True(IsOpen(page, BodyName(Here, 4)));
        Assert.True(IsOpen(page, BodyName(Here, 5)));
        Assert.Contains("1 GENUS · ✓ ALL DONE", Texts(Head(page, BodyName(Here, 3))));
        Assert.Equal(Ink(page, ThemeManager.BlueKey), Text(Head(page, BodyName(Here, 3)), "1 GENUS · ✓ ALL DONE").Foreground);

        Assert.Contains("HERE", Texts(Head(page, BodyName(Here, 5))));
        Assert.Equal(Ink(page, ThemeManager.CyanKey), Text(Head(page, BodyName(Here, 5)), BodyName(Here, 5)).Foreground);
        Assert.DoesNotContain("HERE", Texts(Head(page, BodyName(Here, 4))));

        Press(Head(page, BodyName(Here, 4)));
        Press(Head(page, BodyName(Here, 3)));

        Assert.False(IsOpen(page, BodyName(Here, 4)));
        Assert.True(IsOpen(page, BodyName(Here, 3)));

        page.Tick();
        Dispatcher.UIThread.RunJobs();
        window.Close();

        var (again, second) = Shown(Surface(store, onFive, heads: heads));
        ShowBiology(again);

        Assert.False(IsOpen(again, BodyName(Here, 4)));
        Assert.True(IsOpen(again, BodyName(Here, 3)));

        second.Close();
    }

    [AvaloniaFact]
    public void CodexOpensTheEntryAndTheGenusWhereThereIsNoEntry()
    {
        var store = Store();
        Mapped(store, 3, "Bacterium", "Tussock");
        Taken(store, 3, "Bacterium Tela", 3, "Bacterium Tela - Orange");

        var entries = new List<CodexEntry>();
        var genera = new List<string>();
        var codex = new BodyCodex(CodexCatalogue.Table, null, entries.Add, genera.Add);

        var (page, window) = Shown(Surface(store), codex);
        ShowBiology(page);

        Press(Row(page, "Bacterium").GetVisualDescendants().OfType<Button>().Single(button => button.Name == BodyCodex.LinkName));
        Press(Row(page, "Tussock").GetVisualDescendants().OfType<Button>().Single(button => button.Name == BodyCodex.LinkName));

        Assert.Equal([2_320_701L], entries.Select(entry => entry.EntryId));
        Assert.Equal(["Tussock"], genera);

        window.Close();
    }

    [AvaloniaFact]
    public void ASurfaceWithNoCodexDrawsNoCodexTile()
    {
        var store = Store();
        Mapped(store, 3, "Bacterium");

        var (page, window) = Shown(Surface(store));
        ShowBiology(page);

        Assert.DoesNotContain(page.GetVisualDescendants().OfType<Button>(), button => button.Name == BodyCodex.LinkName);

        window.Close();
    }

    [AvaloniaFact]
    public void ASystemThatIsNotTheCommandersShowsItsRecordsWithNoHereTag()
    {
        var store = Store();
        MappedIn(store, Elsewhere, 3, "Osseus");
        Mapped(store, 3, "Bacterium");

        Func<JournalLocation?> onThree = () => new JournalLocation("Vask Reach", BodyName(Here, 3), false, null) { SystemAddress = Here, BodyId = 3 };

        var (page, window) = Shown(Surface(store, onThree), open: Elsewhere);
        Until(() => page.ShownAddress == Elsewhere && Sections(page).ItemsSource!.Cast<string>().Last() == "Biology 1");
        ShowBiology(page);

        Assert.NotNull(Head(page, BodyName(Elsewhere, 3)));
        Assert.NotNull(Row(page, "Osseus"));
        Assert.DoesNotContain("HERE", Texts(page));
        Assert.DoesNotContain(page.GetVisualDescendants().OfType<Border>(), border => Avalonia.Automation.AutomationProperties.GetName(border) == "Bacterium");

        window.Close();
    }

    [AvaloniaFact]
    public void TheSectionRedrawsWhenTheCommanderSamples()
    {
        var store = Store();
        Mapped(store, 3, "Bacterium");

        var (page, window) = Shown(Surface(store));
        ShowBiology(page);

        Assert.Contains("Not started", Texts(Row(page, "Bacterium")));

        Organic(store, 3, "Log", "Bacterium Tela");

        Assert.True(page.Tick());
        Dispatcher.UIThread.RunJobs();

        Assert.Contains("Sampling · 1 / 3", Texts(Row(page, "Bacterium")));
        Assert.False(page.Tick());

        window.Close();
    }

    [AvaloniaFact]
    public void ASystemTheHolderLacksIsAskedOnceAndWrittenToIt()
    {
        var store = Store();
        Mapped(store, 4, "Stratum");

        var answer = new SystemBiology(
            Here,
            [new SurveyedBody(BodyName(Here, 4), 4, 0, [new ExobiologySpecies("Stratum", "Stratum Tectonicas", 1, 19_010_800)])]);
        var galaxy = new Galaxy(answer);
        var surveyed = new SurveyedBiology();

        var (page, window) = Shown(Surface(store, surveyed: surveyed, galaxy: galaxy));
        ShowBiology(page);

        Until(() => surveyed.TryGet(Here, out _) && Texts(page).Contains("REPORTED"));

        for (var i = 0; i < 5; i++)
        {
            page.Tick();
            Dispatcher.UIThread.RunJobs();
        }

        ShowBiology(page);

        Assert.Equal(1, galaxy.Asked);
        Assert.True(surveyed.TryGet(Here, out var held));
        Assert.Same(answer, held);
        Assert.Contains("Stratum Tectonicas — 19,010,800 cr", Texts(Row(page, "Stratum")));

        window.Close();
    }

    private static SurveyedBiology Held(params SurveyedBody[] bodies)
    {
        var held = new SurveyedBiology();
        held.Set(new SystemBiology(Here, bodies));

        return held;
    }

    private static SurveyedBody Reports(int bodyId, long landmark, params ExobiologySpecies[] species) =>
        new(BodyName(Here, bodyId), bodyId, landmark, species);

    [AvaloniaFact]
    public void BodiesOnlySpanshReportsAreListedAndCounted()
    {
        var store = Store();
        Mapped(store, 3, "Bacterium");

        var held = Held(
            Reports(4, 100, new ExobiologySpecies("Stratum", "Stratum Cucumisis", 7, 16_202_800)),
            Reports(5, 200, new ExobiologySpecies("Fungoida", "Fungoida Setisis", 1, 1_000_000)));

        var (page, window) = Shown(Surface(store, surveyed: held));

        Assert.Equal("Biology 3", Sections(page).ItemsSource!.Cast<string>().Last());

        ShowBiology(page);

        Assert.Equal(3, page.GetVisualDescendants().OfType<Button>().Count(button => button.Classes.Contains(StarSystemPage.BiologyHeadClass)));

        window.Close();
    }

    [AvaloniaFact]
    public void AReportedSpeciesShowsItsCountAndValueAndACountOfOneHasNoMultiplier()
    {
        var store = Store();
        var held = Held(
            Reports(4, 100, new ExobiologySpecies("Stratum", "Stratum Cucumisis", 7, 16_202_800)),
            Reports(5, 200, new ExobiologySpecies("Fungoida", "Fungoida Setisis", 1, 1_000_000)));

        var (page, window) = Shown(Surface(store, surveyed: held));
        ShowBiology(page);

        Assert.Contains("Stratum Cucumisis ×7 — 16,202,800 cr", Texts(Row(page, "Stratum")));
        Assert.Contains("Fungoida Setisis — 1,000,000 cr", Texts(Row(page, "Fungoida")));

        window.Close();
    }

    [AvaloniaFact]
    public void ABodyHeadShowsTheSubTypeAndTheReportedValue()
    {
        var store = Store();
        var held = Held(Reports(4, 57_257_100, new ExobiologySpecies("Stratum", "Stratum Cucumisis", 7, 16_202_800)));

        var (page, window) = Shown(Surface(store, surveyed: held));
        ShowBiology(page);

        var texts = Texts(Head(page, BodyName(Here, 4)));

        Assert.Contains("57,257,100 cr", texts);
        Assert.Contains("1,204 Ls", texts);

        window.Close();
    }

    [AvaloniaFact]
    public void ABodyWhoseGeneraAreReportedHasTheGenusSummaryAndNoNotMappedSlab()
    {
        var store = Store();
        var held = Held(Reports(4, 100, new ExobiologySpecies("Stratum", "Stratum Cucumisis", 7, 16_202_800)));

        var (page, window) = Shown(Surface(store, surveyed: held));
        ShowBiology(page);

        Assert.Contains("1 GENUS · 0 DONE", Texts(Head(page, BodyName(Here, 4))));
        Assert.DoesNotContain(StarSystemPage.NotMapped, Texts(page));
        Assert.True(IsOpen(page, BodyName(Here, 4)));

        window.Close();
    }

    [AvaloniaFact]
    public void ASystemWithNoCommanderRecordAsksSpanshOnceAndShowsWhatItReports()
    {
        var answer = new SystemBiology(
            Here,
            [Reports(4, 100, new ExobiologySpecies("Stratum", "Stratum Cucumisis", 7, 16_202_800))]);
        var galaxy = new Galaxy(answer);
        var surveyed = new SurveyedBiology();

        var (page, window) = Shown(Surface(Store(), surveyed: surveyed, galaxy: galaxy));
        ShowBiology(page);

        Until(() => Texts(page).Contains("Stratum Cucumisis ×7 — 16,202,800 cr"));

        Assert.DoesNotContain(StarSystemPage.NothingKnown, Texts(page));
        Assert.Equal(1, galaxy.Asked);

        window.Close();
    }

    [AvaloniaFact]
    public void OpeningASystemOnOverviewAsksForItsBiology()
    {
        var galaxy = new Galaxy(new SystemBiology(Elsewhere, []));

        var (page, window) = Shown(Surface(Store(), galaxy: galaxy), open: Elsewhere);

        Until(() => galaxy.Asked == 1);

        Assert.Equal(1, galaxy.Asked);
        Assert.NotNull(page);

        window.Close();
    }

    [AvaloniaFact]
    public void ASpeciesOnlyCanonnHoldsShowsReportedWithNoFigures()
    {
        var store = Store();
        Mapped(store, 3, "Bacterium");

        var earlier = new[] { new EarlierFind(BodyName(Here, 3), 2_320_103, "Bacterium Aurasus - Yellow", 0, 0) };

        var (page, window) = Shown(Surface(store, earlier: earlier));
        ShowBiology(page);

        var row = Row(page, "Bacterium");
        Assert.Contains("Bacterium Aurasus", Texts(row));
        Assert.Contains("REPORTED", Texts(row));

        window.Close();
    }
}
