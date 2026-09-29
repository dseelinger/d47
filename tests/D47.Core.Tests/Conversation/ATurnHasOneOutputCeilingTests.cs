using D47.Core.Conversation;
using Xunit;

namespace D47.Core.Tests.Conversation;

/// <summary>The output ceiling is set by effort and shared by every round of a turn.</summary>
public sealed class ATurnHasOneOutputCeilingTests : IDisposable
{
    private readonly TempInstall _install = new();

    public void Dispose() => _install.Dispose();

    private TurnLoop Build(ILlmProvider provider, ThinkingEffort effort)
    {
        var registry = TestSurface.For(_install).Registry;

        var loop = new TurnLoop(
            registry,
            new KeywordRouter(registry),
            new LlmAvailabilityState(true),
            new SpendTracker(),
            PriceTable.Default,
            Microsoft.Extensions.Logging.Abstractions.NullLogger<TurnLoop>.Instance,
            provider,
            clock: new InstantClock())
        {
            EffortFloor = effort,
            EffortCeiling = effort,
            Retry = RetryPolicy.Default with { Attempts = 1 },
        };

        return loop;
    }

    private static async Task<TurnResult> RunAsync(TurnLoop loop)
    {
        TurnResult? result = null;

        await foreach (var turnEvent in loop.RunAsync(
                           "work out the best route to Colonia and explain why",
                           cancellationToken: TestContext.Current.CancellationToken))
        {
            if (turnEvent is TurnEvent.Completed completed)
            {
                result = completed.Result;
            }
        }

        Assert.NotNull(result);
        return result;
    }

    private static LlmStreamEvent[] AskingForATool(int outputTokens) =>
    [
        new LlmStreamEvent.ToolUse("call_1", "no_such_tool", "{}"),
        new LlmStreamEvent.Completed(new LlmUsage(100, outputTokens, 0, 0), LlmStopReason.ToolUse),
    ];

    private static LlmStreamEvent[] Answering(int outputTokens) =>
    [
        new LlmStreamEvent.TextDelta("Via Beagle Point."),
        new LlmStreamEvent.Completed(new LlmUsage(100, outputTokens, 0, 0), LlmStopReason.Completed),
    ];

    [Theory]
    [InlineData(ThinkingEffort.Low, 8192)]
    [InlineData(ThinkingEffort.Medium, 8192)]
    [InlineData(ThinkingEffort.High, 8192)]
    [InlineData(ThinkingEffort.Xhigh, 16384)]
    [InlineData(ThinkingEffort.Max, 16384)]
    public async Task TheFirstRoundCarriesTheCeilingForTheEffort(ThinkingEffort effort, int ceiling)
    {
        var provider = new RoundScriptedLlmProvider(Answering(50));

        await RunAsync(Build(provider, effort));

        Assert.Equal(ceiling, provider.Requests[0].MaxOutputTokens);
    }

    [Fact]
    public async Task ALaterRoundCarriesWhatTheEarlierRoundsLeft()
    {
        var provider = new RoundScriptedLlmProvider(AskingForATool(5000), Answering(50));

        await RunAsync(Build(provider, ThinkingEffort.Max));

        Assert.Equal(16384, provider.Requests[0].MaxOutputTokens);
        Assert.Equal(16384 - 5000, provider.Requests[1].MaxOutputTokens);
    }

    [Fact]
    public async Task ATurnWithTooLittleLeftSendsNoFurtherRequestAndSaysSo()
    {
        var provider = new RoundScriptedLlmProvider(AskingForATool(8192 - 1023), Answering(50));

        var result = await RunAsync(Build(provider, ThinkingEffort.High));

        Assert.Single(provider.Requests);
        Assert.Equal(TurnOutcome.Truncated, result.Outcome);
        Assert.EndsWith(TurnLoop.TruncatedLine, result.Text, StringComparison.Ordinal);
    }
}
