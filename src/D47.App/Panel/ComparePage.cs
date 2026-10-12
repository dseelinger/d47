using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using D47.App.Controls;
using D47.App.Theming;
using D47.Core.Interface;
using D47.Core.Journal;
using D47.Core.Knowledge;

namespace D47.App.Panel;

/// <summary>Fleet › Ships › Compare: every ship as last seen fitted, sorted by any column and filtered by cargo (#562).</summary>
public sealed class ComparePage : UserControl
{
    public const string Key = "loadout.compare";

    /// <summary>The footer's phrase; the model answers it with <c>get_fleet_loadouts</c>.</summary>
    public const string Phrase = "which ship jumps furthest";

    public const string Hint = "Each ship as it was when you last flew it. Values in credits.";

    public const string Filtered = "No ship carries that much cargo.";

    public const string Unseen =
        "I have not seen any of your ships yet. A loadout is written each time you board a ship.";

    /// <summary>MINIMUM CARGO in tonnes, parallel to <see cref="Minimums"/>; zero is no minimum.</summary>
    public static readonly IReadOnlyList<int> Tonnes = [0, 16, 32, 64, 128, 256, 512];

    public static readonly IReadOnlyList<string> Minimums =
        [.. Tonnes.Select(tonnes => tonnes == 0 ? "ANY" : $"{tonnes} T")];

    /// <summary>The table's columns: ship, cargo, jump, unladen, fuel, value, rebuy, where.</summary>
    private const string Columns = "1.5*,80,90,100,70,140,120,1.3*";

    /// <summary>One ship's row. A figure is null where no loadout has been read for it.</summary>
    public sealed record Ship(
        string Name,
        string Hull,
        int? Cargo,
        double? Jump,
        double? Unladen,
        double? Fuel,
        long? Value,
        long? Rebuy,
        string Where,
        bool InCurrentSystem);

    /// <summary>The table's columns, each sortable from its header.</summary>
    public static readonly IReadOnlyList<SortColumn<Ship>> Heads =
    [
        SortColumn<Ship>.Text("Ship", ship => ship.Name),
        SortColumn<Ship>.Figure("Cargo", ship => ship.Cargo, SortDirection.Descending),
        SortColumn<Ship>.Figure("Jump", ship => ship.Jump, SortDirection.Descending),
        SortColumn<Ship>.Figure("Unladen", ship => ship.Unladen, SortDirection.Descending),
        SortColumn<Ship>.Figure("Fuel", ship => ship.Fuel, SortDirection.Descending),
        SortColumn<Ship>.Figure("Value", ship => ship.Value, SortDirection.Descending),
        SortColumn<Ship>.Figure("Rebuy", ship => ship.Rebuy, SortDirection.Ascending),
        SortColumn<Ship>.Text("Where", ship => ship.Where == Nowhere ? null : ship.Where),
    ];

    /// <summary>The default order: furthest jump first.</summary>
    public static readonly SortOrder ByJump = new(2, SortDirection.Descending);

    /// <summary>The Where cell of a ship whose location is not known.</summary>
    private const string Nowhere = "—";

    public static NavCrumb Crumb => new(Key, "Compare") { Level = Key, Whole = true };

    private readonly Func<CommanderGameState?> _state;
    private readonly JournalClock _clock;
    private readonly StackPanel _body = new();
    private readonly StackPanel _table = new() { Spacing = IndexPage.ListGap };
    private readonly SortHeader<Ship> _head = new("CompareHead", Heads, ByJump);
    private readonly Stepper _minimum;
    private object? _seen;

    public ComparePage(Func<CommanderGameState?> state)
    {
        _state = state;
        _clock = new JournalClock(() => state()?.Session.LastEventAt);

        _head.Changed += Draw;

        _minimum = new Stepper
        {
            Name = "CompareMinimum",
            ItemsSource = Minimums,
            SelectedIndex = 0,
            Wraps = false,
            Width = 220,
            VerticalAlignment = VerticalAlignment.Center,
        };
        _minimum.SelectionChanged += (_, _) => Draw();

        var root = new DockPanel { Margin = new Thickness(14) };
        var footer = new PageFooter(Phrase, _clock) { Margin = new Thickness(0, 12, 0, 0) };
        DockPanel.SetDock(footer, Dock.Bottom);
        root.Children.Add(footer);
        root.Children.Add(LoadoutPages.Scrolling(_body));

        Content = root;
        Draw();
    }

    /// <summary>The panel keeps a drilled page; opening it again returns the table to its default order.</summary>
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _head.Reset();
    }

    /// <summary>Filters the list by <see cref="Tonnes"/>[<paramref name="index"/>].</summary>
    internal void Minimum(int index)
    {
        _minimum.SelectedIndex = Math.Clamp(index, 0, Tonnes.Count - 1);
        Draw();
    }

    /// <summary>Redraws when the fleet, a loadout, the ship or the system has changed.</summary>
    public bool Tick()
    {
        var changed = _clock.Tick();

        if (Equals(Stamp(), _seen))
        {
            return changed;
        }

        Draw();
        return true;
    }

    private object Stamp()
    {
        var state = _state();
        return (state, state?.Fleet, state?.Loadouts, state?.Ship, state?.Location.StarSystem, state?.Location.StationName);
    }

    /// <summary>Every ship the journal has reported, with its last-seen loadout where one was read.</summary>
    public static IReadOnlyList<Ship> Ships(CommanderGameState? state)
    {
        if (state is null)
        {
            return [];
        }

        var here = state.Location.StarSystem;
        var stored = state.Fleet.Ships.ToDictionary(ship => ship.ShipId);
        var ids = new List<int>();

        if (state.Ship is { IsKnown: true, ShipId: { } flying })
        {
            ids.Add(flying);
        }

        ids.AddRange(state.Loadouts.Ships.Keys);
        ids.AddRange(stored.Keys);

        return
        [
            .. ids.Distinct().Select(id =>
            {
                var aboard = state.Ship is { IsKnown: true } ship && ship.ShipId == id;
                var loadout = aboard ? state.Ship : state.Loadouts.For(id)?.Loadout;
                stored.TryGetValue(id, out var parked);

                var type = loadout?.Type ?? parked?.Type;
                var hull = loadout?.TypeSaid ?? (type is null ? "Unknown hull" : EliteSpecifications.HullSaid(type));
                var name = Blank(loadout?.Name) ?? Blank(parked?.Name) ?? hull;
                var ident = Blank(loadout?.Ident);

                var (where, system) = aboard ? Aboard(state)
                    : parked is { InTransit: true } ? ("In transit", null)
                    : parked is { HasSystem: true } ? (Join(parked.StarSystem, parked.StationName), parked.StarSystem)
                    : (Nowhere, null);

                return new Ship(
                    name,
                    ident is null ? hull : $"{hull} · {ident}",
                    loadout?.CargoCapacity,
                    loadout?.MaxJumpRange,
                    loadout?.UnladenMass,
                    loadout?.FuelCapacity,
                    loadout?.TotalValue,
                    loadout?.Rebuy,
                    where,
                    system is not null && string.Equals(system, here, StringComparison.OrdinalIgnoreCase));
            }),
        ];
    }

    /// <summary>
    /// The ships at or above <paramref name="minimum"/> tonnes of cargo, sorted by <paramref name="order"/>
    /// or, when it is null, furthest jump first, then by name. A ship missing the sorted figure goes last,
    /// and a minimum drops a ship with no cargo figure.
    /// </summary>
    public static IReadOnlyList<Ship> Listed(IReadOnlyList<Ship> ships, SortOrder? order, int minimum) =>
        ColumnSort.Sorted(
            [
                .. ships
                    .Where(ship => minimum <= 0 || ship.Cargo >= minimum)
                    .OrderBy(ship => ship.Jump is null)
                    .ThenByDescending(ship => ship.Jump)
                    .ThenBy(ship => ship.Name, StringComparer.CurrentCultureIgnoreCase),
            ],
            Heads,
            order);

    /// <summary>The title figure.</summary>
    public static string Showing(int shown, int of) =>
        $"{shown.ToString(CultureInfo.InvariantCulture)} OF {of.ToString(CultureInfo.InvariantCulture)}";

    private static (string, string?) Aboard(CommanderGameState state) =>
        state.Location.StarSystem is { Length: > 0 } system
            ? (Join(system, state.Location.StationName), system)
            : ("Aboard", null);

    private static string Join(string system, string? station) =>
        station is { Length: > 0 } ? $"{system} · {station}" : system;

    private static string? Blank(string? value) => string.IsNullOrWhiteSpace(value) ? null : value;

    private void Draw()
    {
        _seen = Stamp();

        var all = Ships(_state());
        var listed = Listed(all, _head.Order, Tonnes[Math.Max(0, _minimum.SelectedIndex)]);

        _body.Children.Clear();
        _body.Children.Add(TitleText.Block(
            TitleText.Build("Compare ships", TypeScale.Title, TitleRank.Screen),
            TitleText.Context("Every ship as last seen"),
            TitleText.Figure("Showing", Showing(listed.Count, all.Count))));

        _body.Children.Add(Controls());

        _table.Children.Clear();
        _body.Children.Add(_table);

        if (all.Count == 0)
        {
            _table.Children.Add(Message(Unseen));
            return;
        }

        _table.Children.Add(Head());

        if (listed.Count == 0)
        {
            _table.Children.Add(Message(Filtered));
            return;
        }

        foreach (var ship in listed)
        {
            _table.Children.Add(Row(ship));
        }

        _body.Children.Add(Message(Hint));
    }

    private Control Controls()
    {
        // The stepper outlives a redraw, so it leaves the last row before joining this one.
        (_minimum.Parent as Avalonia.Controls.Panel)?.Children.Remove(_minimum);

        return new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 24,
            Margin = new Thickness(0, 20, 0, 16),
            Children = { Label("Minimum cargo"), _minimum },
        };
    }

    private static TextBlock Label(string text)
    {
        var block = TitleText.Build(text, TypeScale.Control, TitleRank.Subgroup);
        block.FontFamily = Fonts.ChromeFamily;
        block.VerticalAlignment = VerticalAlignment.Center;
        LoadoutPages.Themed(block, TextBlock.ForegroundProperty, ThemeManager.GreyKey);
        return block;
    }

    private Control Head()
    {
        var grid = Table(new Thickness(14, 0));

        for (var column = 0; column < Heads.Count; column++)
        {
            var cell = _head.Cell(column, column is 0 or 7 ? HorizontalAlignment.Left : HorizontalAlignment.Right);
            Grid.SetColumn(cell, column);
            grid.Children.Add(cell);
        }

        var head = new Border
        {
            BorderThickness = new Thickness(0, 0, 0, 1),
            Margin = new Thickness(0, 0, 0, 2),
            Child = grid,
        };
        LoadoutPages.Themed(head, Border.BorderBrushProperty, ThemeManager.LineKey);

        return head;
    }

    private static Control Row(Ship ship)
    {
        var grid = Table(new Thickness(14, 0));

        var name = TitleText.Build(ship.Name, TypeScale.ControlLarge, TitleRank.Subgroup);
        name.FontFamily = Fonts.ChromeFamily;
        name.FontWeight = FontWeight.SemiBold;
        name.TextTrimming = TextTrimming.CharacterEllipsis;
        LoadoutPages.Themed(name, TextBlock.ForegroundProperty, ThemeManager.WhiteKey);

        var hull = TitleText.Build(ship.Hull, TypeScale.MetaSmall, TitleRank.Subgroup);
        hull.FontFamily = Fonts.ChromeFamily;
        hull.TextTrimming = TextTrimming.CharacterEllipsis;
        LoadoutPages.Themed(hull, TextBlock.ForegroundProperty, ThemeManager.GreyKey);

        Cell(grid, 0, new StackPanel { Children = { name, hull } }, left: true);
        Cell(grid, 1, Number(ship.Cargo is { } cargo ? $"{cargo.ToString(CultureInfo.InvariantCulture)} T" : null, ThemeManager.YellowKey));
        Cell(grid, 2, Number(ship.Jump?.ToString("0.00' LY'", CultureInfo.InvariantCulture), ThemeManager.AKey));
        Cell(grid, 3, Number(ship.Unladen?.ToString("0.0' T'", CultureInfo.InvariantCulture), ThemeManager.WhiteKey));
        Cell(grid, 4, Number(ship.Fuel?.ToString("0.#' T'", CultureInfo.InvariantCulture), ThemeManager.WhiteKey));
        Cell(grid, 5, Number(ship.Value?.ToString("N0", CultureInfo.InvariantCulture), ThemeManager.AKey));
        Cell(grid, 6, Number(ship.Rebuy?.ToString("N0", CultureInfo.InvariantCulture), ThemeManager.AKey));

        var where = TitleText.Build(ship.Where, TypeScale.Control, TitleRank.Subgroup);
        where.FontFamily = Fonts.ChromeFamily;
        where.FontWeight = FontWeight.Medium;
        where.TextTrimming = TextTrimming.CharacterEllipsis;
        LoadoutPages.Themed(where, TextBlock.ForegroundProperty, ship.InCurrentSystem ? ThemeManager.CyanKey : ThemeManager.AKey);
        Cell(grid, 7, where, left: true);

        var row = new Border { Height = TypeScale.MinimumTarget, Child = grid };
        LoadoutPages.Themed(row, Border.BackgroundProperty, ThemeManager.SlabKey);

        return row;
    }

    private static TextBlock Number(string? text, string key)
    {
        var block = new TextBlock
        {
            Text = text ?? "—",
            FontFamily = new FontFamily(Fonts.MonoFamily),
            FontSize = TypeScale.Control,
        };
        LoadoutPages.Themed(block, TextBlock.ForegroundProperty, text is null ? ThemeManager.GreyKey : key);
        return block;
    }

    private static Grid Table(Thickness margin) => new()
    {
        ColumnDefinitions = new ColumnDefinitions(Columns),
        ColumnSpacing = 14,
        Margin = margin,
    };

    private static void Cell(Grid grid, int column, Control cell, bool left = false)
    {
        cell.VerticalAlignment = VerticalAlignment.Center;
        cell.HorizontalAlignment = left ? HorizontalAlignment.Left : HorizontalAlignment.Right;
        Grid.SetColumn(cell, column);
        grid.Children.Add(cell);
    }

    private static TextBlock Message(string text)
    {
        var block = LoadoutPages.Muted(text);
        block.Margin = new Thickness(0, 12, 0, 0);
        return block;
    }
}
