using Avalonia;
using Avalonia.Controls;
using Avalonia.LogicalTree;
using Avalonia.VisualTree;
using D47.App.Panel;

namespace D47.App.Tests;

/// <summary>
/// The checklist's own line ticks, told apart from a checkbox that is part of a page's chrome
/// (<a href="https://github.com/dseelinger/d47/issues/203">#203</a>).
/// <para>
/// <b>Every one of these assertions used to be able to say "the checkboxes on this page".</b> The
/// bar carried buttons and one filter checkbox that is only ever offered beside the engineer
/// filter, so counting checkboxes counted lines. Goals is a checkbox now — it was a two-state
/// disclosure wearing a button — and a test that counts them all counts the bar as a line.
/// </para>
/// <para>
/// Scoped by the chrome class rather than by name or position, so this stays right as the bar
/// gains controls: what these tests mean by a tick is a checkbox that marks a checklist line as
/// done, and the bar is by definition not that.
/// </para>
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

    /// <summary>
    /// Whether anything above this control is marked as chrome. The logical tree, because that is
    /// where a page's own containers live — a visual walk climbs through templates and answers a
    /// different question.
    /// </summary>
    private static bool IsChrome(Control control) =>
        control.GetSelfAndLogicalAncestors().OfType<Control>().Any(PageChrome.IsChrome);
}
