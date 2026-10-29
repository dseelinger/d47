using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using D47.App.Controls;
using D47.App.Theming;
using D47.Core.Capabilities.Builtin;
using D47.Core.Journal;

namespace D47.App.Panel;

/// <summary>One system and faction in the BGS work table; a null figure is nothing in the period.</summary>
public sealed record BgsWorkRow(
    long SystemAddress,
    string? System,
    string Faction,
    int Count,
    long? MarksUp,
    long? MarksDown,
    long? Bounties,
    long? Bonds,
    long? Sold,
    long? Profit,
    long? Cartography,
    long? Organic,
    InfluenceAcrossResult? Influence)
{
    public string SystemText => System ?? BgsWorkPage.UnnamedSystem;

    internal long? Marks => MarksUp is null && MarksDown is null ? null : (MarksUp ?? 0) - (MarksDown ?? 0);

    internal long? Combat => Bounties is null && Bonds is null ? null : (Bounties ?? 0) + (Bonds ?? 0);

    internal long? Data => Cartography is null && Organic is null ? null : (Cartography ?? 0) + (Organic ?? 0);

    internal double? InfluenceChange => Influence is { } across ? across.After - across.Before : null;
}

/// <summary>Commander › Ledgers › BGS work: the work done for minor factions since the BGS tick, or in the last 24 hours (#1287).</summary>
public sealed class BgsWorkPage : UserControl, IPageSummary, IFilterablePage
{
    public const string PageKey = "bgs-work";

    public const string UnnamedSystem = "A system not in your recent journals";

    public const string Hint = "Missions, bounties, combat bonds, trade and data you hand in show here as you hand them in.";

    public const string UnknownTickLabel = "Tick time unknown";

    public const string UnknownTickText =
        "D47 doesn't know when the BGS last ticked, so this shows the last 24 hours. "
        + "Turn on BGS tick time under Outside sources to count from the tick and see influence.";

    private const string Dash = "—";
    private const int InfluenceColumn = 5;

    /// <summary>The columns, each sortable from its heading; Influence is drawn only with a tick time.</summary>
    public static readonly IReadOnlyList<SortColumn<BgsWorkRow>> Heads =
    [
        SortColumn<BgsWorkRow>.Text("System", row => row.System, row => row.Faction) with { Second = "Faction" },
        SortColumn<BgsWorkRow>.Figure("Missions", row => row.Marks, SortDirection.Descending) with { Second = "Up · down" },
        SortColumn<BgsWorkRow>.Figure("Combat", row => row.Combat, SortDirection.Descending) with { Second = "Bounties · bonds" },
        SortColumn<BgsWorkRow>.Figure("Trade", row => row.Profit, SortDirection.Descending) with { Second = "Sold · profit" },
        SortColumn<BgsWorkRow>.Figure("Data", row => row.Data, SortDirection.Descending) with { Second = "Cartography · organic" },
        SortColumn<BgsWorkRow>.Figure("Influence", row => row.InfluenceChange, SortDirection.Descending) with { Second = "Tick → visit" },
    ];

    public const string TableId = "BgsWork";

    private static readonly TableColumn[] ColumnList =
    [
        new("System", ColumnFit.Fill),
        new("Missions"),
        new("Combat"),
        new("Trade"),
        new("Data"),
        new("Influence"),
    ];

    private readonly Func<CommanderGameState?> _state;
    private readonly Func<DateTimeOffset?> _tick;
    private readonly Func<DateTimeOffset> _now;
    private readonly Action<string>? _openSettings;
    private readonly bool _headset;
    private readonly ColumnWidthMemory? _columnWidths;
    private readonly SortHeader<BgsWorkRow> _head = new("BgsWorkHead", Heads);
    private readonly StackPanel _body = new();

    private BgsWork? _seenWork;
    private SystemStandings? _seenStandings;
    private DateTimeOffset? _seenTick;
    private string? _seenSystem;
    private long _seenMinute;
    private string _query = string.Empty;

    public BgsWorkPage(
        Func<CommanderGameState?> state,
        Func<DateTimeOffset?> tick,
        Func<DateTimeOffset> now,
        Action<string>? openSettings = null,
        bool headset = false,
        ColumnWidthMemory? columnWidths = null)
    {
        _state = state;
        _tick = tick;
        _now = now;
        _openSettings = openSettings;
        _headset = headset;
        _columnWidths = columnWidths;

        Margin = new Thickness(14);
        Content = LoadoutPages.Scrolling(_body);
        _head.Changed += Draw;
        Draw();
    }

    public string Summary { get; private set; } = string.Empty;

    public event EventHandler? SummaryChanged;

    public bool Filters => Rows(_state()?.BgsWork ?? BgsWork.None, _tick(), _now(), SystemStandings.Empty).Count > 0;

    public string FilterPlaceholder => "Search system or faction";

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

    /// <summary>Redraws when the work, the standings, the tick, the current system or the minute has changed.</summary>
    public bool Tick()
    {
        var state = _state();

        if (ReferenceEquals(state?.BgsWork, _seenWork)
            && ReferenceEquals(state?.Standings, _seenStandings)
            && _tick() == _seenTick
            && state?.Location.StarSystem == _seenSystem
            && Minute(_now()) == _seenMinute)
        {
            return false;
        }

        Draw();
        return true;
    }

    /// <summary>The rows for the period, most entries first.</summary>
    public static IReadOnlyList<BgsWorkRow> Rows(
        BgsWork work, DateTimeOffset? tick, DateTimeOffset now, SystemStandings standings)
    {
        var from = BgsWork.PeriodStart(tick, now);

        return
        [
            .. work.Entries
                .Where(entry => entry.At >= from)
                .GroupBy(entry => (entry.SystemAddress, entry.Faction))
                .Select(group => RowOf(group.Key.SystemAddress, group.Key.Faction, [.. group], work, tick, standings))
                .OrderByDescending(row => row.Count),
        ];
    }

    /// <summary>The rows whose system or faction contains <paramref name="query"/>, ignoring case.</summary>
    public static IReadOnlyList<BgsWorkRow> Matching(IReadOnlyList<BgsWorkRow> rows, string? query) =>
        string.IsNullOrWhiteSpace(query)
            ? rows
            : [.. rows.Where(row => row.SystemText.Contains(query.Trim(), StringComparison.OrdinalIgnoreCase)
                || row.Faction.Contains(query.Trim(), StringComparison.OrdinalIgnoreCase))];

    /// <summary>An amount of credits: <c>1,284,500 CR</c>, <c>−386,400 CR</c>, or a dash for none.</summary>
    public static string Credits(long? amount) => amount is { } credits
        ? (credits < 0 ? "−" : string.Empty) + Math.Abs(credits).ToString("N0", CultureInfo.InvariantCulture) + " CR"
        : Dash;

    /// <summary>Influence marks up as <c>+6</c>, or a dash for none.</summary>
    public static string MarksUpText(long? marks) => marks is { } up ? "+" + up.ToString(CultureInfo.InvariantCulture) : Dash;

    /// <summary>Influence marks down as <c>−3</c>, or a dash for none.</summary>
    public static string MarksDownText(long? marks) => marks is { } down ? "−" + down.ToString(CultureInfo.InvariantCulture) : Dash;

    /// <summary><c>38.4% → 41.9%</c>.</summary>
    public static string InfluenceText(InfluenceAcrossResult across) => Percent(across.Before) + " → " + Percent(across.After);

    /// <summary><c>VISIT 19:42</c>, in UTC.</summary>
    public static string VisitText(InfluenceAcrossResult across) =>
        "VISIT " + across.LastSeen.UtcDateTime.ToString("HH:mm", CultureInfo.InvariantCulture);

    public static string SummaryText(DateTimeOffset? tick) => tick is null
        ? "The work you have done for minor factions in the last 24 hours."
        : "The work you have done for minor factions since the BGS tick.";

    public static string EmptyText(DateTimeOffset? tick) => tick is { } at
        ? $"No BGS work since the tick at {Clock(at)} UTC."
        : "No BGS work in the last 24 hours.";

    public static string NoMatchText(DateTimeOffset? tick) => tick is null
        ? "No system or faction in the last 24 hours matches that search."
        : "No system or faction since the tick matches that search.";

    /// <summary>"3 hours 28 minutes ago".</summary>
    public static string Ago(TimeSpan age)
    {
        var minutes = (long)Math.Max(0, Math.Floor(age.TotalMinutes));

        if (minutes < 1)
        {
            return "less than a minute ago";
        }

        var hours = minutes / 60;
        var rest = minutes % 60;
        var parts = new List<string>();

        if (hours > 0)
        {
            parts.Add(Plural(hours, "hour"));
        }

        if (rest > 0)
        {
            parts.Add(Plural(rest, "minute"));
        }

        return string.Join(' ', parts) + " ago";
    }

    private static BgsWorkRow RowOf(
        long address,
        string faction,
        IReadOnlyList<BgsEntry> entries,
        BgsWork work,
        DateTimeOffset? tick,
        SystemStandings standings)
    {
        var name = work.NameOf(address);
        var up = entries.Where(entry => entry is { Kind: BgsWorkKind.Mission, Amount: > 0 }).Sum(entry => entry.Amount);
        var down = -entries.Where(entry => entry is { Kind: BgsWorkKind.Mission, Amount: < 0 }).Sum(entry => entry.Amount);
        var trade = entries.Where(entry => entry.Kind == BgsWorkKind.Trade).ToList();

        return new BgsWorkRow(
            address,
            name,
            faction,
            entries.Count,
            up > 0 ? up : null,
            down > 0 ? down : null,
            Total(entries, BgsWorkKind.Bounty),
            Total(entries, BgsWorkKind.CombatBond),
            Total(entries, BgsWorkKind.Trade),
            trade.Count > 0 ? trade.Sum(entry => entry.Profit) : null,
            Total(entries, BgsWorkKind.Cartography),
            Total(entries, BgsWorkKind.Organic),
            tick is { } boundary && name is not null ? standings.InfluenceAcross(name, faction, boundary) : null);
    }

    private static long? Total(IReadOnlyList<BgsEntry> entries, BgsWorkKind kind)
    {
        var ofKind = entries.Where(entry => entry.Kind == kind).ToList();

        return ofKind.Count > 0 ? ofKind.Sum(entry => entry.Amount) : null;
    }

    private static string Percent(double share) => (share * 100).ToString("0.0", CultureInfo.InvariantCulture) + "%";

    private static string Clock(DateTimeOffset at) => at.UtcDateTime.ToString("HH:mm", CultureInfo.InvariantCulture);

    private static string Plural(long count, string unit) => $"{count} {unit}{(count == 1 ? string.Empty : "s")}";

    private static long Minute(DateTimeOffset now) => now.ToUnixTimeSeconds() / 60;

    private void Draw()
    {
        var state = _state();
        var now = _now();
        var tick = _tick();
        var work = state?.BgsWork ?? BgsWork.None;
        var standings = state?.Standings ?? SystemStandings.Empty;

        _seenWork = state?.BgsWork;
        _seenStandings = state?.Standings;
        _seenTick = tick;
        _seenSystem = state?.Location.StarSystem;
        _seenMinute = Minute(now);

        var summary = SummaryText(tick);

        if (summary != Summary)
        {
            Summary = summary;
            SummaryChanged?.Invoke(this, EventArgs.Empty);
        }

        if (tick is null && _head.Order?.Column == InfluenceColumn)
        {
            _head.Show(null);
        }

        var all = Rows(work, tick, now, standings);
        var shown = Matching(all, _query);

        _body.Children.Clear();

        if (tick is null)
        {
            _body.Children.Add(UnknownTick());
        }

        var count = all.Count == shown.Count
            ? Plural(all.Count, "faction")
            : $"{shown.Count} of {Plural(all.Count, "faction")}";

        _body.Children.Add(StarSystemPage.GroupHead(
            tick is null ? "Last 24 hours" : "Since the tick",
            Description(tick, now),
            count));

        if (all.Count == 0)
        {
            _body.Children.Add(CreditsKit.Message(EmptyText(tick)));
            _body.Children.Add(CreditsKit.Note(Hint, 18));
            return;
        }

        if (shown.Count == 0)
        {
            _body.Children.Add(CreditsKit.Message(NoMatchText(tick)));
            return;
        }

        _body.Children.Add(Table(shown, tick is not null, state?.Location.StarSystem));
    }

    private TextBlock Description(DateTimeOffset? tick, DateTimeOffset now)
    {
        var note = RoutingKit.Ink(string.Empty, TypeScale.ControlLarge, ThemeManager.GreyKey);
        var sort = " Press a heading to sort.";

        if (tick is { } at)
        {
            note.Inlines!.Add(Fonts.Mono($"{Clock(at)} UTC"));
            note.Inlines.Add(new Avalonia.Controls.Documents.Run($" · {Ago(now - at)}.{sort}"));
        }
        else
        {
            note.Text = $"Since {Clock(BgsWork.PeriodStart(null, now))} UTC yesterday.{sort}";
        }

        return note;
    }

    private Notice UnknownTick()
    {
        var notice = new Notice(NoticeLevel.Warning)
        {
            Label = UnknownTickLabel,
            Text = UnknownTickText,
            Margin = new Thickness(0, 0, 0, 14),
        };

        if (_openSettings is { } open)
        {
            var tile = new Button { Content = "OUTSIDE SOURCES", Height = TypeScale.MinimumTarget };
            tile.Click += (_, _) => open(GalaxyCapability.Id);
            notice.Actions.Add(tile);
        }

        return notice;
    }

    private StackPanel Table(IReadOnlyList<BgsWorkRow> rows, bool withInfluence, string? current)
    {
        var columns = withInfluence ? Heads.Count : Heads.Count - 1;
        var table = new StackPanel { Spacing = Gaps.Tile, Margin = new Thickness(0, 10, 0, 0) };

        var layout = new TableLayout(TableId, [.. ColumnList.Take(columns)], _columnWidths, _headset ? 12 : 16);

        table.Children.Add(CreditsKit.HeaderRow(_head, layout, i => i == 0 ? HorizontalAlignment.Left : HorizontalAlignment.Right));

        foreach (var row in _head.Sort(rows))
        {
            table.Children.Add(RowView(row, withInfluence, current, layout));
        }

        return table;
    }

    private static Border RowView(BgsWorkRow row, bool withInfluence, string? current, TableLayout layout)
    {
        var grid = layout.Row([]);
        grid.VerticalAlignment = VerticalAlignment.Center;

        var isCurrent = row.System is not null && string.Equals(row.System, current, StringComparison.OrdinalIgnoreCase);
        var system = row.System is null
            ? CreditsKit.Line(UnnamedSystem, Fonts.ProseFamily, TypeScale.ControlLarge, ThemeManager.GreyKey)
            : CreditsKit.Line(row.System, Fonts.ChromeFamily, TypeScale.ControlLarge, isCurrent ? ThemeManager.CyanKey : ThemeManager.AKey);
        var faction = CreditsKit.Line(row.Faction, Fonts.ProseFamily, TypeScale.ControlLarge, ThemeManager.WhiteKey);

        Place(grid, 0, new StackPanel { Children = { system, faction } });
        Place(grid, 1, Values((MarksUpText(row.MarksUp), Key(row.MarksUp)), (MarksDownText(row.MarksDown), row.MarksDown is null ? ThemeManager.Grey2Key : ThemeManager.RedKey)));
        Place(grid, 2, Values((Credits(row.Bounties), Key(row.Bounties)), (Credits(row.Bonds), Key(row.Bonds))));
        Place(grid, 3, Values((Credits(row.Sold), Key(row.Sold)), (Credits(row.Profit), row.Profit switch { null => ThemeManager.Grey2Key, < 0 => ThemeManager.RedKey, _ => ThemeManager.AKey })));
        Place(grid, 4, Values((Credits(row.Cartography), Key(row.Cartography)), (Credits(row.Organic), Key(row.Organic))));

        if (withInfluence)
        {
            Place(
                grid,
                InfluenceColumn,
                row.Influence is { } across
                    ? Values((InfluenceText(across), ThemeManager.AKey), (VisitText(across), ThemeManager.AKey))
                    : Values((Dash, ThemeManager.Grey2Key)));
        }

        var border = new Border
        {
            MinHeight = TypeScale.MinimumTarget,
            Padding = new Thickness(14, 6),
            Child = grid,
        };
        RoutingKit.Themed(border, Border.BackgroundProperty, ThemeManager.SlabKey);

        return border;
    }

    private static string Key(long? value) => value is null ? ThemeManager.Grey2Key : ThemeManager.AKey;

    private static void Place(Grid grid, int column, Control cell)
    {
        cell.VerticalAlignment = VerticalAlignment.Center;
        Grid.SetColumn(cell, column);
        grid.Children.Add(cell);
    }

    private static StackPanel Values(params (string Text, string Key)[] lines)
    {
        var stack = new StackPanel { HorizontalAlignment = HorizontalAlignment.Right };

        foreach (var (text, key) in lines)
        {
            var block = new TextBlock
            {
                Text = text,
                FontFamily = new FontFamily(Fonts.MonoFamily),
                FontSize = TypeScale.Small,
                TextAlignment = TextAlignment.Right,
                HorizontalAlignment = HorizontalAlignment.Right,
            };
            RoutingKit.Themed(block, TextBlock.ForegroundProperty, key);
            stack.Children.Add(block);
        }

        return stack;
    }

}
