using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using D47.Core.Viewing;
using Microsoft.Extensions.Logging;

namespace D47.Knowledge;

/// <summary>
/// <see cref="IVideoSource"/> against archive.org's advanced search, limited to items whose licence is public domain or
/// Creative Commons, with each result's file list read to find an MP4.
/// </summary>
public sealed partial class InternetArchiveVideoSource : IVideoSource, IDisposable
{
    public const string Host = VideoSources.ArchiveHost;

    /// <summary>The clause every query carries. The licence is checked again on each result.</summary>
    public const string LicenceClause = "(licenseurl:*publicdomain* OR licenseurl:*creativecommons*)";

    internal const long MostSearchBytes = 2_000_000;

    internal const long MostFileListBytes = 8_000_000;

    private const int FileListsAtOnce = 6;

    /// <summary>A length past ten days is not a video's.</summary>
    private const double MostSeconds = 864_000;

    private static readonly TimeSpan FileListBudget = TimeSpan.FromSeconds(15);

    private readonly HttpClient _http;
    private readonly ILogger<InternetArchiveVideoSource> _logger;
    private readonly bool _ownsClient;

    public InternetArchiveVideoSource(ILogger<InternetArchiveVideoSource> logger, HttpClient? http = null)
    {
        _logger = logger;
        _ownsClient = http is null;

        _http = KnowledgeHttp.Client(http, Host, TimeSpan.FromSeconds(30));
    }

    public string Id => VideoSources.InternetArchive;

    /// <summary>The advanced search query: the typed words, escaped and in their own parentheses, then the fixed clauses.</summary>
    public static string Query(string text, VideoKind? kind)
    {
        var parts = new List<string>();

        if (!string.IsNullOrWhiteSpace(text))
        {
            parts.Add($"({Escape(text.Trim())})");
        }

        parts.Add("mediatype:movies");

        if (KindClause(kind) is { } clause)
        {
            parts.Add(clause);
        }

        parts.Add(LicenceClause);

        return string.Join(" AND ", parts);
    }

    public async Task<VideoPage> SearchAsync(string query, VideoFilters filters, int page, int pageSize, CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(page, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(pageSize, 1);
        ArgumentNullException.ThrowIfNull(filters);

        var request = new StringBuilder("advancedsearch.php?q=")
            .Append(Uri.EscapeDataString(Query(query ?? string.Empty, filters.Kind)));

        foreach (var field in (string[])["identifier", "title", "licenseurl", "creator"])
        {
            request.Append("&fl%5B%5D=").Append(field);
        }

        request.Append(CultureInfo.InvariantCulture, $"&rows={pageSize}&page={page}&output=json");

        if (Sort(filters.Order) is { } sort)
        {
            request.Append("&sort%5B%5D=").Append(Uri.EscapeDataString(sort));
        }

        List<Found> found;
        int total;

        try
        {
            using var response = await _http
                .GetAsync(request.ToString(), HttpCompletionOption.ResponseHeadersRead, cancellationToken)
                .ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("The Internet Archive answered {Status} to a video search", (int)response.StatusCode);
                return Failed($"it answered {(int)response.StatusCode}");
            }

            using var document = await KnowledgeHttp.ReadJsonAtMostAsync(response, MostSearchBytes, cancellationToken).ConfigureAwait(false);

            if (document is null)
            {
                return Failed("its answer was larger than 2 MB");
            }

            if (ReadSearch(document) is not { } read)
            {
                return Failed("it answered with an error");
            }

            (found, total) = read;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex) when (ex is HttpRequestException or OperationCanceledException or JsonException or InvalidOperationException)
        {
            _logger.LogWarning(ex, "The Internet Archive's video search failed");
            return Failed("it could not be reached");
        }

        using var gate = new SemaphoreSlim(FileListsAtOnce);

        var titles = await Task.WhenAll(found.Select(item => ResolveAsync(item, filters.Kind, gate, cancellationToken))).ConfigureAwait(false);

        return new VideoPage(titles, total);
    }

    public void Dispose()
    {
        if (_ownsClient)
        {
            _http.Dispose();
        }
    }

    /// <summary>A search result whose identifier and licence passed, before its files are read.</summary>
    internal sealed record Found(string Identifier, string Title, string Licence, string? Creator);

    /// <summary>
    /// The results with a usable identifier and a free licence, and the total the search reports; null for an answer
    /// with no <c>response</c>, which is how archive.org reports a failed search.
    /// </summary>
    internal static (List<Found> Found, int Total)? ReadSearch(JsonDocument document)
    {
        if (document.RootElement.ValueKind != JsonValueKind.Object || !document.RootElement.TryGetProperty("response", out var response))
        {
            return null;
        }

        var total = response.TryGetProperty("numFound", out var count) && count.TryGetInt32(out var number) ? number : 0;
        var found = new List<Found>();

        if (response.TryGetProperty("docs", out var docs) && docs.ValueKind == JsonValueKind.Array)
        {
            foreach (var doc in docs.EnumerateArray())
            {
                if (First(doc, "identifier") is { } identifier
                    && IdentifierShape().IsMatch(identifier)
                    && VideoLicence.Label(First(doc, "licenseurl")) is { } licence)
                {
                    found.Add(new Found(identifier, First(doc, "title") ?? identifier, licence, First(doc, "creator")));
                }
            }
        }

        return (found, total);
    }

    /// <summary>The title an item's file list makes of a search result.</summary>
    internal static VideoTitle ReadFiles(Found found, VideoKind? asked, JsonDocument document)
    {
        var root = document.RootElement;
        var collections = root.TryGetProperty("metadata", out var metadata) ? All(metadata, "collection") : [];
        var kind = asked ?? KindOf(collections);
        var files = new List<(string Name, string Format, long? Size, TimeSpan? Length)>();

        if (root.TryGetProperty("files", out var listed) && listed.ValueKind == JsonValueKind.Array)
        {
            foreach (var file in listed.EnumerateArray())
            {
                if (First(file, "name") is { } name && FilePath(name) is not null)
                {
                    files.Add((
                        name,
                        First(file, "format") ?? string.Empty,
                        long.TryParse(First(file, "size"), NumberStyles.None, CultureInfo.InvariantCulture, out var size) ? size : null,
                        Length(First(file, "length"))));
                }
            }
        }

        var mp4 = files
            .Where(file => file.Name.EndsWith(".mp4", StringComparison.OrdinalIgnoreCase))
            .OrderBy(file => file.Format.StartsWith("h.264", StringComparison.OrdinalIgnoreCase) ? 0
                : file.Format.StartsWith("512Kb", StringComparison.OrdinalIgnoreCase) ? 2
                : 1)
            .ThenByDescending(file => file.Size ?? 0)
            .Select(file => ((string Name, string Format, long? Size, TimeSpan? Length)?)file)
            .FirstOrDefault();

        var subtitles = files
            .Where(file => file.Size is not 0
                           && (file.Name.EndsWith(".srt", StringComparison.OrdinalIgnoreCase) || file.Name.EndsWith(".vtt", StringComparison.OrdinalIgnoreCase)))
            .OrderBy(file => file.Name.EndsWith(".srt", StringComparison.OrdinalIgnoreCase) ? 0 : 1)
            .ThenBy(file => file.Name, StringComparer.Ordinal)
            .Select(file => Download(found.Identifier, file.Name))
            .FirstOrDefault();

        if (mp4 is not { } chosen)
        {
            var offered = files
                .Where(file => VideoExtensions.Contains(Path.GetExtension(file.Name), StringComparer.OrdinalIgnoreCase))
                .Select(file => file.Format.Length > 0 ? file.Format : Path.GetExtension(file.Name).TrimStart('.').ToUpperInvariant())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            return Title(found, kind, length: null, media: null, subtitles, offered.Count == 0
                ? "The Internet Archive lists no video file for it."
                : $"Offered as {KnowledgeHttp.Listed(offered)} only, with no MP4 D47 can play.");
        }

        return Title(found, kind, chosen.Length, Download(found.Identifier, chosen.Name), subtitles, unplayable: null);
    }

    /// <summary><c>https://archive.org/download/&lt;identifier&gt;/&lt;file&gt;</c>, each part escaped; null for a file path with an empty, <c>.</c> or <c>..</c> segment.</summary>
    internal static Uri? Download(string identifier, string name) =>
        IdentifierShape().IsMatch(identifier) && FilePath(name) is { } path
            ? new Uri($"https://{Host}/download/{Uri.EscapeDataString(identifier)}/{path}")
            : null;

    private static readonly string[] VideoExtensions = [".avi", ".ogv", ".mkv", ".mov", ".mpeg", ".mpg", ".m2v", ".webm", ".wmv", ".flv", ".m4v", ".3gp"];

    private async Task<VideoTitle> ResolveAsync(Found found, VideoKind? asked, SemaphoreSlim gate, CancellationToken cancellationToken)
    {
        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            using var budget = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            budget.CancelAfter(FileListBudget);

            using var response = await _http
                .GetAsync($"metadata/{Uri.EscapeDataString(found.Identifier)}", HttpCompletionOption.ResponseHeadersRead, budget.Token)
                .ConfigureAwait(false);

            if (response.IsSuccessStatusCode
                && await KnowledgeHttp.ReadJsonAtMostAsync(response, MostFileListBytes, budget.Token).ConfigureAwait(false) is { } document)
            {
                using (document)
                {
                    return ReadFiles(found, asked, document);
                }
            }

            _logger.LogWarning("The Internet Archive's file list for an item was not read ({Status})", (int)response.StatusCode);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex) when (ex is HttpRequestException or OperationCanceledException or JsonException or InvalidOperationException)
        {
            _logger.LogWarning(ex, "The Internet Archive's file list for an item could not be read");
        }
        finally
        {
            gate.Release();
        }

        return Title(found, asked ?? VideoKind.Other, length: null, media: null, subtitles: null, "Its list of files could not be read from the Internet Archive.");
    }

    private static VideoTitle Title(Found found, VideoKind kind, TimeSpan? length, Uri? media, Uri? subtitles, string? unplayable) =>
        new(VideoSources.InternetArchive, found.Identifier, found.Title, kind, found.Licence, length, media, subtitles, found.Creator, unplayable);

    private static VideoPage Failed(string why) =>
        VideoPage.Empty with { Problem = $"The Internet Archive could not be searched: {why}." };

    private static string? KindClause(VideoKind? kind) => kind switch
    {
        VideoKind.FeatureFilm => "collection:feature_films",
        VideoKind.ShortFilm => "collection:short_films",
        VideoKind.Footage => "collection:prelinger",
        VideoKind.Other => "NOT (collection:feature_films OR collection:short_films OR collection:prelinger)",
        _ => null,
    };

    private static VideoKind KindOf(IReadOnlyList<string> collections) =>
        collections.Contains("feature_films", StringComparer.OrdinalIgnoreCase) ? VideoKind.FeatureFilm
        : collections.Contains("short_films", StringComparer.OrdinalIgnoreCase) ? VideoKind.ShortFilm
        : collections.Contains("prelinger", StringComparer.OrdinalIgnoreCase) ? VideoKind.Footage
        : VideoKind.Other;

    private static string? Sort(VideoOrder order) => order switch
    {
        VideoOrder.MostWatched => "downloads desc",
        VideoOrder.Newest => "date desc",
        VideoOrder.Title => "titleSorter asc",
        _ => null,
    };

    /// <summary>
    /// Every Lucene operator character backslash-escaped, so typed words cannot reach past their parentheses. The
    /// range characters, which the search service cannot escape, become spaces.
    /// </summary>
    private static string Escape(string text)
    {
        var escaped = new StringBuilder(text.Length);

        foreach (var c in text)
        {
            if (c is '+' or '-' or '&' or '|' or '!' or '(' or ')' or '{' or '}' or '[' or ']' or '^' or '"' or '~' or '*' or '?' or ':' or '\\' or '/')
            {
                escaped.Append('\\');
            }

            escaped.Append(char.IsControl(c) || c is '<' or '>' or '=' ? ' ' : c);
        }

        return escaped.ToString();
    }

    /// <summary>The file path with each segment escaped, or null when a segment is empty, <c>.</c> or <c>..</c>.</summary>
    private static string? FilePath(string name)
    {
        var segments = name.Split('/');

        return segments.Any(segment => segment is "" or "." or ".." || segment.Contains('\\'))
            ? null
            : string.Join('/', segments.Select(Uri.EscapeDataString));
    }

    /// <summary>Seconds as a number, or <c>h:mm:ss</c> or <c>m:ss</c>.</summary>
    private static TimeSpan? Length(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        if (double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var seconds) && seconds is > 0 and < MostSeconds)
        {
            return TimeSpan.FromSeconds(seconds);
        }

        var parts = text.Split(':');

        if (parts.Length is not (2 or 3))
        {
            return null;
        }

        var total = 0.0;

        foreach (var part in parts)
        {
            if (!int.TryParse(part, NumberStyles.None, CultureInfo.InvariantCulture, out var number))
            {
                return null;
            }

            total = (total * 60) + number;
        }

        return total is > 0 and < MostSeconds ? TimeSpan.FromSeconds(total) : null;
    }

    private static string? First(JsonElement element, string name) => All(element, name).FirstOrDefault();

    /// <summary>A field that archive.org gives as a string or as an array of strings.</summary>
    private static IReadOnlyList<string> All(JsonElement element, string name)
    {
        if (element.ValueKind != JsonValueKind.Object || !element.TryGetProperty(name, out var value))
        {
            return [];
        }

        return value.ValueKind switch
        {
            JsonValueKind.String when value.GetString() is { Length: > 0 } text => [text],
            JsonValueKind.Number => [value.GetRawText()],
            JsonValueKind.Array => [.. value.EnumerateArray().Where(item => item.ValueKind == JsonValueKind.String).Select(item => item.GetString()!).Where(text => text.Length > 0)],
            _ => [],
        };
    }

    [GeneratedRegex("^[A-Za-z0-9][A-Za-z0-9._-]*$")]
    private static partial Regex IdentifierShape();
}
