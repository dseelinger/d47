using System.Text.Json;
using D47.Core.Goals;
using D47.Core.Journal;
using Xunit;

namespace D47.Core.Tests.Goals;

/// <summary>A rank goal counts the percent into the current rank as well as the whole ranks.</summary>
public class ARankGoalShowsItsLadderAndItsRungTests
{
    private static readonly DateTimeOffset Now = new(3311, 6, 1, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public void EliteIVAtFiftySixPercentIsTwelvePointFiveSixOfThirteen()
    {
        var standing = Evaluate("rank.trade", State("\"Trade\":12", "\"Trade\":56"), null);

        Assert.Equal(0.56, standing.Rung!.Value, 6);
        Assert.Equal(12.56 / 13, standing.Fraction!.Value, 6);
        Assert.Equal("Elite V", standing.NextRank);
        Assert.Equal("Elite IV, 56% into it", standing.Note);
    }

    [Fact]
    public void AfterAPromotionTheRungIsUnknownUntilTheNextProgress()
    {
        var state = State("\"Trade\":12", "\"Trade\":56");
        state.Apply(Event("Promotion", "\"Trade\":13"));
        state.Apply(Event("Promotion", "\"Explore\":6"));

        var explore = Evaluate("rank.explore", state, null);

        Assert.Null(explore.Rung);
        Assert.Equal(6.0 / 13, explore.Fraction!.Value, 6);
        Assert.Equal("Pioneer", explore.NextRank);
    }

    [Fact]
    public void TheTopRankHasNeitherARungNorANextRank()
    {
        var standing = Evaluate("rank.trade", State("\"Trade\":13", "\"Trade\":40"), null);

        Assert.True(standing.IsDone);
        Assert.Null(standing.Rung);
        Assert.Null(standing.NextRank);
        Assert.Equal("Elite V", standing.Note);
    }

    [Fact]
    public void AMinedStandingHasNoRungAndCountsWholeRanks()
    {
        var mine = new GoalMine
        {
            FrontierId = "F1",
            MinedAt = Now,
            Journals = 1,
            From = Now,
            To = Now,
            Marks = [new GoalMark { Key = "rank.trade", Have = 12, AsOf = Now, Started = Now }],
        };

        var standing = Evaluate("rank.trade", null, mine);

        Assert.Null(standing.Rung);
        Assert.Equal(12.0 / 13, standing.Fraction!.Value, 6);
    }

    [Theory]
    [InlineData(13, false)]
    [InlineData(14, true)]
    public void ANavyGoalIsDoneAtRankFourteen(int rank, bool done)
    {
        var standing = Evaluate("rank.federation", State($"\"Federation\":{rank}", "\"Federation\":10"), null);

        Assert.Equal(done, standing.IsDone);
        Assert.Equal(done ? null : "Admiral", standing.NextRank);
    }

    [Fact]
    public void TheNavyGoalsSayTheirTopRank()
    {
        Assert.Equal("Empire rank 14 — King.", GoalCatalogue.Find("rank.empire")!.Done);
        Assert.Equal("Federation rank 14 — Admiral.", GoalCatalogue.Find("rank.federation")!.Done);
    }

    [Fact]
    public void TheCareerGoalsAreNamedForEliteV()
    {
        var names = GoalCatalogue.Every.Where(arc => arc.Key.StartsWith("rank.", StringComparison.Ordinal))
            .Take(5).Select(arc => arc.Name);

        Assert.Equal(
            ["Elite V in Combat", "Elite V in Trade", "Elite V in Exploration", "Elite V as a Mercenary", "Elite V in Exobiology"],
            names);
    }

    private static GoalStanding Evaluate(string key, CommanderGameState? state, GoalMine? mine) =>
        GoalEvaluator.Evaluate(GoalCatalogue.Find(key)!, state, mine);

    private static CommanderGameState State(string rank, string progress)
    {
        var state = new CommanderGameState(new CommanderIdentity("F1", "Jameson"));
        state.Apply(Event("Rank", rank));
        state.Apply(Event("Progress", progress));

        return state;
    }

    private static JournalEvent Event(string kind, string fields)
    {
        var text = $"{{ \"timestamp\":\"{Now.UtcDateTime:yyyy-MM-ddTHH:mm:ssZ}\", \"event\":\"{kind}\", {fields} }}";

        return new JournalEvent(Now, kind, JsonDocument.Parse(text).RootElement);
    }
}
