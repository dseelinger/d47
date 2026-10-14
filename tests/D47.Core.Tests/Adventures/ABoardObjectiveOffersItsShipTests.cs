using D47.Core.Adventures;
using D47.Core.Journal;
using D47.Core.Ships;
using Xunit;

namespace D47.Core.Tests.Adventures;

public sealed class ABoardObjectiveOffersItsShipTests
{
    private static AdventureTrigger Board(string? hull = "cobramkiii") =>
        new() { Kind = TriggerKind.Board, ShipType = hull };

    private static FleetEntry Owned(string hull = "cobramkiii", int id = 1) =>
        new(null, new StoredShip(id, hull, null, "Sol"), false);

    private static FleetEntry Planned(string hull = "CobraMkIII", string id = "b1") =>
        new(new ShipBuild("F1", id, hull), null, false);

    [Fact]
    public void OneOwnedShipOfTheHullIsShown()
    {
        var owned = Owned();

        var button = ShipObjective.For(Board(), [owned, Owned("sidewinder", 2)]);

        Assert.NotNull(button);
        Assert.Equal(ShipObjectiveKind.ShowOwned, button.Kind);
        Assert.Equal("Show my Cobra MkIII", button.Label);
        Assert.Same(owned, button.Entry);
    }

    [Fact]
    public void OnePlannedUnboughtBuildOfTheHullIsShown()
    {
        var planned = Planned();

        var button = ShipObjective.For(Board(), [planned]);

        Assert.NotNull(button);
        Assert.Equal(ShipObjectiveKind.ShowPlan, button.Kind);
        Assert.Equal("Show the Cobra MkIII plan", button.Label);
        Assert.Same(planned, button.Entry);
    }

    [Fact]
    public void SeveralOwnedShipsOfTheHullShowTheIndex()
    {
        var button = ShipObjective.For(Board(), [Owned(id: 1), Owned(id: 2)]);

        Assert.NotNull(button);
        Assert.Equal(ShipObjectiveKind.ShowShips, button.Kind);
        Assert.Equal("Show my ships", button.Label);
        Assert.Null(button.Entry);
    }

    [Fact]
    public void SeveralPlannedBuildsOfTheHullShowTheIndex()
    {
        var button = ShipObjective.For(Board(), [Planned(id: "b1"), Planned(id: "b2")]);

        Assert.Equal(ShipObjectiveKind.ShowShips, button!.Kind);
    }

    [Fact]
    public void AnOwnedShipAndAPlanOfTheHullShowTheIndex()
    {
        var button = ShipObjective.For(Board(), [Owned(), Planned()]);

        Assert.Equal(ShipObjectiveKind.ShowShips, button!.Kind);
    }

    [Fact]
    public void NoShipOfTheHullOffersToPlanIt()
    {
        var button = ShipObjective.For(Board(), [Owned("sidewinder", 2), Planned("Python")]);

        Assert.NotNull(button);
        Assert.Equal(ShipObjectiveKind.Plan, button.Kind);
        Assert.Equal("Plan a Cobra MkIII", button.Label);
        Assert.Equal("Cobra MkIII", button.HullName);
        Assert.Null(button.Entry);
    }

    [Fact]
    public void AnObjectiveThatIsNotBoardingHasNoButton()
    {
        var arrive = new AdventureTrigger { Kind = TriggerKind.Arrive, ShipType = "cobramkiii" };

        Assert.Null(ShipObjective.For(arrive, [Owned()]));
    }

    [Theory]
    [InlineData("notashipatall")]
    [InlineData(null)]
    public void AHullWithNoNameHasNoButton(string? hull) =>
        Assert.Null(ShipObjective.For(Board(hull), []));
}
