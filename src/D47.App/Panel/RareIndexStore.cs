using System.Net;
using System.Net.Http;
using System.Text;
using D47.Core.Knowledge;
using D47.Core.Storage;
using Microsoft.Extensions.Logging;

namespace D47.App.Panel;

/// <summary>
/// Downloads the rare index from the <c>rares-1</c> release into <c>data\rares\</c> when a rare is asked about and the
/// copy on disk is missing or older than <see cref="MaxAge"/>, and keeps the parsed index until the next download. A
/// download replaces the copy only once it has parsed. Every fetch and parse runs on the pool.
/// </summary>
public sealed class RareIndexStore : IRareIndexSource
{
    /// <summary>Where the index is published.</summary>
    public const string Source = "https://github.com/dseelinger/d47/releases/download/rares-1/rares.tsv";

    public const string FileName = "rares.tsv";

    /// <summary>The largest download accepted.</summary>
    public const long MaxDownloadBytes = 1024 * 1024;

    /// <summary>How old the copy on disk, by its write time, may be before a question fetches a new one.</summary>
    public static readonly TimeSpan MaxAge = TimeSpan.FromHours(3);

    /// <summary>How long after one download attempt the next may be made.</summary>
    public static readonly TimeSpan RetryAfter = TimeSpan.FromHours(1);

    private static readonly HttpClient Http = CreateClient();

    private readonly string _path;
    private readonly IFileSystem _files;
    private readonly Func<DateTimeOffset> _now;
    private readonly Func<CancellationToken, Task<byte[]?>> _fetch;
    private readonly ILogger _logger;
    private readonly SemaphoreSlim _gate = new(1, 1);

    private RareIndex? _parsed;
    private FileState? _parsedFrom;
    private DateTimeOffset? _lastAttempt;

    /// <param name="folder"><c>AppPaths.Rares</c>.</param>
    /// <param name="fetch">The download, returning the file's bytes or null; the release by default.</param>
    public RareIndexStore(
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

    public async Task<RareIndex?> GetAsync(CancellationToken cancellationToken)
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

    private async Task<RareIndex?> LoadAsync(CancellationToken cancellationToken)
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

    /// <summary>The index for the copy on disk, parsed once per copy; null when it cannot be read.</summary>
    private RareIndex? Parsed(FileState copy)
    {
        if (_parsed is not null && _parsedFrom == copy)
        {
            return _parsed;
        }

        try
        {
            var bytes = _files.ReadBytes(_path);

            if (bytes is null || RareIndex.Parse(Encoding.UTF8.GetString(bytes)) is not { } index)
            {
                return null;
            }

            _parsed = index;
            _parsedFrom = copy;

            return index;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _logger.LogInformation("The rare index on disk could not be read ({Error}).", ex.GetType().Name);
            return null;
        }
    }

    private async Task<RareIndex?> DownloadAsync(CancellationToken cancellationToken)
    {
        var bytes = await _fetch(cancellationToken).ConfigureAwait(false);

        if (bytes is null)
        {
            return null;
        }

        if (RareIndex.Parse(Encoding.UTF8.GetString(bytes)) is not { } index)
        {
            _logger.LogInformation("The downloaded rare index was refused: the header is missing; the copy on disk stays.");
            return null;
        }

        try
        {
            _files.WriteBytes(_path, bytes);
            _parsedFrom = _files.Stat(_path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _logger.LogInformation("The rare index could not be stored ({Error}).", ex.GetType().Name);
            _parsedFrom = null;
        }

        _parsed = index;

        _logger.LogInformation("Rare index downloaded: {Rows} rares.", index.Rows.Count);

        return index;
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
                        ? "No rare index is published yet ({Status})."
                        : "The rare index release returned {Status}.",
                    response.StatusCode);

                return null;
            }

            if (response.Content.Headers.ContentLength > MaxDownloadBytes)
            {
                _logger.LogInformation("The rare index is larger than {Limit} bytes and was not downloaded.", MaxDownloadBytes);
                return null;
            }

            await using var body = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
            using var buffer = new MemoryStream();
            var chunk = new byte[16_384];

            while (await body.ReadAsync(chunk, cancellationToken).ConfigureAwait(false) is var read and > 0)
            {
                if (buffer.Length + read > MaxDownloadBytes)
                {
                    _logger.LogInformation("The rare index is larger than {Limit} bytes and was not downloaded.", MaxDownloadBytes);
                    return null;
                }

                buffer.Write(chunk, 0, read);
            }

            return buffer.ToArray();
        }
        catch (Exception ex) when (ex is HttpRequestException or IOException
            || (ex is TaskCanceledException && !cancellationToken.IsCancellationRequested))
        {
            _logger.LogInformation("The rare index could not be fetched ({Error}).", ex.GetType().Name);
            return null;
        }
    }

    private static HttpClient CreateClient()
    {
        var client = new HttpClient { Timeout = TimeSpan.FromSeconds(60) };

        client.DefaultRequestHeaders.UserAgent.ParseAdd("d47-rare-index");

        return client;
    }
}
