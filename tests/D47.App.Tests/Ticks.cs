using Avalonia;
using Avalonia.Controls;
using Avalonia.LogicalTree;
using Avalonia.VisualTree;
using D47.App.Panel;

namespace D47.App.Tests;

/// <summary>
/// The checklist's own line ticks, told apart from the other checkboxes on a page.
/// </summary>
internal static class Ticks
{
    /// <summary>Every line tick on this tree that is not sitting on a page's chrome bar.</summary>
    public static IReadOnlyList<CheckBox> On(Visual root) =>
    [
        .. root.GetVisualDescendants()
            .OfType<CheckBox>()
            .Where(box => box.Content is "completed" && !IsChrome(box)),
    ];

    /// <summary>The words on those ticks, in the order they are drawn.</summary>
    public static IReadOnlyList<string> Words(Visual root) =>
        [.. On(root).Select(Label)];

    /// <summary>The line's own text — the checkbox carries "completed" as its own Content, not this.</summary>
    public static string Label(CheckBox tick)
    {
        var row = tick.GetVisualAncestors().OfType<DockPanel>().FirstOrDefault();

        if (row is null)
        {
            return string.Empty;
        }

        var body = row.GetVisualChildren()
            .OfType<Control>()
            .FirstOrDefault(child => !ReferenceEquals(child, tick) && !child.GetVisualDescendants().Contains(tick));

        return body?.GetVisualDescendants().OfType<TextBlock>().FirstOrDefault()?.Text ?? string.Empty;
    }

    /// <summary>Whether anything above this control is marked as chrome.</summary>
    private static bool IsChrome(Control control) =>
        control.GetSelfAndLogicalAncestors().OfType<Control>().Any(PageChrome.IsChrome);
}
