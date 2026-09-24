using System.Diagnostics;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using D47.Core.Listening;
using Microsoft.Extensions.Logging;

namespace D47.Stt;

/// <summary>Deepgram's <c>POST /v1/listen</c>, one finished utterance per request.</summary>
public sealed class DeepgramTranscriber : ISpeechTranscriber
{
    public const string Provider = "Deepgram";

    public const string Endpoint = "https://api.deepgram.com/v1/listen";

    public const string DeepgramModel = "nova-3";

    /// <summary>The longest request URL sent; <c>keyterm</c> parameters stop before it.</summary>
    internal const int MostUrlCharacters = 1999;

    private readonly Func<string?> _key;
    private readonly ILogger<DeepgramTranscriber> _logger;
    private readonly HttpClient _http;

    /// <param name="key">Read at each call and never held.</param>
    public DeepgramTranscriber(
        Func<string?> key,
        ILogger<DeepgramTranscriber> logger,
        HttpMessageHandler? handler = null)
    {
        _key = key;
        _logger = logger;
        _http = handler is null ? new HttpClient() : new HttpClient(handler);
        _http.Timeout = TimeSpan.FromSeconds(30);
    }

    public string? Model => DeepgramModel;

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
            using var request = new HttpRequestMessage(HttpMethod.Post, Address(properNouns));
            request.Headers.Authorization = new AuthenticationHeaderValue("Token", key);

            var audio = new ByteArrayContent(HostedTranscription.Wav(utterance));
            audio.Headers.ContentType = new MediaTypeHeaderValue("audio/wav");
            request.Content = audio;

            using var response = await _http.SendAsync(request, cancellationToken).ConfigureAwait(false);

            var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                throw Refused(response, body);
            }

            var (text, confidence) = Alternative(body) ?? throw new TranscriptionUnavailableException(
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
                Model = DeepgramModel,
                Confidence = confidence,
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
    /// The request URL: the fixed options, then one <c>keyterm</c> per name from the front, stopping at the
    /// first that would take the URL past <see cref="MostUrlCharacters"/>.
    /// </summary>
    internal static Uri Address(IReadOnlyList<string> properNouns)
    {
        var url = new StringBuilder(Endpoint)
            .Append("?model=").Append(DeepgramModel)
            .Append("&language=en&smart_format=true");

        foreach (var name in properNouns)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                continue;
            }

            var parameter = "&keyterm=" + Uri.EscapeDataString(name.Trim());

            if (url.Length + parameter.Length > MostUrlCharacters)
            {
                break;
            }

            url.Append(parameter);
        }

        return new Uri(url.ToString());
    }

    /// <summary>Classifies a non-success answer, taking the message from <c>err_msg</c> only.</summary>
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

    private static string? ErrorMessage(string body)
    {
        try
        {
            using var parsed = JsonDocument.Parse(body);

            return parsed.RootElement.ValueKind == JsonValueKind.Object
                   && parsed.RootElement.TryGetProperty("err_msg", out var message)
                   && message.ValueKind == JsonValueKind.String
                ? message.GetString()
                : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary><c>results.channels[0].alternatives[0]</c>'s transcript and confidence, or null if absent.</summary>
    private static (string Text, double Confidence)? Alternative(string body)
    {
        try
        {
            using var parsed = JsonDocument.Parse(body);

            if (parsed.RootElement.ValueKind != JsonValueKind.Object
                || !parsed.RootElement.TryGetProperty("results", out var results)
                || results.ValueKind != JsonValueKind.Object
                || !results.TryGetProperty("channels", out var channels)
                || channels.ValueKind != JsonValueKind.Array
                || channels.GetArrayLength() == 0
                || channels[0].ValueKind != JsonValueKind.Object
                || !channels[0].TryGetProperty("alternatives", out var alternatives)
                || alternatives.ValueKind != JsonValueKind.Array
                || alternatives.GetArrayLength() == 0
                || alternatives[0].ValueKind != JsonValueKind.Object)
            {
                return null;
            }

            var first = alternatives[0];

            if (!first.TryGetProperty("transcript", out var transcript)
                || transcript.ValueKind != JsonValueKind.String)
            {
                return null;
            }

            var confidence = first.TryGetProperty("confidence", out var value) && value.ValueKind == JsonValueKind.Number
                ? Math.Clamp(value.GetDouble(), 0, 1)
                : 1;

            return (transcript.GetString()!, confidence);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    public void Dispose() => _http.Dispose();
}
