using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using D47.Core.Listening;
using Microsoft.Extensions.Logging;

namespace D47.Stt;

/// <summary>A service that serves OpenAI's <c>POST /v1/audio/transcriptions</c>: Groq and OpenAI.</summary>
public sealed class OpenAiCompatibleTranscriber : HostedSpeechTranscriber
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

    private readonly Uri _endpoint;
    private readonly string _model;

    /// <param name="key">Read at each call and never held.</param>
    public OpenAiCompatibleTranscriber(
        string provider,
        Func<string?> key,
        string endpoint,
        string model,
        ILogger<OpenAiCompatibleTranscriber> logger,
        HttpMessageHandler? handler = null)
        : base(provider, key, logger, handler)
    {
        _endpoint = new Uri(endpoint);
        _model = model;
    }

    public override string? Model => _model;

    private protected override HttpRequestMessage Request(
        string key,
        Utterance utterance,
        IReadOnlyList<string> properNouns)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, _endpoint);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", key);

        var form = new MultipartFormDataContent();

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

        return request;
    }

    private protected override string? ErrorMessage(string body)
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
}
