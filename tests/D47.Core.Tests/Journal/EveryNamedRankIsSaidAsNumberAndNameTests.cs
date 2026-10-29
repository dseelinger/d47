using D47.Core.Journal;
using Xunit;

namespace D47.Core.Tests.Journal;

/// <summary>A rank on a ladder with names is said as career, number, top and name.</summary>
public sealed class EveryNamedRankIsSaidAsNumberAndNameTests
{
    [Theory]
    [InlineData("Combat", 0, "Combat Rank 0 of 13, Harmless")]
    [InlineData("Combat", 1, "Combat Rank 1 of 13, Mostly Harmless")]
    [InlineData("Explore", 3, "Exploration Rank 3 of 13, Surveyor")]
    [InlineData("Explore", 9, "Exploration Rank 9 of 13, Elite I")]
    [InlineData("Explore", 13, "Exploration Rank 13 of 13, Elite V")]
    [InlineData("Soldier", 2, "Mercenary Rank 2 of 13, Rookie")]
    [InlineData("CQC", 8, "CQC Rank 8 of 8, Elite")]
    [InlineData("Federation", 3, "Federal Navy Rank 3 of 14, Midshipman")]
    [InlineData("Empire", 7, "Imperial Navy Rank 7 of 14, Baron")]
    [InlineData("Empire", 0, "no Imperial Navy rank")]
    public void EachRowOfTheTableIsSaidExactly(string career, int rank, string said) =>
        Assert.Equal(said, new RankStanding(career, rank).Say(withPercent: false));

    [Fact]
    public void ThePercentFollowsTheName() =>
        Assert.Equal("Combat Rank 1 of 13, Mostly Harmless, 40% into it", new RankStanding("Combat", 1) { Percent = 40 }.Say());

    [Fact]
    public void ThePercentIsLeftOutAtTheTopOfTheLadder() =>
        Assert.Equal("CQC Rank 8 of 8, Elite", new RankStanding("CQC", 8) { Percent = 40 }.Say());

    [Fact]
    public void TheNextRankIsSaidInTheSameForm()
    {
        Assert.Equal("Combat Rank 2 of 13, Novice", new RankStanding("Combat", 1).SayNext());
        Assert.Null(new RankStanding("CQC", 8).SayNext());
        Assert.Null(new RankStanding("Empire", 14).SayNext());
    }
}
