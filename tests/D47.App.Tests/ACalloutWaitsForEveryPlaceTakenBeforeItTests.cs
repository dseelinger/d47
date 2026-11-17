using D47.App.Voice;
using Xunit;

namespace D47.App.Tests;

/// <summary>A place in the <see cref="CalloutOrder"/> is ready only once every place taken before it is disposed.</summary>
public sealed class ACalloutWaitsForEveryPlaceTakenBeforeItTests
{
    private static readonly TimeSpan Patience = TimeSpan.FromSeconds(5);

    [Fact]
    public async Task TheSecondPlaceWaitsForTheFirstEvenWhenItsHolderIsReadyFirst()
    {
        using var order = new CalloutOrder();
        var first = order.Take();
        var second = order.Take();
        var spoken = new List<string>();
        var firstWritten = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        async Task Hold(CalloutOrder.Place place, string line, Task written)
        {
            using (place)
            {
                await written.ConfigureAwait(false);
                await place.Ready.ConfigureAwait(false);

                lock (spoken)
                {
                    spoken.Add(line);
                }
            }
        }

        var firstHolder = Hold(first, "first", firstWritten.Task);
        var secondHolder = Hold(second, "second", Task.CompletedTask);

        await first.Ready.WaitAsync(Patience, TestContext.Current.CancellationToken);
        Assert.False(second.Ready.IsCompleted);
        Assert.Empty(spoken);

        firstWritten.SetResult();
        await Task.WhenAll(firstHolder, secondHolder).WaitAsync(Patience, TestContext.Current.CancellationToken);

        Assert.Equal(["first", "second"], spoken);
    }

    [Fact]
    public async Task APlaceWhoseHolderThrewStillReleasesTheNext()
    {
        using var order = new CalloutOrder();
        var first = order.Take();
        var second = order.Take();

        async Task Throwing()
        {
            using (first)
            {
                await Task.Yield();
                throw new InvalidOperationException("The write failed.");
            }
        }

        await Assert.ThrowsAsync<InvalidOperationException>(Throwing);

        await second.Ready.WaitAsync(Patience, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task ALaterPlaceDisposedFirstDoesNotLetTheNextOvertakeAnEarlierOne()
    {
        using var order = new CalloutOrder();
        var first = order.Take();
        var second = order.Take();
        var third = order.Take();

        second.Dispose();
        await Task.Delay(50, TestContext.Current.CancellationToken);

        Assert.False(third.Ready.IsCompleted);

        first.Dispose();
        await third.Ready.WaitAsync(Patience, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task DisposingTheOrderCancelsEveryPlaceStillWaitingAndNothingWaitingIsSpoken()
    {
        var order = new CalloutOrder();
        var first = order.Take();
        var second = order.Take();
        var third = order.Take();
        var spoke = 0;

        async Task Hold(CalloutOrder.Place place)
        {
            using (place)
            {
                try
                {
                    await place.Ready.ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    return;
                }

                Interlocked.Increment(ref spoke);
            }
        }

        var waiting = Task.WhenAll(Hold(second), Hold(third));

        order.Dispose();
        await waiting.WaitAsync(Patience, TestContext.Current.CancellationToken);

        Assert.True(second.Ready.IsCanceled);
        Assert.True(third.Ready.IsCanceled);
        Assert.True(order.Take().Ready.IsCanceled);
        Assert.Equal(0, spoke);

        first.Dispose();
        order.Dispose();
    }
}
