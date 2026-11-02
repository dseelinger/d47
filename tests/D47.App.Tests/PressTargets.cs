using Avalonia.Controls;
using Avalonia.VisualTree;
using Xunit;

namespace D47.App.Tests;

public static class PressTargets
{
    public static void AssertAtLeastFortyFourTall(Control page)
    {
        var pressable = page.GetVisualDescendants()
            .OfType<Control>()
            .Where(control => control.IsEffectivelyVisible && control is Button or CheckBox or TextBox)
            .Where(control => !control.GetVisualAncestors().OfType<Avalonia.Controls.Primitives.ScrollBar>().Any())
            .ToList();

        Assert.NotEmpty(pressable);
        Assert.All(pressable, control => Assert.True(
            control.Bounds.Height >= 44 - 0.5,
            $"{control.GetType().Name} '{control.Name ?? (control as ContentControl)?.Content?.ToString()}' is {control.Bounds.Height} tall"));
    }
}
