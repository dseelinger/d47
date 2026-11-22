using System.IO.Compression;
using System.Net;
using System.Net.Http;
using D47.Core.Books;
using D47.Core.Storage;
using Microsoft.Extensions.Logging;

namespace D47.App.Panel;

/// <summary>
/// Downloads the weekly Gutenberg catalogue from the <c>books-1</c> release into <c>data\books\gutenberg\</c> when it
/// is asked for and the copy on disk is missing or older than <see cref="MaxAge"/>, and keeps the parsed catalogue
/// until the next download. A download replaces the copy only once it has parsed. Every fetch and parse runs on the pool.
/// </summary>
public sealed class GutenbergCatalogueStore
{
    /// <summary>Where the catalogue is published.</summary>
    public const string Source = "https://github.com/dseelinger/d47/releases/download/books-1/gutenberg.tsv.gz";

    public const string FileName = "catalogue.tsv.gz";

    /// <summary>The largest download accepted, compressed.</summary>
    public const long MaxDownloadBytes = 64L * 1024 * 1024;

    /// <summary>How old the copy on disk may be before a request fetches a new one.</summary>
    public static readonly TimeSpan MaxAge = TimeSpan.FromDays(7);

    /// <summary>How long after one download attempt the next may be made.</summary>
    public static readonly TimeSpan RetryAfter = TimeSpan.FromHours(1);

    private static readonly HttpClient Http = CreateClient();

    private readonly string _path;
    private readonly IFileSystem _files;
    private readonly Func<DateTimeOffset> _now;
    private readonly Func<CancellationToken, Task<byte[]?>> _fetch;
    private readonly ILogger _logger;
    private readonly SemaphoreSlim _gate = new(1, 1);

    private GutenbergCatalogue? _parsed;
    private FileState? _parsedFrom;
    private DateTimeOffset? _lastAttempt;

    /// <param name="folder"><c>AppPaths.Gutenberg</c>.</param>
    /// <param name="fetch">The download, returning the file's bytes or null; the release by default.</param>
    public GutenbergCatalogueStore(
        string folder,
        IFileSystem files,
        Func<DateTimeOffset> now,
        ILogger logger,
        Func<CancellationToken, Task<byte[]?>>? fetch = null)
    {
        _path = Path.Combine(folder, FileName);
        _files = files;
        _now = now;
        _logger = logger;
        _fetch = fetch ?? FetchAsync;
    }

    /// <summary>The catalogue, fetched first when the copy is missing or stale; null when there is none.</summary>
    public async Task<GutenbergCatalogue?> GetAsync(CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            return await Task.Run(() => LoadAsync(cancellationToken), cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task<GutenbergCatalogue?> LoadAsync(CancellationToken cancellationToken)
    {
        var now = _now();
        var held = _files.Stat(_path);

        if (held is { } copy && now.UtcDateTime - copy.Written <= MaxAge && Parsed(copy) is { } current)
        {
            return current;
        }

        if (_lastAttempt is not { } last || now - last >= RetryAfter)
        {
            var fresh = await DownloadAsync(cancellationToken).ConfigureAwait(false);

            _lastAttempt = now;

            if (fresh is not null)
            {
                return fresh;
            }
        }

        return _files.Stat(_path) is { } old ? Parsed(old) : null;
    }

    /// <summary>The catalogue for the copy on disk, parsed once per copy; null when it cannot be read.</summary>
    private GutenbergCatalogue? Parsed(FileState copy)
    {
        if (_parsed is not null && _parsedFrom == copy)
        {
            return _parsed;
        }

        try
        {
            using var stream = _files.OpenRead(_path);

            if (stream is null)
            {
                return null;
            }

            _parsed = Parse(stream);
            _parsedFrom = copy;

            return _parsed;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException)
        {
            _logger.LogInformation("The Gutenberg catalogue on disk could not be read ({Error}).", ex.GetType().Name);
            return null;
        }
    }

    private async Task<GutenbergCatalogue?> DownloadAsync(CancellationToken cancellationToken)
    {
        var bytes = await _fetch(cancellationToken).ConfigureAwait(false);

        if (bytes is null)
        {
            return null;
        }

        GutenbergCatalogue catalogue;

        try
        {
            catalogue = Parse(new MemoryStream(bytes, writable: false));
        }
        catch (InvalidDataException ex)
        {
            _logger.LogInformation("The downloaded Gutenberg catalogue was refused ({Reason}); the copy on disk stays.", ex.Message);
            return null;
        }

        try
        {
            _files.WriteBytes(_path, bytes);
            _parsedFrom = _files.Stat(_path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _logger.LogInformation("The Gutenberg catalogue could not be stored ({Error}).", ex.GetType().Name);
            _parsedFrom = null;
        }

        _parsed = catalogue;

        _logger.LogInformation(
            "Gutenberg catalogue downloaded: {Rows} books, {Malformed} malformed rows skipped.",
            catalogue.Count,
            catalogue.Malformed);

        return catalogue;
    }

    private static GutenbergCatalogue Parse(Stream compressed)
    {
        using var gzip = new GZipStream(compressed, CompressionMode.Decompress);
        using var reader = new StreamReader(gzip);

        return GutenbergCatalogue.Read(reader);
    }

    private async Task<byte[]?> FetchAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var response = await Http.GetAsync(Source, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
                .ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogInformation(
                    response.StatusCode == HttpStatusCode.NotFound
                        ? "No Gutenberg catalogue is published yet ({Status})."
                        : "The Gutenberg catalogue release returned {Status}.",
                    response.StatusCode);

                return null;
            }

            if (response.Content.Headers.ContentLength > MaxDownloadBytes)
            {
                _logger.LogInformation("The Gutenberg catalogue is larger than {Limit} bytes and was not downloaded.", MaxDownloadBytes);
                return null;
            }

            await using var body = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
            using var buffer = new MemoryStream();
            var chunk = new byte[81_920];

            while (await body.ReadAsync(chunk, cancellationToken).ConfigureAwait(false) is var read and > 0)
            {
                if (buffer.Length + read > MaxDownloadBytes)
                {
                    _logger.LogInformation("The Gutenberg catalogue is larger than {Limit} bytes and was not downloaded.", MaxDownloadBytes);
                    return null;
                }

                buffer.Write(chunk, 0, read);
            }

            return buffer.ToArray();
        }
        catch (Exception ex) when (ex is HttpRequestException or IOException
            || (ex is TaskCanceledException && !cancellationToken.IsCancellationRequested))
        {
            _logger.LogInformation("The Gutenberg catalogue could not be fetched ({Error}).", ex.GetType().Name);
            return null;
        }
    }

    private static HttpClient CreateClient()
    {
        var client = new HttpClient { Timeout = TimeSpan.FromSeconds(120) };

        client.DefaultRequestHeaders.UserAgent.ParseAdd("d47-books");

        return client;
    }
}
