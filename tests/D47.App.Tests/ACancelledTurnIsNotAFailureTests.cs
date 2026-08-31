using D47.App.Panel;
using Xunit;

namespace D47.App.Tests;

/// <summary>
/// A turn the Commander called off is not a turn that failed
/// (<a href="https://github.com/dseelinger/d47/issues/222">#222</a>).
/// <para>
/// Reported from a real session: pressing Cancel answered <em>"I couldn't answer that. The details
/// are on the Technical page."</em> — and there was nothing on the Technical page, because nothing
/// had gone wrong. Cancelling threw out of the await like anything else and landed in the same
/// catch as a bug.
/// </para>
/// <para>
/// The Commander's own words for what it should say: <c>[cancelled]</c>.
/// </para>
/// </summary>
public class ACancelledTurnIsNotAFailureTests
{
    [Fact]
    public void CancellingSaysCancelledAndNothingElse()
    {
        var ending = TurnEnding.For(new OperationCanceledException(), calledOff: true);

        Assert.Equal("\n[cancelled]", ending.Conversation);

        // And nothing for the Technical page, which is half the complaint: being sent to look for
        // a fault that does not exist is worse than being told nothing. It is also what keeps the
        // error out of the log — the caller writes both or neither.
        Assert.Null(ending.Technical);
    }

    /// <summary>A <see cref="TaskCanceledException"/> is one of these, and arrives as one.</summary>
    [Fact]
    public void TheTaskFlavourOfCancellationCountsToo()
    {
        var ending = TurnEnding.For(new TaskCanceledException(), calledOff: true);

        Assert.Equal("\n[cancelled]", ending.Conversation);
        Assert.Null(ending.Technical);
    }

    /// <summary>
    /// <b>The token decides, not the exception type.</b> A provider that abandons its own request
    /// throws the same type and <em>is</em> a failure — the Commander did not ask for it to stop,
    /// so telling them it was cancelled would be d47 blaming them for its own timeout.
    /// </summary>
    [Fact]
    public void ACancellationNobodyAskedForIsStillAFailure()
    {
        var ending = TurnEnding.For(new TaskCanceledException("the request timed out"), calledOff: false);

        Assert.Equal("\nI couldn't answer that. The details are on the Technical page.", ending.Conversation);
        Assert.Equal("\n[turn failed: the request timed out]", ending.Technical);
    }

    /// <summary>
    /// And a real fault still reads as one, with its message where somebody debugging will find
    /// it. This is the behaviour that was right all along and had to survive the change.
    /// </summary>
    [Fact]
    public void AThrownTurnStillReportsAndStillKeepsItsDetail()
    {
        var ending = TurnEnding.For(new InvalidOperationException("the calling thread"), calledOff: false);

        Assert.Equal("\nI couldn't answer that. The details are on the Technical page.", ending.Conversation);
        Assert.Equal("\n[turn failed: the calling thread]", ending.Technical);
    }

    /// <summary>
    /// A turn whose token was cancelled but which threw something else is a fault, not a cancel.
    /// The order of the two conditions is what says so, and it is worth pinning: a Commander who
    /// pressed Cancel while a bug was being thrown should still get the bug reported.
    /// </summary>
    [Fact]
    public void CancellingDoesNotSwallowABugThrownAtTheSameMoment()
    {
        var ending = TurnEnding.For(new InvalidOperationException("a real fault"), calledOff: true);

        Assert.Equal("\n[turn failed: a real fault]", ending.Technical);
    }
}
