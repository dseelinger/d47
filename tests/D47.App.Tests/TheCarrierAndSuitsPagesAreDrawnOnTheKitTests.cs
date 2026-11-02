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
using D47.Core.Storage;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.App.Tests;

/// <summary>
/// The Carrier page and the Suits pages, rendered on each theme and at the three panel sizes and saved to
/// <see cref="TestSurface.CaptureDirectory"/> for comparison with brief 03 (#400).
/// </summary>
[Trait("Category", "Integration")]
public class TheCarrierAndSuitsPagesAreDrawnOnTheKitTests
{
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

        var checklists = TestSurface.Checklists(root);

        var state = JournalScript.State(Journal);

        var ships = TestSurface.ShipPlans(checklists, root, () => state);

        var kit = new OnFootPlanService(
            new OnFootBuildStore(Path.Combine(root, "on-foot.json"), new MemoryFileSystem(), NullLogger<OnFootBuildStore>.Instance),
            checklists,
            () => state);

        var panel = new PanelView
        {
            DataContext = new PanelViewModel(),
            Mode = height < 400 ? PanelMode.Mini : PanelMode.Full,
        };

        panel.EnableCopy(new D47.Core.Capabilities.Builtin.RecordingClipboard());
        panel.EnableLoadout(ships, checklists, () => state, kit);

        var window = new Window { Content = panel, Width = width, Height = height };
        window.Show();

        panel.Tab = PanelTab.Assets;
        Dispatcher.UIThread.RunJobs();

        return new Surface(window, panel, kit);
    }

    [Trait("Category", "Integration")]
    [AvaloniaTheory]
    [MemberData(nameof(CaptureMatrix.Rows), MemberType = typeof(CaptureMatrix))]
    public void TheCarrierAndSuitsPagesAreCaptured(string themeId, double width, double height)
    {
        using var look = AppLook.PutForCapture(themeId);

        var surface = Open(width, height);
        var panel = surface.Panel;

        Assert.True(panel.Nav.SelectRoot(LoadoutPages.CarrierRoot));
        var carrier = surface.Window.SaveFrame($"carrier-{themeId}-{width}x{height}.png");

        var build = surface.Kit.BuildFor(OnFootKind.Suit, 7, "Maverick Suit");
        surface.Kit.Plan(build.Id, new KitPlan(OnFootBuild.GradeSlot, 5));

        Assert.True(panel.Nav.SelectRoot(OnFootMode.Root));
        var suits = surface.Window.SaveFrame($"suits-{themeId}-{width}x{height}.png");

        panel.Nav.GoTo(
            new NavCrumb(OnFootMode.KitPrefix + build.Id, "Maverick Suit"),
            new NavCrumb($"{OnFootMode.KitSlotPrefix}{build.Id}|{OnFootBuild.GradeSlot}", "Grade"));
        var grade = surface.Window.SaveFrame($"suits-grade-{themeId}-{width}x{height}.png");

        surface.Window.Close();
        Dispatcher.UIThread.RunJobs();
    }

    /// <summary>The carrier in the Commander's own system reads Cyan; the squadron's, elsewhere, reads A.</summary>
    [Trait("Category", "Integration")]
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
    [Trait("Category", "Gate")]
    public void TheCarrierAndSuitsSourceDrawsOnlyInTheNewTokens(string file)
    {
        var source = File.ReadAllText(Path.Combine(AppSource.RepositoryRoot(), "src", "D47.App", "Panel", file));

        KitTokens.AssertDrawsOnlyInTheNewTokens(source, new KitTokenRules(true, false, true));
    }
}
