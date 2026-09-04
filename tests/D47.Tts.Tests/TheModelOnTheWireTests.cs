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
/// The pin moved twice: to Turbo 2.5 on 2026-08-16 for that reason, and to Flash 2.5 on
/// 2026-08-25 because ElevenLabs deprecated Turbo and named Flash its replacement — <em>"
/// functionally equivalent … except the latency on the Flash models is lower on average"</em>.
/// Same generation, same parameter, same price, less delay.
/// </para>
/// <para>
/// <b>From 2026-09-04 there is no pin: there are two models and a row</b>
/// (<a href="https://github.com/dseelinger/d47/issues/291">#291</a>). v3 Conversational is the
/// default and performs delivery direction; Flash 2.5 is a third of the latency and reads
/// direction out loud, so it is never sent any. Both accept <c>language_code</c>, so the rule this
/// fixture was written for is unchanged and is now asserted against both.
/// </para>
/// </summary>
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

    /// <summary>
    /// v3 Conversational when nobody has chosen, and Flash 2.5 when somebody has. Asserted as
    /// literal ids rather than against the constants, so that changing one is a decision somebody
    /// makes rather than a change this test follows silently — which is what caught the default
    /// moving on 2026-09-04.
    /// </summary>
    [Theory]
    [InlineData(null, "eleven_v3_conversational")]
    [InlineData("eleven_v3_conversational", "eleven_v3_conversational")]
    [InlineData("eleven_flash_v2_5", "eleven_flash_v2_5")]
    public async Task TheChosenModelIsTheModelAsked(string? chosen, string expected)
    {
        var sent = await SynthesisBodyAsync(chosen);

        Assert.Equal(expected, sent.GetProperty("model_id").GetString());
    }

    /// <summary>
    /// A name d47 does not offer resolves to the default rather than going out as it was found.
    /// <c>settings.json</c> is a file a Commander reads and edits, and the one model that must
    /// never be asked for is a name somebody could type into it.
    /// </summary>
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

    /// <summary>
    /// The speed goes only to the model that reads it. v3 accepts 0.5 through 2.0 and acts on none
    /// of it, so sending one would put a number in the request that never changed a sound
    /// (docs/spikes/elevenlabs-v3-conversational.md §3).
    /// </summary>
    [Fact]
    public async Task OnlyTheModelWithARateIsSentOne()
    {
        Assert.True(
            (await SynthesisBodyAsync("eleven_flash_v2_5")).TryGetProperty("voice_settings", out _));

        Assert.False(
            (await SynthesisBodyAsync("eleven_v3_conversational")).TryGetProperty("voice_settings", out _));
    }

    /// <summary>
    /// And the language goes with it. Without this the model chooses, and what it chooses can be
    /// steered by whatever an in-game message happens to be written in.
    /// </summary>
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
    /// It is the one ElevenLabs model that refuses <c>language_code</c>, and it is also dearer
    /// and slower than either offered model — so it loses on every count, not only the one that
    /// matters. Asserted for both models and for the default, because a two-model row is one more
    /// place a wrong name could arrive from.
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

            // Two seconds of silence at 24 kHz mono 16-bit is a plausible clip and decodes to
            // nothing audible, which is all this needs — the assertion is about what went out.
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new ByteArrayContent(new byte[24_000 * 2 * 2]),
            };
        }
    }
}
