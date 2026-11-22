using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;
using D47.Core.Audio;
using Microsoft.Extensions.Logging;

namespace D47.App.Media;

/// <summary>
/// Keeps <see cref="Kept"/> unplayed library tracks in each <c>data\music-pack\&lt;folder&gt;\</c> while the setting is
/// on, fetching each one by byte ranges out of its folder's zip, and deletes a played track once its replacement has
/// landed. Each folder draws its tracks at random from those not played this cycle.
/// </summary>
internal sealed class MusicPackStore
{
    /// <summary>Where the library is published, pinned exactly as every URL in <c>UpdateChecker</c> is.</summary>
    internal const string Source = "https://github.com/dseelinger/d47-music/releases/download/music-1/";

    /// <summary>The version of the folder's zip that the tracks in the folder were checked against.</summary>
    internal const string VersionFile = ".version";

    /// <summary>The last manifest fetched, kept for the size while the setting is off.</summary>
    internal const string ManifestFile = "manifest.json";

    /// <summary>Each folder's tracks played this cycle and the zip checksum its tracks were checked against.</summary>
    internal const string PlayedFile = "played.json";

    /// <summary>Unplayed tracks each folder keeps on disk.</summary>
    internal const int Kept = 2;

    /// <summary>Added to a track's name while it is written; the music scan reads no file with this extension.</summary>
    internal const string Part = ".part";

    private const string Partial = ".partial";
    private const string Old = ".old";

    /// <summary>The longest a read of a download may wait for bytes before the track counts as failed.</summary>
    private static readonly TimeSpan ReadStall = TimeSpan.FromSeconds(60);

    /// <summary>The longest a request may wait for GitHub's response headers, and for the whole manifest.</summary>
    private static readonly TimeSpan Answer = TimeSpan.FromMinutes(2);

    private static readonly JsonSerializerOptions Json = new() { PropertyNameCaseInsensitive = true };

    private readonly string _folder;
    private readonly Func<bool> _allowed;
    private readonly Func<IReadOnlyList<(string Folder, string File)>> _rejects;
    private readonly IMusicPackDisk _disk;
    private readonly HttpClient _http;
    private readonly ILogger _logger;
    private readonly Random _random;
    private readonly Lock _gate = new();

    /// <summary>Each folder's central directory, with the zip it was read from.</summary>
    private readonly Dictionary<string, (Entry From, List<ZipTrack> Tracks)> _directories = new(StringComparer.Ordinal);

    /// <summary>Tracks reported played and not yet written to <see cref="PlayedFile"/>.</summary>
    private readonly List<(string Folder, string File)> _pending = [];

    /// <summary>Folders that failed in the current run; tried again in the next.</summary>
    private readonly HashSet<string> _failed = new(StringComparer.Ordinal);

    private readonly PlayedState _played;

    private IReadOnlyList<Entry> _manifest = [];
    private bool _manifestFetched;
    private (int Tracks, int Folders, long Bytes) _ready;
    private string? _fetching;
    private string? _problem;
    private (string Folder, string Text)? _failure;
    private bool _running;
    private bool _again;
    private bool _refresh;
    private Task _run = Task.CompletedTask;

    /// <param name="allowed">Read before each track and during each download, so turning the setting off stops the next one.</param>
    /// <param name="rejects">The tracks queued for deletion, which are never fetched.</param>
    internal MusicPackStore(
        string folder,
        Func<bool> allowed,
        Func<IReadOnlyList<(string Folder, string File)>> rejects,
        IMusicPackDisk disk,
        HttpClient http,
        ILogger logger,
        Random? random = null)
    {
        _folder = folder;
        _allowed = allowed;
        _rejects = rejects;
        _disk = disk;
        _http = http;
        _logger = logger;
        _random = random ?? Random.Shared;
        _played = ReadPlayed();

        if (Parse(_disk.ReadText(Path.Combine(folder, ManifestFile))) is { } kept)
        {
            _manifest = kept;
        }

        Recount();
    }

    /// <summary>Raised on a background thread when a track has been put in place or deleted.</summary>
    internal event Action? Landed;

    /// <summary>Raised on a background thread when <see cref="Status"/> may read differently.</summary>
    internal event Action? Changed;

    /// <summary>The row's state line: the size, the tracks ready, and what is being fetched or what stopped it.</summary>
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

    /// <summary>
    /// Fetches the manifest again and fills every folder, unless the setting is off. Returns the run, for a test to
    /// wait on; while one is running it runs once more.
    /// </summary>
    internal Task Start() => Kick(refresh: true);

    /// <summary>
    /// Records a library track as played and fills its folder again. Does no file work on the caller's thread. Returns
    /// the run, for a test to wait on.
    /// </summary>
    internal Task Played(MusicTrack track)
    {
        if (track is not { PackFolder: { } folder, PackFile: { } file })
        {
            return Task.CompletedTask;
        }

        lock (_gate)
        {
            _pending.Add((folder, file));
        }

        return Kick(refresh: false);
    }

    private Task Kick(bool refresh)
    {
        lock (_gate)
        {
            _refresh |= refresh;

            if (_running)
            {
                _again = true;
                return _run;
            }

            if (!_allowed())
            {
                return Task.CompletedTask;
            }

            _problem = null;
            _running = true;
            _run = Task.Run(RunAsync);

            return _run;
        }
    }

    private async Task RunAsync()
    {
        try
        {
            lock (_gate)
            {
                _failed.Clear();
            }

            while (true)
            {
                bool refresh;

                lock (_gate)
                {
                    _again = false;
                    refresh = _refresh || !_manifestFetched;
                    _refresh = false;
                }

                await PassAsync(refresh).ConfigureAwait(false);

                lock (_gate)
                {
                    if (!_again || !_allowed())
                    {
                        _running = false;
                        _fetching = null;
                        break;
                    }
                }
            }
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "The music library download stopped");

            lock (_gate)
            {
                _problem = "The download stopped: " + exception.Message;
                _running = false;
                _fetching = null;
            }
        }

        Raise(Changed);
    }

    private async Task PassAsync(bool refresh)
    {
        ClearLeftovers();
        RecordPending();

        if (Trim())
        {
            Recount();
            Raise(Landed);
        }

        if (refresh && await FetchManifestAsync().ConfigureAwait(false) is null)
        {
            return;
        }

        IReadOnlyList<Entry> manifest;

        lock (_gate)
        {
            manifest = _manifest;
        }

        var rejects = _rejects().ToHashSet();

        foreach (var entry in manifest)
        {
            lock (_gate)
            {
                if (_failed.Contains(entry.Name))
                {
                    continue;
                }
            }

            if (!_allowed() || !await FolderAsync(entry, rejects).ConfigureAwait(false))
            {
                return;
            }
        }
    }

    /// <summary>Fills one folder to <see cref="Kept"/> unplayed tracks. False when the run has to stop.</summary>
    private async Task<bool> FolderAsync(Entry entry, HashSet<(string Folder, string File)> rejects)
    {
        var here = Path.Combine(_folder, entry.Name);
        var onDisk = TrackNames(here);
        var played = _played.For(entry.Name);

        bool Spent(string name) => played.Contains(name) || rejects.Contains((entry.Name, name));
        int Unplayed() => onDisk.Count(name => !Spent(name));

        var verified = Version(here) == entry.Version && string.Equals(_played.Sha256(entry.Name), entry.Sha256, StringComparison.OrdinalIgnoreCase);

        if (verified && Unplayed() >= Kept)
        {
            if (DeleteSpent(here, onDisk, Spent, all: true))
            {
                Recount();
                Raise(Landed);
            }

            return true;
        }

        List<ZipTrack> directory;

        try
        {
            directory = await DirectoryAsync(entry).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is HttpRequestException or IOException or InvalidDataException or TaskCanceledException or TimeoutException)
        {
            Fail(entry, exception.Message);
            _logger.LogInformation(exception, "The directory of music folder {Folder} could not be read", entry.Name);
            return true;
        }

        var byName = directory.ToDictionary(track => track.Name, StringComparer.Ordinal);

        if (!verified)
        {
            if (Check(here, onDisk, byName))
            {
                Recount();
                Raise(Landed);
            }

            _disk.WriteText(Path.Combine(here, VersionFile), entry.Version.ToString(CultureInfo.InvariantCulture));
            _played.Checked(entry.Name, entry.Sha256!);
        }

        played.RemoveWhere(name => !byName.ContainsKey(name));
        SavePlayed();

        while (Unplayed() < Kept)
        {
            if (!_allowed())
            {
                return false;
            }

            var candidates = Candidates(directory, onDisk, Spent);

            if (candidates.Count == 0)
            {
                // Every track has played: a new cycle, keeping the played tracks still on disk marked for deletion.
                played.RemoveWhere(name => !onDisk.Contains(name));
                SavePlayed();
                candidates = Candidates(directory, onDisk, Spent);

                if (candidates.Count == 0)
                {
                    break;
                }
            }

            var track = candidates[_random.Next(candidates.Count)];

            if (_disk.FreeBytes(_folder) is { } free && free < 2 * track.Bytes)
            {
                Say(problem: $"Stopped: {entry.Name} needs {Size(2 * track.Bytes)} free and the drive has {Size(free)}.");
                _logger.LogInformation("The music library stopped at {Folder}: {Free} bytes free", entry.Name, free);
                return false;
            }

            Say(fetching: entry.Name);

            if (!await FetchTrackAsync(entry, here, track).ConfigureAwait(false))
            {
                return _allowed();
            }

            onDisk.Add(track.Name);
            ClearFailure(entry.Name);
            DeleteSpent(here, onDisk, Spent, all: Unplayed() >= Kept);
            Recount();
            Raise(Landed);
        }

        return true;
    }

    private static List<ZipTrack> Candidates(List<ZipTrack> directory, HashSet<string> onDisk, Func<string, bool> spent) =>
        directory.Where(track => !onDisk.Contains(track.Name) && !spent(track.Name)).ToList();

    /// <summary>Keeps the tracks on disk that match a directory entry by name, size and CRC-32. True when any was deleted.</summary>
    private bool Check(string here, HashSet<string> onDisk, Dictionary<string, ZipTrack> byName)
    {
        var deleted = false;
        var sizes = _disk.Files(here).ToDictionary(file => file.Name, file => file.Bytes, StringComparer.OrdinalIgnoreCase);

        foreach (var name in onDisk.ToList())
        {
            var path = Path.Combine(here, name);
            bool matches;

            try
            {
                matches = byName.TryGetValue(name, out var track)
                          && sizes.TryGetValue(name, out var bytes)
                          && bytes == track.Bytes
                          && Crc(path) == track.Crc;
            }
            catch (IOException exception)
            {
                _logger.LogInformation(exception, "Could not read {File} to check it", path);
                continue;
            }

            if (!matches && Delete(path))
            {
                onDisk.Remove(name);
                deleted = true;
            }
        }

        return deleted;
    }

    private uint Crc(string path)
    {
        using var stream = _disk.OpenRead(path);
        return Crc32.Of(stream);
    }

    /// <summary>
    /// Deletes spent tracks from the folder: every one when <paramref name="all"/>, otherwise one, for the
    /// replacement that just landed. True when any was deleted.
    /// </summary>
    private bool DeleteSpent(string here, HashSet<string> onDisk, Func<string, bool> spent, bool all)
    {
        var deleted = false;

        foreach (var name in onDisk.Where(spent).Order(StringComparer.Ordinal).ToList())
        {
            if (Delete(Path.Combine(here, name)))
            {
                onDisk.Remove(name);
                deleted = true;

                if (!all)
                {
                    break;
                }
            }
        }

        return deleted;
    }

    /// <summary>A folder holding more than <see cref="Kept"/> tracks keeps that many, unplayed first. True when any was deleted.</summary>
    private bool Trim()
    {
        var deleted = false;

        foreach (var name in Situations.All)
        {
            var here = Path.Combine(_folder, name);
            var tracks = TrackNames(here).Order(StringComparer.Ordinal).ToList();

            if (tracks.Count <= Kept)
            {
                continue;
            }

            var played = _played.Peek(name);
            var keep = tracks
                .OrderBy(track => played.Contains(track))
                .ThenBy(_ => _random.Next())
                .Take(Kept)
                .ToHashSet(StringComparer.Ordinal);

            foreach (var track in tracks.Where(track => !keep.Contains(track)))
            {
                deleted |= Delete(Path.Combine(here, track));
            }
        }

        return deleted;
    }

    /// <summary>Reads the folder's central directory, once for each version of its zip.</summary>
    private async Task<List<ZipTrack>> DirectoryAsync(Entry entry)
    {
        lock (_gate)
        {
            if (_directories.TryGetValue(entry.Name, out var known) && known.From == entry)
            {
                return known.Tracks;
            }
        }

        var tailLength = Math.Min(entry.Bytes, ZipDirectory.TailLength);
        var tailFrom = entry.Bytes - tailLength;
        var tail = await ReadAsync(entry, tailFrom, tailLength).ConfigureAwait(false);
        var (offset, length, count) = ZipDirectory.End(tail, entry.Bytes);

        var directory = offset >= tailFrom
            ? tail.AsMemory((int)(offset - tailFrom), (int)length)
            : await ReadAsync(entry, offset, length).ConfigureAwait(false);

        var tracks = ZipDirectory.Entries(directory.Span, count, offset);

        lock (_gate)
        {
            _directories[entry.Name] = (entry, tracks);
        }

        return tracks;
    }

    /// <summary>Writes one track under its temporary name, checks it, and renames it into place. False when it failed or was stopped.</summary>
    private async Task<bool> FetchTrackAsync(Entry entry, string here, ZipTrack track)
    {
        var target = Path.Combine(here, track.Name);
        var temp = target + Part;

        try
        {
            var header = await ReadAsync(entry, track.Header, ZipDirectory.HeaderLength).ConfigureAwait(false);
            var start = ZipDirectory.DataStart(header, track.Header);

            if (start + track.Bytes > entry.Bytes)
            {
                throw new InvalidDataException("the track lies outside its zip");
            }

            uint crc = 0;
            long written = 0;

            await using (var file = _disk.Create(temp))
            {
                using var response = await RangeAsync(entry, start, track.Bytes).ConfigureAwait(false);
                await using var body = await response.Content.ReadAsStreamAsync().ConfigureAwait(false);
                var buffer = new byte[81920];

                while (written < track.Bytes)
                {
                    if (!_allowed())
                    {
                        break;
                    }

                    var want = (int)Math.Min(buffer.Length, track.Bytes - written);
                    var read = await body.ReadAsync(buffer.AsMemory(0, want)).AsTask().WaitAsync(ReadStall).ConfigureAwait(false);

                    if (read == 0)
                    {
                        break;
                    }

                    crc = Crc32.Append(crc, buffer.AsSpan(0, read));
                    await file.WriteAsync(buffer.AsMemory(0, read)).ConfigureAwait(false);
                    written += read;
                }
            }

            if (!_allowed())
            {
                Delete(temp);
                return false;
            }

            if (written != track.Bytes || crc != track.Crc)
            {
                Delete(temp);
                Fail(entry, $"{track.Name} did not match its checksum.");
                return false;
            }

            _disk.MoveFile(temp, target);
            _logger.LogInformation("Music library track in: {Folder} {File}", entry.Name, track.Name);

            return true;
        }
        catch (Exception exception) when (exception is HttpRequestException or IOException or InvalidDataException or TaskCanceledException or TimeoutException or UnauthorizedAccessException)
        {
            Delete(temp);
            Fail(entry, exception.Message);
            _logger.LogInformation(exception, "Music library track {Folder} {File} could not be fetched", entry.Name, track.Name);

            return false;
        }
    }

    /// <summary>Exactly <paramref name="length"/> bytes of the zip from <paramref name="from"/>.</summary>
    private async Task<byte[]> ReadAsync(Entry entry, long from, long length)
    {
        using var response = await RangeAsync(entry, from, length).ConfigureAwait(false);
        await using var body = await response.Content.ReadAsStreamAsync().ConfigureAwait(false);
        var bytes = new byte[length];
        var at = 0;

        while (at < bytes.Length)
        {
            var read = await body.ReadAsync(bytes.AsMemory(at)).AsTask().WaitAsync(ReadStall).ConfigureAwait(false);

            if (read == 0)
            {
                throw new IOException($"{entry.Zip} ended early");
            }

            at += read;
        }

        return bytes;
    }

    /// <summary>
    /// A ranged GET of the zip, refused unless GitHub answers with exactly that range of a file of the manifest's size,
    /// so a whole zip is never streamed and a zip changed since the manifest is noticed.
    /// </summary>
    private async Task<HttpResponseMessage> RangeAsync(Entry entry, long from, long length)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, Source + entry.Zip);
        request.Headers.Range = new RangeHeaderValue(from, from + length - 1);

        using var answer = new CancellationTokenSource(Answer);
        var response = await _http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, answer.Token).ConfigureAwait(false);

        if (response.StatusCode != HttpStatusCode.PartialContent
            || response.Content.Headers.ContentRange is not { From: { } start, To: { } end, Length: { } total }
            || start != from
            || end != from + length - 1
            || total != entry.Bytes)
        {
            var status = (int)response.StatusCode;
            response.Dispose();
            throw new IOException($"GitHub returned {status} without the part of {entry.Zip} asked for");
        }

        return response;
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
            _manifestFetched = true;
            _problem = null;
        }

        Raise(Changed);

        return manifest;
    }

    /// <summary>Removes what an earlier run cut off: tracks not yet renamed, and the whole-folder download's leftovers.</summary>
    private void ClearLeftovers()
    {
        foreach (var name in Situations.All)
        {
            var target = Path.Combine(_folder, name);

            try
            {
                _disk.DeleteFolder(target + Partial);
                _disk.DeleteFolder(target + Old);

                foreach (var (file, _) in _disk.Files(target).Where(file => file.Name.EndsWith(Part, StringComparison.OrdinalIgnoreCase)))
                {
                    _disk.DeleteFile(Path.Combine(target, file));
                }
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                _logger.LogDebug(exception, "Could not clear what an earlier download of {Folder} left", name);
            }
        }
    }

    /// <summary>The library tracks in a folder, by file name.</summary>
    private HashSet<string> TrackNames(string here) =>
        _disk.Files(here)
            .Select(file => file.Name)
            .Where(ZipDirectory.IsTrackName)
            .ToHashSet(StringComparer.Ordinal);

    private int? Version(string here) =>
        _disk.ReadText(Path.Combine(here, VersionFile)) is { } text
        && int.TryParse(text.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var version)
            ? version
            : null;

    private bool Delete(string path)
    {
        try
        {
            _disk.DeleteFile(path);
            return true;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // Playing, most likely; tried again in a later run.
            _logger.LogInformation(exception, "Could not delete {File}", path);
            return false;
        }
    }

    private void RecordPending()
    {
        List<(string Folder, string File)> pending;

        lock (_gate)
        {
            pending = [.. _pending];
            _pending.Clear();
        }

        if (pending.Count == 0)
        {
            return;
        }

        foreach (var (folder, file) in pending)
        {
            _played.For(folder).Add(file);
        }

        SavePlayed();
        Recount();
        Raise(Changed);
    }

    /// <summary>Counts the unplayed tracks on disk, the folders holding them and their size.</summary>
    private void Recount()
    {
        var tracks = 0;
        var folders = 0;
        long bytes = 0;

        foreach (var name in Situations.All)
        {
            var played = _played.Peek(name);
            var ready = _disk.Files(Path.Combine(_folder, name))
                .Where(file => ZipDirectory.IsTrackName(file.Name) && !played.Contains(file.Name))
                .ToList();

            if (ready.Count == 0)
            {
                continue;
            }

            tracks += ready.Count;
            folders++;
            bytes += ready.Sum(file => file.Bytes);
        }

        lock (_gate)
        {
            _ready = (tracks, folders, bytes);
        }
    }

    private PlayedState ReadPlayed()
    {
        var text = _disk.ReadText(Path.Combine(_folder, PlayedFile));

        if (string.IsNullOrWhiteSpace(text))
        {
            return new PlayedState();
        }

        try
        {
            return JsonSerializer.Deserialize<PlayedState>(text, Json) ?? new PlayedState();
        }
        catch (JsonException exception)
        {
            _logger.LogInformation(exception, "The music library's played list does not parse");
            return new PlayedState();
        }
    }

    private void SavePlayed() =>
        _disk.WriteText(Path.Combine(_folder, PlayedFile), JsonSerializer.Serialize(_played, Json));

    private void Fail(Entry entry, string why)
    {
        lock (_gate)
        {
            _failed.Add(entry.Name);
            _failure = (entry.Name, $"{entry.Name} failed: {why.TrimEnd('.')}. It is tried again later.");
        }

        Raise(Changed);
    }

    private void ClearFailure(string folder)
    {
        lock (_gate)
        {
            if (_failure?.Folder == folder)
            {
                _failure = null;
            }
        }
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
        }

        if (_ready.Tracks > 0)
        {
            parts.Add($"{Count(_ready.Tracks, "track")} ready in {Count(_ready.Folders, "folder")} ({Size(_ready.Bytes)}).");
        }
        else if (_manifest.Count > 0)
        {
            parts.Add("No tracks are ready yet.");
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

        if (_failure is { } failure)
        {
            parts.Add(failure.Text);
        }

        return parts.Count == 0 ? null : string.Join(' ', parts);
    }

    private static string Count(int count, string noun) => $"{count} {noun}{(count == 1 ? "" : "s")}";

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

    /// <summary>A client with no overall timeout. A stalled read is bounded instead.</summary>
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

    /// <summary>What <see cref="PlayedFile"/> holds, by folder. Used only on the run's thread and in the constructor.</summary>
    private sealed class PlayedState
    {
        private static readonly HashSet<string> Empty = [];

        [JsonPropertyName("folders")]
        public Dictionary<string, FolderState> Folders { get; init; } = new(StringComparer.Ordinal);

        public HashSet<string> For(string folder) => State(folder).Played;

        /// <summary>The folder's played tracks, adding no entry for a folder with none.</summary>
        public IReadOnlySet<string> Peek(string folder) =>
            Folders.TryGetValue(folder, out var state) ? state.Played : Empty;

        public string? Sha256(string folder) => Folders.TryGetValue(folder, out var state) ? state.Sha256 : null;

        public void Checked(string folder, string sha256) => State(folder).Sha256 = sha256;

        private FolderState State(string folder)
        {
            if (!Folders.TryGetValue(folder, out var state))
            {
                state = new FolderState();
                Folders[folder] = state;
            }

            return state;
        }
    }

    private sealed class FolderState
    {
        [JsonPropertyName("sha256")]
        public string? Sha256 { get; set; }

        [JsonPropertyName("played")]
        public HashSet<string> Played { get; init; } = new(StringComparer.Ordinal);
    }
}

/// <summary>The file operations <see cref="MusicPackStore"/> uses. Paths are full paths.</summary>
internal interface IMusicPackDisk
{
    /// <summary>Bytes free on the drive holding the folder, or null when it cannot be read.</summary>
    long? FreeBytes(string folder);

    /// <summary>Null when the file is missing.</summary>
    string? ReadText(string path);

    /// <summary>Creates the folder, and replaces the file whole.</summary>
    void WriteText(string path, string contents);

    /// <summary>A readable, writable, seekable stream at the path, removed when it is disposed.</summary>
    Stream CreateTemp(string path);

    /// <summary>The files directly in the folder, with their sizes; empty when it is missing.</summary>
    IReadOnlyList<(string Name, long Bytes)> Files(string folder);

    Stream OpenRead(string path);

    /// <summary>A new file at the path, replacing any, creating the folder.</summary>
    Stream Create(string path);

    void MoveFile(string from, string to);

    /// <summary>Does nothing when the file is missing.</summary>
    void DeleteFile(string path);

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
        var temp = path + ".tmp";
        File.WriteAllText(temp, contents);
        File.Move(temp, path, overwrite: true);
    }

    public Stream CreateTemp(string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);

        return new FileStream(
            path, FileMode.Create, FileAccess.ReadWrite, FileShare.None, 81920, FileOptions.DeleteOnClose | FileOptions.Asynchronous);
    }

    public IReadOnlyList<(string Name, long Bytes)> Files(string folder)
    {
        try
        {
            return new DirectoryInfo(folder).EnumerateFiles().Select(file => (file.Name, file.Length)).ToList();
        }
        catch (DirectoryNotFoundException)
        {
            return [];
        }
    }

    public Stream OpenRead(string path) =>
        new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete, 81920);

    public Stream Create(string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);

        return new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None, 81920, FileOptions.Asynchronous);
    }

    public void MoveFile(string from, string to) => File.Move(from, to, overwrite: true);

    public void DeleteFile(string path) => File.Delete(path);

    public void DeleteFolder(string path)
    {
        if (Directory.Exists(path))
        {
            Directory.Delete(path, recursive: true);
        }
    }
}
