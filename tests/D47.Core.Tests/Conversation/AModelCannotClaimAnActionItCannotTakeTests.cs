using D47.Core.Capabilities;
using D47.Core.Checklists;
using D47.Core.Conversation;
using Xunit;
using static D47.Core.Tests.Conversation.TurnRuns;

namespace D47.Core.Tests.Conversation;

/// <summary>The model said it had removed a checklist item, and it had not.</summary>
public class AModelCannotClaimAnActionItCannotTakeTests
{
    private static TurnLoop Build(TestSurface surface, string reply)
    {
        var loop = TurnLoops.Build(surface.Registry, FakeLlmProvider.Answering(reply));
        loop.Standing = surface.ChecklistService.Standing;
        return loop;
    }

    private static void Propose(TestSurface surface)
    {
        surface.ChecklistService.AddNote(ChecklistScope.Universal, "Unlock Lei Cheung");
        surface.ChecklistService.ProposeChange("Unlock Lei Cheung", ProposalKind.Remove);

        Assert.Single(surface.ChecklistService.Proposals.Pending);
    }

    [Fact]
    public async Task AClaimedRemovalIsFollowedByTheTruth()
    {
        var install = new MemoryInstall();
        var surface = TestSurface.For(install);

        Propose(surface);

        // Deliberately not routed: this is the path the utterance took when nothing matched it.
        var said = await SaidAsync(Build(surface, "Accepted. Removed from the list"), "get rid of it please");

        Assert.Contains("Still waiting on you", said, StringComparison.Ordinal);
        Assert.Contains("Unlock Lei Cheung", said, StringComparison.Ordinal);

        // And the claim was as false as it looked.
        Assert.Single(surface.ChecklistService.Document.In(ChecklistScope.Universal));
    }

    /// <summary>And it says nothing when there is nothing to say, which is what stops it being a nag.</summary>
    [Fact]
    public async Task NothingIsAddedWhenNothingIsWaiting()
    {
        var install = new MemoryInstall();
        var surface = TestSurface.For(install);

        var said = await SaidAsync(Build(surface, "Nothing on your list."), "anything outstanding");

        Assert.Equal("Nothing on your list.", said);
    }
}
