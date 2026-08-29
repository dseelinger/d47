using D47.Core.Checklists;
using Xunit;

namespace D47.Core.Tests.Checklists;

/// <summary>
/// What d47 says about a proposal nobody has answered yet (#154).
/// <para>
/// Reported verbatim, and the report ends with the reason it is a defect rather than a
/// preference:
/// </para>
/// <para>
/// <i>"Still waiting on you: Set the Cartage (Type-8 Transporter) plan's Armour, MainEngines,
/// LifeSupport, Radar, Slot05_Size5, Slot06_Size5 to Grade 5 Heavy Duty on Armour; Deep Plating on
/// Armour; Grade 5 Dirty Drive Tuning on M. Say "accept" or "decline"."</i> — <i>"This is freaking
/// annoying."</i>
/// </para>
/// <para>
/// Three defects in one sentence. It was appended after <em>every</em> turn, unchanged, until it
/// was answered; the sentence was cut mid-word by a cap that bounds a checklist <em>line</em>; and
/// what it said was a list mash with two raw journal slot names in it and no pairing between the
/// slots and the modifications.
/// </para>
/// </summary>
public class TheProposalNagDecaysAndFitsTests
{
    private const string Hull = "type8";

    /// <summary>The reported proposal: six slots on one ship's plan, three of them engineered.</summary>
    private static ChecklistService Waiting(TempInstall install)
    {
        var checklists = TestSurface.Checklists(install.Paths);
        var scope = new ChecklistScope(ChecklistGroup.Ship, "53");

        checklists.ProposePlan(
            scope,
            ChecklistSource.EngineeringPlan,
            EngineeringPlan.Items(
                scope,
                Hull,
                [
                    new BuildRequest("Armour", "Heavy Duty", 5, null),
                    new BuildRequest("Armour", "Deep Plating", null, null),
                    new BuildRequest("MainEngines", "Dirty Drive Tuning", 5, null),
                ],
                checklists.SlotFor),
            ["Armour", "MainEngines", "LifeSupport", "Radar", "Slot05_Size5", "Slot06_Size5"],
            "Cartage (Type-8 Transporter)");

        return checklists;
    }

    /// <summary>
    /// <b>Defect two.</b> A sentence asking for a decision must never be cut into a different
    /// sentence — "Dirty Drive Tuning on M." reads as a finished clause and is not one.
    /// </summary>
    [Fact]
    public void TheProposalIsNeverCutMidWord()
    {
        using var install = new TempInstall();
        var said = Waiting(install).Standing();

        Assert.NotNull(said);
        Assert.DoesNotContain("Tuning on M.", said, StringComparison.Ordinal);

        // Composed to fit rather than truncated to fit: nothing reached the backstop, so nothing
        // trails off.
        Assert.DoesNotContain('…', said!);
    }

    /// <summary>
    /// <b>Defect three.</b> <c>Slot05_Size5</c> is a journal field name and this is a spoken
    /// sentence. The count is what a decision needs; the manifest is what the panel is for.
    /// </summary>
    [Fact]
    public void NoRawJournalSlotNameIsReadOutLoud()
    {
        using var install = new TempInstall();
        var said = Waiting(install).Standing();

        Assert.NotNull(said);
        Assert.DoesNotContain("Slot05_Size5", said, StringComparison.Ordinal);
        Assert.DoesNotContain("Slot06_Size5", said, StringComparison.Ordinal);
        Assert.Contains("six slots", said, StringComparison.Ordinal);
        Assert.Contains("Cartage", said, StringComparison.Ordinal);
    }

    /// <summary>
    /// And a revision small enough to name its slots still names them, which is what makes this a
    /// composition rather than a cap: the count is the fallback, not the rule.
    /// </summary>
    [Fact]
    public void ASmallRevisionStillNamesWhatItChanges()
    {
        using var install = new TempInstall();
        var checklists = TestSurface.Checklists(install.Paths);
        var scope = new ChecklistScope(ChecklistGroup.Ship, "53");

        checklists.ProposePlan(
            scope,
            ChecklistSource.EngineeringPlan,
            EngineeringPlan.Items(
                scope,
                Hull,
                [new BuildRequest("MainEngines", "Dirty Drive Tuning", 5, null)],
                checklists.SlotFor),
            ["MainEngines"],
            "Cartage");

        var said = checklists.Standing();

        Assert.NotNull(said);
        Assert.Contains("MainEngines", said, StringComparison.Ordinal);
        Assert.Contains("Dirty Drive Tuning", said, StringComparison.Ordinal);
    }

    /// <summary>
    /// <b>Defect one, and the annoyance itself.</b> Said in full once, as a clause twice, then
    /// nothing — while the proposal stays exactly where it was.
    /// </summary>
    [Fact]
    public void TheSameProposalIsSaidInFullOnceThenDecaysThenGoesQuiet()
    {
        using var install = new TempInstall();
        var checklists = Waiting(install);

        var first = checklists.Standing();
        Assert.NotNull(first);
        Assert.Contains("Cartage", first, StringComparison.Ordinal);

        checklists.SaidStanding();

        var second = checklists.Standing();
        Assert.NotNull(second);
        Assert.DoesNotContain("Cartage", second, StringComparison.Ordinal);
        Assert.Contains("still waiting", second, StringComparison.OrdinalIgnoreCase);
        Assert.True(
            second!.Length < first!.Length,
            $"The clause should be shorter than the sentence it decayed from: \"{second}\"");

        checklists.SaidStanding();
        Assert.Equal(second, checklists.Standing());

        checklists.SaidStanding();
        Assert.Null(checklists.Standing());

        // Quiet is not gone. It is still waiting, and the panel still draws it.
        Assert.Single(checklists.Proposals.Pending);
    }

    /// <summary>
    /// Asking twice in one turn does not advance the decay. <see cref="TurnLoop"/> calls
    /// <see cref="ChecklistService.Standing"/> once before the model speaks and once after, and
    /// compares them — a line that counted its own repetitions would decay at double speed and
    /// would make those two calls disagree for no reason.
    /// </summary>
    [Fact]
    public void AskingDoesNotCount()
    {
        using var install = new TempInstall();
        var checklists = Waiting(install);

        var before = checklists.Standing();
        var after = checklists.Standing();

        Assert.Equal(before, after);
    }

    /// <summary>
    /// And a different question is owed its full sentence: accepting one of two leaves something
    /// nobody has heard about yet.
    /// </summary>
    [Fact]
    public void ANewProposalResetsTheDecay()
    {
        using var install = new TempInstall();
        var checklists = Waiting(install);

        checklists.SaidStanding();
        checklists.SaidStanding();
        checklists.SaidStanding();

        Assert.Null(checklists.Standing());

        checklists.AddNote(ChecklistScope.Universal, "buy limpets");
        checklists.ProposeChange("buy limpets", ProposalKind.Remove);

        var said = checklists.Standing();

        // The full form again, naming a proposal, rather than the clause it had decayed to.
        Assert.NotNull(said);
        Assert.Contains("Still waiting on you:", said, StringComparison.Ordinal);
        Assert.Contains("Cartage", said, StringComparison.Ordinal);
    }

    /// <summary>
    /// And the way out that does not require answering one at a time.
    /// </summary>
    [Fact]
    public void DecliningEverythingClearsTheQueue()
    {
        using var install = new TempInstall();
        var checklists = Waiting(install);

        checklists.AddNote(ChecklistScope.Universal, "buy limpets");
        checklists.ProposeChange("buy limpets", ProposalKind.Remove);

        Assert.Equal(2, checklists.Proposals.Pending.Count);

        checklists.Decline();

        Assert.Empty(checklists.Proposals.Pending);
        Assert.Null(checklists.Standing());
    }
}
