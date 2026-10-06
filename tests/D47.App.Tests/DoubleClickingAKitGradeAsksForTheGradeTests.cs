using Avalonia.Headless.XUnit;
using Xunit;

namespace D47.App.Tests;

/// <summary>A double-click on a suit's Grade row opens the grade prompt (#682).</summary>
public class DoubleClickingAKitGradeAsksForTheGradeTests
{
    [AvaloniaFact]
    public void DoubleClickingAKitGradeAsksForTheGrade()
    {
        var bench = SlotBench.Open();

        bench.OpenSuit();
        bench.OpenASlot("MOD 2");
        bench.DoubleClick(bench.Centre(bench.Row("GRADE")));

        Assert.True(bench.Asking("loadout.kitgrade"));

        bench.Window.Close();
    }
}
