using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Avalonia.VisualTree;
using D47.App.Controls;
using D47.App.Panel;
using D47.App.Settings;
using D47.App.Theming;
using D47.Core.Actions;
using D47.Core.Configuration;
using D47.Core.Coverage;
using D47.Core.Debrief;
using D47.Core.Hotas;
using D47.Core.Input;
using D47.Core.Interface;
using D47.Core.Logbook;
using D47.Core.Lore;
using D47.Core.Memory;
using D47.Core.Persona;
using D47.Core.Utilities;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.App.Tests;

/// <summary>
/// The Utilities tab, Learned phrases, the in-app help page and the shared modal dialogs, rendered on
/// each theme and saved to <see cref="TestSurface.CaptureDirectory"/> for comparison with brief 03
/// (#406).
/// </summary>
public class UtilitiesHelpAndTheDialogsAreDrawnOnTheKitTests
{
    private static readonly DateTimeOffset Instant = new(2026, 9, 23, 12, 0, 0, TimeSpan.Zero);

    private static readonly GuiColourMatrix Blue = new(0x1A / 255.0, 0, 0, 0, 1, 0, 0, 0, 255.0 / 0x1A);

    private static string Save(Window window, string name)
    {
        Dispatcher.UIThread.RunJobs();

        var path = Path.Combine(TestSurface.CaptureDirectory, name);

        using (var frame = window.CaptureRenderedFrame()!)
        {
            frame.Save(path, new PngBitmapEncoderOptions());
        }

        return path;
    }

    private static Color Ink(string key) =>
        ((ISolidColorBrush)Avalonia.Application.Current!.Resources[key]!).Color;

    [AvaloniaTheory]
    [InlineData(ThemeCatalog.Elite, 1280, 860)]
    [InlineData(ThemeCatalog.Elite, 924, 640)]
    [InlineData(ThemeCatalog.Elite, 512, 280)]
    [InlineData(ThemeCatalog.Dark, 1280, 860)]
    [InlineData(ThemeCatalog.Light, 1280, 860)]
    [InlineData(ThemeCatalog.ElitePaletteId, 1280, 860)]
    public void UtilitiesLearnedPhrasesAndHelpAreCaptured(string themeId, double width, double height)
    {
        using var look = AppLook.Put(themeId, themeId == ThemeCatalog.ElitePaletteId ? Blue : null);

        var saved = new List<string>
        {
            CaptureUtilities(themeId, width, height),
            CaptureLearnedPhrases(themeId, width, height),
            CaptureHelp(themeId, width, height),
        };

        Assert.All(saved, path => Assert.True(File.Exists(path)));
    }

    private static string CaptureUtilities(string themeId, double width, double height)
    {
        var root = TempFolders.Create("d47-utilities-kit");
        var alarms = new AlarmStore(Path.Combine(root, "alarms.json"), NullLogger<AlarmStore>.Instance);
        var timekeeper = new Timekeeper(alarms);

        timekeeper.StartTimer("mining run", TimeSpan.FromMinutes(40), Instant);
        timekeeper.SetAlarm("wake up", Instant.AddHours(9), Instant);

        var panel = new PanelView
        {
            DataContext = new PanelViewModel(),
            Mode = height < 400 ? PanelMode.Mini : PanelMode.Full,
        };

        panel.EnableUtilities(timekeeper, alarms, () => Instant, () => TimeZoneInfo.Utc);

        var window = new Window { Content = panel, Width = width, Height = height };
        window.Show();

        panel.Tab = PanelTab.Utilities;
        panel.TickClocks();
        Dispatcher.UIThread.RunJobs();

        var path = Save(window, $"utilities-{themeId}-{width}x{height}.png");
        window.Close();

        return path;
    }

    private static string CaptureLearnedPhrases(string themeId, double width, double height)
    {
        var root = TempFolders.Create("d47-learned-phrases-kit");

        var store = new D47.Core.Conversation.LearnedPhrasesStore(
            Path.Combine(root, "phrases.json"), NullLogger<D47.Core.Conversation.LearnedPhrasesStore>.Instance);

        store.Learn("F1", "set focus on elite", "set focus to elite", Instant);

        var gameState = new D47.Core.Journal.GameStateStore();

        Assert.True(D47.Core.Journal.JournalEvent.TryParse(
            """{"timestamp":"2026-08-25T09:00:00Z","event":"Commander","FID":"F1","Name":"Jameson"}""",
            NullLogger.Instance,
            out var commander));
        gameState.Apply(commander!);

        var registry = D47.Core.Capabilities.CapabilityRegistry.Build(
        [
            D47.Core.Capabilities.Builtin.LearnedPhrasesCapability.Create(
                store, () => gameState.Active?.Identity.FrontierId ?? string.Empty),
        ]);

        var panel = new PanelView { DataContext = new PanelViewModel() };

        panel.EnableSettings(
            () => new TextBlock { Text = "Settings" },
            learnedPhrases: () => new LearnedPhrasesPage(registry, store, () => gameState.Active));

        var window = new Window { Content = panel, Width = width, Height = height };
        window.Show();

        panel.Tab = PanelTab.Settings;
        Assert.True(panel.Nav.SelectRoot(LearnedPhrasesPage.RootKey));
        Dispatcher.UIThread.RunJobs();

        var path = Save(window, $"learned-phrases-{themeId}-{width}x{height}.png");
        window.Close();

        return path;
    }

    private static string CaptureHelp(string themeId, double width, double height)
    {
        var nav = new PanelNavigator();
        var page = HelpPageView.Build(D47.Core.Help.HelpLibrary.For("help")!, nav, _ => { });

        var window = new Window { Content = page, Width = width, Height = height };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        var path = Save(window, $"help-{themeId}-{width}x{height}.png");
        window.Close();

        return path;
    }

    /// <summary>
    /// Every dialog drawn on <see cref="Modal.Apply"/>, built with just enough content to show a row.
    /// Shared between the dialog capture and the header/Esc test below.
    /// </summary>
    private static IEnumerable<(string Name, Func<Window> Open)> Dialogs()
    {
        yield return ("AudioRecorder", OpenAudioRecorder);
        yield return ("Changelog", OpenChangelog);
        yield return ("Coverage", OpenCoverage);
        yield return ("Debrief", OpenDebrief);
        yield return ("HelpImprove", OpenHelpImprove);
        yield return ("Logbook", OpenLogbook);
        yield return ("Lore", OpenLore);
        yield return ("Macros", OpenMacros);
        yield return ("Memory", OpenMemory);
        yield return ("Persona", OpenPersona);
        yield return ("Switches", OpenSwitches);
    }

    private static Window OpenAudioRecorder()
    {
        var root = TempFolders.Create("d47-dialogs-recorder");
        var log = new D47.Core.Diagnostics.Recording.RecordingLog(root, NullLogger.Instance);

        log.Add(new D47.Core.Diagnostics.Recording.RecordingCapture(
            D47.Core.Diagnostics.Recording.RecordingDirection.Heard,
            Instant,
            D47.Core.Audio.WavWriter.ToBytes(new float[16_000], 16_000),
            TimeSpan.FromSeconds(1))
        {
            Text = "set course for Colonel",
        });

        return new AudioRecorderWindow(log, () => Instant);
    }

    private static Window OpenChangelog() => new ChangelogWindow("- Added a thing\n- Fixed a bug");

    private static CoverageReport CoverageReport()
    {
        var ledger = new CoverageLedger();
        var item = new CoverageItem(CoverageKind.Tool, "worked", "diagnostics", "Probe - worked", "aaaa");

        ledger.Record(item, Instant, CoverageOutcome.Ok);

        return ledger.Report([item]);
    }

    private static Window OpenCoverage() => new CoverageWindow(CoverageReport());

    private static Window OpenDebrief()
    {
        var root = TempFolders.Create("d47-dialogs-debrief");
        var data = Directory.CreateDirectory(Path.Combine(root, D47.Core.AppPaths.DataFolderName)).FullName;

        var store = new StandingDirectionsStore(
            Path.Combine(data, DebriefWriteFence.FileName), NullLogger<StandingDirectionsStore>.Instance);

        var book = new DebriefBook(store, () => "F1");

        return new DebriefWindow(book, () => Instant, () => PersonaCatalog.Resolve(null));
    }

    private static Window OpenHelpImprove() =>
        new HelpImproveWindow(Instant, TestSurface.Excerpt("an excerpt"));

    private static Window OpenLogbook()
    {
        var root = TempFolders.Create("d47-dialogs-logbook");

        var book = new LogbookBook(
            new LogFolder(root, NullLogger<LogFolder>.Instance),
            new LogDigestBuilder(NullLogger<LogDigestBuilder>.Instance),
            new LogWriter(NullLogger<LogWriter>.Instance),
            () => new LogbookSettings(),
            () => [],
            () => Instant,
            () => new LogbookContext(),
            NullLogger<LogbookBook>.Instance);

        return new LogbookWindow(book);
    }

    private static Window OpenLore()
    {
        var root = TempFolders.Create("d47-dialogs-lore");

        var store = new LoreStore(Path.Combine(root, "lore.json"), NullLogger<LoreStore>.Instance);
        var book = new LoreBook(store);

        book.Add(1L, "Colonia", "A long haul from the bubble.", LoreArrival.Panel, Instant, "F1");

        var editing = new LoreEditing(
            book,
            () => new D47.Core.Capabilities.Builtin.LoreCapability.LorePlace(1L, "Colonia", "F1"),
            () => false,
            (_, _) => Task.FromResult<string?>(null),
            () => Instant);

        return new LoreWindow(editing);
    }

    private static Window OpenMacros()
    {
        var root = TempFolders.Create("d47-dialogs-macros");

        var store = new MacroStore(Path.Combine(root, "macros.json"), NullLogger<MacroStore>.Instance);
        var action = GameActions.All.First(a => a.Group != GameActions.Weapons).Id;

        store.Save([new Macro { Name = "night flight", Steps = [new MacroStep(action, DesiredState.On, 250)] }]);
        store.Poll([]);

        return new MacroWindow(store);
    }

    private static Window OpenMemory()
    {
        var root = TempFolders.Create("d47-dialogs-memory");

        var store = new MemoryStore(Path.Combine(root, "memory.json"), NullLogger<MemoryStore>.Instance);
        var book = new MemoryBook(store, () => "F1", () => MemorySituation.Unknown);

        book.Remember("Prefers the scenic route.", MemoryArrival.Panel, Instant);

        return new MemoryWindow(book, () => Instant);
    }

    private static Window OpenPersona()
    {
        var root = TempFolders.Create("d47-dialogs-persona");

        var store = new OwnPersonaStore(Path.Combine(root, "personas.json"), NullLogger<OwnPersonaStore>.Instance);
        store.Save([new OwnPersona("own.rusty", "Rusty", "You are Rusty. Salvage crew, not a Guardian.")]);

        return new PersonaWindow(store);
    }

    private static Window OpenSwitches()
    {
        var root = TempFolders.Create("d47-dialogs-switches");

        var store = new SwitchStore(Path.Combine(root, "switches.json"), NullLogger<SwitchStore>.Instance);

        store.Save([new SwitchMapping
        {
            Name = "throttle detent",
            DeviceId = "stick-1",
            Device = "HOTAS",
            Positions = [new SwitchPosition(0, "LandingGearToggle", DesiredState.On, null)],
        }]);

        return new SwitchWindow(
            store,
            new FakeHotasReader(),
            new SwitchReconciler(NullLogger<SwitchReconciler>.Instance),
            () => Instant,
            Path.Combine(root, "switch-capture.txt"),
            []);
    }

    [AvaloniaTheory]
    [InlineData(ThemeCatalog.Elite)]
    [InlineData(ThemeCatalog.Dark)]
    [InlineData(ThemeCatalog.Light)]
    [InlineData(ThemeCatalog.ElitePaletteId)]
    public void EveryDialogIsCaptured(string themeId)
    {
        using var look = AppLook.Put(themeId, themeId == ThemeCatalog.ElitePaletteId ? Blue : null);

        var saved = new List<string>();

        foreach (var (name, open) in Dialogs())
        {
            var window = open();
            window.Show();
            Dispatcher.UIThread.RunJobs();

            saved.Add(Save(window, $"dialog-{name}-{themeId}.png"));

            window.Close();
            Dispatcher.UIThread.RunJobs();
        }

        Assert.All(saved, path => Assert.True(File.Exists(path)));
    }

    [AvaloniaFact]
    public void EveryDialogOpensWithTheModalHeader()
    {
        using var look = AppLook.Put(ThemeCatalog.Elite, null);

        foreach (var (name, open) in Dialogs())
        {
            var window = open();
            window.Show();
            Dispatcher.UIThread.RunJobs();

            var context = window.GetVisualDescendants().OfType<TextBlock>()
                .Single(block => block.Name == "ModalContext");

            var title = window.GetVisualDescendants().OfType<TextBlock>()
                .Single(block => block.Name == "ModalTitle");

            Assert.Equal(Ink(ThemeManager.AKey), (context.Foreground as ISolidColorBrush)?.Color);
            Assert.Equal(Ink(ThemeManager.WhiteKey), (title.Foreground as ISolidColorBrush)?.Color);

            var closed = false;
            window.Closed += (_, _) => closed = true;

            window.KeyPressQwerty(PhysicalKey.Escape, RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();

            Assert.True(closed, $"{name} did not close on Esc");
        }
    }

    [AvaloniaFact]
    public void TheUtilitiesTitleIsTheScreenTitleAndItsRowsAreListRows()
    {
        using var look = AppLook.Put(ThemeCatalog.Elite, null);

        var root = TempFolders.Create("d47-utilities-rows");
        var alarms = new AlarmStore(Path.Combine(root, "alarms.json"), NullLogger<AlarmStore>.Instance);
        var timekeeper = new Timekeeper(alarms);

        timekeeper.StartTimer("mining run", TimeSpan.FromMinutes(40), Instant);

        var panel = new PanelView { DataContext = new PanelViewModel() };
        panel.EnableUtilities(timekeeper, alarms, () => Instant, () => TimeZoneInfo.Utc);

        var window = new Window { Content = panel, Width = 1280, Height = 860 };
        window.Show();

        panel.Tab = PanelTab.Utilities;
        panel.TickClocks();
        Dispatcher.UIThread.RunJobs();

        var title = panel.GetVisualDescendants().OfType<TextBlock>()
            .Single(block => block.Text == "Utilities" && block.FontSize == TypeScale.Title);

        Assert.Equal(Ink(ThemeManager.WhiteKey), (title.Foreground as ISolidColorBrush)?.Color);

        var name = panel.GetVisualDescendants().OfType<TextBlock>()
            .Single(block => block.Text == "mining run");

        var row = name.GetVisualAncestors().OfType<Border>()
            .First(border => border.Classes.Contains(ListRow.Class));

        Assert.Equal(default, row.BorderThickness);

        window.Close();
    }

    [Theory]
    [InlineData("Panel/UtilitiesPage.cs")]
    [InlineData("Panel/LearnedPhrasesPage.cs")]
    [InlineData("Panel/HelpPageView.cs")]
    [InlineData("Controls/AudioRecorderWindow.cs")]
    [InlineData("Controls/ChangelogWindow.cs")]
    [InlineData("Controls/CoverageWindow.cs")]
    [InlineData("Controls/DebriefWindow.cs")]
    [InlineData("Controls/HelpImproveWindow.cs")]
    [InlineData("Controls/LogbookWindow.cs")]
    [InlineData("Controls/LoreWindow.cs")]
    [InlineData("Controls/MacroWindow.cs")]
    [InlineData("Controls/MemoryWindow.cs")]
    [InlineData("Controls/PersonaWindow.cs")]
    [InlineData("Controls/SwitchWindow.cs")]
    [InlineData("Controls/PickerPage.axaml")]
    [InlineData("Controls/PickerPage.axaml.cs")]
    public void TheSourceDrawsOnlyInTheNewTokens(string relative)
    {
        var source = File.ReadAllText(Path.Combine(Root(), "src", "D47.App", relative.Replace('/', Path.DirectorySeparatorChar)));

        Assert.DoesNotContain("CornerRadius", source, StringComparison.Ordinal);
        Assert.DoesNotContain("CardChrome", source, StringComparison.Ordinal);
        Assert.DoesNotMatch(@"#[0-9A-Fa-f]{6}\b", source);
        Assert.DoesNotMatch(
            @"ThemeManager\.(Background|Surface|SurfaceAlt|Border|Text|TextMuted|TextFaint|Accent|AccentMuted|Danger|Warn|Good|Info|Rule|FillLow|FillHigh|FillHigher|AccentBorder|AccentInk|CardFill|CardFillSelected|RowFill|TagBorder|PaneFill|PaneBorder|TagInk)Key\b",
            source);
        Assert.DoesNotMatch(
            @"D47\.(Background|Surface|SurfaceAlt|Border|Text|TextMuted|TextFaint|Accent|AccentMuted|Danger|Warn|Good|Info|Rule|FillLow|FillHigh|FillHigher|AccentBorder|AccentInk|CardFill|CardFillSelected|RowFill|TagBorder|PaneFill|PaneBorder|TagInk)\}",
            source);
    }

    private static string Root()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "d47.slnx")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName
               ?? throw new InvalidOperationException($"No d47.slnx above {AppContext.BaseDirectory}.");
    }
}
