using System.IO.Compression;
using System.Net;
using System.Security.Cryptography;
using System.Text.Json;
using D47.App.Media;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.App.Tests;

/// <summary>The music library comes down in manifest order, checked, and only while the setting is on.</summary>
public class TheMusicLibraryDownloadsOneFolderAtATimeTests
{
    private const string Root = @"C:\d47\data\music-pack";

    [Fact]
    public async Task FoldersAreFetchedInManifestOrderAndPutInPlace()
    {
        var web = new Web(("docked", 1), ("supercruise", 1), ("normal-space", 1));
        var disk = new Disk();

        await Store(web, disk).Start();

        Assert.Equal(
            ["manifest.json", "docked.zip", "supercruise.zip", "normal-space.zip"],
            web.Asked);
        Assert.Equal("1", disk.ReadText(Path.Combine(Root, "docked", MusicPackStore.VersionFile)));
        Assert.True(disk.HasFile(Path.Combine(Root, "normal-space", "normal-space-1.mp3")));
        Assert.DoesNotContain(disk.Folders, folder => folder.EndsWith(".partial", StringComparison.Ordinal));
    }

    [Fact]
    public async Task AHashMismatchLeavesNoFolderInPlace()
    {
        var web = new Web(("docked", 1)) { Corrupt = "docked" };
        var disk = new Disk();
        var store = Store(web, disk);

        await store.Start();

        Assert.False(disk.FolderExists(Path.Combine(Root, "docked")));
        Assert.DoesNotContain(disk.Folders, folder => folder.Contains("docked", StringComparison.Ordinal));
        Assert.Contains("docked failed", store.Status, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AFolderAlreadyAtTheManifestVersionIsNotFetched()
    {
        var web = new Web(("docked", 2), ("supercruise", 1));
        var disk = new Disk();
        disk.WriteText(Path.Combine(Root, "docked", MusicPackStore.VersionFile), "2");

        await Store(web, disk).Start();

        Assert.Equal(["manifest.json", "supercruise.zip"], web.Asked);
    }

    [Fact]
    public async Task ANewerVersionReplacesTheFolderOnDisk()
    {
        var web = new Web(("docked", 2));
        var disk = new Disk();
        disk.WriteText(Path.Combine(Root, "docked", MusicPackStore.VersionFile), "1");
        disk.WriteText(Path.Combine(Root, "docked", "stale.mp3"), "x");

        await Store(web, disk).Start();

        Assert.Equal("2", disk.ReadText(Path.Combine(Root, "docked", MusicPackStore.VersionFile)));
        Assert.False(disk.HasFile(Path.Combine(Root, "docked", "stale.mp3")));
        Assert.False(disk.FolderExists(Path.Combine(Root, "docked.old")));
    }

    [Fact]
    public async Task AReplacedFolderThatWillNotDeleteStillCountsAsIn()
    {
        var web = new Web(("docked", 2));
        var disk = new Disk { Undeletable = Path.Combine(Root, "docked.old") };
        disk.WriteText(Path.Combine(Root, "docked", MusicPackStore.VersionFile), "1");
        var store = Store(web, disk);
        var landed = 0;
        store.Landed += () => landed++;

        await store.Start();

        Assert.Equal(1, landed);
        Assert.Equal("2", disk.ReadText(Path.Combine(Root, "docked", MusicPackStore.VersionFile)));
        Assert.Contains("All 1 folders are in.", store.Status, StringComparison.Ordinal);
        Assert.DoesNotContain("failed", store.Status, StringComparison.Ordinal);
    }

    [Fact]
    public async Task TurningTheSettingOffStopsBeforeTheNextFolder()
    {
        var on = true;
        var web = new Web(("docked", 1), ("supercruise", 1));
        var disk = new Disk();
        var store = Store(web, disk, () => on);
        store.Landed += () => on = false;

        await store.Start();

        Assert.Equal(["manifest.json", "docked.zip"], web.Asked);
        Assert.False(disk.FolderExists(Path.Combine(Root, "supercruise")));
    }

    [Fact]
    public async Task NothingIsFetchedWhileTheSettingIsOff()
    {
        var web = new Web(("docked", 1));

        await Store(web, new Disk(), () => false).Start();

        Assert.Empty(web.Asked);
    }

    [Fact]
    public async Task ShortDiskSpaceStopsWithTheRowSayingSo()
    {
        var web = new Web(("docked", 1), ("supercruise", 1));
        var disk = new Disk { Free = 10 };
        var store = Store(web, disk);

        await store.Start();

        Assert.Equal(["manifest.json"], web.Asked);
        Assert.Contains("Stopped: docked needs", store.Status, StringComparison.Ordinal);
        Assert.Contains("0 of 2 folders are in.", store.Status, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AFailedFolderIsNotTriedAgainThisSession()
    {
        var web = new Web(("docked", 1), ("supercruise", 1)) { Corrupt = "docked" };
        var disk = new Disk();
        var store = Store(web, disk);

        await store.Start();
        await store.Start();

        Assert.Equal(["manifest.json", "docked.zip", "supercruise.zip", "manifest.json"], web.Asked);
        Assert.Contains("1 of 2 folders are in.", store.Status, StringComparison.Ordinal);
        Assert.Contains("docked failed", store.Status, StringComparison.Ordinal);
    }

    private static MusicPackStore Store(Web web, Disk disk, Func<bool>? allowed = null) =>
        new(Root, allowed ?? (() => true), disk, new HttpClient(web), NullLogger.Instance);

    /// <summary>The release, in memory: a manifest and one zip per folder.</summary>
    private sealed class Web : HttpMessageHandler
    {
        private readonly Dictionary<string, byte[]> _files = new(StringComparer.Ordinal);

        public Web(params (string Name, int Version)[] folders)
        {
            var entries = new List<object>();

            foreach (var (name, version) in folders)
            {
                var zip = Zip(name);
                _files[name + ".zip"] = zip;
                entries.Add(new
                {
                    name,
                    version,
                    zip = name + ".zip",
                    bytes = zip.LongLength,
                    sha256 = Convert.ToHexStringLower(SHA256.HashData(zip)),
                    tracks = 1,
                });
            }

            _files["manifest.json"] = JsonSerializer.SerializeToUtf8Bytes(new { folders = entries });
        }

        public string? Corrupt { get; init; }

        public List<string> Asked { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var url = request.RequestUri!.ToString();
            Assert.StartsWith(MusicPackStore.Source, url, StringComparison.Ordinal);
            var file = url[MusicPackStore.Source.Length..];

            lock (Asked)
            {
                Asked.Add(file);
            }

            if (!_files.TryGetValue(file, out var bytes))
            {
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
            }

            if (file == Corrupt + ".zip")
            {
                bytes = [.. bytes.Reverse()];
            }

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(bytes) });
        }

        private static byte[] Zip(string name)
        {
            using var memory = new MemoryStream();

            using (var zip = new ZipArchive(memory, ZipArchiveMode.Create, leaveOpen: true))
            {
                using var track = zip.CreateEntry(name + "-1.mp3").Open();
                track.Write([1, 2, 3, 4]);
            }

            return memory.ToArray();
        }
    }

    /// <summary>The music-pack folder, in memory.</summary>
    private sealed class Disk : IMusicPackDisk
    {
        private readonly Dictionary<string, byte[]> _files = new(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> _folders = new(StringComparer.OrdinalIgnoreCase);

        public long Free { get; init; } = long.MaxValue;

        public string? Undeletable { get; init; }

        public IReadOnlyCollection<string> Folders => _folders;

        public bool HasFile(string path) => _files.ContainsKey(path);

        public long? FreeBytes(string folder) => Free;

        public bool FolderExists(string path) => _folders.Contains(path);

        public string? ReadText(string path) =>
            _files.TryGetValue(path, out var bytes) ? System.Text.Encoding.UTF8.GetString(bytes) : null;

        public void WriteText(string path, string contents)
        {
            _folders.Add(Path.GetDirectoryName(path)!);
            _files[path] = System.Text.Encoding.UTF8.GetBytes(contents);
        }

        public Stream CreateTemp(string path) => new MemoryStream();

        public void Extract(Stream zip, string folder)
        {
            using var archive = new ZipArchive(zip, ZipArchiveMode.Read, leaveOpen: true);
            _folders.Add(folder);

            foreach (var entry in archive.Entries)
            {
                using var from = entry.Open();
                using var copy = new MemoryStream();
                from.CopyTo(copy);
                _files[Path.Combine(folder, entry.FullName)] = copy.ToArray();
            }
        }

        public void MoveFolder(string from, string to)
        {
            Assert.True(_folders.Remove(from), $"{from} is not there to move");
            Assert.DoesNotContain(to, _folders);
            _folders.Add(to);

            foreach (var file in _files.Keys.Where(file => file.StartsWith(from + "\\", StringComparison.OrdinalIgnoreCase)).ToList())
            {
                _files[to + file[from.Length..]] = _files[file];
                _files.Remove(file);
            }
        }

        public void DeleteFolder(string path)
        {
            if (string.Equals(path, Undeletable, StringComparison.OrdinalIgnoreCase) && _folders.Contains(path))
            {
                throw new IOException($"{path} is in use.");
            }

            _folders.Remove(path);

            foreach (var file in _files.Keys.Where(file => file.StartsWith(path + "\\", StringComparison.OrdinalIgnoreCase)).ToList())
            {
                _files.Remove(file);
            }
        }
    }
}
