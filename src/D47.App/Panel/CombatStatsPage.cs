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
    IReadOnlyList<CombatRow> Rows);

/// <summary>Commander › Ledgers › Combat stats: the kills in the journals on this PC, grouped four ways (#1288).</summary>
public sealed class CombatStatsPage : UserControl, IPageSummary, IFilterablePage
{
    public const string PageKey = "combat-stats";

    public const string SummaryText = "Your kills and the credits they were awarded, from your journals.";

    public const string CareerLine =
        "These counts come from the journals on this PC. Statistics holds Elite's own career figures, which count vouchers claimed.";

    public const string FactionLine =
        "Kills count the faction whose ship you destroyed. Credits count the faction that paid; a kill that paid several factions is split among them.";

    public const string RunningText = "D47 is reading your journals for past kills. The counts appear here when it finishes.";

    public const string FailedLabel = "History not finished";

    public const string NoMatchText = "Nothing in this period matches that search.";

    public const string BondText = "In conflict zones, ship type not recorded";

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
        ["Victim or paying", "Of their ships", "Paid by them"],
    ];

    public const string TableId = "CombatStats";

    private static readonly TableColumn[] ColumnList =
    [
        new("Name", ColumnFit.Fill),
        new("Kills"),
        new("Credits"),
    ];

    private readonly Func<CommanderGameState?> _state;
    private readonly Func<HistoryState> _history;
    private readonly Func<DateTimeOffset> _now;
    private readonly bool _headset;
    private readonly ColumnWidthMemory? _columnWidths;
    private readonly SortHeader<CombatRow>[] _heads;
    private readonly Segment _period = SortBar.Choice("CombatPeriod", Periods);
    private readonly Segment _grouping = SortBar.Choice("CombatGrouping", Groupings);
    private readonly WrapPanel _controls = new() { ItemSpacing = 16, LineSpacing = Gaps.Tile, Margin = new Thickness(0, 0, 0, 8) };
    private readonly StackPanel _notice = new();
    private readonly StackPanel _body = new();

    private CombatRecord? _seenRecord;
    private HistoryState _seenHistory;
    private long? _seenAddress;
    private long _seenMinute;
    private string _query = string.Empty;

    public CombatStatsPage(
        Func<CommanderGameState?> state,
        Func<HistoryState> history,
        Func<DateTimeOffset> now,
        bool headset = false,
        ColumnWidthMemory? columnWidths = null)
    {
        _state = state;
        _history = history;
        _now = now;
        _headset = headset;
        _columnWidths = columnWidths;

        _heads =
        [
            .. Enum.GetValues<CombatGrouping>().Select(grouping =>
            {
                var head = new SortHeader<CombatRow>("CombatStatsHead", Heads(grouping));
                head.Changed += Draw;
                return head;
            }),
        ];

        _period.SelectedIndex = (int)CombatPeriod.Week;
        _period.SelectionChanged += (_, _) => Draw();
        _grouping.SelectionChanged += (_, _) =>
        {
            Head.Show(null);
            Draw();
        };

        _period.Width = _grouping.Width = 140.0 * Periods.Length;

        _controls.Children.Add(_period);
        _controls.Children.Add(_grouping);

        Margin = new Thickness(14);
        Content = LoadoutPages.Scrolling(new StackPanel { Children = { _notice, _controls, _body } });
        Draw();
    }

    public string Summary => SummaryText;

    public event EventHandler? SummaryChanged
    {
        add { }
        remove { }
    }

    public bool Filters => Build() is { Running: false, Rows.Count: > 0 };

    public string FilterPlaceholder => "Search by name";

    private CombatPeriod Period => (CombatPeriod)Math.Clamp(_period.SelectedIndex, 0, Periods.Length - 1);

    private CombatGrouping Grouping => (CombatGrouping)Math.Clamp(_grouping.SelectedIndex, 0, Groupings.Length - 1);

    private SortHeader<CombatRow> Head => _heads[(int)Grouping];

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

    /// <summary>Redraws when the record, the history walk, the current system or the minute has changed.</summary>
    public bool Tick()
    {
        var state = _state();

        if (ReferenceEquals(state?.Combat, _seenRecord)
            && _history() == _seenHistory
            && state?.Location.SystemAddress == _seenAddress
            && Minute(_now()) == _seenMinute)
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
        + (since is { } at ? $"at {Clock(at)} UTC today" : "when D47 began counting")
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
            CombatGrouping.EnemyShip => slice.ByTarget.Select(group => EnemyRow(record, group)),
            CombatGrouping.OwnShip => slice.ByVehicle.Select(group => OwnRow(record, group)),
            CombatGrouping.System => slice.BySystem.Select(group => SystemRow(record, group)),
            _ => FactionRows(slice),
        };

        return new CombatStatsView(
            running,
            failed,
            record.OnRecordSince,
            slice.Kills,
            slice.Awarded,
            [.. rows.OrderByDescending(row => row.Kills ?? 0).ThenByDescending(row => row.Awarded ?? 0)]);
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

    /// <summary><c>1,240,000 CR</c>, or a dash for none.</summary>
    public static string Credits(long? amount) =>
        amount is { } credits ? credits.ToString("N0", CultureInfo.InvariantCulture) + " CR" : Dash;

    public static string Number(long? count) =>
        count is { } kills ? kills.ToString("N0", CultureInfo.InvariantCulture) : Dash;

    private static CombatRow EnemyRow(CombatRecord record, CombatGroup<string?> group)
    {
        if (group.Key is not { } target)
        {
            return new CombatRow(BondText, null, CombatNameStyle.Unnamed, group.Kills, group.Awarded);
        }

        return new CombatRow(
            record.TargetName(target),
            target.Contains("suitai", StringComparison.OrdinalIgnoreCase) ? "on foot" : null,
            CombatNameStyle.Named,
            group.Kills,
            group.Awarded);
    }

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

    private static IEnumerable<CombatRow> FactionRows(CombatSlice slice)
    {
        var victims = slice.ByVictimFaction.Where(group => group.Key is not null).ToList();
        var names = victims.Select(group => group.Key!)
            .Concat(slice.ByPayingFaction.Select(group => group.Key))
            .Distinct(StringComparer.OrdinalIgnoreCase);

        foreach (var name in names)
        {
            var killed = victims.FirstOrDefault(group => string.Equals(group.Key, name, StringComparison.OrdinalIgnoreCase));
            var paid = slice.ByPayingFaction.FirstOrDefault(group => string.Equals(group.Key, name, StringComparison.OrdinalIgnoreCase));

            yield return new CombatRow(name, null, CombatNameStyle.Named, killed?.Kills, paid?.Awarded);
        }
    }

    private static string Plural(long count, string unit) => $"{count.ToString("N0", CultureInfo.InvariantCulture)} {unit}{(count == 1 ? string.Empty : "s")}";

    private static string Clock(DateTimeOffset at) => at.UtcDateTime.ToString("HH:mm", CultureInfo.InvariantCulture);

    private static long Minute(DateTimeOffset now) => now.ToUnixTimeSeconds() / 60;

    private CombatStatsView Build() =>
        Rows(_state()?.Combat ?? CombatRecord.None, Period, Grouping, _now(), _history());

    private void Draw()
    {
        var state = _state();
        var now = _now();

        _seenRecord = state?.Combat;
        _seenHistory = _history();
        _seenAddress = state?.Location.SystemAddress;
        _seenMinute = Minute(now);

        var view = Build();
        var shown = Matching(view.Rows, _query);

        _notice.Children.Clear();
        _body.Children.Clear();
        _controls.IsVisible = !view.Running;

        if (view.Running)
        {
            _body.Children.Add(RoutingKit.Ink(RunningText, TypeScale.ControlLarge, ThemeManager.GreyKey, wrap: true));
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

        var count = shown.Count == view.Rows.Count
            ? CountText(Grouping, view.Rows.Count)
            : $"{shown.Count.ToString("N0", CultureInfo.InvariantCulture)} of {CountText(Grouping, view.Rows.Count)}";

        _body.Children.Add(StarSystemPage.GroupHead(PeriodName(Period), Description(view), count));
        _body.Children.Add(CreditsKit.Note(CareerLine, 6));

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

        if (Grouping == CombatGrouping.Faction)
        {
            _body.Children.Add(CreditsKit.Note(FactionLine, 6));
        }

        _body.Children.Add(Table(shown, state?.Location.SystemAddress));
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

    private StackPanel Table(IReadOnlyList<CombatRow> rows, long? current)
    {
        var table = new StackPanel { Spacing = Gaps.Tile, Margin = new Thickness(0, 10, 0, 0) };
        var head = Head;

        var layout = new TableLayout(TableId, ColumnList, _columnWidths, _headset ? 12 : 16);

        table.Children.Add(CreditsKit.HeaderRow(head, layout, i => i == 0 ? HorizontalAlignment.Left : HorizontalAlignment.Right));

        foreach (var row in head.Sort(rows))
        {
            table.Children.Add(RowView(row, current, layout));
        }

        return table;
    }

    private static Border RowView(CombatRow row, long? current, TableLayout layout)
    {
        var grid = layout.Row([]);
        grid.VerticalAlignment = VerticalAlignment.Center;

        Place(grid, 0, NameOf(row, current));
        Place(grid, 1, Figure(Number(row.Kills), row.Kills));
        Place(grid, 2, Figure(Credits(row.Awarded), row.Awarded));

        var border = new Border
        {
            MinHeight = TypeScale.MinimumTarget,
            Padding = new Thickness(14, 6),
            Child = grid,
        };
        ThemeInk.Bind(border, Border.BackgroundProperty, ThemeManager.SlabKey);

        return border;
    }

    private static Control NameOf(CombatRow row, long? current)
    {
        var dock = new DockPanel { LastChildFill = false };

        if (row.Style == CombatNameStyle.Unnamed)
        {
            dock.Children.Add(CreditsKit.Line(row.Text, Fonts.ProseFamily, TypeScale.ControlLarge, ThemeManager.GreyKey));
            return dock;
        }

        if (row.Style == CombatNameStyle.System)
        {
            var isCurrent = current is { } address && row.SystemAddress == address;
            dock.Children.Add(CreditsKit.Line(row.Text, Fonts.ChromeFamily, TypeScale.ControlLarge, isCurrent ? ThemeManager.CyanKey : ThemeManager.AKey));
            return dock;
        }

        var text = row.Text;
        var suffix = row.Suffix;

        if (suffix is null && text.EndsWith(')') && text.LastIndexOf(" (", StringComparison.Ordinal) is > 0 and var cut)
        {
            suffix = text[(cut + 1)..];
            text = text[..cut];
        }

        dock.Children.Add(CreditsKit.Line(text, Fonts.ProseFamily, TypeScale.ControlLarge, ThemeManager.WhiteKey));

        if (suffix is not null)
        {
            var tail = CreditsKit.Line(suffix, Fonts.ProseFamily, TypeScale.Small, ThemeManager.GreyKey);
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

    private static void Place(Grid grid, int column, Control cell)
    {
        cell.VerticalAlignment = VerticalAlignment.Center;
        Grid.SetColumn(cell, column);
        grid.Children.Add(cell);
    }

}
