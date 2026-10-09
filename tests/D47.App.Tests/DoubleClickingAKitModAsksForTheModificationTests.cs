using Avalonia.Headless.XUnit;
using D47.Core.Loadout;
using Xunit;

namespace D47.App.Tests;

/// <summary>A double-click on a suit's mod row opens the modification prompt for that slot (#682).</summary>
[Trait("Category", "Integration")]
public class DoubleClickingAKitModAsksForTheModificationTests
{
    [AvaloniaFact]
    public void DoubleClickingAKitModAsksForTheModification()
    {
        var bench = SlotBench.Open();

        bench.OpenSuit();
        bench.OpenASlot("MOD 2");
        bench.DoubleClick(bench.Centre(bench.Row("MOD 1")));

        Assert.True(bench.Asking("loadout.kitmod"));
        Assert.Contains(
            bench.Panel.Nav.Trail,
            crumb => crumb.Key.EndsWith("|" + OnFootBuild.ModSlot(1), StringComparison.Ordinal));

        bench.Window.Close();
    }
}
