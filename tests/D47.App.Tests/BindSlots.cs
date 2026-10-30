using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Threading;
using Avalonia.VisualTree;
using D47.App.Settings;

namespace D47.App.Tests;

/// <summary>Finds a bind row's slots and clicks them the way a Commander does.</summary>
internal static class BindSlots
{
    /// <summary>The row's visible slots, key slot first.</summary>
    public static List<Border> Of(Visual row) =>
        row.GetVisualDescendants().OfType<Border>()
            .Where(chip => chip.Classes.Contains(SettingsView.BindingChipClass) && chip.IsEffectivelyVisible)
            .ToList();

    public static string? Says(Border slot) => (slot.Child as TextBlock)?.Text;

    public static void Click(TopLevel window, Control target)
    {
        target.BringIntoView();
        (window as Layoutable)?.UpdateLayout();
        Dispatcher.UIThread.RunJobs();

        var at = target.TranslatePoint(new Point(8, 8), window)!.Value;

        window.MouseDown(at, MouseButton.Left);
        window.MouseUp(at, MouseButton.Left);
        Dispatcher.UIThread.RunJobs();
    }

    public static void Press(TopLevel window, Key key, PhysicalKey physical)
    {
        window.KeyPress(key, RawInputModifiers.None, physical, null);
        window.KeyRelease(key, RawInputModifiers.None, physical, null);
        Dispatcher.UIThread.RunJobs();
    }
}
