using D47.Core.Conversation;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Conversation;

/// <summary>
/// Cancelling is not silencing, and the difference is billed. Silencing stops the mouth and
/// leaves the model generating into a void that is still charged for; cancelling ends the work.
/// </summary>
public class TurnCancellationTests
{
    private static TurnCancellation New() => new(NullLogger<TurnCancellation>.Instance);

    [Fact]
    public void ThereIsNothingToCancelBeforeATurnStarts()
    {
        var cancellation = New();

        Assert.False(cancellation.InFlight);
        Assert.False(cancellation.Cancel());
    }

    [Fact]
    public void CancellingATurnInFlightTripsItsToken()
    {
        var cancellation = New();
        var source = cancellation.Begin(TestContext.Current.CancellationToken);

        Assert.True(cancellation.InFlight);
        Assert.True(cancellation.Cancel());
        Assert.True(source.Token.IsCancellationRequested);

        cancellation.End(source);
    }

    [Fact]
    public void EndingATurnReleasesTheSlot()
    {
        var cancellation = New();
        var source = cancellation.Begin(TestContext.Current.CancellationToken);
        cancellation.End(source);

        Assert.False(cancellation.InFlight);
        Assert.False(cancellation.Cancel());
    }

    /// <summary>
    /// Two turns talking over each other is not a state worth supporting, and letting the older
    /// one run is how a turn nobody is listening to keeps spending.
    /// </summary>
    [Fact]
    public void StartingASecondTurnCancelsTheFirst()
    {
        var cancellation = New();

        var first = cancellation.Begin(TestContext.Current.CancellationToken);
        var second = cancellation.Begin(TestContext.Current.CancellationToken);

        Assert.True(first.Token.IsCancellationRequested);
        Assert.False(second.Token.IsCancellationRequested);

        cancellation.End(second);
    }

    /// <summary>
    /// The identity check in End: a turn cancelled by a newer one must not release the newer
    /// one's slot as it unwinds, or "cancel" would find nothing to cancel a moment later.
    /// </summary>
    [Fact]
    public void AnOldTurnUnwindingDoesNotReleaseTheNewOnesSlot()
    {
        var cancellation = New();

        var first = cancellation.Begin(TestContext.Current.CancellationToken);
        var second = cancellation.Begin(TestContext.Current.CancellationToken);

        cancellation.End(first);

        Assert.True(cancellation.InFlight);
        Assert.True(cancellation.Cancel());
        Assert.True(second.Token.IsCancellationRequested);
    }

    [Fact]
    public void CancellingTwiceIsHarmless()
    {
        var cancellation = New();
        var source = cancellation.Begin(TestContext.Current.CancellationToken);

        Assert.True(cancellation.Cancel());
        Assert.True(cancellation.Cancel());

        cancellation.End(source);
    }

    /// <summary>
    /// The claim the whole component exists for: a cancelled turn stops asking the provider for
    /// more. Without the token reaching it, generation — and billing — continues past the point
    /// the Commander called it off.
    /// </summary>
    [Fact]
    public async Task ACancelledTurnStopsPullingFromTheProvider()
    {
        using var install = new TempInstall();
        var registry = TestSurface.For(install).Registry;
        var cancellation = New();
        var provider = new EndlessLlmProvider();

        var loop = new TurnLoop(
            registry,
            new KeywordRouter(registry),
            new LlmAvailabilityState(providerConfigured: true),
            new SpendTracker(),
            PriceTable.Default,
            NullLogger<TurnLoop>.Instance,
            provider,
            clock: new InstantClock());

        var source = cancellation.Begin(TestContext.Current.CancellationToken);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
        {
            await foreach (var turnEvent in loop.RunAsync("tell me about hyperspace physics", cancellationToken: source.Token))
            {
                // Cancel once it is genuinely under way, which is the real-world case: the
                // Commander hears it start and calls it off.
                if (turnEvent is TurnEvent.TextDelta)
                {
                    cancellation.Cancel();
                }
            }
        });

        var deliveredWhenCancelled = provider.Delivered;

        // The provider is infinite. If cancellation did not reach it, it would still be
        // producing; a bounded count is the proof that it stopped.
        await Task.Delay(50, TestContext.Current.CancellationToken);
        Assert.Equal(deliveredWhenCancelled, provider.Delivered);
        Assert.True(provider.Delivered < 1000, $"delivered {provider.Delivered} deltas before stopping");

        cancellation.End(source);
    }
}

/// <summary>Streams forever. The only way out is cancellation.</summary>
public sealed class EndlessLlmProvider : ILlmProvider
{
    private int _delivered;

    public int Delivered => Volatile.Read(ref _delivered);

    public string Id => "anthropic";

    public string DisplayName => "Endless";

    public string DefaultModel => "claude-opus-5";

    public LlmProviderCapabilities CapabilitiesFor(string model) => new()
    {
        SupportsPromptCaching = true,
        SupportsThinkingEffort = true,
        SupportsOperatorSystemMessages = true,
        MinimumCacheablePrefixTokens = 512,
    };

    public async IAsyncEnumerable<LlmStreamEvent> StreamAsync(
        LlmRequest request,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
    {
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Interlocked.Increment(ref _delivered);
            yield return new LlmStreamEvent.TextDelta("and another thing. ");
            await Task.Yield();
        }
    }
}
