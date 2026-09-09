using D47.Core.Audio;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Tts.Tests;

/// <summary>Live: the undocumented endpoint still speaks <see cref="EdgeProtocol"/>.</summary>
public class EdgeNeuralLiveTests
{
    private static bool Enabled =>
        Environment.GetEnvironmentVariable("D47_TTS_LIVE") == "1";

    private static EdgeNeuralTtsProvider Provider() =>
        new(NullLogger<EdgeNeuralTtsProvider>.Instance);

    [Fact]
    public async Task TheVoiceListStillLoads()
    {
        Assert.SkipUnless(Enabled, "set D47_TTS_LIVE=1 to run tests that contact Microsoft");

        using var provider = Provider();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));

        var voices = (await provider.ListVoicesAsync(timeout.Token)).Voices;

        Assert.NotEmpty(voices);
        Assert.Contains(voices, voice => voice.Id == EdgeNeuralTtsProvider.DefaultVoice);
        Assert.Contains(voices, voice => voice.Locale.StartsWith("en-", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ASentenceComesBackAsAudioInTheArbitersFormat()
    {
        Assert.SkipUnless(Enabled, "set D47_TTS_LIVE=1 to run tests that contact Microsoft");

        using var provider = Provider();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));

        var clip = await provider.SynthesizeAsync(
            "Docking granted, Commander. Pad three.",
            VoiceSelection.Default,
            timeout.Token);

        Assert.Equal(AudioFormat.Standard, clip.Format);

        // A seven-word sentence is a second or two of speech.
        Assert.True(
            clip.Duration > TimeSpan.FromSeconds(0.5),
            $"got {clip.Duration.TotalSeconds:0.00}s of audio, which is too short to be that sentence");

        Assert.True(clip.Duration < TimeSpan.FromSeconds(15), "implausibly long for one sentence");
    }

    [Fact]
    public async Task AnUnknownVoiceFailsAsATtsExceptionRatherThanSomethingElse()
    {
        Assert.SkipUnless(Enabled, "set D47_TTS_LIVE=1 to run tests that contact Microsoft");

        using var provider = Provider();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));

        await Assert.ThrowsAsync<TtsException>(() => provider.SynthesizeAsync(
            "This should not be spoken.",
            new VoiceSelection("en-GB-NoSuchVoiceNeural"),
            timeout.Token));
    }
}
