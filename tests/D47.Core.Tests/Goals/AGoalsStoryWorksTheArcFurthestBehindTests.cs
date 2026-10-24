using D47.Core.Adventures;
using D47.Core.Engineers;
using D47.Core.Goals;
using D47.Core.Journal;
using D47.Core.Knowledge;
using D47.Core.Stories;
using D47.Core.Tests.Adventures;
using Xunit;
using static D47.Core.Tests.Stories.StoryFixtures;

namespace D47.Core.Tests.Goals;

/// <summary>A Goals story chapter works the unfinished arc with the lowest share of its whole, and carries one step for it.</summary>
public sealed class AGoalsStoryWorksTheArcFurthestBehindTests
{
    /// <summary>Every built-in arc at <paramref name="fraction"/> of its whole, with the overrides in <paramref name="set"/>.</summary>
    private static List<GoalStanding> Standings(double fraction, params GoalStanding[] set) =>
    [
        .. GoalCatalogue.Every.Select(arc => set.FirstOrDefault(standing => standing.Arc.Key == arc.Key) ?? At(arc.Key, fraction)),
    ];

    private static GoalStanding At(string key, double fraction, double? rung = null) => new()
    {
        Arc = GoalCatalogue.Find(key)!,
        Have = (long)Math.Round(fraction * 1000),
        Need = 1000,
        Rung = rung,
        Source = GoalSource.Live,
    };

    private static GoalStanding Rank(string key, int rank, double? rung) => new()
    {
        Arc = GoalCatalogue.Find(key)!,
        Have = rank,
        Need = RankStanding.EliteTop,
        Rung = rung,
        Source = GoalSource.Live,
    };

    private static GoalStanding Done(GoalStanding standing) => standing with { IsDone = true };

    private static CommanderGameState State(long credits = 1_000_000_000, params string[] events)
    {
        var state = Loaded(credits);

        foreach (var json in events)
        {
            state.Apply(AdventureFixtures.Event(json));
        }

        return state;
    }

    [Fact]
    public void TheLowestFractionWins()
    {
        var step = GoalClimb.Next(Standings(0.5, At("rank.explore", 0.2)), State(), null);

        Assert.Equal("rank.explore", step?.Key);
        Assert.Equal("Explore", step?.Draw);
        Assert.Null(step?.Objective);
    }

    [Fact]
    public void ANullFractionCountsAsZero()
    {
        var unknown = new GoalStanding { Arc = GoalCatalogue.Find("rank.trade")!, Source = GoalSource.Unknown };

        Assert.Null(unknown.Fraction);
        Assert.Equal("rank.trade", GoalClimb.Next(Standings(0.01, unknown), State(), null)?.Key);
    }

    [Fact]
    public void TiesGoInCatalogueOrder()
    {
        Assert.Equal("rank.combat", GoalClimb.Next(Standings(0.4), State(), null)?.Key);
        Assert.Equal("rank.trade", GoalClimb.Next(Standings(0.4, Done(At("rank.combat", 0.4))), State(), null)?.Key);
    }

    [Fact]
    public void AnUnaffordableHullIsSkippedForTheNextLowestArc()
    {
        var standings = Standings(0.6, At(GoalCatalogue.Ships, 0.1), At("rank.trade", 0.3));

        var poor = GoalClimb.Next(standings, State(credits: 1_000), null);
        var rich = GoalClimb.Next(standings, State(), null);

        Assert.Equal("rank.trade", poor?.Key);
        Assert.Equal(GoalCatalogue.Ships, rich?.Key);
        Assert.Equal(TriggerKind.Board, rich?.Objective?.Kind);
        Assert.Equal(GoalBook.NextHull(State())!.Symbol, rich?.Objective?.ShipType);
    }

    [Fact]
    public void AnUnpledgedCommanderGetsAPledgeStep()
    {
        var unpledged = State(events: """{ "timestamp":"2026-09-30T12:00:00Z", "event":"PowerplayLeave", "Power":"Aisling Duval" }""");
        var standing = GoalEvaluator.Evaluate(GoalCatalogue.Find(GoalCatalogue.Powerplay)!, unpledged, null);

        var step = GoalClimb.Next(Standings(0.5, standing), unpledged, null);

        Assert.Equal(GoalCatalogue.Powerplay, step?.Key);
        Assert.Equal(TriggerKind.Pledge, step?.Objective?.Kind);
    }

    [Fact]
    public void APledgedCommanderIsAskedForTheNextPowerplayRank()
    {
        var pledged = State(events: """{ "timestamp":"2026-09-30T12:00:00Z", "event":"Powerplay", "Power":"Aisling Duval", "Rank":20, "Merits":100000 }""");
        var standing = GoalEvaluator.Evaluate(GoalCatalogue.Find(GoalCatalogue.Powerplay)!, pledged, null);

        var step = GoalClimb.Next(Standings(0.99, standing), pledged, null);

        Assert.Equal(TriggerKind.PowerRank, step?.Objective?.Kind);
        Assert.Equal(21, step?.Objective?.Rank);
    }

    [Fact]
    public void APledgeNobodyHasReadIsPassedOver()
    {
        var standing = GoalEvaluator.Evaluate(GoalCatalogue.Find(GoalCatalogue.Powerplay)!, State(), null);

        Assert.Equal("rank.trade", GoalClimb.Next(Standings(0.5, standing, At("rank.trade", 0.2)), State(), null)?.Key);
    }

    [Fact]
    public void TheEngineersStepIsTheFirstOnTheRouteAtTheStageItNeeds()
    {
        var engineer = EngineerDirectory.All[0];
        var report = new EngineerReport { Route = [new UnlockCandidate { Engineer = engineer, Chain = UnlockChain.Done }] };

        var step = GoalClimb.Next(Standings(0.5, At(GoalCatalogue.Engineers, 0.1)), State(), report);

        Assert.Equal(TriggerKind.Engineer, step?.Objective?.Kind);
        Assert.Equal(engineer.Name, step?.Objective?.Engineer);
        Assert.Equal("Invited", step?.Objective?.Stage);
        Assert.Equal("rank.combat", GoalClimb.Next(Standings(0.5, At(GoalCatalogue.Engineers, 0.1)), State(), null)?.Key);
    }

    [Fact]
    public void NothingComesBackOnceEveryArcIsDone()
    {
        Assert.Null(GoalClimb.Next([.. Standings(1).Select(Done)], State(), null));
    }

    [Fact]
    public void ACareerNearItsNextRankIsAskedForThePromotion()
    {
        var step = GoalClimb.Next(Standings(0.9, Rank("rank.combat", 5, 0.8)), State(), null);

        Assert.Equal(TriggerKind.Rank, step?.Objective?.Kind);
        Assert.Equal("Combat", step?.Objective?.Career);
        Assert.Equal(6, step?.Objective?.Rank);
        Assert.Null(step?.Draw);
    }

    [Fact]
    public void ANavyNearItsNextRankIsAskedForThePromotion()
    {
        var step = GoalClimb.Next(Standings(0.9, Rank("rank.empire", 3, 0.9) with { Need = GoalCatalogue.NavyTop }), State(), null);

        Assert.Equal(TriggerKind.Rank, step?.Objective?.Kind);
        Assert.Equal(Careers.Empire, step?.Objective?.Career);
        Assert.Equal(4, step?.Objective?.Rank);
    }

    [Fact]
    public void ACareerFarFromItsNextRankLimitsTheDrawToItsRows()
    {
        var step = GoalClimb.Next(Standings(0.9, Rank("rank.explore", 5, 0.3)), State(), null);

        Assert.Equal("Explore", step?.Draw);

        foreach (var chapter in Enumerable.Range(1, 40))
        {
            var drawn = Draw(step!, ChapterDraw.Seed("the-test-story", chapter));

            Assert.NotEmpty(drawn);
            Assert.All(drawn, slot => Assert.True(step!.Earns(ChapterActivities.Find(slot.Kind, slot.Family, slot.Organic)!), $"{slot.Kind} {slot.Family}"));
        }
    }

    [Fact]
    public void AMercenaryArcTheCommanderCannotBeDrawnForIsPassedOver()
    {
        var step = GoalClimb.Next(Standings(0.5, Rank("rank.soldier", 0, 0.1), At("rank.trade", 0.2)), State(), null);

        Assert.Equal("rank.trade", step?.Key);
    }

    [Fact]
    public void ACareerWhoseRowsTheDrawWouldRefuseIsPassedOver()
    {
        var standings = Standings(0.5, Rank("rank.explore", 2, 0.1), At("rank.trade", 0.2));

        var refused = GoalClimb.Next(standings, State(), null, row => !GoalClimb.Earns("Explore", row));

        Assert.Equal("rank.explore", GoalClimb.Next(standings, State(), null)?.Key);
        Assert.Equal("rank.trade", refused?.Key);
    }

    [Fact]
    public void ARequiredObjectiveTakesTheLastSlot()
    {
        var drawn = ChapterDraw.Draw(
            CareerWeights.Even, RankState.Empty, ShipLoadout.Unknown, odyssey: true, [], null, comfort: false, CareerStatistics.Empty, 0, 7u,
            slots: ChapterDraw.Slots - 1);

        Assert.Equal(ChapterDraw.Slots - 1, drawn.Count);
    }

    [Fact]
    public void ALimitThatLeavesNoRowFallsBackToTheWholeTable()
    {
        var plain = ChapterDraw.Draw(CareerWeights.Even, RankState.Empty, ShipLoadout.Unknown, odyssey: true, [], null, comfort: false, CareerStatistics.Empty, 0, 11u);
        var limited = ChapterDraw.Draw(
            CareerWeights.Even, RankState.Empty, ShipLoadout.Unknown, odyssey: true, [], null, comfort: false, CareerStatistics.Empty, 0, 11u, limit: _ => false);

        Assert.Equal(plain, limited);
    }

    [Theory]
    [InlineData("Combat", TriggerKind.Bounty, null, null, true)]
    [InlineData("Combat", TriggerKind.Mission, "Mission_Massacre", null, true)]
    [InlineData("Combat", TriggerKind.Mission, "Mission_OnFoot_Massacre", null, false)]
    [InlineData("Soldier", TriggerKind.Mission, "Mission_OnFoot_Massacre", null, true)]
    [InlineData("Trade", TriggerKind.Mission, "Mission_Courier", null, true)]
    [InlineData("Explore", TriggerKind.Signal, null, null, false)]
    [InlineData("Explore", TriggerKind.DataSale, null, false, true)]
    [InlineData("Explore", TriggerKind.DataSale, null, true, false)]
    [InlineData("Exobiologist", TriggerKind.DataSale, null, true, true)]
    [InlineData("Federation", TriggerKind.Mission, "Mission_Courier", null, true)]
    [InlineData("Federation", TriggerKind.Bounty, null, null, false)]
    public void EachLadderEarnsFromItsOwnRows(string ladder, TriggerKind kind, string? family, bool? organic, bool earns)
    {
        Assert.Equal(earns, GoalClimb.Earns(ladder, ChapterActivities.Find(kind, family, organic)!));
    }

    private static IReadOnlyList<DrawnActivity> Draw(GoalClimbStep step, uint seed) =>
        ChapterDraw.Draw(
            CareerWeights.Even,
            RankState.Empty,
            ShipLoadout.Unknown,
            odyssey: true,
            [],
            null,
            comfort: seed % 3 == 0,
            CareerStatistics.Empty,
            0,
            seed,
            events: StepEventCatalogue.Default,
            tools: [new DrawableTool("plot_trade_route", "plot a trade route")],
            limit: step.Earns);
}
