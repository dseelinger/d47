using System.Net;
using System.Text.Json;
using D47.Core.Audio;
using D47.Tts;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Tts.Tests;

/// <summary>
/// Which model d47 asks ElevenLabs for, and what it tells it to speak in.
/// <para>
/// <b>Both facts are load-bearing and neither was asserted anywhere.</b> The model decides the
/// price d47 quotes in the spend ledger, the speed range the rate row narrows to, and — the one
/// that is a correctness question rather than a preference — whether <c>language_code</c> is
/// accepted at all. Multilingual 2 rejects it outright and picks the language line by line from
/// the text, which is how a material milestone came back read half in German. d47 re-voices
/// messages other Commanders write, and in-game text is untrusted by invariant, so the language
/// must be d47's to set.
/// </para>
/// <para>
/// The pin has moved twice: to Turbo 2.5 on 2026-08-16 for that reason, and to Flash 2.5 on
/// 2026-08-25 because ElevenLabs deprecated Turbo and named Flash its replacement — <em>"
/// functionally equivalent … except the latency on the Flash models is lower on average"</em>.
/// Same generation, same parameter, same price, less delay.
/// </para>
/// </summary>
public class TheModelOnTheWireTests
{
    private static async Task<JsonElement> SynthesisBodyAsync()
    {
        var capture = new CaptureRequest();

        using var http = new HttpClient(capture);
        using var provider = new ElevenLabsTtsProvider(
            () => "sk_test", NullLogger<ElevenLabsTtsProvider>.Instance, http);

        await provider.SynthesizeAsync(
            "Hull integrity is nominal.",
            new VoiceSelection("voice-1"),
            TestContext.Current.CancellationToken);

        Assert.NotNull(capture.Body);

        return JsonDocument.Parse(capture.Body!).RootElement.Clone();
    }

    /// <summary>
    /// Flash 2.5 by name. Asserted as the literal id rather than against the constant, so that
    /// changing the constant is a decision somebody makes rather than a change this test follows
    /// silently.
    /// </summary>
    [Fact]
    public async Task FlashIsTheModelAsked()
    {
        var sent = await SynthesisBodyAsync();

        Assert.Equal("eleven_flash_v2_5", sent.GetProperty("model_id").GetString());
    }

    /// <summary>
    /// And the language goes with it. Without this the model chooses, and what it chooses can be
    /// steered by whatever an in-game message happens to be written in.
    /// </summary>
    [Fact]
    public async Task EnglishIsPinnedRatherThanInferred()
    {
        var sent = await SynthesisBodyAsync();

        Assert.Equal("en", sent.GetProperty("language_code").GetString());
    }

    /// <summary>
    /// The model d47 must never ask for, named so the exclusion is a test rather than a comment.
    /// It is the one ElevenLabs model that refuses <c>language_code</c>, and it is also dearer
    /// and slower than the pin — so it loses on every count, not only the one that matters.
    /// </summary>
    [Fact]
    public async Task TheModelThatWillNotHoldALanguageIsNeverAsked()
    {
        var sent = await SynthesisBodyAsync();

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

            // Two seconds of silence at 24 kHz mono 16-bit is a plausible clip and decodes to
            // nothing audible, which is all this needs — the assertion is about what went out.
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new ByteArrayContent(new byte[24_000 * 2 * 2]),
            };
        }
    }
}
