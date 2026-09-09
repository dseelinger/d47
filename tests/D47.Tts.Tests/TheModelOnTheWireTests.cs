using System.Net;
using System.Text.Json;
using D47.Core.Audio;
using D47.Tts;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Tts.Tests;

/// <summary>Which model d47 asks ElevenLabs for, and what it tells it to speak in.</summary>
public class TheModelOnTheWireTests
{
    private static async Task<JsonElement> SynthesisBodyAsync(string? model = null)
    {
        var capture = new CaptureRequest();

        using var http = new HttpClient(capture);
        using var provider = new ElevenLabsTtsProvider(
            () => "sk_test", NullLogger<ElevenLabsTtsProvider>.Instance, http, model: () => model);

        await provider.SynthesizeAsync(
            "Hull integrity is nominal.",
            new VoiceSelection("voice-1"),
            TestContext.Current.CancellationToken);

        Assert.NotNull(capture.Body);

        return JsonDocument.Parse(capture.Body!).RootElement.Clone();
    }

    /// <summary>v3 Conversational when nobody has chosen, and Flash 2.5 when somebody has.</summary>
    [Theory]
    [InlineData(null, "eleven_v3_conversational")]
    [InlineData("eleven_v3_conversational", "eleven_v3_conversational")]
    [InlineData("eleven_flash_v2_5", "eleven_flash_v2_5")]
    public async Task TheChosenModelIsTheModelAsked(string? chosen, string expected)
    {
        var sent = await SynthesisBodyAsync(chosen);

        Assert.Equal(expected, sent.GetProperty("model_id").GetString());
    }

    /// <summary>A name d47 does not offer resolves to the default rather than going out as it was found.</summary>
    [Theory]
    [InlineData("eleven_multilingual_v2")]
    [InlineData("eleven_v3")]
    [InlineData("")]
    [InlineData("nonsense")]
    public async Task AModelD47DoesNotOfferBecomesTheDefault(string stored)
    {
        var sent = await SynthesisBodyAsync(stored);

        Assert.Equal("eleven_v3_conversational", sent.GetProperty("model_id").GetString());
    }

    /// <summary>The speed goes only to the model that reads it. v3 accepts 0.5 through 2.0 and acts on none of
    /// it, so sending one would put a number in the request that never changed a sound.</summary>
    [Fact]
    public async Task OnlyTheModelWithARateIsSentOne()
    {
        Assert.True(
            (await SynthesisBodyAsync("eleven_flash_v2_5")).TryGetProperty("voice_settings", out _));

        Assert.False(
            (await SynthesisBodyAsync("eleven_v3_conversational")).TryGetProperty("voice_settings", out _));
    }

    /// <summary>And the language goes with it.</summary>
    [Theory]
    [InlineData("eleven_flash_v2_5")]
    [InlineData("eleven_v3_conversational")]
    public async Task EnglishIsPinnedRatherThanInferred(string chosen)
    {
        var sent = await SynthesisBodyAsync(chosen);

        Assert.Equal("en", sent.GetProperty("language_code").GetString());
    }

    /// <summary>
    /// The model d47 must never ask for, named so the exclusion is a test rather than a comment.
    /// </summary>
    [Theory]
    [InlineData(null)]
    [InlineData("eleven_flash_v2_5")]
    [InlineData("eleven_v3_conversational")]
    public async Task TheModelThatWillNotHoldALanguageIsNeverAsked(string? chosen)
    {
        var sent = await SynthesisBodyAsync(chosen);

        Assert.NotEqual("eleven_multilingual_v2", sent.GetProperty("model_id").GetString());
    }

    private sealed class CaptureRequest : HttpMessageHandler
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

            // Two seconds of silence at 24 kHz mono 16-bit is a plausible clip and decodes to nothing
            // audible, which is all this needs — the assertion is about what went out.
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new ByteArrayContent(new byte[24_000 * 2 * 2]),
            };
        }
    }
}
