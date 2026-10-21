using System.Text.Json;
using D47.Core.Goals;
using D47.Core.Journal;
using D47.Core.Stories;
using Xunit;

namespace D47.Core.Tests.Goals;

/// <summary>Each goal with steps is one track of segments in chapters, placed from the same figures as its percent.</summary>
public class AGoalIsOneChapterTrackTests
{
    private static readonly DateTimeOffset Now = new(3311, 6, 1, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public void ACareerGoalIsThirteenSegmentsInTwoChapters()
    {
        var standing = Rank("rank.trade", "\"Trade\":12", "\"Trade\":56");
        var track = standing.Track!;

        Assert.Equal(13, track.Segments.Count);
        Assert.Equal("Mostly Penniless", track.Segments[1].Name);
        Assert.Equal("Elite IV", track.Segments[12].Name);
        Assert.Equal([("To Elite", 0, 8), ("To Elite V", 8, 5)], track.Chapters.Select(c => (c.Label, c.First, c.Count)));
        Assert.Equal(12, track.Current);
        Assert.Equal(0.56, track.Fill!.Value, 6);
        Assert.Equal(12.56 / 13, track.Whole, 6);
        Assert.Equal(8.0 / 13, track.ShareOf(track.Chapters[0]), 6);
    }

    [Fact]
    public void ACareerAtTheTopIsAllPassed()
    {
        var track = Rank("rank.trade", "\"Trade\":13", "\"Trade\":40").Track!;

        Assert.True(track.IsDone);
        Assert.Equal(1, track.Whole);
        Assert.Null(track.CurrentChapter);
    }

    [Fact]
    public void ANavyGoalIsFourteenSegmentsInOneChapterToItsTop()
    {
        var track = Rank("rank.empire", "\"Empire\":3", "\"Empire\":20").Track!;

        Assert.Equal(14, track.Segments.Count);
        Assert.Equal("Master", track.Segments[3].Name);
        Assert.Equal([("To King", 0, 14)], track.Chapters.Select(c => (c.Label, c.First, c.Count)));
        Assert.Equal(3, track.Current);
        Assert.Equal(0.20, track.Fill!.Value, 6);
    }

    [Fact]
    public void APowerplayGoalIsEighteenSegmentsSizedByMerits()
    {
        var state = new CommanderGameState(new CommanderIdentity("F1", "Jameson"));
        state.Apply(Event("Powerplay", "\"Power\":\"Aisling Duval\", \"Rank\":8, \"Merits\":43000"));

        var standing = GoalEvaluator.Evaluate(GoalCatalogue.Find("powerplay")!, state, null);
        var track = standing.Track!;

        Assert.Equal(18, track.Segments.Count);
        Assert.Equal(["2–10", "11–20", "21–30"], new[] { track.Chapters[0], track.Chapters[1], track.Chapters[2] }.Select(c => c.Label));
        Assert.Equal(10, track.Chapters.Count);
        Assert.Equal(2000, track.Segments[0].Weight);
        Assert.Equal(80000, track.Segments[9].Weight);
        Assert.Equal(7, track.Current);
        Assert.Equal((43000 - 39000) / 8000.0, track.Fill!.Value, 6);
        Assert.Equal(43000 / 775000.0, standing.Fraction!.Value, 6);
    }

    [Fact]
    public void AStoryGoalIsOneSegmentPerLineInStageChapters()
    {
        var story = new Story
        {
            Id = "s",
            Title = "S",
            PublicLayer = "p",
            Chapters = [],
            Length = StoryPacing.OneMonth.Key,
            CluesGiven = 4,
        };

        var standing = StoryGoal.Of([story], _ => null)!;
        var track = standing.Track!;

        Assert.Equal(StoryPacing.OneMonth.Lines, track.Segments.Count);
        Assert.Equal(
            [("Fun and Games", 0, 2), ("Midpoint", 2, 1), ("All Is Lost", 3, 2), ("Finale", 5, 3)],
            track.Chapters.Select(c => (c.Label, c.First, c.Count)));
        Assert.Equal(4, track.Current);
        Assert.Equal(4.0 / 8, standing.Fraction!.Value, 6);
    }

    private static GoalStanding Rank(string key, string rank, string progress)
    {
        var state = new CommanderGameState(new CommanderIdentity("F1", "Jameson"));
        state.Apply(Event("Rank", rank));
        state.Apply(Event("Progress", progress));

        return GoalEvaluator.Evaluate(GoalCatalogue.Find(key)!, state, null);
    }

    private static JournalEvent Event(string kind, string fields)
    {
        var text = $"{{ \"timestamp\":\"{Now.UtcDateTime:yyyy-MM-ddTHH:mm:ssZ}\", \"event\":\"{kind}\", {fields} }}";

        return new JournalEvent(Now, kind, JsonDocument.Parse(text).RootElement);
    }
}
