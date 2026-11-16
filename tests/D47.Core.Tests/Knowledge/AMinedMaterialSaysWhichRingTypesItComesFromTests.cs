using D47.Core.Knowledge;
using D47.Core.Mining;
using Xunit;

namespace D47.Core.Tests.Knowledge;

/// <summary>The shipped ring table, generated from journals by <c>tools/gen-ring-materials.py</c> (#2453).</summary>
public class AMinedMaterialSaysWhichRingTypesItComesFromTests
{
    public static TheoryData<string> MinedMaterials => [.. MiningTarget.Materials];

    [Theory]
    [MemberData(nameof(MinedMaterials))]
    public void EveryMinedMaterialHasARingType(string material) =>
        Assert.NotEmpty(RingMaterials.For(material));

    [Fact]
    public void VoidOpalIsCoredFromIcyRings()
    {
        var rows = RingMaterials.For("Void Opal");

        var row = Assert.Single(rows);
        Assert.Equal(("opal", "Void Opal", "Icy", 1, MiningMethod.Core), (row.Symbol, row.Name, row.RingType, row.Rank, row.Method));
    }

    [Fact]
    public void GoldIsLaseredFromMetalRichAndMetallicRings()
    {
        var rows = RingMaterials.For("Gold");

        Assert.Equal(["Metal Rich", "Metallic"], rows.Select(r => r.RingType).Order(StringComparer.Ordinal));
        Assert.All(rows, r => Assert.Equal(MiningMethod.Laser, r.Method));
        Assert.Equal(1, rows[0].Rank);
    }

    [Theory]
    [InlineData("Tritium")]
    [InlineData("Low Temperature Diamonds")]
    [InlineData("Void Opal")]
    [InlineData("Lithium Hydroxide")]
    public void AnIceMaterialComesFromIcyRingsOnly(string material) =>
        Assert.Equal(["Icy"], RingMaterials.For(material).Select(r => r.RingType));

    [Fact]
    public void HaematiteComesFromRockyRingsOnly() =>
        Assert.Equal(["Rocky"], RingMaterials.For("Haematite").Select(r => r.RingType));

    [Theory]
    [InlineData("Silver")]
    [InlineData("Osmium")]
    public void APreciousMetalComesFromMetalRichAndMetallicRings(string material) =>
        Assert.Equal(["Metal Rich", "Metallic"], RingMaterials.For(material).Select(r => r.RingType).Order(StringComparer.Ordinal));

    [Fact]
    public void RowsAreInRankOrder()
    {
        foreach (var material in MiningTarget.Materials)
        {
            var ranks = RingMaterials.For(material).Select(r => r.Rank).ToList();

            Assert.Equal(ranks.Order(), ranks);
        }
    }

    [Theory]
    [InlineData("Bananas")]
    [InlineData("")]
    public void AMaterialWithNoRowsHasAnEmptyList(string material) =>
        Assert.Empty(RingMaterials.For(material));
}
