using D47.Core.Viewing;
using Xunit;

namespace D47.Core.Tests.Viewing;

public class TheBlenderFilmsAreSearchedOnThisPcTests
{
    [Fact]
    public void EveryFilmIsPlayableFromDownloadBlenderOrgWithItsCreditAndLicence()
    {
        Assert.NotEmpty(BlenderOpenMovies.Films);

        foreach (var (title, _, _) in BlenderOpenMovies.Films)
        {
            Assert.Equal(VideoSources.Blender, title.Source);
            Assert.True(title.Playable);
            Assert.StartsWith("CC BY ", title.Licence, StringComparison.Ordinal);
            Assert.False(string.IsNullOrWhiteSpace(title.Credit));

            foreach (var uri in ((Uri?[])[title.Media, title.Subtitles]).OfType<Uri>())
            {
                Assert.Equal("https", uri.Scheme);
                Assert.Equal(VideoSources.BlenderHost, uri.Host);
            }
        }
    }

    [Fact]
    public async Task ASearchMatchesTheTitleAndItsWords()
    {
        var films = new BlenderOpenMovies();

        var steel = await films.SearchAsync("steel", new VideoFilters(), 1, 25, TestContext.Current.CancellationToken);
        var robots = await films.SearchAsync("Robots", new VideoFilters(), 1, 25, TestContext.Current.CancellationToken);
        var none = await films.SearchAsync("rocketship", new VideoFilters(), 1, 25, TestContext.Current.CancellationToken);

        Assert.Equal(["tears-of-steel"], steel.Titles.Select(title => title.Id));
        Assert.Equal(["tears-of-steel"], robots.Titles.Select(title => title.Id));
        Assert.Empty(none.Titles);
        Assert.Equal(0, none.Total);
    }

    [Fact]
    public async Task AKindTheFilmsAreNotGivesNothing()
    {
        var page = await new BlenderOpenMovies().SearchAsync(string.Empty, new VideoFilters(VideoKind.FeatureFilm), 1, 25, TestContext.Current.CancellationToken);

        Assert.Empty(page.Titles);
    }

    [Fact]
    public async Task PagingCountsTheWholeList()
    {
        var films = new BlenderOpenMovies();

        var first = await films.SearchAsync(string.Empty, new VideoFilters(Order: VideoOrder.Newest), 1, 1, TestContext.Current.CancellationToken);
        var second = await films.SearchAsync(string.Empty, new VideoFilters(Order: VideoOrder.Newest), 2, 1, TestContext.Current.CancellationToken);

        Assert.Equal(BlenderOpenMovies.Films.Count, first.Total);
        Assert.Equal("tears-of-steel", Assert.Single(first.Titles).Id);
        Assert.Equal("elephants-dream", Assert.Single(second.Titles).Id);
    }
}
