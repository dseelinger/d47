using D47.Core.Knowledge;
using Xunit;

namespace D47.Core.Tests.Knowledge;

public class APowerHasItsRanksTests
{
    private static readonly string[] Journal =
    [
        "Zemina Torval", "Felicia Winters", "Li Yong-Rui", "Archon Delaine", "Nakato Kaine", "Jerome Archer",
        "Denton Patreus", "Pranav Antal", "Yuri Grom", "A. Lavigny-Duval", "Edmund Mahon", "Aisling Duval",
    ];

    [Fact]
    public void TheTableNamesTheTwelvePowersAsTheJournalSpellsThem() =>
        Assert.Equal(Journal.Order(), PowerplayRanks.Powers.Order());

    [Fact]
    public void EveryPowerplayModuleUnlocksForEveryPower()
    {
        var entitlements = EliteSpecifications.Modules
            .Where(m => m.NeedsPledge)
            .Select(m => m.Entitlement!)
            .Distinct()
            .ToList();

        Assert.Equal(12, entitlements.Count);

        foreach (var power in PowerplayRanks.Powers)
        {
            foreach (var entitlement in entitlements)
            {
                Assert.True(
                    PowerplayRanks.RankUnlocking(power, entitlement) is not null,
                    $"{power} has no rank for {entitlement}");
            }
        }
    }

    [Fact]
    public void LiYongRuiUnlocksThePackHoundAtRankThirtyFour() =>
        Assert.Equal(34, PowerplayRanks.RankUnlocking("li yong-rui", "ELITE_SPECIFIC_V_POWER_200080"));

    [Fact]
    public void RankNineNeedsFortySevenThousandMerits() =>
        Assert.Equal(47_000, PowerplayRanks.MeritsNeeded(9));

    [Fact]
    public void EveryRankPastOneHundredGivesAFullCarePackage()
    {
        Assert.False(PowerplayRanks.GivesFullCarePackage(100));
        Assert.True(PowerplayRanks.GivesFullCarePackage(101));
        Assert.Null(PowerplayRanks.MeritsNeeded(101));
    }

    [Fact]
    public void EveryRankHasItsMeritFigure()
    {
        for (var rank = 1; rank <= PowerplayRanks.LastTabulatedRank; rank++)
        {
            Assert.NotNull(PowerplayRanks.MeritsNeeded(rank));
        }
    }

    [Fact]
    public void EveryPowerAgreesOnTheMeritLadder()
    {
        foreach (var reward in PowerplayRanks.Powers.SelectMany(PowerplayRanks.RewardsFor))
        {
            Assert.Equal(PowerplayRanks.MeritsNeeded(reward.Rank), reward.Merits);
        }
    }

    [Fact]
    public void ARewardListIsInRankOrder()
    {
        var ranks = PowerplayRanks.RewardsFor("Edmund Mahon").Select(r => r.Rank).ToList();

        Assert.Equal(ranks.Order(), ranks);
        Assert.Empty(PowerplayRanks.RewardsFor("Nobody"));
    }

    [Fact]
    public void MahonsTradeBondReachesTwentyFivePercentAtRankFortyEight()
    {
        var last = PowerplayRanks.RewardsFor("Edmund Mahon")
            .Last(r => r.Kind == PowerplayRewardKind.Perk && r.Subject == "trade_sales");

        Assert.Equal(48, last.Rank);
        Assert.Equal(25, last.Value);
    }

    [Fact]
    public void EveryPerkKeyInTheTableHasASpokenLabel()
    {
        var keys = PowerplayRanks.Powers
            .SelectMany(PowerplayRanks.RewardsFor)
            .Where(r => r.Kind == PowerplayRewardKind.Perk)
            .Select(r => r.Subject)
            .Distinct()
            .ToList();

        Assert.NotEmpty(keys);

        foreach (var key in keys)
        {
            Assert.False(string.IsNullOrWhiteSpace(PowerplayRanks.PerkLabel(key)), key);
        }
    }
}
