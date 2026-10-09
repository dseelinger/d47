using D47.Core.Stories;
using D47.Core.Tests.Conversation;
using Xunit;
using static D47.Core.Tests.Stories.StoryFixtures;

namespace D47.Core.Tests.Stories;

/// <summary>A finished story owes one ending message, which stays owed until it is posted and answerable after a restart.</summary>
public sealed class AFinishedStoryPostsItsEndingOnceTests
{
    private static readonly Story Done = new()
    {
        Id = Id,
        Title = Card.Title,
        PublicLayer = Card.Describe(),
        PickedAt = Now.AddDays(-400),
        State = StoryState.Finished,
        StoppedAt = Now,
    };

    [Trait("Category", "Integration")]
    [Fact]
    public void ARunningStoryOwesNoEnding()
    {
        using var fixtures = new StoryFixtures(new RoundScriptedLlmProvider());

        fixtures.Stories.Save("F1", Done with { State = StoryState.Running, StoppedAt = null });

        Assert.Null(fixtures.Director.EndingDue("F1"));
    }

    [Trait("Category", "Integration")]
    [Fact]
    public void AFinishedStoryOwesItsEndingUntilItIsPosted()
    {
        using var fixtures = new StoryFixtures(new RoundScriptedLlmProvider());

        fixtures.Stories.Save("F1", Done);

        var due = fixtures.Director.EndingDue("F1");

        Assert.Equal((Id, Card.Title, Secret.End), (due!.StoryId, due.Title, due.End));
        Assert.Equal(Secret.Options, due.Options);

        fixtures.Director.EndingPosted("F1", Id, Now);
        fixtures.Director.EndingPosted("F1", Id, Now.AddHours(1));

        Assert.Null(fixtures.Director.EndingDue("F1"));
        Assert.Equal(Now, fixtures.Stories.Find("F1", Id)!.EndingPostedAt);
    }

    [Trait("Category", "Integration")]
    [Fact]
    public void APostedEndingStaysAnswerableAfterARestart()
    {
        using var fixtures = new StoryFixtures(new RoundScriptedLlmProvider());

        fixtures.Stories.Save("F1", Done);
        fixtures.Director.EndingPosted("F1", Id, Now);

        var reopened = StoryStore.Open(fixtures.StoryPath, Microsoft.Extensions.Logging.Abstractions.NullLogger<StoryStore>.Instance);
        var story = reopened.Find("F1", Id)!;

        Assert.Equal(Now, story.EndingPostedAt);
        Assert.Null(story.EndingChoice);
        Assert.Single(fixtures.Director.EndingOptions("F1"));
    }

    [Fact]
    public void AnEndingPostedByAnotherStoryKeyIsOwnedWhileTheStoryExists()
    {
        Assert.Equal("story.end.the-test-story", StoryEnding.Key(Id));
    }
}
