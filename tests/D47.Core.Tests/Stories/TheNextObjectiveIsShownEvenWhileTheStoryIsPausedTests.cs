using D47.Core.Adventures;
using D47.Core.Conversation;
using D47.Core.Tests.Conversation;
using Xunit;
using static D47.Core.Tests.Adventures.AdventureFixtures;
using static D47.Core.Tests.Stories.StoryFixtures;

namespace D47.Core.Tests.Stories;

/// <summary>The objective the next-objective strip shows is the one Mark done picks, and a paused stock story is returned flagged.</summary>
public sealed class TheNextObjectiveIsShownEvenWhileTheStoryIsPausedTests
{
    private static StoryFixtures Fixtures(params string[] answers) =>
        new(new RoundScriptedLlmProvider(answers.Select(answer => RoundScriptedLlmProvider.Saying(answer)).ToArray()));

    private static Adventure ShortStory(string key, string name) => LanternRoute() with { Key = key, Name = name, StoryId = null };

    [Fact]
    public async Task ARunningStockStoryShowsItsChapterUnderTheStorysTitle()
    {
        using var fixtures = Fixtures(Spine, BeatsToTheBeacon);
        Assert.Null(await fixtures.Director.PickAsync("F1", Id, Now, TestContext.Current.CancellationToken));

        var shown = fixtures.Director.CurrentObjective("F1");

        Assert.NotNull(shown);
        Assert.False(shown.Paused);
        Assert.Equal(fixtures.Stories.Current("F1")!.Title, shown.StoryName);
        Assert.Equal(fixtures.Stories.Current("F1")!.CurrentChapter, shown.Standing.Adventure.Key);
    }

    [Fact]
    public async Task APausedStockStoryIsStillShownAndFlaggedPaused()
    {
        using var fixtures = Fixtures(Spine, BeatsToTheBeacon);
        Assert.Null(await fixtures.Director.PickAsync("F1", Id, Now, TestContext.Current.CancellationToken));
        Assert.Null(fixtures.Director.SetOn("F1", false, Now.AddMinutes(3)));

        Assert.True(fixtures.Director.CurrentObjective("F1")!.Paused);
        Assert.NotNull(fixtures.Director.MarkObjectiveDone("F1", Now.AddMinutes(4)));
    }

    [Fact]
    public void TheOneActiveShortStoryIsShownUnderItsName()
    {
        using var fixtures = Fixtures();
        Assert.Null(fixtures.Book.Write("F1", ShortStory("the-anchorage-run", "The Anchorage Run")));
        Assert.Null(fixtures.Book.Begin("F1", "the-anchorage-run", Accepted));

        var shown = fixtures.Director.CurrentObjective("F1");

        Assert.Equal("The Anchorage Run", shown!.StoryName);
        Assert.False(shown.Paused);
    }

    [Fact]
    public void SeveralShortStoriesOrNoneShowNothing()
    {
        using var fixtures = Fixtures();
        Assert.Null(fixtures.Director.CurrentObjective("F1"));

        Assert.Null(fixtures.Book.Write("F1", ShortStory("the-anchorage-run", "The Anchorage Run")));
        Assert.Null(fixtures.Book.Write("F1", ShortStory("the-quiet-field", "The Quiet Field")));
        Assert.Null(fixtures.Book.Begin("F1", "the-anchorage-run", Accepted));
        Assert.Null(fixtures.Book.Begin("F1", "the-quiet-field", Accepted));

        Assert.Null(fixtures.Director.CurrentObjective("F1"));
    }
}
