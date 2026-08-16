using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using D47.Core.Audio;
using Microsoft.Extensions.Logging;

namespace D47.Tts;

/// <summary>
/// The paid voices (list.md Phase 11, "ElevenLabs").
/// <para>
/// The whole requirement is that a Commander with a key hears it and nothing else changes.
/// That is what <see cref="ITtsProvider"/> already guaranteed — this class exists entirely
/// below that line, and the arbiter, the sentence splitter, the caption timing and every
/// caller are untouched by its arrival.
/// </para>
/// <para>
/// Audio is requested as <c>pcm_24000</c> rather than MP3, for the same reason Edge's raw
/// output was preferred: no decoder in the dependency graph, and the identical 2× upsample to
/// the arbiter's 48 kHz. Unlike Edge this is a documented, supported API, so a failure here is
/// a real failure worth reporting rather than a sign that an undocumented endpoint moved.
/// </para>
/// </summary>
public sealed class ElevenLabsTtsProvider : ITtsProvider, IDisposable
{
    private const string BaseUrl = "https://api.elevenlabs.io/v1";

    /// <summary>
    /// The name this provider's key is stored under. Matches the shape the LLM providers use,
    /// so the secret store holds one kind of thing.
    /// </summary>
    public const string KeySecretName = "elevenlabs.apiKey";

    public const string ProviderId = "elevenlabs";

    /// <summary>
    /// How many syntheses this provider will have in flight at once, across everything in the
    /// process.
    /// <para>
    /// The account has a concurrency limit - the cheapest tiers allow five - and d47 renders a
    /// reply's sentences <em>concurrently</em> on purpose, so the second is usually ready before
    /// the first has finished playing. A six-sentence reply therefore asked for six at once and
    /// the account refused the tail of it, which arrived as a red banner and a sentence the
    /// Commander never heard. Callouts, crew lines and re-voiced comms all share the same
    /// account, so the gate has to be here rather than in any one pipeline.
    /// </para>
    /// <para>
    /// Three rather than five: it is under the smallest published limit with room for a callout
    /// to overtake a reply, and the latency it buys is almost all in the first sentence anyway.
    /// </para>
    /// </summary>
    private const int MaxConcurrent = 3;

    private readonly SemaphoreSlim _inFlight = new(MaxConcurrent, MaxConcurrent);

    /// <summary>
    /// The multilingual model. Named rather than left to the service's default so that a
    /// change on their side is not a change in what the Commander hears, and because the speed
    /// control this provider exposes is a property of the model generation rather than of the
    /// account.
    /// </summary>
    public const string DefaultModel = "eleven_multilingual_v2";

    /// <summary>
    /// What leaves the machine. Stated here so the settings row and the documentation page read
    /// it from one place (list.md Phase 4, "Say what each provider receives").
    /// </summary>
    public const string EgressDisclosure =
        "The text of every line D47 speaks is sent to ElevenLabs to be turned into audio, along " +
        "with your API key. That includes re-voiced in-game messages when you have turned those " +
        "on, which are written by other players. No journal content, game state or other keys " +
        "are sent. Selecting a different voice provider stops all of it.";

    /// <summary>
    /// ElevenLabs accepts a speaking rate between these, and rejects the request outright
    /// outside them rather than clamping. d47's own rate is normalised around 1.0 with a wider
    /// range than this, so the conversion clamps — a Commander who set 1.6 for Edge gets this
    /// provider's fastest rather than an error and silence.
    /// </summary>
    public const double MinimumSpeed = 0.7;

    public const double MaximumSpeed = 1.2;

    private static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
    };

    private readonly ILogger<ElevenLabsTtsProvider> _logger;
    private readonly HttpClient _http;
    private readonly bool _ownsHttp;
    private readonly Func<string?> _key;

    private VoiceCatalogue? _voices;

    /// <param name="key">
    /// Asked for rather than held, because a key can be added or replaced mid-session and the
    /// next line has to use it — "apply every setting without a restart" reaches in here
    /// (list.md Phase 4). Null means no key is stored yet, which is a capability being off
    /// rather than an error to raise.
    /// </param>
    public ElevenLabsTtsProvider(
        Func<string?> key,
        ILogger<ElevenLabsTtsProvider> logger,
        HttpClient? http = null)
    {
        _key = key;
        _logger = logger;
        _http = http ?? new HttpClient();
        _ownsHttp = http is null;
    }

    public string Id => ProviderId;

    public string Name => "ElevenLabs";

    public async Task<VoiceCatalogue> ListVoicesAsync(CancellationToken cancellationToken = default)
    {
        if (_voices is { } cached)
        {
            return cached;
        }

        if (_key() is not { Length: > 0 } key)
        {
            // No key is not a failure to log loudly. The picker's contract is that an empty
            // list still lets the Commander keep the current value or type one (list.md Phase 4)
            // — and it now says which kind of empty this is, rather than showing the same
            // nothing it shows for a refused key.
            //
            // Not cached, deliberately: the key is the thing most likely to arrive next.
            _logger.LogDebug("No ElevenLabs key is stored, so no voices can be listed");
            return VoiceCatalogue.NoKey("no key is stored");
        }

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, $"{BaseUrl}/voices");
            request.Headers.Add("xi-api-key", key);

            using var response = await _http.SendAsync(request, cancellationToken).ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                // The service's own message, for the same reason a refused synthesis prefers it:
                // "Invalid API key" is what the Commander needs and 401 is not. Measured — every
                // one of no-header, bad-key and a 500 answers with a distinct `detail.message`.
                var said = await MessageFromBodyAsync(response, cancellationToken).ConfigureAwait(false);

                _logger.LogWarning(
                    "ElevenLabs would not list its voices: {Status} {Said}", (int)response.StatusCode, said);

                return response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden
                    ? VoiceCatalogue.KeyRejected(said ?? "the key was rejected")
                    : VoiceCatalogue.Unreachable(said ?? $"it answered {(int)response.StatusCode}");
            }

            var listed = await response.Content
                .ReadFromJsonAsync<VoiceListResponse>(Json, cancellationToken)
                .ConfigureAwait(false);

            _voices = VoiceCatalogue.Of(
            [
                .. (listed?.Voices ?? [])
                    .Where(voice => voice.VoiceId is not null)
                    .Select(voice => new VoiceInfo(
                        voice.VoiceId!,
                        voice.Name ?? voice.VoiceId!,

                        // ElevenLabs voices are not locale-tagged the way Edge's are — the
                        // model is multilingual and a voice carries an accent rather than a
                        // locale. The accent label is the nearest true thing, and the picker
                        // renders it in the same slot.
                        voice.Labels?.GetValueOrDefault("accent") ?? "multilingual",
                        voice.Labels?.GetValueOrDefault("gender"))),
            ]);

            _logger.LogInformation("ElevenLabs offers {Count} voices", _voices.Count);
            return _voices;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "Could not list ElevenLabs voices");
            return VoiceCatalogue.Unreachable(ex.Message);
        }
    }

    public async Task<AudioClip> SynthesizeAsync(
        string text,
        VoiceSelection voice,
        CancellationToken cancellationToken = default)
    {
        if (_key() is not { Length: > 0 } key)
        {
            throw new TtsException("No ElevenLabs API key is stored. Add one in Settings.");
        }

        if (voice.VoiceId is not { Length: > 0 } voiceId)
        {
            // Deliberately not a hardcoded fallback voice id. Edge can default to a named voice
            // because its catalogue is fixed and public; an ElevenLabs account's voice list is
            // its own, so guessing an id here would fail as a 404 that reads like an outage.
            throw new TtsException("No ElevenLabs voice has been chosen. Pick one in Settings.");
        }

        var url = $"{BaseUrl}/text-to-speech/{Uri.EscapeDataString(voiceId)}?output_format=pcm_24000";

        // Queued rather than refused. Waiting a moment for a slot is a sentence that arrives
        // late; going ahead without one is a sentence that never arrives at all.
        await _inFlight.WaitAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, url);
            request.Headers.Add("xi-api-key", key);
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("audio/*"));
            request.Content = JsonContent.Create(
                new SynthesisRequest
                {
                    Text = text,
                    ModelId = DefaultModel,
                    VoiceSettings = new VoiceSettings { Speed = SpeedFor(voice.Rate) },
                },
                options: Json);

            using var response = await _http.SendAsync(request, cancellationToken).ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                var (reason, fault) = await DescribeAsync(response, text, voiceId, cancellationToken)
                    .ConfigureAwait(false);

                throw new TtsException(reason, fault: fault);
            }

            var pcm = await response.Content.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false);

            if (pcm.Length == 0)
            {
                throw new TtsException($"ElevenLabs returned no audio for \"{Excerpt(text)}\".");
            }

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
            throw new TtsException($"ElevenLabs could not speak \"{Excerpt(text)}\": {ex.Message}", ex);
        }
        finally
        {
            _inFlight.Release();
        }
    }

    /// <summary>
    /// d47's normalised rate to this provider's speed. The conversion is the entire reason
    /// <see cref="VoiceSelection.Rate"/> is normalised rather than stored in provider units:
    /// Edge takes a percentage offset with a wide range, this takes a multiplier with a narrow
    /// one, and a settings value that meant either depending on what was selected would be a
    /// value that changed meaning underneath the Commander.
    /// </summary>
    internal static double SpeedFor(double rate) => Math.Clamp(rate, MinimumSpeed, MaximumSpeed);

    /// <summary>
    /// What went wrong, in words a Commander can act on.
    /// <para>
    /// <b>The service's own message is preferred over anything mapped from the status code</b>,
    /// because it is consistently the more useful of the two and the status code is consistently
    /// not what you would guess. Verified against the live API: an invalid key is a 401
    /// ("Invalid API key"), but a bad voice id is a <em>400</em> — the voice is validated before
    /// the key, so a request with both wrong reports the voice. A mapping table alone would have
    /// answered "it answered 400" there, which tells the Commander nothing at all.
    /// </para>
    /// <para>
    /// That same verified behaviour is what the fault is read from. A 400 or a 404 is about the
    /// voice — 400 because the voice is validated first, 404 because the id is not on this
    /// account — and so is any answer whose message quotes the id back, which is what "an
    /// invalid ID has been received: 'en-US-RogerNeural'" is. A voice d47 can see is refused is
    /// a voice d47 can stop using; every other reason here is one only the Commander can act on.
    /// </para>
    /// </summary>
    private static async Task<(string Reason, TtsFault Fault)> DescribeAsync(
        HttpResponseMessage response,
        string text,
        string voiceId,
        CancellationToken cancellationToken)
    {
        var said = await MessageFromBodyAsync(response, cancellationToken).ConfigureAwait(false);

        var reason = said
                     ?? response.StatusCode switch
                     {
                         HttpStatusCode.Unauthorized => "the API key was rejected",
                         HttpStatusCode.Forbidden => "the API key is not allowed to use that voice or model",
                         HttpStatusCode.NotFound => "that voice id is not on this account",
                         HttpStatusCode.TooManyRequests => "the account is rate limited",
                         HttpStatusCode.PaymentRequired => "the account is out of characters",
                         _ => $"it answered {(int)response.StatusCode}",
                     };

        return (
            $"ElevenLabs could not speak \"{Excerpt(text)}\": {reason}.",
            FaultFor(response.StatusCode, said, voiceId));
    }

    /// <summary>
    /// Whether a refusal was about the voice, and so whether d47 should stop using it.
    /// <para>
    /// A 400 or a 404 is: 400 because the voice is validated ahead of the key, 404 because the
    /// id is not on this account. So is any answer whose message quotes the id back, which is
    /// what "an invalid ID has been received: 'en-US-RogerNeural'" is — and that one is worth
    /// matching on its own, because the status it arrives with is not worth relying on.
    /// </para>
    /// <para>
    /// Everything else stays <see cref="TtsFault.Unknown"/> on purpose. A rejected key, an empty
    /// account and a rate limit are all things only the Commander can act on, and discarding a
    /// perfectly good voice on the way to telling them would be a second fault to recover from.
    /// </para>
    /// </summary>
    internal static TtsFault FaultFor(HttpStatusCode status, string? said, string voiceId) =>
        status is HttpStatusCode.BadRequest or HttpStatusCode.NotFound
        || said?.Contains(voiceId, StringComparison.Ordinal) == true
            ? TtsFault.VoiceRejected
            : TtsFault.Unknown;

    /// <summary>
    /// The <c>detail.message</c> ElevenLabs puts in an error body, or null when there is not one
    /// to read. Never throws: this runs while already reporting a failure, and a failure to
    /// describe a failure must not replace it.
    /// </summary>
    private static async Task<string?> MessageFromBodyAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        try
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

            if (body is not { Length: > 0 and < 4000 })
            {
                return null;
            }

            using var document = JsonDocument.Parse(body);

            if (document.RootElement.TryGetProperty("detail", out var detail)
                && detail.ValueKind == JsonValueKind.Object
                && detail.TryGetProperty("message", out var message)
                && message.GetString() is { Length: > 0 } stated)
            {
                return stated.TrimEnd('.');
            }

            return null;
        }
        catch (Exception)
        {
            // A body that will not read or will not parse. The status-code mapping is the
            // fallback and is why this returns null rather than raising.
            return null;
        }
    }

    private static string Excerpt(string text) => text.Length <= 40 ? text : text[..40] + "…";

    private sealed record VoiceListResponse
    {
        public List<ElevenVoice>? Voices { get; init; }
    }

    private sealed record ElevenVoice
    {
        [JsonPropertyName("voice_id")]
        public string? VoiceId { get; init; }

        public string? Name { get; init; }

        public Dictionary<string, string>? Labels { get; init; }
    }

    private sealed record SynthesisRequest
    {
        public required string Text { get; init; }

        [JsonPropertyName("model_id")]
        public required string ModelId { get; init; }

        [JsonPropertyName("voice_settings")]
        public required VoiceSettings VoiceSettings { get; init; }
    }

    private sealed record VoiceSettings
    {
        public required double Speed { get; init; }
    }

    public void Dispose()
    {
        _inFlight.Dispose();

        if (_ownsHttp)
        {
            _http.Dispose();
        }
    }
}
