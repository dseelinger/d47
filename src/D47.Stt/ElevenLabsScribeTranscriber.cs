using System.Net.Http.Headers;
using System.Text.Json;
using D47.Core.Listening;
using Microsoft.Extensions.Logging;

namespace D47.Stt;

/// <summary>ElevenLabs Scribe's <c>POST /v1/speech-to-text</c>, one finished utterance per request.</summary>
public sealed class ElevenLabsScribeTranscriber : HostedSpeechTranscriber
{
    public const string Provider = "ElevenLabs";

    public const string Endpoint = "https://api.elevenlabs.io/v1/speech-to-text";

    public const string ScribeModel = "scribe_v2";

    /// <summary>At 100 keyterms Scribe bills every request as at least 20 seconds of audio.</summary>
    internal const int MostKeyterms = 99;

    internal const int KeytermCharacters = 50;

    internal const int KeytermWords = 5;

    private static readonly char[] Unsupported = ['<', '>', '{', '}', '[', ']', '\\'];

    /// <param name="key">Read at each call and never held.</param>
    public ElevenLabsScribeTranscriber(
        Func<string?> key,
        ILogger<ElevenLabsScribeTranscriber> logger,
        HttpMessageHandler? handler = null)
        : base(Provider, key, logger, handler)
    {
    }

    public override string? Model => ScribeModel;

    private protected override HttpRequestMessage Request(
        string key,
        Utterance utterance,
        IReadOnlyList<string> properNouns)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, Endpoint);
        request.Headers.Add("xi-api-key", key);

        var form = new MultipartFormDataContent();

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

        return request;
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

    /// <summary><c>detail.message</c>, or the first <c>detail[].msg</c> of a validation error.</summary>
    private protected override string? ErrorMessage(string body)
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
}
