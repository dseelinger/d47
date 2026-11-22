using System.IO.Compression;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using D47.App.Media;
using D47.Core.Audio;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.App.Tests;

/// <summary>
/// The music library keeps two unplayed tracks in each folder, fetched by byte ranges out of the folder's zip, and
/// deletes a played track once its replacement has landed (#2650).
/// </summary>
public class TheMusicLibraryKeepsTwoTracksInEachFolderTests
{
    private const string Root = @"C:\d47\data\music-pack";

    private static readonly string[] Five = ["a.mp3", "b.mp3", "c.mp3", "d.mp3", "e.mp3"];

    [Fact]
    public async Task EveryFolderFillsToTwoTracksThatMatchTheZip()
    {
        var web = new Web(new Folder("docked", 1, Five), new Folder("supercruise", 1, Five));
        var disk = new Disk();
        var store = Store(web, disk);

        await store.Start();

        foreach (var folder in new[] { "docked", "supercruise" })
        {
            var tracks = disk.Tracks(folder);
            Assert.Equal(2, tracks.Count);
            Assert.All(tracks, name => Assert.Equal(Web.Content(folder, name), disk.Bytes(Path.Combine(Root, folder, name))));
        }

        Assert.Contains("4 tracks ready in 2 folders", store.Status, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ATrackIsWrittenUnderATemporaryNameAndRenamedIntoPlace()
    {
        var web = new Web(new Folder("docked", 1, Five));
        var disk = new Disk();

        await Store(web, disk).Start();

        var moves = disk.Log.Where(line => line.StartsWith("move ", StringComparison.Ordinal)).ToList();
        Assert.Equal(2, moves.Count);
        Assert.All(moves, move => Assert.Matches(@"^move .+\.mp3\.part -> .+\.mp3$", move));
        Assert.DoesNotContain(disk.Paths, path => path.EndsWith(MusicPackStore.Part, StringComparison.Ordinal));
    }

    [Fact]
    public async Task ATrackThatDoesNotMatchItsCrcIsDiscardedAndTriedAgainLater()
    {
        var web = new Web(new Folder("docked", 1, Five)) { Corrupt = true };
        var disk = new Disk();
        var store = Store(web, disk);

        await store.Start();

        Assert.Empty(disk.Tracks("docked"));
        Assert.DoesNotContain(disk.Paths, path => path.EndsWith(MusicPackStore.Part, StringComparison.Ordinal));
        Assert.Contains("docked failed", store.Status, StringComparison.Ordinal);

        web.Corrupt = false;
        await store.Start();

        Assert.Equal(2, disk.Tracks("docked").Count);
        Assert.DoesNotContain("failed", store.Status, StringComparison.Ordinal);
    }

    [Fact]
    public async Task APlayedTrackIsDeletedOnlyAfterItsReplacementLands()
    {
        var web = new Web(new Folder("docked", 1, Five));
        var disk = new Disk();
        var store = Store(web, disk);
        await store.Start();
        var played = disk.Tracks("docked")[0];
        var playedPath = Path.Combine(Root, "docked", played);
        var goneAtLanding = new List<bool>();
        store.Landed += () => goneAtLanding.Add(!disk.HasFile(playedPath));
        disk.Log.Clear();

        await store.Played(Track("docked", played));

        var landed = disk.Log.FindIndex(line => line.StartsWith("move ", StringComparison.Ordinal));
        var deleted = disk.Log.IndexOf("delete " + playedPath);
        Assert.True(landed >= 0 && deleted > landed, string.Join(" | ", disk.Log));
        Assert.Equal([true], goneAtLanding);
        Assert.Equal(2, disk.Tracks("docked").Count);
        Assert.DoesNotContain(played, disk.Tracks("docked"));
    }

    [Fact]
    public async Task WithNoNetworkAFolderKeepsAndRepeatsWhatItHas()
    {
        var web = new Web(new Folder("docked", 1, Five));
        var disk = new Disk();
        var store = Store(web, disk);
        await store.Start();
        var before = disk.Tracks("docked");
        web.Offline = true;

        foreach (var name in before)
        {
            await store.Played(Track("docked", name));
        }

        await store.Start();

        Assert.Equal(before, disk.Tracks("docked"));
        Assert.Contains("could not be read", store.Status, StringComparison.Ordinal);
    }

    [Fact]
    public async Task APlayedTrackIsNotFetchedAgainUntilEveryTrackHasPlayed()
    {
        var web = new Web(new Folder("docked", 1, ["a.mp3", "b.mp3", "c.mp3"]));
        var disk = new Disk();
        var store = Store(web, disk);
        await store.Start();
        var first = disk.Tracks("docked");
        var third = new[] { "a.mp3", "b.mp3", "c.mp3" }.Except(first).Single();

        await store.Played(Track("docked", first[0]));

        Assert.Equal(new[] { first[1], third }.Order(), disk.Tracks("docked"));

        await store.Played(Track("docked", first[1]));

        Assert.Equal(new[] { first[0], third }.Order(), disk.Tracks("docked"));
    }

    [Fact]
    public async Task ThePlayedTracksAreKeptAcrossStarts()
    {
        string[] four = ["a.mp3", "b.mp3", "c.mp3", "d.mp3"];
        var web = new Web(new Folder("docked", 1, four));
        var disk = new Disk();
        var store = Store(web, disk);
        await store.Start();
        var first = disk.Tracks("docked");
        await store.Played(Track("docked", first[0]));
        var fetched = disk.Tracks("docked").Except(first).Single();
        var unfetched = four.Except(first).Except([fetched]).Single();

        var restarted = Store(web, disk);
        await restarted.Played(Track("docked", first[1]));

        Assert.Equal(new[] { fetched, unfetched }.Order(), disk.Tracks("docked"));
    }

    [Fact]
    public async Task ARejectedTrackIsNeverFetched()
    {
        var web = new Web(new Folder("docked", 1, ["a.mp3", "b.mp3", "c.mp3"]));
        var disk = new Disk();

        await Store(web, disk, rejects: [("docked", "b.mp3")]).Start();

        Assert.Equal(["a.mp3", "c.mp3"], disk.Tracks("docked"));
    }

    [Fact]
    public async Task ANewZipVersionKeepsOnlyTheTracksStillInIt()
    {
        var disk = new Disk();
        await Store(new Web(new Folder("docked", 1, Five)), disk).Start();
        var before = disk.Tracks("docked");
        var web = new Web(new Folder("docked", 2, Five.Where(name => name != before[0]).ToArray()));

        await Store(web, disk).Start();

        var after = disk.Tracks("docked");
        Assert.Equal(2, after.Count);
        Assert.DoesNotContain(before[0], after);
        Assert.Contains(before[1], after);
        Assert.Equal("2", disk.ReadText(Path.Combine(Root, "docked", MusicPackStore.VersionFile)));
    }

    [Fact]
    public async Task AFolderHoldingTheWholeLibraryKeepsTwoAtTheFirstStart()
    {
        var web = new Web(new Folder("docked", 1, Five));
        var disk = new Disk();

        foreach (var name in Five)
        {
            disk.Put(Path.Combine(Root, "docked", name), Web.Content("docked", name));
        }

        disk.WriteText(Path.Combine(Root, "docked", MusicPackStore.VersionFile), "1");
        var store = Store(web, disk);
        var landed = 0;
        store.Landed += () => landed++;

        await store.Start();

        Assert.Equal(2, disk.Tracks("docked").Count);
        Assert.True(landed > 0);
        Assert.DoesNotContain(disk.Log, line => line.StartsWith("move ", StringComparison.Ordinal));
    }

    [Fact]
    public async Task TheFirstStartTrimNeedsNoNetwork()
    {
        var web = new Web(new Folder("docked", 1, Five)) { Offline = true };
        var disk = new Disk();

        foreach (var name in Five)
        {
            disk.Put(Path.Combine(Root, "docked", name), Web.Content("docked", name));
        }

        await Store(web, disk).Start();

        Assert.Equal(2, disk.Tracks("docked").Count);
    }

    [Fact]
    public async Task TurningTheSettingOffStopsBeforeTheNextTrack()
    {
        var on = true;
        var web = new Web(new Folder("docked", 1, Five), new Folder("supercruise", 1, Five));
        var disk = new Disk();
        var store = Store(web, disk, () => on);
        store.Landed += () => on = false;

        await store.Start();

        Assert.Single(disk.Tracks("docked"));
        Assert.Empty(disk.Tracks("supercruise"));
    }

    [Fact]
    public async Task NothingIsFetchedWhileTheSettingIsOff()
    {
        var web = new Web(new Folder("docked", 1, Five));

        await Store(web, new Disk(), () => false).Start();

        Assert.Empty(web.Asked);
    }

    [Fact]
    public async Task ShortDiskSpaceStopsWithTheRowSayingSo()
    {
        var web = new Web(new Folder("docked", 1, Five), new Folder("supercruise", 1, Five));
        var disk = new Disk { Free = 10 };
        var store = Store(web, disk);

        await store.Start();

        Assert.Empty(disk.Tracks("docked"));
        Assert.DoesNotContain(web.Asked, asked => asked.StartsWith("supercruise", StringComparison.Ordinal));
        Assert.Contains("Stopped: docked needs", store.Status, StringComparison.Ordinal);
        Assert.Contains("No tracks are ready yet.", store.Status, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AServerThatIgnoresTheRangeFetchesNothing()
    {
        var web = new Web(new Folder("docked", 1, Five)) { IgnoresRange = true };
        var disk = new Disk();
        var store = Store(web, disk);

        await store.Start();

        Assert.Empty(disk.Tracks("docked"));
        Assert.Contains("docked failed", store.Status, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AnEntryThatIsNotABareMp3NameIsNeverWritten()
    {
        var web = new Web(new Folder("docked", 1, [@"..\escape.mp3", "../up.mp3", "notes.txt", "a.mp3", "b.mp3"]));
        var disk = new Disk();

        await Store(web, disk).Start();

        Assert.Equal(["a.mp3", "b.mp3"], disk.Tracks("docked"));
        Assert.All(disk.Paths, path => Assert.StartsWith(Root, path, StringComparison.Ordinal));
        Assert.DoesNotContain(disk.Paths, path => path.Contains("escape", StringComparison.Ordinal) || path.Contains("up.mp3", StringComparison.Ordinal));
    }

    [Fact]
    public void TheCrcIsZipsCrc32()
    {
        Assert.Equal(0xCBF43926u, Crc32.Append(0, "123456789"u8));
    }

    private static MusicPackStore Store(
        Web web, Disk disk, Func<bool>? allowed = null, IReadOnlyList<(string Folder, string File)>? rejects = null) =>
        new(Root, allowed ?? (() => true), () => rejects ?? [], disk, new HttpClient(web), NullLogger.Instance, new Random(3));

    private static MusicTrack Track(string folder, string file) =>
        new(Path.GetFileNameWithoutExtension(file), () => throw new NotSupportedException(), folder, file);

    private sealed record Folder(string Name, int Version, string[] Tracks);

    /// <summary>The release, in memory: a manifest and one stored zip per folder, answering range requests.</summary>
    private sealed class Web : HttpMessageHandler
    {
        private readonly Dictionary<string, byte[]> _files = new(StringComparer.Ordinal);
        private readonly Dictionary<string, byte[]> _corrupt = new(StringComparer.Ordinal);

        public Web(params Folder[] folders)
        {
            var entries = new List<object>();

            foreach (var folder in folders)
            {
                var zip = Zip(folder);
                _files[folder.Name + ".zip"] = zip;
                _corrupt[folder.Name + ".zip"] = Corrupted(folder, zip);
                entries.Add(new
                {
                    name = folder.Name,
                    version = folder.Version,
                    zip = folder.Name + ".zip",
                    bytes = zip.LongLength,
                    sha256 = Convert.ToHexStringLower(SHA256.HashData(zip)),
                    tracks = folder.Tracks.Length,
                });
            }

            _files["manifest.json"] = JsonSerializer.SerializeToUtf8Bytes(new { folders = entries });
        }

        public bool Corrupt { get; set; }

        public bool Offline { get; set; }

        public bool IgnoresRange { get; init; }

        /// <summary>Each request: the file, then the range asked for.</summary>
        public List<string> Asked { get; } = [];

        public static byte[] Content(string folder, string name) =>
            Encoding.UTF8.GetBytes(string.Concat(Enumerable.Repeat($"{folder}/{name};", 40)));

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (Offline)
            {
                throw new HttpRequestException("No route to host.");
            }

            var url = request.RequestUri!.ToString();
            Assert.StartsWith(MusicPackStore.Source, url, StringComparison.Ordinal);
            var file = url[MusicPackStore.Source.Length..];
            var range = request.Headers.Range?.Ranges.Single();

            lock (Asked)
            {
                Asked.Add(range is null ? file : $"{file} {range.From}-{range.To}");
            }

            if (!_files.TryGetValue(file, out var bytes))
            {
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
            }

            if (Corrupt && _corrupt.TryGetValue(file, out var corrupt))
            {
                bytes = corrupt;
            }

            if (range is null || IgnoresRange)
            {
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(bytes) });
            }

            var from = range.From!.Value;
            var to = Math.Min(range.To!.Value, bytes.LongLength - 1);
            var content = new ByteArrayContent(bytes, (int)from, (int)(to - from + 1));
            content.Headers.ContentRange = new ContentRangeHeaderValue(from, to, bytes.LongLength);

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.PartialContent) { Content = content });
        }

        private static byte[] Zip(Folder folder)
        {
            using var memory = new MemoryStream();

            using (var zip = new ZipArchive(memory, ZipArchiveMode.Create, leaveOpen: true))
            {
                foreach (var name in folder.Tracks)
                {
                    using var track = zip.CreateEntry(name, CompressionLevel.NoCompression).Open();
                    track.Write(Content(folder.Name, name));
                }
            }

            return memory.ToArray();
        }

        /// <summary>The zip with one byte of every track changed, so each track fails its CRC.</summary>
        private static byte[] Corrupted(Folder folder, byte[] zip)
        {
            var copy = zip.ToArray();

            foreach (var name in folder.Tracks)
            {
                var at = copy.AsSpan().IndexOf(Content(folder.Name, name));

                if (at >= 0)
                {
                    copy[at + 5] ^= 0xFF;
                }
            }

            return copy;
        }
    }

    /// <summary>The music-pack folder, in memory.</summary>
    private sealed class Disk : IMusicPackDisk
    {
        private readonly Dictionary<string, byte[]> _files = new(StringComparer.OrdinalIgnoreCase);

        public long Free { get; init; } = long.MaxValue;

        public List<string> Log { get; } = [];

        public IReadOnlyCollection<string> Paths
        {
            get
            {
                lock (_files)
                {
                    return [.. _files.Keys];
                }
            }
        }

        public bool HasFile(string path)
        {
            lock (_files)
            {
                return _files.ContainsKey(path);
            }
        }

        public byte[] Bytes(string path)
        {
            lock (_files)
            {
                return _files[path];
            }
        }

        /// <summary>The library tracks in a folder, sorted.</summary>
        public List<string> Tracks(string folder) =>
            [.. Files(Path.Combine(Root, folder)).Select(file => file.Name).Where(name => name.EndsWith(".mp3", StringComparison.Ordinal)).Order(StringComparer.Ordinal)];

        public void Put(string path, byte[] bytes)
        {
            lock (_files)
            {
                _files[path] = bytes;
            }
        }

        public long? FreeBytes(string folder) => Free;

        public string? ReadText(string path)
        {
            lock (_files)
            {
                return _files.TryGetValue(path, out var bytes) ? Encoding.UTF8.GetString(bytes) : null;
            }
        }

        public void WriteText(string path, string contents) => Put(path, Encoding.UTF8.GetBytes(contents));

        public Stream CreateTemp(string path) => new MemoryStream();

        public IReadOnlyList<(string Name, long Bytes)> Files(string folder)
        {
            lock (_files)
            {
                return [.. _files
                    .Where(file => string.Equals(Path.GetDirectoryName(file.Key), folder, StringComparison.OrdinalIgnoreCase))
                    .Select(file => (Path.GetFileName(file.Key), file.Value.LongLength))];
            }
        }

        public Stream OpenRead(string path) => new MemoryStream(Bytes(path), writable: false);

        public Stream Create(string path) => new Written(this, path);

        public void MoveFile(string from, string to)
        {
            lock (_files)
            {
                Assert.True(_files.Remove(from, out var bytes), $"{from} is not there to move");
                _files[to] = bytes;
                Log.Add($"move {from} -> {to}");
            }
        }

        public void DeleteFile(string path)
        {
            lock (_files)
            {
                if (_files.Remove(path))
                {
                    Log.Add("delete " + path);
                }
            }
        }

        public void DeleteFolder(string path)
        {
            lock (_files)
            {
                foreach (var file in _files.Keys.Where(file => file.StartsWith(path + "\\", StringComparison.OrdinalIgnoreCase)).ToList())
                {
                    _files.Remove(file);
                }
            }
        }

        /// <summary>A file that appears on the disk when it is closed.</summary>
        private sealed class Written(Disk disk, string path) : MemoryStream
        {
            protected override void Dispose(bool disposing)
            {
                if (disposing)
                {
                    disk.Put(path, ToArray());
                    disk.Log.Add("write " + path);
                }

                base.Dispose(disposing);
            }
        }
    }
}
