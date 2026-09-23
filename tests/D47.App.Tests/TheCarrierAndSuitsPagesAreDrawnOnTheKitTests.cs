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
using D47.Core.Interface;
using D47.Core.Journal;
using D47.Core.Knowledge;
using D47.Core.Loadout;
using D47.Core.Ships;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.App.Tests;

/// <summary>
/// The Carrier page and the Suits pages, rendered on each theme and at the three panel sizes and saved to
/// <see cref="TestSurface.CaptureDirectory"/> for comparison with brief 03 (#400).
/// </summary>
public class TheCarrierAndSuitsPagesAreDrawnOnTheKitTests
{
    private static readonly GuiColourMatrix Blue = new(0x1A / 255.0, 0, 0, 0, 1, 0, 0, 0, 255.0 / 0x1A);

    private static readonly string[] Journal =
    [
        """{"timestamp":"2026-08-18T09:00:00Z","event":"Commander","FID":"F1","Name":"Jameson"}""",
        """{"timestamp":"2026-08-18T09:00:00Z","event":"Location","StarSystem":"Deciat","SystemAddress":1,"StarPos":[0,0,0],"Docked":false}""",
        """{"timestamp":"2026-08-18T09:00:00Z","event":"CarrierStats","CarrierID":3715429376,"CarrierType":"FleetCarrier","Callsign":"BNH-T2F","Name":"Sacred Fire","DockingAccess":"all","AllowNotorious":false,"FuelLevel":792,"JumpRangeCurr":500.0,"PendingDecommission":false,"SpaceUsage":{"TotalCapacity":25000,"Cargo":540,"FreeSpace":23530},"Finance":{"CarrierBalance":750352669},"Crew":[{"CrewRole":"Refuel","Activated":true,"Enabled":true},{"CrewRole":"Repair","Activated":true,"Enabled":false}]}""",
        """{"timestamp":"2026-08-18T09:00:00Z","event":"CarrierLocation","CarrierType":"FleetCarrier","CarrierID":3715429376,"StarSystem":"Deciat"}""",
        """{"timestamp":"2026-08-18T09:00:00Z","event":"CarrierStats","CarrierID":3713474048,"CarrierType":"SquadronCarrier","Callsign":"QRS-11X","Name":"Wandering Home","DockingAccess":"squadronfriends","FuelLevel":140}""",
        """{"timestamp":"2026-08-18T09:00:00Z","event":"CarrierLocation","CarrierType":"SquadronCarrier","CarrierID":3713474048,"StarSystem":"Kuwemaki"}""",
        """{"timestamp":"2026-08-18T09:00:00Z","event":"SuitLoadout","SuitID":7,"SuitName":"utilitysuit_class3","LoadoutName":"Ground","SuitMods":[],"Modules":[{"SlotName":"PrimaryWeapon1","SuitModuleID":9,"ModuleName":"wpn_m_assaultrifle_kinetic_fauto","Class":2,"WeaponMods":[]}]}""",
        """{"timestamp":"2026-08-18T09:00:00Z","event":"Materials","Raw":[{"Name":"iron","Count":20}],"Manufactured":[],"Encoded":[]}""",
    ];

    private sealed record Surface(Window Window, PanelView Panel, OnFootPlanService Kit);

    private static Surface Open(double width, double height)
    {
        var root = TempFolders.Create("d47-carrier-suits-capture");

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

        var kit = new OnFootPlanService(
            new OnFootBuildStore(Path.Combine(root, "on-foot.json"), NullLogger<OnFootBuildStore>.Instance),
            checklists,
            () => store.Active);

        var panel = new PanelView
        {
            DataContext = new PanelViewModel(),
            Mode = height < 400 ? PanelMode.Mini : PanelMode.Full,
        };

        panel.EnableCopy(new D47.Core.Capabilities.Builtin.RecordingClipboard());
        panel.EnableLoadout(ships, checklists, () => store.Active, kit);

        var window = new Window { Content = panel, Width = width, Height = height };
        window.Show();

        panel.Tab = PanelTab.Loadout;
        Dispatcher.UIThread.RunJobs();

        return new Surface(window, panel, kit);
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

    [AvaloniaTheory]
    [InlineData(ThemeCatalog.Elite, 1280, 860)]
    [InlineData(ThemeCatalog.Elite, 924, 640)]
    [InlineData(ThemeCatalog.Elite, 512, 280)]
    [InlineData(ThemeCatalog.Dark, 1280, 860)]
    [InlineData(ThemeCatalog.Light, 1280, 860)]
    [InlineData(ThemeCatalog.ElitePaletteId, 1280, 860)]
    public void TheCarrierAndSuitsPagesAreCaptured(string themeId, double width, double height)
    {
        using var look = AppLook.Put(themeId, themeId == ThemeCatalog.ElitePaletteId ? Blue : null);

        var surface = Open(width, height);
        var panel = surface.Panel;

        Assert.True(panel.Nav.SelectRoot(LoadoutPages.CarrierRoot));
        var carrier = Save(surface.Window, $"carrier-{themeId}-{width}x{height}.png");

        var build = surface.Kit.BuildFor(OnFootKind.Suit, 7, "Maverick Suit");
        surface.Kit.Plan(build.Id, new KitPlan(OnFootBuild.GradeSlot, 5));

        Assert.True(panel.Nav.SelectRoot(OnFootMode.Root));
        var suits = Save(surface.Window, $"suits-{themeId}-{width}x{height}.png");

        panel.Nav.GoTo(
            new NavCrumb(OnFootMode.KitPrefix + build.Id, "Maverick Suit"),
            new NavCrumb($"{OnFootMode.KitSlotPrefix}{build.Id}|{OnFootBuild.GradeSlot}", "Grade"));
        var grade = Save(surface.Window, $"suits-grade-{themeId}-{width}x{height}.png");

        surface.Window.Close();
        Dispatcher.UIThread.RunJobs();

        Assert.True(File.Exists(carrier));
        Assert.True(File.Exists(suits));
        Assert.True(File.Exists(grade));
    }

    /// <summary>The carrier in the Commander's own system reads Cyan; the squadron's, elsewhere, reads A.</summary>
    [AvaloniaFact]
    public void TheCarrierInYourSystemIsCyan()
    {
        using var look = AppLook.Put(ThemeCatalog.Elite, null);

        var surface = Open(1280, 860);

        Assert.True(surface.Panel.Nav.SelectRoot(LoadoutPages.CarrierRoot));
        Dispatcher.UIThread.RunJobs();

        var page = surface.Panel.GetVisualDescendants().OfType<CarrierPage>().Single();

        Assert.Equal(Ink(ThemeManager.CyanKey), Figure(page, "Deciat"));
        Assert.Equal(Ink(ThemeManager.AKey), Figure(page, "Kuwemaki"));

        surface.Window.Close();
    }

    private static Color? Figure(Control page, string text) =>
        (page.GetVisualDescendants().OfType<TextBlock>().First(block => block.Text == text).Foreground
            as ISolidColorBrush)?.Color;

    private static Color Ink(string key) =>
        ((ISolidColorBrush)Avalonia.Application.Current!.Resources[key]!).Color;

    [Theory]
    [InlineData("CarrierPage.cs")]
    [InlineData("OnFootMode.cs")]
    public void TheCarrierAndSuitsSourceDrawsOnlyInTheNewTokens(string file)
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
