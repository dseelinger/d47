using D47.Core.Adventures;
using D47.Core.Stories;
using Xunit;
using static D47.Core.Tests.Stories.StoryFixtures;

namespace D47.Core.Tests.Stories;

/// <summary>A boarding handed with the seats it needs is placed only on or after an objective that boards a hull with that many (#1762).</summary>
public sealed class ABoardingWithNoSeatIsPlacedAtABoardObjectiveTests
{
    private static readonly StorySwitch Rider = new("rider", true, "setUp", false, 2);

    private static AdventureBeat Beat(string title, string? boardsHull = null, params string[] boards) => new()
    {
        Title = title,
        Line = "Go.",
        Trigger = boardsHull is null
            ? new AdventureTrigger { Kind = TriggerKind.Arrive, System = "Sol" }
            : new AdventureTrigger { Kind = TriggerKind.Board, ShipType = boardsHull },
        Boards = boards,
    };

    private static Adventure Chapter(IReadOnlyList<string> opening, params AdventureBeat[] beats) =>
        new() { Key = "c", Name = "C", OpeningBoards = opening, Beats = beats };

    private static IEnumerable<string> Placed(Adventure chapter) =>
        chapter.OpeningBoards.Concat(chapter.Beats.SelectMany(beat => beat.Boards));

    [Fact]
    public void ABoardingListedBeforeTheBoardObjectiveIsKeptOnlyAfterIt()
    {
        var chapter = Chapter([], Beat("one", null, "rider"), Beat("two", "CobraMkIII"), Beat("three", null, "rider"));

        var placed = StoryAboard.Placed(chapter, [Rider]);

        Assert.Empty(placed.Beats[0].Boards);
        Assert.Equal(["rider"], placed.Beats[2].Boards);
    }

    [Fact]
    public void ABoardingListedOnTheBoardObjectiveItselfIsKept()
    {
        var chapter = Chapter([], Beat("one"), Beat("two", "CobraMkIII", "rider"));

        Assert.Equal(["rider"], StoryAboard.Placed(chapter, [Rider]).Beats[1].Boards);
    }

    [Fact]
    public void ABoardingListedAtTheOpeningIsDropped()
    {
        var chapter = Chapter(["rider"], Beat("one", "CobraMkIII"));

        Assert.Empty(StoryAboard.Placed(chapter, [Rider]).OpeningBoards);
    }

    [Fact]
    public void ABoardObjectiveIntoASidewinderDoesNotMeetTwoSeats()
    {
        var chapter = Chapter([], Beat("one", "Sidewinder"), Beat("two", null, "rider"));

        Assert.Empty(Placed(StoryAboard.Placed(chapter, [Rider])));
    }

    [Fact]
    public void AChapterWithNoBoardObjectiveKeepsItNowhere()
    {
        var chapter = Chapter(["rider"], Beat("one", null, "rider"), Beat("two", null, "rider"));

        Assert.Empty(Placed(StoryAboard.Placed(chapter, [Rider])));
    }

    [Fact]
    public void ABoardIntoAHullWithNoCrewValueMeetsAnySeats()
    {
        var chapter = Chapter([], Beat("one", "NoSuchHull", "rider"));

        Assert.Equal(["rider"], Placed(StoryAboard.Placed(chapter, [Rider])));
    }

    [Fact]
    public void ABoardingWithoutSeatsIsPlacedAsBefore()
    {
        var chapter = Chapter(["rider"], Beat("one", null, "rider"));

        Assert.Equal(["rider"], StoryAboard.Placed(chapter, [new StorySwitch("rider", true, "setUp")]).OpeningBoards);
    }

    [Fact]
    public void ABoardingFromAStepOfABeatCountsWhereverTheStepIs()
    {
        var beat = Beat("two", null, "rider") with
        {
            Steps = [new BeatStep { Trigger = new AdventureTrigger { Kind = TriggerKind.Arrive, System = "Sol" } }, new BeatStep { Trigger = new AdventureTrigger { Kind = TriggerKind.Board, ShipType = "Python" } }],
        };

        Assert.Equal(["rider"], Placed(StoryAboard.Placed(Chapter([], beat), [Rider])));
    }
}
