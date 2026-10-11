using D47.Core.Stories;
using D47.Core.Tests.Conversation;
using Xunit;
using static D47.Core.Tests.Stories.StoryFixtures;

namespace D47.Core.Tests.Stories;

/// <summary>The director says a story began when it is picked, resumed or switched on, so its cast can be paired.</summary>
[Trait("Category", "Integration")]
public sealed class AStoryThatBeginsAsksForItsCastToBePairedTests
{
    private static StoryFixtures Fixtures() => new(new RoundScriptedLlmProvider(
        RoundScriptedLlmProvider.Saying(Spine),
        RoundScriptedLlmProvider.Saying(BeatsToTheBeacon)));

    [Fact]
    public async Task PickingAStorySaysItBegan()
    {
        using var fixtures = Fixtures();
        var began = 0;
        fixtures.Director.Began += () => began++;

        Assert.Null(await fixtures.Director.PickAsync("F1", Id, Now, CancellationToken.None));

        Assert.Equal(1, began);
        Assert.Equal(Id, fixtures.Director.CurrentCast("F1")!.Value.Secret.Id);
    }

    [Fact]
    public async Task ResumingAPausedStorySaysItBegan()
    {
        using var fixtures = Fixtures();
        Assert.Null(await fixtures.Director.PickAsync("F1", Id, Now, CancellationToken.None));

        var began = 0;
        fixtures.Director.Began += () => began++;

        fixtures.Book.Abandon("F1", fixtures.Stories.Current("F1")!.CurrentChapter!, Now.AddDays(1));
        Assert.Null(fixtures.Director.Tick("F1", Now.AddDays(1)));
        Assert.Equal(0, began);

        Assert.Null(await fixtures.Director.ResumeAsync("F1", Now.AddDays(2), CancellationToken.None));

        Assert.Equal(1, began);
    }

    [Fact]
    public async Task AChapterRunningAgainUnderAPausedStorySaysItBegan()
    {
        using var fixtures = Fixtures();
        Assert.Null(await fixtures.Director.PickAsync("F1", Id, Now, CancellationToken.None));
        fixtures.Stories.Save("F1", fixtures.Stories.Current("F1")!.Paused(Now.AddMinutes(1)));

        var began = 0;
        fixtures.Director.Began += () => began++;

        Assert.Null(fixtures.Director.Tick("F1", Now.AddMinutes(2)));

        Assert.Equal(StoryState.Running, fixtures.Stories.Current("F1")!.State);
        Assert.Equal(1, began);
    }

    [Fact]
    public async Task SwitchingAStoryOnSaysItBeganAndSwitchingItOffDoesNot()
    {
        using var fixtures = Fixtures();
        Assert.Null(await fixtures.Director.PickAsync("F1", Id, Now, CancellationToken.None));

        var began = 0;
        fixtures.Director.Began += () => began++;

        Assert.Null(fixtures.Director.SetOn("F1", false, Now.AddMinutes(1)));
        Assert.Equal(0, began);

        Assert.Null(fixtures.Director.SetOn("F1", true, Now.AddMinutes(2)));
        Assert.Equal(1, began);
    }
}
