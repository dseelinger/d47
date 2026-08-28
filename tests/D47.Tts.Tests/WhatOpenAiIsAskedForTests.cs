using System.Net;
using System.Text.Json;
using D47.Core.Audio;
using D47.Tts;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Tts.Tests;

/// <summary>
/// What d47 sends OpenAI, and what it does with what comes back (Phase 58).
/// <para>
/// The sibling of <see cref="TheModelOnTheWireTests"/> and for the same reasons: the model
/// decides what a Commander hears and what they are billed, and neither fact is asserted anywhere
/// unless it is asserted here. One thing this provider cannot be asked is the one that matters
/// most — there is no language field, and sending one is accepted and ignored — so the guard
/// against a French message read in French lives at the settings surface instead, in
/// <c>TtsProviderInfo.LanguageCanBePinned</c>.
/// </para>
/// </summary>
public class WhatOpenAiIsAskedForTests
{
    private static async Task<JsonElement> SentAsync(VoiceSelection? voice = null)
    {
        var capture = new CaptureSpeech();

        using var provider = new OpenAiTtsProvider(
            () => "sk-test", NullLogger<OpenAiTtsProvider>.Instance, capture);

        await provider.SynthesizeAsync(
            "Hull integrity is nominal.",
            voice ?? new VoiceSelection("onyx"),
            TestContext.Current.CancellationToken);

        Assert.NotNull(capture.Body);

        return JsonDocument.Parse(capture.Body!).RootElement.Clone();
    }

    /// <summary>
    /// The dated snapshot by name. Asserted as the literal id rather than against the constant, so
    /// that changing the constant is a decision somebody makes rather than a change this test
    /// follows silently — the rule <see cref="TheModelOnTheWireTests"/> already sets.
    /// </summary>
    [Fact]
    public async Task TheDatedSnapshotIsWhatIsAskedFor()
    {
        Assert.Equal("gpt-4o-mini-tts-2025-12-15", (await SentAsync()).GetProperty("model").GetString());
    }

    [Fact]
    public async Task AndNeverTheFloatingAlias()
    {
        // A floating alias is a voice that can change under a Commander who chose it.
        Assert.NotEqual("gpt-4o-mini-tts", (await SentAsync()).GetProperty("model").GetString());
    }

    [Fact]
    public async Task RawSamplesRatherThanAContainer()
    {
        // Nothing has to be decoded on the way to the arbiter, which is the same trade the other
        // paid provider makes with pcm_24000.
        Assert.Equal("pcm", (await SentAsync()).GetProperty("response_format").GetString());
    }

    [Fact]
    public async Task NoLanguageIsSentBecauseThereIsNothingToSend()
    {
        var sent = await SentAsync();

        // Not an oversight and not a thing to add later: the field does not exist, and one sent
        // anyway is accepted with 200 and ignored, which is worse than a refusal because nothing
        // can see it happen (docs/spikes/openai-tts-language-and-speed.md §2).
        Assert.False(sent.TryGetProperty("language", out _));
    }

    [Fact]
    public async Task AVoiceThatWasNotChosenFallsBackToANamedOne()
    {
        var sent = await SentAsync(VoiceSelection.Default);

        // A named default is right here and wrong for ElevenLabs: this catalogue is fixed and
        // public, so naming one is a choice rather than a guess at somebody's account.
        Assert.Equal("onyx", sent.GetProperty("voice").GetString());
    }

    [Theory]
    [InlineData(1.0, 1.0)]
    [InlineData(0.1, 0.25)]
    [InlineData(9.0, 4.0)]
    public async Task TheRateIsClampedToWhatTheEndpointAccepts(double asked, double sent)
    {
        var body = await SentAsync(new VoiceSelection("onyx", asked));

        Assert.Equal(sent, body.GetProperty("speed").GetDouble());
    }

    /// <summary>
    /// 24 kHz in, 48 kHz out. The arbiter's format is not a suggestion — a clip at half the rate
    /// plays at half the speed and an octave down, which is audible and which nothing downstream
    /// would report.
    /// </summary>
    [Fact]
    public async Task TheClipArrivesInTheArbitersFormat()
    {
        var capture = new CaptureSpeech();

        using var provider = new OpenAiTtsProvider(
            () => "sk-test", NullLogger<OpenAiTtsProvider>.Instance, capture);

        var clip = await provider.SynthesizeAsync(
            "Hull integrity is nominal.",
            new VoiceSelection("onyx"),
            TestContext.Current.CancellationToken);

        Assert.Equal(AudioFormat.Standard, clip.Format);
        Assert.Equal(48_000, clip.Format.SampleRate);

        // One second of 24 kHz mono in, one second of 48 kHz mono out.
        Assert.Equal(1.0, clip.Duration.TotalSeconds, 2);
    }

    [Fact]
    public async Task TheThirteenAreOfferedWithNoKeyAtAll()
    {
        using var provider = new OpenAiTtsProvider(
            () => null, NullLogger<OpenAiTtsProvider>.Instance);

        var listed = await provider.ListVoicesAsync(TestContext.Current.CancellationToken);

        Assert.Equal(VoiceListing.Listed, listed.Listing);
        Assert.Equal(13, listed.Count);
        Assert.Contains(listed.Voices, voice => voice.Id == "onyx");

        // And every one survives into the pool a re-voiced sender is drawn from. "multilingual"
        // is a word rather than a locale, which VoicePool is careful to tell apart — reading it
        // as one once discarded 472 of a 473-voice account.
        Assert.Equal(13, VoicePool.From(listed.Voices).Count);
    }

    /// <summary>
    /// No gender is claimed, so the sex-matching in <see cref="VoiceCast.ForSender"/> widens to
    /// the whole pool rather than narrowing on a guess. OpenAI publishes none, and a voice tagged
    /// as a woman's decides who gets it.
    /// </summary>
    [Fact]
    public async Task AndNoneOfThemClaimsAGender()
    {
        using var provider = new OpenAiTtsProvider(
            () => null, NullLogger<OpenAiTtsProvider>.Instance);

        var listed = await provider.ListVoicesAsync(TestContext.Current.CancellationToken);

        Assert.All(listed.Voices, voice => Assert.Null(voice.Gender));
        Assert.Empty(VoicePool.Feminine(listed.Voices));
    }

    [Fact]
    public async Task NoKeyIsTheKeyBeingRefusedRatherThanAnOutage()
    {
        using var provider = new OpenAiTtsProvider(
            () => null, NullLogger<OpenAiTtsProvider>.Instance);

        var failed = await Assert.ThrowsAsync<TtsException>(() => provider.SynthesizeAsync(
            "Hull integrity is nominal.",
            VoiceSelection.Default,
            TestContext.Current.CancellationToken));

        Assert.Equal(TtsFault.KeyRejected, failed.Fault);
    }

    /// <summary>
    /// The distinction the Check button is built on: one of these the Commander can fix from a
    /// row they can see, and the other is the network's problem
    /// (docs/spikes/elevenlabs-voice-sources.md §3).
    /// </summary>
    [Theory]
    [InlineData(HttpStatusCode.Unauthorized, TtsFault.KeyRejected)]
    [InlineData(HttpStatusCode.Forbidden, TtsFault.KeyRejected)]
    [InlineData(HttpStatusCode.TooManyRequests, TtsFault.Unreachable)]
    [InlineData(HttpStatusCode.InternalServerError, TtsFault.Unreachable)]
    [InlineData(HttpStatusCode.ServiceUnavailable, TtsFault.Unreachable)]
    public async Task ARefusalIsClassifiedIntoSomethingActionable(HttpStatusCode status, TtsFault fault)
    {
        using var provider = new OpenAiTtsProvider(
            () => "sk-test",
            NullLogger<OpenAiTtsProvider>.Instance,
            new Refuses(status, "{\"error\":{\"message\":\"Incorrect API key provided.\"}}"));

        var failed = await Assert.ThrowsAsync<TtsException>(() => provider.SynthesizeAsync(
            "Hull integrity is nominal.",
            new VoiceSelection("onyx"),
            TestContext.Current.CancellationToken));

        Assert.Equal(fault, failed.Fault);

        // The service's own words rather than a status code translated, for the reason the
        // ElevenLabs path gives: a mapping worth writing would answer "it said 400".
        Assert.Contains("Incorrect API key provided.", failed.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AVoiceItDoesNotHaveIsWorthForgettingRatherThanRetrying()
    {
        using var provider = new OpenAiTtsProvider(
            () => "sk-test",
            NullLogger<OpenAiTtsProvider>.Instance,
            new Refuses(HttpStatusCode.BadRequest, "{\"error\":{\"message\":\"Invalid value: 'george'. "
                                                   + "Supported values are: 'alloy', 'onyx'.\","
                                                   + "\"type\":\"invalid_request_error\",\"param\":\"voice\"}}"));

        var failed = await Assert.ThrowsAsync<TtsException>(() => provider.SynthesizeAsync(
            "Hull integrity is nominal.",
            new VoiceSelection("george"),
            TestContext.Current.CancellationToken));

        // Unlike ElevenLabs this cannot be a stale id from another account — the catalogue is
        // fixed — so it is a value typed or carried across, and forgetting it is still right.
        //
        // The body's shape is the one the spike observed on a refused `speed`: a message, a type,
        // and the field blamed in `param`. That last is what has to be read, because the message
        // for a bad voice does not carry the word "voice" anywhere in it.
        Assert.Equal(TtsFault.VoiceRejected, failed.Fault);
    }

    private sealed class CaptureSpeech : HttpMessageHandler
    {
        public string? Body { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            if (request.Content is { } content)
            {
                Body = await content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            }

            // One second of silence at 24 kHz mono 16-bit, which is what the endpoint returns for
            // `response_format: pcm` and is all the assertions here need.
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new ByteArrayContent(new byte[24_000 * 2]),
            };
        }
    }

    private sealed class Refuses(HttpStatusCode status, string body) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(status) { Content = new StringContent(body) });
    }
}
