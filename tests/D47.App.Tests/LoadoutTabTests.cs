using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using D47.App.Panel;
using D47.Core.Checklists;
using D47.Core.Interface;
using D47.Core.Journal;
using D47.Core.Ships;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.App.Tests;

/// <summary>
/// The Loadout tab: Fleet, then a ship, then a slot (list.md Phase 26, "Ships").
/// <para>
/// Three levels of the drill stack Phase 25 built, so the breadcrumb, the reflow and the way back
/// come from there rather than from anything here.
/// </para>
/// </summary>
public class LoadoutTabTests
{
    private sealed record Surface(
        Window Window, PanelView Panel, ShipPlanService Ships, ChecklistService Checklists);

    private static Surface Open(bool flying = true)
    {
        var root = TempFolders.Create("d47-loadout-tests");

        var checklists = new ChecklistService(
            new ChecklistStore(Path.Combine(root, "checklist.json"), NullLogger<ChecklistStore>.Instance),
            new ChecklistProposalStore(
                Path.Combine(root, "checklist-proposals.json"),
                NullLogger<ChecklistProposalStore>.Instance),
            () => null);

        var store = new ShipBuildStore(
            Path.Combine(root, "ships.json"), NullLogger<ShipBuildStore>.Instance);

        var state = flying ? Flying() : null;
        var ships = new ShipPlanService(store, checklists, () => state);

        var panel = new PanelView { DataContext = new PanelViewModel() };

        panel.EnableLoadout(ships, checklists, () => state);

        var window = new Window { Content = panel, Width = 900, Height = 700 };
        window.Show();

        panel.Tab = PanelTab.Loadout;
        Dispatcher.UIThread.RunJobs();

        return new Surface(window, panel, ships, checklists);
    }

    private static CommanderGameState Flying()
    {
        var store = new GameStateStore();

        foreach (var line in new[]
                 {
                     """{"timestamp":"2026-08-18T09:00:00Z","event":"Commander","FID":"F1","Name":"Jameson"}""",
                     """{"timestamp":"2026-08-18T09:00:00Z","event":"EngineerProgress","Engineers":[{"Engineer":"Felicity Farseer","EngineerID":300100,"Progress":"Unlocked","Rank":5}]}""",
                     """{"timestamp":"2026-08-18T09:00:00Z","event":"Loadout","Ship":"python","ShipID":12,"ShipName":"Bad Idea","ShipIdent":"BI-01","Modules":[{"Slot":"MainEngines","Item":"int_engine_size5_class5","On":true,"Priority":0,"Health":1.0}]}""",
                 })
        {
            Assert.True(JournalEvent.TryParse(line, NullLogger.Instance, out var parsed));
            store.Apply(parsed!);
        }

        return store.Active!;
    }

    private static IReadOnlyList<string> Text(PanelView panel) =>
        [.. panel.GetVisualDescendants().OfType<TextBlock>().Select(block => block.Text ?? string.Empty)];

    private static Button Row(PanelView panel, string label) =>
        panel.GetVisualDescendants().OfType<Button>()
            .First(button => button.GetVisualDescendants().OfType<TextBlock>()
                .Any(text => text.Text == label));

    /// <summary>The tab arrives when the host furnishes it, on both surfaces.</summary>
    [AvaloniaFact]
    public void TheTabIsThereOnceTheHostGivesIt()
    {
        var panel = new PanelView { DataContext = new PanelViewModel() };

        Assert.False(panel.FindControl<Control>("LoadoutTab")!.IsVisible);

        var surface = Open();

        Assert.True(surface.Panel.FindControl<Control>("LoadoutTab")!.IsVisible);

        surface.Window.Close();
    }

    /// <summary>
    /// The fleet is a root rather than a level, and it earns being landed on by answering where
    /// each ship is before anything is drilled.
    /// </summary>
    [AvaloniaFact]
    public void TheFleetOpensOnWhereEachShipIs()
    {
        var surface = Open();

        Assert.True(surface.Panel.Nav.AtRoot);
        Assert.Equal("Ships", surface.Panel.Nav.Root.Word);

        var shown = Text(surface.Panel);

        Assert.Contains("Bad Idea (Python)", shown);
        Assert.Contains(shown, line => line.Contains("flying", StringComparison.Ordinal));

        surface.Window.Close();
    }

    /// <summary>
    /// A hull the Commander does not own appears in the list and says it is not bought yet —
    /// acquiring it is the plan's first step rather than a precondition sitting outside it.
    /// </summary>
    [AvaloniaFact]
    public void AHullYouDoNotOwnSaysSo()
    {
        var surface = Open();

        surface.Ships.Intend("Anaconda");
        Dispatcher.UIThread.RunJobs();

        var shown = Text(surface.Panel);

        Assert.Contains("Anaconda", shown);
        Assert.Contains(shown, line => line.Contains("not bought yet", StringComparison.Ordinal));

        surface.Window.Close();
    }

    /// <summary>Fleet, then the ship, then the slot — one stack, with the breadcrumb behind it.</summary>
    [AvaloniaFact]
    public void DrillingGoesFleetThenShipThenSlot()
    {
        var surface = Open();

        Row(surface.Panel, "Bad Idea (Python)").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(["Ships", "Bad Idea"], surface.Panel.Nav.Trail.Select(crumb => crumb.Word));

        // The slot index: one line each, drawn from what the journal reports for the ship being
        // flown as well as from what is planned.
        Row(surface.Panel, "MainEngines").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(
            ["Ships", "Bad Idea", "MainEngines"],
            surface.Panel.Nav.Trail.Select(crumb => crumb.Word));

        surface.Window.Close();
    }

    /// <summary>
    /// Fitted and planned are two blocks and never one merged line, because a plan is a second
    /// thing the Commander wants rather than an edit to the truth.
    /// </summary>
    [AvaloniaFact]
    public void FittedAndPlannedAreTwoBlocks()
    {
        var surface = Open();

        var build = surface.Ships.BuildFor(12, "python", "Bad Idea");

        surface.Ships.Plan(
            build.Id, new SlotPlan("MainEngines", "Dirty Drive Tuning", 5, "Felicity Farseer"));

        surface.Panel.Nav.GoTo(
            LoadoutPages.Ship(surface.Ships.Fleet()[0]),
            LoadoutPages.Slot(build.Id, "MainEngines"));

        Dispatcher.UIThread.RunJobs();

        var shown = Text(surface.Panel);

        Assert.Contains("Fitted", shown);
        Assert.Contains("Planned", shown);

        // What is actually there, and what is wanted, each on its own.
        Assert.Contains(shown, line => line.Contains("Engine", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(shown, line => line.Contains("Dirty Drive Tuning", StringComparison.Ordinal));

        // And the cost of this plan, on this slot, with held and needed both.
        Assert.Contains("What it costs", shown);

        surface.Window.Close();
    }

    /// <summary>
    /// A plan carries the journal's verdict and no checkbox: a derived item's progress is a diff
    /// against live state, and a tick would be undone or left standing and lying by the next read.
    /// </summary>
    [AvaloniaFact]
    public void APlanCarriesAVerdictAndNoCheckbox()
    {
        var surface = Open();

        var build = surface.Ships.BuildFor(12, "python", "Bad Idea");

        surface.Ships.Plan(build.Id, new SlotPlan("MainEngines", "Dirty Drive Tuning", 5));

        surface.Panel.Nav.GoTo(
            LoadoutPages.Ship(surface.Ships.Fleet()[0]),
            LoadoutPages.Slot(build.Id, "MainEngines"));

        Dispatcher.UIThread.RunJobs();

        Assert.Empty(surface.Panel.GetVisualDescendants().OfType<CheckBox>());

        // The engines are fitted and unengineered, so the verdict is that it is not done - said,
        // rather than left blank.
        Assert.Contains(
            Text(surface.Panel),
            line => line.Contains("no engineering", StringComparison.OrdinalIgnoreCase)
                    || line.Contains("not", StringComparison.OrdinalIgnoreCase));

        surface.Window.Close();
    }

    /// <summary>
    /// A ship the Commander is not flying reports no modules at all, and the page says so rather
    /// than showing a blank that implies disagreement.
    /// </summary>
    [AvaloniaFact]
    public void AShipYouAreNotFlyingSaysNothingCanBeSaid()
    {
        var surface = Open(flying: false);

        var build = surface.Ships.Intend("Python")!;

        surface.Ships.Plan(build.Id, new SlotPlan("MainEngines", "Dirty Drive Tuning", 5));

        surface.Panel.Nav.GoTo(
            new NavCrumb(LoadoutPages.ShipPrefix + build.Id, "Python"),
            LoadoutPages.Slot(build.Id, "MainEngines"));

        Dispatcher.UIThread.RunJobs();

        Assert.Contains(
            Text(surface.Panel),
            line => line.Contains("sitting in", StringComparison.Ordinal));

        surface.Window.Close();
    }

    /// <summary>
    /// The ray points and the voice edits, so every page carries the phrase for what it is
    /// showing — a page that offers no phrase is one whose faster half is invisible.
    /// </summary>
    [AvaloniaFact]
    public void EveryPageCarriesItsSayLine()
    {
        var surface = Open();

        Assert.Contains(Text(surface.Panel), line => line.StartsWith("Say:", StringComparison.Ordinal));

        Row(surface.Panel, "Bad Idea (Python)").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();

        Assert.Contains(Text(surface.Panel), line => line.StartsWith("Say:", StringComparison.Ordinal));

        surface.Window.Close();
    }

    /// <summary>
    /// Promoting from the ship page proposes rather than writing, which is the trust boundary the
    /// whole phase turns on: the plan owns what, the checklist owns when.
    /// </summary>
    [AvaloniaFact]
    public void PromotingFromThePageProposesRatherThanWriting()
    {
        var surface = Open();

        var build = surface.Ships.BuildFor(12, "python", "Bad Idea");

        surface.Ships.Plan(build.Id, new SlotPlan("MainEngines", "Dirty Drive Tuning", 5));

        surface.Panel.Nav.Drill(LoadoutPages.Ship(surface.Ships.Fleet()[0]));
        Dispatcher.UIThread.RunJobs();

        Row(surface.Panel, "Put this build on my checklist")
            .RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

        Dispatcher.UIThread.RunJobs();

        Assert.Empty(surface.Checklists.Document.Items);
        Assert.NotEmpty(surface.Checklists.Proposals.Pending);

        surface.Window.Close();
    }

    /// <summary>The page at the size the headset renders it, for a human to look at.</summary>
    [AvaloniaFact]
    public void TheLoadoutTabRendersToACapture()
    {
        var surface = Open();

        var build = surface.Ships.BuildFor(12, "python", "Bad Idea");

        surface.Ships.Plan(build.Id, new SlotPlan("MainEngines", "Dirty Drive Tuning", 5));
        surface.Ships.Intend("Anaconda");

        surface.Window.Width = 1024;
        surface.Window.Height = 640;

        Dispatcher.UIThread.RunJobs();

        surface.Window.CaptureRenderedFrame()!.Save(
            Path.Combine(TestSurface.CaptureDirectory, "loadout-fleet.png"),
            new Avalonia.Media.Imaging.PngBitmapEncoderOptions());

        surface.Window.Close();
    }
}
