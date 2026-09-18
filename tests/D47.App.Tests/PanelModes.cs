using Avalonia.Controls;
using Avalonia.Threading;
using D47.App.Panel;

namespace D47.App.Tests;

/// <summary>
/// Driving the panel's mode control, which is a real <see cref="D47.App.Controls.Stepper"/> in the
/// page bar.
/// </summary>
internal static class PanelModes
{
    /// <summary>Whether this surface is offering a choice of readings at all.</summary>
    public static bool Offered(PanelView panel) =>
        panel.GetControl<StackPanel>("ModePicker").IsVisible;

    /// <summary>The reading the box says is showing, as the Commander reads it.</summary>
    public static string? Showing(PanelView panel) =>
        panel.GetControl<D47.App.Controls.Stepper>("ModeBox").SelectedItem;

    /// <summary>
    /// Picks one by its root key, the way a Commander picks it: move the selection. Goes through the
    /// navigator directly rather than the stepper's arrows, which fire only on a real press (#274) —
    /// a headset press on the same arrow is proven separately, in <see cref="TheVrPanelIsClickableTests"/>.
    /// </summary>
    public static void Choose(PanelView panel, string root)
    {
        if (!panel.Nav.Roots(panel.Tab).Any(crumb => crumb.Key == root))
        {
            throw new InvalidOperationException(
                $"No reading called \"{root}\" is on offer. The tab holds: "
                + string.Join(", ", panel.Nav.Roots(panel.Tab).Select(crumb => crumb.Key)));
        }

        panel.Nav.SelectRoot(root);
        Dispatcher.UIThread.RunJobs();
    }

    /// <summary>How many readings this tab offers, from the navigator that decides them.</summary>
    public static int Count(PanelView panel) => panel.Nav.Roots(panel.Tab).Count;
}
