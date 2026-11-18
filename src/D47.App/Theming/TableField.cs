using Avalonia.Controls;
using Avalonia.Layout;

namespace D47.App.Theming;

/// <summary>
/// One entry of an <see cref="ArrangedTable{TRow}"/>: a column, its header and how a row's cell is built. A field
/// with no <see cref="Label"/> is a control column and is not arranged.
/// </summary>
public sealed record TableField<TRow>(string Key, TableColumn Column, Func<TRow, Control> Cell)
{
    /// <summary>The header text and the name the arrangement shows; null for a control column.</summary>
    public string? Label { get; init; }

    /// <summary>The name the arrangement shows where it differs from the header <see cref="Label"/>, such as a field drawing several columns.</summary>
    public string? Name { get; init; }

    /// <summary>A line under the header label.</summary>
    public string? Second { get; init; }

    public HorizontalAlignment Alignment { get; init; } = HorizontalAlignment.Left;

    /// <summary>How the header sorts the rows; the header shows its label. Null where the header does not press.</summary>
    public SortColumn<TRow>? Sort { get; init; }

    public bool ByDefault { get; init; } = true;

    /// <summary>Never left out of the arrangement.</summary>
    public bool Required { get; init; }

    /// <summary>Further columns drawn after this one from the same entry; they move with it and are not arranged on their own.</summary>
    public IReadOnlyList<TableField<TRow>> More { get; init; } = [];
}
