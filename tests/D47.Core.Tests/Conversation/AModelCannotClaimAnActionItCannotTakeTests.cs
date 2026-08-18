using D47.Core.Capabilities;
using D47.Core.Checklists;
using D47.Core.Conversation;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Conversation;

/// <summary>
/// The model said it had removed a checklist item, and it had not (remediation.md 10, item 10).
/// <para>
/// Four defences were already in place when that happened. <c>accept_proposal</c> is
/// <see cref="ToolDefinition.Protected"/>, so it is never advertised and is refused if the model
/// names it. The prompt says every turn that D47 cannot accept on the Commander's behalf. The
/// reply it was answering says "I cannot make this change myself". And the guardrails say never
/// to claim an action that was not taken. All four, and it still said "Accepted. Removed from
/// the list."
/// </para>
/// <para>
/// So the correction is not more words for the model to ignore. The turn loop asks the store what
/// is outstanding before the model speaks and again afterwards, and states the answer itself when
/// nothing has changed. Text the model writes cannot be trusted to describe what the model did.
/// </para>
/// </summary>
public class AModelCannotClaimAnActionItCannotTakeTests
{
    private static async Task<string> SaidAsync(TurnLoop loop, string input)
    {
        var text = new System.Text.StringBuilder();

        await foreach (var turnEvent in loop.RunAsync(input, cancellationToken: TestContext.Current.CancellationToken))
        {
            if (turnEvent is TurnEvent.TextDelta delta)
            {
                text.Append(delta.Text);
            }
        }

        return text.ToString();
    }

    private static TurnLoop Build(TestSurface surface, string reply)
    {
        var loop = new TurnLoop(
            surface.Registry,
            new KeywordRouter(surface.Registry),
            new LlmAvailabilityState(providerConfigured: true),
            new SpendTracker(),
            PriceTable.Default,
            NullLogger<TurnLoop>.Instance,
            FakeLlmProvider.Answering(reply),
            clock: new InstantClock())
        {
            Standing = surface.ChecklistService.Standing,
        };

        loop.Retry = RetryPolicy.Default with { Attempts = 1 };
        return loop;
    }

    private static void Propose(TestSurface surface)
    {
        surface.ChecklistService.AddNote(ChecklistScope.Universal, "Unlock Lei Cheung");
        surface.ChecklistService.ProposeChange("Unlock Lei Cheung", ProposalKind.Remove);

        Assert.Single(surface.ChecklistService.Proposals.Pending);
    }

    /// <summary>The reported turn, word for word.</summary>
    [Fact]
    public async Task AClaimedRemovalIsFollowedByTheTruth()
    {
        using var install = new TempInstall();
        var surface = TestSurface.For(install);

        Propose(surface);

        // Deliberately not routed: this is the path the utterance took when nothing matched it.
        var said = await SaidAsync(Build(surface, "Accepted. Removed from the list"), "get rid of it please");

        Assert.Contains("Still waiting on you", said, StringComparison.Ordinal);
        Assert.Contains("Unlock Lei Cheung", said, StringComparison.Ordinal);

        // And the claim was as false as it looked.
        Assert.Single(surface.ChecklistService.Document.In(ChecklistScope.Universal));
    }

    /// <summary>
    /// And it says nothing when there is nothing to say, which is what stops it being a nag. A
    /// line appended to every turn regardless would be one a Commander learns to read past, which
    /// is the same as not having it.
    /// </summary>
    [Fact]
    public async Task NothingIsAddedWhenNothingIsWaiting()
    {
        using var install = new TempInstall();
        var surface = TestSurface.For(install);

        var said = await SaidAsync(Build(surface, "Nothing on your list."), "anything outstanding");

        Assert.Equal("Nothing on your list.", said);
    }
}
