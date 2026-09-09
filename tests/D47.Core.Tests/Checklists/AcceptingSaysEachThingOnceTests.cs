using D47.Core.Checklists;
using Xunit;

namespace D47.Core.Tests.Checklists;

public class AcceptingSaysEachThingOnceTests
{
    private static ChecklistService Waiting(TempInstall install, params string[] lines)
    {
        var checklists = TestSurface.Checklists(install.Paths);

        foreach (var line in lines)
        {
            checklists.AddNote(ChecklistScope.Universal, line);
            checklists.ProposeChange(line, ProposalKind.Remove);
        }

        return checklists;
    }

    /// <summary>Two proposals whose items are both gone.</summary>
    [Fact]
    public void OneOutcomeIsSaidOnceHoweverManyProposalsProducedIt()
    {
        using var install = new TempInstall();
        var checklists = Waiting(install, "buy limpets", "fit a fuel scoop");

        Assert.Equal(2, checklists.Proposals.Pending.Count);

        // Both gone by hand in between, which is the race a Commander with the panel open runs.
        foreach (var item in checklists.Document.In(ChecklistScope.Universal).ToList())
        {
            checklists.Delete(item.Id);
        }

        var said = checklists.Accept();

        Assert.Equal("There is no such item on your checklist.", said);
    }

    /// <summary>
    /// And two different outcomes are still both said — collapsing is about repetition, not about
    /// brevity.
    /// </summary>
    [Fact]
    public void TwoDifferentOutcomesAreBothReported()
    {
        using var install = new TempInstall();
        var checklists = Waiting(install, "buy limpets", "fit a fuel scoop");

        // One of them gone, the other still there.
        checklists.Delete(checklists.Document.In(ChecklistScope.Universal)[0].Id);

        var said = checklists.Accept();

        Assert.Contains("There is no such item on your checklist.", said, StringComparison.Ordinal);
        Assert.Contains("fit a fuel scoop", said, StringComparison.Ordinal);
    }

    /// <summary>
    /// The other half, and the reason there were two proposals to repeat: asking for the same change
    /// twice recorded it twice.
    /// </summary>
    [Fact]
    public void TheSameProposalIsNotRecordedTwice()
    {
        using var install = new TempInstall();
        var checklists = TestSurface.Checklists(install.Paths);

        checklists.AddNote(ChecklistScope.Universal, "buy limpets");

        var first = checklists.ProposeChange("buy limpets", ProposalKind.Remove);
        var again = checklists.ProposeChange("buy limpets", ProposalKind.Remove);

        Assert.Single(checklists.Proposals.Pending);

        // And it says so rather than pretending to have recorded a second one.
        Assert.Contains("already", again, StringComparison.OrdinalIgnoreCase);
        Assert.NotEqual(first, again);
    }

    /// <summary>Two proposals about different things are not duplicates, however alike they look.</summary>
    [Fact]
    public void TwoProposalsAboutDifferentLinesBothStand()
    {
        using var install = new TempInstall();
        var checklists = Waiting(install, "buy limpets", "fit a fuel scoop");

        Assert.Equal(2, checklists.Proposals.Pending.Count);
    }

    /// <summary>
    /// And nor are two different acts on one line: proposing to finish something and proposing to drop
    /// it are opposite requests about the same words.
    /// </summary>
    [Fact]
    public void CompletingAndRemovingTheSameLineAreDifferentProposals()
    {
        using var install = new TempInstall();
        var checklists = TestSurface.Checklists(install.Paths);

        checklists.AddNote(ChecklistScope.Universal, "buy limpets");
        checklists.ProposeChange("buy limpets", ProposalKind.Complete);
        checklists.ProposeChange("buy limpets", ProposalKind.Remove);

        Assert.Equal(2, checklists.Proposals.Pending.Count);
    }
}
