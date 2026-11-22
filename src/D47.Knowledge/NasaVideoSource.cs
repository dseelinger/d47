using System.Globalization;
using System.Text.Json;
using D47.Core.Viewing;
using Microsoft.Extensions.Logging;

namespace D47.Knowledge;

/// <summary>
/// <see cref="IVideoSource"/> against NASA's Image and Video Library, videos only, with each result's file list read
/// from images-assets.nasa.gov to find an MP4. Every URL taken from an answer must be on images-assets.nasa.gov.
/// </summary>
public sealed class NasaVideoSource : IVideoSource, IDisposable
{
    public const string Host = VideoSources.NasaSearchHost;

    internal const long MostSearchBytes = 4_000_000;

    internal const long MostFileListBytes = 1_000_000;

    private const int FileListsAtOnce = 6;

    private static readonly TimeSpan FileListBudget = TimeSpan.FromSeconds(15);

    /// <summary>The renditions NASA lists, best first: large and medium stream more lightly than the original.</summary>
    private static readonly string[] Renditions = ["~large.mp4", "~medium.mp4", "~orig.mp4", "~small.mp4", "~mobile.mp4"];

    private readonly HttpClient _http;
    private readonly ILogger<NasaVideoSource> _logger;
    private readonly bool _ownsClient;

    public NasaVideoSource(ILogger<NasaVideoSource> logger, HttpClient? http = null)
    {
        _logger = logger;
        _ownsClient = http is null;

        _http = KnowledgeHttp.Client(http, Host, TimeSpan.FromSeconds(30));
    }

    public string Id => VideoSources.Nasa;

    public async Task<VideoPage> SearchAsync(string query, VideoFilters filters, int page, int pageSize, CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(page, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(pageSize, 1);
        ArgumentNullException.ThrowIfNull(filters);

        if (filters.Kind is { } kind && kind != VideoKind.Footage)
        {
            return VideoPage.Empty;
        }

        var request = string.IsNullOrWhiteSpace(query)
            ? "search?media_type=video"
            : $"search?q={Uri.EscapeDataString(query.Trim())}&media_type=video";
        request += string.Create(CultureInfo.InvariantCulture, $"&page={page}&page_size={pageSize}");

        List<Found> found;
        int total;

        try
        {
            using var response = await _http.GetAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("NASA's library answered {Status} to a video search", (int)response.StatusCode);
                return Failed($"it answered {(int)response.StatusCode}");
            }

            using var document = await KnowledgeHttp.ReadJsonAtMostAsync(response, MostSearchBytes, cancellationToken).ConfigureAwait(false);

            if (document is null)
            {
                return Failed("its answer was larger than 4 MB");
            }

            if (ReadSearch(document) is not { } read)
            {
                return Failed("its answer was not a list of results");
            }

            (found, total) = read;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex) when (ex is HttpRequestException or OperationCanceledException or JsonException or InvalidOperationException)
        {
            _logger.LogWarning(ex, "NASA's video search failed");
            return Failed("it could not be reached");
        }

        using var gate = new SemaphoreSlim(FileListsAtOnce);

        var titles = await Task.WhenAll(found.Select(item => ResolveAsync(item, gate, cancellationToken))).ConfigureAwait(false);

        return new VideoPage(titles, total);
    }

    public void Dispose()
    {
        if (_ownsClient)
        {
            _http.Dispose();
        }
    }

    /// <summary>A search result, before its file list is read. <paramref name="Files"/> is null when the answer named no list on images-assets.nasa.gov.</summary>
    internal sealed record Found(string NasaId, string Title, string? Center, Uri? Files, Uri? Captions);

    /// <summary>The results and the total the search reports; null for an answer with no <c>collection</c>.</summary>
    internal static (List<Found> Found, int Total)? ReadSearch(JsonDocument document)
    {
        if (document.RootElement.ValueKind != JsonValueKind.Object || !document.RootElement.TryGetProperty("collection", out var collection))
        {
            return null;
        }

        var total = collection.TryGetProperty("metadata", out var metadata)
                    && metadata.TryGetProperty("total_hits", out var hits)
                    && hits.TryGetInt32(out var number)
            ? number
            : 0;
        var found = new List<Found>();

        if (collection.TryGetProperty("items", out var items) && items.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in items.EnumerateArray())
            {
                if (!item.TryGetProperty("data", out var data) || data.ValueKind != JsonValueKind.Array || data.GetArrayLength() == 0)
                {
                    continue;
                }

                var first = data[0];

                if (Text(first, "nasa_id") is not { } nasaId || Text(first, "media_type") is { } type && type != "video")
                {
                    continue;
                }

                Uri? captions = null;

                if (item.TryGetProperty("links", out var links) && links.ValueKind == JsonValueKind.Array)
                {
                    captions = links.EnumerateArray()
                        .Where(link => Text(link, "rel") == "captions")
                        .Select(link => Asset(Text(link, "href")))
                        .FirstOrDefault(uri => uri is not null);
                }

                found.Add(new Found(nasaId, Text(first, "title") ?? nasaId, Text(first, "center"), Asset(Text(item, "href")), captions));
            }
        }

        return (found, total);
    }

    /// <summary>The title a result's file list makes of it.</summary>
    internal static VideoTitle ReadFiles(Found found, JsonDocument document)
    {
        var files = document.RootElement.ValueKind == JsonValueKind.Array
            ? document.RootElement.EnumerateArray()
                .Where(entry => entry.ValueKind == JsonValueKind.String)
                .Select(entry => entry.GetString()!)
                .ToList()
            : [];

        var media = Renditions
            .Select(ending => files.FirstOrDefault(file => file.EndsWith(ending, StringComparison.OrdinalIgnoreCase)))
            .Concat(files.Where(file => file.EndsWith(".mp4", StringComparison.OrdinalIgnoreCase)
                                        && !file.EndsWith("~preview.mp4", StringComparison.OrdinalIgnoreCase)))
            .Select(Asset)
            .FirstOrDefault(uri => uri is not null);

        var subtitles = found.Captions
                        ?? files.Where(file => file.EndsWith(".srt", StringComparison.OrdinalIgnoreCase) || file.EndsWith(".vtt", StringComparison.OrdinalIgnoreCase))
                            .Select(Asset)
                            .FirstOrDefault(uri => uri is not null);

        if (media is null)
        {
            var offered = files
                .Select(file => Path.GetExtension(file).TrimStart('.').ToUpperInvariant())
                .Where(extension => extension is "MOV" or "AVI" or "MKV" or "WEBM" or "WMV" or "MPG" or "MPEG" or "M4V")
                .Distinct(StringComparer.Ordinal)
                .ToList();

            return Title(found, media: null, subtitles, offered.Count == 0
                ? "NASA lists no video file D47 can reach for it."
                : $"Offered as {KnowledgeHttp.Listed(offered)} only, with no MP4 D47 can play.");
        }

        return Title(found, media, subtitles, unplayable: null);
    }

    /// <summary>
    /// An http or https URL on images-assets.nasa.gov, as https; null for any other host, port or scheme, or a URL
    /// carrying a user name.
    /// </summary>
    internal static Uri? Asset(string? text)
    {
        if (!Uri.TryCreate(text?.Trim(), UriKind.Absolute, out var uri)
            || uri.Scheme is not ("http" or "https")
            || !uri.IsDefaultPort
            || uri.UserInfo.Length > 0
            || !string.Equals(uri.Host, VideoSources.NasaAssetsHost, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return new UriBuilder(uri) { Scheme = Uri.UriSchemeHttps, Port = -1 }.Uri;
    }

    private async Task<VideoTitle> ResolveAsync(Found found, SemaphoreSlim gate, CancellationToken cancellationToken)
    {
        if (found.Files is null)
        {
            return Title(found, media: null, found.Captions, "NASA's list of its files is not on images-assets.nasa.gov.");
        }

        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            using var budget = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            budget.CancelAfter(FileListBudget);

            using var response = await _http.GetAsync(found.Files, HttpCompletionOption.ResponseHeadersRead, budget.Token).ConfigureAwait(false);

            if (response.IsSuccessStatusCode
                && await KnowledgeHttp.ReadJsonAtMostAsync(response, MostFileListBytes, budget.Token).ConfigureAwait(false) is { } document)
            {
                using (document)
                {
                    return ReadFiles(found, document);
                }
            }

            _logger.LogWarning("NASA's file list for a video was not read ({Status})", (int)response.StatusCode);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex) when (ex is HttpRequestException or OperationCanceledException or JsonException)
        {
            _logger.LogWarning(ex, "NASA's file list for a video could not be read");
        }
        finally
        {
            gate.Release();
        }

        return Title(found, media: null, found.Captions, "Its list of files could not be read from NASA's library.");
    }

    private static VideoTitle Title(Found found, Uri? media, Uri? subtitles, string? unplayable) =>
        new(
            VideoSources.Nasa,
            found.NasaId,
            found.Title,
            VideoKind.Footage,
            VideoLicence.UsGovernmentWork,
            Length: null,
            media,
            subtitles,
            string.IsNullOrWhiteSpace(found.Center) ? "NASA" : $"NASA {found.Center}",
            unplayable);

    private static VideoPage Failed(string why) =>
        VideoPage.Empty with { Problem = $"NASA's library could not be searched: {why}." };

    private static string? Text(JsonElement element, string name) =>
        element.ValueKind == JsonValueKind.Object
        && element.TryGetProperty(name, out var value)
        && value.ValueKind == JsonValueKind.String
        && value.GetString() is { Length: > 0 } text
            ? text
            : null;
}
