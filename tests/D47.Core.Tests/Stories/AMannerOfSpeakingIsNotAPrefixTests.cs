using D47.Core.Adventures;
using D47.Core.Persona;
using D47.Core.Stories;
using Xunit;

namespace D47.Core.Tests.Stories;

/// <summary>Every prompt that passes a cast member's description to a writer says a manner in it is not a set phrase.</summary>
public sealed class AMannerOfSpeakingIsNotAPrefixTests
{
    private const string Who = "Speaks in clipped file notes.";

    [Fact]
    public async Task TheReplyToTheCommandersAnswerCarriesTheRule()
    {
        var line = new CastLine(_ => new AdventureSpeaker("rowan", "Rowan", Who), () => null);

        line.Heard("rowan", "For the file.");
        var decision = await line.RouteAsync(D47.Core.Tests.Conversation.OnComms.Cast("rowan", "Rowan"), "Who are you?", CancellationToken.None);

        var taken = Assert.IsType<LineDecision.Taken>(decision);
        Assert.Contains(Who, taken.Speaker.Brief);
        Assert.Contains(StorySpeaker.MannerRule, taken.Speaker.Brief);
    }

    [Fact]
    public void ARecurringCharactersReactionCarriesTheRule()
    {
        var now = new DateTimeOffset(2026, 10, 24, 12, 0, 0, TimeSpan.Zero);
        var notable = new Notable(NotableKind.Promotion, now, "The Commander was promoted.");
        var brief = StoryReactions.Asking(notable, [new("rowan", "Rowan", Who)]);

        Assert.Contains(Who, brief.Instruction);
        Assert.Contains(StorySpeaker.MannerRule, brief.Instruction);
    }

    [Fact]
    public void AClueSpokenByACastMemberCarriesTheRule()
    {
        var brief = StoryClues.Speaking("A beacon was moved.", "Rowan", Who);

        Assert.Contains(Who, brief.Speaker);
        Assert.Contains(StorySpeaker.MannerRule, brief.Speaker);
    }
}
