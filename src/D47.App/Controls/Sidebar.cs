namespace D47.App.Controls;

/// <summary>One row of a <see cref="Sidebar"/>: a key the caller is handed back, its label and an optional count.</summary>
/// <param name="CountKey">The theme brush the count takes while the row is not selected; null follows the label.</param>
public sealed record SidebarItem(string Key, string Label, string? Count = null, string? CountKey = null);

/// <summary>A run of <see cref="SidebarItem"/> rows, under a heading when one is given and a note when it has no rows.</summary>
public sealed record SidebarGroup(string? Heading, IReadOnlyList<SidebarItem> Items, string? Note = null);

/// <summary>
/// The kit's <c>.d47-sidebar</c>: a 230-wide column of 44px rows, grouped under slab headings, with a
/// <c>line2</c> rule on its right and 28 before the content. Hover is Tile2; the selected row is solid A
/// with Knock text. It is the page sidebar; a list inside a page is a <see cref="ListColumn"/>.
/// </summary>
public sealed class Sidebar : RowColumn
{
    public Sidebar(
        IReadOnlyList<SidebarGroup> groups,
        string? selected = null,
        Action<string>? select = null,
        string? note = null,
        double width = ColumnWidth)
        : base(groups, selected, select, note, width)
    {
    }
}
