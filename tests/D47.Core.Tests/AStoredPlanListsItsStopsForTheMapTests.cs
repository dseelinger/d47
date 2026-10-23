using D47.Core.Journal;
using D47.Core.Knowledge;
using Xunit;

namespace D47.Core.Tests;

public class AStoredPlanListsItsStopsForTheMapTests
{
    private static readonly StarPosition At = new(1, 2, 3);

    private static RouteEndpoint Endpoint(string name) => new(name, At);

    private static PlottedRoute JumpRoute() =>
        new("Sol", "Colonia", 100, 2, [new RouteWaypoint("A", 1, null, true) { Position = At }]) { Start = Endpoint("Sol") };

    private static RichesRoute RichesRoute() =>
        new([new RichesStop("A", 1, []) { Position = At }]) { Start = Endpoint("Home"), End = Endpoint("Home") };

    private static ExobiologyRoute ExobiologyRoute() =>
        new([new ExobiologyStop("A", 1, []) { Position = At }]) { Start = Endpoint("Home"), End = Endpoint("Home") };

    private static CarrierRoute CarrierRoute() =>
        new([new CarrierWaypoint("A", 0, 0, 0, 0, true, 0, 0, false, false, false) { Position = At }]);

    [Fact]
    public void AJumpPlanGivesWhatItsOwnRouteGives()
    {
        var route = JumpRoute();

        Assert.Equal(RouteMapStop.From(route), RouteMapStop.From(new StoredRoutePlan { Kind = RoutePlanKind.Jump, Jump = route }));
    }

    [Fact]
    public void ARichesPlanGivesWhatItsOwnRouteGives()
    {
        var route = RichesRoute();

        Assert.Equal(RouteMapStop.From(route), RouteMapStop.From(new StoredRoutePlan { Kind = RoutePlanKind.Riches, Riches = route }));
    }

    [Fact]
    public void AnExobiologyPlanGivesWhatItsOwnRouteGives()
    {
        var route = ExobiologyRoute();

        Assert.Equal(RouteMapStop.From(route), RouteMapStop.From(new StoredRoutePlan { Kind = RoutePlanKind.Exobiology, Exobiology = route }));
    }

    [Fact]
    public void ACarrierPlanGivesWhatItsOwnRouteGives()
    {
        var route = CarrierRoute();

        Assert.Equal(RouteMapStop.From(route), RouteMapStop.From(new StoredRoutePlan { Kind = RoutePlanKind.Carrier, Carrier = route }));
    }

    [Fact]
    public void ATradePlanGivesNoStops()
    {
        Assert.Null(RouteMapStop.From(new StoredRoutePlan { Kind = RoutePlanKind.Trade, Trade = new TradeRoute([]) }));
    }

    [Fact]
    public void APlanWithNoRouteRecordedGivesNoStops()
    {
        Assert.Null(RouteMapStop.From(new StoredRoutePlan { Kind = RoutePlanKind.Jump }));
    }
}
