using System.Net;
using System.Text;
using D47.App.Media;
using D47.Core.Storage;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.App.Tests.Media;

/// <summary>
/// The podcast store reads its cache when built and requests nothing until a refresh is asked for. A refresh caches
/// the feed's text once it parses; a feed over 10 MB, or one that does not parse, leaves the previous cache.
/// </summary>
public class APodcastFeedIsFetchedOnlyWhenAskedTests
{
    private const string Folder = @"C:\d47\data\podcasts";

    private static readonly Uri Feed = new("https://feeds.example/show.xml");

    [Fact]
    public void NothingIsRequestedWhenTheStoreIsBuilt()
    {
        var disk = new MemoryFileSystem();
        var handler = new FakeHandler(_ => Ok(Rss("First")));

        _ = Build(disk, handler, Feed);

        Assert.Empty(handler.Requests);
        Assert.Empty(Cached(disk));
    }

    [Fact]
    public async Task ARefreshCachesTheFeedsText()
    {
        var disk = new MemoryFileSystem();
        var handler = new FakeHandler(_ => Ok(Rss("First")));
        var store = Build(disk, handler, Feed);
        var changed = 0;
        store.Changed += () => changed++;

        var result = await store.RefreshAsync(Feed);

        Assert.Null(result.Problem);
        Assert.Equal("First", result.Show!.Title);
        Assert.Equal([Feed], handler.Requests);
        var path = Assert.Single(Cached(disk));
        Assert.Matches(@"^[0-9a-f]{64}\.xml$", Path.GetFileName(path));
        Assert.Equal(Rss("First"), disk.ReadText(path));
        Assert.Equal("First", Assert.Single(store.Shows).Show.Title);
        Assert.Equal(["cdn.example"], store.EpisodeHosts);
        Assert.Equal(1, changed);
    }

    [Fact]
    public async Task TheCacheIsReadAtTheNextStart()
    {
        var disk = new MemoryFileSystem();
        await Build(disk, new FakeHandler(_ => Ok(Rss("First"))), Feed).RefreshAsync(Feed);
        var handler = new FakeHandler(_ => Ok(Rss("Second")));

        var store = Build(disk, handler, Feed);

        Assert.Equal("First", Assert.Single(store.Shows).Show.Title);
        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task AFeedOverTenMegabytesIsRefusedWithASentence()
    {
        var disk = new MemoryFileSystem();
        var handler = new FakeHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            // No Content-Length, so the counted read is what refuses it.
            Content = new StreamContent(new EndlessStream()),
        });
        var store = Build(disk, handler, Feed);

        var result = await store.RefreshAsync(Feed);

        Assert.Null(result.Show);
        Assert.Equal("The feed is larger than 10 MB, so D47 does not read it.", result.Problem);
        Assert.Empty(Cached(disk));
        Assert.Empty(store.Shows);
    }

    [Fact]
    public async Task AFeedThatSaysItIsOverTenMegabytesIsRefusedBeforeItsBodyIsRead()
    {
        var content = new ByteArrayContent([]);
        content.Headers.ContentLength = PodcastStore.MostBytes + 1;
        var store = Build(new MemoryFileSystem(), new FakeHandler(_ => new HttpResponseMessage(HttpStatusCode.OK) { Content = content }), Feed);

        var result = await store.RefreshAsync(Feed);

        Assert.Equal("The feed is larger than 10 MB, so D47 does not read it.", result.Problem);
    }

    [Fact]
    public async Task AFeedThatFailsToParseKeepsThePreviousCache()
    {
        var disk = new MemoryFileSystem();
        var answers = new Queue<string>([Rss("First"), "<html>not a feed</html>"]);
        var store = Build(disk, new FakeHandler(_ => Ok(answers.Dequeue())), Feed);
        await store.RefreshAsync(Feed);

        var result = await store.RefreshAsync(Feed);

        Assert.Equal("This is not an RSS feed.", result.Problem);
        Assert.Equal(Rss("First"), disk.ReadText(Assert.Single(Cached(disk))));
        Assert.Equal("First", Assert.Single(store.Shows).Show.Title);
    }

    [Fact]
    public async Task AServerErrorKeepsThePreviousCacheAndSaysWhatHappened()
    {
        var disk = new MemoryFileSystem();
        var answers = new Queue<HttpResponseMessage>([Ok(Rss("First")), new HttpResponseMessage(HttpStatusCode.NotFound)]);
        var store = Build(disk, new FakeHandler(_ => answers.Dequeue()), Feed);
        await store.RefreshAsync(Feed);

        var result = await store.RefreshAsync(Feed);

        Assert.Equal("The feed could not be fetched: feeds.example returned 404.", result.Problem);
        Assert.Equal("First", Assert.Single(store.Shows).Show.Title);
    }

    [Fact]
    public async Task RefreshingAllFetchesEveryFeedAdded()
    {
        var other = new Uri("http://other.example/rss");
        var handler = new FakeHandler(request => Ok(Rss(request.RequestUri!.Host)));
        var store = Build(new MemoryFileSystem(), handler, Feed, other);

        var results = await store.RefreshAllAsync();

        Assert.Equal([Feed, other], handler.Requests);
        Assert.All(results, result => Assert.Null(result.Problem));
        Assert.Equal(["feeds.example", "other.example"], store.Shows.Select(cached => cached.Show.Title));
    }

    [Fact]
    public async Task AFeedNoLongerAddedIsLeftOutOfTheShows()
    {
        var feeds = new List<Uri> { Feed };
        var store = new PodcastStore(Folder, () => [.. feeds], new MemoryFileSystem(), new HttpClient(new FakeHandler(_ => Ok(Rss("First")))), NullLogger.Instance);
        await store.RefreshAsync(Feed);

        feeds.Clear();

        Assert.Empty(store.Shows);
        Assert.Empty(store.EpisodeHosts);
    }

    private static IReadOnlyList<string> Cached(MemoryFileSystem disk) => disk.Enumerate(Path.Combine(Folder, "feeds"), "*.xml");

    private static PodcastStore Build(MemoryFileSystem disk, FakeHandler handler, params Uri[] feeds) =>
        new(Folder, () => feeds, disk, new HttpClient(handler), NullLogger.Instance);

    private static HttpResponseMessage Ok(string text) =>
        new(HttpStatusCode.OK) { Content = new StringContent(text, Encoding.UTF8, "application/rss+xml") };

    private static string Rss(string title) =>
        $"""
         <?xml version="1.0" encoding="UTF-8"?>
         <rss version="2.0">
           <channel>
             <title>{title}</title>
             <item>
               <guid>one</guid>
               <title>Episode one</title>
               <pubDate>Mon, 02 Nov 2026 10:00:00 GMT</pubDate>
               <enclosure url="https://cdn.example/one.mp3" type="audio/mpeg" length="1000" />
             </item>
           </channel>
         </rss>
         """;

    private sealed class FakeHandler(Func<HttpRequestMessage, HttpResponseMessage> answer) : HttpMessageHandler
    {
        public List<Uri> Requests { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            lock (Requests)
            {
                Requests.Add(request.RequestUri!);
            }

            return Task.FromResult(answer(request));
        }
    }

    /// <summary>Spaces without end, and no length.</summary>
    private sealed class EndlessStream : Stream
    {
        public override bool CanRead => true;

        public override bool CanSeek => false;

        public override bool CanWrite => false;

        public override long Length => throw new NotSupportedException();

        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            buffer.AsSpan(offset, count).Fill((byte)' ');
            return count;
        }

        public override void Flush()
        {
        }

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}
