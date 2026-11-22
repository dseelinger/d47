using D47.Core.Podcasts;
using Xunit;

namespace D47.Core.Tests.Podcasts;

public sealed class APodcastFeedIsReadFromItsTextTests
{
    private static readonly Uri Feed = new("https://example.test/shows/night/feed.xml");

    private const string Header =
        "<?xml version=\"1.0\"?><rss version=\"2.0\" xmlns:itunes=\"http://www.itunes.com/dtds/podcast-1.0.dtd\"><channel><title>Night Watch</title>";

    private static string Rss(params string[] items) => Header + string.Concat(items) + "</channel></rss>";

    private static string Item(string enclosure, string extra = "") =>
        $"<item><title>Episode</title>{extra}{enclosure}</item>";

    private static string Audio(string url = "https://cdn.example.test/a.mp3") =>
        $"<enclosure url=\"{url}\" type=\"audio/mpeg\" length=\"1234\"/>";

    [Fact]
    public void AnItunesFeedGivesItsTitleAndEpisodesNewestFirst()
    {
        var show = PodcastFeed.Parse(
            Rss(
                "<item><title>Old</title><guid>g1</guid><pubDate>Mon, 01 Jan 2024 10:00:00 GMT</pubDate>" + Audio() + "<itunes:duration>90</itunes:duration></item>",
                "<item><title>New</title><guid>g2</guid><pubDate>Tue, 02 Jan 2024 10:00:00 GMT</pubDate>" + Audio("https://cdn.example.test/b.mp3") + "</item>"),
            Feed);

        Assert.Equal("Night Watch", show.Title);
        Assert.Equal(["New", "Old"], show.Episodes.Select(episode => episode.Title));

        var old = show.Episodes[1];
        Assert.Equal("g1", old.Id);
        Assert.Equal(new Uri("https://cdn.example.test/a.mp3"), old.Enclosure);
        Assert.Equal("audio/mpeg", old.MediaType);
        Assert.Equal(1234, old.Length);
        Assert.Equal(TimeSpan.FromSeconds(90), old.Duration);
        Assert.Equal(new DateTimeOffset(2024, 1, 1, 10, 0, 0, TimeSpan.Zero), old.Published);
        Assert.Null(show.Episodes[0].Duration);
    }

    [Fact]
    public void AnItemWithNoGuidIsIdentifiedByItsEnclosureUrl()
    {
        var show = PodcastFeed.Parse(Rss(Item(Audio())), Feed);

        Assert.Equal("https://cdn.example.test/a.mp3", show.Episodes.Single().Id);
    }

    [Fact]
    public void ARelativeEnclosureIsResolvedAgainstTheFeedUrl()
    {
        var show = PodcastFeed.Parse(Rss(Item("<enclosure url=\"audio/one.mp3\" type=\"audio/mpeg\"/>")), Feed);

        Assert.Equal(new Uri("https://example.test/shows/night/audio/one.mp3"), show.Episodes.Single().Enclosure);
    }

    [Fact]
    public void AVideoEnclosureAndAnItemWithNoEnclosureAreSkipped()
    {
        var show = PodcastFeed.Parse(
            Rss(
                Item("<enclosure url=\"https://cdn.example.test/v.mp4\" type=\"video/mp4\"/>"),
                "<item><title>Text only</title></item>",
                Item(Audio())),
            Feed);

        Assert.Single(show.Episodes);
    }

    [Theory]
    [InlineData("754", 754)]
    [InlineData("12:34", 754)]
    [InlineData("1:02:03", 3723)]
    public void ADurationIsReadInItsThreeForms(string text, double seconds)
    {
        var show = PodcastFeed.Parse(Rss(Item(Audio(), $"<itunes:duration>{text}</itunes:duration>")), Feed);

        Assert.Equal(TimeSpan.FromSeconds(seconds), show.Episodes.Single().Duration);
    }

    [Fact]
    public void ADurationThatIsNotATimeIsLeftOut()
    {
        var show = PodcastFeed.Parse(Rss(Item(Audio(), "<itunes:duration>soon</itunes:duration>")), Feed);

        Assert.Null(show.Episodes.Single().Duration);
    }

    [Fact]
    public void AnAtomFeedIsRefused()
    {
        var exception = Assert.Throws<PodcastFeedException>(() => PodcastFeed.Parse(
            "<feed xmlns=\"http://www.w3.org/2005/Atom\"><title>x</title></feed>",
            Feed));

        Assert.Equal("This is not an RSS feed.", exception.Message);
    }

    [Fact]
    public void MalformedXmlIsRefusedWithASentence()
    {
        var exception = Assert.Throws<PodcastFeedException>(() => PodcastFeed.Parse("<rss><channel>", Feed));

        Assert.EndsWith(".", exception.Message);
        Assert.DoesNotContain("Line", exception.Message);
    }

    [Fact]
    public void AFeedCarryingADtdIsRefusedNotExpanded()
    {
        const string xml =
            "<?xml version=\"1.0\"?><!DOCTYPE rss [<!ENTITY big \"aaaa\">]><rss version=\"2.0\"><channel><title>&big;</title></channel></rss>";

        Assert.Throws<PodcastFeedException>(() => PodcastFeed.Parse(xml, Feed));
    }
}
