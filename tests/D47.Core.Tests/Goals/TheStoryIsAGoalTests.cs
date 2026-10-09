using D47.Core.Storage;
using D47.Core.Adventures;
using D47.Core.Goals;
using D47.Core.Stories;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using static D47.Core.Tests.Adventures.AdventureFixtures;

namespace D47.Core.Tests.Goals;

[Trait("Category", "Integration")]
public sealed class TheStoryIsAGoalTests : IDisposable
{
    private const string Labour = "LTT 7786 Labour";

    private static readonly DateTimeOffset Now = new(2026, 9, 30, 12, 0, 0, TimeSpan.Zero);

    private static readonly Adventure Chapter = LanternRoute(Accepted) with
    {
        StoryId = "the-marker",
        Beats = [Beat("The Count", "catalyst", new AdventureTrigger { Kind = TriggerKind.Bond, Count = 8, Faction = Labour }, "Done.")],
    };

    private static readonly Story Running = new()
    {
        Id = "the-marker",
        Title = "The Marker",
        PublicLayer = "A public card.",
        Chapters = [Chapter.Key],
        BeaconScanAt = Now.AddDays(-100),
        PickedAt = Now.AddDays(-100),
        CluesGiven = 11,
    };

    private readonly string _folder = Path.Combine(Path.GetTempPath(), "d47-story-goal", Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        GC.SuppressFinalize(this);

        if (Directory.Exists(_folder))
        {
            Directory.Delete(_folder, recursive: true);
        }
    }

    private static AdventureStanding ThreeBonds() =>
        Enumerable.Range(1, 3)
            .Select(minutes => Event($$"""{ "timestamp":"{{Stamp(Accepted.AddMinutes(minutes))}}", "event":"FactionKillBond", "Reward":1, "AwardingFaction":"{{Labour}}", "VictimFaction":"X" }"""))
            .Aggregate(AdventureFold.Start(Chapter), AdventureFold.Apply);

    private static GoalStanding Goal(Story story) => StoryGoal.Of([story], _ => ThreeBonds())!;

    [Fact]
    public void ARunningStoryShowsItsClueCountStageAndBeat()
    {
        var goal = Goal(Running);

        Assert.Equal(GoalKind.Story, goal.Arc.Kind);
        Assert.Equal("The Marker", goal.Arc.Name);
        Assert.Equal(11, goal.Have);
        Assert.Equal(18, goal.Need);
        Assert.Equal("Bad Guys Close In, clue 11 of 18. Now: kill bonds for LTT 7786 Labour, 3 of 8", goal.Note);
    }

    [Fact]
    public void PausingOrSwitchingOffTheStoryChangesTheNote()
    {
        Assert.Equal("Paused at Bad Guys Close In, clue 11 of 18", Goal(Running.Paused(Now)).Note);
        Assert.StartsWith("Switched off at Bad Guys Close In, clue 11 of 18", Goal(Running.SwitchedOff(Now)).Note, StringComparison.Ordinal);
    }

    [Fact]
    public void AnAbandonedOrEndedStoryIsNotShown()
    {
        Assert.Null(StoryGoal.Of([Running with { State = StoryState.Abandoned }], _ => null));
        Assert.Null(StoryGoal.Of([Running with { State = StoryState.Ended }], _ => null));
    }

    [Fact]
    public void AFinishedStoryIsDone()
    {
        var goal = StoryGoal.Of([Running with { State = StoryState.Finished, StoppedAt = Now }], _ => null)!;

        Assert.True(goal.IsDone);
    }

    [Fact]
    public void TheGoalCanBeRemovedButNotFinishedByHand()
    {
        var store = new GoalStore(Path.Combine(_folder, "goals.json"), new MemoryFileSystem(), NullLogger<GoalStore>.Instance);
        store.Poll();

        var book = new GoalBook(store, () => "F1", () => null) { Story = () => Goal(Running) };
        var key = StoryGoal.KeyPrefix + Running.Id;

        Assert.Contains(book.Standings, standing => standing.Arc.Key == key);
        Assert.Contains("worked out from your journal", book.Finish(key, true, Now), StringComparison.Ordinal);

        book.Remove(key);

        Assert.DoesNotContain(book.Standings, standing => standing.Arc.Key == key);
    }
}
