using D47.Core.Knowledge;
using Xunit;

namespace D47.Core.Tests.Knowledge;

public class RingTypesArePhrasedInRankOrderTests
{
    private static RingMaterial Row(string ringType, int rank) =>
        new("x", "X", ringType, rank, MiningMethod.Laser);

    [Fact]
    public void NoRowsHaveNoPhrase() =>
        Assert.Null(RingMaterials.Phrase([]));

    [Fact]
    public void OneTypeIsNamedAlone() =>
        Assert.Equal("Icy rings", RingMaterials.Phrase([Row("Icy", 1)]));

    [Fact]
    public void TypesSharingARankAreJoinedWithOr() =>
        Assert.Equal("Metal Rich or Metallic rings", RingMaterials.Phrase([Row("Metal Rich", 1), Row("Metallic", 1)]));

    [Fact]
    public void RanksAreJoinedWithThen() =>
        Assert.Equal(
            "Metallic rings, then Metal Rich rings",
            RingMaterials.Phrase([Row("Metallic", 1), Row("Metal Rich", 2)]));

    [Fact]
    public void ASharedRankFollowedByAnotherRankReadsInOrder() =>
        Assert.Equal(
            "Metal Rich or Metallic rings, then Rocky rings",
            RingMaterials.Phrase([Row("Metal Rich", 1), Row("Metallic", 1), Row("Rocky", 2)]));

    [Fact]
    public void ThreeRanksAreJoinedWithThen() =>
        Assert.Equal(
            "Metallic rings, then Metal Rich rings, then Rocky rings",
            RingMaterials.Phrase([Row("Metallic", 1), Row("Metal Rich", 2), Row("Rocky", 3)]));
}
