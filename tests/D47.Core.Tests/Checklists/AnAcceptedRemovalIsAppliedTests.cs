using D47.Core.Checklists;
using Xunit;

namespace D47.Core.Tests.Checklists;

/// <summary>Accepting a removal removes it.</summary>
public class AnAcceptedRemovalIsAppliedTests
{
    private static ChecklistService One(TempInstall install, string line)
    {
        var checklists = TestSurface.Checklists(install.Paths);

        checklists.AddNote(ChecklistScope.Universal, line);

        Assert.Single(checklists.Document.In(ChecklistScope.Universal));

        return checklists;
    }

    [Fact]
    public void TheLineIsGoneAfterAccepting()
    {
        using var install = new TempInstall();
        var checklists = One(install, "Unlock Lei Cheung");

        var proposed = checklists.ProposeChange("Unlock Lei Cheung", ProposalKind.Remove);

        Assert.Single(checklists.Proposals.Pending);

        var said = checklists.Accept();

        Assert.Empty(checklists.Document.In(ChecklistScope.Universal));
        Assert.Empty(checklists.Proposals.Pending);
        Assert.Contains("Removed", said, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>And it is gone from the file, not only from the copy in hand.</summary>
    [Fact]
    public void TheLineIsGoneFromTheFileToo()
    {
        using var install = new TempInstall();
        var checklists = One(install, "Unlock Lei Cheung");

        checklists.ProposeChange("Unlock Lei Cheung", ProposalKind.Remove);
        checklists.Accept();

        var reopened = TestSurface.Checklists(install.Paths);

        Assert.Empty(reopened.Document.In(ChecklistScope.Universal));
    }

    /// <summary>The failure is reported rather than hidden.</summary>
    [Fact]
    public void AcceptingARemovalOfSomethingGoneSaysSo()
    {
        using var install = new TempInstall();
        var checklists = One(install, "Unlock Lei Cheung");

        checklists.ProposeChange("Unlock Lei Cheung", ProposalKind.Remove);

        // Deleted by hand in between, which is the race a Commander with the panel open can run.
        checklists.Delete(checklists.Document.In(ChecklistScope.Universal)[0].Id);

        var said = checklists.Accept();

        Assert.DoesNotContain("Removed", said, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>The defect itself, and it was never in the removal — it was in what reaches it.</summary>
    [Theory]
    [InlineData("Accept.")]
    [InlineData("accept")]
    [InlineData("Accepted")]
    [InlineData("accept it")]
    [InlineData("accept that")]
    [InlineData("Accept the proposal.")]
    public void TheWordsAPersonActuallyUsesReachTheCommittingHalf(string said)
    {
        using var install = new TempInstall();
        var surface = TestSurface.For(install);

        Assert.Equal(
            "accept_proposal",
            surface.Router.MatchToolCommand(said)?.ToolName);
    }

    [Theory]
    [InlineData("Decline.")]
    [InlineData("decline")]
    [InlineData("declined")]
    [InlineData("decline it")]
    public void AndTheWordsForTheOtherAnswer(string said)
    {
        using var install = new TempInstall();
        var surface = TestSurface.For(install);

        Assert.Equal(
            "decline_proposal",
            surface.Router.MatchToolCommand(said)?.ToolName);
    }

    /// <summary>The conversational answers, which are only answers while there is a question.</summary>
    [Theory]
    [InlineData("yes")]
    [InlineData("go ahead")]
    [InlineData("do it")]
    [InlineData("confirm")]
    public void AConfirmationIsNotACommandUntilThereIsSomethingToConfirm(string said)
    {
        using var install = new TempInstall();
        var surface = TestSurface.For(install);

        Assert.Null(surface.Router.MatchToolCommand(said));

        surface.ChecklistService.AddNote(ChecklistScope.Universal, "Unlock Lei Cheung");
        surface.ChecklistService.ProposeChange("Unlock Lei Cheung", ProposalKind.Remove);

        Assert.Equal("accept_proposal", surface.Router.MatchToolCommand(said)?.ToolName);
    }

    [Theory]
    [InlineData("no")]
    [InlineData("forget it")]
    [InlineData("never mind")]
    public void AndTheSameForSayingNo(string said)
    {
        using var install = new TempInstall();
        var surface = TestSurface.For(install);

        Assert.Null(surface.Router.MatchToolCommand(said));

        surface.ChecklistService.AddNote(ChecklistScope.Universal, "Unlock Lei Cheung");
        surface.ChecklistService.ProposeChange("Unlock Lei Cheung", ProposalKind.Remove);

        Assert.Equal("decline_proposal", surface.Router.MatchToolCommand(said)?.ToolName);
    }

    /// <summary>
    /// And once it is answered they are ordinary words again, rather than a router that has silently
    /// kept "yes" for the rest of the session.
    /// </summary>
    [Fact]
    public void AnsweringGivesTheWordsBack()
    {
        using var install = new TempInstall();
        var surface = TestSurface.For(install);

        surface.ChecklistService.AddNote(ChecklistScope.Universal, "Unlock Lei Cheung");
        surface.ChecklistService.ProposeChange("Unlock Lei Cheung", ProposalKind.Remove);

        Assert.NotNull(surface.Router.MatchToolCommand("yes"));

        surface.ChecklistService.Accept();

        Assert.Null(surface.Router.MatchToolCommand("yes"));
    }

    /// <summary>
    /// What d47 says for itself while a proposal is unanswered, and what it stops saying once one is.
    /// </summary>
    [Fact]
    public void TheStandingLineNamesTheProposalAndThenStops()
    {
        using var install = new TempInstall();
        var checklists = One(install, "Unlock Lei Cheung");

        Assert.Null(checklists.Standing());

        checklists.ProposeChange("Unlock Lei Cheung", ProposalKind.Remove);

        var standing = checklists.Standing();

        Assert.NotNull(standing);
        Assert.Contains("Unlock Lei Cheung", standing, StringComparison.Ordinal);

        checklists.Accept();

        Assert.Null(checklists.Standing());
    }
}
