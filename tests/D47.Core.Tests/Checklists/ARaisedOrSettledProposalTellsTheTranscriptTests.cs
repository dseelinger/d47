using D47.Core.Checklists;
using Xunit;

namespace D47.Core.Tests.Checklists;

/// <summary>
/// The two events the conversation's proposal card is built from — a proposal recorded, and one
/// answered — fire regardless of which tool or surface did it (#277).
/// </summary>
public class ARaisedOrSettledProposalTellsTheTranscriptTests
{
    [Fact]
    public void RecordingAProposalRaisesAddedWithItsAssignedId()
    {
        using var install = new TempInstall();
        var checklists = TestSurface.Checklists(install.Paths);

        ChecklistProposal? raised = null;
        checklists.Proposals.Added += proposal => raised = proposal;

        checklists.ProposeAdd(ChecklistScope.Universal, ["buy limpets"]);

        Assert.NotNull(raised);
        Assert.Equal(checklists.Proposals.Pending[0].Id, raised!.Id);
        Assert.Contains("buy limpets", raised.Summary, StringComparison.Ordinal);
    }

    [Fact]
    public void ARefusedDuplicateDoesNotRaiseAdded()
    {
        using var install = new TempInstall();
        var checklists = TestSurface.Checklists(install.Paths);

        checklists.ProposeAdd(ChecklistScope.Universal, ["buy limpets"]);

        var raised = 0;
        checklists.Proposals.Added += _ => raised++;

        checklists.ProposeAdd(ChecklistScope.Universal, ["buy limpets"]);

        Assert.Equal(0, raised);
    }

    [Fact]
    public void AcceptingSettlesItsOwnProposalWithItsOwnReport()
    {
        using var install = new TempInstall();
        var checklists = TestSurface.Checklists(install.Paths);

        checklists.ProposeAdd(ChecklistScope.Universal, ["buy limpets"]);
        var id = checklists.Proposals.Pending[0].Id;

        (string Id, bool Accepted, string Outcome)? settled = null;
        checklists.ProposalSettled += (proposalId, accepted, outcome) => settled = (proposalId, accepted, outcome);

        var said = checklists.Accept(id);

        Assert.NotNull(settled);
        Assert.Equal(id, settled!.Value.Id);
        Assert.True(settled.Value.Accepted);
        Assert.Equal(said, settled.Value.Outcome);
    }

    [Fact]
    public void DecliningSettlesEveryProposalItDroppedWithTheSameAggregateOutcome()
    {
        using var install = new TempInstall();
        var checklists = TestSurface.Checklists(install.Paths);

        checklists.ProposeAdd(ChecklistScope.Universal, ["buy limpets"]);
        checklists.ProposeAdd(ChecklistScope.Universal, ["fit a fuel scoop"]);

        var settled = new List<(string Id, bool Accepted, string Outcome)>();
        checklists.ProposalSettled += (id, accepted, outcome) => settled.Add((id, accepted, outcome));

        var said = checklists.Decline();

        Assert.Equal(2, settled.Count);
        Assert.All(settled, one => Assert.False(one.Accepted));
        Assert.All(settled, one => Assert.Equal(said, one.Outcome));
        Assert.Equal("Dropped 2 proposals.", said);
    }

    [Fact]
    public void AcceptingWithNothingWaitingSettlesNothing()
    {
        using var install = new TempInstall();
        var checklists = TestSurface.Checklists(install.Paths);

        var settled = 0;
        checklists.ProposalSettled += (_, _, _) => settled++;

        checklists.Accept();

        Assert.Equal(0, settled);
    }
}
