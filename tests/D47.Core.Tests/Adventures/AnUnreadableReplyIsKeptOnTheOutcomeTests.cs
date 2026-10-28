using D47.Core.Adventures;
using D47.Core.Conversation;
using D47.Core.Tests.Conversation;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Adventures;

public sealed class AnUnreadableReplyIsKeptOnTheOutcomeTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 22, 12, 0, 0, TimeSpan.Zero);

    private const string Spine = """
        {"name": "The Unrecoverable Column", "premise": "A ledger will not balance.", "want": "To find the missing freight.",
         "stake": "Whether a debt can be owed to nobody.", "turn": "The freight was never loaded.", "ending": "The column balances."}
        """;

    private const string NotJson = "I would rather ask which system you want this to start in.";

    [Fact]
    public async Task ABeatsReplyThatIsNotJsonIsCarriedWithItsStep()
    {
        var provider = new RoundScriptedLlmProvider(
            RoundScriptedLlmProvider.Saying(Spine),
            RoundScriptedLlmProvider.Saying(NotJson));

        var outcome = await Write(provider);

        Assert.False(outcome.Succeeded);
        Assert.Equal(NotJson, outcome.UnreadReply);
        Assert.Equal("beats", outcome.UnreadStep);
    }

    [Fact]
    public async Task NothingComingBackLeavesNoUnreadReply()
    {
        var provider = new RoundScriptedLlmProvider([new LlmStreamEvent.Failed("The server is overloaded", Transient: false)]);

        var outcome = await Write(provider);

        Assert.False(outcome.Succeeded);
        Assert.Null(outcome.UnreadReply);
        Assert.Null(outcome.UnreadStep);
    }

    private static Task<AdventureOutcome> Write(ILlmProvider provider) =>
        AdventureGeneratorTests.Generator(provider, new AdventureGeneratorTests.Galaxy(), NullLogger.Instance)
            .GenerateAsync(new AdventureAsk(Length: AdventureLength.Short), Now, CancellationToken.None);
}
