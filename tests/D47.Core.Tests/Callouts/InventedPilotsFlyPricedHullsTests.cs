using D47.Core.Callouts;
using D47.Core.Knowledge;
using Xunit;

namespace D47.Core.Tests.Callouts;

public class InventedPilotsFlyPricedHullsTests
{
    private static readonly string[] Names = ["Sidewinder", "Imperial Cutter"];

    private static IEnumerable<ShipSpecification> Picks(string? stationType, int speaker = 0) =>
        Enumerable.Range(0, 10_000).Select(i => NpcChatter.InventedPilotHull(i, speaker, stationType));

    private static double Share(IEnumerable<ShipSpecification> picks, string name) =>
        picks.Count(ship => ship.Name == name) / 10_000.0;

    [Fact]
    public void TheSameExchangeGivesTheSameHull()
    {
        Assert.Equal(
            NpcChatter.InventedPilotHull(123, 1, null).Symbol,
            NpcChatter.InventedPilotHull(123, 1, null).Symbol);
    }

    [Fact]
    public void EveryPricedHullIsPickedAndTheSharesFollowThePrice()
    {
        var picks = Picks(null).ToList();
        var priced = EliteSpecifications.Ships.Where(ship => ship.Cost is > 0).Select(ship => ship.Symbol);

        Assert.Empty(priced.Except(picks.Select(ship => ship.Symbol)));
        Assert.InRange(Share(picks, Names[0]), 0.13, 0.17);
        Assert.InRange(Share(picks, Names[1]), 0.002, 0.007);
    }

    [Fact]
    public void NoLargeHullIsPickedAtAnOutpost()
    {
        Assert.DoesNotContain(Picks("Outpost"), ship => string.Equals(ship.Pad, "large", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void AHullWithNoPriceIsNeverPicked()
    {
        Assert.DoesNotContain(Picks(null).Concat(Picks("Outpost", 1)), ship => ship.Cost is not > 0);
    }

    [Fact]
    public void ControllerAndPassersbyNameTheInventedPilotsHulls()
    {
        var controller = NpcChatter.Instruction(NpcChatterKind.Controller, docked: true, exchangeIndex: 4);
        var passersby = NpcChatter.Instruction(NpcChatterKind.Passersby, docked: true, exchangeIndex: 4);
        var first = NpcChatter.InventedPilotHull(4, 0, null).Name;

        Assert.Contains("The pilot the controller is talking to flies", controller);
        Assert.Contains(first, controller);
        Assert.Contains("The first of them flies", passersby);
        Assert.Contains("the second flies", passersby);
    }

    [Fact]
    public void HailAndCombatInstructionsNameNoInventedHull()
    {
        Assert.DoesNotContain("flies", NpcChatter.Instruction(NpcChatterKind.Hail, exchangeIndex: 4));
        Assert.DoesNotContain("The first of them flies", NpcChatter.Instruction(NpcChatterKind.Combat, exchangeIndex: 4));
    }
}
