using System.Globalization;
using System.IO.Compression;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using D47.App.Media;
using D47.Core.Audio;
using D47.Core.Storage;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.App.Tests;

/// <summary>Deleting the queued music tracks replaces the folder's zip and manifest on the release before anything here goes (#2034).</summary>
public sealed class TheThumbsDownTracksLeaveGitHubBeforeThisPcTests
{
    private const string Pack = @"C:\d47\data\music-pack";
    private const string Rejects = @"C:\d47\data\music-rejects.tsv";
    private const string Source = @"D:\music";
    private const string Token = "github_pat_test";

    private readonly MemoryFileSystem _files = new();
    private readonly MusicRejects _rejects;
    private readonly FakeRelease _github = new();
    private string? _source;

    public TheThumbsDownTracksLeaveGitHubBeforeThisPcTests()
    {
        _rejects = new MusicRejects(Rejects, _files);

        Folder(Situations.Docked, 3, "docked-001.mp3", "docked-002.mp3", "docked-003.mp3");
        Folder(Situations.Supercruise, 1, "supercruise-001.mp3");
        _github.Publish();
    }

    [Fact]
    public async Task TwoQueuedTracksInOneFolderGiveOneZipWithoutThemAndOnlyThatFolderChanges()
    {
        _rejects.Add(Situations.Docked, "docked-001.mp3");
        _rejects.Add(Situations.Docked, "docked-002.mp3");
        var before = _github.Manifest();

        var (reply, said) = await Run();

        Assert.Equal("Deleting 2 tracks in 1 folder from the music library.", reply);
        Assert.StartsWith("Deleted 2 tracks: 2 from docked.", said, StringComparison.Ordinal);
        Assert.Contains("generator's copies were left", said, StringComparison.Ordinal);

        Assert.Equal(["docked.next.zip", "manifest.next.json"], _github.Uploaded);

        var zip = _github.Asset("docked.zip");
        Assert.Equal(["docked-003.mp3"], Entries(zip));

        var after = _github.Manifest();
        var docked = Entry(after, Situations.Docked);
        Assert.Equal(4, (int)docked["version"]!);
        Assert.Equal(zip.LongLength, (long)docked["bytes"]!);
        Assert.Equal(Convert.ToHexStringLower(SHA256.HashData(zip)), (string)docked["sha256"]!);
        Assert.Equal(1, (int)docked["tracks"]!);
        Assert.True(JsonNode.DeepEquals(Entry(before, Situations.Supercruise), Entry(after, Situations.Supercruise)));
        Assert.Equal(["docked.zip", "manifest.json", "supercruise.zip"], _github.Names);

        Assert.Equal("4", _files.ReadText(Path.Combine(Pack, Situations.Docked, MusicPackStore.VersionFile)));
        Assert.True(JsonNode.DeepEquals(after, JsonNode.Parse(_files.ReadText(Path.Combine(Pack, MusicPackStore.ManifestFile))!)));
        Assert.Equal([Path.Combine(Pack, Situations.Docked, "docked-003.mp3")], _files.Enumerate(Path.Combine(Pack, Situations.Docked), "*.mp3"));
        Assert.Empty(_rejects.Rows);

        Assert.All(_github.Requests, request =>
        {
            Assert.Equal("Bearer " + Token, request.Authorization);
            Assert.DoesNotContain(Token, request.Url, StringComparison.Ordinal);
        });
    }

    [Fact]
    public async Task AFailedUploadLeavesTheQueueRowsAndDeletesNothing()
    {
        _rejects.Add(Situations.Docked, "docked-001.mp3");
        _rejects.Add(Situations.Docked, "docked-002.mp3");
        var zip = _github.Asset("docked.zip");
        var manifest = _github.Manifest();
        _github.FailUploads = true;

        var (_, said) = await Run();

        Assert.Contains("docked was not changed", said, StringComparison.Ordinal);
        Assert.Contains("Server Error", said, StringComparison.Ordinal);
        Assert.Equal(2, _rejects.Rows.Count);
        Assert.Equal(3, _files.Enumerate(Path.Combine(Pack, Situations.Docked), "*.mp3").Count);
        Assert.Equal("3", _files.ReadText(Path.Combine(Pack, Situations.Docked, MusicPackStore.VersionFile)));
        Assert.Equal(zip, _github.Asset("docked.zip"));
        Assert.True(JsonNode.DeepEquals(manifest, _github.Manifest()));
    }

    [Fact]
    public async Task AFolderBehindTheManifestVersionIsRefused()
    {
        _files.WriteText(Path.Combine(Pack, Situations.Docked, MusicPackStore.VersionFile), "2");
        _rejects.Add(Situations.Docked, "docked-001.mp3");

        var (_, said) = await Run();

        Assert.Contains("docked is behind the published library", said, StringComparison.Ordinal);
        Assert.Contains("Let the download finish first.", said, StringComparison.Ordinal);
        Assert.Empty(_github.Uploaded);
        Assert.Single(_rejects.Rows);
        Assert.Equal(3, _files.Enumerate(Path.Combine(Pack, Situations.Docked), "*.mp3").Count);
    }

    [Fact]
    public async Task WithTheGeneratorFolderSetItsCopyIsDeletedAndTheIdRecorded()
    {
        _source = Source;
        _files.WriteBytes(Path.Combine(Source, "tracks", Situations.Docked, "docked-002.mp3"), [9]);
        _files.WriteBytes(Path.Combine(Source, "tracks", Situations.Docked, "docked-003.mp3"), [9]);
        _files.WriteText(Path.Combine(Source, MusicRejectDeleter.RejectedFile), "docked-002\n");
        _rejects.Add(Situations.Docked, "docked-002.mp3");

        var (_, said) = await Run();

        Assert.Equal("Deleted 1 track: 1 from docked.", said);
        Assert.Null(_files.ReadText(Path.Combine(Source, "tracks", Situations.Docked, "docked-002.mp3")));
        Assert.NotNull(_files.ReadText(Path.Combine(Source, "tracks", Situations.Docked, "docked-003.mp3")));
        Assert.Equal("docked-002\ndocked-002\n", _files.ReadText(Path.Combine(Source, MusicRejectDeleter.RejectedFile)));
    }

    [Fact]
    public async Task AFolderWhoseEveryTrackIsQueuedIsLeft()
    {
        _rejects.Add(Situations.Supercruise, "supercruise-001.mp3");

        var (_, said) = await Run();

        Assert.Contains("Every track in supercruise is queued", said, StringComparison.Ordinal);
        Assert.Empty(_github.Uploaded);
        Assert.Single(_rejects.Rows);
        Assert.Single(_files.Enumerate(Path.Combine(Pack, Situations.Supercruise), "*.mp3"));
    }

    [Fact]
    public async Task AManifestLeftUnderItsSecondNameIsRenamedBeforeTheRun()
    {
        _github.Rename("manifest.json", "manifest.next.json");
        _rejects.Add(Situations.Docked, "docked-001.mp3");

        var (_, said) = await Run();

        Assert.StartsWith("Deleted 1 track: 1 from docked.", said, StringComparison.Ordinal);
        Assert.Equal(["docked.zip", "manifest.json", "supercruise.zip"], _github.Names);
        Assert.Equal(4, (int)Entry(_github.Manifest(), Situations.Docked)["version"]!);
    }

    [Fact]
    public async Task TwoFoldersInOneRunBothLeaveGitHubWithTheManifestIntact()
    {
        _rejects.Add(Situations.Docked, "docked-001.mp3");
        _rejects.Add(Situations.Docked, "docked-002.mp3");
        Folder(Situations.NormalSpace, 1, "normal-space-001.mp3", "normal-space-002.mp3");
        _github.Publish();
        _rejects.Add(Situations.NormalSpace, "normal-space-001.mp3");

        var (_, said) = await Run();

        Assert.StartsWith("Deleted 3 tracks: 2 from docked, 1 from normal-space.", said, StringComparison.Ordinal);
        Assert.Contains("manifest.json", _github.Names);
        Assert.DoesNotContain(_github.Names, name => name.Contains(".next", StringComparison.Ordinal));
        Assert.Equal(4, (int)Entry(_github.Manifest(), Situations.Docked)["version"]!);
        Assert.Equal(2, (int)Entry(_github.Manifest(), Situations.NormalSpace)["version"]!);
    }

    [Fact]
    public async Task AManifestRenameThatFailsIsRepairedBeforeTheRunReplies()
    {
        _rejects.Add(Situations.Docked, "docked-001.mp3");
        _github.FailRenameTo = "manifest.json";

        var (_, said) = await Run();

        Assert.Contains("docked was not changed. Replacing the manifest failed: Server Error", said, StringComparison.Ordinal);
        Assert.Equal(["docked.zip", "manifest.json", "supercruise.zip"], _github.Names);
    }

    [Fact]
    public async Task AGeneratorFolderWithNoTracksFolderIsLeftAlone()
    {
        _source = @"D:\nowhere";
        _rejects.Add(Situations.Docked, "docked-001.mp3");

        var (_, said) = await Run();

        Assert.Contains("has no tracks folder", said, StringComparison.Ordinal);
        Assert.Null(_files.ReadText(Path.Combine(@"D:\nowhere", MusicRejectDeleter.RejectedFile)));
    }

    [Fact]
    public void NothingQueuedStartsNothing()
    {
        var (reply, run) = Deleter().Start();

        Assert.Equal("No tracks are queued for deletion.", reply);
        Assert.Null(run);
        Assert.Empty(_github.Requests);
    }

    private async Task<(string Reply, string Said)> Run()
    {
        var (reply, run) = Deleter().Start();
        Assert.NotNull(run);

        return (reply, await run);
    }

    private MusicRejectDeleter Deleter() => new(
        Pack,
        _rejects,
        _files,
        () => Token,
        name => name == MusicRejectDeleter.SourceVariable ? _source : null,
        () => new MemoryStream(),
        NullLogger.Instance,
        _github);

    /// <summary>A downloaded folder at a version, with its zip and manifest entry on the fake release.</summary>
    private void Folder(string name, int version, params string[] tracks)
    {
        var folder = Path.Combine(Pack, name);
        _files.WriteText(Path.Combine(folder, MusicPackStore.VersionFile), version.ToString(CultureInfo.InvariantCulture));

        foreach (var track in tracks)
        {
            _files.WriteBytes(Path.Combine(folder, track), Encoding.UTF8.GetBytes(track));
        }

        _github.Add(name, version, tracks);
    }

    private static List<string> Entries(byte[] zip)
    {
        using var archive = new ZipArchive(new MemoryStream(zip), ZipArchiveMode.Read);

        return [.. archive.Entries.Select(entry => entry.FullName)];
    }

    private static JsonObject Entry(JsonNode manifest, string folder) =>
        manifest["folders"]!.AsArray().OfType<JsonObject>().Single(entry => (string)entry["name"]! == folder);

    private sealed record Sent(string Method, string Url, string? Authorization);

    /// <summary>The <c>music-1</c> release's endpoints, in memory.</summary>
    private sealed class FakeRelease : HttpMessageHandler
    {
        private const string Api = "/repos/" + MusicRejectDeleter.Repository;
        private const long ReleaseId = 7;

        private readonly Lock _gate = new();
        private readonly List<(long Id, string Name, byte[] Bytes)> _assets = [];
        private readonly List<object> _entries = [];
        private long _next = 100;

        public List<Sent> Requests { get; } = [];

        public List<string> Uploaded { get; } = [];

        public bool FailUploads { get; set; }

        /// <summary>The next rename to this name answers 500, once.</summary>
        public string? FailRenameTo { get; set; }

        public IEnumerable<string> Names => _assets.Select(asset => asset.Name).Order(StringComparer.Ordinal);

        public void Add(string name, int version, string[] tracks)
        {
            using var memory = new MemoryStream();

            using (var archive = new ZipArchive(memory, ZipArchiveMode.Create, leaveOpen: true))
            {
                foreach (var track in tracks)
                {
                    using var entry = archive.CreateEntry(track).Open();
                    entry.Write(Encoding.UTF8.GetBytes(track));
                }
            }

            var zip = memory.ToArray();
            _assets.Add((_next++, name + ".zip", zip));
            _entries.Add(new
            {
                name,
                version,
                zip = name + ".zip",
                bytes = zip.LongLength,
                sha256 = Convert.ToHexStringLower(SHA256.HashData(zip)),
                tracks = tracks.Length,
            });
        }

        /// <summary>Writes the manifest with a BOM, as Windows PowerShell's <c>Set-Content -Encoding utf8</c> does.</summary>
        public void Publish()
        {
            _assets.RemoveAll(asset => asset.Name == "manifest.json");
            _assets.Add((_next++, "manifest.json", [.. Encoding.UTF8.GetPreamble(), .. JsonSerializer.SerializeToUtf8Bytes(new { folders = _entries })]));
        }

        public void Rename(string from, string to)
        {
            var index = _assets.FindIndex(asset => asset.Name == from);
            _assets[index] = _assets[index] with { Name = to };
        }

        public byte[] Asset(string name) => _assets.Single(asset => asset.Name == name).Bytes;

        public JsonNode Manifest() => JsonNode.Parse(Encoding.UTF8.GetString(Asset("manifest.json")).TrimStart('\uFEFF'))!;

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var body = request.Content is null ? [] : await request.Content.ReadAsByteArrayAsync(cancellationToken);
            var uri = request.RequestUri!;

            lock (_gate)
            {
                Requests.Add(new Sent(request.Method.Method, uri.ToString(), request.Headers.Authorization?.ToString()));

                return Answer(request.Method, uri, body);
            }
        }

        private HttpResponseMessage Answer(HttpMethod method, Uri uri, byte[] body)
        {
            var path = uri.AbsolutePath;

            if (method == HttpMethod.Get && path == $"{Api}/releases/tags/{MusicRejectDeleter.Tag}")
            {
                return Json(HttpStatusCode.OK, JsonSerializer.Serialize(new { id = ReleaseId }));
            }

            if (method == HttpMethod.Get && path == $"{Api}/releases/{ReleaseId}/assets")
            {
                var page = int.Parse(Query(uri, "page"), CultureInfo.InvariantCulture);
                var size = int.Parse(Query(uri, "per_page"), CultureInfo.InvariantCulture);

                return Json(HttpStatusCode.OK, JsonSerializer.Serialize(
                    _assets.Skip((page - 1) * size).Take(size).Select(asset => new { id = asset.Id, name = asset.Name })));
            }

            if (method == HttpMethod.Get && path.StartsWith($"{Api}/releases/assets/", StringComparison.Ordinal))
            {
                var asset = _assets.Single(held => held.Id == IdOf(path));

                return new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(asset.Bytes) };
            }

            if (method == HttpMethod.Post && uri.Host == "uploads.github.com" && path == $"{Api}/releases/{ReleaseId}/assets")
            {
                if (FailUploads)
                {
                    return Json(HttpStatusCode.InternalServerError, "{\"message\":\"Server Error\"}");
                }

                var name = Query(uri, "name");

                if (_assets.Any(asset => asset.Name == name))
                {
                    return Json(HttpStatusCode.UnprocessableEntity, "{\"message\":\"Validation Failed\"}");
                }

                Uploaded.Add(name);
                var id = _next++;
                _assets.Add((id, name, body));

                return Json(HttpStatusCode.Created, JsonSerializer.Serialize(new { id }));
            }

            if (method == HttpMethod.Delete && path.StartsWith($"{Api}/releases/assets/", StringComparison.Ordinal))
            {
                if (_assets.RemoveAll(asset => asset.Id == IdOf(path)) == 0)
                {
                    return Json(HttpStatusCode.NotFound, "{\"message\":\"Not Found\"}");
                }

                return new HttpResponseMessage(HttpStatusCode.NoContent);
            }

            if (method == HttpMethod.Patch && path.StartsWith($"{Api}/releases/assets/", StringComparison.Ordinal))
            {
                var id = IdOf(path);
                var name = JsonDocument.Parse(body).RootElement.GetProperty("name").GetString()!;

                if (FailRenameTo == name)
                {
                    FailRenameTo = null;

                    return Json(HttpStatusCode.InternalServerError, "{\"message\":\"Server Error\"}");
                }

                if (_assets.Any(asset => asset.Name == name && asset.Id != id))
                {
                    return Json(HttpStatusCode.UnprocessableEntity, "{\"message\":\"Validation Failed\"}");
                }

                var index = _assets.FindIndex(asset => asset.Id == id);
                _assets[index] = _assets[index] with { Name = name };

                return Json(HttpStatusCode.OK, JsonSerializer.Serialize(new { id }));
            }

            return Json(HttpStatusCode.NotFound, "{\"message\":\"Not Found\"}");
        }

        private static long IdOf(string path) => long.Parse(path.Split('/')[^1], CultureInfo.InvariantCulture);

        private static string Query(Uri uri, string name) =>
            Uri.UnescapeDataString(uri.Query.TrimStart('?').Split('&').Select(part => part.Split('=')).First(pair => pair[0] == name)[1]);

        private static HttpResponseMessage Json(HttpStatusCode status, string body) =>
            new(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };
    }
}
