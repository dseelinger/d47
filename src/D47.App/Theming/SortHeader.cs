using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Styling;

namespace D47.App.Theming;

public enum SortDirection
{
    Ascending,
    Descending,
}

/// <summary>A table sorted by the column at <see cref="Column"/>, in <see cref="Direction"/>.</summary>
public readonly record struct SortOrder(int Column, SortDirection Direction);

/// <summary>
/// One column of a sortable table: its label, an optional second line, and how its rows compare. A
/// column built with <see cref="Plain"/> does not sort.
/// </summary>
public sealed record SortColumn<TRow>
{
    private SortColumn(string label, SortDirection first, Func<TRow, bool>? present, Comparison<TRow>? compare)
    {
        Label = label;
        First = first;
        Present = present;
        Compare = compare;
    }

    public string Label { get; }

    /// <summary>A line under the label; part of the same press target, not a sort of its own.</summary>
    public string? Second { get; init; }

    /// <summary>The direction the first press sorts in.</summary>
    public SortDirection First { get; }

    /// <summary>Sorts in <see cref="First"/> only: a second press returns the table to its default order.</summary>
    public bool OneWay { get; init; }

    public bool Sortable => Compare is not null;

    internal Func<TRow, bool>? Present { get; }

    internal Comparison<TRow>? Compare { get; }

    public static SortColumn<TRow> Plain(string label) => new(label, SortDirection.Ascending, null, null);

    /// <summary>A column whose rows are sorted elsewhere, such as by a service: it presses and shows its arrow, and compares nothing.</summary>
    public static SortColumn<TRow> Remote(string label, SortDirection first) =>
        new(label, first, _ => true, (_, _) => 0);

    /// <summary>Sorts A–Z first, ignoring case; a null or blank value is missing.</summary>
    public static SortColumn<TRow> Text(string label, Func<TRow, string?> key) => new(
        label,
        SortDirection.Ascending,
        row => !string.IsNullOrWhiteSpace(key(row)),
        (a, b) => StringComparer.CurrentCultureIgnoreCase.Compare(key(a), key(b)));

    /// <summary>
    /// Sorts by a figure, first in <paramref name="first"/>: ascending for a distance, time or cost,
    /// descending for a size, credits, a count or a date. A null figure is missing.
    /// </summary>
    public static SortColumn<TRow> Figure<TKey>(string label, Func<TRow, TKey?> key, SortDirection first)
        where TKey : struct, IComparable<TKey> => new(
        label,
        first,
        row => key(row).HasValue,
        (a, b) => key(a)!.Value.CompareTo(key(b)!.Value));
}

/// <summary>The sort a column header applies, as pure functions over the rows.</summary>
public static class ColumnSort
{
    public static SortDirection Reversed(SortDirection direction) =>
        direction == SortDirection.Ascending ? SortDirection.Descending : SortDirection.Ascending;

    /// <summary>
    /// The order after pressing column <paramref name="pressed"/>: a new column sorts in its first
    /// direction, the sorted column reverses, and the reversed column returns to the default order
    /// (null). The column the default order follows counts as a new column while no sort is chosen.
    /// </summary>
    public static SortOrder? Next<TRow>(IReadOnlyList<SortColumn<TRow>> columns, SortOrder? current, int pressed)
    {
        if (pressed < 0 || pressed >= columns.Count || !columns[pressed].Sortable)
        {
            return current;
        }

        var column = columns[pressed];

        if (current is { } sorted && sorted.Column == pressed)
        {
            return sorted.Direction == column.First && !column.OneWay
                ? new SortOrder(pressed, Reversed(column.First))
                : null;
        }

        return new SortOrder(pressed, column.First);
    }

    /// <summary>
    /// <paramref name="rows"/>, given in the table's default order, sorted by <paramref name="order"/>.
    /// Rows missing the column's value go last in either direction; ties keep the default order. A null
    /// order returns the rows as given.
    /// </summary>
    public static IReadOnlyList<TRow> Sorted<TRow>(
        IReadOnlyList<TRow> rows, IReadOnlyList<SortColumn<TRow>> columns, SortOrder? order)
    {
        if (order is not { } sort
            || sort.Column < 0
            || sort.Column >= columns.Count
            || columns[sort.Column] is not { Present: { } present, Compare: { } compare })
        {
            return rows;
        }

        var comparer = Comparer<TRow>.Create(compare);
        var known = rows.Where(present);
        var ordered = sort.Direction == SortDirection.Ascending
            ? known.OrderBy(row => row, comparer)
            : known.OrderByDescending(row => row, comparer);

        return [.. ordered, .. rows.Where(row => !present(row))];
    }
}

/// <summary>
/// A table's sortable column headers. Each pressable cell is a button at least
/// <see cref="TypeScale.MinimumTarget"/> tall, so it presses in the headset too; the sorted column
/// reads ▲ or ▼ after its label in A, the rest are Grey. The sort lasts as long as this object.
/// </summary>
public sealed class SortHeader<TRow>
{
    private const double Bleed = 6;

    private readonly string _name;

    public SortHeader(string name, IReadOnlyList<SortColumn<TRow>> columns, SortOrder? byDefault = null)
    {
        _name = name;
        Columns = columns;
        Default = byDefault;
    }

    public IReadOnlyList<SortColumn<TRow>> Columns { get; }

    /// <summary>The column the table's default order follows, if it follows one; shown while no sort is chosen.</summary>
    public SortOrder? Default { get; }

    /// <summary>The chosen sort; null is the table's default order.</summary>
    public SortOrder? Order { get; private set; }

    /// <summary>Raised after <see cref="Order"/> changes.</summary>
    public event Action? Changed;

    public void Press(int column)
    {
        var next = ColumnSort.Next(Columns, Order, column);

        if (next == Order)
        {
            return;
        }

        Order = next;
        Changed?.Invoke();
    }

    /// <summary>Sets <see cref="Order"/> to a sort held elsewhere, without raising <see cref="Changed"/>.</summary>
    public void Show(SortOrder? order) => Order = order;

    /// <summary>Returns the table to its default order.</summary>
    public void Reset()
    {
        if (Order is null)
        {
            return;
        }

        Order = null;
        Changed?.Invoke();
    }

    /// <summary><paramref name="rows"/>, given in the default order, sorted by <see cref="Order"/>.</summary>
    public IReadOnlyList<TRow> Sort(IReadOnlyList<TRow> rows) => ColumnSort.Sorted(rows, Columns, Order);

    /// <summary>The control name of column <paramref name="column"/>'s cell: the header's name, a dot, the label without spaces.</summary>
    public string CellName(int column) => $"{_name}.{Columns[column].Label.Replace(" ", string.Empty, StringComparison.Ordinal)}";

    /// <summary>A new cell for column <paramref name="column"/>, its label set to <paramref name="alignment"/>.</summary>
    public Control Cell(int column, HorizontalAlignment alignment)
    {
        var spec = Columns[column];
        var shown = Order ?? Default;
        var arrow = shown is { } sorted && sorted.Column == column && spec.Sortable
            ? sorted.Direction == SortDirection.Ascending ? " ▲" : " ▼"
            : null;

        var label = Line(spec.Label + arrow, TypeScale.Meta, FontWeight.Medium, arrow is null ? ThemeManager.GreyKey : ThemeManager.AKey);
        label.HorizontalAlignment = alignment;

        Control content = label;

        if (spec.Second is { } second)
        {
            var under = Line(second, TypeScale.MetaSmall, FontWeight.Normal, ThemeManager.GreyKey);
            under.HorizontalAlignment = alignment;
            content = new StackPanel { Children = { label, under } };
        }

        if (!spec.Sortable)
        {
            content.HorizontalAlignment = alignment;
            content.VerticalAlignment = VerticalAlignment.Center;
            content.Name = CellName(column);
            return content;
        }

        var button = new Button
        {
            Name = CellName(column),
            Theme = Application.Current?.FindResource("D47.SortHeader") as ControlTheme,
            Content = content,
            MinHeight = TypeScale.MinimumTarget,
            Margin = new Thickness(-Bleed, 0),
            Padding = new Thickness(Bleed, 0),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
            HorizontalContentAlignment = alignment,
            VerticalContentAlignment = VerticalAlignment.Center,
        };
        Avalonia.Automation.AutomationProperties.SetName(button, $"Sort by {spec.Label}");
        button.Click += (_, _) => Press(column);

        return button;
    }

    private static TextBlock Line(string text, double size, FontWeight weight, string key)
    {
        var block = TitleText.Build(text, size, TitleRank.Subgroup);
        block.FontFamily = Fonts.ChromeFamily;
        block.FontWeight = weight;
        block.Bind(TextBlock.ForegroundProperty, Application.Current!.Resources.GetResourceObservable(key));
        return block;
    }
}
