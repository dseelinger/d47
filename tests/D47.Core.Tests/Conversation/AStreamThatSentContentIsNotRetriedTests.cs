using D47.Core.Conversation;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Conversation;

/// <summary>An attempt that sent content is not resent, and d47's own timeout does not mark the model unavailable.</summary>
public class AStreamThatSentContentIsNotRetriedTests
{
    private static TurnLoop Build(
        TempInstall install,
        ILlmProvider provider,
        ITurnClock clock,
        RetryPolicy retry,
        LlmAvailabilityState availability)
    {
        var registry = TestSurface.For(install).Registry;

        return new TurnLoop(
            registry,
            new KeywordRouter(registry),
            availability,
            new SpendTracker(),
            PriceTable.Default,
            NullLogger<TurnLoop>.Instance,
            provider,
            clock: clock)
        {
            Retry = retry,
        };
    }

    private static async Task<TurnResult> RunAsync(TurnLoop loop)
    {
        TurnResult? result = null;

        await foreach (var turnEvent in loop.RunAsync(
                           "tell me about hyperspace physics", cancellationToken: TestContext.Current.CancellationToken))
        {
            if (turnEvent is TurnEvent.Completed completed)
            {
                result = completed.Result;
            }
        }

        return result!;
    }

    [Fact]
    public async Task AnAttemptThatThoughtThenFailedIsTriedOnce()
    {
        using var install = new TempInstall();
        var provider = new FakeLlmProvider(
            new LlmStreamEvent.ThinkingDelta("working"),
            new LlmStreamEvent.Failed("Connection reset.", Transient: true));
        var loop = Build(install, provider, new InstantClock(), new RetryPolicy { Attempts = 4 }, new LlmAvailabilityState(true));

        await RunAsync(loop);

        Assert.Equal(1, provider.CallCount);
    }

    [Fact]
    public async Task AnAttemptThatAskedForAToolThenFailedIsTriedOnce()
    {
        using var install = new TempInstall();
        var provider = new FakeLlmProvider(
            new LlmStreamEvent.ToolUse("t1", "get_status", "{}"),
            new LlmStreamEvent.Failed("Connection reset.", Transient: true));
        var loop = Build(install, provider, new InstantClock(), new RetryPolicy { Attempts = 4 }, new LlmAvailabilityState(true));

        await RunAsync(loop);

        Assert.Equal(1, provider.CallCount);
    }

    [Fact]
    public async Task AnAttemptThatFailedBeforeAnyEventIsStillRetried()
    {
        using var install = new TempInstall();
        var provider = new FakeLlmProvider(new LlmStreamEvent.Failed("Overloaded.", Transient: true));
        var loop = Build(install, provider, new InstantClock(), new RetryPolicy { Attempts = 4 }, new LlmAvailabilityState(true));

        await RunAsync(loop);

        Assert.Equal(4, provider.CallCount);
    }

    [Fact]
    public async Task TheTurnAfterATimeoutReachesTheModel()
    {
        using var install = new TempInstall();
        var clock = new ManualTurnClock();
        var availability = new LlmAvailabilityState(true);
        var provider = new StallingLlmProvider(clock);
        var loop = Build(
            install,
            provider,
            clock,
            new RetryPolicy { Attempts = 1, AttemptTimeout = TimeSpan.FromSeconds(45) },
            availability);

        var first = await RunAsync(loop);
        Assert.Equal(TurnOutcome.Failed, first.Outcome);
        Assert.Equal(LlmAvailability.Available, availability.Current);

        await RunAsync(loop);
        Assert.Equal(2, provider.CallCount);
    }

    private sealed class StallingLlmProvider(ManualTurnClock clock) : ILlmProvider
    {
        public int CallCount { get; private set; }

        public string Id => "anthropic";

        public string DisplayName => "Stalling";

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
            await Task.CompletedTask;
            CallCount++;
            clock.Advance(TimeSpan.FromSeconds(45));
            cancellationToken.ThrowIfCancellationRequested();
            yield return new LlmStreamEvent.TextDelta("never arrives");
        }
    }
}
