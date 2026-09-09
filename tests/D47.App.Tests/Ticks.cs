using Avalonia;
using Avalonia.Controls;
using Avalonia.LogicalTree;
using Avalonia.VisualTree;
using D47.App.Panel;

namespace D47.App.Tests;

/// <summary>
/// The checklist's own line ticks, told apart from a checkbox that is part of a page's chrome.
/// </summary>
internal static class Ticks
{
    /// <summary>Every checkbox on this tree that is not sitting on a page's chrome bar.</summary>
    public static IReadOnlyList<CheckBox> On(Visual root) =>
    [
        .. root.GetVisualDescendants()
            .OfType<CheckBox>()
            .Where(box => !IsChrome(box)),
    ];

    /// <summary>The words on those ticks, in the order they are drawn.</summary>
    public static IReadOnlyList<string> Words(Visual root) =>
        [.. On(root).Select(tick => tick.Content as string ?? string.Empty)];

    /// <summary>Whether anything above this control is marked as chrome.</summary>
    private static bool IsChrome(Control control) =>
        control.GetSelfAndLogicalAncestors().OfType<Control>().Any(PageChrome.IsChrome);
}
