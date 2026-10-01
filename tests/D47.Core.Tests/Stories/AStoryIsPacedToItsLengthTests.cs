using D47.Core.Stories;
using D47.Core.Tests.Conversation;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using static D47.Core.Tests.Stories.StoryFixtures;

namespace D47.Core.Tests.Stories;

/// <summary>A story's clues, finale, stages and beats follow the length it was picked with.</summary>
public sealed class AStoryIsPacedToItsLengthTests
{
    private static readonly Story Week = new()
    {
        Id = Id,
        Title = Card.Title,
        PublicLayer = Card.Describe(),
        Length = StoryPacing.OneWeek.Key,
        PickedAt = Now,
        BeaconScanAt = Now,
        Chapters = ["one"],
        Sessions = 1,
    };

    private static readonly Story ThreeDays = Week with { Length = StoryPacing.ThreeDays.Key };

    [Fact]
    public void AWeeksFirstClueComesOnDayOne()
    {
        Assert.Null(StoryClues.Due(Week, Now.AddDays(1).AddMinutes(-1)));
        Assert.Equal(new StoryClueDue(Id, 0), StoryClues.Due(Week, Now.AddDays(1)));
    }

    [Fact]
    public void AWeeksSecondClueComesOnDayThreeAfterASessionAndAChapter()
    {
        var oneGiven = Week with { CluesGiven = 1, ClueSession = 1, ClueChapter = 1 };
        var both = oneGiven with { Sessions = 2, Chapters = ["one", "two"] };

        Assert.Null(StoryClues.Due(both, Now.AddDays(3).AddMinutes(-1)));
        Assert.Null(StoryClues.Due(oneGiven with { Sessions = 2 }, Now.AddDays(3)));
        Assert.Null(StoryClues.Due(oneGiven with { Chapters = ["one", "two"] }, Now.AddDays(3)));
        Assert.Equal(new StoryClueDue(Id, 1), StoryClues.Due(both, Now.AddDays(3)));
        Assert.Null(StoryClues.Due(both with { CluesGiven = 2 }, Now.AddDays(30)));
    }

    [Fact]
    public void AWeeksFinaleOpensOnDayFive()
    {
        var allGiven = Week with { CluesGiven = 2 };

        Assert.False(StoryClues.FinaleDue(allGiven, Now.AddDays(5).AddMinutes(-1)));
        Assert.True(StoryClues.FinaleDue(allGiven, Now.AddDays(5)));
        Assert.False(StoryClues.FinaleDue(allGiven with { CluesGiven = 1 }, Now.AddDays(30)));
    }

    [Fact]
    public void AWeekEndsWithItsSecondFinaleChapter()
    {
        var first = Week with { Chapters = ["one", "two"], FinaleFrom = 2, CluesGiven = 3 };
        var second = first with { Chapters = ["one", "two", "three"], CluesGiven = 4 };

        Assert.False(StoryClues.AtTheEnd(first));
        Assert.Equal(new StoryClueDue(Id, 3), StoryClues.Due(second with { CluesGiven = 3 }, Now));
        Assert.Null(StoryClues.Due(second, Now));
        Assert.True(StoryClues.AtTheEnd(second));
    }

    [Fact]
    public async Task AWeekLongStoryFinishesAfterItsSecondFinaleChapter()
    {
        using var fixtures = new StoryFixtures(new RoundScriptedLlmProvider(
            RoundScriptedLlmProvider.Saying(Spine),
            RoundScriptedLlmProvider.Saying(BeatsToTheBeacon)));

        Assert.Null(await fixtures.Director.PickAsync("F1", Id, Now, CancellationToken.None));

        var last = fixtures.Stories.Current("F1")!.CurrentChapter!;

        fixtures.Stories.Update("F1", Id, story => story with
        {
            Length = StoryPacing.OneWeek.Key,
            Chapters = ["one", "two", last],
            FinaleFrom = 2,
            CluesGiven = 4,
            BeaconScanAt = Now,
        });

        fixtures.Finish("F1", last, Now);

        Assert.Null(fixtures.Director.Tick("F1", Now.AddDays(10)));
        Assert.Equal(StoryState.Finished, fixtures.Stories.Find("F1", Id)!.State);
        Assert.Single(fixtures.Asks);
    }

    [Fact]
    public void AThreeDayStoryGetsOnlyTheShortSheet()
    {
        Assert.Equal(StoryStage.BreakIntoTwo, StoryClues.Stage(ThreeDays));
        Assert.Equal(["breakIntoTwo"], Keys(StoryStage.BreakIntoTwo));

        Assert.Equal(StoryStage.Midpoint, StoryClues.Stage(ThreeDays with { CluesGiven = 1 }));
        Assert.Equal(["midpoint"], Keys(StoryStage.Midpoint));

        Assert.Equal(["openingImage", "catalyst", "breakIntoTwo"], Keys(StoryStage.ActOne));
        Assert.Equal(["finale", "finalImage"], Keys(StoryStage.Finale, finale: 1));
    }

    [Theory]
    [InlineData("3-days", 0, StoryStage.BreakIntoTwo)]
    [InlineData("3-days", 1, StoryStage.Midpoint)]
    [InlineData("1-week", 2, StoryStage.AllIsLost)]
    [InlineData("2-weeks", 1, StoryStage.FunAndGames)]
    [InlineData("2-weeks", 4, StoryStage.AllIsLost)]
    [InlineData("1-month", 3, StoryStage.Midpoint)]
    [InlineData("1-month", 5, StoryStage.AllIsLost)]
    [InlineData("3-months", 3, StoryStage.FunAndGames)]
    [InlineData("3-months", 5, StoryStage.BadGuysCloseIn)]
    [InlineData("3-months", 7, StoryStage.DarkNightOfTheSoul)]
    [InlineData("6-months", 4, StoryStage.FunAndGames)]
    [InlineData("6-months", 8, StoryStage.DarkNightOfTheSoul)]
    public void EachLengthHasItsOwnStages(string length, int given, StoryStage stage) =>
        Assert.Equal(stage, StoryClues.Stage(Week with { Length = length, CluesGiven = given }));

    [Fact]
    public void EveryLengthHasAStageForEachNumberOfCluesGiven() =>
        Assert.All(StoryPacing.All, pacing => Assert.Equal(pacing.ClueDays.Count + 1, pacing.Stages.Count));

    [Theory]
    [InlineData("3-days", 2, 2, 1)]
    [InlineData("1-week", 4, 5, 2)]
    [InlineData("2-weeks", 6, 11, 2)]
    [InlineData("1-month", 8, 23, 3)]
    [InlineData("3-months", 10, 84, 3)]
    [InlineData("6-months", 12, 180, 4)]
    [InlineData("1-year", 18, 360, 4)]
    public void EachLengthHasItsLinesAndFinale(string length, int lines, int finaleDay, int finaleChapters)
    {
        var pacing = StoryPacing.Find(length)!;

        Assert.Equal(lines, pacing.Lines);
        Assert.Equal(finaleDay, pacing.FinaleDay);
        Assert.Equal(finaleChapters, pacing.FinaleChapters);
    }

    [Fact]
    public void TheFullSheetHasAllFifteenBeats() =>
        Assert.Equal(15, StoryPacing.ThreeMonths.BeatKeys.Count);

    [Fact]
    public async Task APickedStoryKeepsItsCardsLength()
    {
        using var fixtures = new StoryFixtures(new RoundScriptedLlmProvider(
            RoundScriptedLlmProvider.Saying(Spine),
            RoundScriptedLlmProvider.Saying(BeatsToTheBeacon)));

        Assert.Null(await fixtures.Director.PickAsync("F1", Id, Now, CancellationToken.None));

        var story = fixtures.Stories.Current("F1")!;

        Assert.Equal(StoryPacing.OneYear.Key, story.Length);
        Assert.Contains("Length: 1 year", story.PublicLayer, StringComparison.Ordinal);
    }

    [Fact]
    public void AStorySavedWithoutALengthIsAYear()
    {
        var path = Path.Combine(Path.GetTempPath(), $"d47-story-{Guid.NewGuid():N}.json");

        try
        {
            File.WriteAllText(
                path,
                """{"commanders":[{"frontierId":"F1","stories":[{"id":"old","title":"Old","publicLayer":"Old.","pickedAt":"2026-09-30T12:00:00+00:00"}]}]}""");

            var story = StoryStore.Open(path, NullLogger<StoryStore>.Instance).Current("F1")!;

            Assert.Equal(StoryPacing.OneYear.Key, story.Length);
            Assert.Same(StoryPacing.OneYear, story.Pacing);
        }
        finally
        {
            File.Delete(path);
        }
    }

    private static string[] Keys(StoryStage stage, int? finale = null) =>
        [.. StoryClues.BeatKeys(StoryPacing.ThreeDays, stage, true, finale)];
}
