using D47.Core.Adventures;
using D47.Core.Conversation;
using D47.Core.Tests.Conversation;
using Microsoft.Extensions.Logging;
using Xunit;

namespace D47.Core.Tests.Adventures;

public sealed class AFailedStoryLogsWhichStepFailedAndWhyTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 22, 12, 0, 0, TimeSpan.Zero);

    private const string Spine = """
        {"name": "The Unrecoverable Column", "premise": "A ledger will not balance.", "want": "To find the missing freight.",
         "stake": "Whether a debt can be owed to nobody.", "turn": "The freight was never loaded.", "ending": "The column balances."}
        """;

    private const string UnclosedBeats = """{"opening": "Somebody is paying.", "beats": [{"title": "The Lantern" """;

    [Fact]
    public async Task AFailedSpineCallNamesTheStepTheModelAndTheProvidersMessage()
    {
        var provider = new RoundScriptedLlmProvider([new LlmStreamEvent.Failed("max_tokens: must be at most 8192", Transient: false)]);
        var log = new RecordingLogger();

        var outcome = await Write(provider, log);

        Assert.True(outcome.Internal);
        var warning = SingleWarning(log);
        Assert.Contains("spine step", warning);
        Assert.Contains("claude-opus-5", warning);
        Assert.Contains("Failed: max_tokens: must be at most 8192", warning);
        Assert.Matches(@"\d+\.\ds since the write began", warning);
    }

    [Fact]
    public async Task AFailedBeatsCallNamesTheBeatsStepNotTheSpine()
    {
        var provider = new RoundScriptedLlmProvider(
            RoundScriptedLlmProvider.Saying(Spine),
            [new LlmStreamEvent.Failed("The server is overloaded", Transient: true)]);
        var log = new RecordingLogger();

        var outcome = await Write(provider, log);

        Assert.True(outcome.Internal);
        var warning = SingleWarning(log);
        Assert.Contains("beats step", warning);
        Assert.DoesNotContain("spine step", warning);
        Assert.Contains("The server is overloaded", warning);
    }

    [Fact]
    public async Task ABeatsReplyWithAnUnclosedObjectLogsHowManyCharactersCameBack()
    {
        var provider = new RoundScriptedLlmProvider(
            RoundScriptedLlmProvider.Saying(Spine),
            RoundScriptedLlmProvider.Saying(UnclosedBeats));
        var log = new RecordingLogger();

        var outcome = await Write(provider, log);

        Assert.True(outcome.Internal);
        var warning = SingleWarning(log);
        Assert.Contains("beats step", warning);
        Assert.Contains($"{UnclosedBeats.Trim().Length} characters came back and could not be read", warning);
    }

    private static Task<AdventureOutcome> Write(ILlmProvider provider, ILogger log) =>
        AdventureGeneratorTests.Generator(provider, new AdventureGeneratorTests.Galaxy(), log)
            .GenerateAsync(new AdventureAsk(Length: AdventureLength.Short), Now, CancellationToken.None);

    private static string SingleWarning(RecordingLogger log) =>
        Assert.Single(log.Entries, entry => entry.Level == LogLevel.Warning).Message;
}
