using D47.Core.Stories;
using D47.Core.Tests.Conversation;
using Xunit;
using static D47.Core.Tests.Stories.StoryFixtures;

namespace D47.Core.Tests.Stories;

/// <summary>When a story's chapter finishes, the tick has the next one written from it and begun.</summary>
public sealed class AFinishedChapterWritesTheNextTests
{
    private static StoryFixtures Fixtures() => new(new RoundScriptedLlmProvider(
        RoundScriptedLlmProvider.Saying(Spine),
        RoundScriptedLlmProvider.Saying(BeatsToTheBeacon),
        RoundScriptedLlmProvider.Saying(NextSpine),
        RoundScriptedLlmProvider.Saying(NextBeats)));

    [Fact]
    public async Task TheNextChapterFollowsTheOneThatFinished()
    {
        using var fixtures = Fixtures();
        Assert.Null(await fixtures.Director.PickAsync("F1", Id, Now, CancellationToken.None));

        var first = Assert.Single(fixtures.Stories.Current("F1")!.Chapters);

        // Under way, so nothing is owed.
        Assert.Null(fixtures.Director.Tick("F1", Now.AddMinutes(1)));

        fixtures.Finish("F1", first, Now);

        var writing = fixtures.Director.Tick("F1", Now.AddDays(3));
        Assert.NotNull(writing);
        Assert.Null(await writing);

        var story = fixtures.Stories.Current("F1")!;
        Assert.Equal(2, story.Chapters.Count);

        var next = fixtures.Book.Store.Find("F1", story.Chapters[1])!;
        Assert.Equal(first, next.Follows);
        Assert.Equal(Id, next.StoryId);
        Assert.True(next.IsActive);

        var prompt = fixtures.Provider.Requests[2].Prompt.History[0].Text;
        Assert.Contains("chapter 2 of \"The Test Story\"", prompt);
        Assert.Contains("This story is the next chapter of one the Commander has already flown to its end.", prompt);
        Assert.Contains("running for 3 days", prompt);
        Assert.DoesNotContain("its last beat is \"arrive\"", prompt);

        // Written once: the next tick finds chapter two under way.
        Assert.Null(fixtures.Director.Tick("F1", Now.AddDays(3)));
    }

    [Fact]
    public async Task AWriteThatThrowsIsAFailureAndTheTickDoesNotRetryIt()
    {
        using var fixtures = Fixtures();
        Assert.Null(await fixtures.Director.PickAsync("F1", Id, Now, CancellationToken.None));

        fixtures.Finish("F1", Assert.Single(fixtures.Stories.Current("F1")!.Chapters), Now);
        fixtures.Throws = true;

        Assert.NotNull(await fixtures.Director.Tick("F1", Now.AddDays(1))!);
        Assert.True(fixtures.Director.WriteFailed("F1"));
        Assert.Null(fixtures.Director.Tick("F1", Now.AddDays(1)));

        fixtures.Throws = false;

        Assert.Null(await fixtures.Director.WriteNextAsync("F1", Now.AddDays(1), CancellationToken.None));
        Assert.Equal(2, fixtures.Stories.Current("F1")!.Chapters.Count);
    }

    [Fact]
    public async Task AnAbandonedChapterPausesTheStoryAndResumeBeginsItAgain()
    {
        using var fixtures = Fixtures();
        Assert.Null(await fixtures.Director.PickAsync("F1", Id, Now, CancellationToken.None));

        var first = Assert.Single(fixtures.Stories.Current("F1")!.Chapters);
        fixtures.Book.Abandon("F1", first, Now.AddMinutes(1));

        Assert.Null(fixtures.Director.Tick("F1", Now.AddMinutes(2)));
        Assert.Equal(StoryState.Paused, fixtures.Stories.Current("F1")!.State);

        Assert.Null(await fixtures.Director.ResumeAsync("F1", Now.AddMinutes(3), CancellationToken.None));
        Assert.Equal(StoryState.Running, fixtures.Stories.Current("F1")!.State);
        Assert.True(fixtures.Book.Store.Find("F1", first)!.IsActive);
    }

    [Fact]
    public async Task TheStoryIsKeptOnDisk()
    {
        using var fixtures = Fixtures();
        Assert.Null(await fixtures.Director.PickAsync("F1", Id, Now, CancellationToken.None));

        var reread = StoryStore.Open(fixtures.StoryPath, fixtures.Files, Microsoft.Extensions.Logging.Abstractions.NullLogger<StoryStore>.Instance);
        var story = reread.Current("F1")!;

        Assert.Equal(Id, story.Id);
        Assert.Equal(StoryState.Running, story.State);
        Assert.Equal(Now, story.PickedAt);
        Assert.Single(story.Chapters);
        Assert.DoesNotContain("isCurrent", fixtures.Files.ReadText(fixtures.StoryPath), StringComparison.Ordinal);
    }
}
