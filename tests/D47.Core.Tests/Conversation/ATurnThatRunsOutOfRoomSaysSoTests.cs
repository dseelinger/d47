using D47.Core.Conversation;
using Microsoft.Extensions.Logging;
using Xunit;
using static D47.Core.Tests.Conversation.TurnRuns;

namespace D47.Core.Tests.Conversation;

/// <summary>A conversation turn that reaches the output ceiling says so rather than ending mid-sentence or silent.</summary>
public sealed class ATurnThatRunsOutOfRoomSaysSoTests
{
    private readonly MemoryInstall _install = new();

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

    [Fact]
    public async Task AnAnswerCutOffMidSentenceEndsOnTheLineSayingSo()
    {
        var provider = new FakeLlmProvider(
            new LlmStreamEvent.TextDelta("The best route to Colonia runs through"),
            new LlmStreamEvent.Completed(LlmUsage.None, LlmStopReason.MaxTokens));

        var (result, text) = await RunWithTextAsync(Build(provider), "work out the best route to Colonia and explain why");

        Assert.Equal(TurnOutcome.Truncated, result.Outcome);
        Assert.Equal($"The best route to Colonia runs through {TurnLoop.TruncatedLine} {MoreRoom}", text);
        Assert.Equal(text, result.Text);
    }

    [Fact]
    public async Task ATurnThatSpentTheWholeCeilingThinkingIsNotSilent()
    {
        var provider = new FakeLlmProvider(
            new LlmStreamEvent.ThinkingDelta("First the jump range, then the neutron stars..."),
            new LlmStreamEvent.Completed(LlmUsage.None, LlmStopReason.MaxTokens));

        var (result, text) = await RunWithTextAsync(Build(provider), "work out the best route to Colonia and explain why");

        Assert.Equal(TurnOutcome.Truncated, result.Outcome);
        Assert.Equal($"{TurnLoop.TruncatedLine} {MoreRoom}", text);
    }

    private const string MoreRoom = "Ask again and say \"think carefully\", and I'll have more room.";

    private const string OnePart = "Ask for one part of it at a time.";

    private static FakeLlmProvider Truncated(bool takesEffort = true) =>
        new(
            new LlmStreamEvent.TextDelta("The best route"),
            new LlmStreamEvent.Completed(LlmUsage.None, LlmStopReason.MaxTokens))
        {
            ThinkingEffort = takesEffort,
        };

    [Fact]
    public async Task AModelWithNoEffortSettingIsToldToAskForOnePartAtATime()
    {
        var (result, text) = await RunWithTextAsync(Build(Truncated(takesEffort: false)), "work out the best route");

        Assert.Equal(TurnOutcome.Truncated, result.Outcome);
        Assert.EndsWith($"{TurnLoop.TruncatedLine} {OnePart}", text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ATurnHeldAtTheCeilingNamesTheCeilingSetting()
    {
        var loop = Build(Truncated());
        loop.EffortCeiling = D47.Core.Conversation.ThinkingEffort.High;

        var (result, text) = await RunWithTextAsync(loop, "think carefully about the best route");

        Assert.Equal(TurnOutcome.Truncated, result.Outcome);
        Assert.EndsWith(
            $"{TurnLoop.TruncatedLine} \"Never think harder than this\" is holding me at High.",
            text,
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task ATurnThatRanAtMaxIsToldToAskForOnePartAtATime()
    {
        var (result, text) = await RunWithTextAsync(Build(Truncated()), "think carefully about the best route");

        Assert.Equal(TurnOutcome.Truncated, result.Outcome);
        Assert.EndsWith($"{TurnLoop.TruncatedLine} {OnePart}", text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ATurnBelowMaxIsToldToSayThinkCarefully()
    {
        var loop = Build(Truncated());
        loop.EffortCeiling = D47.Core.Conversation.ThinkingEffort.Max;

        var (_, text) = await RunWithTextAsync(loop, "work out the best route");

        Assert.EndsWith($"{TurnLoop.TruncatedLine} {MoreRoom}", text, StringComparison.Ordinal);
    }

    [Fact]
    public void TheAdviceToSayThinkCarefullyRoutesToMax()
    {
        Assert.Equal(D47.Core.Conversation.ThinkingEffort.Max, EffortRouter.ChooseFor("think carefully"));
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

        await RunWithTextAsync(loop, "work out the best route to Colonia and explain why");
        await RunWithTextAsync(loop, "compose a sonnet about hyperspace");

        Assert.DoesNotContain(
            provider.Requests[1].Prompt.History,
            message => message.Role == ConversationRole.Assistant);
    }

    [Fact]
    public async Task TheLogRecordsWhereEachTurnStopped()
    {
        var log = new RecordingLogger<TurnLoop>();

        await RunWithTextAsync(Build(FakeLlmProvider.Answering("Quiet out here."), log), "anything to report");

        Assert.Contains(log.Lines, line => line.StartsWith("Model turn", StringComparison.Ordinal)
                                           && line.Contains("stopped on Completed", StringComparison.Ordinal));
    }
}
