using D47.Core.Adventures;
using D47.Core.Conversation;
using D47.Core.Tests.Conversation;
using D47.Core.Tests.Stories;
using Microsoft.Extensions.Logging;
using Xunit;

namespace D47.Core.Tests.Adventures;

/// <summary>
/// A lines reply that stops at a token limit, as Gemma 4 E4B in LM Studio at its default 8192-token context does:
/// 6084 prompt tokens and 2108 reply tokens fill the context long before the lines ceiling (#1240).
/// </summary>
public sealed class ACutOffStoryNamesTheLimitItHitTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 22, 12, 0, 0, TimeSpan.Zero);

    private const string CutOffLines = """{"name": "The Broker's Ledger", "objectives": [{"objective": 1, "title": "Part One", "lines": [{"text": "The ledger" """;

    [Fact]
    public async Task AReplyStoppedShortOfTheCeilingBlamesTheEndpointsContext()
    {
        var log = new RecordingLogger();

        var outcome = await Write(new LlmUsage(6084, 2108, 0, 0), log);

        Assert.True(outcome.Internal);
        var cutOff = Assert.Single(log.Entries, entry => entry.Level == LogLevel.Warning && entry.Message.Contains("short of the", StringComparison.Ordinal)).Message;
        Assert.Contains("6084 prompt and 2108 reply tokens", cutOff);
        Assert.Contains("the endpoint's context ran out", cutOff);
        Assert.DoesNotContain(log.Entries, entry => entry.Message.Contains("spent the whole", StringComparison.Ordinal));

        var notWritten = Assert.Single(log.Entries, entry => entry.Message.StartsWith("A story was not written", StringComparison.Ordinal)).Message;
        Assert.Contains("lines step", notWritten);
        Assert.Contains("cut off at the endpoint's context after 6084 prompt and 2108 reply tokens", notWritten);
    }

    [Fact]
    public async Task AReplyThatUsedTheWholeCeilingStillBlamesTheCeiling()
    {
        var log = new RecordingLogger();

        await Write(new LlmUsage(6084, AdventureGenerator.BeatsOutputCeiling, 0, 0), log);

        Assert.Contains(log.Entries, entry => entry.Message.Contains($"spent the whole {AdventureGenerator.BeatsOutputCeiling}-token ceiling", StringComparison.Ordinal));
        var notWritten = Assert.Single(log.Entries, entry => entry.Message.StartsWith("A story was not written", StringComparison.Ordinal)).Message;
        Assert.Contains($"cut off at the {AdventureGenerator.BeatsOutputCeiling}-token output ceiling", notWritten);
    }

    [Fact]
    public async Task AReplyStoppedAtTheContextTellsTheCommanderTheLengthToSet()
    {
        var outcome = await Write(new LlmUsage(6084, 2108, 0, 0), new RecordingLogger());

        Assert.Equal(
            "The model's context ran out. The prompt took 6084 tokens and the reply was cut off after 2108. "
            + AdventureOutcome.SetContextSentence(16384),
            outcome.Message);
    }

    [Theory]
    [InlineData(6061, 16384)]
    [InlineData(8384, 16384)]
    [InlineData(12000, 20480)]
    public void TheContextNeededIsThePromptPlusAReplyRoundedUp(int prompt, int expected) =>
        Assert.Equal(expected, AdventureOutcome.ContextNeeded(prompt));

    [Fact]
    public void WithoutAPromptCountTheContextNeededIsTheFloor() =>
        Assert.Equal(16384, AdventureOutcome.ContextNeeded(null));

    [Fact]
    public void TheContextSentenceNamesLmStudioAndOtherServers()
    {
        var sentence = AdventureOutcome.SetContextSentence(16384);

        Assert.Contains("16,384", sentence);
        Assert.Contains("LM Studio", sentence);
        Assert.Contains("Remember settings for", sentence);
        Assert.Contains("any other server", sentence);
    }

    [Fact]
    public async Task AReplyStoppedAtTheCeilingKeepsTheUsualMessage()
    {
        var outcome = await Write(new LlmUsage(6084, AdventureGenerator.BeatsOutputCeiling, 0, 0), new RecordingLogger());

        Assert.Equal(AdventureOutcome.NotWritten, outcome.Message);
    }

    private static async Task<AdventureOutcome> Write(LlmUsage linesUsage, ILogger log)
    {
        var ask = PlannedChapterWorld.Ask();
        var provider = new RoundScriptedLlmProvider([new LlmStreamEvent.TextDelta(CutOffLines), new LlmStreamEvent.Completed(linesUsage, LlmStopReason.MaxTokens)]);
        var state = PlannedChapterWorld.State();
        var galaxy = new PlannedChapterWorld.Galaxy();
        var generator = new AdventureGenerator(() => provider, () => null, () => "You are the ship.", () => "core", () => null, () => state, () => galaxy, () => null, null, null, log);

        return await generator.GenerateAsync(ask, Now, CancellationToken.None);
    }
}
