using D47.App.Panel;
using D47.Core.Stories;
using Xunit;

namespace D47.App.Tests.Stories;

public sealed class TheAveragesAreFetchedOncePerSessionTests
{
    [Fact]
    public async Task OpeningThePageTwiceAsksForTheAveragesOnce()
    {
        using var worker = new RatingsWorker { List = """{"format":1,"stories":{"a":{"average":3.8,"count":5}}}""" };
        var client = worker.Client(StoryStore.InMemory());

        Assert.Null(await client.Open());
        Assert.Null(await client.Open());

        var asked = Assert.Single(worker.Requests);
        Assert.Equal(("GET", "/ratings", "1"), (asked.Method, asked.Path, asked.Format));
        Assert.Null(asked.Voter);
        Assert.Equal(new StoryRating(3.8, 5), client.Ratings.Get("a"));
    }

    [Fact]
    public async Task AFailedFetchLeavesEveryStoryUnratedWithNoErrorAndIsTriedAgainOnTheNextOpen()
    {
        using var worker = new RatingsWorker { Down = true };
        var client = worker.Client(StoryStore.InMemory());

        Assert.Null(await client.Open());
        Assert.Equal(0, client.Ratings.Count);

        worker.Down = false;

        Assert.Null(await client.Open());
        Assert.Equal(2, worker.Requests.Count);
    }
}
