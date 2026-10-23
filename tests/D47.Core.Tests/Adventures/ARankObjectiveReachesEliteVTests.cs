using D47.Core.Adventures;
using Xunit;
using static D47.Core.Tests.Adventures.AdventureFixtures;

namespace D47.Core.Tests.Adventures;

/// <summary>A rank objective may name Elite I to Elite V in every career but CQC, and is worded by rank name.</summary>
public sealed class ARankObjectiveReachesEliteVTests
{
    private static Adventure Trade(int rank) => new()
    {
        Key = "trade-rank",
        Name = "Trade Rank",
        Source = AdventureSource.Generated,
        Beats = [Beat("Promotion", "finale", new AdventureTrigger { Kind = TriggerKind.Rank, Career = "Trade", Rank = rank }, "Well.")],
        AcceptedAt = Accepted,
    };

    [Fact]
    public void ATradeObjectiveAtEliteIIIFiresOnThePromotion()
    {
        var standing = AdventureFold.Apply(AdventureFold.Start(Trade(11)), Promotion("Trade", 11, Accepted.AddMinutes(1)));

        Assert.True(standing.IsDone);
        Assert.Empty(AdventureValidation.Problems(Trade(11)));
    }

    [Fact]
    public void APromotionShortOfTheAskedRankDoesNotFireIt()
    {
        var standing = AdventureFold.Apply(AdventureFold.Start(Trade(11)), Promotion("Trade", 10, Accepted.AddMinutes(1)));

        Assert.False(standing.IsDone);
    }

    [Fact]
    public void RanksFromEliteUpAreDescribedByName()
    {
        Assert.Equal("reach Elite in Combat", new AdventureTrigger { Kind = TriggerKind.Rank, Career = "Combat", Rank = 8 }.Describe());
        Assert.Equal("reach Elite III in Trade", new AdventureTrigger { Kind = TriggerKind.Rank, Career = "Trade", Rank = 11 }.Describe());
        Assert.Equal("reach Exploration rank 6", new AdventureTrigger { Kind = TriggerKind.Rank, Career = "Explore", Rank = 6 }.Describe());
    }

    [Fact]
    public void ARankPastEliteVIsRefused()
    {
        Assert.Contains(AdventureValidation.Problems(Trade(14)), problem => problem.Contains("ranks run 1 to 13"));
    }
}
