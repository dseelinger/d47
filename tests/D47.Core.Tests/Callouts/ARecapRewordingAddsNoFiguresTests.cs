using D47.Core.Callouts;
using D47.Core.Conversation;
using Xunit;

namespace D47.Core.Tests.Callouts;

public class ARecapRewordingAddsNoFiguresTests
{
    private const string Authored = "Earlier this session you finished docked at Suri Park, Anlave in a Dominator Suit, grade 3.";

    private static Task<Announcement?> Vary(string reply) =>
        new Rewording(new RewordChance(new Random(1)), null).VaryAsync(
            new Announcement(RecapCallout.Key, Authored),
            hasModel: true,
            personalityEnabled: true,
            rewordPercent: 100,
            () => ShipFacts.Unknown,
            "Doug",
            (brief, instruction, token) => Task.FromResult(new FlavourReply(reply, FlavourMiss.None, null)));

    [Fact]
    public async Task AReplyThatAddsALengthOfTimeIsReplacedByTheAuthoredLine()
    {
        var said = await Vary("Forty-odd minutes of flying, and I'm told the suit got more admiring looks than the pilot did.");

        Assert.Equal(Authored, said?.Text);
    }

    [Fact]
    public async Task AReplyThatAddsANumberIsReplacedByTheAuthoredLine()
    {
        var said = await Vary("You finished docked at Suri Park, Anlave in a Dominator Suit, grade 3, after 12 jumps.");

        Assert.Equal(Authored, said?.Text);
    }

    [Fact]
    public async Task AReplyThatOnlyRephrasesIsSpokenAsReworded()
    {
        const string Reply = "Last time you ended up docked at Suri Park, Anlave, wearing a grade 3 Dominator Suit.";

        var said = await Vary(Reply);

        Assert.Equal(Reply, said?.Text);
    }

    [Fact]
    public void ABriefDoesNotAskForTheFlyingSoFar()
    {
        var brief = FlavourBriefs.For(new Announcement(RecapCallout.Key, Authored), personalityEnabled: true);

        Assert.NotNull(brief);
        Assert.DoesNotContain("flying so far", brief.Instruction, StringComparison.Ordinal);
        Assert.Contains("Do not state how long", brief.Instruction, StringComparison.Ordinal);
    }
}
