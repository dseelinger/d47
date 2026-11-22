using System.Net;
using System.Text;
using System.Web;
using D47.App.Media;
using D47.Core;
using D47.Core.Configuration;
using D47.Core.Storage;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.App.Tests.Media;

/// <summary>
/// A podcast search sends Apple's directory the words and the fixed query, nothing else, and refuses an answer over
/// 1 MB. Adding a result puts its feed in the settings once and fetches it.
/// </summary>
public class APodcastSearchSendsOnlyTheWordsTests
{
    private const string Answer = """
        {"resultCount":1,"results":[{"collectionName":"Lave Radio","artistName":"Pilots Federation",
         "feedUrl":"https://feeds.example/show.xml","trackCount":412,"artworkUrl600":"https://art.example/600.jpg"}]}
        """;

    [Fact]
    public async Task ItSendsExactlyTheFourQueryParameters()
    {
        var handler = new FakeHandler(_ => Ok(Answer));

        var outcome = await new PodcastSearchClient(new HttpClient(handler), NullLogger.Instance).SearchAsync("  elite & dangerous ");

        Assert.Null(outcome.Problem);
        Assert.Equal("Lave Radio", Assert.Single(outcome.Results).Title);
        var request = Assert.Single(handler.Requests);
        Assert.Equal("https://itunes.apple.com/search", request.GetLeftPart(UriPartial.Path));
        var query = HttpUtility.ParseQueryString(request.Query);
        Assert.Equal(["entity", "limit", "media", "term"], query.AllKeys.Order(StringComparer.Ordinal));
        Assert.Equal("elite & dangerous", query["term"]);
        Assert.Equal("podcast", query["media"]);
        Assert.Equal("podcast", query["entity"]);
        Assert.Equal("10", query["limit"]);
    }

    [Fact]
    public async Task BlankWordsAreNotSent()
    {
        var handler = new FakeHandler(_ => Ok(Answer));

        var outcome = await new PodcastSearchClient(new HttpClient(handler), NullLogger.Instance).SearchAsync("   ");

        Assert.Empty(outcome.Results);
        Assert.NotNull(outcome.Problem);
        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task AnAnswerOverOneMegabyteIsRefused()
    {
        var handler = new FakeHandler(_ => new HttpResponseMessage(HttpStatusCode.OK) { Content = new StreamContent(new EndlessStream()) });

        var outcome = await new PodcastSearchClient(new HttpClient(handler), NullLogger.Instance).SearchAsync("lave");

        Assert.Empty(outcome.Results);
        Assert.Equal("The podcast directory's answer is larger than 1 MB, so D47 does not read it.", outcome.Problem);
    }

    [Fact]
    public async Task AnAnswerThatSaysItIsOverOneMegabyteIsRefusedBeforeItsBodyIsRead()
    {
        var content = new ByteArrayContent(Encoding.UTF8.GetBytes(Answer));
        content.Headers.ContentLength = PodcastSearchClient.MostBytes + 1;
        var handler = new FakeHandler(_ => new HttpResponseMessage(HttpStatusCode.OK) { Content = content });

        var outcome = await new PodcastSearchClient(new HttpClient(handler), NullLogger.Instance).SearchAsync("lave");

        Assert.Empty(outcome.Results);
        Assert.Equal("The podcast directory's answer is larger than 1 MB, so D47 does not read it.", outcome.Problem);
    }

    [Fact]
    public async Task AServerErrorSaysWhatHappened()
    {
        var handler = new FakeHandler(_ => new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));

        var outcome = await new PodcastSearchClient(new HttpClient(handler), NullLogger.Instance).SearchAsync("lave");

        Assert.Equal("The podcast directory returned 503.", outcome.Problem);
    }

    [Fact]
    public async Task AddingAResultPutsItsFeedInTheSettingsOnceAndFetchesIt()
    {
        var disk = new MemoryFileSystem();
        var paths = new AppPaths(@"C:\d47");
        var settings = new SettingsService(
            new SettingsStore(paths, disk, NullLogger<SettingsStore>.Instance),
            new SecretStore(paths, new NoopProtector(), disk, NullLogger<SecretStore>.Instance),
            new D47Settings(),
            NullLogger<SettingsService>.Instance);
        var feed = new Uri("https://feeds.example/show.xml");
        var handler = new FakeHandler(_ => Ok(Rss));
        var store = new PodcastStore(paths.Podcasts, () => settings.Current.Podcasts.FeedUris(), disk, new HttpClient(handler), NullLogger.Instance);

        var first = await store.AddAsync(feed, settings);
        await store.AddAsync(feed, settings);

        Assert.Null(first.Problem);
        Assert.Equal(["https://feeds.example/show.xml"], settings.Current.Podcasts.Feeds);
        Assert.Equal([feed, feed], handler.Requests);
        Assert.Equal("Lave Radio", Assert.Single(store.Shows).Show.Title);
    }

    private const string Rss = """
        <?xml version="1.0" encoding="UTF-8"?>
        <rss version="2.0">
          <channel>
            <title>Lave Radio</title>
            <item>
              <guid>one</guid>
              <title>Episode one</title>
              <pubDate>Mon, 02 Nov 2026 10:00:00 GMT</pubDate>
              <enclosure url="https://cdn.example/one.mp3" type="audio/mpeg" length="1000" />
            </item>
          </channel>
        </rss>
        """;

    private static HttpResponseMessage Ok(string text) =>
        new(HttpStatusCode.OK) { Content = new StringContent(text, Encoding.UTF8, "application/json") };

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
