using D47.Core.Knowledge;
using D47.Core.Loadout;
using D47.Core.Ships;
using Xunit;

namespace D47.Core.Tests.Loadout;

/// <summary>A material's Needed for lines name the module and blueprint grade, never the ship (#472).</summary>
public class NeededForNamesTheModuleNotTheShipTests
{
    private static ShipBuild Ship(string id, string hull, string name) =>
        new("F1", id, hull, 12, name, [new SlotPlan("MainEngines", "Dirty Drive Tuning", 5)]);

    [Fact]
    public void TwoShipsAskingForTheSameBlueprintAreOneLine()
    {
        var gap = PlanGap.Of([Ship("ship-1", "python", "Bad Idea"), Ship("ship-2", "anaconda", "Worse Idea")], [], null);
        var report = MaterialTracker.Of(null, gap);

        var rows = report.Ship.SelectMany(card => card.Rows).Where(row => row.Needed > 0).ToList();

        Assert.NotEmpty(rows);
        Assert.All(rows, row =>
        {
            var purpose = Assert.Single(row.NeededFor);

            Assert.Equal("Thrusters · Dirty Drive Tuning 5", purpose.What);
            Assert.Equal(row.Needed, purpose.Units);
        });
    }
}
