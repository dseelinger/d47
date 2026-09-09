using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using D47.Core.Audio;
using Microsoft.Extensions.Logging;

namespace D47.Tts;

/// <summary>OpenAI's speech endpoint (Phase 58).</summary>
public sealed class OpenAiTtsProvider : ITtsProvider, IDisposable
{
    private const string SpeechUrl = "https://api.openai.com/v1/audio/speech";

    public const string ProviderId = "openai";

    /// <summary>The same secret the language-model provider uses, on purpose (Phase 58).</summary>
    public const string KeySecretName = "openai.apiKey";

    /// <summary>
    /// The dated snapshot, pinned for the reason <see cref="ElevenLabsTtsProvider.DefaultModel"/>
    /// gives: a floating alias is a voice that can change under a Commander who chose it.
    /// </summary>
    public const string DefaultModel = "gpt-4o-mini-tts-2025-12-15";

    /// <summary>The voice used when nothing has been chosen.</summary>
    public const string DefaultVoice = "onyx";

    /// <summary>
    /// The documented bounds of <c>speed</c>, and they are real: measured 2026-08-26 across the whole
    /// range, <c>0.25</c> gives 40.82 seconds where <c>1.0</c> gives 10.75
    /// (docs/spikes/openai-tts-language-and-speed.md §3).
    /// </summary>
    public const double MinimumSpeed = 0.25;

    /// <inheritdoc cref="MinimumSpeed"/>
    public const double MaximumSpeed = 4.0;

    /// <summary>In flight at once.</summary>
    private const int MaxConcurrent = 4;

    private readonly SemaphoreSlim _inFlight = new(MaxConcurrent, MaxConcurrent);

    /// <summary>Every voice the service offers, from the request schema's own <c>voice</c> description.</summary>
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

    /// <summary>
    /// <param name="direction"> How the core aboard should be performed, asked at the moment of
    /// synthesis (#49).
    /// </summary>
    /// <param name="direction">
    /// How the core aboard should be performed, asked at the moment of synthesis (#49).
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

    /// <summary>The thirteen, always, key or no key.</summary>
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

        // Queued rather than refused.
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
                    // Sent as written. `ITtsProvider.Billable` is left at its default because this provider
                    // rewrites nothing on the way out — ElevenLabs overrides it only because it spells
                    // numerals, and it is the rewritten length that lands on that bill.
                    Input = text,
                    Voice = voice.VoiceId is { Length: > 0 } chosen ? chosen : DefaultVoice,

                    // Raw samples rather than a container, so nothing has to be decoded on the way to the
                    // arbiter — the same trade ElevenLabs makes with pcm_24000.
                    ResponseFormat = "pcm",
                    Speed = SpeedFor(voice.Rate),

                    // Omitted entirely when there is nobody to perform — the field is nullable and is left
                    // out of the JSON on null, so a slot with no core sends exactly the request it sent
                    // before this existed (#49).
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

            // 24 kHz, 16-bit, mono, little-endian — doubled to the arbiter's 48 kHz exactly as the other paid
            // provider's is.
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

    /// <summary>One character, synthesised and thrown away, to prove a key (Phase 58).</summary>
    public async Task ProveKeyAsync(CancellationToken cancellationToken = default) =>
        _ = await SynthesizeAsync(".", VoiceSelection.Default, cancellationToken).ConfigureAwait(false);

    /// <summary>What the service said, classified into the two answers a Commander can act on.</summary>
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

            // A voice this provider does not have.
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
    /// The <c>error.message</c> OpenAI returns and the field it blames, or nulls where the body is not
    /// one of its errors at all.
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

    /// <summary>d47's normalised rate to this provider's <c>speed</c>.</summary>
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

        /// <summary>How to perform it — accent, tone, intonation, delivery.</summary>
        [JsonPropertyName("instructions")]
        public string? Instructions { get; init; }
    }

    private static string? Blank(string? value) => string.IsNullOrWhiteSpace(value) ? null : value;

    public void Dispose() => _http.Dispose();
}
