using D47.Core.Stories;
using Xunit;
using static D47.Core.Tests.Stories.StoryFixtures;

namespace D47.Core.Tests.Stories;

/// <summary>Switching the story off changes nothing the Commander keeps: the Backstory, the chapters and the story's place.</summary>
public sealed class APausedStoryKeepsItsCoresTests
{
    [Fact]
    public void TheBackstoryAndTheChaptersAreUntouched()
    {
        using var fixtures = PauseSupport.Picked(out var chapter);
        var before = fixtures.Stories.Current("F1")!;

        fixtures.Director.SetOn("F1", false, Now);
        var off = fixtures.Stories.Current("F1")!;

        Assert.Equal(Card.InYourWords, fixtures.Backstory);
        Assert.Equal(StoryState.Running, off.State);
        Assert.Equal(before.Chapters, off.Chapters);
        Assert.Equal(before.PickedAt, off.PickedAt);
        Assert.True(fixtures.Book.Store.Find("F1", chapter)!.IsActive);

        fixtures.Director.SetOn("F1", true, Now.AddHours(1));

        Assert.Equal(Card.InYourWords, fixtures.Backstory);
        Assert.False(fixtures.Stories.Current("F1")!.IsOff);
        Assert.Null(fixtures.Director.Tick("F1", Now.AddHours(1)));
    }

    [Fact]
    public void AnAdventureThatIsNotAStoryChapterIsNotSilenced()
    {
        using var fixtures = PauseSupport.Picked(out var chapter);
        var standing = fixtures.Book.Standing("F1", chapter)!;

        fixtures.Director.SetOn("F1", false, Now);

        Assert.False(fixtures.Book.IsSilenced("F1", standing.Adventure with { StoryId = null }, Now.AddMinutes(1)));
        Assert.True(fixtures.Book.IsSilenced("F1", standing.Adventure, Now.AddMinutes(1)));
    }
}
