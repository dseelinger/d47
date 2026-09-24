using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Avalonia.VisualTree;
using D47.App.Panel;
using D47.App.Theming;
using D47.Core.Checklists;
using D47.Core.Configuration;
using D47.Core.Engineers;
using D47.Core.Interface;
using D47.Core.Journal;
using D47.Core.Knowledge;
using D47.Core.Loadout;
using D47.Core.Ships;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.App.Tests;

/// <summary>
/// The Engineers directory, one engineer and the route, rendered on each theme and at the three panel sizes
/// and saved to <see cref="TestSurface.CaptureDirectory"/> for comparison with brief 03 (#401).
/// </summary>
public class TheEngineersPagesAreDrawnOnTheKitTests
{
    private static readonly GuiColourMatrix Blue = new(0x1A / 255.0, 0, 0, 0, 1, 0, 0, 0, 255.0 / 0x1A);

    private static readonly string[] Journal =
    [
        """{"timestamp":"2026-08-18T09:00:00Z","event":"Commander","FID":"F1","Name":"Jameson"}""",
        """{"timestamp":"2026-08-18T09:00:00Z","event":"Location","StarSystem":"Deciat","StarPos":[122.625,-0.8125,-47.28125],"Docked":false}""",
        """{"timestamp":"2026-08-18T09:00:00Z","event":"Loadout","Ship":"python","ShipID":12,"ShipName":"Bad Idea","ShipIdent":"BI-01","MaxJumpRange":30.0,"Modules":[]}""",
        """{"timestamp":"2026-08-18T09:00:00Z","event":"Rank","Combat":1,"Trade":0,"Explore":1,"Soldier":0,"Exobiologist":0,"Empire":0,"Federation":0,"CQC":0}""",
        """{"timestamp":"2026-08-18T09:00:00Z","event":"EngineerProgress","Engineers":[{"Engineer":"Liz Ryder","EngineerID":300080,"Progress":"Unlocked","Rank":5},{"Engineer":"Felicity Farseer","EngineerID":300100,"Progress":"Known"}]}""",
    ];

    private sealed record Surface(Window Window, PanelView Panel);

    private static Surface Open(double width, double height)
    {
        var root = TempFolders.Create("d47-engineers-capture");
        var store = new GameStateStore();

        foreach (var line in Journal)
        {
            Assert.True(JournalEvent.TryParse(line, NullLogger.Instance, out var parsed));
            store.Apply(parsed!);
        }

        var state = store.Active!;

        var checklists = new ChecklistService(
            new ChecklistStore(Path.Combine(root, "checklist.json"), NullLogger<ChecklistStore>.Instance),
            new ChecklistProposalStore(
                Path.Combine(root, "checklist-proposals.json"),
                NullLogger<ChecklistProposalStore>.Instance),
            () => state);

        var builds = new ShipBuildStore(Path.Combine(root, "ships.json"), NullLogger<ShipBuildStore>.Instance);

        builds.Save([
            new ShipBuild("F1", "ship-1", "python", 12, "Bad Idea",
                [
                    new SlotPlan("FrameShiftDrive", "Increased FSD Range", 5),
                    new SlotPlan("MainEngines", "Dirty Drive Tuning", 5),
                ]),
        ]);

        var kit = new OnFootBuildStore(Path.Combine(root, "on-foot.json"), NullLogger<OnFootBuildStore>.Instance);

        var ships = new ShipPlanService(builds, checklists, () => state);
        var onFoot = new OnFootPlanService(kit, checklists, () => state);
        var unlocks = new EngineerPlanService(builds, kit, checklists, () => state);
        var memory = new EngineerDirectoryMemory(
            new ViewStateStore(new D47.Core.AppPaths(root), NullLogger<ViewStateStore>.Instance));

        var panel = new PanelView
        {
            DataContext = new PanelViewModel(),
            Mode = height < 400 ? PanelMode.Mini : PanelMode.Full,
        };

        panel.EnableCopy(new D47.Core.Capabilities.Builtin.RecordingClipboard());
        panel.EnableEngineers(unlocks, ships, () => state, onFoot, memory, checklists);

        var window = new Window { Content = panel, Width = width, Height = height };
        window.Show();

        panel.Tab = PanelTab.Engineers;
        Dispatcher.UIThread.RunJobs();

        return new Surface(window, panel);
    }

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

    private static Engineer Named(string name) => EngineerDirectory.All.First(engineer => engineer.Name == name);

    [AvaloniaTheory]
    [InlineData(ThemeCatalog.Elite, 1280, 860)]
    [InlineData(ThemeCatalog.Elite, 924, 640)]
    [InlineData(ThemeCatalog.Elite, 512, 280)]
    [InlineData(ThemeCatalog.Dark, 1280, 860)]
    [InlineData(ThemeCatalog.Light, 1280, 860)]
    [InlineData(ThemeCatalog.ElitePaletteId, 1280, 860)]
    public void TheEngineersPagesAreCaptured(string themeId, double width, double height)
    {
        using var look = AppLook.Put(themeId, themeId == ThemeCatalog.ElitePaletteId ? Blue : null);

        var surface = Open(width, height);
        var panel = surface.Panel;

        var directory = Save(surface.Window, $"engineers-directory-{themeId}-{width}x{height}.png");

        panel.Nav.Drill(EngineersPages.Crumb(Named("Felicity Farseer")));
        var felicity = Save(surface.Window, $"engineers-felicity-{themeId}-{width}x{height}.png");

        panel.Nav.SelectRoot(EngineersPages.DirectoryRoot);
        panel.Nav.Drill(EngineersPages.Crumb(Named("Marco Qwent")));
        var marco = Save(surface.Window, $"engineers-marco-{themeId}-{width}x{height}.png");

        Assert.True(panel.Nav.SelectRoot(EngineersPages.RouteRoot));
        var route = Save(surface.Window, $"engineers-route-{themeId}-{width}x{height}.png");

        surface.Window.Close();
        Dispatcher.UIThread.RunJobs();

        Assert.True(File.Exists(directory));
        Assert.True(File.Exists(felicity));
        Assert.True(File.Exists(marco));
        Assert.True(File.Exists(route));
    }

    /// <summary>An engineer in the Commander's own system reads Cyan; one elsewhere reads A.</summary>
    [AvaloniaFact]
    public void AnEngineerInYourSystemIsCyan()
    {
        using var look = AppLook.Put(ThemeCatalog.Elite, null);

        var surface = Open(1280, 860);

        surface.Panel.Nav.Drill(EngineersPages.Crumb(Named("Felicity Farseer")));
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(Ink(ThemeManager.CyanKey), Figure(surface.Panel, "Farseer Inc in Deciat"));

        surface.Panel.Nav.SelectRoot(EngineersPages.DirectoryRoot);
        surface.Panel.Nav.Drill(EngineersPages.Crumb(Named("Liz Ryder")));
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(Ink(ThemeManager.AKey), Figure(surface.Panel, Named("Liz Ryder").Where));

        surface.Window.Close();
    }

    /// <summary>Met is Blue, unknown White, in progress A and not met Grey.</summary>
    [Fact]
    public void ThePrerequisitesTakeTheStatusLadder()
    {
        Assert.Equal(("✓ MET", ThemeManager.BlueKey), EngineersPages.Ladder(new UnlockCriterion("a", true)));
        Assert.Equal(("? UNKNOWN", ThemeManager.WhiteKey), EngineersPages.Ladder(new UnlockCriterion("a", null)));
        Assert.Equal(("NOT MET", ThemeManager.GreyKey), EngineersPages.Ladder(new UnlockCriterion("a", false)));
        Assert.Equal(
            ("IN PROGRESS", ThemeManager.AKey),
            EngineersPages.Ladder(new UnlockCriterion("a", false) { Measure = new UnlockMeasure(1, 2, false) }));
    }

    private static Color? Figure(Control page, string text) =>
        (page.GetVisualDescendants().OfType<EngineerPage>().Single()
            .GetVisualDescendants().OfType<TextBlock>().First(block => block.Text == text).Foreground
            as ISolidColorBrush)?.Color;

    private static Color Ink(string key) =>
        ((ISolidColorBrush)Avalonia.Application.Current!.Resources[key]!).Color;

    [Theory]
    [InlineData("EngineersPages.cs")]
    [InlineData("EngineerLines.cs")]
    public void TheEngineersSourceDrawsOnlyInTheNewTokens(string file)
    {
        var source = File.ReadAllText(Path.Combine(Root(), "src", "D47.App", "Panel", file));

        Assert.DoesNotContain("CornerRadius", source, StringComparison.Ordinal);
        Assert.DoesNotContain("CardChrome", source, StringComparison.Ordinal);
        Assert.DoesNotContain("BorderThickness", source, StringComparison.Ordinal);
        Assert.DoesNotMatch(@"#[0-9A-Fa-f]{6}\b", source);
        Assert.DoesNotMatch(
            @"ThemeManager\.(Background|Surface|SurfaceAlt|Border|Text|TextMuted|TextFaint|Accent|AccentMuted|Danger|Warn|Good|Info|Rule|FillLow|FillHigh|FillHigher|AccentBorder|AccentInk|CardFill|CardFillSelected|RowFill|TagBorder|PaneFill|PaneBorder|TagInk)Key\b",
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
