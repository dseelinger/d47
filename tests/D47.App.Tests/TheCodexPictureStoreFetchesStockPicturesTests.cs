using System.Collections.Concurrent;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using D47.App.Panel;
using D47.Core.Interface;
using D47.Core.Storage;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.App.Tests;

[Trait("Category", "Integration")]
public sealed class TheCodexPictureStoreFetchesStockPicturesTests
{
    private const string Folder = @"C:\d47\data\codex-pictures";

    private static readonly byte[] Bytes = Encoding.ASCII.GetBytes("a picture");

    private static CodexPictureRow Row(long entry = 1, int region = 1)
    {
        var sha = Convert.ToHexStringLower(SHA256.HashData(Bytes));
        return new CodexPictureRow(entry, region, "codex-pictures-1", CodexPictureIndex.AssetName(entry, region, sha), sha, Bytes.Length);
    }

    private sealed class Release : HttpMessageHandler
    {
        private readonly ConcurrentDictionary<string, byte[]> _files = new();

        public ConcurrentQueue<string> Asked { get; } = new();

        public bool Fail { get; set; }

        public void Serve(string tag, string file, byte[] bytes) => _files[$"/{CodexPictureIndex.Repository}/releases/download/{tag}/{file}"] = bytes;

        public void ServeIndex(params CodexPictureRow[] rows) =>
            Serve(CodexPictureIndex.FirstTag, CodexPictureIndex.IndexName, Encoding.UTF8.GetBytes(CodexPictureIndex.Write(rows)));

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Asked.Enqueue(request.RequestUri!.AbsolutePath);

            if (Fail)
            {
                throw new HttpRequestException("down");
            }

            return Task.FromResult(_files.TryGetValue(request.RequestUri.AbsolutePath, out var bytes)
                ? new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(bytes) }
                : new HttpResponseMessage(HttpStatusCode.NotFound));
        }
    }

    private sealed class Messages : ILogger
    {
        public ConcurrentQueue<LogLevel> Levels { get; } = new();

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
            Levels.Enqueue(logLevel);
    }

    private static string Stock(string name) => Path.Combine(Folder, CodexPictureStore.StockFolder, name);

    private static (CodexPictureStore Store, MemoryFileSystem Files) Open(Release release, Func<bool>? allowed = null, ILogger? logger = null)
    {
        var files = new MemoryFileSystem();
        return (new CodexPictureStore(files, Folder, allowed ?? (() => true), logger ?? NullLogger.Instance, release), files);
    }

    [Fact]
    public async Task TheIndexIsFetchedOncePerSessionAndWrittenToDisk()
    {
        var release = new Release();
        var row = Row();
        release.ServeIndex(row);
        var (store, files) = Open(release);

        store.StockRows();
        await store.IndexFetched;
        var rows = store.StockRows();

        Assert.Equal([row], rows);
        Assert.Single(release.Asked);
        Assert.NotNull(files.ReadText(Stock(CodexPictureIndex.IndexName)));
    }

    [Fact]
    public async Task AFailedFetchFallsBackToTheIndexOnDisk()
    {
        var release = new Release { Fail = true };
        var row = Row();
        var (store, files) = Open(release);
        files.WriteText(Stock(CodexPictureIndex.IndexName), CodexPictureIndex.Write([row]));

        store.StockRows();
        await store.IndexFetched;

        Assert.Equal([row], store.StockRows());
    }

    [Fact]
    public async Task A404WithNoIndexOnDiskGivesNoRowsAndLogsNoFailure()
    {
        var log = new Messages();
        var (store, _) = Open(new Release(), logger: log);

        store.StockRows();
        await store.IndexFetched;

        Assert.Empty(store.StockRows());
        Assert.NotEmpty(log.Levels);
        Assert.All(log.Levels, level => Assert.Equal(LogLevel.Information, level));
    }

    [Fact]
    public async Task AMatchingPictureIsFetchedOnceWrittenAndAnnounced()
    {
        var release = new Release();
        var row = Row();
        release.Serve(row.Tag, row.Asset, Bytes);
        var (store, files) = Open(release);
        var arrived = new ConcurrentQueue<CodexPictureKey>();
        store.Arrived += arrived.Enqueue;

        Assert.Null(store.Picture(row));
        Assert.True(await store.Fetched(row.Key));

        Assert.Equal(Stock(row.Asset), store.Picture(row));
        Assert.Equal(Bytes, files.ReadBytes(Stock(row.Asset)));
        Assert.Null(files.ReadBytes(Stock(row.Asset) + ".part"));
        Assert.Equal([row.Key], arrived);
        Assert.Single(release.Asked);
    }

    [Fact]
    public async Task AMismatchedDownloadIsNotKept()
    {
        var release = new Release();
        var row = Row();
        release.Serve(row.Tag, row.Asset, Encoding.ASCII.GetBytes("another one"));
        var (store, files) = Open(release);

        store.Picture(row);

        Assert.False(await store.Fetched(row.Key));
        Assert.Null(files.ReadBytes(Stock(row.Asset)));
        Assert.Null(files.ReadBytes(Stock(row.Asset) + ".part"));
    }

    [Fact]
    public void AMatchingFileAlreadyOnDiskIsUsedWithoutAFetch()
    {
        var release = new Release();
        var row = Row();
        var (store, files) = Open(release);
        files.WriteBytes(Stock(row.Asset), Bytes);

        Assert.Equal(Stock(row.Asset), store.Picture(row));
        Assert.Empty(release.Asked);
    }

    [Fact]
    public async Task ANewIndexDeletesAFileItNoLongerNames()
    {
        var release = new Release();
        var kept = Row(1, 1);
        var gone = Row(2, 1);
        release.ServeIndex(kept);
        var (store, files) = Open(release);
        files.WriteBytes(Stock(kept.Asset), Bytes);
        files.WriteBytes(Stock(gone.Asset), Bytes);

        store.StockRows();
        await store.IndexFetched;

        Assert.NotNull(files.ReadBytes(Stock(kept.Asset)));
        Assert.Null(files.ReadBytes(Stock(gone.Asset)));
    }

    [Fact]
    public async Task WithTheSettingOffNothingIsFetchedAndNoStockRowIsReturned()
    {
        var release = new Release();
        var row = Row();
        release.ServeIndex(row);
        release.Serve(row.Tag, row.Asset, Bytes);
        var (store, files) = Open(release, allowed: () => false);
        files.WriteText(Stock(CodexPictureIndex.IndexName), CodexPictureIndex.Write([row]));
        files.WriteBytes(Stock(row.Asset), Bytes);
        var own = new CodexPictureKey(5, 1);
        files.WriteBytes(Path.Combine(Folder, CodexPictureStore.LocalFolder, own.FileName), Bytes);

        Assert.Empty(store.StockRows());
        await store.IndexFetched;
        Assert.Null(store.Picture(row));

        Assert.Empty(release.Asked);
        Assert.Equal([own], store.LocalKeys());
        Assert.NotNull(files.ReadBytes(Stock(row.Asset)));
    }

    [Fact]
    public void TheLocalKeysAreTheParseableNamesInTheLocalFolder()
    {
        var (store, files) = Open(new Release());
        var own = new CodexPictureKey(7, 2);
        files.WriteBytes(Path.Combine(Folder, CodexPictureStore.LocalFolder, own.FileName), Bytes);
        files.WriteBytes(Path.Combine(Folder, CodexPictureStore.LocalFolder, "notes.txt"), Bytes);
        files.WriteBytes(Path.Combine(Folder, CodexPictureStore.LocalFolder, "x-y.jpg"), Bytes);

        Assert.Equal([own], store.LocalKeys());
    }
}
