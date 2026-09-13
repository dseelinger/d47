using D47.Core.Audio;
using D47.Core.Configuration;
using Xunit;

namespace D47.Core.Tests.Configuration;

/// <summary>
/// A voice a Commander hand-picked overwrites the same slot the pairing pass wrote, so undoing it
/// needs the pairing's choice kept somewhere else (#85).
/// </summary>
public class AHandPickedVoiceCanBeResetToItsPairingTests
{
    private const string Eleven = TtsProviderCatalog.ElevenLabsId;
    private const string Edge = TtsProviderCatalog.EdgeId;

    /// <summary>A core paired to one voice, then hand-picked to another.</summary>
    private static D47Settings Settings() => new()
    {
        Speech = new SpeechSettings
        {
            Provider = Eleven,
            CarrierCaptainVoice = "en-GB-RyanNeural",
            TowerVoice = "en-AU-NatashaNeural",
        },
        Persona = new PersonaSettings
        {
            VoicesPaired = true,
            Voices = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["warden"] = "hand-picked-voice",
                ["cora"] = "en-US-AriaNeural",
            },
            PairedVoices = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["warden"] = "en-US-GuyNeural",
                ["cora"] = "en-US-AriaNeural",
            },
        },
    };

    [Fact]
    public void AHandPickedCoreGoesBackToWhatThePairingChose()
    {
        var (after, _) = VoiceMemory.ResetToPairing(Settings());

        Assert.Equal("en-US-GuyNeural", after.Persona.Voices["warden"]);
    }

    [Fact]
    public void ACoreAlreadyOnItsPairingIsUnaffected()
    {
        var (after, _) = VoiceMemory.ResetToPairing(Settings());

        Assert.Equal("en-US-AriaNeural", after.Persona.Voices["cora"]);
    }

    [Fact]
    public void ItNeverAsksTheModelOrTheNetwork()
    {
        // No provider, no model, nothing awaitable — a pure settings transform.
        var (after, outcome) = VoiceMemory.ResetToPairing(Settings());

        Assert.NotNull(after);
        Assert.NotNull(outcome);
    }

    [Fact]
    public void ACoreWithNoRecordedPairingIsDroppedRatherThanLeftHandPicked()
    {
        var settings = Settings() with
        {
            Persona = Settings().Persona with
            {
                Voices = new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["warden"] = "en-US-GuyNeural",
                    ["sentinel"] = "an-install-from-before-this-shipped",
                },
                PairedVoices = new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["warden"] = "en-US-GuyNeural",
                },
            },
        };

        var (after, outcome) = VoiceMemory.ResetToPairing(settings);

        Assert.False(after.Persona.Voices.ContainsKey("sentinel"));
        Assert.Equal(1, outcome.PairingPending);
    }

    [Fact]
    public void DroppingAnUnrecordedCoreClearsPairedSoItRunsAgain()
    {
        var settings = Settings() with
        {
            Persona = Settings().Persona with
            {
                VoicesPaired = true,
                Voices = new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["sentinel"] = "an-install-from-before-this-shipped",
                },
                PairedVoices = new Dictionary<string, string>(StringComparer.Ordinal),
            },
        };

        var (after, _) = VoiceMemory.ResetToPairing(settings);

        Assert.False(after.Persona.VoicesPaired);
    }

    /// <summary>
    /// The reset works on whatever id a core carries, so a Commander's own written core is restored
    /// exactly like one of the eleven that ship.
    /// </summary>
    [Fact]
    public void ACommandersOwnWrittenCoreIsCoveredTooByItsOwnId()
    {
        var settings = Settings() with
        {
            Persona = Settings().Persona with
            {
                Voices = new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["my-own-core"] = "hand-picked-for-my-own-core",
                },
                PairedVoices = new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["my-own-core"] = "en-US-DavisNeural",
                },
            },
        };

        var (after, _) = VoiceMemory.ResetToPairing(settings);

        Assert.Equal("en-US-DavisNeural", after.Persona.Voices["my-own-core"]);
    }

    [Fact]
    public void WithEveryCoreRecordedPairedStaysTrue()
    {
        var (after, _) = VoiceMemory.ResetToPairing(Settings());

        Assert.True(after.Persona.VoicesPaired);
    }

    [Fact]
    public void TheCarrierAndTowerGoBackToFollowingTheShipAi()
    {
        var (after, _) = VoiceMemory.ResetToPairing(Settings());

        Assert.Null(after.Speech.CarrierCaptainVoice);
        Assert.Null(after.Speech.TowerVoice);
    }

    [Fact]
    public void EveryOtherProviderTheCommanderHasUsedIsCoveredToo()
    {
        var settings = Settings() with
        {
            Speech = Settings().Speech with
            {
                ProviderVoices = new Dictionary<string, VoiceChoices>(StringComparer.OrdinalIgnoreCase)
                {
                    [Edge] = new()
                    {
                        CarrierCaptain = "en-US-ChristopherNeural",
                        Cores = new Dictionary<string, string>(StringComparer.Ordinal)
                        {
                            ["warden"] = "a-hand-picked-edge-voice",
                        },
                        PairedCores = new Dictionary<string, string>(StringComparer.Ordinal)
                        {
                            ["warden"] = "en-US-DavisNeural",
                        },
                        Paired = true,
                    },
                },
            },
        };

        var (after, outcome) = VoiceMemory.ResetToPairing(settings);
        var edge = after.Speech.ProviderVoices[Edge];

        Assert.Equal("en-US-DavisNeural", edge.Cores["warden"]);
        Assert.Null(edge.CarrierCaptain);
        Assert.Equal(2, outcome.ProvidersTouched);
    }

    /// <summary>
    /// The switch that undid the reset before this shipped: a reset that touched only the live slot
    /// was overwritten by whatever the next provider switch restored from the stash.
    /// </summary>
    [Fact]
    public void ResettingAndThenSwitchingProviderDoesNotBringTheHandPickedVoiceBack()
    {
        var settings = Settings() with
        {
            Speech = Settings().Speech with
            {
                VoicesProvider = Eleven,
                ProviderVoices = new Dictionary<string, VoiceChoices>(StringComparer.OrdinalIgnoreCase)
                {
                    [Edge] = new()
                    {
                        Cores = new Dictionary<string, string>(StringComparer.Ordinal)
                        {
                            ["warden"] = "a-hand-picked-edge-voice",
                        },
                        PairedCores = new Dictionary<string, string>(StringComparer.Ordinal)
                        {
                            ["warden"] = "en-US-DavisNeural",
                        },
                        Paired = true,
                    },
                },
            },
        };

        var (reset, _) = VoiceMemory.ResetToPairing(settings);
        var switched = VoiceMemory.Reconciled(reset with { Speech = reset.Speech with { Provider = Edge } });

        Assert.Equal("en-US-DavisNeural", switched.Persona.Voices["warden"]);
    }

    [Fact]
    public void TheOutcomeNamesHowManyWereRestoredAndHowManyAreStillPending()
    {
        var settings = Settings() with
        {
            Persona = Settings().Persona with
            {
                Voices = new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["warden"] = "en-US-GuyNeural",
                    ["sentinel"] = "an-install-from-before-this-shipped",
                },
                PairedVoices = new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["warden"] = "en-US-GuyNeural",
                },
            },
        };

        var (_, outcome) = VoiceMemory.ResetToPairing(settings);

        Assert.Contains("One core now has the voice", outcome.Said, StringComparison.Ordinal);
        Assert.Contains("paired again", outcome.Said, StringComparison.Ordinal);
    }

    [Fact]
    public void AFreshInstallWithNothingRecordedIsToldItWillBePairedAgain()
    {
        var settings = new D47Settings
        {
            Speech = new SpeechSettings { Provider = Eleven },
            Persona = new PersonaSettings
            {
                VoicesPaired = true,
                Voices = new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["warden"] = "whatever-was-there-before-this-shipped",
                },
            },
        };

        var (after, outcome) = VoiceMemory.ResetToPairing(settings);

        Assert.Empty(after.Persona.Voices);
        Assert.False(after.Persona.VoicesPaired);
        Assert.Equal(1, outcome.PairingPending);
    }
}
