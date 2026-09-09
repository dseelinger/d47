using System.Net;
using System.Net.Http.Headers;

namespace D47.Llm.OpenAi;

/// <summary>
/// The address half of an OpenAI-shaped provider, and the one HTTP client both decoders share (Phase
/// 29).
/// </summary>
internal sealed class OpenAiEndpoint : IDisposable
{
    /// <summary>
    /// A turn can be long and the first token can be slow on a machine loading weights from disk.
    /// </summary>
    private static readonly TimeSpan Ceiling = TimeSpan.FromMinutes(10);

    private readonly HttpClient _http;
    private readonly bool _ownsClient;

    internal OpenAiEndpoint(string baseUrl, string? apiKey, HttpClient? http = null)
    {
        BaseUrl = Normalise(baseUrl);

        _ownsClient = http is null;
        _http = http ?? new HttpClient { Timeout = Ceiling };

        if (!string.IsNullOrWhiteSpace(apiKey))
        {
            // Trimmed, because a key pasted out of a browser arrives with the newline the selection picked
            // up, and a header value containing one throws rather than 401s.
            _http.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", apiKey.Trim());
        }
    }

    /// <summary>The base the request paths hang off, with no trailing slash.</summary>
    public string BaseUrl { get; }

    public bool IsLoopback => D47.Core.Conversation.LocalEndpoint.IsLoopback(BaseUrl);

    /// <summary>The address the Commander typed, made into something requests can be built on.</summary>
    internal static string Normalise(string baseUrl)
    {
        var trimmed = (baseUrl ?? string.Empty).Trim().TrimEnd('/');

        if (!Uri.TryCreate(trimmed, UriKind.Absolute, out var uri))
        {
            return trimmed;
        }

        return uri.AbsolutePath is "" or "/" ? $"{trimmed}/v1" : trimmed;
    }

    public HttpRequestMessage Post(string path, ReadOnlyMemory<byte> body)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, $"{BaseUrl}{path}")
        {
            Content = new ReadOnlyMemoryContent(body),
        };

        request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
        return request;
    }

    public Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
        _http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);

    public Task<HttpResponseMessage> GetAsync(string path, CancellationToken cancellationToken) =>
        _http.GetAsync($"{BaseUrl}{path}", HttpCompletionOption.ResponseHeadersRead, cancellationToken);

    public void Dispose()
    {
        if (_ownsClient)
        {
            _http.Dispose();
        }
    }

    /// <summary>What went wrong, in the Commander's terms, and whether trying again could help.</summary>
    public (string Message, bool Transient) Describe(HttpStatusCode status, string? detail)
    {
        var said = string.IsNullOrWhiteSpace(detail) ? string.Empty : $" — {detail.Trim()}";

        return status switch
        {
            HttpStatusCode.TooManyRequests => ($"Rate limited by {Host}; it should clear shortly.", true),
            HttpStatusCode.RequestTimeout => ($"{Host} timed out; it should clear shortly.", true),
            HttpStatusCode.Unauthorized => ($"{Host} rejected the API key. Check it in settings.{said}", false),
            HttpStatusCode.Forbidden => ($"This key is not permitted to use that model at {Host}.{said}", false),

            // A 404 from an OpenAI-shaped server is nearly always the address rather than the model, and it
            // is the one failure a Commander can fix from the message alone — so the message says which
            // address was actually built.
            HttpStatusCode.NotFound =>
                ($"{Host} has nothing at {BaseUrl}. Check the endpoint — it should include the version "
                 + $"segment, as in http://127.0.0.1:11434/v1.{said}", false),

            >= HttpStatusCode.InternalServerError => ($"{Host} reported a server error; it should clear shortly.", true),
            _ => ($"{Host} refused the request{(said.Length > 0 ? said : $" with status {(int)status}.")}", false),
        };
    }

    /// <summary>A transport failure — nothing answered.</summary>
    public string DescribeUnreachable(Exception ex) =>
        ex is TaskCanceledException or TimeoutException
            ? $"{Host} did not answer in time."
            : $"Could not reach {Host} at {BaseUrl} — check the address and that the server is running.";

    /// <summary>The endpoint named the way a Commander would recognise it.</summary>
    private string Host =>
        Uri.TryCreate(BaseUrl, UriKind.Absolute, out var uri) ? uri.Host : BaseUrl;
}
