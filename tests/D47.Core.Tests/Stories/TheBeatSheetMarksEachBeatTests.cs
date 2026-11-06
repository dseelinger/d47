using D47.Core.Stories;
using Xunit;

namespace D47.Core.Tests.Stories;

public sealed class TheBeatSheetMarksEachBeatTests
{
    private static Story Running(StoryPacing pacing, int clues = 0, int chapters = 0, int? finaleFrom = null) => new()
    {
        Id = "s",
        Title = "T",
        PublicLayer = "p",
        Length = pacing.Key,
        State = StoryState.Running,
        PickedAt = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero),
        BeaconScanAt = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero),
        CluesGiven = clues,
        Chapters = [.. Enumerable.Range(0, chapters).Select(i => $"c{i}")],
        FinaleFrom = finaleFrom,
    };

    private static string[] Keys(IReadOnlyList<StoryBeatStanding> sheet, StoryBeatState state) =>
        [.. sheet.Where(beat => beat.State == state).Select(beat => beat.Key)];

    [Fact]
    public void AFullSheetListsAllFifteenBeatsInStoryOrderByName()
    {
        var sheet = StoryClues.BeatSheet(Running(StoryPacing.ThreeMonths));

        Assert.Equal(StoryBeats.Keys, sheet.Select(beat => beat.Key));
        Assert.Equal(Enumerable.Range(1, 15), sheet.Select(beat => beat.Number));
        Assert.Equal("Dark Night of the Soul", sheet[11].Name);
        Assert.Equal("Set-Up", sheet[2].Name);
    }

    [Fact]
    public void ActOneIsCurrentAndNothingIsReachedYet()
    {
        var sheet = StoryClues.BeatSheet(Running(StoryPacing.ThreeMonths) with { BeaconScanAt = null });

        Assert.Empty(Keys(sheet, StoryBeatState.Reached));
        Assert.Equal(["openingImage", "themeStated", "setUp", "catalyst", "debate", "breakIntoTwo"], Keys(sheet, StoryBeatState.Current));
    }

    [Fact]
    public void ABeatOfAnEarlierStageIsReachedAndALaterOneIsLater()
    {
        var story = Running(StoryPacing.ThreeMonths, clues: 2, chapters: 3);
        var stage = StoryClues.Stage(story);
        var sheet = StoryClues.BeatSheet(story);

        Assert.NotEqual(StoryStage.ActOne, stage);
        Assert.Contains("openingImage", Keys(sheet, StoryBeatState.Reached));
        Assert.Contains("finalImage", Keys(sheet, StoryBeatState.Later));
        Assert.NotEmpty(Keys(sheet, StoryBeatState.Current));
    }

    [Fact]
    public void FinalImageIsLaterUntilTheLastFinaleChapterBegins()
    {
        var first = StoryClues.BeatSheet(Running(StoryPacing.OneWeek, chapters: 5, finaleFrom: 5));
        Assert.Equal(StoryBeatState.Later, first.Single(beat => beat.Key == "finalImage").State);
        Assert.Equal(StoryBeatState.Current, first.Single(beat => beat.Key == "finale").State);

        var last = StoryClues.BeatSheet(Running(StoryPacing.OneWeek, chapters: 6, finaleFrom: 5));
        Assert.Equal(StoryBeatState.Current, last.Single(beat => beat.Key == "finalImage").State);
    }

    [Fact]
    public void AShortSheetNumbersOnlyItsOwnBeats()
    {
        var sheet = StoryClues.BeatSheet(Running(StoryPacing.OneWeek));

        Assert.Equal(7, sheet.Count);
        Assert.Equal("Opening Image", sheet[0].Name);
        Assert.Equal("Final Image", sheet[^1].Name);
        Assert.Equal(7, sheet[^1].Number);
    }
}
