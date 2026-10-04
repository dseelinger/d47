using System.Text.Json;
using D47.Core.Stories;
using Xunit;

namespace D47.Core.Tests.Stories;

public sealed class StoryFiltersAndSortsUseTheRatingsTests
{
    private static StoryCard Card(string id) => new()
    {
        Id = id,
        Title = id,
        Genre = "drama",
        Level = "endgame",
        Length = "1-week",
        Core = "covas",
        Blurb = "-",
        InYourWords = "-",
    };

    private static readonly StoryRatings Ratings = StoryRatings.Empty
        .With("three-seven", new StoryRating(3.7, 50))
        .With("three-seventy-five", new StoryRating(3.75, 10))
        .With("four-few", new StoryRating(4.0, 2))
        .With("four-many", new StoryRating(4.0, 40))
        .With("five", new StoryRating(4.9, 1));

    private static readonly StoryCard[] Catalogue =
        [Card("unrated-a"), Card("three-seven"), Card("four-few"), Card("unrated-b"), Card("five"), Card("four-many"), Card("three-seventy-five")];

    private static string[] Kept(StoryFilter filter) => [.. Catalogue.Where(card => filter.Matches(card, Ratings)).Select(card => card.Id)];

    private static string[] Ordered(StoryFilter filter) => [.. filter.Order(Catalogue, Ratings).Select(card => card.Id)];

    [Fact]
    public void FourStarsAndUpKeepsAThreeSeventyFiveAverageAndDropsAThreeSevenOne()
    {
        var kept = Kept(new StoryFilter(MinStars: 4));

        Assert.Contains("three-seventy-five", kept);
        Assert.DoesNotContain("three-seven", kept);
    }

    [Fact]
    public void AStoryWithNoVotesFailsEveryMinimum()
    {
        foreach (var least in new[] { 1, 2, 3, 4 })
        {
            Assert.DoesNotContain("unrated-a", Kept(new StoryFilter(MinStars: least)));
        }
    }

    [Fact]
    public void AStoryDrawnWithFourStarsAlwaysPassesFourStarsAndUp()
    {
        var filter = new StoryFilter(MinStars: 4);

        foreach (var card in Catalogue.Where(card => Ratings.Get(card.Id) is { Stars: >= 4 }))
        {
            Assert.True(filter.Matches(card, Ratings));
        }
    }

    [Fact]
    public void HighestRatedOrdersByStarsThenCountThenCatalogueWithUnratedLast()
    {
        Assert.Equal(
            ["five", "four-many", "three-seventy-five", "four-few", "three-seven", "unrated-a", "unrated-b"],
            Ordered(new StoryFilter(Sort: StorySort.HighestRated)));
    }

    [Fact]
    public void TheCatalogueSortLeavesTheOrderAlone() =>
        Assert.Equal(Catalogue.Select(card => card.Id), Ordered(new StoryFilter()));

    [Fact]
    public void TheDefaultFilterKeepsEveryCard()
    {
        Assert.Equal(Catalogue.Length, Kept(new StoryFilter()).Length);
        Assert.Equal(Catalogue.Length, Catalogue.Count(new StoryFilter().Matches));
    }

    [Fact]
    public void OnlyNoStarsAndTheCatalogueSortMakeTheFilterDefault()
    {
        Assert.True(new StoryFilter().IsDefault);
        Assert.False(new StoryFilter(MinStars: 3).IsDefault);
        Assert.False(new StoryFilter(Sort: StorySort.HighestRated).IsDefault);
        Assert.False(new StoryFilter(Level: "endgame").IsDefault);
    }

    [Fact]
    public void ASavedFilterWithoutTheNewMembersStillLoads()
    {
        var filter = JsonSerializer.Deserialize<StoryFilter>("""{"level":"endgame","compare":2,"length":"1-week"}""", JsonSerializerOptions.Web)!;

        Assert.Equal("endgame", filter.Level);
        Assert.Null(filter.MinStars);
        Assert.Equal(StorySort.Catalogue, filter.Sort);
    }

    [Fact]
    public void TheMinimumAndTheSortRoundTrip()
    {
        var saved = new StoryFilter("endgame", MinStars: 3, Sort: StorySort.HighestRated);

        Assert.Equal(saved, JsonSerializer.Deserialize<StoryFilter>(JsonSerializer.Serialize(saved)));
    }
}
