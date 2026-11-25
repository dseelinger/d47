using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Layout;
using Avalonia.Media;
using D47.App.Controls;
using D47.App.Theming;
using D47.Core.Journal;

namespace D47.App.Panel;

/// <summary>The span of the journals the combat stats cover.</summary>
public enum CombatPeriod
{
    Day,
    Week,
    Month,
    Everything,
}

/// <summary>What the combat stats rows are grouped by.</summary>
public enum CombatGrouping
{
    EnemyShip,
    OwnShip,
    System,
    Faction,
}

/// <summary>How a row's name is drawn.</summary>
public enum CombatNameStyle
{
    Named,
    Unnamed,
    System,
}

/// <summary>One row of the combat stats table; a null figure is absent from the grouping's list.</summary>
public sealed record CombatRow(
    string Text,
    string? Suffix,
    CombatNameStyle Style,
    long? Kills,
    long? Awarded,
    long? SystemAddress = null)
{
    /// <summary>The name the heading sorts by; null for an entry the record cannot name.</summary>
    internal string? SortName => Style == CombatNameStyle.Unnamed ? null : Text;
}

/// <summary>What the page draws for one period, grouping and history state.</summary>
public sealed record CombatStatsView(
    bool Running,
    bool Failed,
    DateTimeOffset? OnRecordSince,
    int Kills,
    long Awarded,
    IReadOnlyList<CombatRow> Rows,
    IReadOnlyList<CombatRow>? PaidBy = null);

/// <summary>Commander › Ledgers › Combat stats: the kills in the journals on this PC, grouped four ways (#1288).</summary>
public sealed class CombatStatsPage : UserControl, IPageSummary, IFilterablePage
{
    public const string PageKey = "combat-stats";

    public const string SummaryText = "Your kills and the credits they were awarded, from your journals.";

    public const string CareerLine =
        "These counts come from the journals on this PC. Statistics holds Elite's own career figures, which count vouchers claimed.";

    public const string AgainstName = "Against";

    public const string AgainstText = "The faction whose ships you destroyed, and what those kills were awarded.";

    public const string PaidByName = "Paid by";

    public const string PaidByText = "The faction that paid you. A kill paid by several factions is split among them.";

    public const string NoMatchInTableText = "No faction in this table matches that search.";

    public const string AgainstEmptyText = "Your journals don't record whose ships these kills were.";

    public const string PaidByEmptyText = "No faction paid for these kills.";

    public const string RunningText = "D47 is reading your journals for past kills. The counts appear here when it finishes.";

    public const string FailedLabel = "History not finished";

    public const string NoMatchText = "Nothing in this period matches that search.";

    public const string BondText = "In space conflict zones, ship type not recorded";

    public const string OnFootBondText = "On foot in ground conflict zones, enemy not recorded";

    public const string SrvBondText = "From an SRV in ground conflict zones, enemy not recorded";

    public const string UnnamedShip = "A ship not in your journals";

    public const string UnnamedSystem = "A system not in your journals";

    private const string Dash = "—";

    private static readonly string[] Periods = ["24 hours", "7 days", "30 days", "Everything"];

    private static readonly string[] Groupings = ["Enemy ship", "Own ship", "System", "Faction"];

    private static readonly string[][] Seconds =
    [
        ["Ship or on foot", "Destroyed", "Awarded"],
        ["Ship, on foot or SRV", "Destroyed", "Awarded"],
        ["Where the kill was", "Destroyed", "Awarded"],
        ["Whose ship it was", "Destroyed", "Awarded"],
    ];

    public const string TableId = "CombatStats";

    public const string PaidTableId = "CombatStatsPaid";

    private const double CreditsWidth = 200;

    private readonly Func<CommanderGameState?> _state;
    private readonly Func<HistoryState> _history;
    private readonly Func<DateTimeOffset> _now;
    private readonly bool _headset;
    private readonly Func<string, Task<bool>>? _copy;
    private readonly ArrangedTable<CombatRow>[] _tables;
    private readonly ArrangedTable<CombatRow> _paidTable;
    private readonly Segment _period = SortBar.Choice("CombatPeriod", Periods);
    private readonly Segment _grouping = SortBar.Choice("CombatGrouping", Groupings);
    private readonly WrapPanel _controls = new() { ItemSpacing = 16, LineSpacing = Gaps.Tile, Margin = new Thickness(0, 0, 0, 8) };
    private readonly StackPanel _notice = new();
    private readonly StackPanel _body = new();

    private CombatRecord? _seenRecord;
    private HistoryState _seenHistory;
    private long? _seenAddress;
    private long _seenMinute;
    private long? _current;
    private string _query = string.Empty;

    public CombatStatsPage(
        Func<CommanderGameState?> state,
        Func<HistoryState> history,
        Func<DateTimeOffset> now,
        bool headset = false,
        ColumnWidthMemory? columnWidths = null,
        Func<string, Task<bool>>? copy = null,
        ColumnArrangementMemory? columnArrangements = null)
    {
        _copy = copy;
        _state = state;
        _history = history;
        _now = now;
        _headset = headset;

        var widths = headset ? null : columnWidths;
        var spacing = headset ? 12 : 16;

        _tables =
        [
            .. Enum.GetValues<CombatGrouping>().Select(grouping =>
            {
                var table = new ArrangedTable<CombatRow>(TableId, Fields(Heads(grouping), NameValues(grouping)), columnArrangements, widths, spacing);
                table.Changed += Draw;
                return table;
            }),
        ];

        _paidTable = new ArrangedTable<CombatRow>(PaidTableId, Fields(PaidHeads()), columnArrangements, widths, spacing);
        _paidTable.Changed += Draw;

        _period.SelectedIndex = (int)CombatPeriod.Week;
        _period.SelectionChanged += (_, _) => Draw();
        _grouping.SelectionChanged += (_, _) =>
        {
            Head.ShowSort(null, SortDirection.Ascending);
            _paidTable.ShowSort(null, SortDirection.Ascending);
            Draw();
        };

        _period.Width = _grouping.Width = 140.0 * Periods.Length;

        _controls.Children.Add(_period);
        _controls.Children.Add(_grouping);

        Margin = new Thickness(14);
        Content = LoadoutPages.Scrolling(new StackPanel { Children = { _notice, _controls, _body } });
        Draw();
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);

        foreach (var table in _tables)
        {
            table.Reopen();
        }

        _paidTable.Reopen();
    }

    public string Summary => SummaryText;

    public event EventHandler? SummaryChanged
    {
        add { }
        remove { }
    }

    public bool Filters => Build() is { Running: false } view && (view.Rows.Count > 0 || view.PaidBy is { Count: > 0 });

    public string FilterPlaceholder => "Search by name";

    private CombatPeriod Period => (CombatPeriod)Math.Clamp(_period.SelectedIndex, 0, Periods.Length - 1);

    private CombatGrouping Grouping => (CombatGrouping)Math.Clamp(_grouping.SelectedIndex, 0, Groupings.Length - 1);

    private ArrangedTable<CombatRow> Head => _tables[(int)Grouping];

    public void Filter(string? query)
    {
        if (PageKit.Retarget(query, ref _query))
        {
            Draw();
        }
    }

    /// <summary>Redraws when the record, the history walk, the current system or the minute has changed.</summary>
    public bool Tick()
    {
        var state = _state();

        if (ReferenceEquals(state?.Combat, _seenRecord)
            && _history() == _seenHistory
            && state?.Location.SystemAddress == _seenAddress
            && PageKit.Minute(_now()) == _seenMinute)
        {
            return false;
        }

        Draw();
        return true;
    }

    /// <summary>The start of <paramref name="period"/> before <paramref name="now"/>; null for everything.</summary>
    public static DateTimeOffset? PeriodStart(CombatPeriod period, DateTimeOffset now) => period switch
    {
        CombatPeriod.Day => now - TimeSpan.FromHours(24),
        CombatPeriod.Week => now - TimeSpan.FromDays(7),
        CombatPeriod.Month => now - TimeSpan.FromDays(30),
        _ => null,
    };

    /// <summary>"Last 7 days", "Everything".</summary>
    public static string PeriodName(CombatPeriod period) => period switch
    {
        CombatPeriod.Day => "Last 24 hours",
        CombatPeriod.Week => "Last 7 days",
        CombatPeriod.Month => "Last 30 days",
        _ => "Everything",
    };

    public static string EmptyText(CombatPeriod period) => period switch
    {
        CombatPeriod.Day => "No kills in the last 24 hours.",
        CombatPeriod.Week => "No kills in the last 7 days.",
        CombatPeriod.Month => "No kills in the last 30 days.",
        _ => "No kills on record.",
    };

    public static string FailedText(DateTimeOffset? since) =>
        "D47 couldn't finish reading your older journals, so these counts start "
        + (since is { } at ? $"at {PageKit.Clock(at)} UTC today" : "when D47 began counting")
        + ". Kills from here on are still counted.";

    /// <summary>"35 targets", "1 faction".</summary>
    public static string CountText(CombatGrouping grouping, int count) => grouping switch
    {
        CombatGrouping.EnemyShip => Plural(count, "target"),
        CombatGrouping.OwnShip => Plural(count, "ship"),
        CombatGrouping.System => Plural(count, "system"),
        _ => Plural(count, "faction"),
    };

    /// <summary>The rows for the period and grouping, most kills then most credits first.</summary>
    public static CombatStatsView Rows(
        CombatRecord record,
        CombatPeriod period,
        CombatGrouping grouping,
        DateTimeOffset now,
        HistoryState history)
    {
        var running = history is HistoryState.Pending or HistoryState.Running;
        var failed = history is HistoryState.Failed or HistoryState.Stopped;
        var slice = record.Since(PeriodStart(period, now));

        IEnumerable<CombatRow> rows = grouping switch
        {
            CombatGrouping.EnemyShip =>
            [
                .. slice.ByTarget.Select(group => EnemyRow(record, group)),
                .. slice.ByBondVehicle.Select(BondRow),
            ],
            CombatGrouping.OwnShip => slice.ByVehicle.Select(group => OwnRow(record, group)),
            CombatGrouping.System => slice.BySystem.Select(group => SystemRow(record, group)),
            _ => AgainstRows(slice),
        };

        var paid = grouping == CombatGrouping.Faction ? PaidRows(slice) : null;

        return new CombatStatsView(
            running,
            failed,
            record.OnRecordSince,
            slice.Kills,
            slice.Awarded,
            [.. rows.OrderByDescending(row => row.Kills ?? 0).ThenByDescending(row => row.Awarded ?? 0)],
            paid);
    }

    /// <summary>The rows whose shown name contains <paramref name="query"/>, ignoring case.</summary>
    public static IReadOnlyList<CombatRow> Matching(IReadOnlyList<CombatRow> rows, string? query) =>
        string.IsNullOrWhiteSpace(query)
            ? rows
            : [.. rows.Where(row => row.Text.Contains(query.Trim(), StringComparison.OrdinalIgnoreCase))];

    /// <summary>The columns for a grouping, each sortable from its heading.</summary>
    public static IReadOnlyList<SortColumn<CombatRow>> Heads(CombatGrouping grouping)
    {
        var second = Seconds[(int)grouping];

        return
        [
            SortColumn<CombatRow>.Text(Groupings[(int)grouping], row => row.SortName) with { Second = second[0] },
            SortColumn<CombatRow>.Figure("Kills", row => row.Kills, SortDirection.Descending) with { Second = second[1] },
            SortColumn<CombatRow>.Figure("Credits", row => row.Awarded, SortDirection.Descending) with { Second = second[2] },
        ];
    }

    /// <summary>The columns of the Paid by table.</summary>
    public static IReadOnlyList<SortColumn<CombatRow>> PaidHeads() =>
    [
        SortColumn<CombatRow>.Text(Groupings[(int)CombatGrouping.Faction], row => row.SortName) with { Second = "Who paid" },
        SortColumn<CombatRow>.Figure("Credits", row => row.Awarded, SortDirection.Descending) with { Second = "Paid" },
    ];

    /// <summary><c>1,240,000 CR</c>, or a dash for none.</summary>
    public static string Credits(long? amount) =>
        amount is { } credits ? credits.ToString("N0", CultureInfo.InvariantCulture) + " CR" : Dash;

    public static string Number(long? count) =>
        count is { } kills ? kills.ToString("N0", CultureInfo.InvariantCulture) : Dash;

    private static CombatRow EnemyRow(CombatRecord record, CombatGroup<string> group) =>
        new(
            record.TargetName(group.Key),
            group.Key.Contains("suitai", StringComparison.OrdinalIgnoreCase) ? "on foot" : null,
            CombatNameStyle.Named,
            group.Kills,
            group.Awarded);

    private static CombatRow BondRow(CombatGroup<CombatVehicle> group) =>
        new(
            group.Key switch
            {
                CombatVehicle.OnFoot => OnFootBondText,
                CombatVehicle.Srv => SrvBondText,
                _ => BondText,
            },
            null,
            CombatNameStyle.Unnamed,
            group.Kills,
            group.Awarded);

    private static CombatRow OwnRow(CombatRecord record, CombatGroup<OwnVehicle> group)
    {
        var vehicle = group.Key;

        return vehicle.Vehicle switch
        {
            CombatVehicle.OnFoot => new CombatRow("On foot", null, CombatNameStyle.Named, group.Kills, group.Awarded),
            CombatVehicle.Srv => new CombatRow("In an SRV", null, CombatNameStyle.Named, group.Kills, group.Awarded),
            _ => vehicle.ShipId is { } id && record.ShipName(id) is { } name
                ? new CombatRow(name, null, CombatNameStyle.Named, group.Kills, group.Awarded)
                : new CombatRow(UnnamedShip, null, CombatNameStyle.Unnamed, group.Kills, group.Awarded),
        };
    }

    private static CombatRow SystemRow(CombatRecord record, CombatGroup<long?> group) =>
        group.Key is { } address && record.SystemName(address) is { } name
            ? new CombatRow(name, null, CombatNameStyle.System, group.Kills, group.Awarded, address)
            : new CombatRow(UnnamedSystem, null, CombatNameStyle.Unnamed, group.Kills, group.Awarded, group.Key);

    private static IEnumerable<CombatRow> AgainstRows(CombatSlice slice) =>
        slice.ByVictimFaction
            .Where(group => group.Key is not null)
            .Select(group => new CombatRow(group.Key!, null, CombatNameStyle.Named, group.Kills, group.Awarded));

    private static IReadOnlyList<CombatRow> PaidRows(CombatSlice slice) =>
    [
        .. slice.ByPayingFaction
            .Select(group => new CombatRow(group.Key, null, CombatNameStyle.Named, null, group.Awarded))
            .OrderByDescending(row => row.Awarded ?? 0),
    ];

    private static string Plural(long count, string unit) => $"{count.ToString("N0", CultureInfo.InvariantCulture)} {unit}{(count == 1 ? string.Empty : "s")}";



    private CombatStatsView Build() =>
        Rows(_state()?.Combat ?? CombatRecord.None, Period, Grouping, _now(), _history());

    private void Draw()
    {
        var state = _state();
        var now = _now();

        _seenRecord = state?.Combat;
        _seenHistory = _history();
        _seenAddress = state?.Location.SystemAddress;
        _seenMinute = PageKit.Minute(now);
        _current = state?.Location.SystemAddress;

        var view = Build();
        var shown = Matching(view.Rows, _query);

        _notice.Children.Clear();
        _body.Children.Clear();
        _controls.IsVisible = !view.Running;

        if (view.Running)
        {
            _body.Children.Add(new WaitingState(WaitForm.Body, RunningText));
            return;
        }

        if (view.Failed)
        {
            _notice.Children.Add(new Notice(NoticeLevel.Warning)
            {
                Label = FailedLabel,
                Text = FailedText(view.OnRecordSince),
                Margin = new Thickness(0, 0, 0, 14),
            });
        }

        var split = Grouping == CombatGrouping.Faction;

        _body.Children.Add(StarSystemPage.GroupHead(
            PeriodName(Period),
            Description(view),
            split ? null : Counted(shown.Count, view.Rows.Count)));
        _body.Children.Add(CreditsKit.Note(CareerLine, 6));

        if (split)
        {
            DrawFactions(view);
            return;
        }

        if (view.Rows.Count == 0)
        {
            _body.Children.Add(CreditsKit.Message(EmptyText(Period)));
            return;
        }

        if (shown.Count == 0)
        {
            _body.Children.Add(CreditsKit.Message(NoMatchText));
            return;
        }

        _body.Children.Add(Table(shown, Head));
    }

    private string Counted(int shown, int total) =>
        shown == total
            ? CountText(Grouping, total)
            : $"{shown.ToString("N0", CultureInfo.InvariantCulture)} of {CountText(Grouping, total)}";

    private void DrawFactions(CombatStatsView view)
    {
        if (view.Kills == 0)
        {
            _body.Children.Add(CreditsKit.Message(EmptyText(Period)));
            return;
        }

        var paid = view.PaidBy ?? [];
        var against = Matching(view.Rows, _query);
        var paying = Matching(paid, _query);

        if (against.Count == 0 && paying.Count == 0 && (view.Rows.Count > 0 || paid.Count > 0))
        {
            _body.Children.Add(CreditsKit.Message(NoMatchText));
            return;
        }

        Section(AgainstName, AgainstText, view.Rows, against, AgainstEmptyText, Head);
        Section(PaidByName, PaidByText, paid, paying, PaidByEmptyText, _paidTable);
    }

    private void Section(
        string name,
        string description,
        IReadOnlyList<CombatRow> all,
        IReadOnlyList<CombatRow> shown,
        string emptyText,
        ArrangedTable<CombatRow> columns)
    {
        var group = StarSystemPage.GroupHead(name, description, Counted(shown.Count, all.Count));
        group.Margin = new Thickness(0, 26, 0, 0);
        _body.Children.Add(group);

        if (all.Count == 0)
        {
            _body.Children.Add(CreditsKit.Message(emptyText));
        }
        else if (shown.Count == 0)
        {
            _body.Children.Add(CreditsKit.Message(NoMatchInTableText));
        }
        else
        {
            _body.Children.Add(Table(shown, columns));
        }
    }

    private TextBlock Description(CombatStatsView view)
    {
        var note = RoutingKit.Ink(string.Empty, TypeScale.ControlLarge, ThemeManager.GreyKey);

        note.Inlines!.Add(White(Plural(view.Kills, "kill")));
        note.Inlines.Add(new Run(" · "));
        note.Inlines.Add(White($"{view.Awarded.ToString("N0", CultureInfo.InvariantCulture)} CR"));
        note.Inlines.Add(new Run(" awarded."));

        if (view.OnRecordSince is { } since)
        {
            note.Inlines.Add(new Run(" On record since "));
            note.Inlines.Add(White(since.UtcDateTime.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)));
            note.Inlines.Add(new Run("."));
        }

        return note;
    }

    private static Run White(string text)
    {
        var run = Fonts.Mono(text);
        ThemeInk.Bind(run, TextElement.ForegroundProperty, ThemeManager.WhiteKey);

        return run;
    }

    private StackPanel Table(IReadOnlyList<CombatRow> rows, ArrangedTable<CombatRow> columns)
    {
        columns.Refresh();

        var table = new StackPanel { Spacing = Gaps.Tile, Margin = new Thickness(0, 10, 0, 0) };
        var list = new StackPanel { Spacing = Gaps.Tile };

        table.Children.Add(LedgerTables.Arranger(columns, rows));
        list.Children.Add(LedgerTables.Head(columns, new Thickness(LedgerTables.RowInset, 0)));

        if (ColumnFilter.Empty(columns, rows) is { } empty)
        {
            list.Children.Add(empty);
        }

        foreach (var row in columns.Shown(rows))
        {
            var view = RowView(row, columns);
            view.IsVisible = !ColumnFilter.HidesRows(columns);
            list.Children.Add(view);
        }

        table.Children.Add(LedgerTables.Sideways(columns, list));

        return table;
    }

    /// <summary>The value list of the second value the name cell draws; none for a grouping whose cell draws one.</summary>
    private static IReadOnlyList<ValueList<CombatRow>> NameValues(CombatGrouping grouping) => grouping switch
    {
        CombatGrouping.EnemyShip =>
        [
            new("Ship or on foot", row => row.Style == CombatNameStyle.Unnamed ? [] : [Suffix(row) is null ? "Ship" : "On foot"], "Bond"),
        ],
        CombatGrouping.OwnShip =>
        [
            new("Ship type", row => Suffix(row) is { } suffix ? [suffix.Trim('(', ')')] : [], "No ship type"),
        ],
        _ => [],
    };

    private IReadOnlyList<TableField<CombatRow>> Fields(
        IReadOnlyList<SortColumn<CombatRow>> heads, IReadOnlyList<ValueList<CombatRow>>? nameValues = null)
    {
        var name = StarSystemPage.Headed(heads[0], new TableColumn("Name", ColumnFit.Fill), row => NameOf(row, _current, _copy))
            with { Filters = nameValues ?? [] };
        var credits = StarSystemPage.Headed(
            heads[^1],
            new TableColumn("Credits", ColumnFit.Fixed) { Fixed = CreditsWidth },
            row => Figure(Credits(row.Awarded), row.Awarded),
            right: true);

        return heads.Count == 3
            ? [name, StarSystemPage.Headed(heads[1], new TableColumn("Kills"), row => Figure(Number(row.Kills), row.Kills), right: true), credits]
            : [name, credits];
    }

    private static Border RowView(CombatRow row, ArrangedTable<CombatRow> columns)
    {
        var grid = columns.Row(row);
        grid.VerticalAlignment = VerticalAlignment.Center;

        var border = new Border
        {
            MinHeight = TypeScale.MinimumTarget,
            Padding = new Thickness(14, 6),
            Child = grid,
        };
        ThemeInk.Bind(border, Border.BackgroundProperty, ThemeManager.SlabKey);

        return border;
    }

    /// <summary>The grey value drawn after a name: the row's own suffix, or a trailing "(Type)" of its text.</summary>
    private static string? Suffix(CombatRow row) =>
        row.Suffix ?? (row.Text.EndsWith(')') && row.Text.LastIndexOf(" (", StringComparison.Ordinal) is > 0 and var cut ? row.Text[(cut + 1)..] : null);

    private static Control NameOf(CombatRow row, long? current, Func<string, Task<bool>>? copy)
    {
        var dock = new DockPanel { LastChildFill = false };

        if (row.Style == CombatNameStyle.Unnamed)
        {
            dock.Children.Add(TableCells.Wraps(CreditsKit.Line(row.Text, Fonts.ProseFamily, TypeScale.ControlLarge, ThemeManager.GreyKey)));
            return dock;
        }

        if (row.Style == CombatNameStyle.System)
        {
            var isCurrent = current is { } address && row.SystemAddress == address;
            dock.Children.Add(CopyGlyph.LinkBeside(
                TableCells.Wraps(CreditsKit.Line(row.Text, Fonts.ChromeFamily, TypeScale.ControlLarge, isCurrent ? ThemeManager.CyanKey : ThemeManager.AKey)),
                row.Text,
                copy));
            return dock;
        }

        var suffix = Suffix(row);
        var text = row.Suffix is null && suffix is not null ? row.Text[..row.Text.LastIndexOf(" (", StringComparison.Ordinal)] : row.Text;

        dock.Children.Add(TableCells.Wraps(CreditsKit.Line(text, Fonts.ProseFamily, TypeScale.ControlLarge, ThemeManager.WhiteKey)));

        if (suffix is not null)
        {
            var tail = TableCells.Wraps(CreditsKit.Line(suffix, Fonts.ProseFamily, TypeScale.Small, ThemeManager.GreyKey));
            tail.Margin = new Thickness(8, 0, 0, 0);
            tail.VerticalAlignment = VerticalAlignment.Center;
            dock.Children.Add(tail);
        }

        return dock;
    }

    private static TextBlock Figure(string text, long? value) =>
        Typeset.Mono(
            text, TypeScale.Small, value is null ? ThemeManager.Grey2Key : ThemeManager.AKey,
            horizontal: HorizontalAlignment.Right, align: TextAlignment.Right);
}
