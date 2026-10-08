using D47.Core.Audio;
using D47.Core.Capabilities.Builtin;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Audio;

/// <summary>
/// A provider names its own rows; a pipeline speaking for a cast role names the row that role's voice is
/// chosen in when the voice is what is missing (#952).
/// </summary>
public class ACastVoiceFailureNamesItsOwnRowTests
{
    private sealed class Refusing(TtsException refusal) : ITtsProvider
    {
        public string Id => "refusing";

        public string Name => "Refusing";

        public Task<VoiceCatalogue> ListVoicesAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(VoiceCatalogue.Of([]));

        public Task<AudioClip> SynthesizeAsync(
            string text, VoiceSelection voice, CancellationToken cancellationToken = default) =>
            Task.FromException<AudioClip>(refusal);
    }

    private static readonly TtsException NoVoice = new(
        "No Chatterbox voice has been chosen. Pick one in Settings.",
        fault: TtsFault.NoVoice,
        settingKey: SpeechCapability.ChatterboxVoiceKey);

    private static readonly TtsException NotDownloaded = new(
        "Chatterbox is not downloaded yet. Download it in Settings.",
        settingKey: SpeechCapability.ChatterboxVoiceKey);

    /// <summary>Through the meter, as the app wraps its providers.</summary>
    private static async Task<SynthesisFailure> FailureOf(TtsException refusal, VoiceRole role)
    {
        var arbiter = new AudioArbiter(new RecordingAudioSink(), NullLogger<AudioArbiter>.Instance).Start();
        await using var pipeline = new SpeechPipeline(
            arbiter,
            new MeteredTtsProvider(new Refusing(refusal), new SpeechSpend()),
            VoiceSelection.Default,
            "turn-1",
            NullLogger.Instance,
            voiceRow: SpeechCapability.VoiceRowFor(role));

        var reported = new List<SynthesisFailure>();
        pipeline.SynthesisFailed += reported.Add;

        pipeline.Push("Something to say. ");
        await pipeline.CompleteAsync();

        return Assert.Single(reported);
    }

    [Fact]
    public async Task TheShipsOwnVoiceKeepsTheProvidersRow()
    {
        var failure = await FailureOf(NoVoice, VoiceRole.ShipAi);

        Assert.Equal(NoVoice.Message, failure.Text);
        Assert.Equal(SpeechCapability.ChatterboxVoiceKey, failure.SettingKey);
    }

    [Theory]
    [InlineData(VoiceRole.Narrator, SpeechCapability.NarratorVoiceKey)]
    [InlineData(VoiceRole.TowerControl, SpeechCapability.TowerVoiceKey)]
    [InlineData(VoiceRole.CarrierCaptain, SpeechCapability.CarrierCaptainVoiceKey)]
    public async Task ACastRoleWithNoVoiceNamesItsOwnRow(VoiceRole role, string row)
    {
        var failure = await FailureOf(NoVoice, role);

        Assert.Equal(row, failure.SettingKey);
    }

    [Fact]
    public async Task AFailureThatIsNotTheVoiceKeepsTheProvidersRowForACastRole()
    {
        var failure = await FailureOf(NotDownloaded, VoiceRole.TowerControl);

        Assert.Equal(SpeechCapability.ChatterboxVoiceKey, failure.SettingKey);
    }
}
