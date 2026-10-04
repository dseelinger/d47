using D47.Core.Stories;
using Xunit;

namespace D47.Core.Tests.Stories;

public sealed class StoryRatingsAreRoundedToHalfStarsTests
{
    private const string Body = """{"format":1,"stories":{"a-story":{"average":4.25,"count":12},"b-story":{"average":3.5,"count":2}}}""";

    [Theory]
    [InlineData(3.74, 3.5)]
    [InlineData(3.75, 4.0)]
    [InlineData(1.0, 1.0)]
    [InlineData(4.99, 5.0)]
    [InlineData(4.24, 4.0)]
    public void StarsAreTheAverageToTheNearestHalf(double average, double stars) =>
        Assert.Equal(stars, new StoryRating(average, 3).Stars);

    [Fact]
    public void TheWorkersBodyIsParsedByStoryId()
    {
        var ratings = StoryRatings.Parse(Body)!;

        Assert.Equal(new StoryRating(4.25, 12), ratings.Get("a-story"));
        Assert.Equal(new StoryRating(3.5, 2), ratings.Get("B-STORY"));
        Assert.Null(ratings.Get("no-votes"));
    }

    [Theory]
    [InlineData("""{"format":2,"stories":{}}""")]
    [InlineData("""{"stories":{}}""")]
    [InlineData("""{"format":"1","stories":{}}""")]
    [InlineData("""{"format":1}""")]
    [InlineData("not json")]
    public void AnotherFormatOrAMalformedBodyIsRefused(string body) => Assert.Null(StoryRatings.Parse(body));

    [Fact]
    public void AStoryWithoutAUsableRatingIsLeftOut()
    {
        var ratings = StoryRatings.Parse("""{"format":1,"stories":{"zero":{"average":4,"count":0},"high":{"average":6,"count":1},"ok":{"average":2,"count":1}}}""")!;

        Assert.Equal(1, ratings.Count);
        Assert.NotNull(ratings.Get("ok"));
    }

    [Fact]
    public void WithReplacesOneStoryAndLeavesTheOriginalAlone()
    {
        var before = StoryRatings.Parse(Body)!;

        var after = before.With("a-story", new StoryRating(1, 13)).With("c-story", new StoryRating(5, 1));

        Assert.Equal(new StoryRating(1, 13), after.Get("a-story"));
        Assert.Equal(new StoryRating(3.5, 2), after.Get("b-story"));
        Assert.Equal(new StoryRating(5, 1), after.Get("c-story"));
        Assert.Equal(new StoryRating(4.25, 12), before.Get("a-story"));
        Assert.Null(before.Get("c-story"));
    }
}
