using Avalonia.Headless.XUnit;
using Xunit;

namespace D47.App.Tests;

/// <summary>A double-click on a ship's slot row opens that slot's plan prompt (#682).</summary>
public class DoubleClickingAShipSlotAsksForItsPlanTests
{
    [AvaloniaFact]
    public void DoubleClickingAShipSlotAsksForItsPlan()
    {
        var bench = SlotBench.Open();

        bench.OpenShip();
        bench.OpenASlot("Thrusters");
        bench.DoubleClick(bench.Centre(bench.Row("Large Hardpoint 1")));

        Assert.True(bench.AskingForAModule());
        Assert.Contains(bench.Panel.Nav.Trail, crumb => crumb.Key.EndsWith("|LargeHardpoint1", StringComparison.Ordinal));

        bench.Window.Close();
    }
}
