using System.Net;
using D47.Core.Audio;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Tts.Tests;

public class ElevenLabsTests
{
    private static ElevenLabsTtsProvider Provider(string? key = null) =>
        new(() => key, NullLogger<ElevenLabsTtsProvider>.Instance);

    [Theory]
    [InlineData(1.0, 1.0)]
    [InlineData(0.9, 0.9)]

    // The service rejects an out-of-range speed outright rather than clamping, and a rejected request arrives as silence.
    [InlineData(1.6, 1.2)]
    [InlineData(0.5, 0.7)]
    public void TheRateIsClampedToWhatTheServiceAccepts(double rate, double expected)
    {
        Assert.Equal(expected, ElevenLabsTtsProvider.SpeedFor(rate));
    }

    [Fact]
    public async Task WithNoKeyTheVoiceListIsEmptyRatherThanAnError()
    {
        // An empty list is a supported answer: the picker still lets the Commander type a value.
        using var provider = Provider();

        var listed = await provider.ListVoicesAsync(TestContext.Current.CancellationToken);

        Assert.Empty(listed.Voices);

        Assert.Equal(VoiceListing.NoKey, listed.Listing);
        Assert.Contains("API key", listed.WhyEmpty("ElevenLabs")!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task AFamousVoiceIsNotOffered()
    {
        const string Listing = """
            {"voices":[
              {"voice_id":"ordinary","name":"Brian - Clean, Professional and Balanced","category":"professional"},
              {"voice_id":"famous","name":"Burt Reynolds™ - Masculine Storyteller","category":"professional"},
              {"voice_id":"premade","name":"George - Warm, Captivating Storyteller","category":"premade"}
            ]}
            """;

        using var http = new HttpClient(new CannedResponse(Listing));
        using var provider = new ElevenLabsTtsProvider(
            () => "sk_test", NullLogger<ElevenLabsTtsProvider>.Instance, http);

        var voices = (await provider.ListVoicesAsync(TestContext.Current.CancellationToken)).Voices;

        Assert.Equal(["ordinary", "premade"], voices.Select(voice => voice.Id));
    }

    private sealed class CannedResponse(string body) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(body, System.Text.Encoding.UTF8, "application/json"),
            });
    }

    [Fact]
    public async Task WithNoKeySynthesisSaysWhatIsMissing()
    {
        using var provider = Provider();

        var failure = await Assert.ThrowsAsync<TtsException>(
            () => provider.SynthesizeAsync("test", VoiceSelection.Default, TestContext.Current.CancellationToken));

        Assert.Contains("API key", failure.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task WithNoVoiceChosenItSaysSoRatherThanGuessingAnId()
    {
        // Deliberately not a hardcoded fallback voice.
        using var provider = Provider("a-key-shaped-string");

        var failure = await Assert.ThrowsAsync<TtsException>(
            () => provider.SynthesizeAsync("test", VoiceSelection.Default, TestContext.Current.CancellationToken));

        Assert.Contains("voice", failure.Message, StringComparison.OrdinalIgnoreCase);
    }
}

/// <summary>The half that needs a real account.</summary>
public class ElevenLabsLiveTests
{
    private static string? Key => Environment.GetEnvironmentVariable("D47_ELEVENLABS_KEY");

    private static ElevenLabsTtsProvider Provider() =>
        new(() => Key, NullLogger<ElevenLabsTtsProvider>.Instance);

    [Fact]
    public async Task TheAccountsVoicesLoad()
    {
        Assert.SkipWhen(
            string.IsNullOrWhiteSpace(Key),
            "set D47_ELEVENLABS_KEY to run tests that contact ElevenLabs");

        using var provider = Provider();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));

        var voices = (await provider.ListVoicesAsync(timeout.Token)).Voices;

        Assert.NotEmpty(voices);
        Assert.All(voices, voice => Assert.False(string.IsNullOrWhiteSpace(voice.Id)));
        Assert.All(voices, voice => Assert.False(string.IsNullOrWhiteSpace(voice.Label)));
    }

    [Fact]
    public async Task ASentenceComesBackAsAudioInTheArbitersFormat()
    {
        Assert.SkipWhen(
            string.IsNullOrWhiteSpace(Key),
            "set D47_ELEVENLABS_KEY to run tests that contact ElevenLabs");

        using var provider = Provider();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(60));

        var voices = (await provider.ListVoicesAsync(timeout.Token)).Voices;
        Assert.NotEmpty(voices);

        var clip = await provider.SynthesizeAsync(
            "Frame shift charged. Whenever you're ready, Commander.",
            new VoiceSelection(voices[0].Id),
            timeout.Token);

        // Whatever the provider sends, what reaches the arbiter is 48 kHz mono PCM.
        Assert.Equal(AudioFormat.Standard, clip.Format);
        Assert.True(clip.Pcm.Length > 0);
        Assert.True(clip.Duration > TimeSpan.FromSeconds(1), $"only {clip.Duration} of audio came back");
    }

    [Fact]
    public async Task AWrongKeySaysSoInWordsAnAccountHolderCanActOn()
    {
        Assert.SkipWhen(
            string.IsNullOrWhiteSpace(Key),
            "set D47_ELEVENLABS_KEY to run tests that contact ElevenLabs");

        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(60));

        // A real voice id with a wrong key, which is the only way to test the key at all: ElevenLabs
        // validates the voice id *first*, so a request with both wrong comes back as a 400 about the voice
        // and says nothing about the key.
        using var real = Provider();
        var voices = (await real.ListVoicesAsync(timeout.Token)).Voices;
        Assert.NotEmpty(voices);

        using var wrongKey = new ElevenLabsTtsProvider(
            () => "sk_definitely_not_a_real_key",
            NullLogger<ElevenLabsTtsProvider>.Instance);

        var failure = await Assert.ThrowsAsync<TtsException>(
            () => wrongKey.SynthesizeAsync("test", new VoiceSelection(voices[0].Id), timeout.Token));

        // "401" is not something a Commander can act on.
        Assert.Contains("API key", failure.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ABadVoiceIdIsReportedAsTheVoiceRatherThanAsAStatusCode()
    {
        Assert.SkipWhen(
            string.IsNullOrWhiteSpace(Key),
            "set D47_ELEVENLABS_KEY to run tests that contact ElevenLabs");

        using var provider = Provider();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));

        var failure = await Assert.ThrowsAsync<TtsException>(
            () => provider.SynthesizeAsync("test", new VoiceSelection("not-a-voice-id"), timeout.Token));

        // ElevenLabs answers 400 here, so a status-code mapping leaves the Commander guessing.
        Assert.DoesNotContain("it answered", failure.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("not-a-voice-id", failure.Message, StringComparison.Ordinal);
    }

    [Theory]

    // The voice is validated ahead of the key, so a bad one is a 400 rather than the 404 you would guess.
    [InlineData(HttpStatusCode.BadRequest, null, TtsFault.VoiceRejected)]
    [InlineData(HttpStatusCode.NotFound, null, TtsFault.VoiceRejected)]

    // The service naming the id back is about the voice whatever it arrives as.
    [InlineData(HttpStatusCode.UnprocessableEntity,
        "An invalid ID has been received: 'en-US-RogerNeural'. Make sure to provide a correct one.",
        TtsFault.VoiceRejected)]

    // A voice the account holds and this API will not speak.
    [InlineData(HttpStatusCode.Forbidden,
        "Famous voices can only be used within the Reader App.",
        TtsFault.VoiceRejected)]

    // And these are not.
    [InlineData(HttpStatusCode.Unauthorized, "Invalid API key", TtsFault.Unknown)]
    [InlineData(HttpStatusCode.PaymentRequired, "quota exceeded", TtsFault.Unknown)]
    [InlineData(HttpStatusCode.TooManyRequests, null, TtsFault.Unknown)]
    [InlineData(HttpStatusCode.InternalServerError, null, TtsFault.Unknown)]
    public void OnlyARefusalAboutTheVoiceIsOneToActOn(
        HttpStatusCode status,
        string? said,
        TtsFault expected)
    {
        Assert.Equal(expected, ElevenLabsTtsProvider.FaultFor(status, said, "en-US-RogerNeural"));
    }

}
