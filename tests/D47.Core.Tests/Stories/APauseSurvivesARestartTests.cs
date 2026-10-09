using D47.Core.Stories;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using static D47.Core.Tests.Stories.StoryFixtures;

namespace D47.Core.Tests.Stories;

/// <summary>The pause is written to the story file, so a restart finds the story still off and the off stretch still known.</summary>
[Trait("Category", "Integration")]
public sealed class APauseSurvivesARestartTests
{
    [Fact]
    public void AReopenedStoreStillHasTheStoryOff()
    {
        using var fixtures = PauseSupport.Picked(out _);
        fixtures.Director.SetOn("F1", false, Now.AddMinutes(1));

        var reopened = StoryStore.Open(fixtures.StoryPath, fixtures.Files, NullLogger<StoryStore>.Instance);

        Assert.True(reopened.Current("F1")!.IsOff);
        Assert.True(reopened.Current("F1")!.WasOffAt(Now.AddHours(1)));
        Assert.False(reopened.Current("F1")!.WasOffAt(Now));
    }

    [Fact]
    public void AnEndedStretchIsKeptAfterTheStoryIsSwitchedBackOn()
    {
        using var fixtures = PauseSupport.Picked(out _);
        fixtures.Director.SetOn("F1", false, Now.AddMinutes(1));
        fixtures.Director.SetOn("F1", true, Now.AddMinutes(10));

        var story = StoryStore.Open(fixtures.StoryPath, fixtures.Files, NullLogger<StoryStore>.Instance).Current("F1")!;

        Assert.False(story.IsOff);
        Assert.True(story.WasOffAt(Now.AddMinutes(5)));
        Assert.False(story.WasOffAt(Now.AddMinutes(10)));
    }
}
