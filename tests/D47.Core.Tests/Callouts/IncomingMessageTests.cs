using System.Text.Json;
using D47.Core.Audio;
using D47.Core.Callouts;
using D47.Core.Journal;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Callouts;

/// <summary>In-game chat, read aloud in somebody else's voice.</summary>
public class IncomingMessageTests
{
    private static JournalEvent Message(string from, string message, string channel, string? localised = null)
    {
        var json = JsonSerializer.Serialize(new Dictionary<string, object?>
        {
            ["timestamp"] = "2026-02-10T09:00:00Z",
            ["event"] = "ReceiveText",
            ["From"] = from,
            ["Message"] = message,
            ["Message_Localised"] = localised,
            ["Channel"] = channel,
        });

        Assert.True(JournalEvent.TryParse(json, NullLogger.Instance, out var parsed));
        return parsed!;
    }

    private static IncomingMessages Reader(bool npcs = true) => new()
    {
        Enabled = () => true,
        IncludeNpcs = () => npcs,
    };

    /// <summary>With system chat off and everything else on, a <c>starsystem</c> message produces no announcement and a <c>wing</c> message still does.</summary>
    [Fact]
    public void AChannelSwitchedOffIsSilentWhileOthersStillSpeak()
    {
        var reader = Reader();
        reader.ChannelEnabled = channel => channel != "starsystem";

        Assert.Null(reader.Read(Message("$cmdr_decorate:#name=Vex;", "hello", "starsystem")));

        var wing = reader.Read(Message("$cmdr_decorate:#name=Vex;", "hello", "wing"));
        Assert.NotNull(wing);
    }

    /// <summary>
    /// <c>squadleaders</c> follows the Squadron row rather than having a switch of its own — the same
    /// merge <c>AppHost</c> wires, exercised here as the single boolean a Commander actually controls.
    /// </summary>
    [Fact]
    public void SquadLeadersFollowsTheSquadronRow()
    {
        var squadronRowOn = false;
        var reader = Reader();
        reader.ChannelEnabled = channel => channel switch
        {
            "squadron" or "squadleaders" => squadronRowOn,
            _ => true,
        };

        Assert.Null(reader.Read(Message("$cmdr_decorate:#name=Vex;", "hello", "squadron")));
        Assert.Null(reader.Read(Message("$cmdr_decorate:#name=Vex;", "hello", "squadleaders")));

        squadronRowOn = true;
        Assert.NotNull(reader.Read(Message("$cmdr_decorate:#name=Vex;", "hello", "squadron")));
        Assert.NotNull(reader.Read(Message("$cmdr_decorate:#name=Vex;", "hello", "squadleaders")));
    }

    /// <summary>NPC chatter is never asked about through <see cref="IncomingMessages.ChannelEnabled"/>.</summary>
    [Fact]
    public void ChannelFilteringNeverGatesNpcTraffic()
    {
        var reader = Reader(npcs: true);
        reader.ChannelEnabled = _ => false;

        var read = reader.Read(Message("$ShipName_Police_Federation;", "Scanning.", "npc", localised: "Scanning."));
        Assert.NotNull(read);
    }

    /// <summary>A Frontier-canned line from the Commander's own carrier rides the rewording brief: the <c>$…;</c> key proves no player wrote it, and the two fields that mark somebody else's words are absent.</summary>
    [Fact]
    public void ACannedLineFromYourOwnCarrierGoesToTheBriefNotTheVerbatimReader()
    {
        var reader = Reader();
        reader.CarrierCallSign = "K7Q-B4W";

        var read = reader.Read(Message(
            "K7Q-B4W Ellipsis",
            "$CarrierDockingGranted;",
            "npc",
            localised: "Commander JOHN DEPARAGON, docking granted—welcome back home."));

        Assert.NotNull(read);
        Assert.Equal(IncomingMessages.CarrierCannedKey, read.Key);
        Assert.Equal(VoiceRole.TowerControl, read.Voice);

        // The comms page keeps the original as sent (the Commander's instruction) while the voice says the
        // reworded line — so both marker fields stay, and the brief fires anyway, because the guard's
        // exemption is exactly these $-key-proven kinds.
        Assert.Equal("npc", read.CommsChannel);
        Assert.NotNull(read.Transcript);
        Assert.Contains("docking granted", read.Transcript, StringComparison.OrdinalIgnoreCase);
        Assert.NotNull(FlavourBriefs.For(read, personalityEnabled: true));
    }

    /// <summary>The patrol around the carrier gets the same road: a System Authority vessel's canned line, while the Commander shares a system with their own carrier, is reworded courteously — the owner of the assets being protected is not taunted about a clean scan.</summary>
    [Fact]
    public void AuthorityCannedNearYourOwnCarrierGetsTheOwnerTreatment()
    {
        var reader = Reader();
        reader.AuthorityNearOwnCarrier = () => true;

        var read = reader.Read(Message(
            "$ShipName_Police_Independent;",
            "$Police_Scan_NothingFound;",
            "npc",
            localised: "We didn't find anything on you... this time."));

        Assert.NotNull(read);
        Assert.Equal(IncomingMessages.AuthorityCannedKey, read.Key);
        Assert.Equal(VoiceRole.Comms, read.Voice);
        Assert.NotNull(read.Transcript);

        var brief = FlavourBriefs.For(read, personalityEnabled: true);

        Assert.NotNull(brief);
        Assert.Contains("not subordinate", brief.Instruction, StringComparison.Ordinal);
    }

    /// <summary>Away from the carrier there is no owner context, and it is just a message.</summary>
    [Fact]
    public void AuthorityCannedAwayFromTheCarrierIsJustAMessage()
    {
        var read = Reader().Read(Message(
            "$ShipName_Police_Independent;",
            "$Police_Scan_NothingFound;",
            "npc",
            localised: "We didn't find anything on you... this time."));

        Assert.NotNull(read);
        Assert.NotEqual(IncomingMessages.AuthorityCannedKey, read.Key);
        Assert.Null(FlavourBriefs.For(read, personalityEnabled: true));
    }

    /// <summary>
    /// A police ship's free text — no <c>$…;</c> key, so a player could have written it — stays an
    /// ordinary message even beside the Commander's own carrier: the owner road is only for Frontier's
    /// canned lines (#102).
    /// </summary>
    [Fact]
    public void AFreeTextLineFromAPoliceShipNearYourCarrierIsJustAMessage()
    {
        var reader = Reader();
        reader.AuthorityNearOwnCarrier = () => true;

        var read = reader.Read(Message(
            "$ShipName_Police_Independent;", "Stand down.", "npc", localised: "Stand down."));

        Assert.NotNull(read);
        Assert.NotEqual(IncomingMessages.AuthorityCannedKey, read.Key);
    }

    /// <summary>
    /// Free text from the same sender keeps the verbatim road and stays away from the model: a sender
    /// is a name, and a name can be worn.
    /// </summary>
    [Fact]
    public void FreeTextFromYourOwnCarrierStaysVerbatimAndAwayFromTheModel()
    {
        var reader = Reader();
        reader.CarrierCallSign = "K7Q-B4W";

        var read = reader.Read(Message("K7Q-B4W Ellipsis", "docking granted", "npc"));

        Assert.NotNull(read);
        Assert.Equal(VoiceRole.TowerControl, read.Voice);
        Assert.NotNull(read.Transcript);
        Assert.Null(FlavourBriefs.For(read, personalityEnabled: true));
    }

    /// <summary>Somebody else's carrier gets no owner treatment: it is just a message.</summary>
    [Fact]
    public void ACannedLineFromSomebodyElsesCarrierIsJustAMessage()
    {
        var read = Reader().Read(Message(
            "X9Z-11B Nomad", "$CarrierDockingGranted;", "npc", localised: "Docking granted."));

        Assert.NotNull(read);
        Assert.NotEqual(IncomingMessages.CarrierCannedKey, read.Key);
        Assert.Equal(VoiceRole.Comms, read.Voice);
    }

    [Fact]
    public void APlayerMessageIsSpokenInThatPlayersVoice()
    {
        var read = Reader().Read(Message("$cmdr_decorate:#name=Vex;", "watch your six", "wing"));

        Assert.NotNull(read);
        Assert.Equal(VoiceRole.Comms, read.Voice);
        Assert.Equal("Vex", read.Speaker);
        Assert.True(read.SpeakerIsPlayer);
        Assert.Contains("watch your six", read.Text, StringComparison.Ordinal);
    }

    /// <summary>
 /// A message from the Commander's own carrier comes in the tower's voice, asked for
    /// 2026-08-24.
    /// </summary>
    [Theory]
    [InlineData("JOHN DEPARAGON")]
    [InlineData("K7Q-B4Z")]
    [InlineData("JOHN DEPARAGON K7Q-B4Z")]
    public void TheCommandersOwnCarrierSpeaksAsItsTower(string from)
    {
        var reader = Reader();
        reader.CarrierName = "JOHN DEPARAGON";
        reader.CarrierCallSign = "K7Q-B4Z";

        var read = reader.Read(Message(from, "Docking granted.", "npc", localised: "Docking granted."));

        Assert.NotNull(read);
        Assert.Equal(VoiceRole.TowerControl, read.Voice);
    }

    /// <summary>And nobody else does.</summary>
    [Theory]
    [InlineData("SQUADRON PRIDE", "JOHN DEPARAGON", "K7Q-B4Z")]
    [InlineData("Station Traffic Control", "JOHN DEPARAGON", "K7Q-B4Z")]
    [InlineData("JOHN DEPARAGON", null, null)]
    public void EverybodyElseKeepsTheOrdinaryCommsVoice(string from, string? name, string? call)
    {
        var reader = Reader();
        reader.CarrierName = name;
        reader.CarrierCallSign = call;

        var read = reader.Read(Message(from, "Stand by.", "npc", localised: "Stand by."));

        Assert.NotNull(read);
        Assert.Equal(VoiceRole.Comms, read.Voice);
    }

    [Fact]
    public void NpcChatterIsItsOwnDecision()
    {
        var npc = Message("$ShipName_Police_Federation;", "Scanning.", "npc", localised: "Scanning.");

        Assert.Null(Reader(npcs: false).Read(npc));

        var read = Reader(npcs: true).Read(npc);
        Assert.NotNull(read);
        Assert.False(read.SpeakerIsPlayer);
    }

    [Fact]
    public void AnNpcIsReadAsTheWordsAlone()
    {
        var read = Reader().Read(
            Message("$ShipName_Police_Independent;", "Scanning.", "npc", localised: "Scanning."));

        Assert.NotNull(read);
        Assert.Equal("Scanning.", read.Text);

        // The sender is not lost, it has moved to the page — where there is no voice to carry it.
        Assert.Equal("ShipName Police Independent: Scanning.\n", read.Transcript);
    }

    /// <summary>A player keeps theirs.</summary>
    [Fact]
    public void APlayerIsStillReadWithTheirName()
    {
        var read = Reader().Read(Message("$cmdr_decorate:#name=Vex;", "watch your six", "wing"));

        Assert.NotNull(read);
        Assert.Equal("Vex says: watch your six", read.Text);
        Assert.Equal("Vex: watch your six\n", read.Transcript);
    }

    /// <summary>A message can arrive with no sender at all: an empty name is not a null one.</summary>
    [Fact]
    public void AMessageWithNoSenderIsNotReadWithAnEmptyOne()
    {
        var read = Reader().Read(Message(
            string.Empty, "$STATION_docking_granted;", "npc", localised: "Docking request granted."));

        Assert.NotNull(read);
        Assert.Equal("Docking request granted.", read.Text);
        Assert.Equal("Docking request granted.\n", read.Transcript);
    }

    /// <summary>
    /// Elite tells you which channel you have joined every time you drop out of hyperspace, as a
    /// <c>ReceiveText</c> from nobody — 8,833 of them across the corpus, the commonest shape there is
    /// and second in volume only to station traffic.
    /// </summary>
    [Fact]
    public void EnteringAChannelIsNotAMessage()
    {
        Assert.Null(Reader().Read(Message(
            string.Empty, "$COMMS_entered:#name=Cakutsi;", "npc", localised: "Entered Channel: Cakutsi")));
    }

    /// <summary>
    /// Matched on the token rather than on the English sentence, so a Commander playing in German is
    /// not read it either — which is the whole reason the unlocalised field is the one tested.
    /// </summary>
    [Fact]
    public void TheChannelNoticeIsRecognisedWhateverLanguageItArrivesIn()
    {
        Assert.Null(Reader().Read(Message(
            string.Empty, "$COMMS_entered:#name=Cakutsi;", "npc", localised: "Kanal betreten: Cakutsi")));
    }

    /// <summary>
    /// Every message reaches the Technical page, which is where "true, useful, and not the
    /// conversation" already lives.
    /// </summary>
    [Fact]
    public void EveryMessageIsWrittenDownAsWellAsSpoken()
    {
        var reader = Reader();

        foreach (var channel in new[] { "npc", "wing", "starsystem", "squadron" })
        {
            var read = reader.Read(Message("$cmdr_decorate:#name=Ilse;", "hello", channel));

            Assert.NotNull(read);
            Assert.False(string.IsNullOrWhiteSpace(read.Transcript), channel);
            Assert.EndsWith("\n", read.Transcript, StringComparison.Ordinal);
        }
    }

    /// <summary>
    /// And nothing else does — a Phase 8 callout is d47 speaking, and it belongs on the conversation
    /// rather than beside a station's traffic.
    /// </summary>
    [Fact]
    public void ACalloutThatIsD47SpeakingIsNotOnTheCommsPage()
    {
        Assert.Null(new Announcement("fuel.low", "Fuel is low.").Transcript);
    }

    /// <summary>It is on the conversation instead.</summary>
    [Fact]
    public void ACalloutThatIsD47SpeakingIsOnTheConversation()
    {
        Assert.Equal("Fuel is low.", new Announcement("fuel.low", "Fuel is low.").ConversationLine);
    }

    /// <summary>And a re-voiced message is not, whichever page it lands on.</summary>
    [Fact]
    public void ARevoicedMessageIsNeverOnTheConversation()
    {
        var read = Reader().Read(Message("$cmdr_decorate:#name=Vex;", "watch your six", "wing"));

        Assert.NotNull(read);
        Assert.Null(read.ConversationLine);
    }

    [Fact]
    public void AnUnlocalisedTokenIsNotSpokenAsText()
    {
        // Elite writes `$Pirate_Attack;` style ids when it has no localised form to give.
        Assert.Null(Reader().Read(Message("$npc_name_decorate:#name=Ilse;", "$Pirate_Attack;", "npc")));
    }

    [Fact]
    public void TheCommandersOwnMessagesAreNotReadBackToThem()
    {
        // Elite echoes what you send on the channel it went out on.
        var reader = Reader();
        reader.CommanderName = "Vex";

        Assert.Null(reader.Read(Message("$cmdr_decorate:#name=Vex;", "on my way", "wing")));
        Assert.NotNull(reader.Read(Message("$cmdr_decorate:#name=Ilse;", "on my way", "wing")));
    }

    [Theory]
    [InlineData("$cmdr_decorate:#name=Vex;", "Vex")]
    [InlineData("$npc_name_decorate:#name=Ilse Bruhn;", "Ilse Bruhn")]
    [InlineData("Vex", "Vex")]
    [InlineData("$ShipName_Police_Federation;", "ShipName Police Federation")]
    public void SenderNamesAreUnwrappedRatherThanSpokenWithTheirScaffolding(string from, string expected)
    {
        Assert.Equal(expected, IncomingMessages.Undecorate(from));
    }

    [Fact]
    public void NothingIsSpokenFromTheBacklog()
    {
        // Starting d47 after an hour of flying must not read out an hour of chat, for the same reason the
        // material milestones prime silently.
        var reader = Reader();

        var priming = new CalloutContext(
            DateTimeOffset.UnixEpoch,
            IsPriming: true,
            State: null,
            Status: GameStatus.Unknown,
            Route: NavRoute.None,
            Events: [Message("$cmdr_decorate:#name=Vex;", "hello", "wing")]);

        Assert.Empty(reader.Examine(priming));
    }

    [Fact]
    public void NothingIsSpokenWhileTheSettingIsOff()
    {
        var reader = new IncomingMessages { Enabled = () => false, IncludeNpcs = () => true };

        var context = new CalloutContext(
            DateTimeOffset.UnixEpoch,
            IsPriming: false,
            State: null,
            Status: GameStatus.Unknown,
            Route: NavRoute.None,
            Events: [Message("$cmdr_decorate:#name=Vex;", "hello", "wing")]);

        Assert.Empty(reader.Examine(context));
    }

    [Fact]
    public void MessagesAreNotSuppressedAsRepeats()
    {
        // Two messages on one channel are two messages, not one warning said twice.
        var read = Reader().Read(Message("$cmdr_decorate:#name=Vex;", "one", "wing"));

        Assert.NotNull(read);
        Assert.Equal(TimeSpan.Zero, read.Cooldown);
    }

 /// <summary>Another Commander cannot decide how d47 sounds.</summary>
    [Fact]
    public void DirectionInSomebodyElsesMessageIsReadAsTextAndNotObeyed()
    {
        var read = Reader().Read(Message("Vex", "[shouting] watch your six", "wing"));

        Assert.NotNull(read);
        Assert.DoesNotContain("[shouting]", read.Text, StringComparison.Ordinal);
        Assert.Contains("watch your six", read.Text, StringComparison.Ordinal);
    }

    /// <summary>
    /// And a message that was only direction leaves nothing worth speaking, so it is dropped rather
    /// than read out as a silence with a sender's name attached.
    /// </summary>
    [Fact]
    public void AMessageThatWasOnlyDirectionIsNotSpokenAtAll() =>
        Assert.Null(Reader().Read(Message("Vex", "[whispers]", "wing")));
}
