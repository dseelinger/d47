using D47.Core.Knowledge;
using Xunit;

namespace D47.Core.Tests.Goals;

public sealed class APowerplayGoalListsItsBenefitsTests
{
    private static PowerplayBenefits LiYongRuiAtEight() => PowerplayBenefits.For("Li Yong-Rui", 8)!;

    [Fact]
    public void HeldNamesEachPerkAtItsLatestValueAndTheRankItChanged()
    {
        var held = LiYongRuiAtEight();

        Assert.Equal(
            [
                ("rebuy in Li Yong-Rui's territory", "−40%", 8),
                ("exploration data sales", "+20%", 5),
            ],
            held.Perks.Select(p => (p.Name, p.Value, p.Since)));
    }

    [Fact]
    public void HeldWithNoModuleNamesTheFirstOneAndItsRank()
    {
        var held = LiYongRuiAtEight();

        Assert.Empty(held.Modules);
        Assert.Equal(34, held.FirstModule!.Rank);
        Assert.Equal("Pack-Hound Missile Rack", held.FirstModule.Name);
    }

    [Fact]
    public void HeldCountsMiniCarePackagesAndTheDecal()
    {
        var held = LiYongRuiAtEight();

        Assert.Equal([3, 4, 6, 7], held.CarePackageRanks);
        Assert.True(held.HasDecal);
    }

    [Fact]
    public void ToComeShowsEachChangeAsOldToNew()
    {
        var chapters = LiYongRuiAtEight().ToCome;
        var rows = chapters.SelectMany(c => c.Rows).GroupBy(r => r.Rank).ToDictionary(g => g.Key, g => g.First());

        Assert.Equal("+20% → +30%", rows[14].Change);
        Assert.Equal("exploration data sales", rows[14].Name);
        Assert.Equal("−33%", rows[11].Change);
        Assert.Equal("rebuy when a rival Power's ship kills you", rows[11].Name);
        Assert.Equal("MODULE", rows[34].Change);
    }

    [Fact]
    public void ToComeStartsWithTheRestOfTheCurrentChapter()
    {
        var chapters = LiYongRuiAtEight().ToCome;

        Assert.True(chapters[0].IsCurrent);
        Assert.Equal("2–10", chapters[0].Label);
        Assert.Empty(chapters[0].Rows);
        Assert.Equal("Mini care package at ranks 9, 10.", chapters[0].CarePackages);
        Assert.Equal("11–20", chapters[1].Label);
        Assert.False(chapters[1].IsCurrent);
        Assert.Equal(10, chapters.Count);
    }

    [Fact]
    public void ARankWithTwoChangesHasTwoRows()
    {
        var rows = LiYongRuiAtEight().ToCome.SelectMany(c => c.Rows).Where(r => r.Rank == 86).ToList();

        Assert.Equal(2, rows.Count);
        Assert.Contains(rows, r => r.Name == "exploration data sales");
        Assert.Contains(rows, r => r.Name == "rearm, refuel and repair prices");
    }

    [Fact]
    public void TheTopRankHasNothingToCome()
    {
        Assert.Empty(PowerplayBenefits.For("Li Yong-Rui", 100)!.ToCome);
    }

    [Fact]
    public void AnUnknownPowerHasNoBenefits()
    {
        Assert.Null(PowerplayBenefits.For("Nobody", 8));
    }
}
