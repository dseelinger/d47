using D47.Core.Audio;
using D47.Core.Configuration;
using Xunit;

namespace D47.Core.Tests.Configuration;

/// <summary>
/// Switching provider and switching back costs nothing (Phase 19, "Remember which voice
/// you chose for each provider").
/// <para>
/// The behaviour this joins is <see cref="VoicesAreProviderScopedTests"/>, which is still right:
/// a voice id means nothing to a provider that did not issue it, so the live slots are emptied on
/// a switch. What was wrong was the other half — they were emptied into nothing, so a Commander
/// who selected ElevenLabs to hear what it sounded like and went back to Edge had lost the ship's
/// own voice, both named roles and eleven paired cores, and the pairing pass then re-picked all
/// eleven from scratch.
/// </para>
/// </summary>
public class VoicesAreRememberedPerProviderTests
{
    private const string Edge = TtsProviderCatalog.EdgeId;
    private const string Eleven = TtsProviderCatalog.ElevenLabsId;

    /// <summary>
    /// Settings with a full set of Edge choices, recorded as Edge's.
    /// <para>
    /// <b>Every slot follows the ship's provider</b>, which is what an empty
    /// <see cref="SpeechSettings.GroupProviders"/> means since Phase 57 — a Commander who has put
    /// them all back onto one. That is the configuration this file's behaviour is about: the
    /// carrier moving with the companion is only true while the carrier is following it, and
    /// <c>VoicesFollowTheirOwnSlotTests</c> covers the case where it is not.
    /// </para>
    /// <para>
    /// Written rather than absent, because absent means "before Phase 57" and reconciling such a
    /// file migrates it — which is a change, and this file is about the ones that are not.
    /// </para>
    /// </summary>
    private static D47Settings OnEdge() => new()
    {
        Speech = new SpeechSettings
        {
            Provider = Edge,
            VoicesProvider = Edge,
            CarrierVoicesProvider = Edge,
            GroupProviders = new Dictionary<string, string>(),
            Voice = "en-US-RogerNeural",
            CarrierCaptainVoice = "en-GB-RyanNeural",
            TowerVoice = "en-AU-NatashaNeural",
        },
        Persona = new PersonaSettings
        {
            VoicesPaired = true,
            Voices = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["warden"] = "en-GB-RyanNeural",
                ["archivist"] = "en-GB-SoniaNeural",
            },
        },
    };

    private static D47Settings Select(D47Settings settings, string provider) =>
        VoiceMemory.Reconciled(settings with { Speech = settings.Speech with { Provider = provider } });

    [Fact]
    public void SwitchingAwayStillEmptiesEveryLiveSlot()
    {
        var after = Select(OnEdge(), Eleven);

        Assert.Null(after.Speech.Voice);
        Assert.Null(after.Speech.CarrierCaptainVoice);
        Assert.Null(after.Speech.TowerVoice);
        Assert.Empty(after.Persona.Voices);
        Assert.False(after.Persona.VoicesPaired);
    }

    [Fact]
    public void AndFilesThemUnderTheProviderTheyBelongedTo()
    {
        var after = Select(OnEdge(), Eleven);

        var kept = Assert.Contains(Edge, after.Speech.ProviderVoices);

        Assert.Equal("en-US-RogerNeural", kept.Ship);
        Assert.Equal("en-GB-RyanNeural", kept.CarrierCaptain);
        Assert.Equal("en-AU-NatashaNeural", kept.Tower);
        Assert.Equal("en-GB-SoniaNeural", kept.Cores["archivist"]);
        Assert.True(kept.Paired);
    }

    [Fact]
    public void GoingBackPutsAllOfItWhereItWas()
    {
        var there = Select(OnEdge(), Eleven);
        var back = Select(there, Edge);

        Assert.Equal("en-US-RogerNeural", back.Speech.Voice);
        Assert.Equal("en-GB-RyanNeural", back.Speech.CarrierCaptainVoice);
        Assert.Equal("en-AU-NatashaNeural", back.Speech.TowerVoice);
        Assert.Equal(2, back.Persona.Voices.Count);
        Assert.Equal("en-GB-RyanNeural", back.Persona.Voices["warden"]);
    }

    /// <summary>
    /// The field the item is really about. Restoring eleven pairings and then letting the pairing
    /// pass run over them would put the Commander back where they started, one launch later.
    /// </summary>
    [Fact]
    public void AndDoesNotAskForThemToBePairedAgain()
    {
        var back = Select(Select(OnEdge(), Eleven), Edge);

        Assert.True(back.Persona.VoicesPaired);
    }

    [Fact]
    public void WhatWasChosenOnTheOtherProviderIsKeptToo()
    {
        var onEleven = Select(OnEdge(), Eleven) with { };

        // Choose on ElevenLabs, then go back to Edge, then to ElevenLabs again.
        onEleven = onEleven with
        {
            Speech = onEleven.Speech with { Voice = "JBFqnCBsd6RMkjVDRZzb" },
            Persona = onEleven.Persona with
            {
                VoicesPaired = true,
                Voices = new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["warden"] = "JBFqnCBsd6RMkjVDRZzb",
                },
            },
        };

        var backOnEdge = Select(onEleven, Edge);
        var elevenAgain = Select(backOnEdge, Eleven);

        Assert.Equal("JBFqnCBsd6RMkjVDRZzb", elevenAgain.Speech.Voice);
        Assert.Equal("JBFqnCBsd6RMkjVDRZzb", elevenAgain.Persona.Voices["warden"]);

        // And Edge's are back in the stash rather than gone.
        Assert.Contains(Edge, elevenAgain.Speech.ProviderVoices);
    }

    /// <summary>
    /// The stash holds the providers not in use and nothing else. An entry for the provider that
    /// is live would be restored a second time, over whatever has been chosen since.
    /// </summary>
    [Fact]
    public void TheSelectedProviderIsNeverInTheStash()
    {
        var there = Select(OnEdge(), Eleven);

        Assert.DoesNotContain(Eleven, there.Speech.ProviderVoices);
        Assert.DoesNotContain(Edge, Select(there, Edge).Speech.ProviderVoices.Keys);
    }

    [Fact]
    public void SwitchingAwayFromAProviderNothingWasChosenOnLeavesNoEntry()
    {
        var fresh = new D47Settings
        {
            Speech = new SpeechSettings
            {
                Provider = Edge,
                VoicesProvider = Edge,
                CarrierVoicesProvider = Edge,
                GroupProviders = new Dictionary<string, string>(),
            },
        };

        // An entry that restores nothing is a row in a file the Commander reads.
        Assert.Empty(Select(fresh, Eleven).Speech.ProviderVoices);
    }

    /// <summary>
    /// A settings file written before d47 recorded whose voices these are. They are dropped
    /// rather than filed: filing them under a guess would offer one provider's ids back as
    /// another's, which is the exact failure the whole mechanism exists to stop.
    /// </summary>
    [Fact]
    public void VoicesWithNoRecordedOwnerAreDroppedRatherThanFiledUnderAGuess()
    {
        var unrecorded = OnEdge() with
        {
            Speech = OnEdge().Speech with
            {
                VoicesProvider = null,
                CarrierVoicesProvider = null,
                Provider = Eleven,
            },
        };

        var after = VoiceMemory.Reconciled(unrecorded);

        // Stamped, not cleared — see VoiceMemory.Reconciled. The voices stay live because d47
        // has no reason to believe they are wrong, and a genuine mismatch is caught at the seam.
        Assert.Equal(Eleven, after.Speech.VoicesProvider);
        Assert.Equal("en-US-RogerNeural", after.Speech.Voice);
        Assert.Empty(after.Speech.ProviderVoices);
    }

    [Fact]
    public void ReconcilingWhenNothingHasChangedAnswersTheSameInstance()
    {
        var settings = OnEdge();

        // The caller compares by reference to decide whether to write. An equal-but-new instance
        // would be a settings save and an announcement on every single apply.
        Assert.Same(settings, VoiceMemory.Reconciled(settings));
    }

    [Fact]
    public void NothingOutsideTheVoicesMoves()
    {
        var before = OnEdge() with
        {
            Speech = OnEdge().Speech with
            {
                Rate = 1.15,
                OutputDevice = "Headphones",
                CuesEnabled = false,
                ProviderRates = new Dictionary<string, double> { [Edge] = 1.15 },
            },
        };

        var after = Select(before, Eleven);

        Assert.Equal(before.Speech.Rate, after.Speech.Rate);
        Assert.Equal(before.Speech.OutputDevice, after.Speech.OutputDevice);
        Assert.Equal(before.Speech.CuesEnabled, after.Speech.CuesEnabled);
        Assert.Equal(before.Speech.ProviderRates, after.Speech.ProviderRates);
        Assert.Equal(before.Ui, after.Ui);
        Assert.Equal(before.Llm, after.Llm);
        Assert.Equal(before.Persona.Id, after.Persona.Id);
    }

    /// <summary>
    /// Provider ids are compared case-insensitively everywhere else, and a hand-edited file
    /// spelling one "Edge" must find the same entry rather than a second one.
    /// </summary>
    [Fact]
    public void TheStashIsKeyedTheWayProviderIdsAreCompared()
    {
        var there = Select(OnEdge(), Eleven);

        Assert.Contains("EDGE", there.Speech.ProviderVoices);
    }
}
