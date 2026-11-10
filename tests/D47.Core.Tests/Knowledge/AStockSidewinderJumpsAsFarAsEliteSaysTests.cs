using D47.Core.Knowledge;
using Xunit;

namespace D47.Core.Tests.Knowledge;

/// <summary>A hull's base jump range is its stock fitting's, as Elite's <c>Loadout</c> reports it (#2295).</summary>
public class AStockSidewinderJumpsAsFarAsEliteSaysTests
{
    /// <summary>
    /// An unmodified stock Sidewinder writes <c>UnladenMass</c> 42.9 and <c>MaxJumpRange</c> 8.15 in its
    /// <c>Loadout</c> event.
    /// </summary>
    [Fact]
    public void TheStockSidewinderJumpsEightPointOneFiveLightYears()
    {
        var sidewinder = EliteSpecifications.Ship("sidewinder")!;

        var unladen = sidewinder.HullMass!.Value
                      + sidewinder.Stock.Sum(symbol => EliteSpecifications.Module(symbol)!.Mass ?? 0);

        Assert.Equal(42.9, unladen, 0.001);
        Assert.Equal(8.15, sidewinder.BaseJumpRange!.Value, 0.01);
    }

    [Fact]
    public void EveryStockedHullJumpsBetweenFourAndOneHundredLightYears()
    {
        var stocked = EliteSpecifications.Ships.Where(ship => ship.Stock.Count > 0).ToList();

        Assert.NotEmpty(stocked);

        Assert.All(stocked, ship => Assert.InRange(ship.BaseJumpRange ?? 0, 4, 100));
    }
}
