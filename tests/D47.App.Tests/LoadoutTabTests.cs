using Avalonia.Controls;
using Avalonia.Controls.Documents;
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

    private static Surface Open(bool flying = true, bool checklist = false, bool engineered = false)
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

        var state = flying ? Flying(engineered) : null;
        var ships = new ShipPlanService(store, checklists, () => state);

        var panel = new PanelView { DataContext = new PanelViewModel() };

        panel.EnableLoadout(ships, checklists, () => state);

        // A second tab, for the tests that need somewhere to go and come back from.
        if (checklist)
        {
            panel.EnableChecklist(checklists);
        }

        var window = new Window { Content = panel, Width = 900, Height = 700 };
        window.Show();

        panel.Tab = PanelTab.Loadout;
        Dispatcher.UIThread.RunJobs();

        return new Surface(window, panel, ships, checklists);
    }

    private static CommanderGameState Flying(bool engineered = false)
    {
        var store = new GameStateStore();

        foreach (var line in new[]
                 {
                     """{"timestamp":"2026-08-18T09:00:00Z","event":"Commander","FID":"F1","Name":"Jameson"}""",
                     """{"timestamp":"2026-08-18T09:00:00Z","event":"StoredShips","StarSystem":"Shinrarta Dezhra","StationName":"Jameson Memorial","ShipsHere":[{"ShipID":7,"ShipType":"anaconda","ShipType_Localised":"Anaconda","Name":"Big Slow","Value":150000000}],"ShipsRemote":[]}""",
                     """{"timestamp":"2026-08-18T09:00:00Z","event":"EngineerProgress","Engineers":[{"Engineer":"Felicity Farseer","EngineerID":300100,"Progress":"Unlocked","Rank":5}]}""",
                     """{"timestamp":"2026-08-18T09:00:00Z","event":"Loadout","Ship":"python","ShipID":12,"ShipName":"Bad Idea","ShipIdent":"BI-01","MaxJumpRange":34.25,"CargoCapacity":128,"UnladenMass":350.5,"HullValue":55000000,"ModulesValue":45000000,"Rebuy":5000000,"HullHealth":0.87,"Modules":[{"Slot":"MainEngines","Item":"int_engine_size5_class5","On":true,"Priority":0,"Health":1.0},{"Slot":"LargeHardpoint1","Item":"hpt_pulselaser_gimbal_large","On":true,"Priority":0,"Health":1.0ENGINEERING},{"Slot":"PaintJob","Item":"paintjob_python_metallic_gold","On":true,"Priority":0,"Health":1.0},{"Slot":"VesselVoice","Item":"voicepack_verity","On":true,"Priority":0,"Health":1.0},{"Slot":"PlanetaryApproachSuite","Item":"int_planetapproachsuite_advanced","On":true,"Priority":0,"Health":1.0}]}""",
                 })
        {
            // A rolled module where the test asks for one, so the mark that says "this has been
            // engineered" has something to be drawn against.
            var written = line.Replace(
                "ENGINEERING",
                engineered
                    ? ""","Engineering":{"BlueprintName":"Weapon_LongRange","Level":5,"Quality":0.9,"Engineer":"Felicity Farseer"}"""
                    : string.Empty,
                StringComparison.Ordinal);

            Assert.True(JournalEvent.TryParse(written, NullLogger.Instance, out var parsed));
            store.Apply(parsed!);
        }

        return store.Active!;
    }

    private static IReadOnlyList<string> Text(PanelView panel) =>
        [.. panel.GetVisualDescendants().OfType<TextBlock>().Select(block => block.Text ?? string.Empty)];

    private static Control Modal(PanelView panel) =>
        panel.GetControl<Border>("ModalPane");

    private static IReadOnlyList<string> Offered(PanelView panel) =>
        [.. Modal(panel).GetVisualDescendants().OfType<TextBlock>()
            .Select(block => block.Text ?? string.Empty)];

    private static Button Pick(PanelView panel, string label) =>
        Modal(panel).GetVisualDescendants().OfType<Button>()
            .First(button => button.GetVisualDescendants().OfType<TextBlock>()
                .Any(text => text.Text == label));

    private static Button Press(PanelView panel, string label) =>
        panel.GetVisualDescendants().OfType<Button>()
            .First(button => button.GetVisualDescendants().OfType<TextBlock>()
                .Any(text => text.Text is { } said && said.Contains(label, StringComparison.Ordinal)));

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

    /// <summary>
    /// A tab that has been left and come back to still drills (remediation.md 17, item 5).
    /// <para>
    /// Reported as *"clicking on the Checklist tab and then on the Loadout tab prevents navigating
    /// to ships — clicking on a ship does nothing"*. The strip subscribed to the navigator in its
    /// constructor and unsubscribed on detach, and switching tab reparents it, so it went deaf and
    /// the pane stopped following the trail.
    /// </para>
    /// <para>
    /// <b>The assertion is what is drawn, not where the trail is.</b> The trail moved perfectly
    /// well throughout the fault — <c>Nav.Drill</c> pushes whether anybody is listening or not, and
    /// the breadcrumb row is drawn by the panel rather than by the strip, so both looked right
    /// while the content pane was frozen. A test watching the trail passes against the bug.
    /// </para>
    /// </summary>
    [AvaloniaFact]
    public void ATabThatHasBeenLeftStillDrills()
    {
        var surface = Open(checklist: true);

        Assert.Contains("Bad Idea (Python)", Text(surface.Panel));

        surface.Panel.Tab = PanelTab.Checklist;
        Dispatcher.UIThread.RunJobs();

        surface.Panel.Tab = PanelTab.Loadout;
        Dispatcher.UIThread.RunJobs();

        Row(surface.Panel, "Bad Idea (Python)").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();

        // The ship's slot index, which only exists if the pane followed the click.
        Assert.Contains("Main Engines", Text(surface.Panel));

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

        // The slot index: one line each, for every slot the hull has rather than only the ones
        // the journal mentioned, and said in words rather than in Frontier's spelling
        // (remediation.md 12, items 1, 3 and 4).
        Row(surface.Panel, "Main Engines").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(
            ["Ships", "Bad Idea", "Main Engines"],
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
    public void PromotingFromThePagePutsItOnTheChecklist()
    {
        var surface = Open();

        var build = surface.Ships.BuildFor(12, "python", "Bad Idea");

        surface.Ships.Plan(build.Id, new SlotPlan("MainEngines", "Dirty Drive Tuning", 5));

        surface.Panel.Nav.Drill(LoadoutPages.Ship(surface.Ships.Fleet()[0]));
        Dispatcher.UIThread.RunJobs();

        Row(surface.Panel, "Put this build on my checklist")
            .RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

        Dispatcher.UIThread.RunJobs();

        // Remediation 15 item 12, and the assertion is inverted from what it was: the button's own
        // words are the contract, and it used to make a proposal instead.
        Assert.NotEmpty(surface.Checklists.Document.Items);
        Assert.Empty(surface.Checklists.Proposals.Pending);

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

    /// <summary>
    /// The module list is the outfitting screen's four blocks, in its order
    /// (remediation.md 12, items 1 and 6).
    /// </summary>
    [AvaloniaFact]
    public void TheModuleListIsGroupedTheWayOutfittingIs()
    {
        var surface = Open();

        Row(surface.Panel, "Bad Idea (Python)").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();

        var shown = Text(surface.Panel).ToList();

        foreach (var heading in new[]
                 {
                     "Hardpoints", "Utility Mounts", "Core Internal", "Optional Internal",
                 })
        {
            Assert.Contains(heading, shown);
        }

        // In that order, and the utility mounts under their own heading rather than among the
        // hardpoints — which is where the journal's name for them would have put them.
        Assert.True(shown.IndexOf("Hardpoints") < shown.IndexOf("Utility Mounts"));
        Assert.True(shown.IndexOf("Utility Mounts") < shown.IndexOf("Core Internal"));
        Assert.True(shown.IndexOf("Core Internal") < shown.IndexOf("Optional Internal"));

        Assert.True(
            shown.IndexOf("Utility Mount 1") > shown.IndexOf("Utility Mounts"),
            "a utility mount is drawn under Utility Mounts");

        surface.Window.Close();
    }

    /// <summary>
    /// An empty slot is still a slot (remediation.md 12, item 3).
    /// <para>
    /// The fixture flies a Python with two modules on it. Every other slot the hull has is empty,
    /// and before this the page showed none of them — so there was nowhere to press to plan a
    /// weapon into an empty hardpoint.
    /// </para>
    /// </summary>
    [AvaloniaFact]
    public void AnEmptySlotIsStillASlot()
    {
        var surface = Open();

        Row(surface.Panel, "Bad Idea (Python)").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();

        var shown = Text(surface.Panel).ToList();

        // A Python has three large hardpoints and two medium; one of them is fitted.
        Assert.Contains("Large Hardpoint 1", shown);
        Assert.Contains("Large Hardpoint 2", shown);
        Assert.Contains("Medium Hardpoint 1", shown);

        // And the empty ones say so, rather than carrying a blank note that reads as d47 having
        // nothing to say about them.
        Assert.Contains("empty", shown);

        surface.Window.Close();
    }

    /// <summary>
    /// Nothing that is not outfitting is on the list (remediation.md 12, item 2).
    /// <para>
    /// The fixture fits a paint job, a voice pack and a planetary approach suite, all of which
    /// arrive in the <c>Loadout</c> event exactly as a power plant does. None of them is a thing
    /// anybody outfits.
    /// </para>
    /// </summary>
    [AvaloniaFact]
    public void NothingCosmeticReachesTheModuleList()
    {
        var surface = Open();

        Row(surface.Panel, "Bad Idea (Python)").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();

        var shown = string.Join("\n", Text(surface.Panel));

        Assert.DoesNotContain("PaintJob", shown, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("paintjob", shown, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("VesselVoice", shown, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("voicepack", shown, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("PlanetaryApproachSuite", shown, StringComparison.OrdinalIgnoreCase);

        surface.Window.Close();
    }

    /// <summary>
    /// A module is named the way the outfitting screen names it (remediation.md 12, item 4).
    /// <para>
    /// It used to be the journal's symbol with the underscores taken out, which is how "int" and
    /// "class5" ended up in a sentence a Commander reads.
    /// </para>
    /// </summary>
    [AvaloniaFact]
    public void AFittedModuleIsNamedRatherThanSpelt()
    {
        var surface = Open();

        Row(surface.Panel, "Bad Idea (Python)").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();

        var shown = Text(surface.Panel).ToList();
        var joined = string.Join("\n", shown);

        Assert.Contains("5A Thrusters", shown);
        Assert.Contains("3E Pulse Laser, gimballed", shown);

        Assert.DoesNotContain("int_engine", joined, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("int engine", joined, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("class5", joined, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("class 5", joined, StringComparison.OrdinalIgnoreCase);

        surface.Window.Close();
    }


    /// <summary>
    /// The gear sits after the module name rather than in front of it (remediation.md 17,
    /// item 10). Asked for as *"Gear Glyph should appear to the right of the Module Name, not
    /// leftmost"*.
    /// <para>
    /// <b>An inline of the name, which is what "to the right of it" has to mean here.</b> The
    /// name lives in the row's star column, so a glyph given a column of its own would sit against
    /// the note at the far edge and drift further from the name every time the window widened. The
    /// test therefore asks the label what its own last inline is, rather than asking the row what
    /// is in it somewhere.
    /// </para>
    /// <para>
    /// The dot stays where it was — a separate mark, a separate question, and the Commander asked
    /// for the gear.
    /// </para>
    /// </summary>
    [AvaloniaFact]
    public void TheGearFollowsTheModuleNameAndTheDotDoesNot()
    {
        var surface = Open(engineered: true);

        Row(surface.Panel, "Bad Idea (Python)").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();

        var label = surface.Panel.GetVisualDescendants().OfType<TextBlock>()
            .First(block => block.Inlines is { Count: > 0 } inlines
                && inlines[0] is Run named
                && named.Text == "Large Hardpoint 1");

        // The name, then the mark, in that order and in one block.
        Assert.Equal(2, label.Inlines!.Count);
        Assert.Equal(" ⚙", ((Run)label.Inlines[1]).Text);

        // And nothing before the name: an engineered row with no plan has no leading glyph at all.
        var row = label.GetVisualAncestors().OfType<Button>().First();

        Assert.DoesNotContain(
            row.GetVisualDescendants().OfType<TextBlock>(),
            block => block.Text == "⚙" || block.Text == "●");

        surface.Window.Close();
    }

    /// <summary>
    /// Keeping what is fitted still lists that module's rolls (remediation.md 17, item 9).
    /// <para>
    /// Reported against Oxen's Military 02: *"Keep the 5D Hull…" shows the wrong engineering
    /// options — Ammo Capacity was one listed — but selecting "5D Hull Reinforcement Package" does
    /// show the correct ones.* Keeping answers the module question with null on purpose, so that
    /// the plan does not come to name a module the Commander only wanted left alone; what went
    /// wrong is that the blueprint question lost its subject with it and fell through to the
    /// "d47 does not know this module" branch, which offers the whole catalogue.
    /// </para>
    /// <para>
    /// A pulse laser stands in for the hull reinforcement: same shape, and its seven rolls are all
    /// weapon mounts, so an offer of Ammo Capacity — which belongs to things that carry ammunition
    /// — is unambiguous evidence of the fallback rather than of a near miss.
    /// </para>
    /// </summary>
    [AvaloniaFact]
    public void KeepingWhatIsFittedListsThatModulesRolls()
    {
        var surface = Open();

        Row(surface.Panel, "Bad Idea (Python)").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();

        // The one with the gimballed large pulse laser in it.
        Row(surface.Panel, "Large Hardpoint 1").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();

        Press(surface.Panel, "Plan this slot").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();

        // The row names what is in the slot, which is the whole reason the question below has an
        // answer to fall back on.
        Assert.Contains(Offered(surface.Panel), line => line.StartsWith("Keep the 3E Pulse Laser", StringComparison.Ordinal));

        Modal(surface.Panel).GetVisualDescendants().OfType<Button>()
            .First(button => button.GetVisualDescendants().OfType<TextBlock>()
                .Any(text => text.Text?.StartsWith("Keep the 3E Pulse Laser", StringComparison.Ordinal) == true))
            .RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

        Dispatcher.UIThread.RunJobs();

        var rolls = Offered(surface.Panel);

        Assert.Contains("Focused Weapon", rolls);
        Assert.Contains("Long Range Weapon", rolls);

        // The reported symptom, and the one that says the catalogue was offered whole.
        Assert.DoesNotContain("Ammo Capacity", rolls);

        // It also says what it is asking about, which the fallback could not.
        Assert.Contains(rolls, line => line.Contains("Pulse Laser", StringComparison.Ordinal));

        surface.Window.Close();
    }

    /// <summary>
    /// Planning a slot offers the valid choices for it, and never asks for a spelling
    /// (remediation.md 12, item 5).
    /// <para>
    /// Three lists — the module, the roll, the grade — and the whole plan is made by pressing.
    /// </para>
    /// </summary>
    [AvaloniaFact]
    public void PlanningASlotOffersWhatFitsIt()
    {
        var surface = Open();

        Row(surface.Panel, "Bad Idea (Python)").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();

        Row(surface.Panel, "Large Hardpoint 2").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();

        Press(surface.Panel, "Plan this slot").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();

        var offered = Offered(surface.Panel);

        // A large hardpoint takes weapons, and it says which slot it is choosing for.
        Assert.Contains("Pulse Laser", offered);
        Assert.Contains("Plasma Accelerator", offered);
        Assert.Contains(offered, line => line.Contains("Large Hardpoint 2", StringComparison.Ordinal));

        // And nothing that goes somewhere else: a shield generator is an optional internal and a
        // shield booster is a utility mount.
        Assert.DoesNotContain("Shield Generator", offered);
        Assert.DoesNotContain("Shield Booster", offered);

        // And nothing twice. The id list spells one weapon both "AX Missile Rack" and "Ax
        // Missile Rack", which grouped exactly is one thing to choose between two of.
        var names = offered.Where(line => line.Length > 0).ToList();

        Assert.Equal(
            names.Distinct(StringComparer.OrdinalIgnoreCase).Count(),
            names.Distinct(StringComparer.Ordinal).Count());

        Pick(surface.Panel, "Pulse Laser").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();

        // Then which one of it, in the words a Commander uses rather than the code the outfitting
        // screen files it under (remediation.md 13, item 8).
        var variants = Offered(surface.Panel);

        Assert.Contains("Large, gimballed", variants);
        Assert.Contains("Small, fixed", variants);
        Assert.Contains("3E · 8.0 t · 0.92 MW · 140,600 cr", variants);

        // Nothing bigger than the slot, and the decline is still a real plan.
        Assert.DoesNotContain("Huge, fixed", variants);
        Assert.Contains("Any Pulse Laser — size and mount do not matter", variants);

        Pick(surface.Panel, "Large, gimballed").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();

        // Then the rolls that apply to a pulse laser, rather than every blueprint in the game.
        var rolls = Offered(surface.Panel);

        Assert.Contains("Lightweight Mount", rolls);
        Assert.DoesNotContain("Increased FSD Range", rolls);

        Pick(surface.Panel, "Lightweight Mount").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();

        // **No grade page at all** (remediation.md 15, item 4). It used to be asked here, and the
        // Commander's report was that 999 times out of 1000 the answer is 5 — measured, 155 of 160
        // module-and-blueprint pairs reach it. The grade is now taken as the highest the blueprint
        // offers and adjusted with a stepper on the slot page, where moving it re-costs the block
        // underneath it. So the flow goes straight from the roll to the effect.
        var grades = Text(surface.Panel).ToList();

        Assert.DoesNotContain("Grade 5", grades);
        Assert.DoesNotContain("Any grade", grades);

        // And then the experimental effects a pulse laser supports (remediation.md 13, item 10).
        var effects = Offered(surface.Panel);

        Assert.Contains("Concordant Sequence", effects);
        Assert.Contains("No effect", effects);

        Pick(surface.Panel, "No effect").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();

        var plan = surface.Ships.Store.Builds
            .SelectMany(build => build.Slots)
            .Single(slot => slot.Slot == "LargeHardpoint2");

        Assert.Equal("Pulse Laser", plan.Module);
        Assert.Equal("hpt_pulselaser_gimbal_large", plan.Variant);
        Assert.Equal("Lightweight Mount", plan.Blueprint);
        Assert.Equal(5, plan.Grade);
        Assert.Null(plan.Experimental);

        surface.Window.Close();
    }

    /// <summary>
    /// The grade reads last on the line, with its stepper next to it (remediation.md 17, item 11).
    /// <para>
    /// Reported as *"it is not clear that the step controls for the engineering grade are
    /// associated with it — put Grade last (rightmost) on the line, followed by the stepper"*. The
    /// number used to sit inside the sentence, with a blueprint name, an experimental effect and an
    /// engineer between it and the arrows at the end of the row.
    /// </para>
    /// <para>
    /// The order is the assertion, because everything here was already on screen before the fix —
    /// what was wrong was where. And the sentence is checked for <em>not</em> carrying the grade,
    /// so the two cannot both say it.
    /// </para>
    /// </summary>
    [AvaloniaFact]
    public void TheGradeReadsLastAndTheStepperFollowsIt()
    {
        var surface = Open();

        Row(surface.Panel, "Bad Idea (Python)").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();

        Row(surface.Panel, "Large Hardpoint 2").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();

        Press(surface.Panel, "Plan this slot").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();

        foreach (var choice in new[] { "Pulse Laser", "Large, gimballed", "Lightweight Mount", "No effect" })
        {
            Pick(surface.Panel, choice).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Dispatcher.UIThread.RunJobs();
        }

        var line = surface.Panel.GetVisualDescendants().OfType<StackPanel>()
            .First(panel => panel.Children.OfType<TextBlock>()
                .Any(block => block.Text == "Grade 5"));

        var order = line.Children.ToList();

        // The sentence, then the grade, then the two arrows — in that order and nothing between.
        var said = Assert.IsType<TextBlock>(order[0]);

        Assert.Contains("Lightweight Mount", said.Text ?? string.Empty, StringComparison.Ordinal);
        Assert.DoesNotContain("grade 5", said.Text ?? string.Empty, StringComparison.Ordinal);

        Assert.Equal("Grade 5", Assert.IsType<TextBlock>(order[1]).Text);
        Assert.Equal("▲", Assert.IsType<Button>(order[2]).Content);
        Assert.Equal("▼", Assert.IsType<Button>(order[3]).Content);

        surface.Window.Close();
    }

    /// <summary>
    /// The chooser narrows as the Commander types (remediation.md 12, item 5). A size 6
    /// compartment offers around forty things, which is a scroll hunt without it.
    /// </summary>
    [AvaloniaFact]
    public void TheChooserNarrowsAsYouType()
    {
        var surface = Open();

        Row(surface.Panel, "Bad Idea (Python)").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();

        Row(surface.Panel, "Compartment 1 (size 6)").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();

        Press(surface.Panel, "Plan this slot").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();

        Assert.Contains("Fuel Scoop", Offered(surface.Panel));
        Assert.Contains("Cargo Rack", Offered(surface.Panel));

        var box = surface.Panel.GetVisualDescendants().OfType<TextBox>()
            .Single(found => found.PlaceholderText == "Search");

        box.Text = "cargo";
        Dispatcher.UIThread.RunJobs();

        var narrowed = Offered(surface.Panel);

        Assert.Contains("Cargo Rack", narrowed);
        Assert.DoesNotContain("Fuel Scoop", narrowed);

        // And it is a filter rather than a spelling test: emptying it brings the list back.
        box.Text = string.Empty;
        Dispatcher.UIThread.RunJobs();

        Assert.Contains("Fuel Scoop", Offered(surface.Panel));

        surface.Window.Close();
    }

    /// <summary>
    /// A ship you are not flying says what it is and where it is (remediation.md 13, item 2).
    /// <para>
    /// The page carried one sentence about builds and a list of slots reading "not seen", which
    /// is less than the row you pressed to get there already told you.
    /// </para>
    /// </summary>
    [AvaloniaFact]
    public void AnOwnedShipShowsItsDetails()
    {
        var surface = Open();

        Row(surface.Panel, "Big Slow (Anaconda)").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();

        var shown = Text(surface.Panel);

        // Where it is, from the journal, and what it is worth.
        Assert.Contains("Parked at Jameson Memorial, Shinrarta Dezhra.", shown);
        Assert.Contains("Worth 150,000,000 cr.", shown);

        // And what the hull is, from the shipped table — true of every Anaconda ever built, which
        // is why it can be said for a ship in another dock.
        Assert.Contains("The ship", shown);
        Assert.Contains("Anaconda, by Faulcon DeLacy. Needs a large pad.", shown);
        Assert.Contains("180 m/s, 240 boosting.", shown);

        // What it cannot say, and why. A jump range for a ship d47 cannot see would have to be
        // modelled, and a modelled figure that reads like a measured one is the whole thing the
        // specification table is built the way it is to avoid.
        Assert.Contains(shown, line => line.Contains(
            "only known while you are in it", StringComparison.Ordinal));

        Assert.DoesNotContain("As it is fitted", shown);

        surface.Window.Close();
    }

    /// <summary>
    /// And the one you are flying adds the figures that depend on what is fitted, because those
    /// are the ones Elite actually reports (remediation.md 13, item 2).
    /// </summary>
    [AvaloniaFact]
    public void TheShipYouAreFlyingAddsWhatIsFitted()
    {
        var surface = Open();

        Row(surface.Panel, "Bad Idea (Python)").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();

        var shown = Text(surface.Panel);

        Assert.Contains("You are flying it.", shown);
        Assert.Contains("As it is fitted", shown);
        // 34.25 formats to 34.2, not 34.3: N1 rounds a midpoint to the even digit.
        Assert.Contains("34.2 ly a jump, full tank and empty hold.", shown);
        Assert.Contains("128 tonnes of hold.", shown);
        Assert.Contains("Worth 100,000,000 cr, ship and modules together.", shown);
        Assert.Contains("Rebuy is 5,000,000 cr.", shown);
        Assert.Contains("Hull at 87%.", shown);

        surface.Window.Close();
    }

    /// <summary>
    /// A long note beside a row does not squeeze the row's own name away
    /// (remediation.md 14, item 1).
    /// <para>
    /// Reported with a picture: the hardpoint carrying a plan was three hundred pixels tall, with
    /// "Large Hardpoint 1" wrapped one character per line down the left edge while the plan took
    /// the rest of the width. A right-docked child takes as much as it asks for, and what is left
    /// is what the name gets — which for a plan naming a module, a blueprint, a grade and an
    /// experimental effect was a few pixels.
    /// </para>
    /// </summary>
    [AvaloniaFact]
    public void ALongNoteDoesNotSqueezeTheRowsName()
    {
        var surface = Open();

        var build = surface.Ships.BuildFor(12, "python", "Bad Idea");

        surface.Ships.Plan(
            build.Id,
            new SlotPlan("LargeHardpoint1", "Overcharged Weapon", 5, Experimental: "Flow Control")
            {
                Module = "Pulse Laser",
                Variant = "hpt_pulselaser_gimbal_large",
            });

        Row(surface.Panel, "Bad Idea (Python)").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();

        var row = Row(surface.Panel, "Large Hardpoint 1");
        var name = row.GetVisualDescendants().OfType<TextBlock>()
            .First(block => block.Text == "Large Hardpoint 1");

        Assert.True(
            name.Bounds.Width >= 100,
            $"the row's name got {name.Bounds.Width:N0} pixels");

        // And the row stays a row rather than becoming a paragraph.
        Assert.True(row.Bounds.Height <= 80, $"the row is {row.Bounds.Height:N0} pixels tall");

        // Narrow enough that the note cannot fit beside the name on one line. It has to wrap
        // rather than be cut off mid-word, which is what an Auto column does to a wrapping block:
        // it measures as though it had forever, asks for its whole width, and is clipped to what
        // it is given.
        surface.Window.Width = 900;
        Dispatcher.UIThread.RunJobs();

        row = Row(surface.Panel, "Large Hardpoint 1");

        var note = row.GetVisualDescendants().OfType<TextBlock>()
            .First(block => block.Text is { } said && said.StartsWith("3E Pulse Laser", StringComparison.Ordinal));

        Assert.True(note.Bounds.Height > note.FontSize, "the note wrapped rather than being cut");
        Assert.True(
            note.Bounds.Width <= row.Bounds.Width,
            $"the note is {note.Bounds.Width:N0} pixels in a {row.Bounds.Width:N0} pixel row");

        Assert.True(
            Row(surface.Panel, "Large Hardpoint 1").GetVisualDescendants().OfType<TextBlock>()
                .First(block => block.Text == "Large Hardpoint 1").Bounds.Width >= 100,
            "and the name still has room at that width");

        surface.Window.Close();
    }

}
