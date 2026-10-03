using D47.Core.Stories;
using Xunit;
using static D47.Core.Tests.Stories.StoryFixtures;

namespace D47.Core.Tests.Stories;

/// <summary>
/// When a running story's cast loses a voice — the recording deleted, a model removed — the story is paused, its chapter
/// stops, the message is posted again, and it cannot be resumed until the voice is back (#714).
/// </summary>
public sealed class LosingACastVoicePausesTheStoryTests
{
    [Fact]
    public async Task DeletingTheRecordingPausesTheStoryAndPostsTheMessageAgain()
    {
        var recorded = true;
        var posted = new List<(string Title, string Message)>();
        using var fixtures = AStoryWithAnUnreadyVoiceCannotBePickedTests.Fixtures(
            AStoryWithAnUnreadyVoiceCannotBePickedTests.WithOwnVoice,
            () => new CastVoicesHere(Kokoro: true, Chatterbox: true, OwnRecording: recorded),
            posted);

        Assert.Null(await fixtures.Director.PickAsync("F1", Id, Now, CancellationToken.None));
        var chapter = fixtures.Stories.Current("F1")!.CurrentChapter!;

        Assert.Null(fixtures.Director.Tick("F1", Now.AddMinutes(1)));
        Assert.Equal(StoryState.Running, fixtures.Stories.Current("F1")!.State);

        recorded = false;
        Assert.Null(fixtures.Director.Tick("F1", Now.AddMinutes(2)));

        Assert.Equal(StoryState.Paused, fixtures.Stories.Current("F1")!.State);
        Assert.True(fixtures.Book.Store.Find("F1", chapter)!.IsAbandoned);
        Assert.Equal(
            [(Card.Title, $"{Card.Title} is paused because a voice it needs is not ready. Record your voice under Settings, Your voice. Then resume it on the Stories page.")],
            posted);

        // Paused, it is not checked again, so the message is posted once.
        Assert.Null(fixtures.Director.Tick("F1", Now.AddMinutes(3)));
        Assert.Single(posted);
    }

    [Fact]
    public async Task ResumingIsRefusedUntilARecordingExists()
    {
        var recorded = true;
        var posted = new List<(string Title, string Message)>();
        using var fixtures = AStoryWithAnUnreadyVoiceCannotBePickedTests.Fixtures(
            AStoryWithAnUnreadyVoiceCannotBePickedTests.WithOwnVoice,
            () => new CastVoicesHere(Kokoro: true, Chatterbox: true, OwnRecording: recorded),
            posted);

        Assert.Null(await fixtures.Director.PickAsync("F1", Id, Now, CancellationToken.None));

        recorded = false;
        Assert.Null(fixtures.Director.Tick("F1", Now.AddMinutes(1)));

        var refusal = await fixtures.Director.ResumeAsync("F1", Now.AddMinutes(2), CancellationToken.None);

        Assert.Contains("Record your voice under Settings, Your voice.", refusal, StringComparison.Ordinal);
        Assert.Equal(StoryState.Paused, fixtures.Stories.Current("F1")!.State);

        recorded = true;

        Assert.Null(await fixtures.Director.ResumeAsync("F1", Now.AddMinutes(3), CancellationToken.None));
        Assert.Equal(StoryState.Running, fixtures.Stories.Current("F1")!.State);
    }

    [Fact]
    public async Task TheCastIsCheckedAtMostEveryFewSeconds()
    {
        var checks = 0;
        var posted = new List<(string Title, string Message)>();
        using var fixtures = AStoryWithAnUnreadyVoiceCannotBePickedTests.Fixtures(
            AStoryWithAnUnreadyVoiceCannotBePickedTests.WithOwnVoice,
            () =>
            {
                checks++;
                return CastVoicesHere.All;
            },
            posted);

        Assert.Null(await fixtures.Director.PickAsync("F1", Id, Now, CancellationToken.None));
        checks = 0;

        for (var tick = 0; tick < 50; tick++)
        {
            Assert.Null(fixtures.Director.Tick("F1", Now.AddMinutes(1).AddMilliseconds(tick * 100)));
        }

        Assert.Equal(1, checks);
    }
}
