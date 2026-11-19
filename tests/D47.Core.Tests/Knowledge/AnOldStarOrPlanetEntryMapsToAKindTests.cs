using D47.Core.Knowledge;
using Xunit;

namespace D47.Core.Tests.Knowledge;

/// <summary>Each star and planet EntryID of the old codex table names one node of the shipped kind tree (#2603).</summary>
public class AnOldStarOrPlanetEntryMapsToAKindTests
{
    private static readonly long[] Stock =
    [
        1100301, 1100501, 1100502, 1100601, 1100701, 1100801, 1100901, 1200701,
        1300301, 1300302, 1300401, 1300501, 1300601, 1301501, 1301601, 1301801,
    ];

    [Fact]
    public void EveryOldEntryMapsToADistinctKindInTheShippedTable()
    {
        Assert.Equal(56, CodexEntryKinds.Map.Count);
        Assert.Equal(56, CodexEntryKinds.Map.Values.Distinct(StringComparer.Ordinal).Count());
        Assert.All(CodexEntryKinds.Map.Values, key => Assert.NotNull(CodexCatalogue.Table.Entry(key)?.Kind));
    }

    [Fact]
    public void EveryStockPictureEntryIsMapped() =>
        Assert.All(Stock, entryId => Assert.NotNull(CodexEntryKinds.Of(entryId)));

    [Theory]
    [InlineData(1100601, "star.k_yellow_orange_star.dwarf")]
    [InlineData(1100602, "star.k_yellow_orange_giant_star.giant")]
    [InlineData(1102300, "star.neutron_star")]
    [InlineData(1301501, "planet.high_metal_content_world.not_terraformable.atmosphere")]
    [InlineData(1300301, "planet.water_world.not_terraformable")]
    [InlineData(1300100, "planet.earth_like_world")]
    [InlineData(1200501, "gas.class_i_gas_giant")]
    public void AStarMapsToItsKindAndAPlanetToTheGroupItCovered(long entryId, string kind) =>
        Assert.Equal(kind, CodexEntryKinds.Of(entryId));

    [Fact]
    public void AnyOtherEntryMapsToNothing() =>
        Assert.Null(CodexEntryKinds.Of(2420101));
}
