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
using D47.Core.Knowledge;
using D47.Core.Loadout;
using D47.Core.Ships;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.App.Tests;

/// <summary>
/// The Loadout tab's other two modes: Suits and weapons, and the gap (Phase 27).
/// <para>
/// <b>One page kind built once and shown twice.</b> The assertions here are deliberately the same
/// assertions <see cref="LoadoutTabTests"/> makes about Ships — an index, a drill, two blocks on a
/// slot, a say-line on every level — because if the on-foot mode needed different ones then the
/// pages are not the same pages.
/// </para>
/// </summary>
public class OnFootLoadoutTabTests
{
    private sealed record Surface(
        Window Window,
        PanelView Panel,
        ShipPlanService Ships,
        OnFootPlanService Kit,
        ChecklistService Checklists);

    private static Surface Open(bool onFoot = true)
    {
        var root = TempFolders.Create("d47-onfoot-loadout-tests");

        var state = onFoot ? OnFoot() : null;

        var checklists = new ChecklistService(
            new ChecklistStore(Path.Combine(root, "checklist.json"), NullLogger<ChecklistStore>.Instance),
            new ChecklistProposalStore(
                Path.Combine(root, "checklist-proposals.json"),
                NullLogger<ChecklistProposalStore>.Instance),
            () => state);

        var ships = new ShipPlanService(
            new ShipBuildStore(Path.Combine(root, "ships.json"), NullLogger<ShipBuildStore>.Instance),
            checklists,
            () => state);

        var kit = new OnFootPlanService(
            new OnFootBuildStore(
                Path.Combine(root, "on-foot.json"), NullLogger<OnFootBuildStore>.Instance),
            checklists,
            () => state);

        var panel = new PanelView { DataContext = new PanelViewModel() };

        panel.EnableLoadout(ships, checklists, () => state, kit);

        var window = new Window { Content = panel, Width = 900, Height = 700 };
        window.Show();

        panel.Tab = PanelTab.Loadout;
        Dispatcher.UIThread.RunJobs();

        return new Surface(window, panel, ships, kit, checklists);
    }

    /// <summary>
    /// A Commander on foot in a grade 3 Maverick carrying one weapon, rank 5 with Farseer, with a
    /// ship and some materials — enough for both ledgers to have something in them.
    /// </summary>
    private static CommanderGameState OnFoot()
    {
        var store = new GameStateStore();

        foreach (var line in new[]
                 {
                     """{"timestamp":"2026-08-18T09:00:00Z","event":"Commander","FID":"F1","Name":"Jameson"}""",
                     """{"timestamp":"2026-08-18T09:00:00Z","event":"SuitLoadout","SuitID":7,"SuitName":"utilitysuit_class3","LoadoutName":"Ground","SuitMods":[],"Modules":[{"SlotName":"PrimaryWeapon1","SuitModuleID":9,"ModuleName":"wpn_m_assaultrifle_kinetic_fauto","Class":2,"WeaponMods":[]}]}""",
                     """{"timestamp":"2026-08-18T09:00:00Z","event":"EngineerProgress","Engineers":[{"Engineer":"Felicity Farseer","EngineerID":300100,"Progress":"Unlocked","Rank":5}]}""",
                     """{"timestamp":"2026-08-18T09:00:00Z","event":"Materials","Raw":[{"Name":"iron","Count":20}],"Manufactured":[],"Encoded":[]}""",
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

    /// <summary>
    /// Three roots of one tab rather than three tabs: the game separates ship and on-foot hard and
    /// so does its vocabulary, but nothing about the layout is redrawn.
    /// </summary>
    [AvaloniaFact]
    public void TheTabHasThreeModes()
    {
        var surface = Open();

        // Carrier joined them in #230, on the tab that took its name.
        Assert.Equal(
            ["Ships", "Suits", "Gap", "Carrier"],
            surface.Panel.Nav.Roots(PanelTab.Loadout).Select(root => root.Word));

        surface.Window.Close();
    }

    /// <summary>
    /// The index is what the Commander is wearing and carrying, and it answers where each thing is
    /// before anything is drilled — the fleet page's own claim, on foot.
    /// </summary>
    [AvaloniaFact]
    public void SuitsOpensOnWhatYouAreWearing()
    {
        var surface = Open();

        Assert.True(surface.Panel.Nav.SelectRoot(OnFootMode.Root));
        Dispatcher.UIThread.RunJobs();

        var shown = Text(surface.Panel);

        Assert.Contains("Maverick Suit", shown);
        Assert.Contains(shown, line => line.Contains("on you, grade 3", StringComparison.Ordinal));

        // And the say-line, on this level as on every other.
        Assert.Contains(shown, line => line.StartsWith("Say:", StringComparison.Ordinal));

        surface.Window.Close();
    }

    /// <summary>
    /// Suits, then the item, then the slot — the same three levels of the same drill stack, with
    /// the grade first because a grade 1 item has no modification slots.
    /// </summary>
    [AvaloniaFact]
    public void DrillingGoesKitThenItemThenSlot()
    {
        var surface = Open();

        surface.Panel.Nav.SelectRoot(OnFootMode.Root);
        Dispatcher.UIThread.RunJobs();

        Row(surface.Panel, "Maverick Suit").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(
            ["Suits", "Maverick Suit"],
            surface.Panel.Nav.Trail.Select(crumb => crumb.Word));

        // The grade is the first slot, and a grade 3 suit has two modification slots under it.
        var slots = Text(surface.Panel);

        Assert.Contains("Grade", slots);
        Assert.Contains("Mod 1", slots);
        Assert.Contains("Mod 2", slots);
        Assert.DoesNotContain("Mod 3", slots);

        Row(surface.Panel, "Grade").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(
            ["Suits", "Maverick Suit", "Grade"],
            surface.Panel.Nav.Trail.Select(crumb => crumb.Word));

        surface.Window.Close();
    }

    /// <summary>
    /// Fitted and planned are two blocks and never one merged line, on foot as on a hull — and a
    /// mod slot says outright that its number is d47's own, because Elite reports modifications as
    /// a set with no positions in it.
    /// </summary>
    [AvaloniaFact]
    public void FittedAndPlannedAreTwoBlocksOnFootToo()
    {
        var surface = Open();

        var build = surface.Kit.BuildFor(OnFootKind.Suit, 7, "Maverick Suit");

        surface.Kit.Plan(build.Id, new KitPlan(OnFootBuild.GradeSlot, 5));

        surface.Panel.Nav.SelectRoot(OnFootMode.Root);
        surface.Panel.Nav.GoTo(
            new NavCrumb(OnFootMode.KitPrefix + build.Id, "Maverick Suit"),
            new NavCrumb($"{OnFootMode.KitSlotPrefix}{build.Id}|{OnFootBuild.GradeSlot}", "Grade"));

        Dispatcher.UIThread.RunJobs();

        var shown = Text(surface.Panel);

        Assert.Contains("Fitted", shown);
        Assert.Contains("Planned", shown);

        // What it is now, and what is wanted, each on its own.
        Assert.Contains("Grade 3", shown);
        Assert.Contains(shown, line => line.Contains("grade 5", StringComparison.Ordinal));

        // And what the two upgrade steps cost, exactly - nothing on foot is rolled.
        Assert.Contains("What it costs", shown);

        // No checkbox: a derived item's progress is a diff against live state.
        Assert.Empty(surface.Panel.GetVisualDescendants().OfType<CheckBox>());

        surface.Window.Close();
    }

    /// <summary>
    /// Promoting from the page proposes rather than writing, which is the boundary the whole
    /// arrangement turns on: the plan owns what, the checklist owns when.
    /// </summary>
    [AvaloniaFact]
    public void PromotingASuitPlanProposesRatherThanWriting()
    {
        var surface = Open();

        var build = surface.Kit.BuildFor(OnFootKind.Suit, 7, "Maverick Suit");

        surface.Kit.Plan(build.Id, new KitPlan(OnFootBuild.GradeSlot, 5));

        surface.Panel.Nav.SelectRoot(OnFootMode.Root);
        surface.Panel.Nav.Drill(new NavCrumb(OnFootMode.KitPrefix + build.Id, "Maverick Suit"));
        Dispatcher.UIThread.RunJobs();

        Row(surface.Panel, "Put this plan on my checklist")
            .RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

        Dispatcher.UIThread.RunJobs();

        Assert.Empty(surface.Checklists.Document.Items);
        Assert.NotEmpty(surface.Checklists.Proposals.Pending);

        surface.Window.Close();
    }

    /// <summary>
    /// The gap reads across both the other modes, keeps the ledgers apart, and says what each
    /// shortfall is for.
    /// </summary>
    [AvaloniaFact]
    public void TheGapReadsAcrossBothAndNamesWhatWantsIt()
    {
        var surface = Open();

        var ship = surface.Ships.Intend("Python")!;

        surface.Ships.Plan(
            ship.Id, new SlotPlan("MainEngines", "Dirty Drive Tuning", 3, "Felicity Farseer"));

        var suit = surface.Kit.BuildFor(OnFootKind.Suit, 7, "Maverick Suit");

        surface.Kit.Plan(suit.Id, new KitPlan(OnFootBuild.GradeSlot, 4));

        Assert.True(surface.Panel.Nav.SelectRoot(LoadoutPages.GapRoot));
        Dispatcher.UIThread.RunJobs();

        var shown = Text(surface.Panel);

        Assert.Contains(shown, line => line.Contains("units still to find", StringComparison.Ordinal));

        // Two ledgers, never added together.
        Assert.Contains(shown, line => line.StartsWith("Materials —", StringComparison.Ordinal));
        Assert.Contains(shown, line => line.StartsWith("Ship locker —", StringComparison.Ordinal));

        // And they are not added together: the headline counts units to find, which is a shopping
        // list rather than a balance.
        Assert.Contains(
            shown,
            line => line.Contains("never a balance", StringComparison.Ordinal));

        // And every shortfall reads back to what asked for it.
        Assert.Contains(shown, line => line.StartsWith("Wanted by:", StringComparison.Ordinal));

        surface.Window.Close();
    }

    /// <summary>
    /// Whether hulls the Commander does not own count is a filter they flip, not a decision taken
    /// once on their behalf — so the button says which question it would ask next.
    /// </summary>
    [AvaloniaFact]
    public void TheGapCanBeToldToCountOnlyWhatYouOwn()
    {
        var surface = Open();

        var ship = surface.Ships.Intend("Python")!;

        surface.Ships.Plan(
            ship.Id, new SlotPlan("MainEngines", "Dirty Drive Tuning", 3, "Felicity Farseer"));

        surface.Panel.Nav.SelectRoot(LoadoutPages.GapRoot);
        Dispatcher.UIThread.RunJobs();

        Assert.Contains(
            Text(surface.Panel),
            line => line.Contains("units still to find", StringComparison.Ordinal));

        Row(surface.Panel, "Counting what you do not own yet — show only what you can finish now")
            .RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

        Dispatcher.UIThread.RunJobs();

        // The only plan was for a hull nobody owns, so excluding it leaves nothing planned at all
        // — which is a different answer from "you have everything", and says so.
        Assert.Contains(
            Text(surface.Panel),
            line => line.Contains("Nothing is planned yet", StringComparison.Ordinal));

        surface.Window.Close();
    }

    /// <summary>Both new pages at the size the headset renders them, for a human to look at.</summary>
    [AvaloniaFact]
    public void TheSuitsAndGapPagesRenderToACapture()
    {
        var surface = Open();

        var suit = surface.Kit.BuildFor(OnFootKind.Suit, 7, "Maverick Suit");

        surface.Kit.Plan(suit.Id, new KitPlan(OnFootBuild.GradeSlot, 5));
        surface.Kit.Plan(suit.Id, new KitPlan(OnFootBuild.ModSlot(1), Modification: "Night Vision"));
        surface.Kit.Intend("Dominator");

        surface.Window.Width = 1024;
        surface.Window.Height = 640;

        surface.Panel.Nav.SelectRoot(OnFootMode.Root);
        Dispatcher.UIThread.RunJobs();

        surface.Window.CaptureRenderedFrame()!.Save(
            Path.Combine(TestSurface.CaptureDirectory, "loadout-suits.png"),
            new Avalonia.Media.Imaging.PngBitmapEncoderOptions());

        surface.Panel.Nav.SelectRoot(LoadoutPages.GapRoot);
        Dispatcher.UIThread.RunJobs();

        surface.Window.CaptureRenderedFrame()!.Save(
            Path.Combine(TestSurface.CaptureDirectory, "loadout-gap.png"),
            new Avalonia.Media.Imaging.PngBitmapEncoderOptions());

        surface.Window.Close();
    }

    /// <summary>
    /// A surface handed no on-foot plans has the one root Phase 26 gave it, rather than two that
    /// answer nothing.
    /// </summary>
    [AvaloniaFact]
    public void ASurfaceWithNoOnFootHalfKeepsTheOneRoot()
    {
        var root = TempFolders.Create("d47-onfoot-loadout-tests");

        var checklists = new ChecklistService(
            new ChecklistStore(Path.Combine(root, "checklist.json"), NullLogger<ChecklistStore>.Instance),
            new ChecklistProposalStore(
                Path.Combine(root, "checklist-proposals.json"),
                NullLogger<ChecklistProposalStore>.Instance),
            () => null);

        var ships = new ShipPlanService(
            new ShipBuildStore(Path.Combine(root, "ships.json"), NullLogger<ShipBuildStore>.Instance),
            checklists,
            () => null);

        var panel = new PanelView { DataContext = new PanelViewModel() };

        panel.EnableLoadout(ships, checklists, () => null);

        // Suits and Gap are the on-foot half and are absent; Carrier is not gated on owning one,
        // because "d47 has not seen a carrier" is a thing the page can say and "you have none" is
        // not something it can know (#230).
        Assert.Equal(
            ["Ships", "Carrier"],
            panel.Nav.Roots(PanelTab.Loadout).Select(item => item.Word));
    }
}
