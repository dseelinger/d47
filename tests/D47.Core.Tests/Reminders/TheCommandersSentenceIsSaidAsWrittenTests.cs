using D47.Core.Callouts;
using D47.Core.Conversation;
using D47.Core.Reminders;
using Xunit;

namespace D47.Core.Tests.Reminders;

/// <summary>A reminder's lead-in may be reworded; the Commander's sentence follows it unchanged and never reaches a model.</summary>
public class TheCommandersSentenceIsSaidAsWrittenTests
{
    private const string Sentence = "Ask Marlowe about the convoy, and don't forget the 40 tonnes of gold.";

    private static Announcement Reminder() =>
        new(JournalReminderCallout.KeyPrefix + "abc", "You asked me to remind you when you docked.") { Verbatim = Sentence };

    private static async Task<(Announcement? Said, List<string> Asked)> Vary(int percent, string? reply)
    {
        var asked = new List<string>();

        var said = await new Rewording(new RewordChance(new Random(1)), null).VaryAsync(
            Reminder(),
            hasModel: true,
            personalityEnabled: true,
            percent,
            () => ShipFacts.Unknown,
            "Doug",
            (brief, instruction, token) =>
            {
                asked.Add(instruction);
                asked.Add(brief.Instruction);
                return Task.FromResult(new FlavourReply(reply, reply is null ? FlavourMiss.Failed : FlavourMiss.None));
            });

        return (said, asked);
    }

    [Fact]
    public async Task AReworkedLeadInIsFollowedByTheSentenceUnchanged()
    {
        var (said, asked) = await Vary(100, "We're down, Commander. You wanted this said:");

        Assert.NotNull(said);
        Assert.Equal($"We're down, Commander. You wanted this said: {Sentence}", said.Heard);
        Assert.NotEmpty(asked);
        Assert.DoesNotContain(asked, instruction => instruction.Contains("Marlowe", StringComparison.Ordinal));
    }

    [Fact]
    public async Task AnAuthoredLeadInIsFollowedByTheSentenceUnchanged()
    {
        var (said, asked) = await Vary(0, "unused");

        Assert.NotNull(said);
        Assert.Empty(asked);
        Assert.Equal($"You asked me to remind you when you docked. {Sentence}", said.Heard);
    }

    [Fact]
    public async Task AFailedRewordFallsBackToTheAuthoredLeadInAndTheSentence()
    {
        var (said, _) = await Vary(100, null);

        Assert.NotNull(said);
        Assert.Equal($"You asked me to remind you when you docked. {Sentence}", said.Heard);
    }

    [Fact]
    public async Task ALeadInWithNoStopOfItsOwnGetsOne()
    {
        var (said, _) = await Vary(100, "Heads up, Commander");

        Assert.Equal($"Heads up, Commander. {Sentence}", said!.Heard);
    }

    [Fact]
    public void TheConversationFeedCarriesTheLeadInOnly() =>
        Assert.Equal("You asked me to remind you when you docked.", Reminder().ConversationLine);
}
