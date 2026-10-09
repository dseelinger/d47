using Avalonia.Headless.XUnit;
using Xunit;

namespace D47.App.Tests;

/// <summary>A second press that did not follow a press on the same row asks for no plan (#682).</summary>
[Trait("Category", "Integration")]
public class ADoubleClickThatStartedElsewherePlansNothingTests
{
    [AvaloniaFact]
    public void ADoubleClickThatStartedElsewherePlansNothing()
    {
        var bench = SlotBench.Open();

        bench.OpenShip();
        bench.OpenASlot("Thrusters");

        // The first press of the pair was on one row; the second, counted as a double, is on another.
        bench.Click(bench.Centre(bench.Row("Thrusters")));
        bench.PressAs(bench.Row("Large Hardpoint 1"), clickCount: 2);

        Assert.False(bench.AskingForAModule());

        bench.Window.Close();
    }

    [AvaloniaFact]
    public void ADoubleClickWithControlHeldPlansNothing()
    {
        var bench = SlotBench.Open();

        bench.OpenShip();
        bench.OpenASlot("Thrusters");

        var row = bench.Row("Large Hardpoint 1");

        bench.PressAs(row, clickCount: 1);
        bench.PressAs(row, clickCount: 2, Avalonia.Input.KeyModifiers.Control);

        Assert.False(bench.AskingForAModule());

        bench.Window.Close();
    }
}
