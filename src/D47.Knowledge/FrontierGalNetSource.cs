using System.Globalization;
using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using D47.Core.Knowledge;
using Microsoft.Extensions.Logging;

namespace D47.Knowledge;

/// <summary><see cref="IGalNetSource"/> against Frontier's GalNet server.</summary>
public sealed partial class FrontierGalNetSource : IGalNetSource, IDisposable
{
    /// <summary>Named here and in <see cref="D47.Core.Configuration.EgressDisclosure"/>.</summary>
    public const string Host = "cms.zaonce.net";

    public const int PageSize = 50;

    /// <summary>The largest answer read.</summary>
    public const int MostBytes = 4 * 1024 * 1024;

    private readonly HttpClient _http;

    private readonly ILogger<FrontierGalNetSource> _logger;

    public FrontierGalNetSource(ILogger<FrontierGalNetSource> logger, HttpMessageHandler? handler = null)
    {
        _logger = logger;
        _http = new HttpClient(handler ?? InfomancerBgsTickSource.Handler());
        _http.Timeout = TimeSpan.FromSeconds(15);
        _http.MaxResponseContentBufferSize = MostBytes;
        _http.DefaultRequestHeaders.UserAgent.ParseAdd("d47");
    }

    public static string AddressOf(int offset) =>
        $"https://{Host}/en-GB/jsonapi/node/galnet_article?sort=-published_at"
        + $"&page[limit]={PageSize.ToString(CultureInfo.InvariantCulture)}"
        + $"&page[offset]={offset.ToString(CultureInfo.InvariantCulture)}";

    public async Task<GalNetPage?> PageAsync(int offset, CancellationToken cancellationToken)
    {
        try
        {
            using var response = await _http.GetAsync(AddressOf(offset), cancellationToken).ConfigureAwait(false);

            if (response.StatusCode != HttpStatusCode.OK)
            {
                _logger.LogInformation("GalNet fetch skipped: the host answered {Status}", (int)response.StatusCode);
                return null;
            }

            var body = await response.Content.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false);

            if (body.Length > MostBytes)
            {
                _logger.LogInformation("GalNet fetch ignored: the answer was too long");
                return null;
            }

            var page = Parse(System.Text.Encoding.UTF8.GetString(body));

            if (page is null)
            {
                _logger.LogInformation("GalNet fetch ignored: the answer was not a list of articles");
            }

            return page;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException
            && !cancellationToken.IsCancellationRequested)
        {
            _logger.LogInformation("GalNet fetch failed: {Reason}", ex.GetType().Name);
            return null;
        }
    }

    /// <summary>The articles in a response, or null when it is not JSON or has the wrong shape. An item without an id, title or time is left out.</summary>
    public static GalNetPage? Parse(string response)
    {
        try
        {
            using var document = JsonDocument.Parse(response);
            var root = document.RootElement;

            if (root.ValueKind != JsonValueKind.Object
                || !root.TryGetProperty("data", out var data)
                || data.ValueKind != JsonValueKind.Array)
            {
                return null;
            }

            var articles = new List<GalNetArticle>();

            foreach (var item in data.EnumerateArray())
            {
                if (Article(item) is { } article)
                {
                    articles.Add(article);
                }
            }

            var hasOlder = root.TryGetProperty("links", out var links)
                && links.ValueKind == JsonValueKind.Object
                && links.TryGetProperty("next", out var next)
                && next.ValueKind != JsonValueKind.Null;

            return new GalNetPage(articles, hasOlder);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static GalNetArticle? Article(JsonElement item)
    {
        if (item.ValueKind != JsonValueKind.Object
            || !item.TryGetProperty("attributes", out var attributes)
            || attributes.ValueKind != JsonValueKind.Object
            || Text(attributes, "field_galnet_guid") is not { Length: > 0 } id
            || Text(attributes, "title") is not { Length: > 0 } title
            || !DateTimeOffset.TryParse(
                Text(attributes, "published_at"),
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal,
                out var published))
        {
            return null;
        }

        var body = attributes.TryGetProperty("body", out var value) && value.ValueKind == JsonValueKind.Object
            ? Text(value, "value") ?? string.Empty
            : string.Empty;

        return new GalNetArticle(
            id,
            Clean(title),
            [.. Clean(body).Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)],
            published,
            Text(attributes, "field_galnet_date") ?? string.Empty);
    }

    private static string? Text(JsonElement owner, string name) =>
        owner.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;

    private static string Clean(string text) => WebUtility.HtmlDecode(Tag().Replace(text, string.Empty));

    [GeneratedRegex("<[^>]*>")]
    private static partial Regex Tag();

    public void Dispose() => _http.Dispose();
}
