using D47.Core.Stories;
using Xunit;

namespace D47.App.Tests.Stories;

public sealed class AVoteCarriesTheVoterAndReplacesTheAverageTests
{
    private static Story Picked(string id) => new() { Id = id, Title = id, PublicLayer = "-" };

    [Fact]
    public async Task ARatingIsAPutWithTheFormatTheVoterAndTheStars()
    {
        using var worker = new RatingsWorker
        {
            List = """{"format":1,"stories":{"a":{"average":2,"count":1}}}""",
            Reply = """{"average":3,"count":2}""",
        };
        var stories = StoryStore.InMemory();
        stories.Save("F1", Picked("a"));
        var client = worker.Client(stories);
        await client.Open();

        Assert.True(await client.Rate("F1", "a", 4));

        var vote = worker.Requests[^1];
        Assert.Equal(("PUT", "/ratings/a", "1"), (vote.Method, vote.Path, vote.Format));
        Assert.Equal(stories.Voter("F1"), vote.Voter);
        Assert.Matches("^[0-9a-f]{32}$", vote.Voter!);
        Assert.Equal("""{"stars":4}""", vote.Body);
        Assert.Equal(new StoryRating(3, 2), client.Ratings.Get("a"));
        Assert.Equal(4, stories.Find("F1", "a")!.Rating);
        Assert.False(stories.Find("F1", "a")!.RatingPending);
    }

    [Fact]
    public async Task ClearingIsADeleteAndALastVoteWithdrawnLeavesNoAverage()
    {
        using var worker = new RatingsWorker { Reply = """{"average":4,"count":1}""" };
        var stories = StoryStore.InMemory();
        stories.Save("F1", Picked("a"));
        var client = worker.Client(stories);

        await client.Rate("F1", "a", 4);
        worker.Reply = """{"average":0,"count":0}""";

        Assert.True(await client.Clear("F1", "a"));

        var withdrawal = worker.Requests[^1];
        Assert.Equal(("DELETE", "/ratings/a", stories.Voter("F1"), null), (withdrawal.Method, withdrawal.Path, withdrawal.Voter, withdrawal.Body));
        Assert.Null(client.Ratings.Get("a"));
        Assert.Null(stories.Find("F1", "a")!.Rating);
        Assert.False(stories.Find("F1", "a")!.RatingPending);
    }

    [Fact]
    public async Task AStoryTheCommanderHasNotPickedIsNotRated()
    {
        using var worker = new RatingsWorker();
        var client = worker.Client(StoryStore.InMemory());

        Assert.False(await client.Rate("F1", "a", 5));
        Assert.Empty(worker.Requests);
    }
}
