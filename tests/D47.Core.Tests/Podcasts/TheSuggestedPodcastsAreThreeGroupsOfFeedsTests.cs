using Xunit;
using D47.Core.Podcasts;

namespace D47.Core.Tests.Podcasts;

public sealed class TheSuggestedPodcastsAreThreeGroupsOfFeedsTests
{
    [Fact]
    public void TheGroupsAreInOrderWithThreeNineAndFifteenShows()
    {
        var groups = SuggestedPodcasts.Groups;

        Assert.Equal(["Elite Dangerous", "Science fiction", "Science fact"], groups.Select(group => group.Name));
        Assert.Equal([3, 9, 15], groups.Select(group => group.Shows.Count));
    }

    [Fact]
    public void EveryFeedIsAnHttpOrHttpsAddress()
    {
        foreach (var show in SuggestedPodcasts.Groups.SelectMany(group => group.Shows))
        {
            Assert.True(show.Feed.IsAbsoluteUri && show.Feed.Scheme is "http" or "https", show.Title);
        }
    }

    [Fact]
    public void NoFeedAppearsTwice()
    {
        var feeds = SuggestedPodcasts.Groups.SelectMany(group => group.Shows).Select(show => show.Feed.AbsoluteUri).ToList();

        Assert.Equal(feeds.Count, feeds.Distinct(StringComparer.OrdinalIgnoreCase).Count());
    }

    [Fact]
    public void EveryShowHasATitleAndAnAuthor()
    {
        foreach (var show in SuggestedPodcasts.Groups.SelectMany(group => group.Shows))
        {
            Assert.False(string.IsNullOrWhiteSpace(show.Title));
            Assert.False(string.IsNullOrWhiteSpace(show.Author));
        }
    }
}
