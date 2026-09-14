using D47.Core.Audio;
using D47.Core.Speech;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Audio;

/// <summary>
/// The rule lives in <see cref="SpokenAddress"/> (#196); these tests are that a
/// <see cref="SpeechPipeline"/> handed one actually uses it, and only on what reaches the provider.
/// </summary>
public class TheProviderDoesNotHearARepeatedCommanderTests
{
    private sealed class FakeClock : IWallClock
    {
        public DateTimeOffset UtcNow { get; set; } = new(2026, 9, 14, 14, 47, 0, TimeSpan.Zero);
    }

    private static (AudioArbiter Arbiter, RecordingAudioSink Sink) Build()
    {
        var sink = new RecordingAudioSink();
        return (new AudioArbiter(sink, NullLogger<AudioArbiter>.Instance).Start(), sink);
    }

    [Fact]
    public async Task ADroppedAddressReachesTheProviderWithoutItButTheCaptionKeepsIt()
    {
        var (arbiter, sink) = Build();
        var tts = new FakeTtsProvider();
        var address = new SpokenAddress(new FakeClock()) { CommanderName = "Doug DEPARAGON" };

        await using (var first = new SpeechPipeline(
            arbiter, tts, VoiceSelection.Default, "turn-1", NullLogger.Instance, address: address))
        {
            first.Push("welcome home, Commander DEPARAGON.");
            await first.CompleteAsync();
        }

        // Let the first clip finish so the arbiter moves straight on to the second.
        sink.CompleteCurrent();

        await using var second = new SpeechPipeline(
            arbiter, tts, VoiceSelection.Default, "turn-2", NullLogger.Instance, address: address);

        second.Push("Standard starport protocol applies as always, Commander.");
        await second.CompleteAsync();

        // A different group after one that finished on its own gets a short silence ahead of it (#44).
        sink.CompleteCurrent();

        Assert.Contains("welcome home, Commander DEPARAGON.", tts.Requested);
        Assert.Contains("Standard starport protocol applies as always.", tts.Requested);

        // The caption is unaffected — the written form keeps "Commander" (#196 is spoken-form only).
        Assert.Equal(
            "Standard starport protocol applies as always, Commander.",
            arbiter.Activity.Caption);
    }

    [Fact]
    public async Task ASentenceThatIsOnlyTheAddressIsNotSentToTheProviderAtAll()
    {
        var (arbiter, _) = Build();
        var tts = new FakeTtsProvider();
        var address = new SpokenAddress(new FakeClock()) { CommanderName = "Doug DEPARAGON" };

        await using (var first = new SpeechPipeline(
            arbiter, tts, VoiceSelection.Default, "turn-1", NullLogger.Instance, address: address))
        {
            first.Push("welcome home, Commander DEPARAGON.");
            await first.CompleteAsync();
        }

        await using var second = new SpeechPipeline(
            arbiter, tts, VoiceSelection.Default, "turn-2", NullLogger.Instance, address: address);

        second.Push("Commander.");
        await second.CompleteAsync();

        Assert.DoesNotContain(tts.Requested, text => !text.Contains("welcome home", StringComparison.Ordinal));
    }

    [Fact]
    public async Task WithNoAddressCooldownGivenNothingIsDropped()
    {
        var (arbiter, _) = Build();
        var tts = new FakeTtsProvider();

        await using (var first = new SpeechPipeline(
            arbiter, tts, VoiceSelection.Default, "turn-1", NullLogger.Instance))
        {
            first.Push("welcome home, Commander DEPARAGON.");
            await first.CompleteAsync();
        }

        await using var second = new SpeechPipeline(
            arbiter, tts, VoiceSelection.Default, "turn-2", NullLogger.Instance);

        second.Push("Standard starport protocol applies as always, Commander.");
        await second.CompleteAsync();

        Assert.Contains("Standard starport protocol applies as always, Commander.", tts.Requested);
    }
}
