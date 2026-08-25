using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Headless.XUnit;
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
/// The slot list is a table: what is in the slot, and what you wanted there
/// (docs/plans/change-requests.md 38).
/// <para>
/// <b>Asked for after an evening of the Loadout tab being read wrongly in three different
/// ways.</b> Every one of those readings came from the same place — one line carrying two facts
/// had to pick which to say, and each pick describes something that is not there. Two columns is
/// the answer that was unavailable while the row was an index.
/// </para>
/// <para>
/// <b>Driven through the drawn page.</b> The arithmetic being right and the page showing it are
/// two claims, and only one of them is what a Commander looks at — which is exactly how the
/// original defects got out.
/// </para>
/// </summary>
public class TheSlotListIsATableTests
{
    private sealed class Sitting
    {
        public CommanderGameState? State { get; set; }
    }

    private sealed record Surface(Window Window, PanelView Panel, ShipPlanService Ships);

    /// <summary>
    /// A Python with four things in it and nothing else — so every other slot on the hull is
    /// genuinely empty, which is the state the reports were made against.
    /// </summary>
    private static Surface Open()
    {
        var root = TempFolders.Create("d47-slot-table-tests");

        var checklists = new ChecklistService(
            new ChecklistStore(Path.Combine(root, "checklist.json"), NullLogger<ChecklistStore>.Instance),
            new ChecklistProposalStore(
                Path.Combine(root, "checklist-proposals.json"),
                NullLogger<ChecklistProposalStore>.Instance),
            () => null);

        var store = new GameStateStore();

        foreach (var line in new[]
                 {
                     """{"timestamp":"2026-08-25T09:00:00Z","event":"Commander","FID":"F1","Name":"Jameson"}""",
                     """{"timestamp":"2026-08-25T09:00:00Z","event":"Loadout","Ship":"python","ShipID":12,"ShipName":"Bad Idea","ShipIdent":"BI-01","HullValue":1,"ModulesValue":1,"Rebuy":1,"Modules":[{"Slot":"Slot01_Size6","Item":"int_hullreinforcement_size5_class2","On":true,"Priority":0,"Health":1.0,"Engineering":{"BlueprintName":"HullReinforcement_HeavyDuty","Level":5,"Quality":1.0,"Engineer":"Selene Jean","ExperimentalEffect":"special_armour_chunky","ExperimentalEffect_Localised":"Deep Plating"}},{"Slot":"PowerDistributor","Item":"int_powerdistributor_size7_class5","On":true,"Priority":0,"Health":1.0,"Engineering":{"BlueprintName":"PowerDistributor_PrioritySystems","Level":5,"Quality":1.0,"Engineer":"The Dweller"}},{"Slot":"MainEngines","Item":"int_engine_size5_class5","On":true,"Priority":0,"Health":1.0}]}""",
                 })
        {
            Assert.True(JournalEvent.TryParse(line, NullLogger.Instance, out var parsed));
            store.Apply(parsed!);
        }

        var sitting = new Sitting { State = store.Active };

        var ships = new ShipPlanService(
            new ShipBuildStore(Path.Combine(root, "ships.json"), NullLogger<ShipBuildStore>.Instance),
            checklists,
            () => sitting.State);

        var panel = new PanelView { DataContext = new PanelViewModel() };

        panel.EnableLoadout(ships, checklists, () => sitting.State);

        var window = new Window { Content = panel, Width = 1100, Height = 800 };

        window.Show();

        panel.Tab = PanelTab.Loadout;
        Dispatcher.UIThread.RunJobs();

        return new Surface(window, panel, ships);
    }

    private static ShipBuild Drill(Surface surface)
    {
        var build = surface.Ships.BuildFor(12, "python", "Bad Idea");

        return build;
    }

    private static void Board(Surface surface)
    {
        var ship = surface.Panel.GetVisualDescendants().OfType<Button>()
            .First(button => button.GetVisualDescendants().OfType<TextBlock>()
                .Any(text => text.Text is { } said && said.Contains("Bad Idea", StringComparison.Ordinal)));

        ship.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();
    }

    /// <summary>Every string the page draws, inlines flattened the way the eye reads them.</summary>
    private static IReadOnlyList<string> Drawn(PanelView panel) =>
        [.. panel.GetVisualDescendants().OfType<TextBlock>()
            .Select(block => block.Inlines is { Count: > 0 } inlines
                ? string.Concat(inlines.OfType<Run>().Select(run => run.Text))
                : block.Text ?? string.Empty)];

    /// <summary>One row of the table, by the slot it announces.</summary>
    private static Button Row(PanelView panel, string slot) =>
        panel.GetVisualDescendants().OfType<Button>()
            .First(button => AutomationProperties.GetName(button) == slot);

    private static IReadOnlyList<string> Cells(Button row) =>
        [.. row.GetVisualDescendants().OfType<TextBlock>()
            .Select(block => block.Inlines is { Count: > 0 } inlines
                ? string.Concat(inlines.OfType<Run>().Select(run => run.Text))
                : block.Text ?? string.Empty)];

    /// <summary>
    /// Two columns that are not labelled are two columns a Commander has to work out, so the list
    /// is headed once.
    /// </summary>
    [AvaloniaFact]
    public void TheListSaysWhichColumnIsWhich()
    {
        var surface = Open();

        Board(surface);

        var drawn = Drawn(surface.Panel);

        Assert.Contains("SLOT", drawn);
        Assert.Contains("CURRENT", drawn);
        Assert.Contains("PLAN", drawn);

        // Once, above the first group — not once per block of the outfitting screen.
        Assert.Equal(1, drawn.Count(said => said == "CURRENT"));

        surface.Window.Close();
    }

    /// <summary>
    /// <b>The first state, and the one that misled.</b> A plan for a slot Elite never mentioned is
    /// a plan for an empty slot: the Current column says the slot is empty and the Plan column
    /// names the module, and the two cannot be confused for one another.
    /// </summary>
    [AvaloniaFact]
    public void APlanForAnEmptySlotSaysEmptyOnOneSideAndTheModuleOnTheOther()
    {
        var surface = Open();

        surface.Ships.Plan(Drill(surface).Id, new SlotPlan("Slot02_Size6")
        {
            Module = "Shield Booster",
            Blueprint = "Heavy Duty",
            Grade = 5,
        });

        Board(surface);

        var cells = Cells(Row(surface.Panel, "Compartment 2 (size 6)"));

        Assert.Contains("empty", cells);
        Assert.Contains(cells, said => said.Contains("SB", StringComparison.Ordinal));

        // And never the two in one cell, which is what "something IS fitted on oxen utility mount
        // 8" was reading.
        Assert.DoesNotContain(
            cells,
            said => said.Contains("empty", StringComparison.Ordinal)
                    && said.Contains("SB", StringComparison.Ordinal));

        surface.Window.Close();
    }

    /// <summary>
    /// <b>Agreement collapses.</b> Slot01 holds a 5D hull reinforcement rolled Heavy Duty G5 with
    /// Deep Plating, and the plan asks for exactly that — so the second column is a tick and stops
    /// rather than repeating the words already in the first.
    /// </summary>
    [AvaloniaFact]
    public void WhereThePlanIsMetTheSecondColumnCollapses()
    {
        var surface = Open();

        surface.Ships.Plan(Drill(surface).Id, new SlotPlan("Slot01_Size6")
        {
            Module = "Hull Reinforcement Package",
            Variant = "int_hullreinforcement_size5_class2",
            Blueprint = "Heavy Duty Hull Reinforcement",
            Grade = 5,
            Experimental = "Deep Plating",
        });

        Board(surface);

        var row = Row(surface.Panel, "Compartment 1 (size 6)");
        var cells = Cells(row);

        Assert.Contains("✓", cells);

        // Said once. The plan's words are not repeated beside the hull's.
        Assert.Equal(1, cells.Count(said => said.Contains("Heavy Duty", StringComparison.Ordinal)));

        surface.Window.Close();
    }

    /// <summary>
    /// <b>The case a plan exists to catch</b>, and the one d47 was silent about: the right module,
    /// engineered to the right grade, with the wrong blueprint. Both rolls are on the row now,
    /// each in its own column.
    /// </summary>
    [AvaloniaFact]
    public void ARollThatDisagreesShowsBothRolls()
    {
        var surface = Open();

        surface.Ships.Plan(Drill(surface).Id, new SlotPlan("PowerDistributor")
        {
            Blueprint = "Weapon Focused",
            Grade = 5,
        });

        Board(surface);

        var cells = Cells(Row(surface.Panel, "Power Distributor"));

        Assert.Contains(cells, said => said.Contains("System Focused", StringComparison.Ordinal));
        Assert.Contains(cells, said => said.Contains("Weapon Focused", StringComparison.Ordinal));

        // Two cells, never one sentence made out of both.
        Assert.DoesNotContain(
            cells,
            said => said.Contains("System Focused", StringComparison.Ordinal)
                    && said.Contains("Weapon Focused", StringComparison.Ordinal));

        surface.Window.Close();
    }

    /// <summary>
    /// <b>The blueprint stops repeating the module.</b> A row already saying HRP says <i>Heavy
    /// Duty</i> beside it rather than <i>Heavy Duty Hull Reinforcement</i> — which is what makes
    /// "this whole ship is Heavy Duty" something an eye can see down the column.
    /// </summary>
    [AvaloniaFact]
    public void TheBlueprintDoesNotRepeatTheModule()
    {
        var surface = Open();

        Board(surface);

        var cells = Cells(Row(surface.Panel, "Compartment 1 (size 6)"));

        Assert.Contains(cells, said => said.Contains("Heavy Duty", StringComparison.Ordinal));
        Assert.DoesNotContain(
            cells,
            said => said.Contains("Heavy Duty Hull Reinforcement", StringComparison.Ordinal));

        surface.Window.Close();
    }

    /// <summary>
    /// <b>A short name is never the only name</b> (the Commander's ruling, 2026-08-25). The long
    /// form stays on the tooltip and the slot's own name is announced in full, so a Commander who
    /// does not know an abbreviation has somewhere to learn it without leaving the row.
    /// </summary>
    [AvaloniaFact]
    public void TheLongNameIsStillReachable()
    {
        var surface = Open();

        Board(surface);

        var row = Row(surface.Panel, "Compartment 1 (size 6)");

        var tips = row.GetVisualDescendants().OfType<TextBlock>()
            .Select(ToolTip.GetTip)
            .OfType<string>()
            .ToList();

        Assert.Contains(tips, tip => tip.Contains("Hull Reinforcement Package", StringComparison.Ordinal));

        // And the slot, which the column now says short as well.
        Assert.Contains("Compartment 1 (size 6)", tips);

        surface.Window.Close();
    }

    /// <summary>
    /// <b>Mini shows only the rows that disagree</b> (the Commander's ruling, 2026-08-25). Two
    /// columns do not fit 512 pixels thirty times over, and the question a Commander at a workshop
    /// is actually asking is what is still to do on this ship.
    /// </summary>
    [AvaloniaFact]
    public void MiniDrawsOnlyTheRowsWithWorkLeftInThem()
    {
        var surface = Open();

        surface.Ships.Plan(Drill(surface).Id, new SlotPlan("Slot02_Size6")
        {
            Module = "Shield Booster",
            Blueprint = "Heavy Duty",
            Grade = 5,
        });

        Board(surface);

        var full = surface.Panel.GetVisualDescendants().OfType<Button>()
            .Count(button => AutomationProperties.GetName(button) is { Length: > 0 } said
                             && said.StartsWith("Compartment", StringComparison.Ordinal));

        surface.Panel.Mode = PanelMode.Mini;
        Dispatcher.UIThread.RunJobs();

        var mini = surface.Panel.GetVisualDescendants().OfType<Button>()
            .Select(button => AutomationProperties.GetName(button))
            .OfType<string>()
            .Where(said => said.StartsWith("Compartment", StringComparison.Ordinal))
            .ToList();

        // The planned-and-empty one is the only compartment with anything outstanding.
        Assert.Equal(["Compartment 2 (size 6)"], mini);
        Assert.True(full > mini.Count, "Mini should be shorter than the full list, not the same.");

        // And it comes back when the window does.
        surface.Panel.Mode = PanelMode.Full;
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(
            full,
            surface.Panel.GetVisualDescendants().OfType<Button>()
                .Count(button => AutomationProperties.GetName(button) is { Length: > 0 } said
                                 && said.StartsWith("Compartment", StringComparison.Ordinal)));

        surface.Window.Close();
    }
}
