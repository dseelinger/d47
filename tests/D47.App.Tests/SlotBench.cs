using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using D47.App.Panel;
using D47.Core.Checklists;
using D47.Core.Interface;
using D47.Core.Journal;
using D47.Core.Knowledge;
using D47.Core.Loadout;
using D47.Core.Ships;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.App.Tests;

/// <summary>A Loadout tab holding one Python and one Maverick suit, driven by real mouse presses.</summary>
internal sealed class SlotBench
{
    private SlotBench(Window window, PanelView panel, OnFootPlanService kit)
    {
        Window = window;
        Panel = panel;
        Kit = kit;
    }

    public Window Window { get; }

    public PanelView Panel { get; }

    public OnFootPlanService Kit { get; }

    public static SlotBench Open(double width = 1400)
    {
        var root = TempFolders.Create("d47-slot-bench");
        var state = Flying();

        var checklists = new ChecklistService(
            new ChecklistStore(Path.Combine(root, "checklist.json"), NullLogger<ChecklistStore>.Instance),
            new ChecklistProposalStore(
                Path.Combine(root, "checklist-proposals.json"), NullLogger<ChecklistProposalStore>.Instance),
            () => state);

        var ships = new ShipPlanService(
            new ShipBuildStore(Path.Combine(root, "ships.json"), NullLogger<ShipBuildStore>.Instance),
            checklists,
            () => state);

        var kit = new OnFootPlanService(
            new OnFootBuildStore(Path.Combine(root, "on-foot.json"), NullLogger<OnFootBuildStore>.Instance),
            checklists,
            () => state);

        var panel = new PanelView { DataContext = new PanelViewModel() };

        panel.EnableLoadout(ships, checklists, () => state, kit);

        var window = new Window { Content = panel, Width = width, Height = 1600 };

        window.Show();
        panel.Tab = PanelTab.Assets;
        Dispatcher.UIThread.RunJobs();

        return new SlotBench(window, panel, kit);
    }

    private static CommanderGameState Flying()
    {
        var store = new GameStateStore();

        foreach (var line in new[]
                 {
                     """{"timestamp":"2026-08-18T09:00:00Z","event":"Commander","FID":"F1","Name":"Jameson"}""",
                     """{"timestamp":"2026-08-18T09:00:00Z","event":"Loadout","Ship":"python","ShipID":12,"ShipName":"Bad Idea","ShipIdent":"BI-01","MaxJumpRange":34.25,"CargoCapacity":128,"UnladenMass":350.5,"HullValue":55000000,"ModulesValue":45000000,"Rebuy":5000000,"HullHealth":0.87,"Modules":[{"Slot":"MainEngines","Item":"int_engine_size5_class5","On":true,"Priority":0,"Health":1.0},{"Slot":"LargeHardpoint1","Item":"hpt_pulselaser_gimbal_large","On":true,"Priority":0,"Health":1.0}]}""",
                     """{"timestamp":"2026-08-18T09:00:00Z","event":"SuitLoadout","SuitID":7,"SuitName":"utilitysuit_class3","LoadoutName":"Ground","SuitMods":[],"Modules":[{"SlotName":"PrimaryWeapon1","SuitModuleID":9,"ModuleName":"wpn_m_assaultrifle_kinetic_fauto","Class":2,"WeaponMods":[]}]}""",
                 })
        {
            Assert.True(JournalEvent.TryParse(line, NullLogger.Instance, out var parsed));
            store.Apply(parsed!);
        }

        return store.Active!;
    }

    public void OpenShip()
    {
        Row("Bad Idea (Python)").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();
    }

    public void OpenSuit()
    {
        Panel.Nav.SelectRoot(OnFootMode.Root);
        Dispatcher.UIThread.RunJobs();

        var build = Kit.BuildFor(OnFootKind.Suit, 7, "Maverick Suit");

        Panel.Nav.GoTo(new NavCrumb(OnFootMode.KitPrefix + build.Id, "Maverick Suit"));
        Dispatcher.UIThread.RunJobs();
    }

    public Button Row(string label) =>
        Panel.GetVisualDescendants().OfType<Button>()
            .First(button => AutomationProperties.GetName(button) == label
                             || button.GetVisualDescendants().OfType<TextBlock>().Any(text => text.Text == label));

    public Point Centre(Control control) =>
        control.TranslatePoint(new Point(control.Bounds.Width / 2, control.Bounds.Height / 2), Window)
        ?? throw new InvalidOperationException("That control is not in the window.");

    /// <summary>One left press and release, then the work the click queued (the page rebuilds its rows).</summary>
    public void Click(Point at)
    {
        Window.MouseDown(at, MouseButton.Left);
        Window.MouseUp(at, MouseButton.Left);
        Dispatcher.UIThread.RunJobs();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        Dispatcher.UIThread.RunJobs();
    }

    /// <summary>Opens another slot first, so the strip is already at its full width and a click moves nothing.</summary>
    public void OpenASlot(string label)
    {
        Row(label).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        Dispatcher.UIThread.RunJobs();
    }

    /// <summary>A left press on a row carrying the click count the pointer stack would have given it.</summary>
    public void PressAs(Control row, int clickCount, KeyModifiers modifiers = KeyModifiers.None)
    {
        row.RaiseEvent(new PointerPressedEventArgs(
            row,
            new Pointer(1, PointerType.Mouse, isPrimary: true),
            Panel,
            Centre(row),
            0,
            new PointerPointProperties(RawInputModifiers.LeftMouseButton, PointerUpdateKind.LeftButtonPressed),
            modifiers,
            clickCount));
        Dispatcher.UIThread.RunJobs();
    }

    public void DoubleClick(Point at)
    {
        Click(at);
        Click(at);
    }

    /// <summary>Whether the module chooser is up: it sits in the panel's modal pane, not on the trail.</summary>
    public bool AskingForAModule() =>
        Panel.GetControl<Border>("ModalPane").GetVisualDescendants().OfType<TextBlock>()
            .Any(text => text.Text is { } said && said.StartsWith("What goes in", StringComparison.Ordinal));

    /// <summary>Whether the trail ends at the prompt with this key.</summary>
    public bool Asking(string key) => Panel.Nav.Trail.Count > 0 && Panel.Nav.Trail[^1].Key == key;
}
