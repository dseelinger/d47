using D47.Core.Stories;
using Xunit;

namespace D47.App.Tests.Stories;

public sealed class TurningStoryRatingsOffSendsNothingTests
{
    [Fact]
    public async Task NeitherTheAveragesNorAVoteNorAPendingVoteIsSent()
    {
        using var worker = new RatingsWorker();
        var stories = StoryStore.InMemory();
        stories.Save("F1", new Story { Id = "a", Title = "a", PublicLayer = "-", Rating = 3, RatingPending = true });
        stories.Save("F1", new Story { Id = "b", Title = "b", PublicLayer = "-" });
        var client = worker.Client(stories, allowed: false);

        Assert.Null(await client.Open());
        Assert.False(await client.Rate("F1", "b", 4));
        Assert.False(await client.Clear("F1", "a"));

        Assert.Empty(worker.Requests);
        Assert.False(client.Enabled);
        Assert.Null(stories.Find("F1", "b")!.Rating);
        Assert.Null(stories.Voter("F1"));
    }
}
