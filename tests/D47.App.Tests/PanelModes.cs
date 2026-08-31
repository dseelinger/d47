using Avalonia.Controls;
using Avalonia.Threading;
using D47.App.Panel;

namespace D47.App.Tests;

/// <summary>
/// Driving the panel's mode control, which is a real <see cref="ComboBox"/> in the page bar
/// (<a href="https://github.com/dseelinger/d47/issues/231">#231</a>).
/// <para>
/// It used to be a button that opened a chooser drawn into the panel's own layer, on the belief
/// that a <c>ComboBox</c> could not be used here at all — a popup needs a top level and the
/// headset's host window is never shown. That is true of the offscreen copy and was applied to
/// both; this one lives in a real window. The headset's copy still gets a drawn list, because
/// <c>OffscreenSurface</c> intercepts the press before a pointer event exists.
/// </para>
/// <para>
/// One helper rather than a copy in each test file, because the previous shape was copied into
/// three of them and every one had to be found when it changed.
/// </para>
/// </summary>
internal static class PanelModes
{
    /// <summary>Whether this surface is offering a choice of readings at all.</summary>
    public static bool Offered(PanelView panel) =>
        panel.GetControl<StackPanel>("ModePicker").IsVisible;

    /// <summary>The reading the box says is showing, as the Commander reads it.</summary>
    public static string? Showing(PanelView panel) =>
        panel.GetControl<ComboBox>("ModeBox").SelectedItem as string;

    /// <summary>
    /// Picks one by its root key, the way a Commander picks it: move the selection. A combo box
    /// reports that through <c>SelectionChanged</c> whether it was moved by a press or by the
    /// keyboard, so this is the same path either gesture takes.
    /// </summary>
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
