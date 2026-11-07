using D47.Core.Diagnostics.Donation;
using D47.Core.Storage;
using D47.Core.Stories;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Stories;

public sealed class ACommandersVoteIsKeptWithTheirStoriesTests
{
    private readonly MemoryFileSystem _files = new();

    private static string StoryPath => Path.Combine(@"C:\d47-test", "story.json");

    private static Story Picked(string id) => new() { Id = id, Title = id, PublicLayer = "-" };

    private StoryStore Reopen() => StoryStore.Open(StoryPath, _files, NullLogger<StoryStore>.Instance);

    [Fact]
    public void AVoteAndItsPendingFlagSurviveARestart()
    {
        var store = Reopen();
        store.Save("F1", Picked("a") with { Rating = 4, RatingPending = true });
        store.Save("F1", Picked("b"));

        var reopened = Reopen();

        Assert.Equal(4, reopened.Find("F1", "a")!.Rating);
        Assert.True(reopened.Find("F1", "a")!.RatingPending);
        Assert.Null(reopened.Find("F1", "b")!.Rating);
        Assert.False(reopened.Find("F1", "b")!.RatingPending);
    }

    [Fact]
    public void TheVoterIsMintedOnFirstUseAndIsThirtyTwoLowercaseHexCharacters()
    {
        var store = Reopen();
        store.Save("F1", Picked("a"));

        Assert.Null(StoryFileVoters.Of(_files, StoryPath, "F1"));

        var voter = store.EnsureVoter("F1");

        Assert.Equal(32, voter.Length);
        Assert.True(DonorToken.IsWellFormed(voter));
        Assert.Equal(voter, store.EnsureVoter("F1"));
        Assert.Equal(voter, StoryFileVoters.Of(_files, StoryPath, "F1"));
        Assert.Equal(voter, Reopen().EnsureVoter("F1"));
    }

    [Fact]
    public void TheVoterIsTheSameForEveryStoryOfOneCommanderAndDifferentBetweenCommanders()
    {
        var store = Reopen();
        store.Save("F1", Picked("a"));
        store.Save("F1", Picked("b"));
        store.Save("F2", Picked("a"));

        var first = store.EnsureVoter("F1");
        store.Update("F1", "b", story => story with { Rating = 5 });
        var second = store.EnsureVoter("F2");

        Assert.Equal(first, StoryFileVoters.Of(_files, StoryPath, "F1"));
        Assert.NotEqual(first, second);
        Assert.Equal(second, StoryFileVoters.Of(_files, StoryPath, "F2"));
    }

    [Fact]
    public void AVoterIsNotTheDonorToken() =>
        Assert.NotEqual(DonorToken.NewToken(), InMemoryStories.Create().EnsureVoter("F1"));

    [Fact]
    public void AStoryFileWrittenBeforeRatingsLoadsWithNoRatingsAndNoVoter()
    {
        _files.WriteText(
            StoryPath,
            """{"commanders":[{"frontierId":"F1","stories":[{"id":"a","title":"A","publicLayer":"-","state":"running"}]}]}""");

        var store = Reopen();

        Assert.Null(store.Find("F1", "a")!.Rating);
        Assert.False(store.Find("F1", "a")!.RatingPending);
        Assert.Null(StoryFileVoters.Of(_files, StoryPath, "F1"));
    }

    [Fact]
    public void AMalformedVoterInTheFileIsDropped()
    {
        _files.WriteText(
            StoryPath,
            """{"commanders":[{"frontierId":"F1","voter":"NOT-A-TOKEN","stories":[{"id":"a","title":"A","publicLayer":"-","state":"running"}]}]}""");

        Assert.NotEqual("NOT-A-TOKEN", Reopen().EnsureVoter("F1"));
    }

    [Fact]
    public void TheForgottenBeaconSurvivesARestartAndIsPerCommander()
    {
        var store = Reopen();
        store.SetBeaconForgotten("F1", true);

        var reopened = Reopen();

        Assert.True(reopened.BeaconForgotten("F1"));
        Assert.False(reopened.BeaconForgotten("F2"));

        reopened.SetBeaconForgotten("F1", false);

        Assert.False(Reopen().BeaconForgotten("F1"));
    }
}
