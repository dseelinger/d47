namespace D47.App.Controls;

/// <summary>
/// A list inside a page's content, beside its detail: list heads over 44px rows, the selected row solid accent.
/// It is never a second sidebar beside the page sidebar.
/// </summary>
public sealed class ListColumn : RowColumn
{
    public ListColumn(
        IReadOnlyList<SidebarGroup> groups,
        string? selected = null,
        Action<string>? select = null,
        string? note = null,
        double width = ColumnWidth)
        : base(groups, selected, select, note, width)
    {
    }
}
