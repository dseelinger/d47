using D47.Core.Configuration;
using D47.Core.Podcasts;
using Xunit;

namespace D47.Core.Tests.Podcasts;

/// <summary>The podcast page marks each episode finished, in progress, new or nothing from the listening record (#2659).</summary>
public sealed class AnEpisodeIsMarkedFromTheListeningRecordTests
{
    private static readonly Uri Feed = new("https://example.test/feed.xml");

    private static PodcastEpisode Episode(int number) =>
        new($"e{number}", $"Episode {number}", new DateTimeOffset(2026, 1, number, 0, 0, 0, TimeSpan.Zero), new Uri($"https://cdn.example.test/{number}.mp3"), "audio/mpeg", null, TimeSpan.FromHours(1));

    private static readonly PodcastShow Show = new("Deep Space Hour", [Episode(1), Episode(5), Episode(3), Episode(4), Episode(2)]);

    private static PodcastEpisodeMark[] Marks(PodcastListening listening) =>
        [.. PodcastBrowse.Episodes(Feed, Show, listening).Select(line => line.Mark)];

    [Fact]
    public void EpisodesComeNewestFirst()
    {
        var order = PodcastBrowse.Episodes(Feed, Show, new PodcastListening()).Select(line => line.Episode.Id);

        Assert.Equal(["e5", "e4", "e3", "e2", "e1"], order);
    }

    [Fact]
    public void AShowNothingWasStartedInHasOnlyItsNewestEpisodeNew()
    {
        var marks = Marks(new PodcastListening());

        Assert.Equal(
            [PodcastEpisodeMark.New, PodcastEpisodeMark.None, PodcastEpisodeMark.None, PodcastEpisodeMark.None, PodcastEpisodeMark.None],
            marks);
    }

    [Fact]
    public void EpisodesNewerThanTheNewestStartedOneAreNew()
    {
        var listening = new PodcastListening()
            .With(Feed, "e3", TimeSpan.FromMinutes(12), finished: false)
            .With(Feed, "e1", TimeSpan.Zero, finished: true);

        var lines = PodcastBrowse.Episodes(Feed, Show, listening);

        Assert.Equal(
            [PodcastEpisodeMark.New, PodcastEpisodeMark.New, PodcastEpisodeMark.InProgress, PodcastEpisodeMark.None, PodcastEpisodeMark.Finished],
            lines.Select(line => line.Mark));
        Assert.Equal(TimeSpan.FromMinutes(12), lines[2].Position);
        Assert.Equal(2, PodcastBrowse.NewCount(lines));
    }

    [Fact]
    public void AShowListenedToUpToDateHasNothingNew()
    {
        var listening = new PodcastListening().With(Feed, "e5", TimeSpan.Zero, finished: true);

        Assert.Equal(0, PodcastBrowse.NewCount(PodcastBrowse.Episodes(Feed, Show, listening)));
    }

    [Fact]
    public void AnotherFeedsRecordIsNotRead()
    {
        var listening = new PodcastListening().With(new Uri("https://example.test/other.xml"), "e5", TimeSpan.Zero, finished: true);

        Assert.Equal(PodcastEpisodeMark.New, Marks(listening)[0]);
    }

    [Fact]
    public void ForgettingAFeedDropsOnlyThatFeedsRecord()
    {
        var other = new Uri("https://example.test/other.xml");
        var listening = new PodcastListening().With(Feed, "e1", TimeSpan.FromMinutes(1), false).With(other, "e1", TimeSpan.FromMinutes(2), false);

        var kept = listening.Without(Feed);

        Assert.Null(kept.Find(Feed, "e1"));
        Assert.NotNull(kept.Find(other, "e1"));
        Assert.NotNull(listening.Find(Feed, "e1"));
    }

    [Fact]
    public void RemovingAFeedLeavesTheOthersAndChangesNothingWhenItIsNotAdded()
    {
        var settings = new PodcastSettings()
            .WithFeed(Feed)
            .WithFeed(new Uri("https://example.test/other.xml"));

        var removed = settings.WithoutFeed(Feed);

        Assert.Equal(["https://example.test/other.xml"], removed.Feeds);
        Assert.Same(removed, removed.WithoutFeed(Feed));
    }
}
