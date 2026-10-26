using System.Net;
using System.Text.Json;
using D47.Core.Audio;
using D47.Tts;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Tts.Tests;

/// <summary>A line's language goes on the wire in place of English, and an English line goes out as before (#1752).</summary>
public class ALineIsSentInItsOwnLanguageTests
{
    private const string Japanese = "第3格納庫へ、12時までに。";

    private static readonly DateTimeOffset Noon = new(2026, 10, 26, 12, 0, 0, TimeSpan.Zero);

    private static async Task<JsonElement> ElevenLabsBodyAsync(string text, string? language)
    {
        var capture = new Capture();

        using var http = new HttpClient(capture);
        using var provider = new ElevenLabsTtsProvider(
            () => "sk_test", NullLogger<ElevenLabsTtsProvider>.Instance, http, model: () => null);

        await provider.SynthesizeAsync(
            text,
            new VoiceSelection("voice-1") { Language = language },
            TestContext.Current.CancellationToken);

        return JsonDocument.Parse(capture.Body!).RootElement.Clone();
    }

    private static async Task<JsonElement> CartesiaBodyAsync(string? language)
    {
        var capture = new Capture();

        using var provider = new CartesiaTtsProvider(
            () => "sk-test", NullLogger<CartesiaTtsProvider>.Instance, capture);

        await provider.SynthesizeAsync(
            Japanese,
            new VoiceSelection("a0e99841-438c-4a64-b679-ae501e7d6091") { Language = language },
            TestContext.Current.CancellationToken);

        return JsonDocument.Parse(capture.Body!).RootElement.Clone();
    }

    [Fact]
    public async Task ElevenLabsIsToldTheLinesLanguage()
    {
        Assert.Equal("ja", (await ElevenLabsBodyAsync(Japanese, "ja")).GetProperty("language_code").GetString());
    }

    [Fact]
    public async Task ElevenLabsKeepsTheNumeralsOfALineInAnotherLanguage()
    {
        Assert.Equal(Japanese, (await ElevenLabsBodyAsync(Japanese, "ja")).GetProperty("text").GetString());
    }

    [Fact]
    public async Task ElevenLabsStillSpellsOutTheNumeralsOfAnEnglishLine()
    {
        var sent = await ElevenLabsBodyAsync("Bay 3 by 12.", null);

        Assert.Equal("en", sent.GetProperty("language_code").GetString());
        Assert.DoesNotContain("3", sent.GetProperty("text").GetString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task CartesiaIsToldTheLinesLanguage()
    {
        Assert.Equal("ja", (await CartesiaBodyAsync("ja")).GetProperty("language").GetString());
    }

    [Fact]
    public async Task CartesiaStillSendsEnglishForALineWithNone()
    {
        Assert.Equal("en", (await CartesiaBodyAsync(null)).GetProperty("language").GetString());
    }

    [Fact]
    public void EdgeTagsALineInAnotherLanguageWithItsVoicesLocale()
    {
        var ssml = EdgeProtocol.SsmlRequest(Japanese, "ja-JP-NanamiNeural", 1.0, "req", Noon, "ja");

        Assert.Contains("xml:lang='ja-JP'", ssml, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("en-GB-SoniaNeural", null)]
    [InlineData("ja-JP-NanamiNeural", null)]
    [InlineData("en-GB-SoniaNeural", "en")]
    public void EdgeTagsAnEnglishLineAsItAlwaysHas(string voice, string? language)
    {
        var ssml = EdgeProtocol.SsmlRequest("Docking granted.", voice, 1.0, "req", Noon, language);

        Assert.Contains("xml:lang='en-US'", ssml, StringComparison.Ordinal);
    }

    [Fact]
    public void EdgeFallsBackToTheLanguageForAVoiceIdWithNoLocale()
    {
        var ssml = EdgeProtocol.SsmlRequest(Japanese, "Nanami", 1.0, "req", Noon, "ja");

        Assert.Contains("xml:lang='ja'", ssml, StringComparison.Ordinal);
    }

    private sealed class Capture : HttpMessageHandler
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

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new ByteArrayContent(new byte[24_000 * 2]),
            };
        }
    }
}
