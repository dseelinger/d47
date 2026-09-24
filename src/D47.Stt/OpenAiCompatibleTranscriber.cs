using System.Diagnostics;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using D47.Core.Listening;
using Microsoft.Extensions.Logging;

namespace D47.Stt;

/// <summary>A service that serves OpenAI's <c>POST /v1/audio/transcriptions</c>: Groq and OpenAI.</summary>
public sealed class OpenAiCompatibleTranscriber : ISpeechTranscriber
{
    public const string GroqEndpoint = "https://api.groq.com/openai/v1/audio/transcriptions";

    public const string GroqModel = "whisper-large-v3-turbo";

    public const string OpenAiEndpoint = "https://api.openai.com/v1/audio/transcriptions";

    public const string OpenAiModel = "gpt-4o-mini-transcribe";

    /// <summary>Whisper reads only the last 224 tokens of a prompt.</summary>
    internal const int PromptTokens = 224;

    /// <summary>
    /// Characters per token, estimated low: system names such as "Col 285 Sector AB-C d14-5" are mostly
    /// digits and hyphens, which tokenize at about two characters each.
    /// </summary>
    internal const int CharactersPerToken = 2;

    internal const int PromptCharacters = PromptTokens * CharactersPerToken;

    private readonly string _provider;
    private readonly Func<string?> _key;
    private readonly Uri _endpoint;
    private readonly string _model;
    private readonly ILogger<OpenAiCompatibleTranscriber> _logger;
    private readonly HttpClient _http;

    /// <param name="key">Read at each call and never held.</param>
    public OpenAiCompatibleTranscriber(
        string provider,
        Func<string?> key,
        string endpoint,
        string model,
        ILogger<OpenAiCompatibleTranscriber> logger,
        HttpMessageHandler? handler = null)
    {
        _provider = provider;
        _key = key;
        _endpoint = new Uri(endpoint);
        _model = model;
        _logger = logger;
        _http = handler is null ? new HttpClient() : new HttpClient(handler);
        _http.Timeout = TimeSpan.FromSeconds(30);
    }

    public string? Model => _model;

    public bool IsReady => _key() is { Length: > 0 };

    public async Task<Transcription> TranscribeAsync(
        Utterance utterance,
        IReadOnlyList<string> properNouns,
        CancellationToken cancellationToken = default)
    {
        if (_key() is not { Length: > 0 } key)
        {
            throw new TranscriptionUnavailableException(
                _provider,
                TranscriptionFailure.KeyRejected,
                $"No {_provider} API key is stored. Add one in Settings.");
        }

        var stopwatch = Stopwatch.StartNew();

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, _endpoint);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", key);

            using var form = new MultipartFormDataContent();

            var file = new ByteArrayContent(HostedTranscription.Wav(utterance));
            file.Headers.ContentType = new MediaTypeHeaderValue("audio/wav");
            form.Add(file, "file", "utterance.wav");
            form.Add(new StringContent(_model), "model");
            form.Add(new StringContent("en"), "language");
            form.Add(new StringContent("json"), "response_format");

            if (Prompt(properNouns) is { } prompt)
            {
                form.Add(new StringContent(prompt), "prompt");
            }

            request.Content = form;

            using var response = await _http.SendAsync(request, cancellationToken).ConfigureAwait(false);

            var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                throw Refused(response, body);
            }

            var text = Text(body) ?? throw new TranscriptionUnavailableException(
                _provider,
                TranscriptionFailure.Failed,
                $"{_provider} answered without a transcript.")
            {
                Detail = "no transcript came back",
            };

            _logger.LogInformation(
                "{Provider} transcribed {Seconds:0.#}s of audio in {Elapsed}ms with {Nouns} name hints",
                _provider,
                utterance.Duration.TotalSeconds,
                stopwatch.ElapsedMilliseconds,
                properNouns.Count);

            return new Transcription(text.Trim())
            {
                Elapsed = stopwatch.Elapsed,
                Model = _model,
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
            _logger.LogWarning("{Provider} could not be reached: {Because}", _provider, ex.Message);

            throw new TranscriptionUnavailableException(
                _provider,
                TranscriptionFailure.Unreachable,
                $"{_provider} could not be reached: {ex.Message}",
                ex);
        }
    }

    /// <summary>Classifies a non-success answer, taking the message from <c>error.message</c> only.</summary>
    private TranscriptionUnavailableException Refused(HttpResponseMessage response, string body)
    {
        var said = ErrorMessage(body) ?? response.ReasonPhrase ?? response.StatusCode.ToString();

        var reason = HostedTranscription.Reason(response.StatusCode);

        _logger.LogWarning(
            "{Provider} refused to transcribe ({Status}): {Because}", _provider, (int)response.StatusCode, said);

        return new TranscriptionUnavailableException(
            _provider, reason, $"{_provider} could not transcribe: {said}")
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
                   && parsed.RootElement.TryGetProperty("error", out var error)
                   && error.ValueKind == JsonValueKind.Object
                   && error.TryGetProperty("message", out var message)
                   && message.ValueKind == JsonValueKind.String
                ? message.GetString()
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

    /// <summary>
    /// The names joined with ", ", keeping whole names from the front until <see cref="PromptCharacters"/>,
    /// or null for none. Whisper drops the start of an overlong prompt, which is where the most relevant
    /// names are.
    /// </summary>
    internal static string? Prompt(IReadOnlyList<string> properNouns)
    {
        var prompt = new StringBuilder();

        foreach (var name in properNouns)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                continue;
            }

            var trimmed = name.Trim();
            var needed = prompt.Length == 0 ? trimmed.Length : prompt.Length + 2 + trimmed.Length;

            if (needed > PromptCharacters)
            {
                break;
            }

            if (prompt.Length > 0)
            {
                prompt.Append(", ");
            }

            prompt.Append(trimmed);
        }

        return prompt.Length == 0 ? null : prompt.ToString();
    }

    public void Dispose() => _http.Dispose();
}
