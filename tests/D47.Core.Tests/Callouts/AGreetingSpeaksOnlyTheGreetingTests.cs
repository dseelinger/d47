using D47.Core.Callouts;
using D47.Core.Conversation;
using Xunit;

namespace D47.Core.Tests.Callouts;

public class AGreetingSpeaksOnlyTheGreetingTests
{
    private const string Authored = "Good evening, Commander. Good to have you back.";

    private static Task<Announcement?> Vary(string reply) =>
        new Rewording(new RewordChance(new Random(1)), null).VaryAsync(
            new Announcement(ContinuityCallout.Key, Authored),
            hasModel: true,
            personalityEnabled: true,
            rewordPercent: 100,
            () => ShipFacts.Unknown,
            "Doug",
            (brief, instruction, token) => Task.FromResult(new FlavourReply(reply, FlavourMiss.None, null)));

    [Fact]
    public async Task AReplyThatReportsItsOwnChoiceIsReplacedByTheAuthoredLine()
    {
        var said = await Vary(
            "I'm keeping the greeting as given, since nothing new is aboard and no session ended on anything I can name. "
            + "The first sentence keeps its time of day, and the second is kept as is. Good evening, Commander. Good to have you back.");

        Assert.Equal(Authored, said?.Text);
    }

    [Fact]
    public async Task AGreetingThatOpensAsOneIsSpokenUnchanged()
    {
        var said = await Vary("Good evening, Commander. Good to have you back.");

        Assert.Equal("Good evening, Commander. Good to have you back.", said?.Text);
    }

    [Fact]
    public void TheBriefAsksForTheGreetingAlone()
    {
        var brief = FlavourBriefs.For(new Announcement(ContinuityCallout.Key, Authored), personalityEnabled: true);

        Assert.NotNull(brief);
        Assert.Contains("Answer with the two sentences of the greeting alone", brief.Instruction, StringComparison.Ordinal);
        Assert.DoesNotContain("what is aboard", brief.Instruction, StringComparison.Ordinal);
    }
}
