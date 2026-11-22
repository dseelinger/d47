using D47.Core.Podcasts;
using Xunit;

namespace D47.Core.Tests.Podcasts;

public sealed class PodcastListeningRoundTripsTests
{
    private static readonly Uri Feed = new("https://example.test/feed.xml");

    [Fact]
    public void APositionAndAFinishedEpisodeSurviveAJsonRoundTrip()
    {
        var listening = new PodcastListening()
            .With(Feed, "g1", TimeSpan.FromSeconds(125.5), false)
            .With(Feed, "g2", TimeSpan.FromMinutes(40), true);

        var read = PodcastListening.FromJson(listening.ToJson());

        Assert.Null(read.Problem);
        Assert.Equal(TimeSpan.FromSeconds(125.5), read.Listening.Find(Feed, "g1")!.Position);
        Assert.False(read.Listening.Find(Feed, "g1")!.Finished);
        Assert.True(read.Listening.Find(Feed, "g2")!.Finished);
        Assert.Null(read.Listening.Find(Feed, "other"));
    }

    [Fact]
    public void SettingAnEpisodeAgainReplacesItsPosition()
    {
        var listening = new PodcastListening()
            .With(Feed, "g1", TimeSpan.FromSeconds(10), false)
            .With(Feed, "g1", TimeSpan.FromSeconds(20), false);

        Assert.Equal(TimeSpan.FromSeconds(20), listening.Find(Feed, "g1")!.Position);
        Assert.Single(listening.All);
    }

    [Theory]
    [InlineData("")]
    [InlineData("not json")]
    [InlineData("[1, 2")]
    [InlineData("null")]
    public void UnreadableJsonGivesAnEmptyRecordAndAReason(string json)
    {
        var read = PodcastListening.FromJson(json);

        Assert.Empty(read.Listening.All);
        Assert.False(string.IsNullOrEmpty(read.Problem));
    }
}
