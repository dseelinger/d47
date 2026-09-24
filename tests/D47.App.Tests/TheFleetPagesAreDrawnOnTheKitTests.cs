using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Avalonia.VisualTree;
using D47.App.Panel;
using D47.App.Theming;
using D47.Core.Checklists;
using D47.Core.Interface;
using D47.Core.Journal;
using D47.Core.Loadout;
using D47.Core.Ships;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.App.Tests;

/// <summary>
/// The Fleet ship page, a slot of it and the Materials page, rendered on each theme and at the three panel
/// sizes and saved to <see cref="TestSurface.CaptureDirectory"/> for comparison with brief 03 (#399).
/// </summary>
public class TheFleetPagesAreDrawnOnTheKitTests
{
    private static readonly GuiColourMatrix Blue = new(0x1A / 255.0, 0, 0, 0, 1, 0, 0, 0, 255.0 / 0x1A);

    private static readonly string[] Journal =
    [
        """{"timestamp":"2026-08-18T09:00:00Z","event":"Commander","FID":"F1","Name":"Jameson"}""",
        """{"timestamp":"2026-08-18T09:00:00Z","event":"StoredShips","StarSystem":"Shinrarta Dezhra","StationName":"Jameson Memorial","ShipsHere":[{"ShipID":7,"ShipType":"anaconda","ShipType_Localised":"Anaconda","Name":"Big Slow","Value":150000000}],"ShipsRemote":[{"ShipID":9,"ShipType":"sidewinder","ShipType_Localised":"Sidewinder","Name":"Runabout","StarSystem":"Deciat","ShipMarketID":1,"TransferPrice":12000,"TransferTime":300,"Value":32000}]}""",
        """{"timestamp":"2026-08-18T09:00:00Z","event":"Location","StarSystem":"Shinrarta Dezhra","SystemAddress":1,"StarPos":[0,0,0],"Docked":true,"StationName":"Jameson Memorial"}""",
        """{"timestamp":"2026-08-18T09:00:00Z","event":"Materials","Raw":[{"Name":"iron","Count":20}],"Manufactured":[],"Encoded":[]}""",
        """{"timestamp":"2026-08-18T09:00:00Z","event":"Loadout","Ship":"python","ShipID":12,"ShipName":"Bad Idea","ShipIdent":"BI-01","MaxJumpRange":34.25,"CargoCapacity":128,"UnladenMass":350.5,"HullValue":55000000,"ModulesValue":45000000,"Rebuy":5000000,"HullHealth":0.87,"Modules":[{"Slot":"MainEngines","Item":"int_engine_size5_class5","On":true,"Priority":0,"Health":1.0},{"Slot":"LifeSupport","Item":"int_lifesupport_size4_class2","On":true,"Priority":0,"Health":1.0},{"Slot":"Radar","Item":"int_sensors_size6_class2","On":true,"Priority":0,"Health":1.0},{"Slot":"LargeHardpoint1","Item":"hpt_pulselaser_gimbal_large","On":true,"Priority":0,"Health":1.0,"Engineering":{"BlueprintName":"Weapon_LongRange","Level":5,"Quality":0.9,"Engineer":"Felicity Farseer"}}]}""",
    ];

    private static (Window Window, PanelView Panel) Open(double width, double height)
    {
        var root = TempFolders.Create("d47-fleet-capture");

        var checklists = new ChecklistService(
            new ChecklistStore(Path.Combine(root, "checklist.json"), NullLogger<ChecklistStore>.Instance),
            new ChecklistProposalStore(
                Path.Combine(root, "checklist-proposals.json"),
                NullLogger<ChecklistProposalStore>.Instance),
            () => null);

        var store = new GameStateStore();

        foreach (var line in Journal)
        {
            Assert.True(JournalEvent.TryParse(line, NullLogger.Instance, out var parsed));
            store.Apply(parsed!);
        }

        var ships = new ShipPlanService(
            new ShipBuildStore(Path.Combine(root, "ships.json"), NullLogger<ShipBuildStore>.Instance),
            checklists,
            () => store.Active);

        var build = ships.BuildFor(12, "python", "Bad Idea");
        ships.Plan(build.Id, new SlotPlan("MainEngines", "Dirty Drive Tuning", 5));

        var panel = new PanelView
        {
            DataContext = new PanelViewModel(),
            Mode = height < 400 ? PanelMode.Mini : PanelMode.Full,
        };

        panel.EnableCopy(new D47.Core.Capabilities.Builtin.RecordingClipboard());
        var kit = new OnFootPlanService(
            new OnFootBuildStore(Path.Combine(root, "on-foot.json"), NullLogger<OnFootBuildStore>.Instance),
            checklists,
            () => store.Active);

        panel.EnableLoadout(ships, checklists, () => store.Active, kit);

        var window = new Window { Content = panel, Width = width, Height = height };
        window.Show();

        panel.Tab = PanelTab.Loadout;
        Dispatcher.UIThread.RunJobs();

        return (window, panel);
    }

    private static Button Row(PanelView panel, string label) =>
        panel.GetVisualDescendants().OfType<Button>()
            .First(button => AutomationProperties.GetName(button) == label);

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

    [AvaloniaTheory]
    [InlineData(ThemeCatalog.Elite, 1280, 860)]
    [InlineData(ThemeCatalog.Elite, 924, 640)]
    [InlineData(ThemeCatalog.Elite, 512, 280)]
    [InlineData(ThemeCatalog.Dark, 1280, 860)]
    [InlineData(ThemeCatalog.Light, 1280, 860)]
    [InlineData(ThemeCatalog.ElitePaletteId, 1280, 860)]
    public void TheFleetPagesAreCaptured(string themeId, double width, double height)
    {
        using var look = AppLook.Put(themeId, themeId == ThemeCatalog.ElitePaletteId ? Blue : null);

        var (window, panel) = Open(width, height);

        Row(panel, "Bad Idea (Python)").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();

        var ship = Save(window, $"fleet-ship-{themeId}-{width}x{height}.png");

        // The card of the ship being shown is filled solid, not outlined; mini draws only the ship page.
        if (panel.Mode == PanelMode.Full)
        {
            Assert.Contains(
                panel.GetVisualDescendants().OfType<Button>(),
                button => button.Classes.Contains(ListRow.SelectedClass) && button.BorderThickness == default);
        }

        // The first slot row, which on mini is the only one left: the slot with a plan.
        panel.GetVisualDescendants().OfType<Button>()
            .First(button => button.Content is Grid { ColumnDefinitions.Count: 5 })
            .RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();

        var slot = Save(window, $"fleet-slot-{themeId}-{width}x{height}.png");

        Assert.True(panel.Nav.SelectRoot(LoadoutPages.GapRoot));
        Dispatcher.UIThread.RunJobs();

        var materials = Save(window, $"fleet-materials-{themeId}-{width}x{height}.png");

        window.Close();
        Dispatcher.UIThread.RunJobs();

        Assert.True(File.Exists(ship));
        Assert.True(File.Exists(slot));
        Assert.True(File.Exists(materials));
    }

    [Theory]
    [InlineData("ShipsMode.cs")]
    [InlineData("LoadoutPages.cs")]
    [InlineData("LoadoutMode.cs")]
    [InlineData("HullPicture.cs")]
    [InlineData("ShipArt.cs")]
    public void TheFleetSourceDrawsOnlyInTheNewTokens(string file)
    {
        var source = File.ReadAllText(Path.Combine(Root(), "src", "D47.App", "Panel", file));

        Assert.DoesNotContain("CornerRadius", source, StringComparison.Ordinal);
        Assert.DoesNotContain("CardChrome", source, StringComparison.Ordinal);
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
