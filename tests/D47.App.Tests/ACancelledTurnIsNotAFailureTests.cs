using D47.App.Panel;
using Xunit;

namespace D47.App.Tests;

/// <summary>A turn the Commander called off is not a turn that failed.</summary>
public class ACancelledTurnIsNotAFailureTests
{
    [Fact]
    public void CancellingSaysCancelledAndNothingElse()
    {
        var ending = TurnEnding.For(new OperationCanceledException(), calledOff: true);

        Assert.Equal("\n[cancelled]", ending.Conversation);

        // And nothing to record, which is half the complaint: being sent to look for a fault that does not
        // exist is worse than being told nothing.
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

    /// <summary>The token decides, not the exception type.</summary>
    [Fact]
    public void ACancellationNobodyAskedForIsStillAFailure()
    {
        var ending = TurnEnding.For(new TaskCanceledException("the request timed out"), calledOff: false);

        Assert.Equal("\nI couldn't answer that. The details are on the Log File reading.", ending.Conversation);
        Assert.Equal("\n[response failed: the request timed out]", ending.Technical);
    }

    /// <summary>
    /// And a real fault still reads as one, with its message where somebody debugging will find it.
    /// </summary>
    [Fact]
    public void AThrownTurnStillReportsAndStillKeepsItsDetail()
    {
        var ending = TurnEnding.For(new InvalidOperationException("the calling thread"), calledOff: false);

        Assert.Equal("\nI couldn't answer that. The details are on the Log File reading.", ending.Conversation);
        Assert.Equal("\n[response failed: the calling thread]", ending.Technical);
    }

    /// <summary>
    /// A turn whose token was cancelled but which threw something else is a fault, not a cancel.
    /// </summary>
    [Fact]
    public void CancellingDoesNotSwallowABugThrownAtTheSameMoment()
    {
        var ending = TurnEnding.For(new InvalidOperationException("a real fault"), calledOff: true);

        Assert.Equal("\n[response failed: a real fault]", ending.Technical);
    }
}
