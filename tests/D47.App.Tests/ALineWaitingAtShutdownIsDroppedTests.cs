using D47.App.Voice;
using Xunit;

namespace D47.App.Tests;

/// <summary>
/// Unprompted speakers take the <see cref="SpeakingTurn"/> one at a time, and a line still waiting for it when d47
/// closes is dropped rather than thrown on the pool.
/// </summary>
public sealed class ALineWaitingAtShutdownIsDroppedTests
{
    private static readonly TimeSpan Patience = TimeSpan.FromSeconds(5);

    [Fact]
    public async Task TwoSpeakersStartedTogetherNeverSpeakAtOnce()
    {
        using var turn = new SpeakingTurn();
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var speaking = 0;
        var most = 0;
        var spoke = 0;

        async Task Speak()
        {
            var now = Interlocked.Increment(ref speaking);
            most = Math.Max(most, now);
            await release.Task.ConfigureAwait(false);
            Interlocked.Increment(ref spoke);
            Interlocked.Decrement(ref speaking);
        }

        var first = turn.TakeAsync(Speak);
        var second = turn.TakeAsync(Speak);

        Assert.Equal(1, Volatile.Read(ref speaking));

        release.SetResult();
        await Task.WhenAll(first, second).WaitAsync(Patience, TestContext.Current.CancellationToken);

        Assert.Equal(2, spoke);
        Assert.Equal(1, most);
    }

    [Fact]
    public async Task ASpeakerWaitingWhenTheTurnClosesIsDroppedWithoutThrowing()
    {
        var turn = new SpeakingTurn();
        var hold = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var secondSpoke = false;
        var thirdSpoke = false;

        var first = turn.TakeAsync(() => hold.Task);
        var second = turn.TakeAsync(() =>
        {
            secondSpoke = true;
            return Task.CompletedTask;
        });

        turn.Dispose();
        hold.SetResult();

        await first.WaitAsync(Patience, TestContext.Current.CancellationToken);
        await second.WaitAsync(Patience, TestContext.Current.CancellationToken);

        await turn.TakeAsync(() =>
        {
            thirdSpoke = true;
            return Task.CompletedTask;
        }).WaitAsync(Patience, TestContext.Current.CancellationToken);

        Assert.False(secondSpoke);
        Assert.False(thirdSpoke);
    }
}
