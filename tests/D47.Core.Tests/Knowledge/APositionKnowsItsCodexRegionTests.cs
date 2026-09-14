using D47.Core.Journal;
using D47.Core.Knowledge;
using Xunit;

namespace D47.Core.Tests.Knowledge;

/// <summary>A position knows which of the 42 codex regions holds it, or that none does.</summary>
public class APositionKnowsItsCodexRegionTests
{
    [Fact]
    public void TheOriginIsInsideTheInnerOrionSpur() =>
        Assert.Equal("Inner Orion Spur", GalacticRegions.Find(StarPosition.Origin));

    [Fact]
    public void ASpanshCoordinateOutInScutumCentaurusMatchesItsReportedRegion() =>
        Assert.Equal(
            "Inner Scutum-Centaurus Arm",
            GalacticRegions.Find(new StarPosition(-7056, -569.4375, 15100.75)));

    [Fact]
    public void APositionFarOutsideTheMapHasNoRegion() =>
        Assert.Null(GalacticRegions.Find(new StarPosition(-200_000, 0, -200_000)));
}
