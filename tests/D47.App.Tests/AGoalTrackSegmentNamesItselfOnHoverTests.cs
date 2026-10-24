using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.LogicalTree;
using D47.App.Controls;
using D47.Core.Goals;
using D47.Core.Knowledge;
using Xunit;

namespace D47.App.Tests;

public class AGoalTrackSegmentNamesItselfOnHoverTests
{
    [AvaloniaTheory]
    [InlineData(GoalTrackBar.RowHeight)]
    [InlineData(GoalTrackBar.OpenHeight)]
    public void EveryRankSegmentCarriesItsRankNameAsATooltip(double height)
    {
        var track = GoalTrack.Ladder("Combat", 3, 0.5, "Elite V");

        var tips = TipsOf(GoalTrackBar.Build(track, height));

        Assert.Equal(track.Segments.Select(segment => segment.Name), tips);
    }

    [AvaloniaFact]
    public void ThePowerplayTrackNamesItsSegmentsToo()
    {
        var track = GoalTrack.Powerplay(5000);

        var tips = TipsOf(GoalTrackBar.Build(track, GoalTrackBar.RowHeight, PowerplayBenefits.For("Li Yong-Rui", 8)!));

        Assert.Equal(track.Segments.Select(segment => segment.Name), tips);
    }

    [AvaloniaFact]
    public void TheCollapsedTrackHasAHitAreaTallerThanItsDrawnSegment()
    {
        var root = GoalTrackBar.Build(GoalTrack.Ladder("Combat", 3, 0.5, "Elite V"), GoalTrackBar.RowHeight);

        var hit = root.GetLogicalDescendants().OfType<Grid>().First(grid => ToolTip.GetTip(grid) is string);

        Assert.Equal(GoalTrackBar.RowHeight, ((Control)hit.Children[0]).Height);
        Assert.NotNull(hit.Background);
    }

    private static List<string> TipsOf(Control root) =>
        root.GetLogicalDescendants().OfType<Grid>()
            .Select(grid => ToolTip.GetTip(grid))
            .OfType<string>()
            .ToList();
}
