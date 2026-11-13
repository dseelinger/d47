using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using D47.App.Panel;
using D47.Core.Journal;
using D47.Core.Knowledge;
using Xunit;

namespace D47.App.Tests;

/// <summary>The Galaxy map's legend has an entry for each kind of mark drawn inside the view, and the headset band wraps to fit them.</summary>
public class TheGalaxyMapLegendNamesTheMarksInViewTests
{
    private static StarPosition EngineerAt => TheGalaxyMapDrawsWhereTheCommanderHasBeenTests.BubbleEngineer().Position!.Value;

    private static GalaxyViewport ShowingEngineerAndBookmark => GalaxyViewport.Around(EngineerAt) with { LightYearsAcross = 6_000 };

    private static IReadOnlyList<string> Marks(GalaxyMapPage page) =>
        [.. page.LegendEntries().Where(entry => entry is "ENGINEER" or "BOOKMARK" or "YOUR CARRIER" or "STORED SHIP")];

    [AvaloniaFact]
    public void TheLegendNamesAnEngineerAndABookmarkInViewAndNeitherOnceTheyArePannedOut()
    {
        var (_, _, page) = TheGalaxyMapDrawsWhereTheCommanderHasBeenTests.InWindow(TheGalaxyMapDrawsWhereTheCommanderHasBeenTests.Keeping());

        page.Map().View(ShowingEngineerAndBookmark);
        Dispatcher.UIThread.RunJobs();

        Assert.Contains("ENGINEER", Marks(page));
        Assert.Contains("BOOKMARK", Marks(page));

        page.Map().View(GalaxyViewport.Around(new StarPosition(5_000, 0, 5_000)) with { LightYearsAcross = 400 });
        Dispatcher.UIThread.RunJobs();

        Assert.Empty(Marks(page));
    }

    [Fact]
    public void TheEntriesRunInMarkKindOrderAfterTheRoutesStars()
    {
        var at = new StarPosition(10, 0, 20);
        var scene = GalaxyMapScene.Empty with
        {
            Marks =
            [
                new GalaxyMapMark(GalaxyMapMarkKind.Ships, "Yard", at, ["Python"]),
                new GalaxyMapMark(GalaxyMapMarkKind.Carrier, "Dock", at, ["BNH-T2F"]),
                new GalaxyMapMark(GalaxyMapMarkKind.Engineer, "Shop", at, ["Someone"]),
                new GalaxyMapMark(GalaxyMapMarkKind.Bookmark, "Mark", at, ["Here"]),
            ],
        };

        var drawn = GalaxyMapLayout.Of(scene, GalaxyViewport.Around(at) with { LightYearsAcross = 400 }, new Size(600, 600)).Drawn;

        Assert.Equal(
            [GalaxyMapMarkKind.Engineer, GalaxyMapMarkKind.Bookmark, GalaxyMapMarkKind.Carrier, GalaxyMapMarkKind.Ships],
            drawn.MarkKinds);
    }

    [Fact]
    public void AMarkOffTheEdgeOfTheViewIsNotDrawnAndSoIsNotNamed()
    {
        var at = new StarPosition(0, 0, 0);
        var scene = GalaxyMapScene.Empty with { Marks = [new GalaxyMapMark(GalaxyMapMarkKind.Bookmark, "Far", at, ["Here"])] };

        var drawn = GalaxyMapLayout.Of(scene, GalaxyViewport.Around(new StarPosition(900, 0, 900)) with { LightYearsAcross = 400 }, new Size(600, 600)).Drawn;

        Assert.Empty(drawn.MarkKinds);
    }

    [AvaloniaFact]
    public void OnTheHeadsetTheBandWrapsToTwoLinesAndTheScaleBarSitsAboveIt()
    {
        var (surface, _, page) = TheGalaxyMapDrawsWhereTheCommanderHasBeenTests.OnThePanel(TheGalaxyMapDrawsWhereTheCommanderHasBeenTests.Keeping());
        var band = page.GetVisualDescendants().OfType<Border>().First(border => border.Child is WrapPanel);

        page.Map().View(GalaxyViewport.Around(new StarPosition(5_000, 0, 5_000)) with { LightYearsAcross = 400 });
        surface.Render();

        var one = band.Bounds.Height;
        Assert.Equal(one, page.Map().BottomInset);

        page.Map().View(ShowingEngineerAndBookmark);
        surface.Render();

        Assert.Equal(page.Map().Bounds.Width, band.Bounds.Width);
        Assert.True(band.Bounds.Height > one, $"{band.Bounds.Height} px with {page.LegendEntries().Count} entries over {band.Bounds.Width} px");
        Assert.Equal(band.Bounds.Height, page.Map().BottomInset);
    }
}
