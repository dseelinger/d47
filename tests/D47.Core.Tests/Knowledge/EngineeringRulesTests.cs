using D47.Core.Knowledge;
using Xunit;

namespace D47.Core.Tests.Knowledge;

/// <summary>
/// The fixed game rules, checked against the numbers a 912-journal corpus actually measured.
/// <para>
/// Not against the published table alone. The published table was right about the shape and wrong
/// in two places — 34% is really a third, and a grade completes at 0.85 rather than 1.0 — so a
/// test suite that only reproduced what Frontier printed would pass while d47 told Commanders
/// their finished modules were unfinished.
/// </para>
/// </summary>
public class EngineeringRulesTests
{
    [Theory]
    // Frontier's published table, cell by cell, as rolls rather than percentages.
    [InlineData(1, 1, 5)] [InlineData(1, 2, 4)] [InlineData(1, 3, 3)] [InlineData(1, 4, 2)] [InlineData(1, 5, 1)]
    [InlineData(2, 2, 5)] [InlineData(2, 3, 4)] [InlineData(2, 4, 3)] [InlineData(2, 5, 2)]
    [InlineData(3, 3, 5)] [InlineData(3, 4, 4)] [InlineData(3, 5, 3)]
    [InlineData(4, 4, 5)] [InlineData(4, 5, 4)]
    [InlineData(5, 5, 5)]
    public void ThePublishedRollTableIsReproducedExactly(int grade, int rank, int rolls) =>
        Assert.Equal(rolls, EngineeringRules.RollsFor(grade, rank));

    [Fact]
    public void RankBelowGradeIsUnreachableRatherThanSlow()
    {
        // Every dash in the published table. A gate is something no amount of gathering fixes, so
        // it has to answer null and not a large number of rolls.
        for (var grade = 2; grade <= 5; grade++)
        {
            for (var rank = 1; rank < grade; rank++)
            {
                Assert.Null(EngineeringRules.RollsFor(grade, rank));
                Assert.Null(EngineeringRules.ProgressPerRoll(grade, rank));
                Assert.Null(EngineeringRules.RollsRemaining(grade, rank, 0.5));
            }
        }
    }

    [Fact]
    public void GradeFiveAdmitsExactlyOneStepSize()
    {
        // The corpus's strongest single result: 1,428 of 1,428 grade 5 deltas are 0.2, which is
        // what "grade N is unreachable below rank N" predicts and what nothing else does. A grade 5
        // blueprint rollable at rank 4 would have shown a smaller step somewhere in 1,428 samples.
        Assert.Equal(0.2, EngineeringRules.ProgressPerRoll(5, 5)!.Value, 10);

        for (var rank = 1; rank <= 4; rank++)
        {
            Assert.Null(EngineeringRules.ProgressPerRoll(5, rank));
        }
    }

    [Fact]
    public void ThePublishedThirtyFourPercentIsReallyAThird()
    {
        // Observed deltas are 0.333 and 0.334, never 0.34. Three rolls therefore reach 0.99 rather
        // than overshooting, and 0.99 has to count as finished — see the next test.
        var step = EngineeringRules.ProgressPerRoll(3, 5)!.Value;

        Assert.Equal(1.0 / 3.0, step, 10);
        Assert.NotEqual(0.34, step, 3);
    }

    [Fact]
    public void AGradeSittingAtPointNineNineIsFinished()
    {
        // The bug that would have shipped. 93% of grades reach exactly 1.0, so gating on that would
        // have looked correct in testing and told the other 7% their finished module was not.
        Assert.Equal(0, EngineeringRules.RollsRemaining(3, 5, 0.99));
        Assert.Equal(0, EngineeringRules.RollsRemaining(5, 5, 0.85));
        Assert.Equal(0, EngineeringRules.RollsRemaining(1, 1, 0.95));
    }

    [Fact]
    public void BelowTheBandTheRollsRemainingCountTowardsAFullGrade()
    {
        // 0.8 and below is genuinely in progress — every abandoned grind in the corpus sat there.
        // Counted to 1.0 rather than to 0.85, because the band is where the game stops insisting
        // and not a target: a Commander finishing the grade properly needs the larger number.
        Assert.Equal(4, EngineeringRules.RollsRemaining(5, 5, 0.2));
        Assert.Equal(5, EngineeringRules.RollsRemaining(5, 5, 0.0));
        Assert.Equal(1, EngineeringRules.RollsRemaining(1, 5, 0.0));
    }

    [Theory]
    // The corpus's own ratio table, paid : received, over 1,096 trades. Reduced to lowest terms,
    // which is the same exchange and the number a Commander should actually hand over.
    [InlineData(5, 1, true, 1, 81)]   [InlineData(5, 1, false, 2, 27)]
    [InlineData(4, 1, true, 1, 27)]   [InlineData(4, 1, false, 2, 9)]
    [InlineData(3, 1, true, 1, 9)]    [InlineData(3, 1, false, 2, 3)]
    [InlineData(2, 1, true, 1, 3)]    [InlineData(2, 1, false, 2, 1)]
    [InlineData(1, 2, true, 6, 1)]    [InlineData(1, 2, false, 36, 1)]
    [InlineData(1, 3, true, 36, 1)]   [InlineData(1, 3, false, 216, 1)]
    [InlineData(1, 4, true, 216, 1)]
    public void TheTradeRateMatchesWhatTradersActuallyCharged(
        int from, int to, bool sameLine, int paid, int received)
    {
        var exchange = EngineeringRules.TradeRate(from, to, sameLine);

        Assert.NotNull(exchange);
        Assert.Equal(new MaterialExchange(paid, received), exchange);
    }

    [Fact]
    public void ASameGradeTradeIsAlwaysCrossLine()
    {
        // Within a line a grade names exactly one material, so a same-grade same-line exchange is
        // a material for itself. The corpus has no same-line column at grade delta 0 for that
        // reason, and it charges 6 for 1 across lines.
        Assert.Null(EngineeringRules.TradeRate(3, 3, sameLine: true));
        Assert.Equal(new MaterialExchange(6, 1), EngineeringRules.TradeRate(3, 3, sameLine: false));
    }

    [Fact]
    public void SomeDefinedTradesAreOnesNobodyCanMake()
    {
        // Arithmetic the rules permit and the caps do not. Saying so is the answer; quoting the
        // number and letting the Commander discover it is the same failure as inventing one.
        Assert.True(EngineeringRules.IsBeyondCapacity(1, 5, sameLine: true));    // 1,296 against 300
        Assert.True(EngineeringRules.IsBeyondCapacity(3, 5, sameLine: false));   // 216 against 200
        Assert.True(EngineeringRules.IsBeyondCapacity(1, 4, sameLine: false));   // 1,296 against 300

        // And the ones on the right side of the line, including the largest trade in the corpus.
        Assert.False(EngineeringRules.IsBeyondCapacity(1, 4, sameLine: true));   // 216 against 300
        Assert.False(EngineeringRules.IsBeyondCapacity(1, 3, sameLine: false));  // 216 against 300
        Assert.False(EngineeringRules.IsBeyondCapacity(5, 1, sameLine: false));
    }

    [Fact]
    public void TheCostOfWhatIsWantedRoundsUpToWholeTrades()
    {
        // Trading down returns three at a time, so wanting four costs two trades' worth of input.
        var down = EngineeringRules.TradeRate(2, 1, sameLine: true)!.Value;

        Assert.Equal(1, down.CostOf(3));
        Assert.Equal(2, down.CostOf(4));
        Assert.Equal(0, down.CostOf(0));
    }

    [Fact]
    public void ReputationHasAPublishedPriceAndGradeOneHasNone()
    {
        Assert.Equal(500_000, EngineeringRules.ReputationCost(2));
        Assert.Equal(2_000_000, EngineeringRules.ReputationCost(3));
        Assert.Equal(8_000_000, EngineeringRules.ReputationCost(4));
        Assert.Equal(16_000_000, EngineeringRules.ReputationCost(5));

        // Grade 1 comes with the unlock. Null rather than zero: "no price" and "free" are
        // different claims and only one of them is checkable.
        Assert.Null(EngineeringRules.ReputationCost(1));
        Assert.Null(EngineeringRules.ReputationCost(6));
    }

    [Fact]
    public void GradesOutsideOneToFiveAreNotAnswered()
    {
        Assert.Null(EngineeringRules.RollsFor(0, 3));
        Assert.Null(EngineeringRules.RollsFor(6, 5));
        Assert.Null(EngineeringRules.TradeRate(0, 3, sameLine: true));
        Assert.Null(EngineeringRules.TradeRate(3, 6, sameLine: false));
        Assert.False(EngineeringRules.IsBeyondCapacity(0, 3, sameLine: true));
    }
}
