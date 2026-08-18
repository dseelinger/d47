using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using D47.App.Panel;
using Xunit;

namespace D47.App.Tests;

/// <summary>
/// Driving the panel's mode control, which is a drop-down inside the pane rather than a row of
/// segments beside the tabs (remediation.md 10, item 1).
/// <para>
/// One helper rather than a copy in each test file, because the previous shape was copied into
/// three of them and every one had to be found when it changed.
/// </para>
/// </summary>
internal static class PanelModes
{
    /// <summary>Whether this surface is offering a choice of readings at all.</summary>
    public static bool Offered(PanelView panel) => panel.GetControl<Button>("ModeButton").IsVisible;

    /// <summary>The reading the button says is showing, as the Commander reads it.</summary>
    public static string? Showing(PanelView panel) =>
        panel.GetControl<Button>("ModeButton")
            .GetVisualDescendants()
            .OfType<TextBlock>()
            .Select(text => text.Text)
            .FirstOrDefault(text => text is { Length: > 1 });

    /// <summary>
    /// Picks one by its root key, the way a Commander picks it: press the button, then press the
    /// answer. Through the real chooser rather than by setting a property, because the chooser is
    /// what the change now goes through.
    /// </summary>
    public static void Choose(PanelView panel, string root)
    {
        var word = panel.Nav.Roots(panel.Tab).First(crumb => crumb.Key == root).Word;

        panel.GetControl<Button>("ModeButton").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();

        var option = panel.GetVisualDescendants()
            .OfType<Button>()
            .First(button => button.GetVisualDescendants()
                .OfType<TextBlock>()
                .Any(text => text.Text == word));

        option.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();
    }

    /// <summary>How many readings this tab offers, from the navigator that decides them.</summary>
    public static int Count(PanelView panel) => panel.Nav.Roots(panel.Tab).Count;
}
