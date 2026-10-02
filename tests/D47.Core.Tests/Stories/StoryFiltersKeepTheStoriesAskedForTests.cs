using D47.Core.Stories;
using Xunit;

namespace D47.Core.Tests.Stories;

public sealed class StoryFiltersKeepTheStoriesAskedForTests
{
    private static readonly string[] Lengths = [.. StoryPacing.All.Select(pacing => pacing.Key), "no-such-length"];

    private static List<StoryCard> Cards() =>
    [
        .. from level in StoryCard.Levels
           from length in Lengths
           select new StoryCard
           {
               Id = $"{level}-{length}",
               Title = $"{level} {length}",
               Genre = "drama",
               Level = level,
               Length = length,
               Core = "covas",
               Blurb = "-",
               InYourWords = "-",
           },
    ];

    private static IEnumerable<string> Kept(StoryFilter filter) => Cards().Where(filter.Matches).Select(card => card.Id);

    private static string[] LengthsKept(StoryFilter filter) => [.. Cards().Where(filter.Matches).Select(card => card.Length!).Distinct()];

    [Fact]
    public void AtLeastOneMonthKeepsTheLongerStories()
    {
        var kept = LengthsKept(new StoryFilter(null, StoryLengthCompare.AtLeast, "1-month"));

        Assert.Equal(["1-month", "3-months", "6-months", "1-year", "no-such-length"], kept);
        Assert.DoesNotContain("2-weeks", kept);
    }

    [Fact]
    public void AtMostOneWeekKeepsTheShorterStories()
    {
        var kept = LengthsKept(new StoryFilter(null, StoryLengthCompare.AtMost, "1-week"));

        Assert.Equal(["3-days", "1-week"], kept);
    }

    [Fact]
    public void ExactlyTwoWeeksKeepsOnlyTwoWeeks()
    {
        Assert.Equal(["2-weeks"], LengthsKept(new StoryFilter(null, StoryLengthCompare.Exactly, "2-weeks")));
    }

    [Fact]
    public void EndgameKeepsOnlyEndgameAndALengthNarrowsIt()
    {
        Assert.All(Cards().Where(new StoryFilter("endgame").Matches), card => Assert.Equal("endgame", card.Level));
        Assert.Equal(Lengths.Length, Kept(new StoryFilter("endgame")).Count());

        var narrowed = Cards().Where(new StoryFilter("endgame", StoryLengthCompare.AtMost, "1-month").Matches).ToList();

        Assert.All(narrowed, card => Assert.Equal("endgame", card.Level));
        Assert.Equal(["3-days", "1-week", "2-weeks", "1-month"], narrowed.Select(card => card.Length));
    }

    [Fact]
    public void ACardWithAnUnknownLengthCountsAsAYear()
    {
        Assert.Contains("midrange-no-such-length", Kept(new StoryFilter(null, StoryLengthCompare.AtLeast, "1-year")));
        Assert.DoesNotContain("midrange-no-such-length", Kept(new StoryFilter(null, StoryLengthCompare.AtMost, "6-months")));
    }

    [Fact]
    public void AnUnknownFilterLengthKeepsEveryLength()
    {
        Assert.Equal(Cards().Count, Kept(new StoryFilter(null, StoryLengthCompare.AtLeast, "no-such-length")).Count());
    }

    [Fact]
    public void TheDefaultFilterKeepsEveryCard()
    {
        Assert.Equal(Cards().Count, Kept(new StoryFilter()).Count());
        Assert.True(new StoryFilter().IsDefault);
    }
}
