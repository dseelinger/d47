using D47.Core.Knowledge;
using D47.Core.Mining;
using Xunit;

namespace D47.Core.Tests.Knowledge;

/// <summary>Ring hotspot materials are the 13 Elite has; the other ring-mined minerals are mining targets only.</summary>
public class OnlyRealHotspotMaterialsAreHotspotsTests
{
    private static readonly string[] Hotspots =
    [
        "Alexandrite", "Benitoite", "Bromellite", "Grandidierite", "Low Temperature Diamonds",
        "Monazite", "Musgravite", "Painite", "Platinum", "Rhodplumsite", "Serendibite", "Tritium",
        "Void Opal",
    ];

    [Fact]
    public void TheHotspotMaterialsAreExactlyThe13()
    {
        Assert.Equal(Hotspots, BodyCatalogue.RingSignals.Order(StringComparer.Ordinal));
    }

    [Fact]
    public void ARingMinedMaterialIsNotAcceptedAsAHotspotAndNothingIsSearched()
    {
        var parsed = BodyQuery.TryParse(new BodyRequest { RingSignal = "Lithium Hydroxide" }, out _, out var failure);

        Assert.False(parsed);
        Assert.NotNull(failure);
    }

    [Theory]
    [InlineData("Lithium Hydroxide")]
    [InlineData("Liquid oxygen")]
    [InlineData("Painite")]
    [InlineData("Gold")]
    public void EveryRingMinedMaterialStaysAMiningTarget(string material)
    {
        Assert.Equal(material, MiningTarget.Match(material));
    }

    [Fact]
    public void MiningTargetsAreTheHotspotsTheRingMinedAndTheFiveRockMaterials()
    {
        Assert.Equal(30 + 5, MiningTarget.Materials.Count);
        Assert.Empty(BodyCatalogue.RingSignals.Intersect(BodyCatalogue.RingMinedMaterials));
    }
}
