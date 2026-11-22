using System.Net;
using System.Web;
using D47.Core.Viewing;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Knowledge.Tests;

/// <summary>
/// A NASA search asks for videos only, takes each result's MP4 from its file list on images-assets.nasa.gov, and
/// gives the player no URL on any other host.
/// </summary>
public class ANasaVideoSearchReachesOnlyNasasHostsTests
{
    private const string UltimateList = "https://images-assets.nasa.gov/video/Ultimate%20Saturn%20V%20Launch%20w%20Enhanced%20Sound/collection.json";

    private const string CrawlerList = "https://images-assets.nasa.gov/video/KSC-1960s-MH-NAS01-0001-Apollo_Saturn_V_Crawler,_VAB_Pad_Film_To_Tape/collection.json";

    [Fact]
    public async Task TheSearchAsksForVideoWithThePageAndItsSize()
    {
        var nasa = new Nasa();
        using var source = Source(nasa);

        await source.SearchAsync("saturn v", new VideoFilters(), 2, 8, TestContext.Current.CancellationToken);

        var search = nasa.Requests.Single(uri => uri.Host == "images-api.nasa.gov");
        Assert.Equal("/search", search.AbsolutePath);
        var query = HttpUtility.ParseQueryString(search.Query);
        Assert.Equal("video", query["media_type"]);
        Assert.Equal("saturn v", query["q"]);
        Assert.Equal("2", query["page"]);
        Assert.Equal("8", query["page_size"]);
    }

    [Fact]
    public async Task EachTitleTakesTheLargeMp4AndItsCaptionsOverHttps()
    {
        using var source = Source(new Nasa());

        var page = await source.SearchAsync("saturn v", new VideoFilters(), 1, 25, TestContext.Current.CancellationToken);

        Assert.Equal(24, page.Total);
        var ultimate = page.Titles[0];
        Assert.Equal("Ultimate Saturn V Launch w Enhanced Sound", ultimate.Id);
        Assert.Equal(VideoKind.Footage, ultimate.Kind);
        Assert.Equal("US government work", ultimate.Licence);
        Assert.Equal("NASA MSFC", ultimate.Credit);
        Assert.Equal(
            new Uri("https://images-assets.nasa.gov/video/Ultimate%20Saturn%20V%20Launch%20w%20Enhanced%20Sound/Ultimate%20Saturn%20V%20Launch%20w%20Enhanced%20Sound~large.mp4"),
            ultimate.Media);
        Assert.Equal(
            new Uri("https://images-assets.nasa.gov/video/Ultimate%20Saturn%20V%20Launch%20w%20Enhanced%20Sound/Ultimate%20Saturn%20V%20Launch%20w%20Enhanced%20Sound.srt"),
            ultimate.Subtitles);
        Assert.EndsWith(".vtt", page.Titles[1].Subtitles!.AbsolutePath, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AFileListOnAnotherHostIsNeverFetchedOrHandedOn()
    {
        var nasa = new Nasa();
        using var source = Source(nasa);

        var page = await source.SearchAsync("saturn v", new VideoFilters(), 1, 25, TestContext.Current.CancellationToken);

        var loopback = page.Titles.Single(title => title.Id == "Loopback");
        Assert.False(loopback.Playable);
        Assert.Null(loopback.Subtitles);
        Assert.Equal("NASA's list of its files is not on images-assets.nasa.gov.", loopback.Unplayable);
        Assert.All(nasa.Requests, uri => Assert.Contains(uri.Host, (string[])["images-api.nasa.gov", "images-assets.nasa.gov"]));
    }

    [Fact]
    public async Task MediaOnAnyOtherHostLeavesTheTitleUnplayable()
    {
        using var source = Source(new Nasa(crawler: "nasa-collection-elsewhere.json"));

        var page = await source.SearchAsync("saturn v", new VideoFilters(), 1, 25, TestContext.Current.CancellationToken);

        var crawler = page.Titles[1];
        Assert.Null(crawler.Media);
        Assert.Equal("NASA lists no video file D47 can reach for it.", crawler.Unplayable);
        Assert.All(
            page.Titles.SelectMany(title => (Uri?[])[title.Media, title.Subtitles]).OfType<Uri>(),
            uri =>
            {
                Assert.Equal("https", uri.Scheme);
                Assert.Equal("images-assets.nasa.gov", uri.Host);
            });
    }

    [Fact]
    public async Task ATitleWithNoMp4IsMarkedUnplayableWithTheReason()
    {
        using var source = Source(new Nasa(crawler: "nasa-collection-mov-only.json"));

        var page = await source.SearchAsync("saturn v", new VideoFilters(), 1, 25, TestContext.Current.CancellationToken);

        Assert.Equal("Offered as MOV only, with no MP4 D47 can play.", page.Titles[1].Unplayable);
        Assert.True(page.Titles[0].Playable);
    }

    [Fact]
    public async Task AKindNasaDoesNotHoldSendsNothing()
    {
        var nasa = new Nasa();
        using var source = Source(nasa);

        var page = await source.SearchAsync("saturn v", new VideoFilters(VideoKind.FeatureFilm), 1, 25, TestContext.Current.CancellationToken);

        Assert.Empty(page.Titles);
        Assert.Equal(0, page.Total);
        Assert.Empty(nasa.Requests);
    }

    [Fact]
    public async Task AServerErrorIsAProblem()
    {
        using var source = Source(new Nasa(status: HttpStatusCode.ServiceUnavailable));

        var page = await source.SearchAsync("saturn v", new VideoFilters(), 1, 25, TestContext.Current.CancellationToken);

        Assert.Equal("NASA's library could not be searched: it answered 503.", page.Problem);
    }

    [Fact]
    public void OnlyImagesAssetsUrlsAreKept()
    {
        Assert.Equal(new Uri("https://images-assets.nasa.gov/video/a%20b~large.mp4"), NasaVideoSource.Asset("http://images-assets.nasa.gov/video/a b~large.mp4"));
        Assert.Null(NasaVideoSource.Asset("http://127.0.0.1/video/a~large.mp4"));
        Assert.Null(NasaVideoSource.Asset("http://192.168.1.20/video/a~large.mp4"));
        Assert.Null(NasaVideoSource.Asset("http://images-assets.nasa.gov:8080/video/a~large.mp4"));
        Assert.Null(NasaVideoSource.Asset("https://user@images-assets.nasa.gov/video/a~large.mp4"));
        Assert.Null(NasaVideoSource.Asset("file:///C:/video/a~large.mp4"));
        Assert.Null(NasaVideoSource.Asset("https://images-assets.nasa.gov.example.com/video/a~large.mp4"));
    }

    private static NasaVideoSource Source(Nasa nasa) =>
        new(NullLogger<NasaVideoSource>.Instance, new HttpClient(nasa));

    /// <summary>Answers the search and the two recorded file lists; any other request is 404.</summary>
    private sealed class Nasa(string crawler = "nasa-collection-crawler.json", HttpStatusCode status = HttpStatusCode.OK) : HttpMessageHandler
    {
        public List<Uri> Requests { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var uri = request.RequestUri!;

            lock (Requests)
            {
                Requests.Add(uri);
            }

            var fixture = uri.Host == "images-api.nasa.gov" && uri.AbsolutePath == "/search" ? "nasa-search-saturn-v.json"
                : uri.AbsoluteUri == UltimateList ? "nasa-collection-ultimate-saturn-v.json"
                : uri.AbsoluteUri == CrawlerList ? crawler
                : null;

            return Task.FromResult(fixture is null
                ? new HttpResponseMessage(HttpStatusCode.NotFound)
                : status != HttpStatusCode.OK
                    ? new HttpResponseMessage(status)
                    : new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(Fixture.Text(fixture)) });
        }
    }
}
