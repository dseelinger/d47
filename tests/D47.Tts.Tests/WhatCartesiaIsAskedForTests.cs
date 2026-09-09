using System.Net;
using System.Text.Json;
using D47.Core.Audio;
using D47.Tts;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Tts.Tests;

/// <summary>What d47 sends Cartesia, and what it does with what comes back.</summary>
public class WhatCartesiaIsAskedForTests
{
    private static async Task<JsonElement> SentAsync(VoiceSelection? voice = null)
    {
        var capture = new CaptureSpeech();

        using var provider = new CartesiaTtsProvider(
            () => "sk-test", NullLogger<CartesiaTtsProvider>.Instance, capture);

        await provider.SynthesizeAsync(
            "Hull integrity is nominal.",
            voice ?? new VoiceSelection("a0e99841-438c-4a64-b679-ae501e7d6091"),
            TestContext.Current.CancellationToken);

        Assert.NotNull(capture.Body);

        return JsonDocument.Parse(capture.Body!).RootElement.Clone();
    }

    /// <summary>The model by name.</summary>
    [Fact]
    public async Task TheModelIsNamedRatherThanLeftToTheService()
    {
        Assert.Equal("sonic-2", (await SentAsync()).GetProperty("model_id").GetString());
    }

    /// <summary>The field that makes this provider eligible where OpenAI is barred.</summary>
    [Fact]
    public async Task TheLanguageIsPinnedAndSentWithEveryLine()
    {
        Assert.Equal("en", (await SentAsync()).GetProperty("language").GetString());
    }

    [Fact]
    public async Task RawSamplesRatherThanAContainer()
    {
        var format = (await SentAsync()).GetProperty("output_format");

        // Nothing has to be decoded on the way to the arbiter, and 24 kHz is an exact 2× of the arbiter's 48
        // — the same trade both other paid providers make.
        Assert.Equal("raw", format.GetProperty("container").GetString());
        Assert.Equal("pcm_s16le", format.GetProperty("encoding").GetString());
        Assert.Equal(24_000, format.GetProperty("sample_rate").GetInt32());
    }

    [Fact]
    public async Task TheVoiceIsNamedByIdBecauseThatIsWhatThePickerStores()
    {
        var voice = (await SentAsync()).GetProperty("voice");

        Assert.Equal("id", voice.GetProperty("mode").GetString());
        Assert.Equal("a0e99841-438c-4a64-b679-ae501e7d6091", voice.GetProperty("id").GetString());
    }

    /// <summary>The finding, asserted as an absence.</summary>
    [Theory]
    [InlineData(1.0)]
    [InlineData(0.7)]
    [InlineData(1.9)]
    public async Task NoSpeedIsSentAtAnyRateBecauseNoneOfThemWouldDoAnything(double rate)
    {
        var voice = (await SentAsync(new VoiceSelection("a0e99841-438c-4a64-b679-ae501e7d6091", rate)))
            .GetProperty("voice");

        Assert.False(voice.TryGetProperty("__experimental_controls", out _));
        Assert.False(voice.TryGetProperty("speed", out _));
    }

    /// <summary>
    /// Both headers, because the request fails without either — and the version is part of the finding
    /// rather than a constant: everything the spike measured was measured against it.
    /// </summary>
    [Fact]
    public async Task TheKeyAndTheApiVersionBothTravelInHeaders()
    {
        var capture = new CaptureSpeech();

        using var provider = new CartesiaTtsProvider(
            () => "sk-test", NullLogger<CartesiaTtsProvider>.Instance, capture);

        await provider.SynthesizeAsync(
            "Hull integrity is nominal.",
            new VoiceSelection("a0e99841-438c-4a64-b679-ae501e7d6091"),
            TestContext.Current.CancellationToken);

        Assert.Equal("sk-test", Assert.Single(capture.Headers!.GetValues("X-API-Key")));
        Assert.Equal("2024-11-13", Assert.Single(capture.Headers!.GetValues("Cartesia-Version")));
    }

    /// <summary>24 kHz in, 48 kHz out.</summary>
    [Fact]
    public async Task TheClipArrivesInTheArbitersFormat()
    {
        var capture = new CaptureSpeech();

        using var provider = new CartesiaTtsProvider(
            () => "sk-test", NullLogger<CartesiaTtsProvider>.Instance, capture);

        var clip = await provider.SynthesizeAsync(
            "Hull integrity is nominal.",
            new VoiceSelection("a0e99841-438c-4a64-b679-ae501e7d6091"),
            TestContext.Current.CancellationToken);

        Assert.Equal(AudioFormat.Standard, clip.Format);
        Assert.Equal(48_000, clip.Format.SampleRate);

        // One second of 24 kHz mono in, one second of 48 kHz mono out.
        Assert.Equal(1.0, clip.Duration.TotalSeconds, 2);
    }

    [Fact]
    public async Task WithNoKeyNothingIsSentAndTheMessageSaysWhereToPutOne()
    {
        using var provider = new CartesiaTtsProvider(
            () => null, NullLogger<CartesiaTtsProvider>.Instance);

        var refused = await Assert.ThrowsAsync<TtsException>(() => provider.SynthesizeAsync(
            "Hull integrity is nominal.",
            new VoiceSelection("a0e99841-438c-4a64-b679-ae501e7d6091"),
            TestContext.Current.CancellationToken));

        Assert.Equal(TtsFault.KeyRejected, refused.Fault);
        Assert.Contains("Settings", refused.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// No hardcoded fallback id, for the reason ElevenLabs gives: this catalogue's ids are opaque and
    /// belong to an account, so a guess would fail as a 404 that reads like an outage.
    /// </summary>
    [Fact]
    public async Task WithNoVoiceChosenItSaysSoRatherThanGuessingAnId()
    {
        using var provider = new CartesiaTtsProvider(
            () => "sk-test", NullLogger<CartesiaTtsProvider>.Instance, new CaptureSpeech());

        var refused = await Assert.ThrowsAsync<TtsException>(() => provider.SynthesizeAsync(
            "Hull integrity is nominal.",
            VoiceSelection.Default,
            TestContext.Current.CancellationToken));

        Assert.Contains("Pick one in Settings", refused.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// A refused key and an unreachable service are different answers with different remedies, which is
 /// what makes a key check worth having.
    /// </summary>
    [Theory]
    [InlineData(HttpStatusCode.Unauthorized, TtsFault.KeyRejected)]
    [InlineData(HttpStatusCode.TooManyRequests, TtsFault.Unreachable)]
    [InlineData(HttpStatusCode.InternalServerError, TtsFault.Unreachable)]
    [InlineData(HttpStatusCode.NotFound, TtsFault.VoiceRejected)]
    public async Task ARefusalIsClassifiedIntoSomethingACommanderCanActOn(
        HttpStatusCode status,
        TtsFault fault)
    {
        using var provider = new CartesiaTtsProvider(
            () => "sk-test",
            NullLogger<CartesiaTtsProvider>.Instance,
            new Refuses(status, "{\"error\":\"nope\"}"));

        var refused = await Assert.ThrowsAsync<TtsException>(() => provider.SynthesizeAsync(
            "Hull integrity is nominal.",
            new VoiceSelection("a0e99841-438c-4a64-b679-ae501e7d6091"),
            TestContext.Current.CancellationToken));

        Assert.Equal(fault, refused.Fault);
    }

    /// <summary>
    /// The service's own words, preferred over anything mapped from a status code — and this API
    /// answers in plain text as readily as in JSON, which is why the body itself is the fallback rather
    /// than nothing.
    /// </summary>
    [Fact]
    public async Task AndItSaysWhatTheServiceSaidRatherThanWhatStatusItSaidItWith()
    {
        using var provider = new CartesiaTtsProvider(
            () => "sk-test",
            NullLogger<CartesiaTtsProvider>.Instance,
            new Refuses(
                HttpStatusCode.BadRequest,
                "invalid voice controls: speed float must be between -1.0 and 1.0"));

        var refused = await Assert.ThrowsAsync<TtsException>(() => provider.SynthesizeAsync(
            "Hull integrity is nominal.",
            new VoiceSelection("a0e99841-438c-4a64-b679-ae501e7d6091"),
            TestContext.Current.CancellationToken));

        Assert.Contains("between -1.0 and 1.0", refused.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// 924 voices arrive a hundred at a time, so paging is the normal case here rather than an edge one
    /// — and a listing that stopped after the first page would leave a Commander casting from a ninth
    /// of the library that is the whole reason this provider was added.
    /// </summary>
    [Fact]
    public async Task TheWholeLibraryIsListedRatherThanTheFirstPageOfIt()
    {
        using var provider = new CartesiaTtsProvider(
            () => "sk-test", NullLogger<CartesiaTtsProvider>.Instance, new TwoPages());

        var listed = await provider.ListVoicesAsync(TestContext.Current.CancellationToken);

        Assert.Equal(VoiceListing.Listed, listed.Listing);
        Assert.Equal(2, listed.Count);
        Assert.Equal(["one", "two"], listed.Voices.Select(voice => voice.Id));

        // The tags this provider publishes and OpenAI does not — a language rather than an accent label, and
        // a sex, which is what lets `VoiceCast.ForSender` match a sender here.
        Assert.Equal("en", listed.Voices[0].Locale);
        Assert.Equal("feminine", listed.Voices[0].Gender);
    }

    [Fact]
    public async Task AndWithNoKeyItSaysWhichKindOfEmptyItIs()
    {
        using var provider = new CartesiaTtsProvider(
            () => null, NullLogger<CartesiaTtsProvider>.Instance);

        var listed = await provider.ListVoicesAsync(TestContext.Current.CancellationToken);

        // Not the same nothing a refused key shows: the picker's contract is that an empty list says why.
        Assert.Equal(VoiceListing.NoKey, listed.Listing);
    }

    private sealed class CaptureSpeech : HttpMessageHandler
    {
        public string? Body { get; private set; }

        public System.Net.Http.Headers.HttpRequestHeaders? Headers { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Headers = request.Headers;

            if (request.Content is { } content)
            {
                Body = await content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            }

            // One second of silence at 24 kHz mono 16-bit, which is what the endpoint returns for a raw
            // pcm_s16le output format and is all the assertions here need.
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

    /// <summary>Two pages of one voice each, the second saying there are no more.</summary>
    private sealed class TwoPages : HttpMessageHandler
    {
        private int _served;

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            var body = _served++ == 0
                ? """
                  {"data":[{"id":"one","name":"One","language":"en","gender":"feminine"}],
                   "has_more":true,"next_page":"one"}
                  """
                : """
                  {"data":[{"id":"two","name":"Two","language":"en","gender":"masculine"}],
                   "has_more":false,"next_page":null}
                  """;

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(body),
            });
        }
    }
}
