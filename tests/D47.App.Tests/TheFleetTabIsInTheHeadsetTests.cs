using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using D47.App.Headset;
using D47.App.Panel;
using D47.App.Theming;
using D47.Core.Checklists;
using D47.Core.Interface;
using D47.Core.Journal;
using D47.Core.Loadout;
using D47.Core.Ships;
using D47.Vr;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.App.Tests;

/// <summary>
/// The Fleet tab, in the headset (#53): every root, drawn and pressed on the surface the headset is
/// actually handed, and the one control the drill has that stays a mouse convenience.
/// </summary>
public class TheFleetTabIsInTheHeadsetTests
{
    /// <summary>The repo's own hull art, so the drawings switch has something to switch off.</summary>
    private static string Assets
    {
        get
        {
            var at = new DirectoryInfo(AppContext.BaseDirectory);

            while (at is not null && !Directory.Exists(Path.Combine(at.FullName, "assets", "ships")))
            {
                at = at.Parent;
            }

            return at is null
                ? throw new DirectoryNotFoundException("assets/ships not found above the test binary")
                : Path.Combine(at.FullName, "assets", "ships");
        }
    }

    private static ChecklistService Checklists(string folder, Func<CommanderGameState?> state) =>
        new(
            new ChecklistStore(Path.Combine(folder, "checklist.json"), NullLogger<ChecklistStore>.Instance),
            new ChecklistProposalStore(
                Path.Combine(folder, "checklist-proposals.json"), NullLogger<ChecklistProposalStore>.Instance),
            state);

    /// <summary>A Commander in one ship, wearing one suit, so every root has something to draw.</summary>
    private static CommanderGameState Flying()
    {
        var store = new GameStateStore();

        foreach (var line in new[]
                 {
                     """{"timestamp":"2026-09-09T09:00:00Z","event":"Commander","FID":"F1","Name":"Jameson"}""",
                     """{"timestamp":"2026-09-09T09:00:00Z","event":"Loadout","Ship":"python","ShipID":12,"ShipName":"Bad Idea","ShipIdent":"BI-01","MaxJumpRange":34.25,"CargoCapacity":128,"UnladenMass":350.5,"HullValue":55000000,"ModulesValue":45000000,"Rebuy":5000000,"HullHealth":0.87,"Modules":[{"Slot":"MainEngines","Item":"int_engine_size5_class5","On":true,"Priority":0,"Health":1.0},{"Slot":"LifeSupport","Item":"int_lifesupport_size4_class2","On":true,"Priority":0,"Health":1.0}]}""",
                     """{"timestamp":"2026-09-09T09:00:00Z","event":"SuitLoadout","SuitID":7,"SuitName":"utilitysuit_class3","LoadoutName":"Ground","SuitMods":[],"Modules":[{"SlotName":"PrimaryWeapon1","SuitModuleID":9,"ModuleName":"wpn_m_assaultrifle_kinetic_fauto","Class":2,"WeaponMods":[]}]}""",
                 })
        {
            Assert.True(JournalEvent.TryParse(line, NullLogger.Instance, out var parsed));
            store.Apply(parsed!);
        }

        return store.Active!;
    }

    /// <summary>The headset's own copy of the panel, on the Fleet tab, every mode wired.</summary>
    private static (VrPanelSurface Panel, VrPixels Pixels) Headset()
    {
        var (settings, viewState, paths) = TestSurface.Create();

        // Full, said out loud: mini carries no buttons and there would be nothing to press.
        settings.Apply(
            D47.Core.Capabilities.Builtin.VrCapability.ModeKey,
            "full",
            D47.Core.Configuration.SettingsCaller.Panel);

        new ThemeManager(Application.Current!, NullLogger<ThemeManager>.Instance).Apply(ThemeCatalog.Elite);

        var flying = Flying();
        Func<CommanderGameState?> state = () => flying;

        var checklists = Checklists(paths.Data, state);

        var ships = new ShipPlanService(
            new ShipBuildStore(Path.Combine(paths.Data, "ships.json"), NullLogger<ShipBuildStore>.Instance),
            checklists,
            state);

        var onFoot = new OnFootPlanService(
            new OnFootBuildStore(Path.Combine(paths.Data, "on-foot.json"), NullLogger<OnFootBuildStore>.Instance),
            checklists,
            state);

        var panel = new VrPanelSurface(
            new PanelViewModel(),
            settings,
            _ => null,
            checklists: checklists,
            ships: ships,
            gameState: state,
            onFoot: onFoot,
            modulePower: () => ModulePower.None,
            drawings: new ShipsDrawingsMemory(viewState));

        Dispatcher.UIThread.RunJobs();

        var (width, height) = panel.Size;
        var pixels = new VrPixels(width, height);

        return (panel, pixels);
    }

    /// <summary>Selects a root and rasterises it, the way the runtime asks for a frame.</summary>
    private static void Draw(VrPanelSurface panel, VrPixels pixels, string root)
    {
        panel.Nav.Select(PanelTab.Loadout);
        panel.Nav.SelectRoot(PanelTab.Loadout, root);
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(PanelTab.Loadout, panel.Nav.Tab);
        Assert.Equal(root, panel.Nav.RootKeyOf(PanelTab.Loadout));

        panel.Invalidate();
        panel.Draw(pixels.Address, pixels.RowBytes);
        Dispatcher.UIThread.RunJobs();
    }

    /// <summary>How many colours the submitted buffer has, up to the point the answer is settled.</summary>
    private static int Colours(VrPixels pixels, (int Width, int Height) size)
    {
        var distinct = new HashSet<uint>();

        unsafe
        {
            var read = (uint*)pixels.Address;

            for (var index = 0; index < size.Width * size.Height; index++)
            {
                distinct.Add(read[index]);

                if (distinct.Count > 8)
                {
                    break;
                }
            }
        }

        return distinct.Count;
    }

    private static Control? Pressable(VrPanelSurface panel, Func<Control, bool> wanted) =>
        panel.Board.View.GetVisualDescendants().OfType<Control>().FirstOrDefault(wanted);

    /// <summary>Presses a control where it is drawn on the quad, in the 0..1 a ray answers in.</summary>
    private static bool Press(VrPanelSurface panel, VrPixels pixels, Control control)
    {
        // A row past the fold has real bounds but sits outside the scroller's own viewport - a ray never
        // reaches it either, until the strip is scrolled the same way a spoken "page down" already does.
        // BringIntoView asks a real render loop to settle before it moves the offset, which headless
        // drawing here never does, so the offset is set directly instead. The scroll only takes effect
        // in the coordinates a ray answers in once a render pass has run over the new offset.
        if (control.GetVisualAncestors().OfType<ScrollViewer>().FirstOrDefault() is { Content: Visual content } scroller
            && control.TranslatePoint(new Point(0, 0), content) is { } withinContent)
        {
            var maxY = Math.Max(0, scroller.Extent.Height - scroller.Viewport.Height);
            scroller.Offset = scroller.Offset.WithY(Math.Clamp(withinContent.Y - 20, 0, maxY));

            Dispatcher.UIThread.RunJobs();
            panel.Invalidate();
            panel.Draw(pixels.Address, pixels.RowBytes);
            Dispatcher.UIThread.RunJobs();
        }

        var (width, height) = panel.Size;

        var centre = control.TranslatePoint(
            new Point(control.Bounds.Width / 2, control.Bounds.Height / 2), panel.Board.View);

        Assert.NotNull(centre);

        return panel.Press((float)(centre!.Value.X / width), (float)(centre.Value.Y / height));
    }

    private static List<PanelTab> Tabs(PanelNavigator nav) =>
        [.. Enum.GetValues<PanelTab>().Where(nav.Has)];

    /// <summary>
    /// The claim itself: the headset carries the tab, and it carries it where the window does — right
    /// after the transcript rather than at the end of the strip.
    /// </summary>
    [AvaloniaFact]
    public void FleetFollowsTranscriptOnTheHeadsetAsItDoesInTheWindow()
    {
        var (panel, _) = Headset();

        var (_, _, paths) = TestSurface.Create();
        var flying = Flying();
        Func<CommanderGameState?> state = () => flying;
        var checklists = Checklists(paths.Data, state);
        var ships = new ShipPlanService(
            new ShipBuildStore(Path.Combine(paths.Data, "ships.json"), NullLogger<ShipBuildStore>.Instance),
            checklists,
            state);

        var window = new PanelView { DataContext = new PanelViewModel() };

        // The headset's own copy of the panel furnishes Checklist too, so the window built to compare
        // against it furnishes the same tabs — otherwise the two would differ on a tab this test has no
        // claim about.
        window.EnableChecklist(checklists);
        window.EnableLoadout(ships, checklists, state);
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(Tabs(window.Nav), Tabs(panel.Nav));
        Assert.Contains(PanelTab.Loadout, Tabs(panel.Nav));

        var order = Tabs(panel.Nav);

        Assert.Equal(order.IndexOf(PanelTab.Transcript) + 1, order.IndexOf(PanelTab.Loadout));
    }

    /// <summary>Every root the tab has, drawn — the headset's own copy, through the real rasterise.</summary>
    [AvaloniaTheory]
    [InlineData(LoadoutPages.FleetRoot)]
    [InlineData(OnFootMode.Root)]
    [InlineData(LoadoutPages.GapRoot)]
    [InlineData(LoadoutPages.CarrierRoot)]
    public void EveryRootRastersInTheHeadset(string root)
    {
        var (panel, pixels) = Headset();

        Draw(panel, pixels, root);

        Assert.True(
            Colours(pixels, panel.Size) > 8,
            $"The submitted buffer for {root} has one or two colours, which is a blank quad rather "
            + "than a rendered page.");
    }

    /// <summary>A ship's own card, pressed by a ray, drills into it the way a tap on the window's card does.</summary>
    [AvaloniaFact]
    public void AShipCardTakesARayPress()
    {
        var (panel, pixels) = Headset();

        Draw(panel, pixels, LoadoutPages.FleetRoot);

        var card = Pressable(
            panel,
            control => control is Button
                && control.GetVisualDescendants().OfType<TextBlock>()
                    .Any(text => (text.Text ?? string.Empty).Contains("Bad Idea", StringComparison.Ordinal)));

        Assert.NotNull(card);
        Assert.True(Press(panel, pixels, card!));
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(2, panel.Nav.Trail.Count);
    }

    /// <summary>And the mode switch beside the cards: a <see cref="ToggleSwitch"/>, the ray presses it too.</summary>
    [AvaloniaFact]
    public void TheDrawingsSwitchTakesARayPress()
    {
        // The switch withdraws itself when the fleet has no captured hull to draw (LoadoutPages.IndexPage.
        // Refresh) - "python" needs its own art on disk to make the switch worth pressing.
        ShipArt.Shipped = null;
        ShipArt.Folder = Assets;

        try
        {
            var (panel, pixels) = Headset();

            Draw(panel, pixels, LoadoutPages.FleetRoot);

            // Not just "any ToggleSwitch": PanelView carries its own raw-JSON switch, hidden but still in
            // the visual tree, and FirstOrDefault would happily land on that one instead.
            var toggle = Pressable(panel, control => control is ToggleSwitch { Content: "Drawings" });

            Assert.NotNull(toggle);

            var before = ((ToggleSwitch)toggle!).IsChecked;

            Assert.True(Press(panel, pixels, toggle!));
            Dispatcher.UIThread.RunJobs();

            Assert.NotEqual(before, ((ToggleSwitch)toggle!).IsChecked);
        }
        finally
        {
            ShipArt.Folder = null;
        }
    }

    /// <summary>
    /// Three grip-backs from a slot row: two return through the drill to the Fleet root, and the third
    /// carries the panel off the tab rather than doing anything to that root, the same as a grip-back at
    /// the root of any other tab.
    /// </summary>
    [AvaloniaFact]
    public void ThreeGripBacksFromASlotRowReturnToTheRoot()
    {
        var (panel, pixels) = Headset();

        Draw(panel, pixels, LoadoutPages.FleetRoot);

        var card = Pressable(
            panel,
            control => control is Button
                && control.GetVisualDescendants().OfType<TextBlock>().Any(text => (text.Text ?? string.Empty).Contains("Bad Idea", StringComparison.Ordinal)));

        Assert.NotNull(card);
        Assert.True(Press(panel, pixels, card!));
        Dispatcher.UIThread.RunJobs();

        // A full frame, the way the runtime asks for one after any press: the strip's own pane count is
        // decided from its arranged width and settles a beat behind the drill itself.
        panel.Invalidate();
        panel.Draw(pixels.Address, pixels.RowBytes);
        Dispatcher.UIThread.RunJobs();

        panel.Invalidate();
        panel.Draw(pixels.Address, pixels.RowBytes);
        Dispatcher.UIThread.RunJobs();

        // Whichever slot row the mode listed first - the point is that a slot row takes a ray, not which
        // slot it is. A named button alone is not enough to say so: the tab strip and the scrollbar carry
        // automation names too. A slot row is the one built from LoadoutPages.SlotRow's own four-column
        // grid, at its own fixed height.
        var slot = Pressable(
            panel,
            control => control is Button { Bounds.Width: 430, Bounds.Height: 34 } button &&
                !string.IsNullOrEmpty(AutomationProperties.GetName(button)));

        Assert.NotNull(slot);
        Assert.True(Press(panel, pixels, slot!));
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(3, panel.Nav.Trail.Count);

        Assert.True(panel.Back());
        Assert.True(panel.Back());

        Assert.Single(panel.Nav.Trail);
        Assert.Equal(LoadoutPages.FleetRoot, panel.Nav.RootKeyOf(PanelTab.Loadout));

        // Nothing left in the drill: the third grip-back carries the panel off the Loadout tab instead,
        // the same as a grip-back at the root of any other tab - and leaves the root it left behind alone.
        Assert.True(panel.Back());
        Assert.NotEqual(PanelTab.Loadout, panel.Nav.Tab);
        Assert.Equal(LoadoutPages.FleetRoot, panel.Nav.RootKeyOf(PanelTab.Loadout));
    }

    /// <summary>
    /// The drag handlers stay desktop-only: <see cref="Draggable"/> answers to a Ctrl-held pointer-moved
    /// stream, and this surface's whole public reach into the board is one click with no modifier and
    /// nothing that moves — the reason a ray can copy a plan onto a slot has no path to ride here.
    /// </summary>
    [AvaloniaFact]
    public void NoPointerMovedPathReachesTheHeadsetSurface()
    {
        var methods = typeof(VrPanelSurface)
            .GetMethods(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance)
            .Where(method => method.DeclaringType == typeof(VrPanelSurface))
            .ToList();

        Assert.DoesNotContain(methods, method => method.Name.Contains("Move", StringComparison.Ordinal));

        Assert.DoesNotContain(
            methods,
            method => method.GetParameters().Any(parameter => parameter.ParameterType == typeof(KeyModifiers)));
    }
}
