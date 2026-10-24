using D47.Core.Goals;
using D47.Core.Stories;
using D47.Core.Tests.Conversation;
using Xunit;
using static D47.Core.Tests.Stories.StoryFixtures;

namespace D47.Core.Tests.Stories;

/// <summary>
/// A Goals story's clues before the finale wait for the Commander's overall Goals progress as well as their days, and
/// its finale waits for every Goal; any other story is paced as before.
/// </summary>
public sealed class AGoalsStoryFollowsGoalsProgressTests
{
    private static readonly Story Picked = new()
    {
        Id = Id,
        Title = Card.Title,
        PublicLayer = Card.Describe(),
        PickedAt = Now,
        BeaconScanAt = Now,
        GoalsAtPick = 0.4,
    };

    // A year has fourteen clues, so each threshold is a fifteenth more of the way from 0.4 to 1: clue 1 at 0.44, clue 5 at 0.6.
    private static readonly double FirstThreshold = 0.4 + (0.6 / 15);

    private static Story FourGiven => Picked with { CluesGiven = 4, ClueSession = 1, Sessions = 2, ClueChapter = 1, Chapters = ["one", "two"] };

    private static Story AllGiven => Picked with { CluesGiven = 14 };

    private static GoalStanding Arc(string key, long have, long need) => new()
    {
        Arc = new GoalArc { Key = key, Name = key, Done = "Done." },
        Have = have,
        Need = need,
        Source = GoalSource.Live,
        IsDone = have >= need,
    };

    [Fact]
    public void OverallProgressIsTheMeanFractionWithAnUnknownArcAtZero()
    {
        var unknown = new GoalStanding { Arc = new GoalArc { Key = "ships", Name = "ships", Done = "Done." }, Source = GoalSource.Unknown };

        Assert.Equal(0.25, GoalClimb.Overall([Arc("a", 1, 2), Arc("b", 0, 4), unknown, Arc("c", 1, 2)]), 9);
        Assert.Equal(0, GoalClimb.Overall([]));
        Assert.False(GoalClimb.AllDone([Arc("a", 2, 2), unknown]));
        Assert.True(GoalClimb.AllDone([Arc("a", 2, 2), Arc("b", 4, 4)]));
        Assert.False(GoalClimb.AllDone([]));
        Assert.Equal(0.75, GoalClimb.Overall([unknown with { IsDone = true }, Arc("a", 1, 2)]), 9);
    }

    [Fact]
    public void ProgressIsNotReadBeforeTheClueCouldOtherwiseBeDue()
    {
        var reads = 0;

        Assert.Null(StoryClues.Due(Picked, Now.AddDays(6), () => ++reads));
        Assert.NotNull(StoryClues.Due(Picked with { GoalsAtPick = null }, Now.AddDays(30), () => ++reads));
        Assert.Equal(0, reads);
        Assert.NotNull(StoryClues.Due(Picked, Now.AddDays(30), () => ++reads));
        Assert.Equal(1, reads);
    }

    [Fact]
    public void ThresholdsClimbEvenlyFromTheProgressAtThePick()
    {
        Assert.Equal(FirstThreshold, StoryClues.GoalsThreshold(Picked, 0), 9);
        Assert.Equal(0.6, StoryClues.GoalsThreshold(Picked, 4), 9);
        Assert.Equal(0.4 + (0.6 * 14 / 15), StoryClues.GoalsThreshold(Picked, 13), 9);
        Assert.Equal(0, StoryClues.GoalsThreshold(Picked with { GoalsAtPick = null }, 13));
    }

    [Fact]
    public void AClueWhoseDayHasPassedWaitsForItsThreshold()
    {
        Assert.Null(StoryClues.Due(Picked, Now.AddDays(30), () => FirstThreshold - 0.001));
        Assert.Equal(new StoryClueDue(Id, 0), StoryClues.Due(Picked, Now.AddDays(30), () => FirstThreshold));
    }

    [Fact]
    public void AClueWhoseThresholdIsReachedWaitsForItsDay()
    {
        Assert.Null(StoryClues.Due(Picked, Now.AddDays(7).AddMinutes(-1), () => 1));
        Assert.Equal(new StoryClueDue(Id, 0), StoryClues.Due(Picked, Now.AddDays(7), () => 1));
    }

    [Fact]
    public void AStoryWithoutGoalsAtPickIgnoresProgress()
    {
        var plain = Picked with { GoalsAtPick = null };

        Assert.Equal(new StoryClueDue(Id, 0), StoryClues.Due(plain, Now.AddDays(7)));
        Assert.Equal(new StoryClueDue(Id, 0), StoryClues.Due(plain, Now.AddDays(7), () => 0));
        Assert.True(StoryClues.FinaleDue(plain with { CluesGiven = 14 }, Now.AddDays(360)));
    }

    [Fact]
    public void TheFinaleWaitsForEveryGoal()
    {
        Assert.False(StoryClues.FinaleDue(AllGiven, Now.AddDays(400), goalsDone: false));
        Assert.False(StoryClues.FinaleDue(AllGiven, Now.AddDays(359), goalsDone: true));
        Assert.True(StoryClues.FinaleDue(AllGiven, Now.AddDays(360), goalsDone: true));
    }

    [Fact]
    public void AStoryPickedWithEveryGoalDoneIsPacedByItsDays()
    {
        var done = Picked with { GoalsAtPick = 1 };

        Assert.Null(StoryClues.Due(done, Now.AddDays(7).AddMinutes(-1), () => 1));
        Assert.Equal(new StoryClueDue(Id, 0), StoryClues.Due(done, Now.AddDays(7), () => 1));
        Assert.Equal(new StoryClueDue(Id, 13), StoryClues.Due(done with { CluesGiven = 13 }, Now.AddDays(330), () => 1));
        Assert.True(StoryClues.FinaleDue(done with { CluesGiven = 14 }, Now.AddDays(360), goalsDone: true));
    }

    [Fact]
    public void ADropInProgressHoldsTheNextClueAndKeepsTheGivenOnes()
    {
        Assert.Equal(new StoryClueDue(Id, 4), StoryClues.Due(FourGiven, Now.AddDays(90), () => 0.6));

        // The fleet not yet listed this session reads the ship collection as 0.
        Assert.Null(StoryClues.Due(FourGiven, Now.AddDays(90), () => 0.55));
        Assert.Contains(Secret.Clues[3].Text, StoryClues.Brief(FourGiven, Secret));
        Assert.Equal(StoryClues.Stage(FourGiven with { GoalsAtPick = null }), StoryClues.Stage(FourGiven));
    }

    [Fact]
    public void AFinaleUnderWayIsNotWithdrawn()
    {
        var finale = AllGiven with { FinaleFrom = 3, Chapters = ["one", "two", "three"] };

        Assert.Equal(new StoryClueDue(Id, 14), StoryClues.Due(finale, Now.AddDays(400), () => 0));
    }

    [Fact]
    public void AGoalsStoryCannotBeJumped()
    {
        Assert.Equal(StoryJump.GoalsRefusal, StoryJump.Refusal(Picked, StoryJumpTarget.AtClue(0)));
        Assert.Equal(StoryJump.GoalsRefusal, StoryJump.Refusal(Picked, StoryJumpTarget.Finale));
        Assert.Null(StoryJump.Jump(Picked, StoryJumpTarget.Finale, Now.AddDays(1)));
        Assert.NotNull(StoryJump.Jump(Picked with { GoalsAtPick = null }, StoryJumpTarget.Finale, Now.AddDays(1)));
    }

    [Fact]
    public void TheGoalsPageSaysWhatTheNextClueWaitsFor()
    {
        var goal = StoryGoal.Of([FourGiven], _ => null, () => [Arc("a", 1, 4), Arc("b", 1, 2)])!;

        Assert.Contains("Clue 5 waits for Goals progress of 60%; you are at 37%", goal.Note, StringComparison.Ordinal);
        Assert.DoesNotContain("waits", StoryGoal.Of([FourGiven], _ => null, () => [Arc("a", 3, 5)])!.Note, StringComparison.Ordinal);
        Assert.DoesNotContain("waits", StoryGoal.Of([FourGiven with { GoalsAtPick = null }], _ => null, () => [])!.Note, StringComparison.Ordinal);
    }

    [Fact]
    public void TheGoalsPageSaysTheFinaleWaitsForEveryGoal()
    {
        IReadOnlyList<GoalStanding> goals = [.. Enumerable.Range(0, 11).Select(at => Arc($"arc{at}", at < 9 ? 1 : 0, 1))];

        Assert.Contains("The finale waits for every Goal: 9 of 11 done", StoryGoal.Of([AllGiven], _ => null, () => goals)!.Note, StringComparison.Ordinal);
        Assert.DoesNotContain("waits", StoryGoal.Of([AllGiven], _ => null, () => [Arc("a", 1, 1)])!.Note, StringComparison.Ordinal);
    }

    [Trait("Category", "Integration")]
    [Fact]
    public async Task TheDirectorRecordsTheProgressAtThePickAndHoldsTheClue()
    {
        using var fixtures = new StoryFixtures(
            new RoundScriptedLlmProvider(
                RoundScriptedLlmProvider.Saying(NextSpine), RoundScriptedLlmProvider.Saying(NextBeats), RoundScriptedLlmProvider.Saying(NothingObjected)),
            card: Card with { Length = "2-weeks", Goals = true });
        IReadOnlyList<GoalStanding> goals = [Arc("a", 0, 2), Arc("b", 1, 2)];
        fixtures.Director.Goals = () => goals;

        // A two-week story's beacon scan is narrated at the pick, so its clue days count from the pick.
        Assert.Null(await fixtures.Director.PickAsync("F1", Id, Now, CancellationToken.None));

        var story = fixtures.Stories.Current("F1")!;
        var day = story.BeaconScanAt!.Value.AddDays(story.Pacing.ClueDays[0]);

        Assert.Equal(0.25, story.GoalsAtPick);
        Assert.Null(fixtures.Director.ClueDue("F1", day));

        goals = [Arc("a", 2, 2), Arc("b", 2, 2)];

        Assert.Equal(new StoryClueDue(Id, 0), fixtures.Director.ClueDue("F1", day));
    }
}
