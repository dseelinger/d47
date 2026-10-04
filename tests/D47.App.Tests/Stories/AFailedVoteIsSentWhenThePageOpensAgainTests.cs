using D47.App.Panel;
using D47.Core.Stories;
using Xunit;

namespace D47.App.Tests.Stories;

public sealed class AFailedVoteIsSentWhenThePageOpensAgainTests
{
    [Fact]
    public async Task TheVoteStaysPendingUntilTheNextOpenSendsIt()
    {
        using var worker = new RatingsWorker { Down = true, Reply = """{"average":5,"count":1}""" };
        var stories = StoryStore.InMemory();
        stories.Save("F1", new Story { Id = "a", Title = "a", PublicLayer = "-" });
        var client = worker.Client(stories);

        Assert.False(await client.Rate("F1", "a", 5));
        Assert.Equal(5, stories.Find("F1", "a")!.Rating);
        Assert.True(stories.Find("F1", "a")!.RatingPending);

        worker.Down = false;

        Assert.Null(await client.Open());

        Assert.Equal(["PUT", "GET", "PUT"], worker.Requests.Select(asked => asked.Method));
        Assert.Equal("""{"stars":5}""", worker.Requests[^1].Body);
        Assert.False(stories.Find("F1", "a")!.RatingPending);
        Assert.Equal(new StoryRating(5, 1), client.Ratings.Get("a"));
    }

    [Fact]
    public async Task AResendThatFailsSaysTheRatingIsSaved()
    {
        using var worker = new RatingsWorker { RefuseVotes = true };
        var stories = StoryStore.InMemory();
        stories.Save("F1", new Story { Id = "a", Title = "a", PublicLayer = "-", Rating = 3, RatingPending = true });
        var client = worker.Client(stories);

        Assert.Equal(StoryRatingClient.SendFailed, await client.Open());
        Assert.Equal(["GET", "PUT"], worker.Requests.Select(asked => asked.Method));
        Assert.True(stories.Find("F1", "a")!.RatingPending);
    }
}
