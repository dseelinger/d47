using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using D47.Core.Audio;
using Microsoft.Extensions.Logging;

namespace D47.Tts;

/// <summary>Cartesia's speech endpoint (Phase 60).</summary>
public sealed class CartesiaTtsProvider : ITtsProvider, IDisposable
{
    private const string BaseUrl = "https://api.cartesia.ai";

    public const string ProviderId = "cartesia";

    public const string KeySecretName = "cartesia.apiKey";

    /// <summary>The API version, which Cartesia pins by date in a header rather than by a path segment.</summary>
    public const string ApiVersion = "2024-11-13";

    /// <summary>The model.</summary>
    public const string DefaultModel = "sonic-2";

    /// <summary>The language every line is synthesised as.</summary>
    public const string Language = "en";

    /// <summary>
    /// The sample rate asked for, doubled to the arbiter's 48 kHz by <see cref="PcmUpsample.Double"/> —
    /// the same trade both paid providers already make, and an exact 2× rather than a resample.
    /// </summary>
    private const int SampleRate = 24_000;

    /// <summary>In flight at once, and tighter than the other two.</summary>
    private const int MaxConcurrent = 2;

    private readonly SemaphoreSlim _inFlight = new(MaxConcurrent, MaxConcurrent);

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private readonly Func<string?> _key;
    private readonly ILogger<CartesiaTtsProvider> _logger;
    private readonly HttpClient _http;

    private VoiceCatalogue? _voices;

    /// <summary>
    /// <param name="key"> Asked for rather than held, because a key can be added or replaced
    /// mid-session and the next line has to use it (Phase 4).
    /// </summary>
    /// <param name="key">
    /// Asked for rather than held, because a key can be added or replaced mid-session and the next line
    /// has to use it (Phase 4).
    /// </param>
    public CartesiaTtsProvider(
        Func<string?> key,
        ILogger<CartesiaTtsProvider> logger,
        HttpMessageHandler? handler = null)
    {
        _key = key;
        _logger = logger;
        _http = handler is null ? new HttpClient() : new HttpClient(handler);
        _http.Timeout = TimeSpan.FromSeconds(60);
    }

    public string Id => ProviderId;

    public string Name => "Cartesia";

    /// <summary>
    /// The account's voices, paged. 924 of them on the Commander's account, which is enough that paging
    /// is the normal case rather than an edge one — the endpoint answers 100 at a time and says whether
    /// there are more.
    /// </summary>
    public async Task<VoiceCatalogue> ListVoicesAsync(CancellationToken cancellationToken = default)
    {
        if (_voices is { } cached)
        {
            return cached;
        }

        if (_key() is not { Length: > 0 } key)
        {
            // Not cached, deliberately: the key is the thing most likely to arrive next.
            _logger.LogDebug("No Cartesia key is stored, so no voices can be listed");
            return VoiceCatalogue.NoKey("no key is stored");
        }

        try
        {
            var collected = new List<VoiceInfo>();
            var path = "/voices/?limit=100";

            while (path is not null)
            {
                using var request = Request(HttpMethod.Get, path, key);
                using var response = await _http.SendAsync(request, cancellationToken).ConfigureAwait(false);

                if (!response.IsSuccessStatusCode)
                {
                    var said = await MessageAsync(response, cancellationToken).ConfigureAwait(false);

                    _logger.LogWarning(
                        "Cartesia would not list its voices: {Status} {Said}",
                        (int)response.StatusCode,
                        said);

                    return response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden
                        ? VoiceCatalogue.KeyRejected(said ?? "the key was rejected")
                        : VoiceCatalogue.Unreachable(said ?? $"it answered {(int)response.StatusCode}");
                }

                var page = Page(
                    await response.Content
                        .ReadFromJsonAsync<JsonElement>(Json, cancellationToken)
                        .ConfigureAwait(false));

                collected.AddRange(
                    (page.Data ?? [])
                        .Where(voice => voice.Id is not null)
                        .Select(voice => new VoiceInfo(
                            voice.Id!,
                            voice.Name ?? voice.Id!,

                            // The language tag, which this provider publishes and which is a locale rather
                            // than an accent label — so it is read as one.
                            voice.Language ?? "multilingual",

                            // Published for all 924 — 480 feminine, 443 masculine, one neutral — which is
                            // what lets `VoiceCast.ForSender` match sex here where it cannot on OpenAI.
                            voice.Gender)));

                path = page is { HasMore: true, NextPage: { Length: > 0 } next }
                    ? $"/voices/?limit=100&starting_after={Uri.EscapeDataString(next)}"
                    : null;

                // A page that answered nothing but claimed more would page for ever.
                if (page.Data is null or { Count: 0 })
                {
                    break;
                }
            }

            _voices = VoiceCatalogue.Of(collected);

            _logger.LogInformation("Cartesia offers {Count} voices", _voices.Count);
            return _voices;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "Could not list Cartesia voices");
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
            throw new TtsException(
                "No Cartesia API key is stored. Add one in Settings.",
                fault: TtsFault.KeyRejected);
        }

        if (voice.VoiceId is not { Length: > 0 } voiceId)
        {
            // No hardcoded fallback id, for ElevenLabs' reason: this catalogue's ids are opaque and belong to
            // an account, so a guess here fails as a 404 that reads like an outage.
            throw new TtsException("No Cartesia voice has been chosen. Pick one in Settings.");
        }

        // Queued rather than refused.
        await _inFlight.WaitAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            using var request = Request(HttpMethod.Post, "/tts/bytes", key);
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("audio/*"));

            request.Content = JsonContent.Create(
                new SpeechRequest
                {
                    ModelId = DefaultModel,
                    Transcript = text,

                    // No `__experimental_controls`, and that is the finding rather than an oversight: the
                    // speed inside it is validated and inert, so sending one would be d47 asking for
                    // something it has measured not to happen (docs/spikes/cartesia-voices-and-speed.md §3).
                    Voice = new VoiceRef { Id = voiceId },

                    // Raw samples rather than a container, so nothing has to be decoded on the way to the
                    // arbiter — the same trade both paid providers already make.
                    OutputFormat = new OutputFormat
                    {
                        Container = "raw",
                        Encoding = "pcm_s16le",
                        SampleRate = SampleRate,
                    },

                    // Pinned, which is the property that makes this provider eligible for a slot carrying
                    // another player's words.
                    Language = Language,
                },
                options: Json);

            using var response = await _http.SendAsync(request, cancellationToken).ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                throw await DescribeAsync(response, text, voiceId, cancellationToken).ConfigureAwait(false);
            }

            var pcm = await response.Content.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false);

            if (pcm.Length == 0)
            {
                throw new TtsException($"Cartesia returned no audio for \"{Excerpt(text)}\".");
            }

            // 24 kHz, 16-bit, mono, little-endian — doubled to the arbiter's 48 kHz exactly as the other two
            // paid providers' output is.
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
                $"Cartesia could not speak \"{Excerpt(text)}\": {ex.Message}",
                ex,
                TtsFault.Unreachable);
        }
        finally
        {
            _inFlight.Release();
        }
    }

    /// <summary>One page of voices, from either shape the endpoint answers in.</summary>
    private static VoicePage Page(JsonElement body) =>
        body.ValueKind == JsonValueKind.Array
            ? new VoicePage { Data = body.Deserialize<IReadOnlyList<CartesiaVoice>>(Json) }
            : body.Deserialize<VoicePage>(Json) ?? new VoicePage();

    /// <summary>The two headers every call needs.</summary>
    private static HttpRequestMessage Request(HttpMethod method, string path, string key)
    {
        var request = new HttpRequestMessage(method, $"{BaseUrl}{path}");
        request.Headers.Add("X-API-Key", key);
        request.Headers.Add("Cartesia-Version", ApiVersion);
        return request;
    }

    /// <summary>
    /// What the service said, classified into the answers a Commander can act on — the distinction
    /// docs/spikes/elevenlabs-voice-sources.md §3 requires, since "refused the key"
    /// and "could not be reached" have different remedies.
    /// </summary>
    private async Task<TtsException> DescribeAsync(
        HttpResponseMessage response,
        string text,
        string voiceId,
        CancellationToken cancellationToken)
    {
        var said = await MessageAsync(response, cancellationToken).ConfigureAwait(false)
                   ?? response.ReasonPhrase
                   ?? response.StatusCode.ToString();

        var fault = response.StatusCode switch
        {
            HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden => TtsFault.KeyRejected,

            // A voice this account cannot use.
            HttpStatusCode.NotFound => TtsFault.VoiceRejected,
            HttpStatusCode.BadRequest when said.Contains(voiceId, StringComparison.Ordinal)
                => TtsFault.VoiceRejected,

            HttpStatusCode.TooManyRequests or >= HttpStatusCode.InternalServerError
                => TtsFault.Unreachable,

            _ => TtsFault.Unknown,
        };

        _logger.LogWarning(
            "Cartesia refused to speak ({Status}): {Because}", (int)response.StatusCode, said);

        return new TtsException($"Cartesia could not speak \"{Excerpt(text)}\": {said}", fault: fault);
    }

    /// <summary>The message out of an error body, or null where there is not one to read.</summary>
    private static async Task<string?> MessageAsync(
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

            // A refusal is plain text as often as it is JSON here — the speed refusal the spike recorded
            // reads "invalid voice controls: speed float must be between -1.0 and 1.0", which is the whole of
            // the useful answer — so the body itself is the fallback rather than nothing.
            try
            {
                using var parsed = JsonDocument.Parse(body);

                if (parsed.RootElement.ValueKind == JsonValueKind.Object
                    && parsed.RootElement.TryGetProperty("error", out var error))
                {
                    return error.ValueKind == JsonValueKind.String
                        ? error.GetString()
                        : error.TryGetProperty("message", out var message)
                            ? message.GetString()
                            : body;
                }

                return body;
            }
            catch (JsonException)
            {
                return body;
            }
        }
        catch (Exception)
        {
            return null;
        }
    }

    private static string Excerpt(string text) => text.Length <= 40 ? text : text[..40] + "…";

    private sealed record SpeechRequest
    {
        [JsonPropertyName("model_id")]
        public required string ModelId { get; init; }

        public required string Transcript { get; init; }

        public required VoiceRef Voice { get; init; }

        [JsonPropertyName("output_format")]
        public required OutputFormat OutputFormat { get; init; }

        public required string Language { get; init; }
    }

    private sealed record VoiceRef
    {
        /// <summary>How the voice is being named.</summary>
        public string Mode { get; init; } = "id";

        public required string Id { get; init; }
    }

    private sealed record OutputFormat
    {
        public required string Container { get; init; }

        public required string Encoding { get; init; }

        [JsonPropertyName("sample_rate")]
        public required int SampleRate { get; init; }
    }

    private sealed record VoicePage
    {
        public IReadOnlyList<CartesiaVoice>? Data { get; init; }

        [JsonPropertyName("has_more")]
        public bool HasMore { get; init; }

        [JsonPropertyName("next_page")]
        public string? NextPage { get; init; }
    }

    private sealed record CartesiaVoice
    {
        public string? Id { get; init; }

        public string? Name { get; init; }

        public string? Language { get; init; }

        public string? Gender { get; init; }
    }

    public void Dispose() => _http.Dispose();
}
