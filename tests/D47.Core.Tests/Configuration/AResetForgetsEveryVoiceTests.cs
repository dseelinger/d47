using D47.Core.Audio;
using D47.Core.Configuration;
using Xunit;

namespace D47.Core.Tests.Configuration;

/// <summary>The reset row forgets every voice on every provider, so each is paired again from its list.</summary>
public class AResetForgetsEveryVoiceTests
{
    private const string Eleven = TtsProviderCatalog.ElevenLabsId;
    private const string Edge = TtsProviderCatalog.EdgeId;

    /// <summary>Paired, hand-picked, and a second provider with choices of its own filed away.</summary>
    private static D47Settings Settings() => new()
    {
        Speech = new SpeechSettings
        {
            Provider = Eleven,
            VoicesProvider = Eleven,
            Voice = "ship-voice",
            CarrierCaptainVoice = "captain-voice",
            TowerVoice = "tower-voice",
            ProviderVoices = new Dictionary<string, VoiceChoices>(StringComparer.OrdinalIgnoreCase)
            {
                [Edge] = new()
                {
                    Ship = "en-GB-RyanNeural",
                    CarrierCaptain = "en-US-ChristopherNeural",
                    Tower = "en-AU-NatashaNeural",
                    Cores = new Dictionary<string, string>(StringComparer.Ordinal) { ["warden"] = "en-US-DavisNeural" },
                    PairedCores = new Dictionary<string, string>(StringComparer.Ordinal) { ["warden"] = "en-US-DavisNeural" },
                    Paired = true,
                },
            },
        },
        Persona = new PersonaSettings
        {
            VoicesPaired = true,
            Voices = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["warden"] = "hand-picked-voice",
                ["cora"] = "paired-voice",
            },
            PairedVoices = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["warden"] = "earlier-paired-voice",
                ["cora"] = "paired-voice",
            },
        },
    };

    [Fact]
    public void ARecordedPairingIsDiscardedNotRestored()
    {
        var (after, _) = VoiceMemory.Forgotten(Settings());

        Assert.Empty(after.Persona.Voices);
        Assert.Empty(after.Persona.PairedVoices);
        Assert.False(after.Persona.VoicesPaired);
    }

    [Fact]
    public void TheShipTheCarrierCaptainAndTheTowerAreForgottenToo()
    {
        var (after, _) = VoiceMemory.Forgotten(Settings());

        Assert.Null(after.Speech.Voice);
        Assert.Null(after.Speech.CarrierCaptainVoice);
        Assert.Null(after.Speech.TowerVoice);
    }

    [Fact]
    public void AStashedProviderIsForgottenSoItIsPairedWhenNextSelected()
    {
        var (after, providers) = VoiceMemory.Forgotten(Settings());
        var edge = after.Speech.ProviderVoices[Edge];

        Assert.Empty(edge.Cores);
        Assert.Empty(edge.PairedCores);
        Assert.False(edge.Paired);
        Assert.Null(edge.Ship);
        Assert.Null(edge.CarrierCaptain);
        Assert.Null(edge.Tower);
        Assert.Equal(2, providers);
    }

    [Fact]
    public void SwitchingProviderAfterAResetBringsNothingBack()
    {
        var (reset, _) = VoiceMemory.Forgotten(Settings());
        var switched = VoiceMemory.Reconciled(reset with { Speech = reset.Speech with { Provider = Edge } });

        Assert.Empty(switched.Persona.Voices);
        Assert.False(switched.Persona.VoicesPaired);
    }

    [Fact]
    public void TheRowSaysWhatWasPairedAndOnHowManyProviders()
    {
        var said = VoiceMemory.ForgottenSaid(11, 2, 3, "Edge Neural", byModel: false);

        Assert.Contains("all 3 voice providers", said, StringComparison.Ordinal);
        Assert.Contains("Paired 11 cores and the carrier captain and the tower from Edge Neural's list", said, StringComparison.Ordinal);
        Assert.Contains("without a language model", said, StringComparison.Ordinal);
    }

    [Fact]
    public void BeforeTheListArrivesTheRowSaysPairingIsStillToCome()
    {
        var said = VoiceMemory.ForgottenSaid(0, 0, 1, "Edge Neural", byModel: true);

        Assert.Contains("voice provider in use", said, StringComparison.Ordinal);
        Assert.Contains("has not arrived", said, StringComparison.Ordinal);
    }
}
