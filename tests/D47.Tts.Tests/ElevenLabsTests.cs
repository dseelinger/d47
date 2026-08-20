using System.Net;
using D47.Core.Audio;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Tts.Tests;

/// <summary>
/// What can be checked without an account.
/// </summary>
public class ElevenLabsTests
{
    private static ElevenLabsTtsProvider Provider(string? key = null) =>
        new(() => key, NullLogger<ElevenLabsTtsProvider>.Instance);

    [Theory]
    [InlineData(1.0, 1.0)]
    [InlineData(0.9, 0.9)]

    // d47's rate range is wider than this provider's, and it rejects an out-of-range speed
    // outright rather than clamping — a rejected request arrives as silence, which is the worst
    // possible symptom because it is indistinguishable from having nothing to say.
    [InlineData(1.6, 1.2)]
    [InlineData(0.5, 0.7)]
    public void TheRateIsClampedToWhatTheServiceAccepts(double rate, double expected)
    {
        Assert.Equal(expected, ElevenLabsTtsProvider.SpeedFor(rate));
    }

    [Fact]
    public async Task WithNoKeyTheVoiceListIsEmptyRatherThanAnError()
    {
        // An empty list is a supported answer: the picker still lets the Commander keep the
        // current value or type one (list.md Phase 4). A provider with no key is a capability
        // that is off, not a failure to handle.
        using var provider = Provider();

        var listed = await provider.ListVoicesAsync(TestContext.Current.CancellationToken);

        Assert.Empty(listed.Voices);

        // And says which empty it is. "No key" is the one the Commander can fix from the row
        // above, and it used to be indistinguishable from an outage
        // (docs/spikes/elevenlabs-voice-sources.md §3).
        Assert.Equal(VoiceListing.NoKey, listed.Listing);
        Assert.Contains("API key", listed.WhyEmpty("ElevenLabs")!, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// A voice the API will not speak is never offered (remediation.md 17, item 13).
    /// <para>
    /// Reported from the log: *"ElevenLabs could not speak … Famous voices can only be used within
    /// the Reader App"*, and what a Commander experiences is d47 going quiet, because a sentence
    /// that will not synthesise is dropped.
    /// </para>
    /// <para>
    /// <b>The fixture is the finding.</b> The obvious filter is `category` and it cannot work: the
    /// ™ voices come back `professional`, exactly like the several hundred ordinary voices beside
    /// them — which is why this fixture gives both the same category. The trademark in the name is
    /// the only thing that separates them.
    /// </para>
    /// </summary>
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
        // Deliberately not a hardcoded fallback voice. Edge can default to a named voice because
        // its catalogue is fixed and public; an ElevenLabs account's voice list is its own, so a
        // guessed id would fail as a 404 that reads like an outage.
        using var provider = Provider("a-key-shaped-string");

        var failure = await Assert.ThrowsAsync<TtsException>(
            () => provider.SynthesizeAsync("test", VoiceSelection.Default, TestContext.Current.CancellationToken));

        Assert.Contains("voice", failure.Message, StringComparison.OrdinalIgnoreCase);
    }
}

/// <summary>
/// The half that needs a real account (list.md Phase 11, "ElevenLabs").
/// <para>
/// Opt-in via <c>D47_ELEVENLABS_KEY</c>, so <c>dotnet test</c> stays hermetic, CI never depends
/// on a paid third party, and nobody's character quota is spent by a routine test run:
/// </para>
/// <code>
/// D47_ELEVENLABS_KEY=sk_... dotnet test tests/D47.Tts.Tests
/// </code>
/// <para>
/// The key is read from the environment rather than from d47's own secret store on purpose. The
/// store is DPAPI-encrypted for one Windows account and lives beside an installed executable;
/// a test run should not need one to exist, and should not be able to read the Commander's.
/// </para>
/// </summary>
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

        // The whole contract of the seam: whatever the provider sends, what reaches the arbiter
        // is 48 kHz mono PCM. Anything else plays at the wrong pitch.
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

        // A real voice id with a wrong key, which is the only way to test the key at all:
        // ElevenLabs validates the voice id *first*, so a request with both wrong comes back as
        // a 400 about the voice and says nothing about the key. The first version of this test
        // passed a made-up id and was quietly asserting nothing.
        using var real = Provider();
        var voices = (await real.ListVoicesAsync(timeout.Token)).Voices;
        Assert.NotEmpty(voices);

        using var wrongKey = new ElevenLabsTtsProvider(
            () => "sk_definitely_not_a_real_key",
            NullLogger<ElevenLabsTtsProvider>.Instance);

        var failure = await Assert.ThrowsAsync<TtsException>(
            () => wrongKey.SynthesizeAsync("test", new VoiceSelection(voices[0].Id), timeout.Token));

        // "401" is not something a Commander can act on. The service says "Invalid API key",
        // which is, and that is what the provider now surfaces in preference to its own mapping.
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

        // This is the case that exposed the bug: ElevenLabs answers 400 here, so every
        // status-code mapping worth writing produces "it answered 400" and the Commander is
        // left guessing. The service's own message names the id and says what to do.
        Assert.DoesNotContain("it answered", failure.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("not-a-voice-id", failure.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// A refusal about the voice is told apart from a refusal about anything else, because only
    /// the first is one d47 repairs by itself.
    /// <para>
    /// Reported from a running build: an Edge voice id survived a switch to this provider, and
    /// the answer — "an invalid ID has been received: 'en-US-RogerNeural'" — arrived on every
    /// sentence of every turn on every launch. It is the id in the message that makes that one
    /// unmistakable, and the status it comes with that makes the general case decidable.
    /// </para>
    /// </summary>
    [Theory]

    // The voice is validated ahead of the key, so a bad one is a 400 rather than the 404 you
    // would guess. Verified against the live API.
    [InlineData(HttpStatusCode.BadRequest, null, TtsFault.VoiceRejected)]
    [InlineData(HttpStatusCode.NotFound, null, TtsFault.VoiceRejected)]

    // The service naming the id back is about the voice whatever it arrives as.
    [InlineData(HttpStatusCode.UnprocessableEntity,
        "An invalid ID has been received: 'en-US-RogerNeural'. Make sure to provide a correct one.",
        TtsFault.VoiceRejected)]

    // A voice the account holds and this API will not speak (remediation.md 17, item 13). Matched
    // on what it says rather than the status it says it with: the listing filter that usually
    // prevents this reads a ™ out of a display name, which ElevenLabs can change without telling
    // anybody, so this half deliberately does not depend on the name. 403 stands in for "some
    // status this does not otherwise treat as being about the voice".
    [InlineData(HttpStatusCode.Forbidden,
        "Famous voices can only be used within the Reader App.",
        TtsFault.VoiceRejected)]

    // And these are not. Discarding a good voice on the way to reporting a key or a balance
    // would leave the Commander two things to fix instead of one.
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
