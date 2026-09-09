using Avalonia.Controls;
using Avalonia.Threading;
using D47.App.Panel;

namespace D47.App.Tests;

/// <summary>
/// Driving the panel's mode control, which is a real <see cref="ComboBox"/> in the page bar.
/// </summary>
internal static class PanelModes
{
    /// <summary>Whether this surface is offering a choice of readings at all.</summary>
    public static bool Offered(PanelView panel) =>
        panel.GetControl<StackPanel>("ModePicker").IsVisible;

    /// <summary>The reading the box says is showing, as the Commander reads it.</summary>
    public static string? Showing(PanelView panel) =>
        panel.GetControl<ComboBox>("ModeBox").SelectedItem as string;

    /// <summary>Picks one by its root key, the way a Commander picks it: move the selection.</summary>
    public static void Choose(PanelView panel, string root)
    {
        var word = panel.Nav.Roots(panel.Tab).First(crumb => crumb.Key == root).Word;
        var box = panel.GetControl<ComboBox>("ModeBox");

        var index = (box.ItemsSource as IReadOnlyList<string>)?
            .ToList()
            .IndexOf(word) ?? -1;

        if (index < 0)
        {
            throw new InvalidOperationException(
                $"No reading called \"{word}\" is on offer. The box holds: "
                + string.Join(", ", (box.ItemsSource as IReadOnlyList<string>) ?? []));
        }

        box.SelectedIndex = index;
        Dispatcher.UIThread.RunJobs();
    }

    /// <summary>How many readings this tab offers, from the navigator that decides them.</summary>
    public static int Count(PanelView panel) => panel.Nav.Roots(panel.Tab).Count;
}
