using D47.Core.Viewing;
using Xunit;

namespace D47.Core.Tests.Viewing;

/// <summary>A caption stretch waits while the Listener is busy and is cancelled and run again when it starts (#2677).</summary>
public class WritingCaptionsGivesWayToPushToTalkTests
{
    private static readonly TimeSpan Patience = TimeSpan.FromSeconds(10);

    [Fact]
    public async Task AStretchRunningWhenPushToTalkStartsIsCancelledAndWrittenAgainAfterwards()
    {
        var time = new ManualTime();
        var busy = false;
        var tokens = new List<CancellationToken>();
        var second = new TaskCompletionSource<int>();

        var running = GivesWayToListener.RunAsync(
            token =>
            {
                tokens.Add(token);

                if (tokens.Count == 1)
                {
                    var first = new TaskCompletionSource<int>();
                    token.Register(() => first.TrySetCanceled(token));
                    return first.Task;
                }

                return second.Task;
            },
            () => busy,
            time,
            CancellationToken.None);

        Assert.Single(tokens);

        busy = true;
        time.Advance(GivesWayToListener.CheckEvery);

        Assert.True(tokens[0].IsCancellationRequested);
        Assert.Single(tokens);

        busy = false;

        var deadline = DateTime.UtcNow + Patience;

        while (tokens.Count < 2 && DateTime.UtcNow < deadline)
        {
            time.Advance(GivesWayToListener.CheckEvery);
            await Task.Delay(1, TestContext.Current.CancellationToken);
        }

        Assert.Equal(2, tokens.Count);
        second.SetResult(7);
        Assert.Equal(7, await running);
    }

    [Fact]
    public async Task AStretchDoesNotStartWhileTheListenerIsBusy()
    {
        var time = new ManualTime();
        var busy = true;
        var started = 0;

        var running = GivesWayToListener.RunAsync(
            _ =>
            {
                started++;
                return Task.FromResult(1);
            },
            () => busy,
            time,
            CancellationToken.None);

        time.Advance(GivesWayToListener.CheckEvery * 4);

        Assert.Equal(0, started);

        busy = false;

        var deadline = DateTime.UtcNow + Patience;

        while (!running.IsCompleted && DateTime.UtcNow < deadline)
        {
            time.Advance(GivesWayToListener.CheckEvery);
            await Task.Delay(1, TestContext.Current.CancellationToken);
        }

        Assert.Equal(1, await running);
        Assert.Equal(1, started);
    }

    [Fact]
    public async Task StoppingTheWriterIsNotGivingWay()
    {
        var time = new ManualTime();
        using var stop = new CancellationTokenSource();

        var running = GivesWayToListener.RunAsync(
            async token =>
            {
                await Task.Delay(Timeout.Infinite, token);
                return 0;
            },
            () => false,
            time,
            stop.Token);

        await stop.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => running);
    }
}
