using System.Text.Json;
using D47.Core.Audio;
using D47.Core.Callouts;
using D47.Core.Journal;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Callouts;

/// <summary>
/// In-game chat, read aloud in somebody else's voice (list.md Phase 11).
/// </summary>
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
    /// A message from the Commander's own carrier comes in the tower's voice
    /// (<a href="https://github.com/dseelinger/d47/issues/28">#28</a>), asked for 2026-08-24.
    /// <para>
    /// Every part of this existed except the join: the role, its settings row and its audition were
    /// all there, and only d47's own carrier callouts ever used them. A message <em>from</em> the
    /// carrier was answered in a stranger's voice.
    /// </para>
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

    /// <summary>
    /// And nobody else does. A squadron's carrier reaching this would be the fleet-carrier fault of
    /// 0.45.0 arriving in the ear instead of the answer, so the match is against the Commander's
    /// own carrier alone.
    /// </summary>
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

    /// <summary>
    /// An NPC is read as the words alone (docs/plans/change-requests.md item 11).
    /// <para>
    /// The preamble was written for people, and Elite's NPC traffic is mostly not people. In the
    /// 912-journal corpus the two commonest NPC senders are <c>$ShipName_Police_Independent;</c>
    /// and a station's name, so what the Commander actually heard on an approach was "ShipName
    /// Police Independent says" over and over, in front of transmissions three words long.
    /// </para>
    /// </summary>
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

    /// <summary>
    /// A player keeps theirs. A Commander's name is a name, said once, and in wing chat it is the
    /// thing the Commander most needs: the voice says "not you", only the name says which of them.
    /// </summary>
    [Fact]
    public void APlayerIsStillReadWithTheirName()
    {
        var read = Reader().Read(Message("$cmdr_decorate:#name=Vex;", "watch your six", "wing"));

        Assert.NotNull(read);
        Assert.Equal("Vex says: watch your six", read.Text);
        Assert.Equal("Vex: watch your six\n", read.Transcript);
    }

    /// <summary>
    /// A message can arrive with no sender at all, and it used to be spoken as
    /// " says: <em>whatever it was</em>", because an empty name is not a null one.
    /// </summary>
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
    /// <c>ReceiveText</c> from nobody — 8,833 of them across the corpus, the commonest shape
    /// there is and second in volume only to station traffic. It is a fact the Commander can read
    /// off the system name in front of them, and it is not worth a voice
    /// (remediation.md, "Do not announce entering a new channel").
    /// </summary>
    [Fact]
    public void EnteringAChannelIsNotAMessage()
    {
        Assert.Null(Reader().Read(Message(
            string.Empty, "$COMMS_entered:#name=Cakutsi;", "npc", localised: "Entered Channel: Cakutsi")));
    }

    /// <summary>
    /// Matched on the token rather than on the English sentence, so a Commander playing in
    /// German is not read it either — which is the whole reason the unlocalised field is the one
    /// tested.
    /// </summary>
    [Fact]
    public void TheChannelNoticeIsRecognisedWhateverLanguageItArrivesIn()
    {
        Assert.Null(Reader().Read(Message(
            string.Empty, "$COMMS_entered:#name=Cakutsi;", "npc", localised: "Kanal betreten: Cakutsi")));
    }

    /// <summary>
    /// Every message reaches the Technical page, which is where "true, useful, and not the
    /// conversation" already lives. Non-null <see cref="Announcement.Transcript"/> is the signal
    /// that carries it there, so a message with none would be heard and then unfindable.
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
    /// And nothing else does — a Phase 8 callout is d47 speaking, and it belongs on the
    /// conversation rather than beside a station's traffic.
    /// </summary>
    [Fact]
    public void ACalloutThatIsD47SpeakingIsNotOnTheCommsPage()
    {
        Assert.Null(new Announcement("fuel.low", "Fuel is low.").Transcript);
    }

    /// <summary>
    /// It is on the conversation instead. This was the gap: <see cref="Announcement.Transcript"/>
    /// carried in-game comms to the Technical page and nothing carried a callout anywhere, so a
    /// fuel warning was heard once and was afterwards findable only in the log file
    /// (remediation.md, "Ship AI callouts belong on the Conversation and Technical tabs").
    /// </summary>
    [Fact]
    public void ACalloutThatIsD47SpeakingIsOnTheConversation()
    {
        Assert.Equal("Fuel is low.", new Announcement("fuel.low", "Fuel is low.").ConversationLine);
    }

    /// <summary>
    /// And a re-voiced message is not, whichever page it lands on. A station clearing the
    /// Commander to dock is not their companion talking, and putting it on the conversation is
    /// the exact confusion the two roles exist to keep apart.
    /// </summary>
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
        // Elite writes `$Pirate_Attack;` style ids when it has no localised form to give. Saying
        // "dollar pirate underscore attack semicolon" out loud is worse than silence — and the
        // danger callouts already treat these as ids rather than as prose.
        Assert.Null(Reader().Read(Message("$npc_name_decorate:#name=Ilse;", "$Pirate_Attack;", "npc")));
    }

    [Fact]
    public void TheCommandersOwnMessagesAreNotReadBackToThem()
    {
        // Elite echoes what you send on the channel it went out on. Dictating into wing chat and
        // then hearing yourself in a stranger's voice is the most confusing thing this could do.
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
        // Starting d47 after an hour of flying must not read out an hour of chat, for the same
        // reason the material milestones prime silently.
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
        // Two messages on one channel are two messages, not one warning said twice. The callout
        // cooldown exists for conditions that stay true across hundreds of ticks.
        var read = Reader().Read(Message("$cmdr_decorate:#name=Vex;", "one", "wing"));

        Assert.NotNull(read);
        Assert.Equal(TimeSpan.Zero, read.Cooldown);
    }
}
