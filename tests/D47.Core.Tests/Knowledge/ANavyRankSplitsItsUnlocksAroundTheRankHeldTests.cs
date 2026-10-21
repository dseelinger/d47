using D47.Core.Knowledge;
using Xunit;

namespace D47.Core.Tests.Knowledge;

/// <summary>Navy rank unlocks on Standing (#1461).</summary>
public class ANavyRankSplitsItsUnlocksAroundTheRankHeldTests
{
    [Fact]
    public void ASerfHasNothingUnlockedAndMasterIsNext()
    {
        var unlocks = NavalRanks.UnlocksFor(Navy.Empire, 2);

        Assert.Empty(unlocks.Unlocked);
        Assert.Equal(3, unlocks.NextRank);
        Assert.Equal(["Imperial Courier"], unlocks.Next.Select(unlock => unlock.Name));
        Assert.Equal(
            ["Achenar permit", "Imperial Clipper", "Imperial Cutter"],
            unlocks.Later.Select(unlock => unlock.Name));
    }

    [Fact]
    public void ALieutenantHasFourUnlockedAndCorvetteNext()
    {
        var unlocks = NavalRanks.UnlocksFor(Navy.Federation, 8);

        Assert.Equal(
            ["Federal Dropship", "Sol permit", "Federal Assault Ship", "Federal Gunship"],
            unlocks.Unlocked.Select(unlock => unlock.Name));
        Assert.Equal(12, unlocks.NextRank);
        Assert.Equal(["Federal Corvette"], unlocks.Next.Select(unlock => unlock.Name));
        Assert.Empty(unlocks.Later);
    }

    [Fact]
    public void AKingHasEveryUnlockAndNothingNext()
    {
        var unlocks = NavalRanks.UnlocksFor(Navy.Empire, 14);

        Assert.Equal(4, unlocks.Unlocked.Count);
        Assert.Null(unlocks.NextRank);
        Assert.Empty(unlocks.Later);
    }

    [Fact]
    public void EveryRankGatedShipIsListedInItsNavy()
    {
        foreach (var ship in EliteSpecifications.Ships)
        {
            if (NavalRanks.Gate(ship.Requirement) is not { } gate)
            {
                continue;
            }

            var all = NavalRanks.UnlocksFor(gate.Navy, 0);
            var listed = all.Next.Concat(all.Later);

            Assert.Contains(listed, unlock => unlock.Name == ship.Name && unlock.Rank == gate.Rank);
        }

        Assert.Contains(EliteSpecifications.Ships, ship => NavalRanks.Gate(ship.Requirement) is not null);
    }
}
