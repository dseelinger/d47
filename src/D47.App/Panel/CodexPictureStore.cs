using System.Collections.Concurrent;
using System.Net;
using System.Net.Http;
using System.Text;
using D47.Core.Interface;
using D47.Core.Storage;
using Microsoft.Extensions.Logging;

namespace D47.App.Panel;

/// <summary>
/// Stock codex pictures fetched from the codex pictures repository's releases into <c>stock\</c>, and each
/// Commander's own pictures kept in <c>local\&lt;Frontier ID&gt;\</c>. Every fetch runs on the pool.
/// </summary>
public sealed class CodexPictureStore
{
    public const string StockFolder = "stock";

    public const string LocalFolder = "local";

    private readonly IFileSystem _files;
    private readonly string _folder;
    private readonly Func<bool> _allowed;
    private readonly Func<string?> _commander;
    private readonly Func<bool> _shared;
    private readonly ILogger _logger;
    private readonly HttpClient _http;
    private readonly Lock _gate = new();
    private readonly ConcurrentDictionary<CodexPictureKey, Task<bool>> _asked = new();
    private IReadOnlyList<CodexPictureRow>? _rows;
    private int _indexAsked;
    private bool _adopted;

    /// <param name="folder"><c>AppPaths.CodexPictures</c>.</param>
    /// <param name="allowed">Read at each fetch and each read of a stock row, so turning the setting off stops both.</param>
    /// <param name="commander">The active Commander's Frontier ID; null when no journal has been read.</param>
    /// <param name="shared">Whether every Commander's own pictures are one pool.</param>
    public CodexPictureStore(
        IFileSystem files,
        string folder,
        Func<bool> allowed,
        ILogger logger,
        HttpMessageHandler? handler = null,
        Func<string?>? commander = null,
        Func<bool>? shared = null)
    {
        _files = files;
        _folder = folder;
        _allowed = allowed;
        _commander = commander ?? (() => null);
        _shared = shared ?? (() => false);
        _logger = logger;
        _http = handler is null ? new HttpClient() : new HttpClient(handler);
        _http.Timeout = TimeSpan.FromSeconds(90);
        _http.DefaultRequestHeaders.UserAgent.ParseAdd("d47-codex-pictures");
    }

    /// <summary>Raised on a background thread after a stock picture has landed on disk.</summary>
    public event Action<CodexPictureKey>? Arrived;

    /// <summary>The index fetch the first <see cref="StockRows"/> call of the session started.</summary>
    public Task IndexFetched { get; private set; } = Task.CompletedTask;

    /// <summary>
    /// The stock rows: the index fetched this session, else the one on disk, else none. The first call in a
    /// session starts the fetch. Empty while <c>Ui.CodexPictures</c> is off.
    /// </summary>
    public IReadOnlyList<CodexPictureRow> StockRows()
    {
        if (!_allowed())
        {
            return [];
        }

        if (Interlocked.Exchange(ref _indexAsked, 1) == 0)
        {
            IndexFetched = Task.Run(FetchIndexAsync);
        }

        lock (_gate)
        {
            return _rows ??= ReadIndexOnDisk();
        }
    }

    /// <summary>Whether a Commander is active, so a picture of their own can be saved.</summary>
    public bool CanSave => Own() is not null;

    /// <summary>
    /// The keys with a picture the codex pages show: the active Commander's, and with sharing on every other
    /// Commander's. Empty while no Commander is active.
    /// </summary>
    public IReadOnlySet<CodexPictureKey> LocalKeys()
    {
        var keys = new HashSet<CodexPictureKey>();

        if (Own() is not { } own)
        {
            return keys;
        }

        foreach (var path in _files.Enumerate(Path.Combine(_folder, LocalFolder), "*", recursive: true))
        {
            if (CodexPictureKey.TryParse(Path.GetFileName(path), out var key) && (_shared() || IsIn(path, own)))
            {
                keys.Add(key);
            }
        }

        return keys;
    }

    /// <summary>
    /// The bytes of the picture to show for <paramref name="key"/>: the active Commander's, else with sharing on
    /// another Commander's; null when there is none.
    /// </summary>
    public byte[]? Local(CodexPictureKey key)
    {
        if (Own(key) is { } mine)
        {
            return mine;
        }

        if (Own() is null || !_shared())
        {
            return null;
        }

        return _files.Enumerate(Path.Combine(_folder, LocalFolder), key.FileName, recursive: true)
            .Select(_files.ReadBytes)
            .FirstOrDefault(bytes => bytes is not null);
    }

    /// <summary>The bytes of the active Commander's own picture; null when there is none.</summary>
    public byte[]? Own(CodexPictureKey key) => Own() is { } own ? _files.ReadBytes(Path.Combine(own, key.FileName)) : null;

    /// <summary>The capture the active Commander's picture was cut from; null when it is not kept.</summary>
    public byte[]? LocalSource(CodexPictureKey key) => Own() is { } own ? _files.ReadBytes(Path.Combine(own, key.SourceFileName)) : null;

    /// <summary>Whether the capture the active Commander's picture was cut from is kept.</summary>
    public bool HasLocalSource(CodexPictureKey key) => Own() is { } own && _files.Stat(Path.Combine(own, key.SourceFileName)) is not null;

    /// <summary>The square the active Commander's picture was cut with; null when it is not kept.</summary>
    public CropSquare? LocalCrop(CodexPictureKey key) =>
        Own() is { } own ? CropSquare.Parse(_files.ReadText(Path.Combine(own, key.CropFileName))) : null;

    /// <summary>Whether the active Commander has a picture of their own for <paramref name="key"/>.</summary>
    public bool HasLocal(CodexPictureKey key) => Own() is { } own && _files.Stat(Path.Combine(own, key.FileName)) is not null;

    /// <summary>Keeps the picture, its source and its square; the picture is written last, so it appears only when the rest is kept.</summary>
    public void SaveLocal(CodexPictureKey key, byte[] picture, byte[] source, CropSquare crop)
    {
        var folder = Own() ?? throw new InvalidOperationException("No Commander is active.");

        _files.WriteBytes(Path.Combine(folder, key.SourceFileName), source);
        _files.WriteText(Path.Combine(folder, key.CropFileName), crop.Write());
        _files.WriteBytes(Path.Combine(folder, key.FileName), picture);
    }

    /// <summary>Deletes the active Commander's picture, its source and its square.</summary>
    public void RemoveLocal(CodexPictureKey key)
    {
        if (Own() is not { } folder)
        {
            return;
        }

        _files.Delete(Path.Combine(folder, key.FileName));
        _files.Delete(Path.Combine(folder, key.SourceFileName));
        _files.Delete(Path.Combine(folder, key.CropFileName));
    }

    /// <summary>
    /// The path of <paramref name="row"/>'s picture when it is on disk and matches the row. Otherwise null, and
    /// the picture is fetched once per session; <see cref="Arrived"/> follows when it lands.
    /// </summary>
    public string? Picture(CodexPictureRow row) => Stock(row) is null ? null : StockPath(row.Asset);

    /// <summary>The bytes of <see cref="Picture"/>; null under the same conditions.</summary>
    public byte[]? Stock(CodexPictureRow row)
    {
        if (!_allowed())
        {
            return null;
        }

        if (_files.ReadBytes(StockPath(row.Asset)) is { } bytes && CodexPictureIndex.Matches(row, bytes))
        {
            return bytes;
        }

        _asked.GetOrAdd(row.Key, _ => Task.Run(() => FetchPictureAsync(row)));
        return null;
    }

    /// <summary>The bytes of <paramref name="row"/>'s picture when it is on disk and matches; null otherwise. Never starts a fetch.</summary>
    public byte[]? StockOnDisk(CodexPictureRow row) =>
        _allowed() && _files.ReadBytes(StockPath(row.Asset)) is { } bytes && CodexPictureIndex.Matches(row, bytes) ? bytes : null;

    /// <summary>
    /// Keeps a picture just published as <paramref name="row"/> and the index that lists it, then deletes the
    /// active Commander's picture of that key; its source and square stay.
    /// </summary>
    public void Published(CodexPictureRow row, byte[] picture, string index, IReadOnlyList<CodexPictureRow> rows)
    {
        _files.WriteBytes(StockPath(row.Asset), picture);
        AcceptIndex(index, rows);

        if (Own() is { } own)
        {
            _files.Delete(Path.Combine(own, row.Key.FileName));
        }
    }

    /// <summary>Keeps the index written without <paramref name="row"/> and deletes its picture from <c>stock\</c>.</summary>
    public void Unpublished(CodexPictureRow row, string index, IReadOnlyList<CodexPictureRow> rows)
    {
        AcceptIndex(index, rows);
        _files.Delete(StockPath(row.Asset));
    }

    private void AcceptIndex(string index, IReadOnlyList<CodexPictureRow> rows)
    {
        _files.WriteText(StockPath(CodexPictureIndex.IndexName), index);

        lock (_gate)
        {
            _rows = rows;
        }
    }

    /// <summary>The fetch started for <paramref name="key"/>; true when the picture landed.</summary>
    public Task<bool> Fetched(CodexPictureKey key) => _asked.TryGetValue(key, out var task) ? task : Task.FromResult(false);

    /// <summary>
    /// The active Commander's folder, or null with none active. The first call with a Commander active moves the
    /// pictures that sit directly in <c>local\</c> into it.
    /// </summary>
    private string? Own()
    {
        if (_commander() is not { Length: > 0 } id)
        {
            return null;
        }

        var root = Path.Combine(_folder, LocalFolder);
        var own = Path.Combine(root, FolderName(id));

        lock (_gate)
        {
            if (!_adopted)
            {
                foreach (var path in _files.Enumerate(root, "*"))
                {
                    if (char.IsAsciiDigit(Path.GetFileName(path)[0]) && _files.Stat(path) is not null)
                    {
                        _files.Copy(path, Path.Combine(own, Path.GetFileName(path)));
                        _files.Delete(path);
                    }
                }

                _adopted = true;
            }
        }

        return own;
    }

    private static string FolderName(string id) =>
        string.Concat(id.Select(c => Array.IndexOf(Path.GetInvalidFileNameChars(), c) >= 0 ? '_' : c));

    private static bool IsIn(string path, string folder) =>
        string.Equals(Path.GetDirectoryName(path), folder, StringComparison.OrdinalIgnoreCase);

    private string StockPath(string name) => Path.Combine(_folder, StockFolder, name);

    private static string Url(string tag, string file) =>
        $"https://github.com/{CodexPictureIndex.Repository}/releases/download/{tag}/{Uri.EscapeDataString(file)}";

    private IReadOnlyList<CodexPictureRow> ReadIndexOnDisk()
    {
        if (_files.ReadText(StockPath(CodexPictureIndex.IndexName)) is not { } text)
        {
            return [];
        }

        var parsed = CodexPictureIndex.Parse(text);
        LogReasons(parsed);
        return parsed.Rows;
    }

    private void LogReasons(CodexPictureIndexParse parsed)
    {
        foreach (var reason in parsed.Reasons)
        {
            _logger.LogInformation("Codex picture index: {Reason}", reason);
        }
    }

    private async Task FetchIndexAsync()
    {
        var bytes = await GetAsync(CodexPictureIndex.FirstTag, CodexPictureIndex.IndexName).ConfigureAwait(false);

        if (bytes is null)
        {
            return;
        }

        var text = Encoding.UTF8.GetString(bytes);
        var parsed = CodexPictureIndex.Parse(text);

        if (parsed.Rows.Count == 0 && parsed.Reasons.Count > 0)
        {
            _logger.LogInformation("The codex picture index could not be used ({Reason}); the copy on disk stays.", parsed.Reasons[0]);
            return;
        }

        LogReasons(parsed);

        try
        {
            _files.WriteText(StockPath(CodexPictureIndex.IndexName), text);
            Prune(parsed.Rows);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _logger.LogInformation("The codex picture index could not be stored ({Error}).", ex.GetType().Name);
        }

        lock (_gate)
        {
            _rows = parsed.Rows;
        }
    }

    private void Prune(IReadOnlyList<CodexPictureRow> rows)
    {
        var named = rows.Select(row => row.Asset).ToHashSet(StringComparer.OrdinalIgnoreCase);
        named.Add(CodexPictureIndex.IndexName);

        foreach (var path in _files.Enumerate(Path.Combine(_folder, StockFolder), "*"))
        {
            if (!named.Contains(Path.GetFileName(path)))
            {
                _files.Delete(path);
            }
        }
    }

    private async Task<bool> FetchPictureAsync(CodexPictureRow row)
    {
        var bytes = await GetAsync(row.Tag, row.Asset).ConfigureAwait(false);

        if (bytes is null)
        {
            return false;
        }

        if (!CodexPictureIndex.Matches(row, bytes))
        {
            _logger.LogInformation("Codex picture {Asset} does not match the index; it was discarded.", row.Asset);
            return false;
        }

        var path = StockPath(row.Asset);

        try
        {
            _files.WriteBytes(path + ".part", bytes);
            _files.Copy(path + ".part", path);
            _files.Delete(path + ".part");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _logger.LogInformation("Codex picture {Asset} could not be stored ({Error}).", row.Asset, ex.GetType().Name);
            return false;
        }

        try
        {
            Arrived?.Invoke(row.Key);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Redrawing after a codex picture arrived failed");
        }

        return true;
    }

    /// <summary>The body of the release asset, or null. A 404 means nothing is published and is not a failure.</summary>
    private async Task<byte[]?> GetAsync(string tag, string file)
    {
        try
        {
            using var response = await _http.GetAsync(Url(tag, file), CancellationToken.None).ConfigureAwait(false);

            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                _logger.LogInformation("No stock codex picture file {File} is published at {Tag}.", file, tag);
                return null;
            }

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogInformation("Stock codex picture file {File} could not be fetched: {Status}.", file, response.StatusCode);
                return null;
            }

            return await response.Content.ReadAsByteArrayAsync(CancellationToken.None).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is HttpRequestException or IOException or TaskCanceledException)
        {
            _logger.LogInformation("Stock codex picture file {File} could not be fetched ({Error}).", file, ex.GetType().Name);
            return null;
        }
    }
}
