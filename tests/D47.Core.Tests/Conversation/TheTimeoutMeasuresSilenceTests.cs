using D47.Core.Conversation;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Conversation;

/// <summary>An attempt fails on time only when the model has sent nothing for the whole timeout.</summary>
public class TheTimeoutMeasuresSilenceTests
{
    private static readonly RetryPolicy OneTry = new()
    {
        Attempts = 1,
        AttemptTimeout = TimeSpan.FromSeconds(45),
    };

    [Fact]
    public async Task AnAttemptThatKeepsThinkingRunsPastTheTimeoutAndAnswers()
    {
        using var install = new TempInstall();
        var clock = new ManualTurnClock();

        // Two minutes of thinking, a delta every 30 seconds.
        var steps = Enumerable.Range(0, 4)
            .Select(_ => (TimeSpan.FromSeconds(30), (LlmStreamEvent)new LlmStreamEvent.ThinkingDelta("still working")))
            .Append((TimeSpan.FromSeconds(30), new LlmStreamEvent.TextDelta("Colonia is 22,000 light years out.")))
            .Append((TimeSpan.Zero, new LlmStreamEvent.Completed(LlmUsage.None, LlmStopReason.Completed)));

        var result = await RunAsync(install, new PacedLlmProvider(clock, steps), clock);

        Assert.Equal(TurnOutcome.Answered, result.Outcome);
        Assert.Equal("Colonia is 22,000 light years out.", result.Text);
        Assert.True(clock.Now > OneTry.AttemptTimeout);
    }

    [Fact]
    public async Task SilenceAfterEarlierEventsStillTimesOut()
    {
        using var install = new TempInstall();
        var clock = new ManualTurnClock();

        LlmStreamEvent thinking = new LlmStreamEvent.ThinkingDelta("still working");
        (TimeSpan, LlmStreamEvent)[] steps =
        [
            (TimeSpan.FromSeconds(10), thinking),
            (TimeSpan.FromSeconds(40), thinking),
            (TimeSpan.FromSeconds(45), new LlmStreamEvent.TextDelta("this never arrives")),
        ];

        var result = await RunAsync(install, new PacedLlmProvider(clock, steps), clock);

        Assert.Equal(TurnOutcome.Failed, result.Outcome);
        Assert.Equal("I couldn't reach the model just then. It sent nothing for 45 seconds.", result.Text);
    }

    [Fact]
    public async Task AnAttemptThatSendsNothingTimesOut()
    {
        using var install = new TempInstall();
        var clock = new ManualTurnClock();

        (TimeSpan, LlmStreamEvent)[] steps =
        [
            (TimeSpan.FromSeconds(45), new LlmStreamEvent.TextDelta("this never arrives")),
        ];

        var result = await RunAsync(install, new PacedLlmProvider(clock, steps), clock);

        Assert.Equal(TurnOutcome.Failed, result.Outcome);
        Assert.EndsWith("It sent nothing for 45 seconds.", result.Text, StringComparison.Ordinal);
    }

    private static async Task<TurnResult> RunAsync(TempInstall install, ILlmProvider provider, ManualTurnClock clock)
    {
        var registry = TestSurface.For(install).Registry;

        var loop = new TurnLoop(
            registry,
            new KeywordRouter(registry),
            new LlmAvailabilityState(providerConfigured: true),
            new SpendTracker(),
            PriceTable.Default,
            NullLogger<TurnLoop>.Instance,
            provider,
            clock: clock)
        {
            Retry = OneTry,
        };

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

    /// <summary>Lets the given time pass on the clock before each event, as a slow stream would.</summary>
    private sealed class PacedLlmProvider(ManualTurnClock clock, IEnumerable<(TimeSpan Before, LlmStreamEvent Event)> steps)
        : ILlmProvider
    {
        public string Id => "anthropic";

        public string DisplayName => "Paced";

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

            foreach (var (before, streamEvent) in steps)
            {
                clock.Advance(before);
                cancellationToken.ThrowIfCancellationRequested();
                yield return streamEvent;
            }
        }
    }
}
