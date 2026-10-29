using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using D47.Core.Listening;
using Microsoft.Extensions.Logging;

namespace D47.Stt;

/// <summary>Deepgram's <c>POST /v1/listen</c>, one finished utterance per request.</summary>
public sealed class DeepgramTranscriber : HostedSpeechTranscriber
{
    public const string Provider = "Deepgram";

    public const string Endpoint = "https://api.deepgram.com/v1/listen";

    public const string DeepgramModel = "nova-3";

    /// <summary>The longest request URL sent; <c>keyterm</c> parameters stop before it.</summary>
    internal const int MostUrlCharacters = 1999;

    /// <param name="key">Read at each call and never held.</param>
    public DeepgramTranscriber(
        Func<string?> key,
        ILogger<DeepgramTranscriber> logger,
        HttpMessageHandler? handler = null)
        : base(Provider, key, logger, handler)
    {
    }

    public override string? Model => DeepgramModel;

    private protected override HttpRequestMessage Request(
        string key,
        Utterance utterance,
        IReadOnlyList<string> properNouns)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, Address(properNouns));
        request.Headers.Authorization = new AuthenticationHeaderValue("Token", key);

        var audio = new ByteArrayContent(HostedTranscription.Wav(utterance));
        audio.Headers.ContentType = new MediaTypeHeaderValue("audio/wav");
        request.Content = audio;

        return request;
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

    private protected override string? ErrorMessage(string body)
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
    private protected override (string Text, double Confidence)? Reply(string body)
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
}
