using D47.App.Panel;
using D47.Core.Checklists;
using D47.Core.Journal;
using D47.Core.Ships;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.App.Tests;

/// <summary>
/// A modifier whose value is already a percentage is drawn as that value, not as a proportion
/// (<a href="https://github.com/dseelinger/d47/issues/53">#53</a>).
/// <para>
/// Reported from the Loadout tab as <c>+1485% KineticResistance</c> against a Heavy Duty G5 hull
/// reinforcement. The resistance had moved from 1.0% to 15.8% — <b>14.85 points</b> — and
/// dividing that by its near-1.0 base is what produced a number a hundred times too large.
/// </para>
/// <para>
/// The figures below are the Commander's own, off <c>Journal.2026-08-25T200908.01.log</c>, so
/// this fails on the exact input that was reported rather than on a constructed one.
/// </para>
/// </summary>
public class AResistanceIsShownAsItsValueTests
{
    /// <summary>
    /// The reported ship: a 2D hull reinforcement rolled Heavy Duty G5, with the three resistances
    /// Elite actually wrote and the two ratio quantities beside them.
    /// </summary>
    private static ShipsMode Flying()
    {
        var paths = new D47.Core.AppPaths(TempFolders.Create("d47-resistance-tests"));

        paths.EnsureCreated();

        var checklists = new ChecklistService(
            new ChecklistStore(Path.Combine(paths.Data, "checklist.json"), NullLogger<ChecklistStore>.Instance),
            new ChecklistProposalStore(
                Path.Combine(paths.Data, "checklist-proposals.json"),
                NullLogger<ChecklistProposalStore>.Instance),
            () => null);

        var store = new GameStateStore();

        foreach (var line in new[]
                 {
                     """{"timestamp":"2026-08-26T00:55:33Z","event":"Commander","FID":"F1","Name":"Jameson"}""",
                     """{"timestamp":"2026-08-26T00:55:33Z","event":"Loadout","Ship":"smallcombat01_nx","ShipID":53,"ShipName":"oxen","ShipIdent":"OX-1","HullValue":1,"ModulesValue":1,"Rebuy":1,"Modules":[{"Slot":"Slot04_Size2","Item":"int_hullreinforcement_size2_class2","On":true,"Priority":1,"Health":1.0,"Engineering":{"Engineer":"Selene Jean","EngineerID":300210,"BlueprintID":128673719,"BlueprintName":"HullReinforcement_HeavyDuty","Level":5,"Quality":1.0,"Modifiers":[{"Label":"Mass","Value":2.8,"OriginalValue":2.0,"LessIsGood":1},{"Label":"DefenceModifierHealthAddition","Value":326.800018,"OriginalValue":190.0,"LessIsGood":0},{"Label":"KineticResistance","Value":15.849996,"OriginalValue":0.999999,"LessIsGood":0},{"Label":"ThermicResistance","Value":-21.200001,"OriginalValue":-20.000004,"LessIsGood":0},{"Label":"ExplosiveResistance","Value":15.849996,"OriginalValue":0.0,"LessIsGood":0}]}}]}""",
                 })
        {
            Assert.True(JournalEvent.TryParse(line, NullLogger.Instance, out var parsed));
            store.Apply(parsed!);
        }

        var live = store.Active!;

        var ships = new ShipPlanService(
            new ShipBuildStore(Path.Combine(paths.Data, "ships.json"), NullLogger<ShipBuildStore>.Instance),
            checklists,
            () => live);

        ships.BuildFor(53, live.Ship!.Type!);

        return new ShipsMode(ships, checklists, () => live);
    }

    private static IReadOnlyList<string> Effects()
    {
        var mode = Flying();
        var ship = Assert.Single(mode.Items());

        var row = Assert.Single(
            mode.Slots(ship.Key),
            candidate => candidate.Key.EndsWith("|Slot04_Size2", StringComparison.Ordinal));

        Assert.NotNull(row.Parts?.Current);

        return row.Parts!.Current!.Effects;
    }

    [Fact]
    public void TheReportedNumberIsGone()
    {
        Assert.DoesNotContain(Effects(), effect => effect.Contains("1485", StringComparison.Ordinal));
    }

    [Fact]
    public void AResistanceReadsAsTheFigureTheGameShows()
    {
        // 15.849996 rounds to the +15.9% on the ship's own ring, which is the number a Commander
        // is comparing against.
        Assert.Contains("15.8% KineticResistance", Effects());
    }

    /// <summary>
    /// The case a proportion cannot express at all. Elite writes thermal resistance negative on
    /// this blueprint — measured across the corpus, routinely — and dividing a change by a
    /// negative base gives a figure whose sign says the opposite of what happened.
    /// </summary>
    [Fact]
    public void ANegativeResistanceKeepsItsSignAndItsMeaning()
    {
        Assert.Contains("-21.2% ThermicResistance", Effects());
    }

    /// <summary>
    /// And the one that used to vanish. A base of exactly 0 is excluded by the guard against
    /// dividing by it, so an explosive resistance rolled up from nothing was dropped from the list
    /// entirely rather than shown — the same defect wearing a different coat.
    /// </summary>
    [Fact]
    public void AResistanceThatStartedAtZeroIsStillShown()
    {
        Assert.Contains("15.8% ExplosiveResistance", Effects());
    }

    [Fact]
    public void AQuantityThatIsNotAPercentageIsStillAProportion()
    {
        // Mass 2.0 → 2.8 really is +40%, and that reading is the right one for it. The fix must
        // not turn every modifier into a bare value.
        Assert.Contains("+40% Mass", Effects());
    }

    /// <summary>
    /// The ordering is the other half of the defect. It ranks by magnitude to put the headline
    /// first, and a resistance scored at a hundred times its true size outranked every real change
    /// on the module for ever.
    /// </summary>
    [Fact]
    public void TheHeadlineIsTheChangeThatActuallyMovedMost()
    {
        var effects = Effects();

        // +72% on the health addition is the largest real move here; the resistances are ~14.85
        // points and mass is +40%.
        Assert.StartsWith("+72", effects[0], StringComparison.Ordinal);
        Assert.Contains("DefenceModifierHealthAddition", effects[0], StringComparison.Ordinal);
    }
}
