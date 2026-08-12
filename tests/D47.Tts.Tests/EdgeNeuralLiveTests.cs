using D47.Core.Audio;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Tts.Tests;

/// <summary>
/// The one thing that cannot be proven offline: that the undocumented endpoint still speaks
/// the protocol in <see cref="EdgeProtocol"/>.
/// <para>
/// Opt-in via <c>D47_TTS_LIVE=1</c>, so `dotnet test` stays hermetic and CI never depends on
/// a third party staying up. Run it when the voice stops working and you need to know whether
/// the endpoint moved or d47 did:
/// </para>
/// <code>
/// D47_TTS_LIVE=1 dotnet test tests/D47.Tts.Tests
/// </code>
/// </summary>
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

        var voices = await provider.ListVoicesAsync(timeout.Token);

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

        // A seven-word sentence is a second or two of speech. Anything under half a second
        // means the socket closed early and returned a fragment, which is the failure mode
        // worth catching — it does not throw, it just says less than it was asked to.
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
