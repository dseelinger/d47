using Avalonia;
using Avalonia.Controls;
using Avalonia.LogicalTree;
using Avalonia.VisualTree;
using D47.App.Panel;

namespace D47.App.Tests;

/// <summary>
/// The checklist's own line ticks, told apart from a switch that is part of a page's chrome.
/// </summary>
internal static class Ticks
{
    /// <summary>Every line tick on this tree that is not sitting on a page's chrome bar.</summary>
    public static IReadOnlyList<ToggleSwitch> On(Visual root) =>
    [
        .. root.GetVisualDescendants()
            .OfType<ToggleSwitch>()
            .Where(toggle => toggle.GetVisualParent() is DockPanel && !IsChrome(toggle)),
    ];

    /// <summary>The words on those ticks, in the order they are drawn.</summary>
    public static IReadOnlyList<string> Words(Visual root) =>
        [.. On(root).Select(Label)];

    public static string Label(ToggleSwitch tick) =>
        (tick.GetVisualParent() as DockPanel)?
            .GetVisualDescendants().OfType<TextBlock>().FirstOrDefault()?.Text
        ?? string.Empty;

    /// <summary>Whether anything above this control is marked as chrome.</summary>
    private static bool IsChrome(Control control) =>
        control.GetSelfAndLogicalAncestors().OfType<Control>().Any(PageChrome.IsChrome);
}
