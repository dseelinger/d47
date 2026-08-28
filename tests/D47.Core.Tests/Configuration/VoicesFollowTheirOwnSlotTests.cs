using D47.Core.Audio;
using D47.Core.Configuration;
using Xunit;

namespace D47.Core.Tests.Configuration;

/// <summary>
/// A stored voice moves with the slot that owns it, not with the ship (Phase 57).
/// <para>
/// <see cref="VoicesAreRememberedPerProviderTests"/> is the Phase 19 behaviour and is unchanged
/// where every slot follows one provider. What is new is that they need not: a carrier left on
/// Edge while the companion moves to ElevenLabs still has a captain, and filing that captain's
/// voice away as though it had moved too would silence the carrier and lose the choice.
/// </para>
/// <para>
/// Only two slots ever reach here. The four comms slots have no stored voice — a sender is drawn
/// from the provider's pool at the moment they speak — so there is nothing of theirs to file.
/// </para>
/// </summary>
public class VoicesFollowTheirOwnSlotTests
{
    private const string Edge = TtsProviderCatalog.EdgeId;
    private const string Eleven = TtsProviderCatalog.ElevenLabsId;

    /// <summary>Everything on Edge, already migrated, with a voice in every slot that holds one.</summary>
    private static D47Settings OnEdge(params (string Slot, string Provider)[] pinned) => new()
    {
        Speech = new SpeechSettings
        {
            Provider = Edge,
            VoicesProvider = Edge,
            CarrierVoicesProvider = Edge,
            GroupProviders = pinned.ToDictionary(pin => pin.Slot, pin => pin.Provider),
            Voice = "en-US-RogerNeural",
            CarrierCaptainVoice = "en-GB-RyanNeural",
            TowerVoice = "en-AU-NatashaNeural",
        },
        Persona = new PersonaSettings
        {
            VoicesPaired = true,
            Voices = new Dictionary<string, string>(StringComparer.Ordinal) { ["warden"] = "en-GB-SoniaNeural" },
        },
    };

    private static D47Settings Ship(D47Settings settings, string provider) =>
        VoiceMemory.Reconciled(settings with { Speech = settings.Speech with { Provider = provider } });

    private static D47Settings Carrier(D47Settings settings, string provider) =>
        VoiceMemory.Reconciled(settings with
        {
            Speech = settings.Speech with
            {
                GroupProviders = new Dictionary<string, string>(
                    settings.Speech.GroupProviders ?? new Dictionary<string, string>(),
                    StringComparer.OrdinalIgnoreCase)
                {
                    ["carrier"] = provider,
                },
            },
        });

    [Fact]
    public void ACarrierPinnedToEdgeKeepsItsVoicesWhenTheShipMovesAway()
    {
        var after = Ship(OnEdge(("carrier", Edge)), Eleven);

        // The ship's are Edge ids and go; the carrier is still speaking through Edge, so its two
        // are still the right ids for the provider that will say them.
        Assert.Null(after.Speech.Voice);
        Assert.Empty(after.Persona.Voices);
        Assert.Equal("en-GB-RyanNeural", after.Speech.CarrierCaptainVoice);
        Assert.Equal("en-AU-NatashaNeural", after.Speech.TowerVoice);
    }

    [Fact]
    public void AndTheCarriersVoicesAreNotFiledAwayAsThoughTheyHadMoved()
    {
        var after = Ship(OnEdge(("carrier", Edge)), Eleven);

        var kept = Assert.Contains(Edge, after.Speech.ProviderVoices);

        Assert.Equal("en-US-RogerNeural", kept.Ship);
        Assert.Null(kept.CarrierCaptain);
        Assert.Null(kept.Tower);
    }

    [Fact]
    public void TheCarrierMovingOnItsOwnLeavesTheShipAlone()
    {
        var after = Carrier(OnEdge(("carrier", Edge)), Eleven);

        Assert.Null(after.Speech.CarrierCaptainVoice);
        Assert.Null(after.Speech.TowerVoice);

        // Untouched, including the flag: the pairing pass has still run against Edge's list, and
        // clearing it here would re-pick every core because the carrier changed provider.
        Assert.Equal("en-US-RogerNeural", after.Speech.Voice);
        Assert.True(after.Persona.VoicesPaired);
        Assert.Equal("en-GB-SoniaNeural", after.Persona.Voices["warden"]);
    }

    /// <summary>
    /// Both slots filing into one provider's entry, one at a time. Overwriting rather than
    /// merging would have the second departure erase what the first left behind.
    /// </summary>
    [Fact]
    public void TwoSlotsLeavingTheSameProviderBothLeaveSomethingToComeBackTo()
    {
        var shipGone = Ship(OnEdge(("carrier", Edge)), Eleven);
        var bothGone = Carrier(shipGone, Eleven);

        var kept = Assert.Contains(Edge, bothGone.Speech.ProviderVoices);

        Assert.Equal("en-US-RogerNeural", kept.Ship);
        Assert.Equal("en-GB-RyanNeural", kept.CarrierCaptain);
        Assert.Equal("en-AU-NatashaNeural", kept.Tower);
        Assert.Equal("en-GB-SoniaNeural", kept.Cores["warden"]);
        Assert.True(kept.Paired);
    }

    [Fact]
    public void AndEachComesBackOnItsOwn()
    {
        var bothGone = Carrier(Ship(OnEdge(("carrier", Edge)), Eleven), Eleven);

        var carrierBack = Carrier(bothGone, Edge);

        Assert.Equal("en-GB-RyanNeural", carrierBack.Speech.CarrierCaptainVoice);

        // The ship is still on ElevenLabs, so its Edge voice stays filed and is not restored
        // early — restoring it here would put an Edge id in front of ElevenLabs.
        Assert.Null(carrierBack.Speech.Voice);

        var shipBack = Ship(carrierBack, Edge);

        Assert.Equal("en-US-RogerNeural", shipBack.Speech.Voice);
        Assert.Equal("en-GB-RyanNeural", shipBack.Speech.CarrierCaptainVoice);
    }

    /// <summary>
    /// The stash holds what is owed and nothing else. An entry left behind once both slots have
    /// come home would be restored a second time over choices made since.
    /// </summary>
    [Fact]
    public void AnEntryWithNothingLeftInItIsRemoved()
    {
        var away = Carrier(Ship(OnEdge(("carrier", Edge)), Eleven), Eleven);
        var home = Ship(Carrier(away, Edge), Edge);

        Assert.DoesNotContain(Edge, home.Speech.ProviderVoices.Keys);
    }

    [Fact]
    public void EachSlotRecordsWhichProviderItsVoicesBelongTo()
    {
        var split = Ship(OnEdge(("carrier", Edge)), Eleven);

        Assert.Equal(Eleven, split.Speech.VoicesProvider);
        Assert.Equal(Edge, split.Speech.CarrierVoicesProvider);
    }

    /// <summary>
    /// A file from before the carrier's own record existed. Stamped rather than cleared, for the
    /// reason <see cref="VoiceMemory.Reconciled"/> gives about the ship's: the voices are as
    /// likely to be right as wrong, and a genuine mismatch is caught at the seam.
    /// </summary>
    [Fact]
    public void ACarrierWithNoRecordedOwnerIsStampedRatherThanEmptied()
    {
        var unrecorded = OnEdge(("carrier", Edge)) with
        {
            Speech = OnEdge(("carrier", Edge)).Speech with { CarrierVoicesProvider = null },
        };

        var after = VoiceMemory.Reconciled(unrecorded);

        Assert.Equal(Edge, after.Speech.CarrierVoicesProvider);
        Assert.Equal("en-GB-RyanNeural", after.Speech.CarrierCaptainVoice);
    }
}
