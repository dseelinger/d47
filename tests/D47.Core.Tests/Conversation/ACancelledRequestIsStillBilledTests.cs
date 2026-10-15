using D47.Core.Conversation;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Conversation;

/// <summary>A request the provider has started is billed for its input, whether or not it completes.</summary>
public class ACancelledRequestIsStillBilledTests
{
    private static readonly LlmUsage AtStart = new(120, 0, 0, 2900);

    private static readonly LlmUsage AtEnd = new(120, 45, 0, 2900);

    private static TurnLoop Build(SpendTracker spend, ILlmProvider provider, ITurnClock clock, int attempts = 1)
    {
        var registry = TestSurface.For(new MemoryInstall()).Registry;

        return new TurnLoop(
            registry,
            new KeywordRouter(registry),
            new LlmAvailabilityState(true),
            spend,
            PriceTable.Default,
            NullLogger<TurnLoop>.Instance,
            provider,
            clock: clock)
        {
            Retry = new RetryPolicy { Attempts = attempts, AttemptTimeout = TimeSpan.FromSeconds(45) },
        };
    }

    private static async Task<TurnResult?> RunAsync(TurnLoop loop, CancellationToken cancellationToken)
    {
        TurnResult? result = null;

        await foreach (var turnEvent in loop.RunAsync("tell me about hyperspace physics", cancellationToken: cancellationToken))
        {
            if (turnEvent is TurnEvent.Completed completed)
            {
                result = completed.Result;
            }
        }

        return result;
    }

    private static Task<FlavourReply> AskForAsync(ILlmProvider provider, SpendTracker spend, CancellationToken cancellationToken) =>
        FlavourTurn.AskForAsync(
            provider,
            model: null,
            persona: null,
            aboutMe: null,
            instruction: "Say that again in other words.",
            gameState: null,
            spend,
            PriceTable.Default,
            logger: null,
            cancellationToken);

    [Fact]
    public async Task ARewordingCancelledAfterItStartedRecordsItsInput()
    {
        using var source = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        var spend = new SpendTracker();
        var provider = new StoppingLlmProvider(AtStart, AtEnd, stops: 1, stop: source.Cancel);

        var reply = await AskForAsync(provider, spend, source.Token);

        Assert.Equal(FlavourMiss.Cancelled, reply.Miss);
        Assert.Equal(1, spend.TurnCount);
        Assert.Equal(AtStart, spend.Last!.Usage);
    }

    [Fact]
    public async Task ARewordingThatCompletesIsRecordedOnceWithItsWholeUsage()
    {
        var spend = new SpendTracker();
        var provider = new StoppingLlmProvider(AtStart, AtEnd, stops: 0, stop: () => { });

        await AskForAsync(provider, spend, TestContext.Current.CancellationToken);

        Assert.Equal(1, spend.TurnCount);
        Assert.Equal(AtEnd, spend.Last!.Usage);
    }

    [Fact]
    public async Task ARewordingCancelledBeforeItStartedRecordsNothing()
    {
        using var source = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        var spend = new SpendTracker();
        var provider = new StoppingLlmProvider(AtStart, AtEnd, stops: 1, stop: source.Cancel) { StopsBeforeStart = true };

        var reply = await AskForAsync(provider, spend, source.Token);

        Assert.Equal(FlavourMiss.Cancelled, reply.Miss);
        Assert.Equal(0, spend.TurnCount);
    }

    [Fact]
    public async Task ATurnTheCommanderCancelsAfterItStartedRecordsItsInput()
    {
        using var source = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        var spend = new SpendTracker();
        var loop = Build(spend, new StoppingLlmProvider(AtStart, AtEnd, stops: 1, stop: source.Cancel), new InstantClock());

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => RunAsync(loop, source.Token));

        Assert.Equal(1, spend.TurnCount);
        Assert.Equal(AtStart, spend.Last!.Usage);
        Assert.Equal(0, spend.UnexplainedColdPrefixes);
    }

    /// <summary>
    /// A cancelled request that wrote cache is billed for it, and its warmth is not judged: the turn did not
    /// get far enough to read what it wrote.
    /// </summary>
    [Fact]
    public async Task ACancelledTurnThatWroteCacheIsNotAnUnexplainedColdPrefix()
    {
        using var source = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        var spend = new SpendTracker();
        var writing = new LlmUsage(120, 0, 2900, 0);
        var provider = new StoppingLlmProvider(writing, AtEnd, stops: 0, stop: source.Cancel);
        var loop = Build(spend, provider, new InstantClock());

        // The first turn is allowed a cold prefix; the second, on the same model, is not.
        await RunAsync(loop, source.Token);
        provider.Stops = provider.CallCount + 1;

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => RunAsync(loop, source.Token));

        Assert.Equal(2, spend.TurnCount);
        Assert.Equal(writing, spend.Last!.Usage);
        Assert.Equal(0, spend.UnexplainedColdPrefixes);
    }

    [Fact]
    public async Task ATurnThatCompletesIsRecordedOnceWithItsWholeUsage()
    {
        var spend = new SpendTracker();
        var loop = Build(spend, new StoppingLlmProvider(AtStart, AtEnd, stops: 0, stop: () => { }), new InstantClock());

        await RunAsync(loop, TestContext.Current.CancellationToken);

        Assert.Equal(1, spend.TurnCount);
        Assert.Equal(AtEnd, spend.Last!.Usage);
    }

    [Fact]
    public async Task ATurnCancelledBeforeItStartedRecordsNothing()
    {
        using var source = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        var spend = new SpendTracker();
        var provider = new StoppingLlmProvider(AtStart, AtEnd, stops: 1, stop: source.Cancel) { StopsBeforeStart = true };
        var loop = Build(spend, provider, new InstantClock());

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => RunAsync(loop, source.Token));

        Assert.Equal(0, spend.TurnCount);
    }

    [Fact]
    public async Task AnAttemptThatTimedOutAfterItStartedIsRecordedWithTheTurn()
    {
        var clock = new ManualTurnClock();
        var spend = new SpendTracker();
        var provider = new StoppingLlmProvider(AtStart, AtEnd, stops: 1, stop: () => clock.Advance(TimeSpan.FromSeconds(45)));
        var loop = Build(spend, provider, clock, attempts: 1);

        var result = await RunAsync(loop, TestContext.Current.CancellationToken);

        Assert.Equal(TurnOutcome.Failed, result!.Outcome);
        Assert.Equal(1, spend.TurnCount);
        Assert.Equal(AtStart, spend.Last!.Usage);
    }

    /// <summary>
    /// The retried request reads the cache the abandoned one wrote, so the turn is warm: the abandoned
    /// request's cache write is billed, and does not count as an unexplained cold prefix.
    /// </summary>
    [Fact]
    public async Task ARetryAfterATimeoutBillsBothRequestsAndJudgesWarmthByTheOneThatCompleted()
    {
        var clock = new ManualTurnClock();
        var spend = new SpendTracker();
        var writing = new LlmUsage(120, 0, 2900, 0);
        var provider = new StoppingLlmProvider(writing, AtEnd, stops: 0, stop: () => clock.Advance(TimeSpan.FromSeconds(45)));
        var loop = Build(spend, provider, clock, attempts: 2);

        // The first turn is allowed a cold prefix; the second, on the same model, is not.
        await RunAsync(loop, TestContext.Current.CancellationToken);
        provider.Stops = provider.CallCount + 1;

        var result = await RunAsync(loop, TestContext.Current.CancellationToken);

        Assert.Equal(TurnOutcome.Answered, result!.Outcome);
        Assert.Equal(3, provider.CallCount);
        Assert.Equal(2, spend.TurnCount);
        Assert.Equal(new LlmUsage(240, 45, 2900, 2900), spend.Last!.Usage);
        Assert.Equal(0, spend.UnexplainedColdPrefixes);
    }

    /// <summary>
    /// Yields <see cref="LlmStreamEvent.Started"/>, then on its first <c>stops</c> calls runs <c>stop</c>
    /// and throws on the cancelled token; on later calls it answers and completes.
    /// </summary>
    private sealed class StoppingLlmProvider(LlmUsage atStart, LlmUsage atEnd, int stops, Action stop) : ILlmProvider
    {
        public int Stops { get; set; } = stops;

        /// <summary>Stops before sending the start event.</summary>
        public bool StopsBeforeStart { get; init; }

        public int CallCount { get; private set; }

        public string Id => "anthropic";

        public string DisplayName => "Stopping";

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
            var stopping = CallCount++ < Stops;

            if (stopping && StopsBeforeStart)
            {
                stop();
                cancellationToken.ThrowIfCancellationRequested();
            }

            yield return new LlmStreamEvent.Started(stopping ? atStart : StartOf(atEnd));

            if (stopping)
            {
                stop();
                cancellationToken.ThrowIfCancellationRequested();
                throw new InvalidOperationException("The stop did not cancel the request.");
            }

            yield return new LlmStreamEvent.TextDelta("Hyperspace is a shortcut.");
            yield return new LlmStreamEvent.Completed(atEnd, LlmStopReason.Completed);
        }

        private static LlmUsage StartOf(LlmUsage usage) =>
            new(usage.InputTokens, 0, usage.CacheCreationInputTokens, usage.CacheReadInputTokens);
    }
}
