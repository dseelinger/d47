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

    private readonly Func<CommanderGameState?> _state;
    private readonly Func<HistoryState> _history;
    private readonly Func<DateTimeOffset> _now;
    private readonly CreditsChoice _choice;
    private readonly CreditKind? _kind;
    private readonly bool _headset;
    private readonly ArrangedTable<CreditTxRow> _columns;
    private readonly ArrangedList _table;
    private readonly Segment _period = CreditsKit.PeriodSegment();
    private readonly WrapPanel _controls = new() { ItemSpacing = 16, LineSpacing = Gaps.Tile, Margin = new Thickness(0, 0, 0, 8) };
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
        ColumnWidthMemory? columnWidths = null,
        ColumnArrangementMemory? columnArrangements = null)
    {
        _state = state;
        _history = history;
        _now = now;
        _choice = choice;
        _kind = kind;
        _headset = headset;
        _columns = new ArrangedTable<CreditTxRow>(
            TableId, Fields(), columnArrangements, headset ? null : columnWidths, headset ? 12 : 16, headName: "CreditsTxHead");
        _table = new ArrangedList(headset, "CreditsRows", item => item is CreditTxRow row ? RowView(row) : null);
        _columns.Changed += () =>
        {
            _columns.Refresh();
            Draw();
        };
        _period.SelectedIndex = choice.Period;
        _period.SelectionChanged += (_, _) =>
        {
            _choice.Period = _period.SelectedIndex;
            Draw();
        };

        _controls.Children.Add(_period);

        Margin = new Thickness(14);
        Content = _table.View;
        Draw();
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _columns.Reopen();
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
        if (PageKit.Retarget(query, ref _query))
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

    private CreditTxView Build() => CreditsModel.Transactions(
        _state()?.CreditLedger ?? CreditLedger.None, Period, _kind, _now(), _history(), _query);

    private void Draw()
    {
        var now = _now();

        _seenLedger = _state()?.CreditLedger;
        _seenHistory = _history();
        _seenMinute = PageKit.Minute(now);

        var view = Build();

        _table.Top.Children.Clear();
        _controls.IsVisible = !view.Running;

        if (view.Running)
        {
            _table.Top.Children.Add(RoutingKit.Ink(CreditsModel.RunningText, TypeScale.ControlLarge, ThemeManager.GreyKey, wrap: true));
            Rows([]);
            return;
        }

        if (view.Failed)
        {
            _table.Top.Children.Add(CreditsKit.Warning(view.OnRecordSince));
        }

        _table.Top.Children.Add(_controls);

        var title = _kind is { } kind
            ? CombatStatsPage.PeriodName(Period) + " · " + CreditsModel.KindName(kind)
            : CombatStatsPage.PeriodName(Period);
        var count = view.Shown == view.Entries
            ? CreditsModel.EntryCount(view.Entries)
            : $"{CreditsModel.Number(view.Shown)} of {CreditsModel.EntryCount(view.Entries)}";

        _table.Top.Children.Add(StarSystemPage.GroupHead(title, Description(view), count));

        if (view.Entries == 0)
        {
            _table.Top.Children.Add(CreditsKit.Message(
                _kind is { } empty ? CreditsModel.EmptyKindText(empty, Period) : CreditsModel.EmptyText(Period)));
            Rows([]);
            return;
        }

        if (view.Shown == 0)
        {
            _table.Top.Children.Add(CreditsKit.Message(CreditsModel.NoMatchText));
            Rows([]);
            return;
        }

        if (view.HasGap)
        {
            _table.Top.Children.Add(CreditsKit.Note(CreditsModel.GapLine, 6));
        }

        if (view.HasNoBalance)
        {
            _table.Top.Children.Add(CreditsKit.Note(CreditsModel.NoBalanceLine, 6));
        }

        _table.Top.Children.Add(LedgerTables.Arranger(_columns, view.Rows));

        if (ColumnFilter.Empty(_columns, view.Rows) is { } hidden)
        {
            _table.Top.Children.Add(hidden);
            Rows([]);
            return;
        }

        var shown = _columns.Shown(view.Rows);
        IReadOnlyList<CreditTxRow> rows = view.Fold is { } fold ? [.. shown, fold] : shown;

        Rows(rows);
    }

    /// <summary>Shows <paramref name="rows"/>, leaving the list and its scroll position alone when they are the rows already shown.</summary>
    private void Rows(IReadOnlyList<CreditTxRow> rows) =>
        _table.Show(_columns, [.. rows.Cast<object>()], new Thickness(LedgerTables.RowInset, 10, LedgerTables.RowInset, 0));

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

    /// <summary>"2026-10-17 19:32", or "10-17 19:32" on the big panel; a folded row gives its first and last dates.</summary>
    public string TimeText(CreditTxRow row)
    {
        var format = _headset ? "MM-dd HH:mm" : "yyyy-MM-dd HH:mm";

        return row.From is { } from
            ? $"{from.UtcDateTime.ToString("MM-dd", CultureInfo.InvariantCulture)} to {row.At.UtcDateTime.ToString("MM-dd", CultureInfo.InvariantCulture)}"
            : row.At.UtcDateTime.ToString(format, CultureInfo.InvariantCulture);
    }

    private IReadOnlyList<TableField<CreditTxRow>> Fields()
    {
        var heads = CreditsModel.TxHeads();

        return
        [
            StarSystemPage.Headed(heads[0], new TableColumn("Time"), row => CreditsKit.Mono(TimeText(row), TypeScale.Small, ThemeManager.GreyKey, right: false)),
            StarSystemPage.Headed(heads[1], new TableColumn("Kind", ColumnFit.Fill), KindCell)
                with
                {
                    Filters =
                    [
                        ValueLists.One<CreditTxRow>("Kind", row => CreditsModel.KindName(row.Kind), "No kind"),
                        ValueLists.One<CreditTxRow>("Event", row => row.Event, "No event"),
                    ],
                },
            StarSystemPage.Headed(
                heads[2],
                new TableColumn("Amount"),
                row => CreditsKit.Mono(
                    CreditsModel.Signed(row.Amount),
                    TypeScale.Small,
                    row.Kind == CreditKind.CarrierBank ? ThemeManager.YellowKey : ThemeManager.AKey,
                    right: true),
                right: true),
            StarSystemPage.Headed(
                heads[3],
                new TableColumn("Balance"),
                row => row.Balance is { } balance
                    ? CreditsKit.Mono(CreditsModel.Credits(balance), TypeScale.Small, ThemeManager.WhiteKey, right: true)
                    : CreditsKit.Mono(CreditsKit.Dash, TypeScale.Small, ThemeManager.Grey2Key, right: true),
                right: true),
        ];
    }

    private Border RowView(CreditTxRow row)
    {
        var grid = _columns.Row(row);
        grid.VerticalAlignment = VerticalAlignment.Center;

        var border = new Border
        {
            MinHeight = TypeScale.MinimumTarget,
            Padding = new Thickness(14, 6),
            Margin = new Thickness(0, 0, 0, Gaps.Tile),
            Child = grid,
        };
        ThemeInk.Bind(border, Border.BackgroundProperty, ThemeManager.SlabKey);

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
