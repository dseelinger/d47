using System.Globalization;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using D47.Core.Audio;
using Microsoft.Extensions.Logging;

namespace D47.App.Media;

/// <summary>
/// Downloads the music library into <c>data\music-pack\&lt;folder&gt;\</c>, one folder at a time in manifest
/// order, while the setting is on.
/// </summary>
internal sealed class MusicPackStore
{
    /// <summary>Where the library is published, pinned exactly as every URL in <c>UpdateChecker</c> is.</summary>
    internal const string Source = "https://github.com/dseelinger/d47-music/releases/download/music-1/";

    /// <summary>Written into a folder before it is renamed into place, so the folder and its version arrive together.</summary>
    internal const string VersionFile = ".version";

    /// <summary>The last manifest fetched, kept for the size and the count while the setting is off.</summary>
    internal const string ManifestFile = "manifest.json";

    private const string Partial = ".partial";
    private const string Old = ".old";
    private const string Download = ".zip.part";

    /// <summary>The longest a read of the download may wait for bytes before the folder counts as failed.</summary>
    private static readonly TimeSpan ReadStall = TimeSpan.FromSeconds(60);

    /// <summary>The longest a request may wait for GitHub's response headers, and for the whole manifest.</summary>
    private static readonly TimeSpan Answer = TimeSpan.FromMinutes(2);

    private static readonly JsonSerializerOptions Json = new() { PropertyNameCaseInsensitive = true };

    private readonly string _folder;
    private readonly Func<bool> _allowed;
    private readonly IMusicPackDisk _disk;
    private readonly HttpClient _http;
    private readonly ILogger _logger;
    private readonly Lock _gate = new();

    /// <summary>Folders that failed this session; tried again at the next start.</summary>
    private readonly HashSet<string> _failed = new(StringComparer.Ordinal);

    private IReadOnlyList<Entry> _manifest = [];
    private int _present;
    private string? _fetching;
    private string? _problem;
    private string? _failure;
    private Task? _run;

    /// <param name="allowed">Read before each folder and during each download, so turning the setting off stops the next one.</param>
    internal MusicPackStore(string folder, Func<bool> allowed, IMusicPackDisk disk, HttpClient http, ILogger logger)
    {
        _folder = folder;
        _allowed = allowed;
        _disk = disk;
        _http = http;
        _logger = logger;

        if (Parse(_disk.ReadText(Path.Combine(folder, ManifestFile))) is { } kept)
        {
            _manifest = kept;
            _present = kept.Count(Installed);
        }
    }

    /// <summary>Raised on a background thread when a folder has been put in place.</summary>
    internal event Action? Landed;

    /// <summary>Raised on a background thread when <see cref="Status"/> may read differently.</summary>
    internal event Action? Changed;

    /// <summary>The row's state line: the size, how many folders are in, and what is being fetched or what stopped it.</summary>
    internal string? Status
    {
        get
        {
            lock (_gate)
            {
                return Describe();
            }
        }
    }

    /// <summary>Starts a download unless one is running or the setting is off. Returns the run, for a test to wait on.</summary>
    internal Task Start()
    {
        lock (_gate)
        {
            if (_run is { IsCompleted: false } running)
            {
                return running;
            }

            if (!_allowed())
            {
                return Task.CompletedTask;
            }

            _problem = null;
            _run = Task.Run(RunAsync);

            return _run;
        }
    }

    private async Task RunAsync()
    {
        try
        {
            ClearLeftovers();

            if (await FetchManifestAsync().ConfigureAwait(false) is not { } manifest)
            {
                return;
            }

            foreach (var entry in manifest)
            {
                if (Installed(entry))
                {
                    continue;
                }

                lock (_gate)
                {
                    if (_failed.Contains(entry.Name))
                    {
                        continue;
                    }
                }

                if (!_allowed())
                {
                    return;
                }

                var free = _disk.FreeBytes(_folder);

                if (free is { } bytes && bytes < 2 * entry.Bytes)
                {
                    Say(problem: $"Stopped: {entry.Name} needs {Size(2 * entry.Bytes)} free and the drive has {Size(bytes)}.");
                    _logger.LogInformation("The music library stopped at {Folder}: {Free} bytes free", entry.Name, bytes);
                    return;
                }

                Say(fetching: entry.Name);

                if (await FetchFolderAsync(entry).ConfigureAwait(false))
                {
                    lock (_gate)
                    {
                        _present = _manifest.Count(Installed);
                    }

                    Raise(Landed);
                }
            }
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "The music library download stopped");
            Say(problem: "The download stopped: " + exception.Message);
        }
        finally
        {
            lock (_gate)
            {
                _fetching = null;
            }

            Raise(Changed);
        }
    }

    private async Task<IReadOnlyList<Entry>?> FetchManifestAsync()
    {
        string text;

        try
        {
            using var answer = new CancellationTokenSource(Answer);
            using var response = await _http.GetAsync(Source + ManifestFile, answer.Token).ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                Say(problem: $"The library's list could not be read: GitHub returned {(int)response.StatusCode}.");
                return null;
            }

            text = await response.Content.ReadAsStringAsync(answer.Token).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException)
        {
            _logger.LogInformation(exception, "The music library's manifest could not be fetched");
            Say(problem: "The library's list could not be read: " + exception.Message);
            return null;
        }

        if (Parse(text) is not { } manifest)
        {
            Say(problem: "The library's list could not be read.");
            return null;
        }

        _disk.WriteText(Path.Combine(_folder, ManifestFile), text);

        lock (_gate)
        {
            _manifest = manifest;
            _present = manifest.Count(Installed);
        }

        Raise(Changed);

        return manifest;
    }

    /// <summary>Downloads, checks, unpacks and renames one folder into place. False when it failed or was stopped.</summary>
    private async Task<bool> FetchFolderAsync(Entry entry)
    {
        var target = Path.Combine(_folder, entry.Name);
        var partial = target + Partial;
        var old = target + Old;

        try
        {
            _disk.DeleteFolder(partial);

            await using (var zip = _disk.CreateTemp(target + Download))
            {
                if (!await DownloadAsync(entry, zip).ConfigureAwait(false))
                {
                    return false;
                }

                zip.Position = 0;
                _disk.Extract(zip, partial);
            }

            _disk.WriteText(Path.Combine(partial, VersionFile), entry.Version.ToString(CultureInfo.InvariantCulture));

            var replacing = _disk.FolderExists(target);

            if (replacing)
            {
                _disk.MoveFolder(target, old);
            }

            try
            {
                _disk.MoveFolder(partial, target);
            }
            catch
            {
                if (replacing)
                {
                    _disk.MoveFolder(old, target);
                }

                throw;
            }

            if (replacing)
            {
                try
                {
                    _disk.DeleteFolder(old);
                }
                catch (Exception exception)
                {
                    // The new folder is in place; the next run clears this one.
                    _logger.LogInformation(exception, "Could not remove {Folder}", old);
                }
            }

            _logger.LogInformation("Music library folder in: {Folder} version {Version}", entry.Name, entry.Version);

            return true;
        }
        catch (Exception exception)
        {
            Fail(entry, exception.Message);
            _logger.LogInformation(exception, "Music library folder {Folder} could not be fetched", entry.Name);

            return false;
        }
        finally
        {
            try
            {
                _disk.DeleteFolder(partial);
            }
            catch (Exception exception)
            {
                // Cleared at the start of the next run; the scan does not read a .partial folder.
                _logger.LogDebug(exception, "Could not remove {Folder}", partial);
            }
        }
    }

    /// <summary>Copies the zip into <paramref name="into"/> and checks it. False when it was stopped or did not match.</summary>
    private async Task<bool> DownloadAsync(Entry entry, Stream into)
    {
        using var answer = new CancellationTokenSource(Answer);
        using var response = await _http.GetAsync(Source + entry.Zip, HttpCompletionOption.ResponseHeadersRead, answer.Token)
            .ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
        {
            Fail(entry, $"GitHub returned {(int)response.StatusCode}.");
            return false;
        }

        await using var body = await response.Content.ReadAsStreamAsync().ConfigureAwait(false);
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        var buffer = new byte[81920];

        while (true)
        {
            if (!_allowed())
            {
                return false;
            }

            var read = await body.ReadAsync(buffer).AsTask().WaitAsync(ReadStall).ConfigureAwait(false);

            if (read == 0)
            {
                break;
            }

            hash.AppendData(buffer, 0, read);
            await into.WriteAsync(buffer.AsMemory(0, read)).ConfigureAwait(false);
        }

        if (!string.Equals(Convert.ToHexStringLower(hash.GetHashAndReset()), entry.Sha256, StringComparison.OrdinalIgnoreCase))
        {
            Fail(entry, "the download did not match its checksum.");
            return false;
        }

        return true;
    }

    /// <summary>Removes what a run cut off left behind: unpacked folders not yet renamed, and replaced ones not yet deleted.</summary>
    private void ClearLeftovers()
    {
        foreach (var name in Situations.All)
        {
            var target = Path.Combine(_folder, name);

            try
            {
                _disk.DeleteFolder(target + Partial);
                _disk.DeleteFolder(target + Old);
            }
            catch (Exception exception)
            {
                _logger.LogDebug(exception, "Could not clear what an earlier download of {Folder} left", name);
            }
        }
    }

    private bool Installed(Entry entry) =>
        _disk.ReadText(Path.Combine(_folder, entry.Name, VersionFile)) is { } text
        && int.TryParse(text.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var version)
        && version == entry.Version;

    private void Fail(Entry entry, string why)
    {
        lock (_gate)
        {
            _failed.Add(entry.Name);
            _failure = $"{entry.Name} failed: {why.TrimEnd('.')}. It is tried again at the next start.";
        }

        Raise(Changed);
    }

    private void Say(string? fetching = null, string? problem = null)
    {
        lock (_gate)
        {
            _fetching = fetching;

            if (problem is not null)
            {
                _problem = problem;
            }
        }

        Raise(Changed);
    }

    private string? Describe()
    {
        var parts = new List<string>();

        if (_manifest.Count > 0)
        {
            parts.Add($"{Size(_manifest.Sum(entry => entry.Bytes))} in all.");
            parts.Add(_present == _manifest.Count
                ? $"All {_manifest.Count} folders are in."
                : $"{_present} of {_manifest.Count} folders are in.");
        }

        if (!_allowed())
        {
            return parts.Count == 0 ? null : string.Join(' ', parts);
        }

        if (_fetching is not null)
        {
            parts.Add($"Fetching {_fetching}.");
        }

        if (_problem is not null)
        {
            parts.Add(_problem);
        }

        if (_failure is not null)
        {
            parts.Add(_failure);
        }

        return parts.Count == 0 ? null : string.Join(' ', parts);
    }

    private void Raise(Action? handler)
    {
        try
        {
            handler?.Invoke();
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "A music library subscriber failed");
        }
    }

    /// <summary>The manifest's folders that are real music folders, in its order; null when it does not parse.</summary>
    private IReadOnlyList<Entry>? Parse(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        Manifest? manifest;

        try
        {
            manifest = JsonSerializer.Deserialize<Manifest>(text.TrimStart('﻿'), Json);
        }
        catch (JsonException exception)
        {
            _logger.LogInformation(exception, "The music library's manifest does not parse");
            return null;
        }

        if (manifest?.Folders is not { } folders)
        {
            return null;
        }

        // The names become paths, so only the folders the music reads, and only their own zip.
        return folders
            .Where(entry => Situations.All.Contains(entry.Name, StringComparer.Ordinal)
                            && entry.Zip == entry.Name + ".zip"
                            && entry.Bytes > 0
                            && entry.Sha256 is { Length: 64 })
            .DistinctBy(entry => entry.Name)
            .ToList();
    }

    internal static string Size(long bytes) => bytes >= 1L << 30
        ? string.Create(CultureInfo.InvariantCulture, $"{bytes / (double)(1L << 30):0.0} GB")
        : string.Create(CultureInfo.InvariantCulture, $"{Math.Max(1, bytes >> 20)} MB");

    /// <summary>A client with no overall timeout: a folder is hundreds of megabytes. A stalled read is bounded instead.</summary>
    internal static HttpClient CreateClient()
    {
        var client = new HttpClient { Timeout = Timeout.InfiniteTimeSpan };

        client.DefaultRequestHeaders.UserAgent.ParseAdd("d47-music-library");

        return client;
    }

    private sealed record Manifest
    {
        [JsonPropertyName("folders")]
        public List<Entry>? Folders { get; init; }
    }

    internal sealed record Entry
    {
        [JsonPropertyName("name")]
        public string Name { get; init; } = "";

        [JsonPropertyName("version")]
        public int Version { get; init; }

        [JsonPropertyName("zip")]
        public string Zip { get; init; } = "";

        [JsonPropertyName("bytes")]
        public long Bytes { get; init; }

        [JsonPropertyName("sha256")]
        public string? Sha256 { get; init; }
    }
}

/// <summary>The file operations <see cref="MusicPackStore"/> uses. Paths are full paths.</summary>
internal interface IMusicPackDisk
{
    /// <summary>Bytes free on the drive holding the folder, or null when it cannot be read.</summary>
    long? FreeBytes(string folder);

    bool FolderExists(string path);

    /// <summary>Null when the file is missing.</summary>
    string? ReadText(string path);

    /// <summary>Creates the folder.</summary>
    void WriteText(string path, string contents);

    /// <summary>A readable, writable, seekable stream at the path, removed when it is disposed.</summary>
    Stream CreateTemp(string path);

    /// <summary>Unpacks the zip into the folder, creating it.</summary>
    void Extract(Stream zip, string folder);

    void MoveFolder(string from, string to);

    /// <summary>Removes the folder and everything in it; does nothing when it is missing.</summary>
    void DeleteFolder(string path);
}

internal sealed class MusicPackDisk : IMusicPackDisk
{
    public long? FreeBytes(string folder)
    {
        try
        {
            return Path.GetPathRoot(Path.GetFullPath(folder)) is { Length: > 0 } root
                ? new DriveInfo(root).AvailableFreeSpace
                : null;
        }
        catch (Exception exception) when (exception is IOException or ArgumentException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    public bool FolderExists(string path) => Directory.Exists(path);

    public string? ReadText(string path)
    {
        try
        {
            return File.ReadAllText(path);
        }
        catch (Exception exception) when (exception is FileNotFoundException or DirectoryNotFoundException)
        {
            return null;
        }
    }

    public void WriteText(string path, string contents)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, contents);
    }

    public Stream CreateTemp(string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);

        return new FileStream(
            path, FileMode.Create, FileAccess.ReadWrite, FileShare.None, 81920, FileOptions.DeleteOnClose | FileOptions.Asynchronous);
    }

    public void Extract(Stream zip, string folder)
    {
        Directory.CreateDirectory(folder);
        ZipFile.ExtractToDirectory(zip, folder, overwriteFiles: true);
    }

    public void MoveFolder(string from, string to) => Directory.Move(from, to);

    public void DeleteFolder(string path)
    {
        if (Directory.Exists(path))
        {
            Directory.Delete(path, recursive: true);
        }
    }
}
