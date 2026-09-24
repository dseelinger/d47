using System.Diagnostics;
using System.Net.Http.Headers;
using System.Text.Json;
using D47.Core.Listening;
using Microsoft.Extensions.Logging;

namespace D47.Stt;

/// <summary>ElevenLabs Scribe's <c>POST /v1/speech-to-text</c>, one finished utterance per request.</summary>
public sealed class ElevenLabsScribeTranscriber : ISpeechTranscriber
{
    public const string Provider = "ElevenLabs";

    public const string Endpoint = "https://api.elevenlabs.io/v1/speech-to-text";

    public const string ScribeModel = "scribe_v2";

    /// <summary>At 100 keyterms Scribe bills every request as at least 20 seconds of audio.</summary>
    internal const int MostKeyterms = 99;

    internal const int KeytermCharacters = 50;

    internal const int KeytermWords = 5;

    private static readonly char[] Unsupported = ['<', '>', '{', '}', '[', ']', '\\'];

    private readonly Func<string?> _key;
    private readonly ILogger<ElevenLabsScribeTranscriber> _logger;
    private readonly HttpClient _http;

    /// <param name="key">Read at each call and never held.</param>
    public ElevenLabsScribeTranscriber(
        Func<string?> key,
        ILogger<ElevenLabsScribeTranscriber> logger,
        HttpMessageHandler? handler = null)
    {
        _key = key;
        _logger = logger;
        _http = handler is null ? new HttpClient() : new HttpClient(handler);
        _http.Timeout = TimeSpan.FromSeconds(30);
    }

    public string? Model => ScribeModel;

    public bool IsReady => _key() is { Length: > 0 };

    public async Task<Transcription> TranscribeAsync(
        Utterance utterance,
        IReadOnlyList<string> properNouns,
        CancellationToken cancellationToken = default)
    {
        if (_key() is not { Length: > 0 } key)
        {
            throw new TranscriptionUnavailableException(
                Provider,
                TranscriptionFailure.KeyRejected,
                $"No {Provider} API key is stored. Add one in Settings.");
        }

        var stopwatch = Stopwatch.StartNew();

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, Endpoint);
            request.Headers.Add("xi-api-key", key);

            using var form = new MultipartFormDataContent();

            var file = new ByteArrayContent(HostedTranscription.Wav(utterance));
            file.Headers.ContentType = new MediaTypeHeaderValue("audio/wav");
            form.Add(file, "file", "utterance.wav");
            form.Add(new StringContent(ScribeModel), "model_id");
            form.Add(new StringContent("en"), "language_code");
            form.Add(new StringContent("false"), "tag_audio_events");

            foreach (var term in Keyterms(properNouns))
            {
                form.Add(new StringContent(term), "keyterms");
            }

            request.Content = form;

            using var response = await _http.SendAsync(request, cancellationToken).ConfigureAwait(false);

            var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                throw Refused(response, body);
            }

            var text = Text(body) ?? throw new TranscriptionUnavailableException(
                Provider,
                TranscriptionFailure.Failed,
                $"{Provider} answered without a transcript.")
            {
                Detail = "no transcript came back",
            };

            _logger.LogInformation(
                "{Provider} transcribed {Seconds:0.#}s of audio in {Elapsed}ms with {Nouns} name hints",
                Provider,
                utterance.Duration.TotalSeconds,
                stopwatch.ElapsedMilliseconds,
                properNouns.Count);

            return new Transcription(text.Trim())
            {
                Elapsed = stopwatch.Elapsed,
                Model = ScribeModel,
            };
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (TranscriptionUnavailableException)
        {
            throw;
        }
        catch (Exception ex) when (ex is HttpRequestException or OperationCanceledException)
        {
            // An OperationCanceledException not from the caller's token is the client's timeout.
            _logger.LogWarning("{Provider} could not be reached: {Because}", Provider, ex.Message);

            throw new TranscriptionUnavailableException(
                Provider,
                TranscriptionFailure.Unreachable,
                $"{Provider} could not be reached: {ex.Message}",
                ex);
        }
    }

    /// <summary>
    /// The names Scribe accepts as keyterms, from the front, up to <see cref="MostKeyterms"/>. A name longer
    /// than <see cref="KeytermCharacters"/>, of more than <see cref="KeytermWords"/> words, or holding a
    /// character Scribe does not accept is skipped.
    /// </summary>
    internal static IReadOnlyList<string> Keyterms(IReadOnlyList<string> properNouns) =>
    [
        .. properNouns
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Select(name => name.Trim())
            .Where(name => name.Length <= KeytermCharacters
                           && name.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length <= KeytermWords
                           && name.IndexOfAny(Unsupported) < 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(MostKeyterms),
    ];

    /// <summary>Classifies a non-success answer, taking the message from <c>detail</c> only.</summary>
    private TranscriptionUnavailableException Refused(HttpResponseMessage response, string body)
    {
        var said = ErrorMessage(body) ?? response.ReasonPhrase ?? response.StatusCode.ToString();

        var reason = HostedTranscription.Reason(response.StatusCode);

        _logger.LogWarning(
            "{Provider} refused to transcribe ({Status}): {Because}", Provider, (int)response.StatusCode, said);

        return new TranscriptionUnavailableException(
            Provider, reason, $"{Provider} could not transcribe: {said}")
        {
            Detail = said,
        };
    }

    /// <summary><c>detail.message</c>, or the first <c>detail[].msg</c> of a validation error.</summary>
    private static string? ErrorMessage(string body)
    {
        try
        {
            using var parsed = JsonDocument.Parse(body);

            if (parsed.RootElement.ValueKind != JsonValueKind.Object
                || !parsed.RootElement.TryGetProperty("detail", out var detail))
            {
                return null;
            }

            var message = detail.ValueKind switch
            {
                JsonValueKind.Object when detail.TryGetProperty("message", out var m) => m,
                JsonValueKind.Array when detail.GetArrayLength() > 0
                                         && detail[0].ValueKind == JsonValueKind.Object
                                         && detail[0].TryGetProperty("msg", out var m) => m,
                _ => default,
            };

            return message.ValueKind == JsonValueKind.String && message.GetString() is { Length: > 0 } stated
                ? stated
                : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string? Text(string body)
    {
        try
        {
            using var parsed = JsonDocument.Parse(body);

            return parsed.RootElement.ValueKind == JsonValueKind.Object
                   && parsed.RootElement.TryGetProperty("text", out var text)
                   && text.ValueKind == JsonValueKind.String
                ? text.GetString()
                : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    public void Dispose() => _http.Dispose();
}
