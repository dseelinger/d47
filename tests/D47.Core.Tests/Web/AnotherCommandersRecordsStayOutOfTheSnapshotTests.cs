using D47.Core.Goals;
using Xunit;

namespace D47.Core.Tests.Web;

/// <summary>A checklist, goal or route plan stored under another Frontier ID is not in the snapshot.</summary>
public sealed class AnotherCommandersRecordsStayOutOfTheSnapshotTests
{
    [Fact]
    public void AnotherCommandersChecklistIsLeftOut()
    {
        var bench = new SnapshotBench();

        bench.Note(SnapshotBench.OtherFid, "Brin Hollow", "Sell the cargo", "Fit a shield booster");

        Assert.Null(bench.Build().Checklist?.Lists);

        bench.Note(SnapshotBench.Fid, "Ada Quillon", "Buy limpets", "Fit a fuel scoop");

        var items = bench.Build().Checklist!.Lists!.SelectMany(list => list.Items!).Select(item => item.Text).ToList();

        Assert.Equal(["Buy limpets", "Fit a fuel scoop"], items);
    }

    [Fact]
    public void AnotherCommandersSetAsideDoesNotHideAGoal()
    {
        var bench = new SnapshotBench();
        var arc = GoalCatalogue.All(bench.State)[0];

        bench.GoalStore.SetAside(SnapshotBench.OtherFid, arc.Key, aside: true);

        Assert.Contains(bench.Build().Checklist!.Goals!, goal => goal.Name == arc.Name);

        bench.GoalStore.SetAside(SnapshotBench.Fid, arc.Key, aside: true);

        Assert.DoesNotContain(bench.Build().Checklist!.Goals!, goal => goal.Name == arc.Name);
    }

    [Fact]
    public void AnotherCommandersRoutePlanIsLeftOut()
    {
        var bench = new SnapshotBench();

        bench.Plot(SnapshotBench.OtherFid, "Sagittarius A*");

        Assert.Null(bench.Build().RoutePlans);

        bench.Plot(SnapshotBench.Fid, "Colonia");

        Assert.Equal("Colonia", Assert.Single(bench.Build().RoutePlans!).Jump!.Destination);
    }
}
