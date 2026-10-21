using Xunit;
using D47.Core.Journal;
using D47.Core.Knowledge;

namespace D47.Core.Tests;

public sealed class TheRegionGridCanBeDrawnTests
{
    private static StarPosition Centre(RegionRun run) =>
        new((run.X0 + run.X1) / 2, 0, (run.Z0 + run.Z1) / 2);

    [Fact]
    public void FindAtTheCentreOfARunReturnsThatRunsRegion()
    {
        var runs = GalacticRegions.Runs();

        for (var i = 0; i < runs.Count; i += 97)
        {
            Assert.Equal(GalacticRegions.NameOf(runs[i].RegionId), GalacticRegions.Find(Centre(runs[i])));
        }
    }

    [Fact]
    public void NoRunIsOutsideEveryRegion() =>
        Assert.DoesNotContain(GalacticRegions.Runs(), run => run.RegionId == 0);

    [Fact]
    public void EveryRegionHasALabelInsideItself()
    {
        var ids = GalacticRegions.Runs().Select(run => run.RegionId).Distinct().ToList();

        Assert.Equal(42, ids.Count);

        foreach (var id in ids)
        {
            var label = GalacticRegions.LabelAt(id);

            Assert.NotNull(label);
            Assert.Equal(GalacticRegions.NameOf(id), GalacticRegions.Find(label.Value));
        }
    }

    [Fact]
    public void AnUnknownRegionHasNoLabel() => Assert.Null(GalacticRegions.LabelAt(-1));

    [Fact]
    public void TheExtentHoldsEveryRun()
    {
        var (x0, z0, x1, z1) = GalacticRegions.Extent;

        Assert.All(GalacticRegions.Runs(), run =>
        {
            Assert.True(run.X0 >= x0 && run.X1 <= x1 + 1e-6);
            Assert.True(run.Z0 >= z0 && run.Z1 <= z1 + 1e-6);
        });
    }
}
