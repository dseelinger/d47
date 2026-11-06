using D47.Core.Adventures;
using D47.Core.Conversation;
using D47.Core.Tests.Conversation;
using D47.Core.Tests.Stories;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Adventures;

public sealed class AnUnreadableReplyIsKeptOnTheOutcomeTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 22, 12, 0, 0, TimeSpan.Zero);

    private const string NotJson = "I would rather ask which system you want this to start in.";

    [Fact]
    public async Task ALinesReplyThatIsNotJsonIsCarriedWithItsStep()
    {
        var provider = new RoundScriptedLlmProvider(RoundScriptedLlmProvider.Saying(NotJson));

        var outcome = await Write(provider);

        Assert.False(outcome.Succeeded);
        Assert.Equal(NotJson, outcome.UnreadReply);
        Assert.Equal("lines", outcome.UnreadStep);
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

    private static async Task<AdventureOutcome> Write(ILlmProvider provider)
    {
        var state = PlannedChapterWorld.State();
        var galaxy = new PlannedChapterWorld.Galaxy();
        var generator = new AdventureGenerator(() => provider, () => null, () => "You are the ship.", () => "core", () => null, () => state, () => galaxy, () => null, null, null, NullLogger.Instance);

        return await generator.GenerateAsync(PlannedChapterWorld.Ask(), Now, CancellationToken.None);
    }
}
