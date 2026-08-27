using D47.Core.Conversation;
using Microsoft.Extensions.Logging;
using Xunit;

namespace D47.Core.Tests.Conversation;

/// <summary>
/// A model that reasons before it answers still has room to answer
/// (<a href="https://github.com/dseelinger/d47/issues/97">#97</a>).
/// <para>
/// <b>Reasoning tokens are spent from the same ceiling as the answer.</b> Asked for 400 in total,
/// a reasoning model spends them deliberating, is truncated before it writes a word, and returns
/// empty content — which every caller here reads as "use the authored line". So the generated
/// lines stop appearing, with no error, no banner and nothing above Debug. It is indistinguishable
/// from a model that is merely dull, which means nobody reports it.
/// </para>
/// <para>
/// <b>Found while probing a local model and it has nothing to do with local models.</b>
/// <c>ChatCompletionsLlmProvider</c> already reads <c>reasoning_content</c>, and
/// <c>openaiCompatible</c> reaches gateways and OpenRouter — and Phase 54's "model for the quiet
/// calls" row exists precisely to point these calls at something cheap, which increasingly means
/// hybrid-reasoning.
/// </para>
/// </summary>
public class AThinkingModelStillGetsToAnswerTests
{
    private static Task<string?> AskAsync(FakeLlmProvider provider, ILogger? logger = null) =>
        FlavourTurn.AskAsync(
            provider,
            model: null,
            persona: "You are Warden.",
            aboutMe: null,
            instruction: "Make one short remark.",
            gameState: null,
            spend: null,
            prices: null,
            logger: logger,
            cancellationToken: TestContext.Current.CancellationToken);

    /// <summary>
    /// The ceiling covers thinking as well as the answer, so it has to be bigger than the answer.
    /// <para>
    /// Measured on the model that exposed this: <c>qwen3:4b</c> spent 524 tokens thinking before a
    /// ten-word answer, and repeated runs ranged 306 to 573. A ceiling of 400 cuts every one of
    /// those off mid-thought.
    /// </para>
    /// </summary>
    [Fact]
    public async Task TheCeilingLeavesRoomToThinkBeforeAnswering()
    {
        var provider = FakeLlmProvider.Answering("Quiet out here.");

        await AskAsync(provider);

        Assert.True(
            provider.LastRequest!.MaxOutputTokens > FlavourTurn.AnswerBudget,
            "the ceiling is the answer budget alone, so a model that thinks first never answers.");

        Assert.Equal(
            FlavourTurn.AnswerBudget + FlavourTurn.ReasoningHeadroom,
            provider.LastRequest!.MaxOutputTokens);

        // Enough for the worst reasoning actually observed, and then the answer on top of it.
        Assert.True(provider.LastRequest!.MaxOutputTokens >= 573 + FlavourTurn.AnswerBudget);
    }

    /// <summary>
    /// A caller that names its own ceiling still gets it — the adventure generator asks for a whole
    /// story in JSON and is the one site that does.
    /// </summary>
    [Fact]
    public async Task AndACallerThatNamesItsOwnCeilingKeepsIt()
    {
        var provider = FakeLlmProvider.Answering("{}");

        await FlavourTurn.AskAsync(
            provider,
            model: null,
            persona: null,
            aboutMe: null,
            instruction: "Write a story.",
            gameState: null,
            spend: null,
            prices: null,
            logger: null,
            cancellationToken: TestContext.Current.CancellationToken,
            maxOutputTokens: 9000);

        Assert.Equal(9000, provider.LastRequest!.MaxOutputTokens);
    }

    /// <summary>
    /// <b>Truncated and declined are different answers and used to be the same silence.</b> Both
    /// ended as null with nothing written down, so a model that ran out of budget mid-thought
    /// looked exactly like one with nothing to say. The stop reason was in hand the whole time and
    /// was read only for a refusal.
    /// </summary>
    [Fact]
    public async Task ATruncatedTurnSaysSoRatherThanVanishing()
    {
        var log = new CapturingLogger();

        var provider = new FakeLlmProvider(
            new LlmStreamEvent.Completed(LlmUsage.None, LlmStopReason.MaxTokens));

        var line = await AskAsync(provider, log);

        Assert.Null(line);

        Assert.Contains(
            log.Warnings,
            said => said.Contains("ceiling", StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// And a model with genuinely nothing to say stays quiet in the log too, because that is not a
    /// fault and a warning on every declined line would be noise nobody could act on.
    /// </summary>
    [Fact]
    public async Task AndADeclinedTurnDoesNotWarn()
    {
        var log = new CapturingLogger();

        var provider = new FakeLlmProvider(
            new LlmStreamEvent.Completed(LlmUsage.None, LlmStopReason.Refusal));

        Assert.Null(await AskAsync(provider, log));
        Assert.Empty(log.Warnings);
    }

    /// <summary>
    /// A model that thought and then answered inside the ceiling is the ordinary case and says
    /// nothing at all.
    /// </summary>
    [Fact]
    public async Task AndAnOrdinaryAnswerSaysNothing()
    {
        var log = new CapturingLogger();

        Assert.Equal("Quiet out here.", await AskAsync(FakeLlmProvider.Answering("Quiet out here."), log));
        Assert.Empty(log.Warnings);
    }

    private sealed class CapturingLogger : ILogger
    {
        public List<string> Warnings { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            if (logLevel >= LogLevel.Warning)
            {
                Warnings.Add(formatter(state, exception));
            }
        }
    }
}
