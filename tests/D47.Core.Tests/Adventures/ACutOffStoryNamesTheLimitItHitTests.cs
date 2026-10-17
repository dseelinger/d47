using D47.Core.Adventures;
using D47.Core.Conversation;
using D47.Core.Tests.Conversation;
using Microsoft.Extensions.Logging;
using Xunit;

namespace D47.Core.Tests.Adventures;

/// <summary>
/// A beats reply that stops at a token limit, as Gemma 4 E4B in LM Studio at its default 8192-token context does:
/// 6084 prompt tokens and 2108 reply tokens fill the context long before the 16000-token ceiling (#1240).
/// </summary>
public sealed class ACutOffStoryNamesTheLimitItHitTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 22, 12, 0, 0, TimeSpan.Zero);

    private const string Spine = """
        {"name": "The Unrecoverable Column", "premise": "A ledger will not balance.", "want": "To find the missing freight.",
         "stake": "Whether a debt can be owed to nobody.", "turn": "The freight was never loaded.", "ending": "The column balances."}
        """;

    private const string CutOffBeats = """{"opening": "Somebody is paying.", "beats": [{"title": "The Lantern", "objective": {"kind": "dock" """;

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
        Assert.Contains("beats step", notWritten);
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

    private static Task<AdventureOutcome> Write(LlmUsage beatsUsage, ILogger log)
    {
        var provider = new RoundScriptedLlmProvider(
            RoundScriptedLlmProvider.Saying(Spine),
            [new LlmStreamEvent.TextDelta(CutOffBeats), new LlmStreamEvent.Completed(beatsUsage, LlmStopReason.MaxTokens)]);

        return AdventureGeneratorTests.Generator(provider, new AdventureGeneratorTests.Galaxy(), log)
            .GenerateAsync(new AdventureAsk(Length: AdventureLength.Short), Now, CancellationToken.None);
    }
}
