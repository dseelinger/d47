using D47.Core.Configuration;
using D47.Core.Podcasts;
using Xunit;

namespace D47.Core.Tests.Podcasts;

public class APodcastSearchAnswerIsReadFromItsTextTests
{
    private const string Recorded = """
        {
         "resultCount":3,
         "results": [
          {"wrapperType":"track", "kind":"podcast", "collectionId":1, "artistName":"Pilots Federation", "collectionName":"Lave Radio",
           "feedUrl":"https://feeds.example/lave.xml", "trackCount":412, "artworkUrl600":"https://art.example/lave/600x600bb.jpg",
           "primaryGenreName":"Games"},
          {"wrapperType":"track", "kind":"podcast", "collectionId":2, "artistName":"Apple Only", "collectionName":"No Feed Here",
           "trackCount":12, "artworkUrl600":"https://art.example/nofeed/600x600bb.jpg"},
          {"wrapperType":"track", "kind":"podcast", "collectionId":3, "artistName":"Old Station", "collectionName":"Plain Feed",
           "feedUrl":"http://other.example/rss"}
         ]
        }
        """;

    [Fact]
    public void EachResultCarriesItsTitleAuthorFeedEpisodeCountAndArtwork()
    {
        var results = PodcastSearch.Parse(Recorded);

        var first = results[0];
        Assert.Equal("Lave Radio", first.Title);
        Assert.Equal("Pilots Federation", first.Author);
        Assert.Equal(new Uri("https://feeds.example/lave.xml"), first.Feed);
        Assert.Equal(412, first.EpisodeCount);
        Assert.Equal(new Uri("https://art.example/lave/600x600bb.jpg"), first.Artwork);
    }

    [Fact]
    public void AResultWithoutAFeedUrlIsDropped()
    {
        var results = PodcastSearch.Parse(Recorded);

        Assert.Equal(["Lave Radio", "Plain Feed"], results.Select(result => result.Title));
        Assert.Null(results[1].EpisodeCount);
        Assert.Null(results[1].Artwork);
    }

    [Fact]
    public void AResultWhoseFeedIsNotHttpOrHttpsIsDropped()
    {
        var results = PodcastSearch.Parse("""{"results":[{"collectionName":"Local","feedUrl":"file:///c:/feed.xml"},{"collectionName":"Odd","feedUrl":"not a url"}]}""");

        Assert.Empty(results);
    }

    [Fact]
    public void AnEmptyResultsListGivesNoResults()
    {
        Assert.Empty(PodcastSearch.Parse("""{"resultCount":0,"results":[]}"""));
    }

    [Fact]
    public void AnAnswerThatIsNotJsonSaysSo()
    {
        var exception = Assert.Throws<PodcastSearchException>(() => PodcastSearch.Parse("<html>busy</html>"));

        Assert.Equal("The podcast directory's answer could not be read.", exception.Message);
    }

    [Fact]
    public void AddingAFeedPutsItInFeedsOnce()
    {
        var feed = new Uri("https://feeds.example/lave.xml");

        var once = new PodcastSettings().WithFeed(feed);
        var twice = once.WithFeed(feed);

        Assert.Equal(["https://feeds.example/lave.xml"], once.Feeds);
        Assert.Same(once, twice);
    }

    [Fact]
    public void AFeedAlreadyAddedWithSpacesAroundItIsNotAddedAgain()
    {
        var settings = new PodcastSettings { Feeds = [" https://feeds.example/lave.xml "] };

        Assert.Same(settings, settings.WithFeed(new Uri("https://feeds.example/lave.xml")));
    }

    [Fact]
    public void AFeedThatIsNotHttpOrHttpsIsNotAdded()
    {
        var settings = new PodcastSettings();

        Assert.Same(settings, settings.WithFeed(new Uri("file:///c:/feed.xml")));
    }
}
