using D47.Core.Stories;
using Xunit;
using static D47.Core.Tests.Stories.StoryFixtures;

namespace D47.Core.Tests.Stories;

/// <summary>
/// One of the fourteen clues waits for its day, a play session and a chapter finished since the last clue, so a
/// Commander who comes back after months gets the next clue in order and no more.
/// </summary>
public sealed class AClueWaitsForAFinishedChapterTests
{
    private static readonly Story ThreeGiven = new()
    {
        Id = Id,
        Title = Card.Title,
        PublicLayer = Card.Describe(),
        PickedAt = Now.AddDays(-1),
        BeaconScanAt = Now,
        CluesGiven = 3,
        Sessions = 3,
        ClueSession = 2,
        ClueChapter = 2,
        Chapters = ["one", "two", "three"],
    };

    [Fact]
    public void TheFourthClueComesWithANewSessionAndANewChapter() =>
        Assert.Equal(new StoryClueDue(Id, 3), StoryClues.Due(ThreeGiven, Now.AddDays(40)));

    [Fact]
    public void TheFourthClueWaitsWhileTheChapterIsUnfinished() =>
        Assert.Null(StoryClues.Due(ThreeGiven with { Chapters = ["one", "two"] }, Now.AddDays(40)));

    [Fact]
    public void TwoCluesAreNeverGivenInOneSession() =>
        Assert.Null(StoryClues.Due(ThreeGiven with { ClueSession = 3 }, Now.AddDays(400)));

    [Fact]
    public void AReturningCommanderGetsTheNextClueAndTheStageMovesOneStep()
    {
        var eight = ThreeGiven with { CluesGiven = 8 };
        var back = Now.AddDays(300);

        Assert.Equal(StoryStage.FunAndGames, StoryClues.Stage(eight));
        Assert.Equal(new StoryClueDue(Id, 8), StoryClues.Due(eight, back));

        var given = eight with { CluesGiven = 9, ClueSession = eight.Sessions, ClueChapter = eight.Chapters.Count };

        Assert.Equal(StoryStage.Midpoint, StoryClues.Stage(given));
        Assert.Null(StoryClues.Due(given, back));
        Assert.Null(StoryClues.Due(given with { Sessions = given.Sessions + 1 }, back));
        Assert.Equal(new StoryClueDue(Id, 9), StoryClues.Due(given with { Sessions = given.Sessions + 1, Chapters = [.. given.Chapters, "four"] }, back));
    }
}
