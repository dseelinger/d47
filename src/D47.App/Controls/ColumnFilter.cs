using System.Globalization;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Layout;
using Avalonia.Media;
using D47.App.Panel;
using D47.App.Theming;

namespace D47.App.Controls;

/// <summary>
/// The FILTER tile and the bar it opens over an <see cref="ArrangedTable{TRow}"/>, the same on the desktop and in the
/// headset, with the head line and the line drawn when every row is hidden. The filter hides rows already drawn and
/// sends nothing anywhere. The page redraws on the table's <see cref="ArrangedTable{TRow}.Changed"/> and places what
/// these return.
/// </summary>
public static class ColumnFilter
{
    public const string TileText = "Filter";

    public const string TickAllText = "Tick all";

    public const string UntickAllText = "Untick all";

    public const string ClearText = "Clear filters";

    public const string DoneText = "Done";

    public const string Choosing = "Press a column's name to choose it. Sorting is off until you press DONE.";

    public const string ChosenTail = " is chosen. Untick a value to hide the rows that have it, or press another column's name. Sorting is off until you press DONE.";

    /// <summary>The hint for a list column; {0} is the block's label in lower case.</summary>
    public const string ListTail = " is chosen. A row shows while at least one of its {0} is ticked. Press another column's name to filter it too. Sorting is off until you press DONE.";

    public const string HeadsetChoosing = "Press a column's name to choose it. The rows come back when you press DONE.";

    public const string HeadsetChosenTail = " is chosen. The rows come back when you press DONE.";

    /// <summary>The caption over the blocks on a search; {0} is the rows fetched.</summary>
    public const string FetchedCaption = "Values in the {0} rows fetched";

    /// <summary>The caption over the blocks elsewhere; {0} is the rows the table has.</summary>
    public const string RowsCaption = "Values in the {0} rows";

    public const string Kept = "These hide rows D47 already has.";

    /// <summary>The line under the blocks on a search; {0} is the rows fetched.</summary>
    public const string KeptSearch = "These hide rows D47 already has. Nothing is sent to Spansh, and the spoken answer still covers all {0}.";

    public const string ListCount = "A count is how many rows list that value. UNTICK ALL, then tick the ones you need, to see only the rows that have them.";

    /// <summary>The breadcrumb of the page for a field with many values; {0} is the field's label.</summary>
    public const string PickCrumb = "Filter by {0}";

    public const string PickHelp = "Untick a value to hide the rows that have it.";

    private const double Gap = 8;

    private const double BlockLabel = 104;

    /// <summary>A picker id is this character plus the block's index, then the value, so no typed text matches the index.</summary>
    private const char BlockMark = '';

    /// <summary>The control name of a part of the filter for table <paramref name="id"/>.</summary>
    public static string NameOf(string id, string part) => $"{id}.Filter.{part}";

    /// <summary>The control name of a value's checkbox in the bar.</summary>
    public static string ValueName(string id, string key, int block, string value) =>
        NameOf(id, $"Value.{key}.{block.ToString(CultureInfo.InvariantCulture)}.{value}");

    /// <summary>A drawn field of the table declares a value list.</summary>
    public static bool Offered<TRow>(ArrangedTable<TRow> table) =>
        table.ShownFields.SelectMany(field => field.More.Prepend(field)).Any(field => table.HasValues(field.Key));

    /// <summary>In the headset the rows are not drawn while the bar is open.</summary>
    public static bool HidesRows<TRow>(ArrangedTable<TRow> table) => table.Headset && table.Filtering;

    /// <summary>FILTER, or FILTER · 2 with two fields filtered; selected while the bar is open; null when no drawn field has a value list.</summary>
    public static Button? Tile<TRow>(ArrangedTable<TRow> table)
    {
        if (!Offered(table))
        {
            return null;
        }

        var filtered = table.FilteredLabels.Count;
        var text = filtered == 0 ? TileText : $"{TileText} · {filtered.ToString(CultureInfo.InvariantCulture)}";
        var tile = Part(table.Id, "Tile", text, table.ToggleFiltering);
        tile.Classes.Set("selected", table.Filtering);

        return tile;
    }

    /// <summary>
    /// The bar, the grey line under it, and the chosen field's blocks of values; null while the bar is closed.
    /// <paramref name="rows"/> are every row the table has, before the filter. With <paramref name="prompts"/>, a field
    /// with more than 20 values in a block opens them on a page of their own; without, they are listed in the bar.
    /// </summary>
    public static Control? Bar<TRow>(ArrangedTable<TRow> table, IReadOnlyList<TRow> rows, bool search = false, PanelPrompts? prompts = null)
    {
        if (!table.Filtering)
        {
            return null;
        }

        table.Observe(rows);

        if (prompts is not null)
        {
            table.PickMany = key => Pick(table, key, prompts);
        }

        var chosen = table.FilterChosen;
        var blocks = chosen is null ? [] : table.FilterBlocks.Where(block => block.FieldKey == chosen).ToList();

        var left = new WrapPanel
        {
            ItemSpacing = Gaps.Tile,
            LineSpacing = Gaps.Tile,
            Children =
            {
                Part(table.Id, "TickAll", TickAllText, () => table.SetTicked(chosen!, (_, _) => true), chosen is not null),
                Part(table.Id, "UntickAll", UntickAllText, () => table.SetTicked(chosen!, (_, _) => false), chosen is not null),
            },
        };

        var right = new WrapPanel
        {
            ItemSpacing = Gaps.Tile,
            LineSpacing = Gaps.Tile,
            Margin = Gaps.TileBefore,
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Top,
            Children =
            {
                Part(table.Id, "Clear", ClearText, table.TickEveryValue, table.FilteredLabels.Count > 0),
                Part(table.Id, "Done", DoneText, table.ToggleFiltering),
            },
        };
        Grid.SetColumn(right, 1);

        var bar = new StackPanel
        {
            Spacing = Gap,
            Margin = new Thickness(0, Gap, 0, 0),
            Children =
            {
                new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), Children = { left, right } },
                Hint(table, blocks),
            },
        };

        if (blocks.Count == 0)
        {
            return bar;
        }

        var count = rows.Count.ToString(CultureInfo.InvariantCulture);
        var values = new StackPanel
        {
            Name = NameOf(table.Id, "Values"),
            Spacing = 6,
            Margin = new Thickness(0, 0, 0, 6),
            Children = { StatTile.Caption(string.Format(CultureInfo.InvariantCulture, search ? FetchedCaption : RowsCaption, count), ThemeManager.GreyKey) },
        };

        var listed = new StackPanel { Spacing = Gap };

        foreach (var block in blocks)
        {
            listed.Children.Add(Block(table, block));
        }

        values.Children.Add(listed);

        if (blocks.Any(block => block.Several))
        {
            values.Children.Add(Grey(NameOf(table.Id, "ListCount"), ListCount));
        }

        values.Children.Add(Grey(NameOf(table.Id, "Kept"), search ? string.Format(CultureInfo.InvariantCulture, KeptSearch, count) : Kept));
        bar.Children.Add(values);

        return bar;
    }

    /// <summary>
    /// "these 18 of 25, filtered by Economy and Power", or with <paramref name="sortedBy"/>, the name of the sort the rows
    /// shown are in, "these 5 of 25 sorted by Population, filtered by Economy"; null while nothing is hidden.
    /// </summary>
    public static string? Words<TRow>(ArrangedTable<TRow> table, IReadOnlyList<TRow> rows, string? sortedBy)
    {
        var hidden = table.Hidden(rows);

        if (hidden == 0)
        {
            return null;
        }

        var shown = (rows.Count - hidden).ToString(CultureInfo.InvariantCulture);
        var all = rows.Count.ToString(CultureInfo.InvariantCulture);
        var by = And(table.FilteredLabels);

        return sortedBy is null
            ? $"these {shown} of {all}, filtered by {by}"
            : $"these {shown} of {all} sorted by {sortedBy}, filtered by {by}";
    }

    /// <summary>The head line's <see cref="Words"/> in A capitals, wrapping; null while nothing is hidden.</summary>
    public static TextBlock? Head<TRow>(ArrangedTable<TRow> table, IReadOnlyList<TRow> rows, string? sortedBy)
    {
        if (Words(table, rows, sortedBy) is not { } words)
        {
            return null;
        }

        var line = Typeset.Chrome(
            words.ToUpperInvariant(), TypeScale.Small, ThemeManager.AKey,
            weight: FontWeight.Medium, tracking: TypeScale.Small * Fonts.ChromeTracking,
            wrap: true, vertical: VerticalAlignment.Center);
        line.Name = NameOf(table.Id, "Words");

        return line;
    }

    /// <summary>The CLEAR FILTERS tile drawn beside the head line.</summary>
    public static Button HeadClear<TRow>(ArrangedTable<TRow> table) => Part(table.Id, "HeadClear", ClearText, table.TickEveryValue);

    /// <summary>
    /// When the filter hides every row, one White line naming the filtered fields and a CLEAR FILTERS tile, followed by
    /// <paramref name="beside"/> when given, drawn in place of the rows; null otherwise.
    /// </summary>
    public static Control? Empty<TRow>(ArrangedTable<TRow> table, IReadOnlyList<TRow> rows, bool search = false, Control? beside = null)
    {
        if (rows.Count == 0 || table.Filter(rows).Count > 0)
        {
            return null;
        }

        var line = Typeset.Text(EmptyWords(table.FilteredLabels, search), TypeScale.Body, ThemeManager.WhiteKey, wrap: true);
        line.Name = NameOf(table.Id, "Empty");

        var tiles = new StackPanel { Orientation = Orientation.Horizontal, Spacing = Gaps.Tile, Children = { Part(table.Id, "EmptyClear", ClearText, table.TickEveryValue) } };

        if (beside is not null)
        {
            tiles.Children.Add(beside);
        }

        return new StackPanel
        {
            Spacing = Gap,
            Margin = new Thickness(0, Gap, 0, 0),
            Children = { line, tiles },
        };
    }

    /// <summary>"No row fetched has both a ticked Economy and a ticked Power.", "No row has a ticked Location."</summary>
    public static string EmptyWords(IReadOnlyList<string> labels, bool search)
    {
        var row = search ? "No row fetched has" : "No row has";
        var ticked = labels.Select(label => $"a ticked {label}").ToList();

        return ticked.Count == 2
            ? $"{row} both {ticked[0]} and {ticked[1]}."
            : $"{row} {And(ticked)}.";
    }

    /// <summary>"A", "A and B", "A, B and C".</summary>
    private static string And(IReadOnlyList<string> items) => items.Count switch
    {
        0 => string.Empty,
        1 => items[0],
        _ => $"{string.Join(", ", items.Take(items.Count - 1))} and {items[^1]}",
    };

    private static TextBlock Hint<TRow>(ArrangedTable<TRow> table, IReadOnlyList<FilterBlock> blocks)
    {
        var hint = Typeset.Text(string.Empty, TypeScale.Small, ThemeManager.GreyKey, wrap: true);
        hint.Name = NameOf(table.Id, "Hint");

        if (blocks.Count == 0)
        {
            hint.Text = table.Headset ? HeadsetChoosing : Choosing;

            return hint;
        }

        var chosen = new Run(blocks[0].FieldLabel.ToUpperInvariant());
        ThemeInk.Bind(chosen, TextElement.ForegroundProperty, ThemeManager.WhiteKey);

        var tail = table.Headset ? HeadsetChosenTail
            : blocks.FirstOrDefault(block => block.Several) is { } list
                ? string.Format(CultureInfo.InvariantCulture, ListTail, list.Label.ToLowerInvariant())
                : ChosenTail;

        hint.Inlines = [chosen, new Run(tail)];

        return hint;
    }

    private static Grid Block<TRow>(ArrangedTable<TRow> table, FilterBlock block)
    {
        var grid = new TileGrid { Columns = 4 };

        foreach (var value in block.Values)
        {
            var (box, text) = LabeledCheckBox.Build(value.Value);
            text.TextWrapping = TextWrapping.Wrap;

            var rows = value.Count.ToString(CultureInfo.InvariantCulture);
            var count = Typeset.Mono(rows, TypeScale.Small, ThemeManager.GreyKey, vertical: VerticalAlignment.Center, margin: new Thickness(12, 0, 0, 0));
            DockPanel.SetDock(count, Dock.Right);

            box.Content = null;
            box.Content = new DockPanel { Children = { count, text } };
            box.Name = ValueName(table.Id, block.FieldKey, block.Index, value.Value);
            box.IsChecked = value.Ticked;
            box.HorizontalAlignment = HorizontalAlignment.Stretch;
            box.HorizontalContentAlignment = HorizontalAlignment.Stretch;
            box.VerticalAlignment = VerticalAlignment.Stretch;
            AutomationProperties.SetName(box, $"{value.Value}, {rows}");

            box.IsCheckedChanged += (_, _) =>
            {
                if (box.IsChecked == true)
                {
                    table.Tick(block.FieldKey, block.Index, value.Value);
                }
                else
                {
                    table.Untick(block.FieldKey, block.Index, value.Value);
                }
            };

            grid.Children.Add(box);
        }

        var name = Typeset.Chrome(
            block.Label.ToUpperInvariant(), TypeScale.Control, ThemeManager.GreyKey,
            weight: FontWeight.SemiBold, tracking: TypeScale.Control * Fonts.ChromeTracking,
            trimming: TextTrimming.CharacterEllipsis, vertical: VerticalAlignment.Center);

        var head = new Border { Height = TypeScale.MinimumTarget, VerticalAlignment = VerticalAlignment.Top, Child = name };
        Grid.SetColumn(grid, 1);

        return new Grid
        {
            ColumnDefinitions = [new ColumnDefinition(BlockLabel, GridUnitType.Pixel), new ColumnDefinition(GridLength.Star)],
            Children = { head, grid },
        };
    }

    /// <summary>Opens every block of the field on a picker page; USE THESE applies its ticks and BACK changes nothing.</summary>
    private static void Pick<TRow>(ArrangedTable<TRow> table, string key, PanelPrompts prompts)
    {
        var blocks = table.FilterBlocks.Where(block => block.FieldKey == key).ToList();

        if (blocks.Count == 0)
        {
            return;
        }

        static string Id(int block, string value) => (char)(BlockMark + block) + value;

        var entries = blocks
            .SelectMany(block => block.Values.Select(value => (Id: Id(block.Index, value.Value), Block: block, Value: value)))
            .ToList();
        var byId = entries.ToDictionary(entry => entry.Id, StringComparer.Ordinal);
        var crumb = string.Format(CultureInfo.InvariantCulture, PickCrumb, blocks[0].FieldLabel);

        prompts.Pick(
            NameOf(table.Id, "Pick." + key),
            crumb,
            new PickerRequest
            {
                Prompt = crumb,
                Help = PickHelp,
                Choices = [.. entries.Select(entry => entry.Id)],
                Describe = id => byId[id].Value.Value,
                Several = true,
                TicksAll = true,
                CurrentSeveral = [.. entries.Where(entry => entry.Value.Ticked).Select(entry => entry.Id)],
                GroupOf = id => byId[id].Block.Label,
                AsideOf = id => byId[id].Value.Count.ToString(CultureInfo.InvariantCulture),
                ChoiceNoun = "values",
            },
            result =>
            {
                var ticked = new HashSet<string>(result.Values ?? [], StringComparer.Ordinal);
                table.SetTicked(key, (block, value) => ticked.Contains(Id(block, value)));
            });
    }

    private static TextBlock Grey(string name, string text)
    {
        var line = Typeset.Text(text, TypeScale.Small, ThemeManager.GreyKey, wrap: true);
        line.Name = name;

        return line;
    }

    private static Button Part(string id, string part, string text, Action press, bool enabled = true)
    {
        var button = new Button
        {
            Name = NameOf(id, part),
            Content = text,
            MinHeight = TypeScale.MinimumTarget,
            IsEnabled = enabled,
            VerticalAlignment = VerticalAlignment.Top,
        };
        button.Click += (_, _) => press();

        return button;
    }
}
