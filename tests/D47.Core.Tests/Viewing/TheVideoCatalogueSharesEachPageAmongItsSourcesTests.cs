using D47.Core.Configuration;
using D47.Core.Viewing;
using Xunit;

namespace D47.Core.Tests.Viewing;

/// <summary>
/// The catalogue asks each ticked source for its own page N of a share of 25, interleaves the shares, and sums the
/// totals; a source that fails leaves the others' results and says so.
/// </summary>
public class TheVideoCatalogueSharesEachPageAmongItsSourcesTests
{
    [Fact]
    public async Task ThreeSourcesShareTwentyFiveAsNineEightAndEight()
    {
        var archive = new Source(VideoSources.InternetArchive, 100);
        var nasa = new Source(VideoSources.Nasa, 40);
        var blender = new Source(VideoSources.Blender, 2);
        var catalogue = new VideoCatalogue([blender, nasa, archive], () => VideoSources.All);

        var page = await catalogue.SearchAsync("moon", new VideoFilters(), 2, TestContext.Current.CancellationToken);

        Assert.Equal((2, 9), archive.Asked.Single());
        Assert.Equal((2, 8), nasa.Asked.Single());
        Assert.Equal((2, 8), blender.Asked.Single());
        Assert.Equal(142, page.Total);
        Assert.Equal(
            ["archive-9", "nasa-8", "archive-10", "nasa-9", "archive-11", "nasa-10"],
            page.Titles.Take(6).Select(title => title.Id));
        Assert.Equal(9 + 8, page.Titles.Count);
        Assert.Null(page.Problem);
    }

    [Fact]
    public async Task PagingGoesOnUntilTheLongestShareRunsOut()
    {
        var catalogue = new VideoCatalogue(
            [new Source(VideoSources.InternetArchive, 100), new Source(VideoSources.Nasa, 40), new Source(VideoSources.Blender, 2)],
            () => VideoSources.All);

        var seventh = await catalogue.SearchAsync("moon", new VideoFilters(), 7, TestContext.Current.CancellationToken);
        var eleventh = await catalogue.SearchAsync("moon", new VideoFilters(), 11, TestContext.Current.CancellationToken);
        var twelfth = await catalogue.SearchAsync("moon", new VideoFilters(), 12, TestContext.Current.CancellationToken);

        Assert.Equal(Enumerable.Range(54, 9).Select(index => $"archive-{index}"), seventh.Titles.Select(title => title.Id));
        Assert.True(seventh.More);
        Assert.True(eleventh.More);
        Assert.Equal(["archive-99"], twelfth.Titles.Select(title => title.Id));
        Assert.False(twelfth.More);
    }

    [Fact]
    public async Task OneSourceGivesAWholePageOfTwentyFive()
    {
        var nasa = new Source(VideoSources.Nasa, 40);
        var catalogue = new VideoCatalogue([nasa], () => [VideoSources.Nasa]);

        var page = await catalogue.SearchAsync("moon", new VideoFilters(), 1, TestContext.Current.CancellationToken);

        Assert.Equal((1, 25), nasa.Asked.Single());
        Assert.Equal(25, page.Titles.Count);
        Assert.Equal(40, page.Total);
    }

    [Fact]
    public async Task ASourceNotTickedIsNotAsked()
    {
        var archive = new Source(VideoSources.InternetArchive, 100);
        var nasa = new Source(VideoSources.Nasa, 40);
        var settings = new D47Settings { Watch = new WatchSettings { Sources = [VideoSources.Nasa, "loc", VideoSources.Nasa] } };
        var catalogue = new VideoCatalogue([archive, nasa], settings.Watch.SearchedSources);

        await catalogue.SearchAsync("moon", new VideoFilters(), 1, TestContext.Current.CancellationToken);

        Assert.Empty(archive.Asked);
        Assert.Equal((1, 25), nasa.Asked.Single());
    }

    [Fact]
    public async Task AFailingSourceLeavesTheOthersAndSaysSo()
    {
        var archive = new Source(VideoSources.InternetArchive, 100, problem: "The Internet Archive could not be searched: it answered 503.");
        var nasa = new Source(VideoSources.Nasa, 40, throws: true);
        var blender = new Source(VideoSources.Blender, 2);
        var catalogue = new VideoCatalogue([archive, nasa, blender], () => VideoSources.All);

        var page = await catalogue.SearchAsync("moon", new VideoFilters(), 1, TestContext.Current.CancellationToken);

        Assert.Equal(["blender-0", "blender-1"], page.Titles.Select(title => title.Id));
        Assert.Equal(2, page.Total);
        Assert.Equal(
            "The Internet Archive could not be searched: it answered 503. NASA Image and Video Library could not be searched.",
            page.Problem);
    }

    [Fact]
    public async Task NoSourceAskedSearchesNothing()
    {
        var archive = new Source(VideoSources.InternetArchive, 100);
        var catalogue = new VideoCatalogue([archive], () => []);

        var page = await catalogue.SearchAsync("moon", new VideoFilters(), 1, TestContext.Current.CancellationToken);

        Assert.Empty(archive.Asked);
        Assert.Empty(page.Titles);
        Assert.Equal("No video source is ticked, so nothing was searched.", page.Problem);
    }

    /// <summary>A source holding <paramref name="holds"/> titles named by their index.</summary>
    private sealed class Source(string id, int holds, string? problem = null, bool throws = false) : IVideoSource
    {
        public List<(int Page, int Size)> Asked { get; } = [];

        public string Id => id;

        public Task<VideoPage> SearchAsync(string query, VideoFilters filters, int page, int pageSize, CancellationToken cancellationToken)
        {
            Asked.Add((page, pageSize));

            if (throws)
            {
                throw new InvalidOperationException("The source failed.");
            }

            if (problem is not null)
            {
                return Task.FromResult(VideoPage.Empty with { Problem = problem });
            }

            var titles = Enumerable.Range((page - 1) * pageSize, Math.Max(0, Math.Min(pageSize, holds - ((page - 1) * pageSize))))
                .Select(index => new VideoTitle(id, $"{id}-{index}", $"Title {index}", VideoKind.Other, "public domain", null, null, null))
                .ToList();

            return Task.FromResult(new VideoPage(titles, holds));
        }
    }
}
