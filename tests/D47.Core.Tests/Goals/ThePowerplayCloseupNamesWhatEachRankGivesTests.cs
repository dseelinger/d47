using D47.Core.Knowledge;
using Xunit;

namespace D47.Core.Tests.Goals;

public sealed class ThePowerplayCloseupNamesWhatEachRankGivesTests
{
    private static Closeup LiYongRuiAtEight() => PowerplayBenefits.For("Li Yong-Rui", 8)!.Closeup!;

    [Fact]
    public void TheFirstChapterHasOneColumnForEachRankFromTwoToTen()
    {
        var closeup = LiYongRuiAtEight();

        Assert.Equal("2–10", closeup.Label);
        Assert.Equal(Enumerable.Range(2, 9), closeup.Ranks.Select(r => r.Rank));
    }

    [Fact]
    public void ARankListsItsPerkRebuyAndCarePackageInPlainWords()
    {
        var ranks = LiYongRuiAtEight().Ranks.ToDictionary(r => r.Rank, r => r.Rewards);

        Assert.Equal(["+20% exploration data sales"], ranks[5]);
        Assert.Equal(["Mini care package"], ranks[6]);
        Assert.Contains(ranks[8], words => words.EndsWith("rebuy in territory", StringComparison.Ordinal));
    }

    [Fact]
    public void ARankWithTwoRewardsListsBoth()
    {
        var closeup = PowerplayBenefits.For("Li Yong-Rui", 8)!.ToCome
            .SelectMany(c => c.Rows).Count(r => r.Rank == 86);

        Assert.Equal(2, closeup);

        var late = PowerplayBenefits.For("Li Yong-Rui", 85)!.Closeup!;
        Assert.Equal("81–90", late.Label);
        Assert.Contains(late.Ranks, rank => rank.Rank == 86 && rank.Rewards.Count >= 2);
    }

    [Fact]
    public void TheTopRankHasNoCloseup()
    {
        Assert.Null(PowerplayBenefits.For("Li Yong-Rui", 100)!.Closeup);
    }

    [Fact]
    public void TheCurrentStretchNamesItsFirstRankAhead()
    {
        var current = PowerplayBenefits.For("Li Yong-Rui", 8)!.ToCome[0];

        Assert.Equal((9, 10), (current.FirstAhead, current.Last));
    }
}
