using D47.Core.Audio;
using D47.Core.Callouts;
using D47.Core.Configuration;
using D47.Core.Journal;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Audio;

/// <summary>
/// Which slot a line belongs to, and which provider speaks for it (Phase 57).
/// <para>
/// The mapping is asserted rather than commented, because it is the whole of the routing and
/// every one of its inputs already existed: a role, and a chat channel. Getting it wrong is not a
/// wrong voice — it is a stranger's text sent to a paid API, which is the bill the phase exists
/// to stop.
/// </para>
/// </summary>
public class EveryVoiceCanComeFromSomewhereDifferentTests
{
    private const string Edge = TtsProviderCatalog.EdgeId;
    private const string Eleven = TtsProviderCatalog.ElevenLabsId;
    private const string None = TtsProviderCatalog.NoneId;

    [Theory]
    [InlineData(VoiceRole.ShipAi)]
    [InlineData(VoiceRole.Crew)]
    public void TheOnlyTwoVoicesInTheRoomAreOneSlot(VoiceRole role)
    {
        // The same division RadioVoice.IsOverTheAir already draws, given a second job rather than
        // a new one drawn beside it.
        Assert.Equal(VoiceGroup.Aboard, VoiceGroups.Of(role));
        Assert.False(RadioVoice.IsOverTheAir(role));
    }

    [Theory]
    [InlineData(VoiceRole.CarrierCaptain)]
    [InlineData(VoiceRole.TowerControl)]
    public void TheCarrierIsTwoPeopleAndOneInstallation(VoiceRole role) =>
        Assert.Equal(VoiceGroup.Carrier, VoiceGroups.Of(role));

    [Theory]
    [InlineData("npc", VoiceGroup.Npcs)]
    [InlineData("friend", VoiceGroup.PeopleYouKnow)]
    [InlineData("wing", VoiceGroup.PeopleYouKnow)]
    [InlineData("squadron", VoiceGroup.PeopleYouKnow)]
    [InlineData("player", VoiceGroup.DirectMessages)]
    [InlineData("local", VoiceGroup.AnyoneInRange)]
    [InlineData("starsystem", VoiceGroup.AnyoneInRange)]
    public void TheHumanChannelsSortByConsentRatherThanByHumanity(string channel, VoiceGroup expected) =>
        Assert.Equal(expected, VoiceGroups.Of(VoiceRole.Comms, channel));

    /// <summary>
    /// The distinction the boolean cannot make. A squadron mate and a stranger shouting in local
    /// are both <c>SpeakerIsPlayer</c>, and one of them is somebody the Commander chose.
    /// </summary>
    [Fact]
    public void ASquadronMateAndAStrangerAreNotTheSameSlot() =>
        Assert.NotEqual(
            VoiceGroups.Of(VoiceRole.Comms, "squadron"),
            VoiceGroups.Of(VoiceRole.Comms, "local"));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("somethingfrontieraddedlater")]
    public void AChannelNeitherReaderKnowsIsAnNpcToBothOfThem(string? channel)
    {
        // Spoken by one and gated by the other is the disagreement that would not report itself,
        // which is why there is one list rather than two.
        Assert.Equal(VoiceGroup.Npcs, VoiceGroups.Of(VoiceRole.Comms, channel));
        Assert.False(VoiceGroups.IsAPerson(channel));
    }

    /// <summary>
    /// Every channel Elite writes has a slot. A channel with none would fall to the NPC default
    /// and be gated behind the NPC switch, which for a player channel is a message silently lost.
    /// </summary>
    [Fact]
    public void EveryChannelEliteWritesIsAccountedFor()
    {
        string[] elites =
            ["npc", "player", "wing", "local", "friend", "squadron", "squadleaders", "starsystem"];

        var covered = VoiceGroups.All.SelectMany(slot => slot.Channels).ToList();

        Assert.Equal([.. elites.Order()], [.. covered.Order()]);
    }

    /// <summary>
    /// The one that carries the money. Frontier writes the NPC chatter and d47 writes the
    /// carrier's; the other three are somebody else typing.
    /// </summary>
    [Fact]
    public void OnlyTheThreeHumanSlotsCarryOtherPeoplesWords()
    {
        var carrying = VoiceGroups.All.Where(slot => slot.OtherPeoplesWords).Select(slot => slot.Group);

        Assert.Equal(
            [VoiceGroup.PeopleYouKnow, VoiceGroup.DirectMessages, VoiceGroup.AnyoneInRange],
            carrying);
    }

    /// <summary>
    /// The ids are settings keys, and the settings file is append-only. Renaming one drops a
    /// Commander's choice silently and puts the slot back on the ship's provider — which for
    /// local chat is the bill this phase exists to prevent, arriving through the fix.
    /// </summary>
    [Fact]
    public void TheSlotIdsAreTheOnesTheSettingsFileHolds() =>
        Assert.Equal(
            ["aboard", "carrier", "npcs", "known", "direct", "range"],
            VoiceGroups.All.Select(slot => slot.Id));

    [Fact]
    public void TheShipsRowIsStillTheShipsProvider()
    {
        var speech = new SpeechSettings
        {
            Provider = Eleven,
            GroupProviders = new Dictionary<string, string> { ["aboard"] = Edge },
        };

        // "aboard" cannot be written into that map by any row, and if a hand-edited file holds
        // one it is ignored: speech.provider has meant the ship's voice since Phase 4.
        Assert.Equal(Eleven, VoiceGroups.ProviderFor(speech, VoiceGroup.Aboard));
    }

    [Fact]
    public void AnAbsentEntryFollowsTheShip()
    {
        var speech = new SpeechSettings { Provider = Eleven, GroupProviders = new Dictionary<string, string>() };

        Assert.Equal(Eleven, VoiceGroups.ProviderFor(speech, VoiceGroup.AnyoneInRange));
    }

    [Fact]
    public void AProviderD47DoesNotShipResolvesTheWayItDoesEverywhereElse()
    {
        var speech = new SpeechSettings
        {
            Provider = Edge,
            GroupProviders = new Dictionary<string, string> { ["range"] = "festival" },
        };

        Assert.Equal(Edge, VoiceGroups.ProviderFor(speech, VoiceGroup.AnyoneInRange));
    }

    /// <summary>
    /// A file written before Phase 57 sounds exactly as it did: one provider, every voice.
    /// </summary>
    [Fact]
    public void AFileFromBeforeThisPhaseReadsAsOneProviderEverywhere()
    {
        var speech = new SpeechSettings { Provider = Eleven };

        Assert.All(
            VoiceGroups.All,
            slot => Assert.Equal(Eleven, VoiceGroups.ProviderFor(speech, slot.Group)));
    }

    [Fact]
    public void MigratingMovesEveryVoiceThatArrivesOverARadioToEdge()
    {
        var migrated = VoiceGroups.Migrated(Settings(Eleven));

        Assert.Equal(Eleven, VoiceGroups.ProviderFor(migrated.Speech, VoiceGroup.Aboard));

        Assert.All(
            VoiceGroups.All.Where(slot => slot.OverTheAir),
            slot => Assert.Equal(Edge, VoiceGroups.ProviderFor(migrated.Speech, slot.Group)));
    }

    /// <summary>
    /// The half a Commander would otherwise discover on a bill: it is not only the untrusted
    /// slots that move, because the carrier and the NPCs arrive over a radio too.
    /// </summary>
    [Fact]
    public void TheCarrierMovesWithThemBecauseItComesOverARadio() =>
        Assert.Equal(
            Edge,
            VoiceGroups.ProviderFor(VoiceGroups.Migrated(Settings(Eleven)).Speech, VoiceGroup.Carrier));

    [Fact]
    public void MigratingHappensOnceAndThenChangesNothing()
    {
        var migrated = VoiceGroups.Migrated(Settings(Eleven));

        // The caller compares by reference to decide whether to write. Migrating on every apply
        // would be a settings save and an announcement every time a slider moved.
        Assert.Same(migrated, VoiceGroups.Migrated(migrated));
    }

    /// <summary>
    /// A Commander who asked for silence must not be answered by five slots starting to talk.
    /// The ruling applies at the moment it means something, which is when a provider that speaks
    /// is chosen.
    /// </summary>
    [Fact]
    public void NothingIsMigratedWhileNothingIsSpeaking()
    {
        var quiet = Settings(None);

        Assert.Same(quiet, VoiceGroups.Migrated(quiet));
        Assert.Null(quiet.Speech.GroupProviders);
    }

    [Fact]
    public void AWrittenMapIsNeverMigratedOverTheTopOfItself()
    {
        var chosen = Settings(Edge) with
        {
            Speech = new SpeechSettings
            {
                Provider = Edge,
                GroupProviders = new Dictionary<string, string> { ["range"] = Eleven },
            },
        };

        // Empty is a Commander who put every slot back on the ship's; a single entry is one who
        // chose. Neither is "before Phase 57", and re-migrating would overwrite the choice.
        Assert.Same(chosen, VoiceGroups.Migrated(chosen));
        Assert.Equal(Eleven, VoiceGroups.ProviderFor(chosen.Speech, VoiceGroup.AnyoneInRange));
    }

    [Fact]
    public void TheProvidersInUseAreCountedOnceEach()
    {
        var speech = VoiceGroups.Migrated(Settings(Eleven)).Speech;

        // Five slots on Edge and one on ElevenLabs is two clients, which is the property
        // ElevenLabsTtsProvider.MaxConcurrent depends on.
        Assert.Equal([Edge, Eleven], VoiceGroups.ProvidersInUse(speech));
    }

    [Fact]
    public void ASilentSlotNeedsNoClient()
    {
        var speech = new SpeechSettings
        {
            Provider = None,
            GroupProviders = new Dictionary<string, string> { ["range"] = None, ["npcs"] = Edge },
        };

        Assert.Equal([Edge], VoiceGroups.ProvidersInUse(speech));
    }

    /// <summary>
    /// The routing reads what the callout wrote, so the two are held against each other rather
    /// than against a fixture: a message on the wire lands in the slot its channel names.
    /// </summary>
    [Fact]
    public void AMessageOffTheWireLandsInItsOwnSlot()
    {
        var messages = new IncomingMessages { Enabled = () => true };

        var read = messages.Read(Received("local", "Vex", "anybody out here"));

        Assert.NotNull(read);
        Assert.Equal("local", read.CommsChannel);
        Assert.Equal(VoiceGroup.AnyoneInRange, VoiceGroups.Of(read.Voice, read.CommsChannel));
    }

    [Fact]
    public void AndTheCarriersOwnTrafficStillLandsInTheCarriers()
    {
        var messages = new IncomingMessages
        {
            Enabled = () => true,
            IncludeNpcs = () => true,
            CarrierCallSign = "K7Q-B4W",
        };

        var read = messages.Read(Received("npc", "K7Q-B4W Ellipsis", "docking granted"));

        Assert.NotNull(read);
        Assert.Equal(VoiceGroup.Carrier, VoiceGroups.Of(read.Voice, read.CommsChannel));
    }

    private static D47Settings Settings(string provider) =>
        new() { Speech = new SpeechSettings { Provider = provider } };

    private static JournalEvent Received(string channel, string from, string message)
    {
        var json = System.Text.Json.JsonSerializer.Serialize(new Dictionary<string, object?>
        {
            ["timestamp"] = "2026-02-10T09:00:00Z",
            ["event"] = "ReceiveText",
            ["From"] = from,
            ["Message"] = message,
            ["Channel"] = channel,
        });

        Assert.True(JournalEvent.TryParse(json, NullLogger.Instance, out var parsed));
        return parsed!;
    }
}
