using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using D47.App.Panel;
using D47.Core.Interface;
using D47.Core.Knowledge;
using Xunit;

namespace D47.App.Tests;

/// <summary>While a stored plan is drawn the Galaxy map names it and offers Plotted route, in both layouts (#1553).</summary>
public class TheGalaxyMapNamesThePlanDrawnTests
{
    private static (PanelView Panel, GalaxyMapPage Page) Opened(bool headset)
    {
        var game = new ThePlanPageOffersTheGalaxyMapTests.Game();
        game.Plans.Record(ThePlanPageOffersTheGalaxyMapTests.RichesRoute(), "Road to Riches", ThePlanPageOffersTheGalaxyMapTests.Now.AddHours(-1));

        var panel = ThePlanPageOffersTheGalaxyMapTests.Panel(game, headset, resultOpen: false);
        ThePlanPageOffersTheGalaxyMapTests.Show(panel, headset);

        return (panel, panel.GetVisualDescendants().OfType<GalaxyMapPage>().Single());
    }

    [AvaloniaTheory]
    [InlineData(false)]
    [InlineData(true)]
    public void ThePlotItsNameAndTheWayBackAreAbsentWhileThePlottedRouteIsDrawn(bool headset)
    {
        var (_, page) = Opened(headset);

        Assert.Null(page.PlanShown());
        Assert.Equal(string.Empty, page.PlanName());
        Assert.False(page.PlanBand()!.IsVisible);
        Assert.Contains(page.PlanBand(), page.PlottedRoute().GetVisualAncestors());
    }

    [AvaloniaTheory]
    [InlineData(false)]
    [InlineData(true)]
    public void AStoredPlanIsNamedWithAPlottedRoutePress(bool headset)
    {
        var (panel, page) = Opened(headset);

        panel.ShowOnGalaxyMap(RoutePlanKind.Riches);

        Assert.Equal(RoutePlanKind.Riches, page.PlanShown());
        Assert.Equal("ROAD TO RICHES PLAN: Road to Riches", page.PlanName());
        Assert.True(page.PlanBand()!.IsVisible);
        Assert.True(page.PlottedRoute().IsVisible);
        Assert.Equal(44, page.PlanBand()!.Height);
    }

    [AvaloniaTheory]
    [InlineData(false)]
    [InlineData(true)]
    public void PressingPlottedRouteDrawsThePlottedRouteAgain(bool headset)
    {
        var (panel, page) = Opened(headset);

        panel.ShowOnGalaxyMap(RoutePlanKind.Riches);
        ThePlanPageOffersTheGalaxyMapTests.Press(page.PlottedRoute());

        Assert.Null(page.PlanShown());
        Assert.Equal(string.Empty, page.PlanName());
        Assert.False(page.PlanBand()!.IsVisible);
    }

    [AvaloniaFact]
    public void TheHeadsetMapKeepsItsHeightWhileAPlanIsNamed()
    {
        var (panel, page) = Opened(headset: true);

        panel.ShowOnGalaxyMap(RoutePlanKind.Riches);

        Assert.Equal(GalaxyMapPage.PanelMapHeight, page.Map().Bounds.Height);
    }

    [AvaloniaTheory]
    [InlineData(false)]
    [InlineData(true)]
    public void RouteFramingKeepsThePlansStopsBelowTheBand(bool headset)
    {
        var (panel, page) = Opened(headset);

        panel.ShowOnGalaxyMap(RoutePlanKind.Riches);

        var stops = ThePlanPageOffersTheGalaxyMapTests.RichesRoute().Stops;

        foreach (var stop in Enumerable.Range(0, 10))
        {
            var at = page.Map().Projected(stops[stop].Position!.Value);

            Assert.True(at.Y >= 44, $"Stop {stop} is drawn {at.Y} px from the top, under the band");
            Assert.True(at.Y <= page.Map().Bounds.Height, $"Stop {stop} is drawn {at.Y} px from the top, below the map");
        }
    }
}
