using System.Net;
using System.Web;
using D47.Core.Viewing;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Knowledge.Tests;

/// <summary>
/// An Internet Archive search always carries the licence clause, drops a result whose licence is not public domain or
/// Creative Commons, finds each result's MP4 in its file list, and marks a title with none unplayable.
/// </summary>
public class AnArchiveVideoSearchListsOnlyFreeLicencesTests
{
    private static readonly Dictionary<string, string> FileLists = new(StringComparer.Ordinal)
    {
        ["UnknownWorld"] = "archive-metadata-unknownworld.json",
        ["Medicusc1939_2"] = "archive-metadata-medicusc1939-2.json",
        ["radarmen1"] = "archive-metadata-radarmen1-without-mp4.json",
    };

    [Fact]
    public async Task TheLicenceClauseIsInTheQuery()
    {
        var archive = new Archive();
        using var source = Source(archive);

        await source.SearchAsync("rocketship", new VideoFilters(), 1, 25, TestContext.Current.CancellationToken);

        var query = HttpUtility.ParseQueryString(archive.Searches.Single().Query)["q"];
        Assert.Equal(
            "(rocketship) AND mediatype:movies AND (licenseurl:*publicdomain* OR licenseurl:*creativecommons*)",
            query);
    }

    [Fact]
    public async Task TypedOperatorsCannotReachPastTheLicenceClause()
    {
        var archive = new Archive();
        using var source = Source(archive);

        await source.SearchAsync("x) OR (mediatype:* \\", new VideoFilters(VideoKind.FeatureFilm), 1, 25, TestContext.Current.CancellationToken);

        var query = HttpUtility.ParseQueryString(archive.Searches.Single().Query)["q"];
        Assert.Equal(
            @"(x\) OR \(mediatype\:\* \\) AND mediatype:movies AND collection:feature_films AND (licenseurl:*publicdomain* OR licenseurl:*creativecommons*)",
            query);
    }

    [Fact]
    public void RangeCharactersTheServiceCannotEscapeBecomeSpaces() =>
        Assert.Equal(
            "(year 1950 AND downloads  10) AND mediatype:movies AND (licenseurl:*publicdomain* OR licenseurl:*creativecommons*)",
            InternetArchiveVideoSource.Query("year>1950 AND downloads<=10", kind: null));

    [Fact]
    public async Task AResultWithoutAFreeLicenceOrAUsableIdentifierIsDropped()
    {
        using var source = Source(new Archive());

        var page = await source.SearchAsync("rocketship", new VideoFilters(), 1, 25, TestContext.Current.CancellationToken);

        Assert.Equal(["UnknownWorld", "TeenagersFromOuterSpace1959", "Medicusc1939_2", "radarmen1"], page.Titles.Select(title => title.Id));
        Assert.Equal(29, page.Total);
        Assert.Null(page.Problem);
    }

    [Fact]
    public async Task TheFullMp4IsChosenWithItsLengthKindAndLicence()
    {
        using var source = Source(new Archive());

        var page = await source.SearchAsync("rocketship", new VideoFilters(), 1, 25, TestContext.Current.CancellationToken);

        var title = page.Titles[0];
        Assert.Equal(VideoSources.InternetArchive, title.Source);
        Assert.Equal("Unknown World", title.Title);
        Assert.Equal(VideoKind.FeatureFilm, title.Kind);
        Assert.Equal("public domain", title.Licence);
        Assert.Equal("Irving Block, Jack Rabin", title.Credit);
        Assert.Equal(TimeSpan.FromSeconds(4437.37), title.Length);
        Assert.Equal(new Uri("https://archive.org/download/UnknownWorld/UnknownWorld.mp4"), title.Media);
        Assert.Null(title.Subtitles);
        Assert.True(title.Playable);
    }

    [Fact]
    public async Task TheArchivesOwnH264IsChosenAndAnEmptySubtitleFileIsPassedOver()
    {
        using var source = Source(new Archive());

        var page = await source.SearchAsync("rocketship", new VideoFilters(), 1, 25, TestContext.Current.CancellationToken);

        var title = page.Titles.Single(title => title.Id == "Medicusc1939_2");
        Assert.Equal(VideoKind.Footage, title.Kind);
        Assert.Equal(new Uri("https://archive.org/download/Medicusc1939_2/Medicusc1939_2.asr.vtt"), title.Subtitles);
        Assert.Equal(new Uri("https://archive.org/download/Medicusc1939_2/Medicusc1939_2.mp4"), title.Media);
    }

    [Fact]
    public async Task ATitleWithNoMp4IsMarkedUnplayableWithTheReason()
    {
        using var source = Source(new Archive());

        var page = await source.SearchAsync("rocketship", new VideoFilters(), 1, 25, TestContext.Current.CancellationToken);

        var title = page.Titles.Single(title => title.Id == "radarmen1");
        Assert.False(title.Playable);
        Assert.Null(title.Media);
        Assert.Equal(VideoKind.ShortFilm, title.Kind);
        Assert.Equal("Offered as QuickTime, Ogg Video and MPEG2 only, with no MP4 D47 can play.", title.Unplayable);
    }

    [Fact]
    public async Task AFileListThatCannotBeReadMarksOnlyThatTitleUnplayable()
    {
        using var source = Source(new Archive());

        var page = await source.SearchAsync("rocketship", new VideoFilters(), 1, 25, TestContext.Current.CancellationToken);

        var title = page.Titles.Single(title => title.Id == "TeenagersFromOuterSpace1959");
        Assert.False(title.Playable);
        Assert.Equal("Its list of files could not be read from the Internet Archive.", title.Unplayable);
        Assert.True(page.Titles[0].Playable);
    }

    [Fact]
    public async Task ThePageAndItsSizeAndTheOrderReachTheRequest()
    {
        var archive = new Archive();
        using var source = Source(archive);

        await source.SearchAsync("rocketship", new VideoFilters(Order: VideoOrder.MostWatched), 3, 9, TestContext.Current.CancellationToken);

        var query = HttpUtility.ParseQueryString(archive.Searches.Single().Query);
        Assert.Equal("3", query["page"]);
        Assert.Equal("9", query["rows"]);
        Assert.Equal("downloads desc", query["sort[]"]);
        Assert.Equal("json", query["output"]);
    }

    [Fact]
    public async Task AnArchiveErrorAnswerIsAProblemNotAnEmptyCatalogue()
    {
        using var source = Source(new Archive(search: """{"error":"[BACKEND_ERROR] Invalid or no response from Elasticsearch"}"""));

        var page = await source.SearchAsync("rocketship", new VideoFilters(), 1, 25, TestContext.Current.CancellationToken);

        Assert.Empty(page.Titles);
        Assert.Equal("The Internet Archive could not be searched: it answered with an error.", page.Problem);
    }

    [Fact]
    public void EveryDownloadIsOnArchiveOrgWithEachPartEscaped()
    {
        Assert.Equal(
            new Uri("https://archive.org/download/An_Item/sub%20folder/a%23b.mp4"),
            InternetArchiveVideoSource.Download("An_Item", "sub folder/a#b.mp4"));
        Assert.Null(InternetArchiveVideoSource.Download("An_Item", "../other/a.mp4"));
        Assert.Null(InternetArchiveVideoSource.Download("../x", "a.mp4"));
        Assert.Null(InternetArchiveVideoSource.Download("An_Item", "a//b.mp4"));
    }

    private static InternetArchiveVideoSource Source(Archive archive) =>
        new(NullLogger<InternetArchiveVideoSource>.Instance, new HttpClient(archive));

    /// <summary>Answers the search with the recorded fixture and each item's file list by identifier; any other is 404.</summary>
    private sealed class Archive(string? search = null) : HttpMessageHandler
    {
        public List<Uri> Searches { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var uri = request.RequestUri!;
            Assert.Equal("https", uri.Scheme);
            Assert.Equal("archive.org", uri.Host);

            if (uri.AbsolutePath == "/advancedsearch.php")
            {
                lock (Searches)
                {
                    Searches.Add(uri);
                }

                return Task.FromResult(Ok(search ?? Fixture.Text("archive-search-rocketship.json")));
            }

            var identifier = uri.AbsolutePath["/metadata/".Length..];

            return Task.FromResult(FileLists.TryGetValue(identifier, out var name)
                ? Ok(Fixture.Text(name))
                : new HttpResponseMessage(HttpStatusCode.NotFound));
        }

        private static HttpResponseMessage Ok(string text) => new(HttpStatusCode.OK) { Content = new StringContent(text) };
    }
}
