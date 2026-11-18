using D47.Core.Tests.Conversation;
using Xunit;
using static D47.Core.Tests.Stories.StoryFixtures;

namespace D47.Core.Tests.Stories;

/// <summary>A chapter that comes due while paid calls are stopped stays due, and the first tick after starts it.</summary>
public sealed class NoChapterStartsWhileTheCommanderIsAwayTests
{
    [Fact]
    public async Task TheDueChapterWaitsAndIsNotAFailure()
    {
        using var fixtures = new StoryFixtures(new RoundScriptedLlmProvider(
            RoundScriptedLlmProvider.Saying(Lines()),
            RoundScriptedLlmProvider.Saying(NothingObjected),
            RoundScriptedLlmProvider.Saying(NextLines),
            RoundScriptedLlmProvider.Saying(NothingObjected)));

        Assert.Null(await fixtures.Director.PickAsync("F1", Id, Now, CancellationToken.None));
        fixtures.Finish("F1", Assert.Single(fixtures.Stories.Current("F1")!.Chapters), Now);

        var away = true;
        fixtures.Director.PaidCallsStopped = () => away;
        var asked = fixtures.Provider.CallCount;

        Assert.Null(fixtures.Director.Tick("F1", Now.AddDays(1)));
        Assert.False(fixtures.Director.WriteFailed("F1"));
        Assert.False(fixtures.Director.IsWriting("F1"));
        Assert.Equal(asked, fixtures.Provider.CallCount);
        Assert.Single(fixtures.Stories.Current("F1")!.Chapters);

        away = false;

        var writing = fixtures.Director.Tick("F1", Now.AddDays(1));
        Assert.NotNull(writing);
        Assert.Null(await writing);
        Assert.Equal(2, fixtures.Stories.Current("F1")!.Chapters.Count);
    }
}
