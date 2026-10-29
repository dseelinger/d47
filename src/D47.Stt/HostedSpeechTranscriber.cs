using System.Diagnostics;
using D47.Core.Listening;
using Microsoft.Extensions.Logging;

namespace D47.Stt;

/// <summary>A transcriber that uploads one utterance to a hosted service and reads the reply.</summary>
public abstract class HostedSpeechTranscriber : ISpeechTranscriber
{
    private readonly string _provider;
    private readonly Func<string?> _key;
    private readonly ILogger _logger;
    private readonly HttpClient _http;

    /// <param name="key">Read at each call and never held.</param>
    private protected HostedSpeechTranscriber(
        string provider,
        Func<string?> key,
        ILogger logger,
        HttpMessageHandler? handler)
    {
        _provider = provider;
        _key = key;
        _logger = logger;
        _http = handler is null ? new HttpClient() : new HttpClient(handler);
        _http.Timeout = TimeSpan.FromSeconds(30);
    }

    public abstract string? Model { get; }

    public bool IsReady => _key() is { Length: > 0 };

    /// <summary>The request for one utterance; its content is disposed with it.</summary>
    private protected abstract HttpRequestMessage Request(
        string key,
        Utterance utterance,
        IReadOnlyList<string> properNouns);

    /// <summary>The message the service states in a non-success body, or null.</summary>
    private protected abstract string? ErrorMessage(string body);

    /// <summary>The transcript and its confidence in a success body, or null if absent.</summary>
    private protected virtual (string Text, double Confidence)? Reply(string body) =>
        HostedTranscription.Text(body) is { } text ? (text, 1) : null;

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
            using var request = Request(key, utterance, properNouns);

            using var response = await _http.SendAsync(request, cancellationToken).ConfigureAwait(false);

            var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                throw Refused(response, body);
            }

            var (text, confidence) = Reply(body) ?? throw new TranscriptionUnavailableException(
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
                Model = Model,
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
            _logger.LogWarning("{Provider} could not be reached: {Because}", _provider, ex.Message);

            throw new TranscriptionUnavailableException(
                _provider,
                TranscriptionFailure.Unreachable,
                $"{_provider} could not be reached: {ex.Message}",
                ex);
        }
    }

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

    public void Dispose() => _http.Dispose();
}
