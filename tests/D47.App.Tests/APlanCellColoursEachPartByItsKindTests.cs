using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using D47.App.Panel;
using D47.App.Theming;
using Xunit;

namespace D47.App.Tests;

/// <summary>The Plan column draws only what is still to change in a slot, each part in the ink of its kind (#2404).</summary>
public sealed class APlanCellColoursEachPartByItsKindTests
{
    private static LoadoutSide Side(string? module, string? blueprint = null, int? grade = null, string? experimental = null) =>
        new(module, null, blueprint, grade, experimental, []);

    private static TextBlock PlanCell(LoadoutParts parts, bool showing = false)
    {
        var row = new LoadoutRow("k", "Slot", "Slot", null, true) { Parts = parts };
        var layout = LoadoutPages.SlotLayout();
        var button = Assert.IsType<Button>(LoadoutPages.SlotRow(row, () => { }, layout, showing));

        // Slot, current, mark, plan, draw.
        return Assert.IsType<TextBlock>(Assert.IsAssignableFrom<Avalonia.Controls.Panel>(button.Content).Children[3]);
    }

    private static IReadOnlyList<(string Text, Color? Ink)> Runs(TextBlock cell) =>
        cell.Inlines is { Count: > 0 } inlines
            ? [.. inlines.OfType<Run>().Select(run => (run.Text ?? string.Empty, (run.Foreground as ISolidColorBrush)?.Color))]
            : [(cell.Text ?? string.Empty, (cell.Foreground as ISolidColorBrush)?.Color)];

    private static Color Ink(string key) => ((ISolidColorBrush)Application.Current!.Resources[key]!).Color;

    [AvaloniaFact]
    public void AModuleThatIsAlreadyFittedIsNotDrawnAndTheRestIsColoured()
    {
        using var look = AppLook.Put();
        var parts = new LoadoutParts(2, "2", Side("2A Beam Laser"), Side("2A Beam Laser", "Efficient Weapon", 3, "Thermal Vent"), "empty")
        {
            BlueprintApplied = false,
            ExperimentalApplied = false,
        };

        var runs = Runs(PlanCell(parts));

        Assert.Equal("Efficient Weapon G3 · Thermal Vent", string.Concat(runs.Select(run => run.Text)));
        Assert.Equal(Ink(ThemeManager.AKey), runs.First(run => run.Text == "Efficient Weapon G3").Ink);
        Assert.Equal(Ink(ThemeManager.CyanKey), runs.First(run => run.Text == "Thermal Vent").Ink);
    }

    [AvaloniaFact]
    public void ARollAlreadyOnTheModuleIsNotDrawn()
    {
        using var look = AppLook.Put();
        var parts = new LoadoutParts(2, "2", Side("2A Multi-cannon", "Overcharged Weapon", 5), Side("2A Multi-cannon", "Overcharged Weapon", 5, "Corrosive Shell"), "empty")
        {
            BlueprintApplied = true,
            ExperimentalApplied = false,
        };

        var runs = Runs(PlanCell(parts));

        var only = Assert.Single(runs);
        Assert.Equal("Corrosive Shell", only.Text);
        Assert.Equal(Ink(ThemeManager.CyanKey), only.Ink);
    }

    [AvaloniaFact]
    public void ADifferentModuleIsWhiteAndBringsItsRollWithIt()
    {
        using var look = AppLook.Put();
        var parts = new LoadoutParts(2, "2", Side("2A Burst Laser, turreted"), Side("2A Burst Laser, gimballed", "Efficient Weapon", 3), "empty");

        var runs = Runs(PlanCell(parts));

        Assert.Equal("2A Burst Laser, gimballed · Efficient Weapon G3", string.Concat(runs.Select(run => run.Text)));
        Assert.Equal(Ink(ThemeManager.WhiteKey), runs[0].Ink);
        Assert.Equal(Ink(ThemeManager.AKey), runs.First(run => run.Text == "Efficient Weapon G3").Ink);
    }

    [AvaloniaFact]
    public void ABlueprintBelowThePlannedGradeIsDrawn()
    {
        using var look = AppLook.Put();
        var parts = new LoadoutParts(2, "2", Side("2A Beam Laser", "Efficient Weapon", 2), Side("2A Beam Laser", "Efficient Weapon", 5), "empty")
        {
            BlueprintApplied = false,
        };

        var only = Assert.Single(Runs(PlanCell(parts)));

        Assert.Equal("Efficient Weapon G5", only.Text);
        Assert.Equal(Ink(ThemeManager.AKey), only.Ink);
    }

    [AvaloniaFact]
    public void TheRowThatIsShowingKeepsItsOwnInk()
    {
        using var look = AppLook.Put();
        var parts = new LoadoutParts(2, "2", Side("2A Beam Laser"), Side("2A Beam Laser", "Efficient Weapon", 3, "Thermal Vent"), "empty")
        {
            BlueprintApplied = false,
            ExperimentalApplied = false,
        };

        var runs = Runs(PlanCell(parts, showing: true));

        Assert.Equal("Efficient Weapon G3 · Thermal Vent", string.Concat(runs.Select(run => run.Text)));
        Assert.DoesNotContain(runs, run => run.Ink == Ink(ThemeManager.CyanKey));
    }
}
