using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Layout;
using D47.App.Controls;
using D47.App.Theming;
using D47.Core.Interface;
using D47.Core.Journal;

namespace D47.App.Panel;

/// <summary>Commander › Ledgers › Credits: where the credits in the journals on this PC came from and went, by kind (#1289).</summary>
public sealed class CreditsPage : UserControl, IPageSummary, IFilterablePage
{
    public const string PageKey = "credits";

    public const string TableId = "Credits.Kinds";

    public const string AllKey = "credits-all";

    private const string KindPrefix = "credits-kind-";

    public const string SummaryText = "Where your credits came from and went, from your journals.";

    public const string HeadsetSummaryText = "Where your credits came from and went.";

    private static readonly TableColumn[] KindColumns =
    [
        new("Kind", ColumnFit.Fill),
        new("In"),
        new("Out"),
        new("Entries"),
        new("Open", ColumnFit.Fixed) { Fixed = 20 },
    ];

    private readonly Func<CommanderGameState?> _state;
    private readonly Func<HistoryState> _history;
    private readonly Func<DateTimeOffset> _now;
    private readonly CreditsChoice _choice;
    private readonly PanelNavigator _nav;
    private readonly bool _headset;
    private readonly ColumnWidthMemory? _columnWidths;
    private readonly SortHeader<CreditKindRow> _head = new("CreditsHead", CreditsModel.KindHeads());
    private readonly Segment _period = CreditsKit.PeriodSegment();
    private readonly WrapPanel _controls = new() { ItemSpacing = 16, LineSpacing = Gaps.Tile, Margin = new Thickness(0, 0, 0, 8) };
    private readonly StackPanel _notice = new();
    private readonly StackPanel _body = new();

    private CreditLedger? _seenLedger;
    private HistoryState _seenHistory;
    private long _seenMinute;
    private string _query = string.Empty;

    public CreditsPage(
        Func<CommanderGameState?> state,
        Func<HistoryState> history,
        Func<DateTimeOffset> now,
        CreditsChoice choice,
        PanelNavigator nav,
        bool headset = false,
        ColumnWidthMemory? columnWidths = null)
    {
        _state = state;
        _history = history;
        _now = now;
        _choice = choice;
        _nav = nav;
        _headset = headset;
        _columnWidths = columnWidths;

        _head.Changed += Draw;
        _period.SelectedIndex = choice.Period;
        _period.SelectionChanged += (_, _) =>
        {
            _choice.Period = _period.SelectedIndex;
            Draw();
        };

        _controls.Children.Add(_period);
        _controls.Children.Add(AllTile());

        Margin = new Thickness(14);
        Content = LoadoutPages.Scrolling(new StackPanel { Children = { _notice, _controls, _body } });
        Draw();
    }

    public string Summary => _headset ? HeadsetSummaryText : SummaryText;

    public event EventHandler? SummaryChanged
    {
        add { }
        remove { }
    }

    public bool Filters => Build() is { Running: false, Total: > 0 };

    public string FilterPlaceholder => "Search kind or event";

    public static NavCrumb AllCrumb => new(AllKey, "All transactions") { Whole = true };

    public static NavCrumb KindCrumb(CreditKind kind) =>
        new(KindPrefix + kind, CreditsModel.KindName(kind)) { Whole = true };

    /// <summary>The kind a transactions crumb is for; null for all of them or for another crumb.</summary>
    public static CreditKind? KindOf(string key) =>
        key.StartsWith(KindPrefix, StringComparison.Ordinal) && Enum.TryParse<CreditKind>(key[KindPrefix.Length..], out var kind)
            ? kind
            : null;

    public static bool IsTransactions(string key) => key == AllKey || key.StartsWith(KindPrefix, StringComparison.Ordinal);

    public void Filter(string? query)
    {
        if (CreditsKit.Retarget(query, ref _query))
        {
            Draw();
        }
    }

    /// <summary>Redraws when the ledger, the history walk, the minute or the period chosen on the other page has changed.</summary>
    public bool Tick()
    {
        if (!CreditsKit.Moved(_state()?.CreditLedger, _seenLedger, _history(), _seenHistory, _now(), _seenMinute, _choice, _period))
        {
            return false;
        }

        _period.SelectedIndex = _choice.Period;
        Draw();

        return true;
    }

    private CombatPeriod Period => CreditsKit.PeriodOf(_period);

    private CreditsView Build() =>
        CreditsModel.Rows(_state()?.CreditLedger ?? CreditLedger.None, Period, _now(), _history());

    private Button AllTile()
    {
        var label = ListRow.Name(new TextBlock { Text = "All transactions ›", VerticalAlignment = VerticalAlignment.Center });
        var tile = ListRow.Dress(new Button
        {
            Name = "CreditsAll",
            Content = label,
            MinHeight = TypeScale.MinimumTarget,
            Padding = new Thickness(16, 0),
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalContentAlignment = HorizontalAlignment.Center,
        });
        Avalonia.Automation.AutomationProperties.SetName(tile, "All transactions");
        tile.Click += (_, _) => _nav.Drill(AllCrumb);

        return tile;
    }

    private void Draw()
    {
        var now = _now();

        _seenLedger = _state()?.CreditLedger;
        _seenHistory = _history();
        _seenMinute = CreditsKit.Minute(now);

        var view = Build();

        _notice.Children.Clear();
        _body.Children.Clear();
        _controls.IsVisible = !view.Running;

        if (view.Running)
        {
            _body.Children.Add(RoutingKit.Ink(CreditsModel.RunningText, TypeScale.ControlLarge, ThemeManager.GreyKey, wrap: true));
            return;
        }

        if (view.Failed)
        {
            _notice.Children.Add(CreditsKit.Warning(view.OnRecordSince));
        }

        var kinds = CreditsModel.Matching(view.Kinds, _query);
        var moved = CreditsModel.Matching(view.Moved, _query);
        var shown = kinds.Count + moved.Count;
        var count = shown == view.Total
            ? CreditsModel.KindCount(view.Total)
            : $"{CreditsModel.Number(shown)} of {CreditsModel.KindCount(view.Total)}";

        _body.Children.Add(StarSystemPage.GroupHead(CombatStatsPage.PeriodName(Period), Description(view), count));

        if (view.Total == 0)
        {
            _body.Children.Add(CreditsKit.Message(CreditsModel.EmptyText(Period)));
            return;
        }

        _body.Children.Add(Strip(view));
        _body.Children.Add(CreditsKit.Note(CreditsModel.StripLine, 8));

        if (shown == 0)
        {
            _body.Children.Add(CreditsKit.Message(CreditsModel.NoMatchText));
            return;
        }

        _body.Children.Add(Table(kinds, moved));
    }

    private static TextBlock Description(CreditsView view)
    {
        var note = RoutingKit.Ink(string.Empty, TypeScale.ControlLarge, ThemeManager.GreyKey);

        if (view.OnRecordSince is { } since)
        {
            note.Inlines!.Add(new Run("On record since "));
            note.Inlines.Add(CreditsKit.White(since.UtcDateTime.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture)));
            note.Inlines.Add(new Run("."));
        }

        return note;
    }

    private Grid Strip(CreditsView view)
    {
        var strip = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,*,*,*,*"),
            ColumnSpacing = Gaps.Tile,
            Margin = new Thickness(0, 10, 0, 0),
        };

        var balance = view.Balance is { } held
            ? Cell("Balance", CreditsModel.Credits(held), ThemeManager.WhiteKey, view.BalanceAt is { } at ? CreditsModel.BalanceLine(at) : string.Empty)
            : Cell("Balance", CreditsKit.Dash, ThemeManager.Grey2Key, view.BalanceAt is { } when ? CreditsModel.BalanceLine(when) : string.Empty);

        Column(strip, 0, Cell("In", CreditsModel.Signed(view.In), ThemeManager.AKey, "earned"));
        Column(strip, 1, Cell("Out", CreditsModel.Signed(-view.Out), ThemeManager.AKey, "spent"));
        Column(strip, 2, Cell("Carrier bank", CreditsModel.Signed(view.CarrierBank), ThemeManager.YellowKey, CreditsModel.CarrierLine(view.CarrierBank)));
        Column(strip, 3, Cell("Unaccounted", CreditsModel.Signed(view.Unaccounted), ThemeManager.AKey, CreditsModel.GapsLine(view.Gaps)));
        Column(strip, 4, balance);

        return strip;
    }

    private static void Column(Grid strip, int column, Control cell)
    {
        Grid.SetColumn(cell, column);
        strip.Children.Add(cell);
    }

    private static Border Cell(string label, string value, string key, string line)
    {
        var cell = new Border
        {
            Name = "CreditsStrip." + label.Replace(" ", string.Empty, StringComparison.Ordinal),
            Padding = new Thickness(12, 8),
            Child = new StackPanel
            {
                Children =
                {
                    CreditsKit.Label(label),
                    CreditsKit.Mono(value, TypeScale.Small, key, right: false),
                    CreditsKit.Line(line, Fonts.ProseFamily, TypeScale.Meta, ThemeManager.GreyKey),
                },
            },
        };
        RoutingKit.Themed(cell, Border.BackgroundProperty, ThemeManager.SlabKey);

        return cell;
    }

    private StackPanel Table(IReadOnlyList<CreditKindRow> kinds, IReadOnlyList<CreditKindRow> moved)
    {
        var table = new StackPanel { Spacing = Gaps.Tile, Margin = new Thickness(0, 10, 0, 0) };
        var layout = new TableLayout(TableId, KindColumns, _columnWidths, _headset ? 12 : 16);

        if (kinds.Count > 0)
        {
            table.Children.Add(CreditsKit.HeaderRow(_head, layout, i => i == 0 ? HorizontalAlignment.Left : HorizontalAlignment.Right));

            foreach (var row in _head.Sort(kinds))
            {
                table.Children.Add(RowView(row, layout));
            }
        }

        if (moved.Count > 0)
        {
            var label = CreditsKit.Label(CreditsModel.MovedLabel);
            label.Margin = new Thickness(14, 12, 0, 4);
            table.Children.Add(label);

            foreach (var row in moved)
            {
                table.Children.Add(RowView(row, layout));
            }
        }

        return table;
    }

    private Button RowView(CreditKindRow row, TableLayout layout)
    {
        var grid = layout.Row([]);
        grid.VerticalAlignment = VerticalAlignment.Center;

        var bank = row.Kind == CreditKind.CarrierBank;
        var figure = bank ? ThemeManager.YellowKey : ThemeManager.AKey;

        CreditsKit.Place(grid, 0, NameOf(row));
        CreditsKit.Place(grid, 1, Figure(row.In, 1, figure));
        CreditsKit.Place(grid, 2, Figure(row.Out, -1, figure));
        CreditsKit.Place(grid, 3, CreditsKit.Mono(CreditsModel.Number(row.Count), TypeScale.Small, ThemeManager.AKey, right: true));
        CreditsKit.Place(grid, 4, CreditsKit.Line("›", Fonts.ChromeFamily, TypeScale.ControlLarge, ThemeManager.AKey));

        var button = ListRow.Dress(new Button
        {
            Name = "CreditsKind." + row.Kind,
            Content = grid,
            MinHeight = TypeScale.MinimumTarget,
            Padding = new Thickness(14, 6),
            HorizontalAlignment = HorizontalAlignment.Stretch,
        });
        Avalonia.Automation.AutomationProperties.SetName(button, row.Name);
        button.Click += (_, _) => _nav.Drill(KindCrumb(row.Kind));

        return button;
    }

    private static Control NameOf(CreditKindRow row)
    {
        var dock = new DockPanel { LastChildFill = false };
        var key = row.Kind == CreditKind.Unaccounted ? ThemeManager.GreyKey : ThemeManager.WhiteKey;

        dock.Children.Add(CreditsKit.Line(row.Name, Fonts.ProseFamily, TypeScale.ControlLarge, key));

        if (row.Description.Length > 0)
        {
            var tail = CreditsKit.Line(row.Description, Fonts.ProseFamily, TypeScale.Small, ThemeManager.GreyKey);
            tail.Margin = new Thickness(8, 0, 0, 0);
            tail.VerticalAlignment = VerticalAlignment.Center;
            dock.Children.Add(tail);
        }

        return dock;
    }

    private static TextBlock Figure(long? value, int sign, string key) => value is { } amount
        ? CreditsKit.Mono(CreditsModel.Signed(sign * amount), TypeScale.Small, key, right: true)
        : CreditsKit.Mono(CreditsKit.Dash, TypeScale.Small, ThemeManager.Grey2Key, right: true);
}
