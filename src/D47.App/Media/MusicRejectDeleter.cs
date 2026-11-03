using System.Globalization;
using System.IO.Compression;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using D47.App.Panel;
using D47.Core.Audio;
using D47.Core.Storage;
using Microsoft.Extensions.Logging;

namespace D47.App.Media;

/// <summary>
/// Deletes the tracks queued in <see cref="MusicRejects"/> from the <c>music-1</c> release and from this PC, one
/// folder at a time, with the maintainer's token. GitHub has a folder's new zip and manifest before anything of that
/// folder is deleted here. One run at a time, on the pool.
/// </summary>
internal sealed class MusicRejectDeleter
{
    internal const string Repository = "dseelinger/d47-music";

    internal const string Tag = "music-1";

    /// <summary>The generator's data folder, holding <c>tracks\&lt;folder&gt;\</c> and <c>rejected.tsv</c>.</summary>
    internal const string SourceVariable = "D47_MUSIC_SOURCE";

    internal const string RejectedFile = "rejected.tsv";

    private const string Api = "https://api.github.com/repos/" + Repository;

    private const string Uploads = "https://uploads.github.com/repos/" + Repository;

    private const string ManifestName = MusicPackStore.ManifestFile;

    private const string NextManifestName = "manifest.next.json";

    private const int PageSize = 100;

    /// <summary>The longest an API call may take.</summary>
    private static readonly TimeSpan Answer = TimeSpan.FromMinutes(2);

    /// <summary>The longest a zip upload may take.</summary>
    private static readonly TimeSpan Upload = TimeSpan.FromMinutes(60);

    /// <summary>Every entry's timestamp, as <c>tools/publish-music.ps1</c> writes it, so the same tracks give the same hash.</summary>
    private static readonly DateTimeOffset EntryStamp = new(2000, 1, 1, 0, 0, 0, TimeSpan.Zero);

    private static readonly JsonSerializerOptions Indented = new() { WriteIndented = true };

    private readonly string _pack;
    private readonly MusicRejects _rejects;
    private readonly IFileSystem _files;
    private readonly Func<string?> _token;
    private readonly Func<string, string?> _environment;
    private readonly Func<Stream> _temp;
    private readonly ILogger _logger;
    private readonly HttpClient _http;
    private int _running;

    /// <param name="token">Read once at each run and never kept.</param>
    /// <param name="temp">A readable, writable, seekable stream for one zip, removed when it is disposed.</param>
    internal MusicRejectDeleter(
        string pack,
        MusicRejects rejects,
        IFileSystem files,
        Func<string?> token,
        Func<string, string?> environment,
        Func<Stream> temp,
        ILogger logger,
        HttpMessageHandler? handler = null)
    {
        _pack = pack;
        _rejects = rejects;
        _files = files;
        _token = token;
        _environment = environment;
        _temp = temp;
        _logger = logger;
        _http = handler is null ? new HttpClient() : new HttpClient(handler);
        _http.Timeout = Timeout.InfiniteTimeSpan;
    }

    /// <summary>
    /// Starts deleting every queued track and returns the reply to say now, with the run, whose result is the reply
    /// to say when it ends. The run is null when nothing was started.
    /// </summary>
    internal (string Reply, Task<string>? Run) Start()
    {
        var queued = Queued();

        if (queued.Count == 0)
        {
            return ("No tracks are queued for deletion.", null);
        }

        if (_token() is not { } token)
        {
            return ("No music publish token is set.", null);
        }

        if (Interlocked.CompareExchange(ref _running, 1, 0) != 0)
        {
            return ("The thumbs-down tracks are already being deleted.", null);
        }

        var tracks = queued.Sum(folder => folder.Files.Count);
        var run = Task.Run(async () =>
        {
            try
            {
                return await RunAsync(queued, token).ConfigureAwait(false);
            }
            finally
            {
                Volatile.Write(ref _running, 0);
            }
        });

        return ($"Deleting {Tracks(tracks)} in {Count(queued.Count, "folder")} from the music library.", run);
    }

    /// <summary>The queued rows that name a library folder and a bare MP3 file name, by folder in library order.</summary>
    private List<Queue> Queued()
    {
        var rows = _rejects.Rows;

        foreach (var (folder, file) in rows.Where(row => !Valid(row.Folder, row.File)))
        {
            _logger.LogInformation("A music reject row names no library track: {Folder} {File}", folder, file);
        }

        return Situations.All
            .Select(folder => new Queue(
                folder,
                rows.Where(row => Valid(row.Folder, row.File) && string.Equals(row.Folder, folder, StringComparison.OrdinalIgnoreCase))
                    .Select(row => row.File)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList()))
            .Where(queue => queue.Files.Count > 0)
            .ToList();
    }

    private static bool Valid(string folder, string file) =>
        Situations.All.Contains(folder, StringComparer.OrdinalIgnoreCase)
        && file.EndsWith(".mp3", StringComparison.OrdinalIgnoreCase)
        && file.Length > ".mp3".Length
        && file == Path.GetFileName(file)
        && file.IndexOfAny(Path.GetInvalidFileNameChars()) < 0
        && file.Trim() == file
        && !file.StartsWith('.');

    private async Task<string> RunAsync(List<Queue> queued, string token)
    {
        var source = _environment(SourceVariable) is { Length: > 0 } set ? set.Trim() : null;
        var report = new Report(
            source is not null && _files.FolderWritten(Path.Combine(source, "tracks")) is not null ? source : null,
            source is not null);

        Release? release = null;
        var stopped = false;

        try
        {
            JsonObject manifest;

            try
            {
                release = await ReleaseAsync(token).ConfigureAwait(false);
                await FinishSwapsAsync(release, token).ConfigureAwait(false);
                manifest = await ManifestAsync(release, token).ConfigureAwait(false);
            }
            catch (DeleteFailure failure)
            {
                _logger.LogInformation("Deleting the music rejects stopped before any folder: {Failure}", failure.Message);
                return "Nothing was deleted. " + failure.Message;
            }

            for (var i = 0; i < queued.Count; i++)
            {
                if (!await FolderAsync(queued[i], release, manifest, token, report).ConfigureAwait(false))
                {
                    report.Untried.AddRange(queued.Skip(i + 1).Select(queue => queue.Folder));
                    stopped = true;
                    break;
                }
            }
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "Deleting the music rejects stopped");
            report.Failed.Add($"It stopped on an error ({exception.GetType().Name}).");
            stopped = true;
        }

        if (stopped && release is { Changed: true })
        {
            await RepairAsync(release, token, report).ConfigureAwait(false);
        }

        var said = report.Describe();
        _logger.LogInformation("Deleting the music rejects ended: {Said}", said);

        return said;
    }

    /// <summary>Lists the release again and renames any second-name asset left without its real name.</summary>
    private async Task RepairAsync(Release stale, string token, Report report)
    {
        try
        {
            var release = await ReleaseAsync(token).ConfigureAwait(false);
            await FinishSwapsAsync(release, token).ConfigureAwait(false);
        }
        catch (DeleteFailure failure)
        {
            _logger.LogInformation("Repairing the release after a stopped run failed: {Failure}", failure.Message);
            report.Failed.Add("Repairing the release afterwards failed. " + failure.Message);
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "Repairing the release after a stopped run failed");
            report.Failed.Add($"Repairing the release afterwards stopped on an error ({exception.GetType().Name}).");
        }
    }

    /// <summary>One folder's six steps. False when GitHub refused or failed, which stops the run.</summary>
    private async Task<bool> FolderAsync(Queue queue, Release release, JsonObject manifest, string token, Report report)
    {
        var folder = queue.Folder;
        var here = Path.Combine(_pack, folder);

        if (Entry(manifest, folder) is not { } entry || Number(entry["version"]) is not { } published)
        {
            report.Failed.Add($"{folder} is not in the published library, so its tracks stay queued.");
            return true;
        }

        if (LocalVersion(here) is not { } local || local < published)
        {
            report.Failed.Add($"{folder} is behind the published library, so it was left. Let the download finish first.");
            return true;
        }

        var present = _files.Enumerate(here, "*.mp3");
        var queuedNames = new HashSet<string>(queue.Files, StringComparer.OrdinalIgnoreCase);

        if (present.Any(path => queuedNames.Contains(Path.GetFileName(path))))
        {
            var keep = present.Where(path => !queuedNames.Contains(Path.GetFileName(path))).ToList();
            string written;

            if (keep.Count == 0)
            {
                report.Failed.Add($"Every track in {folder} is queued, and a folder cannot be emptied, so it was left.");
                return true;
            }

            try
            {
                written = await ReplaceAsync(folder, keep, published + 1, release, manifest, entry, token).ConfigureAwait(false);
            }
            catch (DeleteFailure failure)
            {
                _logger.LogInformation("Music folder {Folder} was not replaced: {Failure}", folder, failure.Message);
                report.Failed.Add($"{folder} was not changed. {failure.Message}");
                return false;
            }

            try
            {
                _files.WriteText(Path.Combine(here, MusicPackStore.VersionFile), (published + 1).ToString(CultureInfo.InvariantCulture));
                _files.WriteText(Path.Combine(_pack, ManifestName), written);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                _logger.LogInformation(exception, "Could not record version {Version} of {Folder}", published + 1, folder);
                report.Failed.Add($"GitHub has the new {folder}, but this PC could not record its version, so the download fetches it again.");
            }
        }

        var gone = new List<(string Folder, string File)>();

        foreach (var file in queue.Files)
        {
            try
            {
                _files.Delete(Path.Combine(here, file));
                gone.Add((folder, file));
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                _logger.LogInformation(exception, "Could not delete {File} from {Folder}", file, folder);
                report.Failed.Add($"{Name(file)} could not be deleted from this PC and stays queued.");
            }
        }

        if (report.Source is { } source)
        {
            foreach (var (_, file) in gone)
            {
                Retire(source, folder, file, report);
            }
        }

        _rejects.Remove(gone);
        report.Deleted.Add((folder, gone.Count));

        return true;
    }

    /// <summary>Deletes the generator's copy and appends its id to <see cref="RejectedFile"/>.</summary>
    private void Retire(string source, string folder, string file, Report report)
    {
        try
        {
            _files.Delete(Path.Combine(source, "tracks", folder, file));
            _files.AppendText(Path.Combine(source, RejectedFile), Name(file) + "\n");
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            _logger.LogInformation(exception, "Could not retire the generator's copy of {File}", file);
            report.Failed.Add($"The generator's copy of {Name(file)} could not be deleted.");
        }
    }

    /// <summary>
    /// Builds the folder's zip from <paramref name="keep"/>, replaces it on the release, then replaces the manifest
    /// with the folder's entry updated. Returns the manifest written.
    /// </summary>
    private async Task<string> ReplaceAsync(
        string folder, List<string> keep, int version, Release release, JsonObject manifest, JsonObject entry, string token)
    {
        var zipName = folder + ".zip";
        var nextZipName = folder + ".next.zip";

        await using var zip = _temp();

        Write(zip, keep);

        var bytes = zip.Length;
        zip.Position = 0;
        var sha256 = Convert.ToHexStringLower(await SHA256.HashDataAsync(zip).ConfigureAwait(false));
        zip.Position = 0;

        var step = $"Uploading {zipName}";
        await DeleteLeftoverAsync(release, nextZipName, step, token).ConfigureAwait(false);

        var upload = new StreamContent(zip);
        upload.Headers.ContentType = new MediaTypeHeaderValue("application/zip");
        upload.Headers.ContentLength = bytes;

        var uploaded = await UploadAsync(release, nextZipName, upload, step, Upload, token).ConfigureAwait(false);
        await SwapAsync(release, zipName, uploaded, step, token).ConfigureAwait(false);

        entry["version"] = version;
        entry["bytes"] = bytes;
        entry["sha256"] = sha256;
        entry["tracks"] = keep.Count;

        var written = manifest.ToJsonString(Indented);

        step = "Replacing the manifest";
        await DeleteLeftoverAsync(release, NextManifestName, step, token).ConfigureAwait(false);

        var text = new ByteArrayContent(Encoding.UTF8.GetBytes(written));
        text.Headers.ContentType = new MediaTypeHeaderValue("application/json");

        var manifestId = await UploadAsync(release, NextManifestName, text, step, Answer, token).ConfigureAwait(false);
        await SwapAsync(release, ManifestName, manifestId, step, token).ConfigureAwait(false);

        _logger.LogInformation("Music folder {Folder} is version {Version} with {Tracks} tracks", folder, version, keep.Count);

        return written;
    }

    /// <summary>The tracks at the zip's root, sorted, stored, with one timestamp, as <c>tools/publish-music.ps1</c> writes them.</summary>
    private void Write(Stream into, List<string> tracks)
    {
        using var archive = new ZipArchive(into, ZipArchiveMode.Create, leaveOpen: true);

        foreach (var track in tracks.OrderBy(Path.GetFileName, StringComparer.OrdinalIgnoreCase))
        {
            var entry = archive.CreateEntry(Path.GetFileName(track), CompressionLevel.NoCompression);
            entry.LastWriteTime = EntryStamp;

            using var from = _files.OpenRead(track) ?? throw new FileNotFoundException("A track went missing while its folder was zipped.", track);
            using var to = entry.Open();
            from.CopyTo(to);
        }
    }

    private int? LocalVersion(string folder) =>
        _files.ReadText(Path.Combine(folder, MusicPackStore.VersionFile)) is { } text
        && int.TryParse(text.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var version)
            ? version
            : null;

    private static JsonObject? Entry(JsonObject manifest, string folder) =>
        (manifest["folders"] as JsonArray)?
        .OfType<JsonObject>()
        .FirstOrDefault(entry => entry["name"] is JsonValue name
                                 && name.TryGetValue<string>(out var text)
                                 && string.Equals(text, folder, StringComparison.Ordinal));

    private static int? Number(JsonNode? node) =>
        node is JsonValue value && value.TryGetValue<int>(out var number) ? number : null;

    private async Task<Release> ReleaseAsync(string token)
    {
        const string step = "Finding the " + Tag + " release";

        var (status, body) = await SendAsync(HttpMethod.Get, $"{Api}/releases/tags/{Tag}", step, token, Answer).ConfigureAwait(false);

        if (status == HttpStatusCode.NotFound)
        {
            throw new DeleteFailure($"{Repository} has no {Tag} release.");
        }

        var id = ReadId(body, step);
        var assets = new Dictionary<string, long>(StringComparer.Ordinal);

        for (var page = 1; ; page++)
        {
            var (_, listed) = await SendAsync(
                    HttpMethod.Get,
                    string.Create(CultureInfo.InvariantCulture, $"{Api}/releases/{id}/assets?per_page={PageSize}&page={page}"),
                    step,
                    token,
                    Answer)
                .ConfigureAwait(false);

            var count = 0;

            foreach (var (assetId, name) in ReadAssets(listed, step))
            {
                assets[name] = assetId;
                count++;
            }

            if (count < PageSize)
            {
                return new Release(id, assets);
            }
        }
    }

    /// <summary>The published manifest, read through the API rather than from the copy on this PC.</summary>
    private async Task<JsonObject> ManifestAsync(Release release, string token)
    {
        const string step = "Reading the manifest";

        if (!release.Assets.TryGetValue(ManifestName, out var id))
        {
            throw new DeleteFailure($"{Tag} has no {ManifestName}.");
        }

        var (_, body) = await SendAsync(
                HttpMethod.Get, $"{Api}/releases/assets/{id}", step, token, Answer, accept: "application/octet-stream")
            .ConfigureAwait(false);

        try
        {
            if (JsonNode.Parse(Encoding.UTF8.GetString(body).TrimStart('﻿')) is JsonObject manifest
                && manifest["folders"] is JsonArray)
            {
                return manifest;
            }
        }
        catch (JsonException)
        {
        }

        throw new DeleteFailure($"The published {ManifestName} could not be read, so it was left as it is.");
    }

    /// <summary>Gives a second-name asset its real name where a run stopped after deleting the asset it replaced.</summary>
    private async Task FinishSwapsAsync(Release release, string token)
    {
        var pairs = release.Assets.Keys
            .Where(name => name == NextManifestName || name.EndsWith(".next.zip", StringComparison.Ordinal))
            .Select(next => (Next: next, Name: next == NextManifestName ? ManifestName : next[..^".next.zip".Length] + ".zip"))
            .Where(pair => !release.Assets.ContainsKey(pair.Name))
            .ToList();

        foreach (var (next, name) in pairs)
        {
            _logger.LogInformation("Renaming {Next} to {Name}, which an earlier run deleted", next, name);
            var id = release.Assets[next];
            release.Assets.Remove(next);
            await SwapAsync(release, name, id, "Repairing " + name, token).ConfigureAwait(false);
        }
    }

    private async Task DeleteLeftoverAsync(Release release, string name, string step, string token)
    {
        if (release.Assets.Remove(name, out var leftover))
        {
            await SendAsync(HttpMethod.Delete, $"{Api}/releases/assets/{leftover}", step, token, Answer).ConfigureAwait(false);
            release.Changed = true;
        }
    }

    /// <summary>Deletes the asset called <paramref name="name"/>, then gives that name to <paramref name="uploaded"/>.</summary>
    private async Task SwapAsync(Release release, string name, long uploaded, string step, string token)
    {
        if (release.Assets.Remove(name, out var old))
        {
            await SendAsync(HttpMethod.Delete, $"{Api}/releases/assets/{old}", step, token, Answer).ConfigureAwait(false);
            release.Changed = true;
        }

        var rename = JsonSerializer.Serialize(new Dictionary<string, string> { ["name"] = name });
        await SendAsync(
                HttpMethod.Patch,
                $"{Api}/releases/assets/{uploaded}",
                step,
                token,
                Answer,
                new StringContent(rename, Encoding.UTF8, "application/json"))
            .ConfigureAwait(false);

        foreach (var stale in release.Assets.Where(asset => asset.Value == uploaded).Select(asset => asset.Key).ToList())
        {
            release.Assets.Remove(stale);
        }

        release.Assets[name] = uploaded;
        release.Changed = true;
    }

    private async Task<long> UploadAsync(Release release, string name, HttpContent content, string step, TimeSpan limit, string token)
    {
        var (_, body) = await SendAsync(
                HttpMethod.Post,
                $"{Uploads}/releases/{release.Id}/assets?name={Uri.EscapeDataString(name)}",
                step,
                token,
                limit,
                content)
            .ConfigureAwait(false);

        var id = ReadId(body, step);
        release.Assets[name] = id;
        release.Changed = true;

        return id;
    }

    /// <summary>
    /// Sends one request with the token in the Authorization header only. A 404 on a GET is returned; any other
    /// failure throws with GitHub's message, never the raw body.
    /// </summary>
    private async Task<(HttpStatusCode Status, byte[] Body)> SendAsync(
        HttpMethod method,
        string url,
        string step,
        string token,
        TimeSpan limit,
        HttpContent? content = null,
        string accept = "application/vnd.github+json")
    {
        using var request = new HttpRequestMessage(method, url) { Content = content };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        request.Headers.Accept.ParseAdd(accept);
        request.Headers.UserAgent.ParseAdd("d47-music-library");
        request.Headers.Add("X-GitHub-Api-Version", "2022-11-28");

        using var cancel = new CancellationTokenSource(limit);

        try
        {
            using var response = await _http.SendAsync(request, cancel.Token).ConfigureAwait(false);
            var body = await response.Content.ReadAsByteArrayAsync(cancel.Token).ConfigureAwait(false);

            if (response.IsSuccessStatusCode || (method == HttpMethod.Get && response.StatusCode == HttpStatusCode.NotFound))
            {
                return (response.StatusCode, body);
            }

            var message = $"{step} failed: {CodexPicturePublisher.GitHubMessage(body) ?? ((int)response.StatusCode).ToString(CultureInfo.InvariantCulture)}.";

            throw new DeleteFailure(response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden
                ? $"{message} The token cannot write to {Repository}."
                : message);
        }
        catch (Exception exception) when (exception is HttpRequestException or IOException or TaskCanceledException)
        {
            throw new DeleteFailure($"{step} failed: GitHub could not be reached ({exception.GetType().Name}).");
        }
    }

    private static long ReadId(byte[] body, string step)
    {
        try
        {
            using var document = JsonDocument.Parse(body);

            if (document.RootElement.ValueKind == JsonValueKind.Object
                && document.RootElement.TryGetProperty("id", out var id)
                && id.TryGetInt64(out var value))
            {
                return value;
            }
        }
        catch (JsonException)
        {
        }

        throw new DeleteFailure($"{step} failed: GitHub's answer had no id.");
    }

    private static List<(long Id, string Name)> ReadAssets(byte[] body, string step)
    {
        var assets = new List<(long Id, string Name)>();

        try
        {
            using var document = JsonDocument.Parse(body);

            if (document.RootElement.ValueKind != JsonValueKind.Array)
            {
                throw new DeleteFailure($"{step} failed: GitHub's answer was not a list of files.");
            }

            foreach (var element in document.RootElement.EnumerateArray())
            {
                if (element.ValueKind == JsonValueKind.Object
                    && element.TryGetProperty("id", out var id)
                    && id.TryGetInt64(out var value)
                    && element.TryGetProperty("name", out var name)
                    && name.GetString() is { } text)
                {
                    assets.Add((value, text));
                }
            }
        }
        catch (JsonException)
        {
            throw new DeleteFailure($"{step} failed: GitHub's answer was not a list of files.");
        }

        return assets;
    }

    private static string Name(string file) => Path.GetFileNameWithoutExtension(file);

    private static string Count(int count, string noun) =>
        string.Create(CultureInfo.InvariantCulture, $"{count} {noun}{(count == 1 ? "" : "s")}");

    private static string Tracks(int count) => Count(count, "track");

    /// <summary>A temp file in the user's temp folder for one zip, removed when it is closed.</summary>
    internal static Stream TempZip(IMusicPackDisk disk) =>
        disk.CreateTemp(Path.Combine(Path.GetTempPath(), $"d47-music-{Guid.NewGuid():N}.zip"));

    private sealed record Queue(string Folder, IReadOnlyList<string> Files);

    private sealed record Release(long Id, Dictionary<string, long> Assets)
    {
        /// <summary>True once GitHub has accepted a change to the release's assets.</summary>
        public bool Changed { get; set; }
    }

    /// <summary>What a run did, in the words of the reply.</summary>
    /// <param name="source">The generator's data folder when it holds a tracks folder.</param>
    /// <param name="sourceSet">Whether the variable was set at all.</param>
    private sealed class Report(string? source, bool sourceSet)
    {
        public string? Source { get; } = source;

        public List<(string Folder, int Tracks)> Deleted { get; } = [];

        public List<string> Failed { get; } = [];

        public List<string> Untried { get; } = [];

        public string Describe()
        {
            var parts = new List<string>();
            var deleted = Deleted.Where(folder => folder.Tracks > 0).ToList();

            parts.Add(deleted.Count == 0
                ? "Nothing was deleted."
                : $"Deleted {Tracks(deleted.Sum(folder => folder.Tracks))}: "
                  + string.Join(", ", deleted.Select(folder => $"{folder.Tracks} from {folder.Folder}")) + ".");

            parts.AddRange(Failed);

            if (Untried.Count > 0)
            {
                parts.Add($"{string.Join(", ", Untried)} {(Untried.Count == 1 ? "was" : "were")} not tried and stay queued.");
            }

            if (deleted.Count > 0 && Source is null)
            {
                parts.Add(sourceSet
                    ? $"The generator's copies were left, because {SourceVariable} has no tracks folder."
                    : $"The generator's copies were left, because {SourceVariable} is not set.");
            }

            return string.Join(' ', parts);
        }
    }

    private sealed class DeleteFailure(string message) : Exception(message);
}
