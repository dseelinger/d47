using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using D47.Core.Audio;
using Microsoft.Extensions.Logging;

namespace D47.Tts;

/// <summary>
/// OpenAI's speech endpoint (list.md Phase 58).
/// <para>
/// The third provider, and the first to arrive after Phase 57 made the <em>slot</em> the unit
/// rather than the app. Nothing above <see cref="ITtsProvider"/> learns it exists: a slot names
/// it, the composition root builds one client however many slots did, and a metering decorator
/// per slot counts what it spends.
/// </para>
/// <para>
/// <b>It cannot be told what language to speak, and that is a property of the service rather
/// than of this class.</b> The request schema has no language field; sending one anyway is
/// accepted with <c>200</c> and ignored, which is worse than a refusal because no caller can see
/// it happen. <see cref="TtsProviderInfo.LanguageCanBePinned"/> is where that fact lives, and the
/// settings surface reads it to keep this provider away from the slots carrying other people's
/// text — see docs/spikes/openai-tts-language-and-speed.md §2.
/// </para>
/// </summary>
public sealed class OpenAiTtsProvider : ITtsProvider, IDisposable
{
    private const string SpeechUrl = "https://api.openai.com/v1/audio/speech";

    public const string ProviderId = "openai";

    /// <summary>
    /// The same secret the language-model provider uses, on purpose (list.md Phase 58).
    /// <para>
    /// One account, one credential. Asking a Commander to paste the same key twice would be an
    /// implementation detail charged to them, and two copies of one secret is a rotation that
    /// half-works: change one, forget the other, and some of d47 keeps speaking while the rest
    /// stops with an error that names neither.
    /// </para>
    /// </summary>
    public const string KeySecretName = "openai.apiKey";

    /// <summary>
    /// The dated snapshot, pinned for the reason <see cref="ElevenLabsTtsProvider.DefaultModel"/>
    /// gives: a floating alias is a voice that can change under a Commander who chose it.
    /// <para>
    /// The date is not decoration. <c>instructions</c> is documented to work on this model and
    /// <b>not</b> on <c>tts-1</c> or <c>tts-1-hd</c>, so the pin is also what keeps
    /// <a href="https://github.com/dseelinger/d47/issues/49">#49</a> reachable later without
    /// moving it.
    /// </para>
    /// </summary>
    public const string DefaultModel = "gpt-4o-mini-tts-2025-12-15";

    /// <summary>
    /// The voice used when nothing has been chosen.
    /// <para>
    /// A named default is right here for the reason it is wrong for ElevenLabs: this catalogue is
    /// fixed and public, so naming one is a choice d47 can stand behind rather than a guess at an
    /// id belonging to somebody's account. Any of the thirteen would work; this one is low and
    /// unhurried, which is the register the ship's core is written in.
    /// </para>
    /// </summary>
    public const string DefaultVoice = "onyx";

    /// <summary>
    /// The documented bounds of <c>speed</c>, and they are real: measured 2026-08-26 across the
    /// whole range, <c>0.25</c> gives 40.82 seconds where <c>1.0</c> gives 10.75
    /// (docs/spikes/openai-tts-language-and-speed.md §3). The brief reported this parameter as
    /// documented but ignored on this model, and that is not what the endpoint does.
    /// <para>
    /// <b>It saturates near the top</b> — <c>4.0</c> buys about 3.3× rather than 4× — so the
    /// fastest rate is faster than asked for in name only. Worth knowing where a settings row
    /// describes it; not worth narrowing the range over, since the figure still moves.
    /// </para>
    /// </summary>
    public const double MinimumSpeed = 0.25;

    /// <inheritdoc cref="MinimumSpeed"/>
    public const double MaximumSpeed = 4.0;

    /// <summary>
    /// In flight at once. Conservative for the reason <see cref="ElevenLabsTtsProvider"/>'s gate
    /// is: the limit belongs to the account and the account's tier is not knowable from here, so
    /// this is a floor rather than a measurement. Since Phase 57 the client is shared by every
    /// slot that named this provider, which is what keeps one gate meaningful.
    /// </summary>
    private const int MaxConcurrent = 4;

    private readonly SemaphoreSlim _inFlight = new(MaxConcurrent, MaxConcurrent);

    /// <summary>
    /// Every voice the service offers, from the request schema's own <c>voice</c> description.
    /// <para>
    /// <b>There is no voices endpoint</b>, so this is a static catalogue rather than a cache of
    /// one — and <see cref="VoiceCatalogue"/> answers <see cref="VoiceListing.Listed"/> always.
    /// <see cref="VoiceListing.Unreachable"/> is unreachable here and <see cref="VoiceListing
    /// .NoKey"/> is wrong: the voices are known without a key, and a picker that stayed empty
    /// until one was pasted would be hiding a list it already had.
    /// </para>
    /// <para>
    /// <b>No gender is claimed and no locale is asserted.</b> OpenAI publishes neither, and
    /// inventing them would put d47's guess where a provider's fact belongs — a voice tagged as a
    /// woman's decides who gets it in <see cref="VoiceCast.ForSender"/>, which is too much to
    /// build on a hunch. "multilingual" is what the accent field honestly holds: the model speaks
    /// whatever language the text is in, which is exactly why it cannot be told one.
    /// </para>
    /// </summary>
    private static readonly IReadOnlyList<VoiceInfo> Catalogue =
    [
        new("alloy", "Alloy", "multilingual"),
        new("ash", "Ash", "multilingual"),
        new("ballad", "Ballad", "multilingual"),
        new("coral", "Coral", "multilingual"),
        new("echo", "Echo", "multilingual"),
        new("fable", "Fable", "multilingual"),
        new("onyx", "Onyx", "multilingual"),
        new("nova", "Nova", "multilingual"),
        new("sage", "Sage", "multilingual"),
        new("shimmer", "Shimmer", "multilingual"),
        new("verse", "Verse", "multilingual"),
        new("marin", "Marin", "multilingual"),
        new("cedar", "Cedar", "multilingual"),
    ];

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private readonly Func<string?> _key;

    /// <summary>How the core aboard should be performed, or null where nothing is being performed.</summary>
    private readonly Func<string?>? _direction;
    private readonly ILogger<OpenAiTtsProvider> _logger;
    private readonly HttpClient _http;

    /// <param name="direction">
    /// How the core aboard should be performed, asked at the moment of synthesis
    /// (<a href="https://github.com/dseelinger/d47/issues/49">#49</a>). Null, or a source
    /// returning null, sends no <c>instructions</c> field at all — which is what every slot that
    /// is not the ship's AI gets, and what personality-off gets.
    /// <para>
    /// <b>Construction rather than the selection record</b>, deliberately. <c>VoiceSelection</c>
    /// is built all over the codebase and the persona is known here; putting it on the selection
    /// would push a field through every one of those call sites. <b>A source rather than a
    /// value</b>, because a Commander switches core while d47 is running and the client is not
    /// rebuilt for it.
    /// </para>
    /// </param>
    public OpenAiTtsProvider(
        Func<string?> key,
        ILogger<OpenAiTtsProvider> logger,
        HttpMessageHandler? handler = null,
        Func<string?>? direction = null)
    {
        _key = key;
        _logger = logger;
        _direction = direction;
        _http = handler is null ? new HttpClient() : new HttpClient(handler);
        _http.Timeout = TimeSpan.FromSeconds(60);
    }

    public string Id => ProviderId;

    public string Name => "OpenAI";

    /// <summary>
    /// The thirteen, always, key or no key. Nothing is asked over the network, so nothing can
    /// fail — which is why the Check button proves a key by synthesising a character instead of
    /// by calling this (list.md Phase 58).
    /// </summary>
    public Task<VoiceCatalogue> ListVoicesAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(new VoiceCatalogue(Catalogue, VoiceListing.Listed));

    public async Task<AudioClip> SynthesizeAsync(
        string text,
        VoiceSelection voice,
        CancellationToken cancellationToken = default)
    {
        if (_key() is not { Length: > 0 } key)
        {
            throw new TtsException(
                "No OpenAI API key is stored. Add one in Settings.",
                fault: TtsFault.KeyRejected);
        }

        // Queued rather than refused. Waiting a moment for a slot is a sentence that arrives
        // late; going ahead without one is a sentence that never arrives at all.
        await _inFlight.WaitAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, SpeechUrl);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", key);
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("audio/*"));

            request.Content = JsonContent.Create(
                new SpeechRequest
                {
                    Model = DefaultModel,
                    // Sent as written. `ITtsProvider.Billable` is left at its default because
                    // this provider rewrites nothing on the way out — ElevenLabs overrides it
                    // only because it spells numerals, and it is the rewritten length that lands
                    // on that bill.
                    Input = text,
                    Voice = voice.VoiceId is { Length: > 0 } chosen ? chosen : DefaultVoice,

                    // Raw samples rather than a container, so nothing has to be decoded on the
                    // way to the arbiter — the same trade ElevenLabs makes with pcm_24000.
                    ResponseFormat = "pcm",
                    Speed = SpeedFor(voice.Rate),

                    // Omitted entirely when there is nobody to perform — the field is nullable and
                    // is left out of the JSON on null, so a slot with no core sends exactly the
                    // request it sent before this existed (#49).
                    Instructions = Blank(_direction?.Invoke()),
                },
                options: Json);

            using var response = await _http.SendAsync(request, cancellationToken).ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                throw await DescribeAsync(response, text, cancellationToken).ConfigureAwait(false);
            }

            var pcm = await response.Content.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false);

            if (pcm.Length == 0)
            {
                throw new TtsException($"OpenAI returned no audio for \"{Excerpt(text)}\".");
            }

            // 24 kHz, 16-bit, mono, little-endian — doubled to the arbiter's 48 kHz exactly as
            // the other paid provider's is.
            return new AudioClip(text, PcmUpsample.Double(pcm), AudioFormat.Standard);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (TtsException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new TtsException(
                $"OpenAI could not speak \"{Excerpt(text)}\": {ex.Message}",
                ex,
                TtsFault.Unreachable);
        }
        finally
        {
            _inFlight.Release();
        }
    }

    /// <summary>
    /// One character, synthesised and thrown away, to prove a key (list.md Phase 58).
    /// <para>
    /// The Check button proves a key by listing voices everywhere else, and there is nothing here
    /// to list. This is the cheapest honest alternative: a fraction of a cent, and it exercises
    /// the exact call that has to work rather than a proxy for it. The distinction it has to keep
    /// is the one docs/spikes/elevenlabs-voice-sources.md §3 establishes as load-bearing —
    /// "refused the key" and "could not be reached" are different answers with different
    /// remedies, and both arrive here as an exception.
    /// </para>
    /// </summary>
    public async Task ProveKeyAsync(CancellationToken cancellationToken = default) =>
        _ = await SynthesizeAsync(".", VoiceSelection.Default, cancellationToken).ConfigureAwait(false);

    /// <summary>
    /// What the service said, classified into the two answers a Commander can act on.
    /// <para>
    /// The message is the service's own rather than a translation of a status code, for the
    /// reason the ElevenLabs path gives: a mapping worth writing would answer "it said 400" and
    /// leave the Commander guessing.
    /// </para>
    /// </summary>
    private async Task<TtsException> DescribeAsync(
        HttpResponseMessage response,
        string text,
        CancellationToken cancellationToken)
    {
        var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        var (message, param) = Error(body);
        var said = message ?? response.ReasonPhrase ?? response.StatusCode.ToString();

        var fault = response.StatusCode switch
        {
            HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden => TtsFault.KeyRejected,

            // A voice this provider does not have. Unlike ElevenLabs this cannot be a stale id
            // from another account — the catalogue is fixed — so it is a settings value that was
            // typed or carried across, and forgetting it is still the right move.
            // Read off `error.param` rather than out of the sentence. OpenAI's message for a
            // bad voice is "Invalid value: 'george'. Supported values are: …" and does not carry
            // the word "voice" anywhere in it, so a prose match answers this one wrong — while
            // the structured field names it exactly, as the spike's refused `speed` showed.
            HttpStatusCode.BadRequest when string.Equals(param, "voice", StringComparison.Ordinal)
                => TtsFault.VoiceRejected,

            HttpStatusCode.TooManyRequests or >= HttpStatusCode.InternalServerError
                => TtsFault.Unreachable,

            _ => TtsFault.Unknown,
        };

        _logger.LogWarning(
            "OpenAI refused to speak ({Status}): {Because}", (int)response.StatusCode, said);

        return new TtsException($"OpenAI could not speak \"{Excerpt(text)}\": {said}", fault: fault);
    }

    /// <summary>
    /// The <c>error.message</c> OpenAI returns and the field it blames, or nulls where the body
    /// is not one of its errors at all.
    /// </summary>
    private static (string? Message, string? Param) Error(string body)
    {
        try
        {
            using var parsed = JsonDocument.Parse(body);

            if (!parsed.RootElement.TryGetProperty("error", out var error))
            {
                return (null, null);
            }

            return (
                error.TryGetProperty("message", out var message) ? message.GetString() : null,
                error.TryGetProperty("param", out var param) ? param.GetString() : null);
        }
        catch (JsonException)
        {
            return (null, null);
        }
    }

    /// <summary>
    /// d47's normalised rate to this provider's <c>speed</c>. A multiplier at both ends, so the
    /// conversion is the identity and the only work is the clamp — the range is wide enough that
    /// a value carried over from another provider lands inside it rather than being rejected.
    /// </summary>
    private static double SpeedFor(double rate) => Math.Clamp(rate, MinimumSpeed, MaximumSpeed);

    private static string Excerpt(string text) =>
        text.Length <= 40 ? text : text[..40] + "…";

    private sealed record SpeechRequest
    {
        public required string Model { get; init; }

        public required string Input { get; init; }

        public required string Voice { get; init; }

        [JsonPropertyName("response_format")]
        public required string ResponseFormat { get; init; }

        public double Speed { get; init; } = 1.0;

        /// <summary>
        /// How to perform it — accent, tone, intonation, delivery. Documented at 4,096 characters
        /// and documented <b>not</b> to work on <c>tts-1</c> or <c>tts-1-hd</c>, which is a second
        /// reason <see cref="DefaultModel"/> is pinned where it is.
        /// </summary>
        [JsonPropertyName("instructions")]
        public string? Instructions { get; init; }
    }

    private static string? Blank(string? value) => string.IsNullOrWhiteSpace(value) ? null : value;

    public void Dispose() => _http.Dispose();
}
