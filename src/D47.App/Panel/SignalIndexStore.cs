using System.IO.Compression;
using System.Net;
using System.Net.Http;
using D47.Core.Knowledge;
using D47.Core.Storage;
using Microsoft.Extensions.Logging;

namespace D47.App.Panel;

/// <summary>
/// Downloads the daily signal index from the <c>signals-1</c> release into <c>data\signals\</c> when a signal search
/// asks for it and the copy on disk is missing or older than <see cref="MaxAge"/>, and keeps the parsed index until the
/// next download. A download replaces the copy only once it has parsed. Every fetch and parse runs on the pool.
/// </summary>
public sealed class SignalIndexStore : ISignalIndexSource
{
    /// <summary>Where the index is published.</summary>
    public const string Source = "https://github.com/dseelinger/d47/releases/download/signals-1/signals.tsv.gz";

    public const string FileName = "signals.tsv.gz";

    /// <summary>The largest download accepted, compressed.</summary>
    public const long MaxDownloadBytes = 256L * 1024 * 1024;

    /// <summary>How old the copy on disk may be before a search fetches a new one.</summary>
    public static readonly TimeSpan MaxAge = TimeSpan.FromHours(20);

    /// <summary>How long after one download attempt the next may be made.</summary>
    public static readonly TimeSpan RetryAfter = TimeSpan.FromHours(1);

    private static readonly HttpClient Http = CreateClient();

    private readonly string _path;
    private readonly IFileSystem _files;
    private readonly Func<bool> _allowed;
    private readonly Func<DateTimeOffset> _now;
    private readonly Func<CancellationToken, Task<byte[]?>> _fetch;
    private readonly ILogger _logger;
    private readonly SemaphoreSlim _gate = new(1, 1);

    private SignalIndex? _parsed;
    private FileState? _parsedFrom;
    private DateTimeOffset? _lastAttempt;

    /// <param name="folder"><c>AppPaths.Signals</c>.</param>
    /// <param name="allowed">Read at each request, so turning the setting off stops the next fetch.</param>
    /// <param name="fetch">The download, returning the file's bytes or null; the release by default.</param>
    public SignalIndexStore(
        string folder,
        IFileSystem files,
        Func<bool> allowed,
        Func<DateTimeOffset> now,
        ILogger logger,
        Func<CancellationToken, Task<byte[]?>>? fetch = null)
    {
        _path = Path.Combine(folder, FileName);
        _files = files;
        _allowed = allowed;
        _now = now;
        _logger = logger;
        _fetch = fetch ?? FetchAsync;
    }

    public async Task<SignalIndex?> GetAsync(CancellationToken cancellationToken)
    {
        if (!_allowed())
        {
            return null;
        }

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

    private async Task<SignalIndex?> LoadAsync(CancellationToken cancellationToken)
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
    private SignalIndex? Parsed(FileState copy)
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
            _logger.LogInformation("The signal index on disk could not be read ({Error}).", ex.GetType().Name);
            return null;
        }
    }

    private async Task<SignalIndex?> DownloadAsync(CancellationToken cancellationToken)
    {
        var bytes = await _fetch(cancellationToken).ConfigureAwait(false);

        if (bytes is null)
        {
            return null;
        }

        SignalIndex index;

        try
        {
            index = Parse(new MemoryStream(bytes, writable: false));
        }
        catch (InvalidDataException ex)
        {
            _logger.LogInformation("The downloaded signal index was refused ({Reason}); the copy on disk stays.", ex.Message);
            return null;
        }

        try
        {
            _files.WriteBytes(_path, bytes);
            _parsedFrom = _files.Stat(_path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _logger.LogInformation("The signal index could not be stored ({Error}).", ex.GetType().Name);
            _parsedFrom = null;
        }

        _parsed = index;

        _logger.LogInformation("Signal index downloaded: {Rows} signals, {Malformed} malformed rows skipped.", index.Count, index.Malformed);

        return index;
    }

    private static SignalIndex Parse(Stream compressed)
    {
        using var gzip = new GZipStream(compressed, CompressionMode.Decompress);
        using var reader = new StreamReader(gzip);

        return SignalIndex.Read(reader);
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
                        ? "No signal index is published yet ({Status})."
                        : "The signal index release returned {Status}.",
                    response.StatusCode);

                return null;
            }

            if (response.Content.Headers.ContentLength > MaxDownloadBytes)
            {
                _logger.LogInformation("The signal index is larger than {Limit} bytes and was not downloaded.", MaxDownloadBytes);
                return null;
            }

            await using var body = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
            using var buffer = new MemoryStream();
            var chunk = new byte[81_920];

            while (await body.ReadAsync(chunk, cancellationToken).ConfigureAwait(false) is var read and > 0)
            {
                if (buffer.Length + read > MaxDownloadBytes)
                {
                    _logger.LogInformation("The signal index is larger than {Limit} bytes and was not downloaded.", MaxDownloadBytes);
                    return null;
                }

                buffer.Write(chunk, 0, read);
            }

            return buffer.ToArray();
        }
        catch (Exception ex) when (ex is HttpRequestException or IOException
            || (ex is TaskCanceledException && !cancellationToken.IsCancellationRequested))
        {
            _logger.LogInformation("The signal index could not be fetched ({Error}).", ex.GetType().Name);
            return null;
        }
    }

    private static HttpClient CreateClient()
    {
        var client = new HttpClient { Timeout = TimeSpan.FromSeconds(120) };

        client.DefaultRequestHeaders.UserAgent.ParseAdd("d47-signal-index");

        return client;
    }
}
