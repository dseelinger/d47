using D47.Core.Callouts;
using D47.Core.Audio;
using D47.Core.Conversation;
using D47.Core.Stories;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Callouts;

public class AModelsReasoningIsNeverSpokenTests
{
    private const string Reasoning =
        "Starport traffic control, reading a routine notice: the line is \"No fire zone entered.\" It's a notice, "
        + "so I'll keep it flat and procedural, with no threat or plea added. [deadpan] Nobody's been cleared into "
        + "the fire zone, so nobody has entered it.";

    private const string GreetingReasoning =
        "I'm keeping the greeting as given, since nothing new is aboard and no session ended on anything I can name. "
        + "The first sentence keeps its time of day, and the second is kept as is. Good evening, Commander. Good to have you back.";

    private static Task<Announcement?> Vary(string key, string text, string reply) =>
        new Rewording(new RewordChance(new Random(1)), NullLogger.Instance).VaryAsync(
            new Announcement(key, text) { Voice = VoiceRole.TowerControl },
            hasModel: true,
            personalityEnabled: true,
            rewordPercent: 100,
            () => ShipFacts.Unknown,
            "Doug",
            (brief, instruction, token) => Task.FromResult(new FlavourReply(reply, FlavourMiss.None, null)));

    private static string Prompt(string key, string text)
    {
        var brief = FlavourBriefs.For(new Announcement(key, text), personalityEnabled: true)!;
        return $"{brief.Speaker} {brief.Instruction}";
    }

    [Fact]
    public async Task AStationNoticeAnsweredWithReasoningIsSpokenAsWritten()
    {
        var said = await Vary("message.canned.STATION", "No fire zone entered.", Reasoning);

        Assert.Equal("No fire zone entered.", said?.Text);
    }

    [Fact]
    public void TheGreetingReplyIsRejectedWithoutAKeyForTheGreeting()
    {
        const string text = "Good evening, Commander. Good to have you back.";

        Assert.False(FlavourBriefs.MayBeSpoken(GreetingReasoning, Prompt(ContinuityCallout.Key, text)));
    }

    [Fact]
    public void AReplyThatQuotesTheLineItWasGivenIsRejected()
    {
        Assert.False(FlavourBriefs.MayBeSpoken(
            "Here it is: \"No fire zone entered.\"", Prompt("message.canned.STATION", "No fire zone entered.")));
    }

    [Theory]
    [InlineData("Rendering it in my own words, same notice, nothing added: \"Your entry into the fire zone is not permitted.\"")]
    [InlineData("Reworded, same meaning, same threat: \"I'd really think twice before you go and do that.\"")]
    [InlineData("Reworded:   “Keep out of the fire zone.”")]
    public void ARewordedLineThatIntroducesAQuotationIsRejected(string reply)
    {
        Assert.False(FlavourBriefs.MayBeSpoken(reply, Prompt("message.canned.STATION", "No fire zone entered.")));
    }

    [Fact]
    public void AColonNotBeforeAQuotationIsAllowedWithAPrompt()
    {
        Assert.True(FlavourBriefs.MayBeSpoken(
            "Traffic control notice: you have left the no-fire zone.", Prompt("message.canned.STATION", "No fire zone entered.")));
    }

    [Fact]
    public void AColonBeforeAQuotationIsAllowedWithoutAPrompt()
    {
        Assert.True(FlavourBriefs.MayBeSpoken("The sign read: \"Keep out.\""));
    }

    [Fact]
    public void CommentaryAheadOfADeliveryTagIsRejectedWithoutAPrompt()
    {
        Assert.False(FlavourBriefs.MayBeSpoken(Reasoning));
    }

    [Fact]
    public void ADeliveryTagInsideALineIsSaid()
    {
        Assert.True(FlavourBriefs.MayBeSpoken("Hold on, Commander. [sighs] Fine, you may dock."));
    }

    [Theory]
    [InlineData("cast")]
    [InlineData("narrated")]
    [InlineData("crew")]
    public void AClueLineThatEchoesTheClueIsSaid(string speaker)
    {
        const string clue = "Bay four was never empty.";

        var brief = speaker switch
        {
            "cast" => StoryClues.Speaking(clue, "Ren", "A tired dock hand."),
            "narrated" => StoryClues.Speaking(clue, narrated: true),
            _ => StoryClues.Speaking(clue, narrated: false),
        };

        Assert.True(FlavourBriefs.MayBeSpoken("Bay four was never empty, you know.", brief.Instruction));
    }

    [Fact]
    public async Task ACleanStationRewordingIsSpokenUnchanged()
    {
        var said = await Vary("message.canned.STATION", "Docking request granted.", "Request granted, you're cleared to dock.");

        Assert.Equal("Request granted, you're cleared to dock.", said?.Text);
    }

    [Fact]
    public async Task ACleanCarrierRewordingIsSpokenUnchanged()
    {
        var said = await Vary("carrier.comms", "Docking request granted.", "Docking granted, Commander.");

        Assert.Equal("Docking granted, Commander.", said?.Text);
    }
}
