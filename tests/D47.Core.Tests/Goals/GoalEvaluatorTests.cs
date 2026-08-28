using System.Text.Json;
using D47.Core.Goals;
using D47.Core.Journal;
using Xunit;

namespace D47.Core.Tests.Goals;

/// <summary>
/// What every arc is worth right now (Phase 34, "Progress is derived, never typed").
/// <para>
/// The precedence is the subject: live state answers now, the mine answers history, and neither is
/// a real answer rather than a zero. That last one is item 2's sentence and it is the assertion
/// this file exists for.
/// </para>
/// </summary>
public class GoalEvaluatorTests
{
    private static readonly DateTimeOffset Now = new(3311, 6, 1, 0, 0, 0, TimeSpan.Zero);

    private static readonly DateTimeOffset Long = Now.AddDays(-400);

    [Fact]
    public void LiveStateIsPreferredToTheMine()
    {
        var state = State(Event("Rank", "\"Combat\":6"));
        var mine = Mine(new GoalMark { Key = "rank.combat", Have = 2, AsOf = Long, Started = Long });

        var standing = Evaluate("rank.combat", state, mine);

        Assert.Equal(6, standing.Have);
        Assert.Equal(GoalSource.Live, standing.Source);

        // The start still comes from the mine, because live state cannot know it. One arc, two
        // sources, and the "as of" stamp belongs to the figure rather than to the arc.
        Assert.Equal(Long, standing.Started);
    }

    [Fact]
    public void WithoutLiveStateTheMinedFigureStandsAndSaysWhenItWasTrue()
    {
        var standing = Evaluate(
            "rank.combat",
            null,
            Mine(new GoalMark { Key = "rank.combat", Have = 2, AsOf = Long, Started = Long }));

        Assert.Equal(2, standing.Have);
        Assert.Equal(GoalSource.Mined, standing.Source);
        Assert.Equal(Long, standing.AsOf);
        Assert.Contains("as of", standing.Describe(Now), StringComparison.Ordinal);
    }

    /// <summary>
    /// <b>Item 2's whole sentence, as one assertion.</b> An arc nothing can currently evaluate must
    /// not read as nothing achieved — a Commander told they are at zero engineers because the game
    /// has not started yet has been lied to about themselves.
    /// </summary>
    [Fact]
    public void NothingKnownIsNotNothingAchieved()
    {
        var standing = Evaluate(GoalCatalogue.Engineers, null, null);

        Assert.Null(standing.Have);
        Assert.Equal(GoalSource.Unknown, standing.Source);
        Assert.Null(standing.Fraction);
        Assert.Contains("cannot say", standing.Describe(Now), StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("0 of", standing.Describe(Now), StringComparison.Ordinal);
    }

    [Fact]
    public void EliteIsTheDefinitionOfDoneAndIsTheOnlyRankNamed()
    {
        var standing = Evaluate("rank.trade", State(Event("Rank", "\"Trade\":8")), null);

        Assert.True(standing.IsDone);
        Assert.Contains("Elite", standing.Describe(Now), StringComparison.Ordinal);
    }

    [Fact]
    public void UnlockedEngineersAreCountedAgainstTheDirectory()
    {
        var state = State(Event(
            "EngineerProgress",
            "\"Engineers\":[{\"Engineer\":\"A\",\"EngineerID\":1,\"Progress\":\"Unlocked\"},"
            + "{\"Engineer\":\"B\",\"EngineerID\":2,\"Progress\":\"Invited\"}]"));

        var standing = Evaluate(GoalCatalogue.Engineers, state, null);

        Assert.Equal(1, standing.Have);
        Assert.Equal(D47.Core.Knowledge.EngineerDirectory.All.Count, standing.Need);
        Assert.Equal(GoalSource.Live, standing.Source);
    }

    /// <summary>
    /// The fleet snapshot is taken at a shipyard and does not list the hull the Commander arrived
    /// in, so counting only the snapshot loses one hull for as long as they are sitting in it.
    /// </summary>
    [Fact]
    public void TheHullBeingFlownCountsTowardsTheCollection()
    {
        var state = State(
            Event("StoredShips", "\"ShipsHere\":[{\"ShipID\":1,\"ShipType\":\"adder\",\"StarSystem\":\"Sol\"}],\"ShipsRemote\":[]"),
            Event("Loadout", "\"Ship\":\"krait_mkii\",\"ShipID\":2"));

        Assert.Equal(2, Evaluate(GoalCatalogue.Ships, state, null).Have);
    }

    [Fact]
    public void AMilestoneArcTargetsTheNextRung()
    {
        var standing = Evaluate(
            GoalCatalogue.Systems,
            null,
            Mine(new GoalMark { Key = GoalCatalogue.Systems, Have = 4_182, AsOf = Now, Started = Long }));

        Assert.Equal(5_000, standing.Need);
        Assert.False(standing.IsDone);
    }

    [Fact]
    public void PastTheTopRungAMilestoneArcIsFinished()
    {
        var standing = Evaluate(
            GoalCatalogue.Distance,
            null,
            Mine(new GoalMark { Key = GoalCatalogue.Distance, Have = 2_000_000, AsOf = Now, Started = Long }));

        Assert.True(standing.IsDone);
        Assert.Null(standing.Need);
    }

    [Fact]
    public void AgeIsMeasuredFromTheStartAndReadsInTheUnitThatFits()
    {
        var standing = Evaluate(
            GoalCatalogue.Systems,
            null,
            Mine(new GoalMark { Key = GoalCatalogue.Systems, Have = 12, AsOf = Now, Started = Now.AddDays(-45) }));

        Assert.Equal(45, standing.Age(Now)?.Days);
        Assert.Contains("6 weeks", standing.Describe(Now), StringComparison.Ordinal);
    }

    [Fact]
    public void AnAuthoredArcIsThePersonsToCallDone()
    {
        var arc = new GoalArc
        {
            Key = "mine.thargoid-war",
            Name = "See the war out",
            Done = "When it ends.",
            Kind = GoalKind.Authored,
            Written = Long,
        };

        var standing = GoalEvaluator.Evaluate(arc, null, null);

        Assert.Equal(GoalSource.Commander, standing.Source);
        Assert.False(standing.IsDone);
        Assert.Equal(Long, standing.Started);
    }

    private static GoalStanding Evaluate(string key, CommanderGameState? state, GoalMine? mine) =>
        GoalEvaluator.Evaluate(GoalCatalogue.Find(key)!, state, mine);

    private static GoalMine Mine(params GoalMark[] marks) =>
        new() { FrontierId = "F1", MinedAt = Now, Journals = 900, From = Long, To = Now, Marks = marks };

    private static CommanderGameState State(params JournalEvent[] events)
    {
        var state = new CommanderGameState(new CommanderIdentity("F1", "Jameson"));

        foreach (var journalEvent in events)
        {
            state.Apply(journalEvent);
        }

        return state;
    }

    private static JournalEvent Event(string kind, string fields)
    {
        var text = $"{{ \"timestamp\":\"{Now.UtcDateTime:yyyy-MM-ddTHH:mm:ssZ}\", \"event\":\"{kind}\", {fields} }}";

        return new JournalEvent(Now, kind, JsonDocument.Parse(text).RootElement);
    }
}
