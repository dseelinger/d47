using D47.Core.Conversation;
using Microsoft.Extensions.Logging;
using Xunit;

namespace D47.Core.Tests.Conversation;

/// <summary>A conversation turn that reaches the output ceiling says so rather than ending mid-sentence or silent.</summary>
public sealed class ATurnThatRunsOutOfRoomSaysSoTests : IDisposable
{
    private readonly TempInstall _install = new();

    public void Dispose() => _install.Dispose();

    private TurnLoop Build(ILlmProvider provider, ILogger<TurnLoop>? logger = null)
    {
        var registry = TestSurface.For(_install).Registry;

        var loop = new TurnLoop(
            registry,
            new KeywordRouter(registry),
            new LlmAvailabilityState(true),
            new SpendTracker(),
            PriceTable.Default,
            logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<TurnLoop>.Instance,
            provider,
            clock: new InstantClock());

        loop.Retry = RetryPolicy.Default with { Attempts = 1 };
        return loop;
    }

    private static async Task<(TurnResult Result, string Text)> RunAsync(TurnLoop loop, string input)
    {
        var text = new System.Text.StringBuilder();
        TurnResult? result = null;

        await foreach (var turnEvent in loop.RunAsync(input, cancellationToken: TestContext.Current.CancellationToken))
        {
            switch (turnEvent)
            {
                case TurnEvent.TextDelta delta:
                    text.Append(delta.Text);
                    break;
                case TurnEvent.Completed completed:
                    result = completed.Result;
                    break;
            }
        }

        Assert.NotNull(result);
        return (result, text.ToString());
    }

    [Fact]
    public async Task AnAnswerCutOffMidSentenceEndsOnTheLineSayingSo()
    {
        var provider = new FakeLlmProvider(
            new LlmStreamEvent.TextDelta("The best route to Colonia runs through"),
            new LlmStreamEvent.Completed(LlmUsage.None, LlmStopReason.MaxTokens));

        var (result, text) = await RunAsync(Build(provider), "work out the best route to Colonia and explain why");

        Assert.Equal(TurnOutcome.Truncated, result.Outcome);
        Assert.Equal($"The best route to Colonia runs through {TurnLoop.TruncatedLine}", text);
        Assert.Equal(text, result.Text);
    }

    [Fact]
    public async Task ATurnThatSpentTheWholeCeilingThinkingIsNotSilent()
    {
        var provider = new FakeLlmProvider(
            new LlmStreamEvent.ThinkingDelta("First the jump range, then the neutron stars..."),
            new LlmStreamEvent.Completed(LlmUsage.None, LlmStopReason.MaxTokens));

        var (result, text) = await RunAsync(Build(provider), "work out the best route to Colonia and explain why");

        Assert.Equal(TurnOutcome.Truncated, result.Outcome);
        Assert.Equal(TurnLoop.TruncatedLine, text);
    }

    [Fact]
    public async Task ATruncatedAnswerIsNotCarriedIntoTheNextTurn()
    {
        var provider = new RoundScriptedLlmProvider(
            [
                new LlmStreamEvent.TextDelta("The best route to Colonia runs through"),
                new LlmStreamEvent.Completed(LlmUsage.None, LlmStopReason.MaxTokens),
            ],
            [
                new LlmStreamEvent.TextDelta("Ready."),
                new LlmStreamEvent.Completed(LlmUsage.None, LlmStopReason.Completed),
            ]);

        var loop = Build(provider);

        await RunAsync(loop, "work out the best route to Colonia and explain why");
        await RunAsync(loop, "compose a sonnet about hyperspace");

        Assert.DoesNotContain(
            provider.Requests[1].Prompt.History,
            message => message.Role == ConversationRole.Assistant);
    }

    [Fact]
    public async Task TheLogRecordsWhereEachTurnStopped()
    {
        var log = new CapturingLogger();

        await RunAsync(Build(FakeLlmProvider.Answering("Quiet out here."), log), "anything to report");

        Assert.Contains(log.Lines, line => line.StartsWith("Model turn", StringComparison.Ordinal)
                                           && line.Contains("stopped on Completed", StringComparison.Ordinal));
    }

    private sealed class CapturingLogger : ILogger<TurnLoop>
    {
        public List<string> Lines { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter) =>
            Lines.Add(formatter(state, exception));
    }
}
