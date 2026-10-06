using D47.Core.Adventures;
using Xunit;
using static D47.Core.Tests.Adventures.AdventureFixtures;

namespace D47.Core.Tests.Adventures;

public class AStoryChapterCannotBeRemovedTests
{
    private static Adventure Chapter(bool story, bool begun, bool abandoned = false) => LanternRoute(begun ? Accepted : null) with
    {
        Source = AdventureSource.Commander,
        StoryId = story ? "journeyman" : null,
        AbandonedAt = abandoned ? Accepted.AddMinutes(1) : null,
    };

    private static AdventureStanding Standing(Adventure adventure, int fired = 0) =>
        new() { Adventure = adventure, Fired = [.. Enumerable.Repeat(Accepted, fired)] };

    [Fact]
    public void AStoryChapterUnderWayShowsAbandonAndNothingElse() =>
        Assert.Equal(["Abandon"], Standing(Chapter(story: true, begun: true)).ReadingButtons());

    [Fact]
    public void AnAbandonedStoryChapterShowsBeginAgainAndNothingElse() =>
        Assert.Equal(["Begin again"], Standing(Chapter(story: true, begun: true, abandoned: true)).ReadingButtons());

    [Fact]
    public void AFinishedStoryChapterShowsNoButtons()
    {
        var adventure = Chapter(story: true, begun: true);

        Assert.Empty(Standing(adventure, fired: adventure.Beats.Count).ReadingButtons());
    }

    [Fact]
    public void AnAdventureTheCommanderWroteUnderWayShowsAbandonAndRemove() =>
        Assert.Equal(["Abandon", "Remove"], Standing(Chapter(story: false, begun: true)).ReadingButtons());

    [Fact]
    public void AnAdventureThatHasNotBegunCanBeBegunAndRemoved() =>
        Assert.Equal(["Begin", "Remove"], Standing(Chapter(story: false, begun: false)).ReadingButtons());

    [Fact]
    public void AFinishedAdventureTheCommanderWroteOffersTheNextChapterAndRemove()
    {
        var adventure = Chapter(story: false, begun: true);

        Assert.Equal(
            ["Write the next chapter", "Remove"],
            Standing(adventure, fired: adventure.Beats.Count).ReadingButtons());
    }
}
