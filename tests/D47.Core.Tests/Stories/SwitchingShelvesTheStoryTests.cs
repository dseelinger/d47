using D47.Core.Messages;
using D47.Core.Persona;
using D47.Core.Stories;
using D47.Core.Tests.Conversation;
using Xunit;
using static D47.Core.Tests.Stories.StoryFixtures;

namespace D47.Core.Tests.Stories;

/// <summary>
/// Switch and No story shelve the active story: its chapter under way is dropped, it gives and holds nothing, and picking
/// it again makes the same run active, paused, so Resume writes that chapter again.
/// </summary>
public sealed class SwitchingShelvesTheStoryTests
{
    private static StoryFixtures Fixtures(int writes) => new(new RoundScriptedLlmProvider(
        [.. Enumerable.Range(0, writes).SelectMany(_ => new[]
        {
            RoundScriptedLlmProvider.Saying(Lines()),
            RoundScriptedLlmProvider.Saying(NothingObjected),
        })]));

    [Fact]
    public async Task SwitchingShelvesTheActiveStoryAndBringsAShelvedOneBackPaused()
    {
        using var fixtures = Fixtures(2);

        Assert.Null(await fixtures.Director.PickAsync("F1", Id, Now, CancellationToken.None));
        var first = Assert.Single(fixtures.Stories.Current("F1")!.Chapters);

        Assert.Null(await fixtures.Director.SwitchAsync("F1", Other.Id, Now.AddDays(1), CancellationToken.None));

        var shelved = fixtures.Stories.Find("F1", Id)!;
        Assert.Equal(StoryState.Shelved, shelved.State);
        Assert.True(shelved.IsOff);
        Assert.Equal(first, shelved.Rewrite!.Chapter);
        Assert.True(fixtures.Book.Store.Find("F1", first)!.IsAbandoned);
        Assert.Equal(Other.Id, fixtures.Stories.Current("F1")!.Id);

        Assert.Null(await fixtures.Director.SwitchAsync("F1", Id, Now.AddDays(2), CancellationToken.None));

        var back = fixtures.Stories.Current("F1")!;
        Assert.Equal(Id, back.Id);
        Assert.Equal(StoryState.Paused, back.State);
        Assert.False(back.IsOff);
        Assert.Equal(shelved.PickedAt, back.PickedAt);
        Assert.Equal(StoryState.Shelved, fixtures.Stories.Find("F1", Other.Id)!.State);
        Assert.Equal(2, fixtures.Stories.For("F1").Count);
        Assert.DoesNotContain(fixtures.Stories.For("F1"), story => story.State is StoryState.Abandoned or StoryState.Ended);
        Assert.Equal(Card.InYourWords, fixtures.Backstory);
        Assert.Null(fixtures.Director.TakeOpening("F1"));
    }

    [Fact]
    public async Task ResumeWritesTheShelvedChapterAgainAfterARestart()
    {
        using var fixtures = Fixtures(3);

        Assert.Null(await fixtures.Director.PickAsync("F1", Id, Now, CancellationToken.None));
        var first = fixtures.Stories.Current("F1")!.CurrentChapter!;
        Assert.Null(await fixtures.Director.SwitchAsync("F1", Other.Id, Now.AddDays(1), CancellationToken.None));

        fixtures.Director.ArchiveChapters();
        Assert.NotNull(fixtures.Book.Store.Find("F1", first));

        Assert.Null(await fixtures.Director.SwitchAsync("F1", Id, Now.AddDays(2), CancellationToken.None));
        Assert.Null(await fixtures.Director.ResumeAsync("F1", Now.AddDays(3), CancellationToken.None));

        var story = fixtures.Stories.Current("F1")!;
        Assert.Equal(StoryState.Running, story.State);
        Assert.Null(story.Rewrite);
        Assert.Single(story.Chapters);
        Assert.NotEqual(first, story.CurrentChapter);
        Assert.True(fixtures.Book.Store.Find("F1", story.CurrentChapter!)!.IsActive);
        Assert.Equal(first, fixtures.Asks[^1].SetAside!.Chapter.Previous.Key);
    }

    [Fact]
    public async Task NoStoryShelvesTheStoryAndLeavesNoneActive()
    {
        using var fixtures = Fixtures(1);

        Assert.Null(await fixtures.Director.PickAsync("F1", Id, Now, CancellationToken.None));
        Assert.NotNull(fixtures.Director.CurrentObjective("F1"));

        Assert.Null(fixtures.Director.ShelveCurrent("F1", Now.AddDays(1)));

        Assert.Null(fixtures.Stories.Current("F1"));
        Assert.Equal(StoryState.Shelved, fixtures.Stories.Find("F1", Id)!.State);
        Assert.Equal(CoreHold.None, fixtures.Cores("F1").Hold);
        Assert.Equal(CoreHold.None, fixtures.Stories.Find("F1", Id)!.CoreHold);
        Assert.Null(fixtures.Director.CurrentObjective("F1"));
        Assert.Null(fixtures.Director.ClueDue("F1", Now.AddDays(60)));
        Assert.DoesNotContain(fixtures.Book.Store.For("F1"), adventure => adventure.IsActive);
        Assert.Null(fixtures.Director.Tick("F1", Now.AddDays(2)));
        Assert.Equal("No story is running.", fixtures.Director.ShelveCurrent("F1", Now.AddDays(2)));
    }

    [Fact]
    public async Task AShelvedStorysLinesAreOwned()
    {
        using var fixtures = Fixtures(1);

        Assert.Null(await fixtures.Director.PickAsync("F1", Id, Now, CancellationToken.None));
        Assert.Null(fixtures.Director.ShelveCurrent("F1", Now.AddDays(1)));

        Assert.True(MessageOwnership.Owned(StoryLines.KeyPrefix + Id, fixtures.Book.Store, fixtures.Stories, () => fixtures.Director.Catalog));
        Assert.True(MessageOwnership.Owned(fixtures.Stories.Find("F1", Id)!.CurrentChapter!, fixtures.Book.Store, fixtures.Stories, () => fixtures.Director.Catalog));
    }

    [Fact]
    public async Task AnEraseRemovesAShelvedRun()
    {
        using var fixtures = Fixtures(1);

        Assert.Null(await fixtures.Director.PickAsync("F1", Id, Now, CancellationToken.None));
        Assert.Null(fixtures.Director.ShelveCurrent("F1", Now.AddDays(1)));

        fixtures.Director.ForgetErased("F1", Now.AddDays(2));

        Assert.Empty(fixtures.Stories.For("F1"));
        Assert.Empty(fixtures.Book.Store.For("F1"));
    }
}
