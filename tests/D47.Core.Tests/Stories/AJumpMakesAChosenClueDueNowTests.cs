using D47.Core.Stories;
using D47.Core.Tests.Conversation;
using Xunit;
using static D47.Core.Tests.Stories.StoryFixtures;

namespace D47.Core.Tests.Stories;

/// <summary>A story can be moved forward so a chosen clue, or the finale, is due now.</summary>
public sealed class AJumpMakesAChosenClueDueNowTests
{
    private static readonly DateTimeOffset Later = Now.AddDays(400);

    private static Story Running(StoryPacing pacing) => new()
    {
        Id = Id,
        Title = Card.Title,
        PublicLayer = Card.Describe(),
        Length = pacing.Key,
        PickedAt = Now,
        BeaconScanAt = Now,
        Chapters = ["one"],
    };

    public static TheoryData<string, int> EveryClue()
    {
        var data = new TheoryData<string, int>();

        foreach (var pacing in StoryPacing.All)
        {
            for (var clue = 0; clue < pacing.ClueDays.Count; clue++)
            {
                data.Add(pacing.Key, clue);
            }
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(EveryClue))]
    public void TheChosenClueIsDueAndTheNextIsNot(string key, int clue)
    {
        var jumped = StoryJump.Jump(Running(StoryPacing.Find(key)!), StoryJumpTarget.AtClue(clue), Later)!;

        Assert.Equal(new StoryClueDue(Id, clue), StoryClues.Due(jumped, Later));

        var next = jumped with { CluesGiven = clue + 1, ClueSession = jumped.Sessions, ClueChapter = jumped.Chapters.Count };

        Assert.Null(StoryClues.Due(next, Later));
    }

    [Theory]
    [MemberData(nameof(EveryClue))]
    public void TheClueAfterTheChosenOneIsStillAhead(string key, int clue)
    {
        var pacing = StoryPacing.Find(key)!;
        var jumped = StoryJump.Jump(Running(pacing), StoryJumpTarget.AtClue(clue), Later)!;
        var after = clue + 1 < pacing.ClueDays.Count ? pacing.ClueDays[clue + 1] : pacing.FinaleDay;

        Assert.True(jumped.SinceBeacon(Later) < TimeSpan.FromDays(after));
    }

    [Fact]
    public void TheFinaleIsDueAfterAJumpToItForEveryLength()
    {
        foreach (var pacing in StoryPacing.All)
        {
            var jumped = StoryJump.Jump(Running(pacing), StoryJumpTarget.Finale, Later)!;

            Assert.True(StoryClues.FinaleDue(jumped, Later), pacing.Key);
        }
    }

    [Fact]
    public void PausedAndSwitchedOffTimeDoNotHoldTheClueBack()
    {
        var story = Running(StoryPacing.OneYear) with
        {
            PausedFor = TimeSpan.FromDays(20),
            State = StoryState.Paused,
            PausedAt = Now.AddDays(100),
            OffSpans = [new StoryOffSpan(Now.AddDays(30), Now.AddDays(60))],
            WithoutOdyssey = [new StoryOffSpan(Now.AddDays(10), Now.AddDays(15))],
        };

        var jumped = StoryJump.Jump(story, StoryJumpTarget.AtClue(3), Later)!.Resumed(Later);

        Assert.Equal(new StoryClueDue(Id, 3), StoryClues.Due(jumped, Later));
        Assert.True(jumped.SinceBeacon(Later) < TimeSpan.FromDays(StoryPacing.OneYear.ClueDays[4]));
    }

    [Fact]
    public void AJumpClearsTheSessionAndChapterAClueWaitsFor()
    {
        var story = Running(StoryPacing.OneYear) with { CluesGiven = 1, ClueSession = 4, Sessions = 4, ClueChapter = 1 };
        var jumped = StoryJump.Jump(story, StoryJumpTarget.AtClue(2), Later)!;

        Assert.Equal(2, jumped.CluesGiven);
        Assert.Null(jumped.ClueSession);
        Assert.Null(jumped.ClueChapter);
        Assert.Equal(new StoryClueDue(Id, 2), StoryClues.Due(jumped, Later));
    }

    [Fact]
    public void AStoryWithNoBeaconScanGetsOne()
    {
        var jumped = StoryJump.Jump(Running(StoryPacing.OneYear) with { BeaconScanAt = null }, StoryJumpTarget.AtClue(0), Later)!;

        Assert.NotNull(jumped.BeaconScanAt);
        Assert.Equal(new StoryClueDue(Id, 0), StoryClues.Due(jumped, Later));
    }

    [Fact]
    public void AJumpBehindTheCluesGivenIsRefused()
    {
        var story = Running(StoryPacing.OneYear) with { CluesGiven = 3 };

        Assert.NotNull(StoryJump.Refusal(story, StoryJumpTarget.AtClue(2)));
        Assert.Null(StoryJump.Jump(story, StoryJumpTarget.AtClue(2), Later));
        Assert.Null(StoryJump.Refusal(story, StoryJumpTarget.AtClue(3)));
    }

    [Fact]
    public void AJumpPastTheLastClueIsRefused() =>
        Assert.NotNull(StoryJump.Refusal(Running(StoryPacing.OneYear), StoryJumpTarget.AtClue(StoryPacing.OneYear.ClueDays.Count)));

    [Fact]
    public void AStoryInItsFinaleIsNotJumped()
    {
        var story = Running(StoryPacing.OneYear) with { FinaleFrom = 1 };

        Assert.NotNull(StoryJump.Refusal(story, StoryJumpTarget.Finale));
        Assert.Null(StoryJump.Jump(story, StoryJumpTarget.Finale, Later));
    }

    [Fact]
    public void AStoryThatIsNotCurrentIsNotJumped()
    {
        foreach (var state in new[] { StoryState.Abandoned, StoryState.Ended, StoryState.Finished })
        {
            Assert.NotNull(StoryJump.Refusal(Running(StoryPacing.OneYear) with { State = state }, StoryJumpTarget.AtClue(0)));
        }
    }

    [Fact]
    public async Task TheDirectorWritesTheNextChapterInPlaceOfTheCurrentOne()
    {
        using var fixtures = new StoryFixtures(new RoundScriptedLlmProvider(
            RoundScriptedLlmProvider.Saying(Spine),
            RoundScriptedLlmProvider.Saying(BeatsToTheBeacon),
            RoundScriptedLlmProvider.Saying(NextSpine),
            RoundScriptedLlmProvider.Saying(NextBeats)));

        Assert.Null(await fixtures.Director.PickAsync("F1", Id, Now, CancellationToken.None));

        var first = Assert.Single(fixtures.Stories.Current("F1")!.Chapters);

        Assert.Null(await fixtures.Director.JumpAsync("F1", StoryJumpTarget.AtClue(1), Now.AddDays(1), CancellationToken.None));

        var story = fixtures.Stories.Current("F1")!;
        var chapter = Assert.Single(story.Chapters);

        Assert.Equal(StoryState.Running, story.State);
        Assert.NotEqual(first, chapter);
        Assert.Null(fixtures.Book.Store.Find("F1", first));
        Assert.True(fixtures.Book.Store.Find("F1", chapter)!.IsActive);
        Assert.Equal(new StoryClueDue(Id, 1), StoryClues.Due(story, Now.AddDays(1)));
    }
}
