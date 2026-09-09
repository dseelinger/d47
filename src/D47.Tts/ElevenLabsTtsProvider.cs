using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using D47.Core.Audio;
using Microsoft.Extensions.Logging;

namespace D47.Tts;

/// <summary>The paid voices (Phase 11, "ElevenLabs").</summary>
public sealed class ElevenLabsTtsProvider : ITtsProvider, IDisposable
{
    private const string BaseUrl = "https://api.elevenlabs.io/v1";

    /// <summary>The name this provider's key is stored under.</summary>
    public const string KeySecretName = "elevenlabs.apiKey";

    public const string ProviderId = "elevenlabs";

    /// <summary>
    /// How many syntheses this provider will have in flight at once, across everything in the process.
    /// </summary>
    private const int MaxConcurrent = 3;

    private readonly SemaphoreSlim _inFlight = new(MaxConcurrent, MaxConcurrent);

    /// <summary>What speaks when nobody has chosen.</summary>
    public const string DefaultModel = ElevenLabsModels.Default;

    /// <summary>The language every line is synthesised as.</summary>
    public const string Language = "en";

    /// <summary>What leaves the machine.</summary>
    public const string EgressDisclosure =
        "The text of every line D47 speaks is sent to ElevenLabs to be turned into audio, along " +
        "with your API key. That includes re-voiced in-game messages when you have turned those " +
        "on, which are written by other players. No journal content, game state or other keys " +
        "are sent. Selecting a different voice provider stops all of it.";

    /// <summary>
    /// ElevenLabs accepts a speaking rate between these, and rejects the request outright outside them
    /// rather than clamping. d47's own rate is normalised around 1.0 with a wider range than this, so
    /// the conversion clamps — a Commander who set 1.6 for Edge gets this provider's fastest rather
    /// than an error and silence.
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
    private readonly Func<string?>? _model;

    private VoiceCatalogue? _voices;

    /// <summary>
    /// <param name="key"> Asked for rather than held, because a key can be added or replaced
    /// mid-session and the next line has to use it — "apply every setting without a restart" reaches in
    /// here (Phase 4).
    /// </summary>
    /// <param name="key">
    /// Asked for rather than held, because a key can be added or replaced mid-session and the next line
    /// has to use it — "apply every setting without a restart" reaches in here (Phase 4).
    /// </param>
    /// <param name="model">
    /// Asked for rather than held, for the same reason the key is: a Commander who switches models
    /// expects the next line to use the new one, not the next session.
    /// </param>
    public ElevenLabsTtsProvider(
        Func<string?> key,
        ILogger<ElevenLabsTtsProvider> logger,
        HttpClient? http = null,
        Func<string?>? model = null)
    {
        _key = key;
        _logger = logger;
        _http = http ?? new HttpClient();
        _ownsHttp = http is null;
        _model = model;
    }

    public string Id => ProviderId;

    public string Name => "ElevenLabs";

    /// <summary>The model in force right now, resolved against what d47 still offers.</summary>
    internal string Model => ElevenLabsModels.Named(_model?.Invoke());

    /// <summary>v3 performs bracketed direction; Flash reads it aloud.</summary>
    public bool ReadsAudioTags => ElevenLabsModels.ReadsTags(Model);

    /// <summary>How much text v3 would rather have at once, and nothing at all for Flash.</summary>
    public int GroupsSentencesUpTo => ElevenLabsModels.GroupsSentencesUpTo(Model);

    public async Task<VoiceCatalogue> ListVoicesAsync(CancellationToken cancellationToken = default)
    {
        if (_voices is { } cached)
        {
            return cached;
        }

        if (_key() is not { Length: > 0 } key)
        {
            // No key is not a failure to log loudly.
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
                // The service's own message, for the same reason a refused synthesis prefers it: "Invalid API
                // key" is what the Commander needs and 401 is not.
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

                    // **The famous ones are not offered** (remediation.md 17, item 13).
                    .Where(voice => voice.Name?.Contains('™', StringComparison.Ordinal) != true)
                    .Select(voice => new VoiceInfo(
                        voice.VoiceId!,
                        voice.Name ?? voice.VoiceId!,

                        // ElevenLabs voices are not locale-tagged the way Edge's are — the model is
                        // multilingual and a voice carries an accent rather than a locale.
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

    /// <summary>Numerals spelled out, and nothing else changed.</summary>
    public string Billable(string text) => SpokenNumbers.Expand(text);

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
            // Deliberately not a hardcoded fallback voice id.
            throw new TtsException("No ElevenLabs voice has been chosen. Pick one in Settings.");
        }

        var url = $"{BaseUrl}/text-to-speech/{Uri.EscapeDataString(voiceId)}?output_format=pcm_24000";

        // Queued rather than refused.
        await _inFlight.WaitAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, url);
            request.Headers.Add("xi-api-key", key);
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("audio/*"));
            var model = Model;

            request.Content = JsonContent.Create(
                new SynthesisRequest
                {
                    Text = Billable(text),
                    ModelId = model,
                    LanguageCode = Language,

                    // Omitted entirely for a model that does not read it, rather than sent and ignored. v3
                    // accepts 0.5 through 2.0 and acts on none of it, so a speed on the wire would be a
                    // number in the request log that never changed a sound — and a request that carries no
                    // rate is one nobody can misread later.
                    VoiceSettings = ElevenLabsModels.ReadsRate(model)
                        ? new VoiceSettings { Speed = SpeedFor(voice.Rate) }
                        : null,
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

            // Named with what was asked for, not with what was sent.
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

    /// <summary>d47's normalised rate to this provider's speed.</summary>
    internal static double SpeedFor(double rate) => Math.Clamp(rate, MinimumSpeed, MaximumSpeed);

    /// <summary>What went wrong, in words a Commander can act on.</summary>
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

    /// <summary>Whether a refusal was about the voice, and so whether d47 should stop using it.</summary>
    internal static TtsFault FaultFor(HttpStatusCode status, string? said, string voiceId) =>
        status is HttpStatusCode.BadRequest or HttpStatusCode.NotFound
        || said?.Contains(voiceId, StringComparison.Ordinal) == true

        // A voice this account may hold but this API will not speak — *"Famous voices can only be used within
        // the Reader App"* (remediation.md 17, item 13).
        || said?.Contains("Reader App", StringComparison.OrdinalIgnoreCase) == true
            ? TtsFault.VoiceRejected
            : TtsFault.Unknown;

    /// <summary>
    /// The <c>detail.message</c> ElevenLabs puts in an error body, or null when there is not one to
    /// read.
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
            // A body that will not read or will not parse.
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

        [JsonPropertyName("language_code")]
        public required string LanguageCode { get; init; }

        /// <summary>Omitted for a model that ignores it - see the call site.</summary>
        [JsonPropertyName("voice_settings")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public VoiceSettings? VoiceSettings { get; init; }
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
