using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Layout;
using D47.App.Controls;
using D47.App.Theming;
using D47.Core.Journal;

namespace D47.App.Panel;

/// <summary>Commander › Ledgers › Credits › a kind, or all transactions: one row per entry in the period (#1289).</summary>
public sealed class CreditTransactionsPage : UserControl, IPageSummary, IFilterablePage
{
    public const string TableId = "Credits.Transactions";

    private static readonly TableColumn[] TxColumns =
    [
        new("Time"),
        new("Kind", ColumnFit.Fill),
        new("Amount"),
        new("Balance"),
    ];

    private readonly Func<CommanderGameState?> _state;
    private readonly Func<HistoryState> _history;
    private readonly Func<DateTimeOffset> _now;
    private readonly CreditsChoice _choice;
    private readonly CreditKind? _kind;
    private readonly bool _headset;
    private readonly ColumnWidthMemory? _columnWidths;
    private TableLayout _layout;
    private readonly SortHeader<CreditTxRow> _head = new("CreditsTxHead", CreditsModel.TxHeads());
    private readonly Segment _period = CreditsKit.PeriodSegment();
    private readonly WrapPanel _controls = new() { ItemSpacing = 16, LineSpacing = Gaps.Tile, Margin = new Thickness(0, 0, 0, 8) };
    private readonly StackPanel _top = new();
    private readonly ItemsControl _list;
    private readonly ScrollViewer _scroll;

    private IReadOnlyList<CreditTxRow> _shown = [];
    private CreditLedger? _seenLedger;
    private HistoryState _seenHistory;
    private long _seenMinute;
    private string _query = string.Empty;

    public CreditTransactionsPage(
        Func<CommanderGameState?> state,
        Func<HistoryState> history,
        Func<DateTimeOffset> now,
        CreditsChoice choice,
        CreditKind? kind,
        bool headset = false,
        ColumnWidthMemory? columnWidths = null)
    {
        _state = state;
        _history = history;
        _now = now;
        _choice = choice;
        _kind = kind;
        _headset = headset;
        _columnWidths = columnWidths;
        _layout = NewLayout();

        _head.Changed += Draw;
        _period.SelectedIndex = choice.Period;
        _period.SelectionChanged += (_, _) =>
        {
            _choice.Period = _period.SelectedIndex;
            Draw();
        };

        _controls.Children.Add(_period);

        _list = new ItemsControl
        {
            Name = "CreditsRows",
            ItemsPanel = new FuncTemplate<Avalonia.Controls.Panel?>(() => new VirtualizingStackPanel()),
            ItemTemplate = new FuncDataTemplate<CreditTxRow>((row, _) => row is null ? null : RowView(row)),
        };

        _scroll = new ScrollViewer
        {
            Content = new Border { Padding = new Thickness(0, 0, 14, 0), Child = _list },
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
        };

        var grid = new Grid { RowDefinitions = new RowDefinitions("Auto,*") };
        _top.Margin = new Thickness(0, 0, 14, 0);
        grid.Children.Add(_top);
        Grid.SetRow(_scroll, 1);
        grid.Children.Add(_scroll);

        Margin = new Thickness(14);
        Content = grid;
        Draw();
    }

    public string Summary => _headset ? CreditsPage.HeadsetSummaryText : CreditsPage.SummaryText;

    public event EventHandler? SummaryChanged
    {
        add { }
        remove { }
    }

    public bool Filters => Build() is { Running: false, Entries: > 0 };

    public string FilterPlaceholder => "Search kind or event";

    public void Filter(string? query)
    {
        var trimmed = query?.Trim() ?? string.Empty;

        if (trimmed == _query)
        {
            return;
        }

        _query = trimmed;
        Draw();
    }

    /// <summary>Redraws when the ledger, the history walk, the minute or the period chosen on the other page has changed.</summary>
    public bool Tick()
    {
        var ledger = _state()?.CreditLedger;

        if (ReferenceEquals(ledger, _seenLedger)
            && _history() == _seenHistory
            && Minute(_now()) == _seenMinute
            && _choice.Period == _period.SelectedIndex)
        {
            return false;
        }

        _period.SelectedIndex = _choice.Period;
        Draw();

        return true;
    }

    private static long Minute(DateTimeOffset now) => now.ToUnixTimeSeconds() / 60;

    private CombatPeriod Period => CreditsKit.PeriodOf(_period);

    private CreditTxView Build() => CreditsModel.Transactions(
        _state()?.CreditLedger ?? CreditLedger.None, Period, _kind, _now(), _history(), _query);

    private void Draw()
    {
        var now = _now();

        _seenLedger = _state()?.CreditLedger;
        _seenHistory = _history();
        _seenMinute = Minute(now);

        var view = Build();

        _top.Children.Clear();
        _controls.IsVisible = !view.Running;

        if (view.Running)
        {
            _top.Children.Add(RoutingKit.Ink(CreditsModel.RunningText, TypeScale.ControlLarge, ThemeManager.GreyKey, wrap: true));
            Rows([]);
            return;
        }

        if (view.Failed)
        {
            _top.Children.Add(CreditsKit.Warning(view.OnRecordSince));
        }

        _top.Children.Add(_controls);

        var title = _kind is { } kind
            ? CombatStatsPage.PeriodName(Period) + " · " + CreditsModel.KindName(kind)
            : CombatStatsPage.PeriodName(Period);
        var count = view.Shown == view.Entries
            ? CreditsModel.EntryCount(view.Entries)
            : $"{CreditsModel.Number(view.Shown)} of {CreditsModel.EntryCount(view.Entries)}";

        _top.Children.Add(StarSystemPage.GroupHead(title, Description(view), count));

        if (view.Entries == 0)
        {
            _top.Children.Add(CreditsKit.Message(
                _kind is { } empty ? CreditsModel.EmptyKindText(empty, Period) : CreditsModel.EmptyText(Period)));
            Rows([]);
            return;
        }

        if (view.Shown == 0)
        {
            _top.Children.Add(CreditsKit.Message(CreditsModel.NoMatchText));
            Rows([]);
            return;
        }

        if (view.HasGap)
        {
            _top.Children.Add(CreditsKit.Note(CreditsModel.GapLine, 6));
        }

        if (view.HasNoBalance)
        {
            _top.Children.Add(CreditsKit.Note(CreditsModel.NoBalanceLine, 6));
        }

        _layout = NewLayout();

        var header = CreditsKit.HeaderRow(_head, _layout, i => i is 0 or 1 ? HorizontalAlignment.Left : HorizontalAlignment.Right);
        header.Margin = new Thickness(14, 10, 14, 0);
        _top.Children.Add(header);

        IReadOnlyList<CreditTxRow> rows = view.Fold is { } fold ? [.. _head.Sort(view.Rows), fold] : _head.Sort(view.Rows);

        Rows(rows);
    }

    /// <summary>Shows <paramref name="rows"/>, leaving the list and its scroll position alone when they are the rows already shown.</summary>
    private void Rows(IReadOnlyList<CreditTxRow> rows)
    {
        _scroll.IsVisible = rows.Count > 0;

        if (rows.Count > 0 && _shown.SequenceEqual(rows))
        {
            return;
        }

        _shown = rows;
        _list.ItemsSource = rows;
    }

    private static TextBlock Description(CreditTxView view)
    {
        var note = RoutingKit.Ink(string.Empty, TypeScale.ControlLarge, ThemeManager.GreyKey);

        note.Inlines!.Add(new Run("In "));
        note.Inlines.Add(CreditsKit.White(CreditsModel.Signed(view.In)));
        note.Inlines.Add(new Run(" · Out "));
        note.Inlines.Add(CreditsKit.White(CreditsModel.Signed(-view.Out)));
        note.Inlines.Add(new Run("."));

        if (view.OnRecordSince is { } since)
        {
            note.Inlines.Add(new Run(" On record since "));
            note.Inlines.Add(CreditsKit.White(since.UtcDateTime.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)));
            note.Inlines.Add(new Run("."));
        }

        return note;
    }

    private TableLayout NewLayout() => new(TableId, TxColumns, _columnWidths, _headset ? 12 : 16);

    /// <summary>"2026-10-17 19:32", or "10-17 19:32" on the big panel; a folded row gives its first and last dates.</summary>
    public string TimeText(CreditTxRow row)
    {
        var format = _headset ? "MM-dd HH:mm" : "yyyy-MM-dd HH:mm";

        return row.From is { } from
            ? $"{from.UtcDateTime.ToString("MM-dd", CultureInfo.InvariantCulture)} to {row.At.UtcDateTime.ToString("MM-dd", CultureInfo.InvariantCulture)}"
            : row.At.UtcDateTime.ToString(format, CultureInfo.InvariantCulture);
    }

    private Border RowView(CreditTxRow row)
    {
        var grid = _layout.Row([]);
        grid.VerticalAlignment = VerticalAlignment.Center;

        var bank = row.Kind == CreditKind.CarrierBank;

        CreditsKit.Place(grid, 0, CreditsKit.Mono(TimeText(row), TypeScale.Small, ThemeManager.GreyKey, right: false));
        CreditsKit.Place(grid, 1, KindCell(row));
        CreditsKit.Place(
            grid,
            2,
            CreditsKit.Mono(CreditsModel.Signed(row.Amount), TypeScale.Small, bank ? ThemeManager.YellowKey : ThemeManager.AKey, right: true));
        CreditsKit.Place(
            grid,
            3,
            row.Balance is { } balance
                ? CreditsKit.Mono(CreditsModel.Credits(balance), TypeScale.Small, ThemeManager.WhiteKey, right: true)
                : CreditsKit.Mono(CreditsKit.Dash, TypeScale.Small, ThemeManager.Grey2Key, right: true));

        var border = new Border
        {
            MinHeight = TypeScale.MinimumTarget,
            Padding = new Thickness(14, 6),
            Margin = new Thickness(0, 0, 0, Gaps.Tile),
            Child = grid,
        };
        RoutingKit.Themed(border, Border.BackgroundProperty, ThemeManager.SlabKey);

        return border;
    }

    private static StackPanel KindCell(CreditTxRow row)
    {
        var line = new DockPanel { LastChildFill = false };
        var key = row.Kind == CreditKind.Unaccounted ? ThemeManager.GreyKey : ThemeManager.WhiteKey;

        line.Children.Add(CreditsKit.Line(CreditsModel.KindName(row.Kind), Fonts.ProseFamily, TypeScale.ControlLarge, key));

        var name = CreditsKit.Line(row.Event, Fonts.MonoFamily, TypeScale.Meta, ThemeManager.GreyKey);
        name.Margin = new Thickness(8, 0, 0, 0);
        name.VerticalAlignment = VerticalAlignment.Center;
        line.Children.Add(name);

        var cell = new StackPanel { Children = { line } };

        if (row.Note is { } note)
        {
            var second = RoutingKit.Ink(note, TypeScale.Small, ThemeManager.GreyKey, wrap: true);
            second.Margin = new Thickness(0, 2, 0, 0);
            cell.Children.Add(second);
        }

        return cell;
    }
}
