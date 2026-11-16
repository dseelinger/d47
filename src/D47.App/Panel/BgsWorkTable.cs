using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using D47.App.Controls;
using D47.App.Theming;
using D47.Core.Journal;

namespace D47.App.Panel;

/// <summary>The rows of one group on the WORK section.</summary>
public sealed record BgsWorkGroup(string Title, IReadOnlyList<BgsWorkRow> Rows);

/// <summary>One system and faction in the WORK table; a null figure is nothing in the period.</summary>
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
    InfluenceAcrossResult? Influence)
{
    public string SystemText => System ?? BgsWorkTable.UnnamedSystem;

    internal long? Marks => MarksUp is null && MarksDown is null ? null : (MarksUp ?? 0) - (MarksDown ?? 0);

    internal long? Combat => Bounties is null && Bonds is null ? null : (Bounties ?? 0) + (Bonds ?? 0);

    internal long? Data => Cartography;

    internal double? InfluenceChange => Influence is { } across ? across.After - across.Before : null;
}

/// <summary>The rows and words of Commander › BGS › WORK: the work done for minor factions since the BGS tick, or in the last 24 hours (#1287).</summary>
public static class BgsWorkTable
{
    public const string UnnamedSystem = "A system not in your recent journals";

    public const string Hint = "Missions, bounties, combat bonds, trade and data you hand in show here as you hand them in.";

    public const string UnknownTickLabel = "Tick time unknown";

    public const string UnknownTickText =
        "D47 doesn't know when the BGS last ticked, so this shows the last 24 hours. "
        + "It counts from the tick and shows influence once the tick time has been read.";

    public const string TableId = "BgsWork";

    internal const string Dash = "—";
    internal const int InfluenceColumn = 5;

    /// <summary>The columns, each sortable from its heading; Influence is drawn only with a tick time.</summary>
    public static readonly IReadOnlyList<SortColumn<BgsWorkRow>> Heads =
    [
        SortColumn<BgsWorkRow>.Text("System", row => row.System, row => row.Faction) with { Second = "Faction" },
        SortColumn<BgsWorkRow>.Figure("Missions", row => row.Marks, SortDirection.Descending) with { Second = "Up · down" },
        SortColumn<BgsWorkRow>.Figure("Combat", row => row.Combat, SortDirection.Descending) with { Second = "Bounties · bonds" },
        SortColumn<BgsWorkRow>.Figure("Trade", row => row.Profit, SortDirection.Descending) with { Second = "Sold · profit" },
        SortColumn<BgsWorkRow>.Figure("Data", row => row.Data, SortDirection.Descending) with { Second = "Cartography" },
        SortColumn<BgsWorkRow>.Figure("Influence", row => row.InfluenceChange, SortDirection.Descending) with { Second = "Tick → visit" },
    ];

    internal static readonly TableColumn[] ColumnList =
    [
        new("System", ColumnFit.Fill),
        new("Missions"),
        new("Combat"),
        new("Trade"),
        new("Data"),
        new("Influence"),
    ];

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

    /// <summary>The rows in the three groups around <paramref name="faction"/>, in order, leaving out any group with no rows.</summary>
    public static IReadOnlyList<BgsWorkGroup> Groups(IReadOnlyList<BgsWorkRow> rows, BgsWork work, string faction)
    {
        var titles = new[] { $"For {faction}", "Other factions in its systems", "Elsewhere" };

        return
        [
            .. rows
                .GroupBy(row => work.GroupOf(row.SystemAddress, row.Faction, faction))
                .OrderBy(group => group.Key)
                .Select(group => new BgsWorkGroup(titles[(int)group.Key], [.. group])),
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
    public static string InfluenceText(InfluenceAcrossResult across) =>
        CampaignModel.InfluenceText(across.Before) + " → " + CampaignModel.InfluenceText(across.After);

    /// <summary><c>VISIT 19:42</c>, in UTC.</summary>
    public static string VisitText(InfluenceAcrossResult across) =>
        "VISIT " + across.LastSeen.UtcDateTime.ToString("HH:mm", CultureInfo.InvariantCulture);

    public static string IntroText(string faction) =>
        $"Your work for other factions, and any work for {faction} that Systems cannot show. "
        + $"Your work for {faction} in its own systems is on Systems.";

    public static string EmptyText(DateTimeOffset? tick) => tick is { } at
        ? $"No BGS work since the tick at {PageKit.Clock(at)} UTC."
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

    internal static string Plural(long count, string unit) => $"{count} {unit}{(count == 1 ? string.Empty : "s")}";

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
            tick is { } boundary && name is not null ? standings.InfluenceAcross(name, faction, boundary) : null);
    }

    private static long? Total(IReadOnlyList<BgsEntry> entries, BgsWorkKind kind)
    {
        var ofKind = entries.Where(entry => entry.Kind == kind).ToList();

        return ofKind.Count > 0 ? ofKind.Sum(entry => entry.Amount) : null;
    }
}

/// <summary>What the WORK section is drawn from.</summary>
public sealed record BgsWorkInput(
    IReadOnlyList<BgsWorkRow> Rows,
    BgsWork Work,
    string Faction,
    DateTimeOffset? Tick,
    DateTimeOffset Now,
    string? Current,
    string Query);

/// <summary>The WORK section of Commander › BGS: the work grouped around the BGS faction, one table per group.</summary>
public sealed class BgsWorkSection
{
    private readonly bool _headset;
    private readonly ColumnWidthMemory? _columnWidths;
    private readonly Func<string, Task<bool>>? _copy;

    public BgsWorkSection(bool headset, ColumnWidthMemory? columnWidths, Func<string, Task<bool>>? copy)
    {
        _headset = headset;
        _columnWidths = columnWidths;
        _copy = copy;
        Head = new SortHeader<BgsWorkRow>("BgsWorkHead", BgsWorkTable.Heads);
    }

    public SortHeader<BgsWorkRow> Head { get; }

    /// <summary>The rows matching the search, across every group; the pager counts these.</summary>
    public int Count { get; private set; }

    /// <summary>Draws the section. On the headset <paramref name="page"/> is clamped to the pages the rows fill.</summary>
    public Control Build(BgsWorkInput input, ref int page)
    {
        var tick = input.Tick;
        var body = new StackPanel();

        if (tick is null && Head.Order?.Column == BgsWorkTable.InfluenceColumn)
        {
            Head.Show(null);
        }

        var shown = BgsWorkTable.Matching(input.Rows, input.Query);
        Count = shown.Count;

        if (tick is null)
        {
            body.Children.Add(new Notice(NoticeLevel.Warning)
            {
                Label = BgsWorkTable.UnknownTickLabel,
                Text = BgsWorkTable.UnknownTickText,
                Margin = new Thickness(0, 0, 0, 14),
            });
        }

        var intro = Typeset.Prose(BgsWorkTable.IntroText(input.Faction), TypeScale.Description, ThemeManager.GreyKey, wrap: true);
        intro.MaxWidth = 900;
        intro.HorizontalAlignment = HorizontalAlignment.Left;
        intro.Margin = new Thickness(0, 0, 0, 8);
        body.Children.Add(intro);

        if (input.Rows.Count == 0)
        {
            body.Children.Add(CreditsKit.Message(BgsWorkTable.EmptyText(tick)));
            body.Children.Add(CreditsKit.Note(BgsWorkTable.Hint, 18));
            return body;
        }

        if (shown.Count == 0)
        {
            body.Children.Add(CreditsKit.Message(BgsWorkTable.NoMatchText(tick)));
            return body;
        }

        var groups = BgsWorkTable.Groups(shown, input.Work, input.Faction)
            .Select(group => new BgsWorkGroup(group.Title, Head.Sort(group.Rows)))
            .ToList();

        var onPage = groups;

        if (_headset)
        {
            page = FactionModel.ClampPage(page, shown.Count);
            var skip = page * FactionModel.PageSize;
            var take = FactionModel.PageSize;
            var slice = new List<BgsWorkGroup>();

            foreach (var group in groups)
            {
                var rows = group.Rows.Skip(Math.Max(0, skip)).Take(Math.Max(0, take)).ToList();
                skip -= group.Rows.Count;
                take -= rows.Count;

                if (rows.Count > 0)
                {
                    slice.Add(new BgsWorkGroup(group.Title, rows));
                }
            }

            onPage = slice;
        }

        for (var i = 0; i < onPage.Count; i++)
        {
            var total = groups.First(group => group.Title == onPage[i].Title).Rows.Count;
            var count = BgsWorkTable.Plural(total, "faction");

            body.Children.Add(i == 0
                ? StarSystemPage.GroupHead(onPage[i].Title, Description(tick, input.Now), count)
                : StarSystemPage.GroupHead(onPage[i].Title, string.Empty, count));
            body.Children.Add(Table(onPage[i].Rows, tick is not null, input.Current));
        }

        return body;
    }

    private static TextBlock Description(DateTimeOffset? tick, DateTimeOffset now)
    {
        var note = RoutingKit.Ink(string.Empty, TypeScale.ControlLarge, ThemeManager.GreyKey);
        var sort = " Press a heading to sort.";

        if (tick is { } at)
        {
            note.Inlines!.Add(Fonts.Mono($"{PageKit.Clock(at)} UTC"));
            note.Inlines.Add(new Avalonia.Controls.Documents.Run($" · {BgsWorkTable.Ago(now - at)}.{sort}"));
        }
        else
        {
            note.Text = $"Since {PageKit.Clock(BgsWork.PeriodStart(null, now))} UTC yesterday.{sort}";
        }

        return note;
    }

    private StackPanel Table(IReadOnlyList<BgsWorkRow> rows, bool withInfluence, string? current)
    {
        var columns = withInfluence ? BgsWorkTable.Heads.Count : BgsWorkTable.Heads.Count - 1;
        var table = new StackPanel { Spacing = Gaps.Tile, Margin = new Thickness(0, 10, 0, 0) };

        var layout = new TableLayout(BgsWorkTable.TableId, [.. BgsWorkTable.ColumnList.Take(columns)], _columnWidths, _headset ? 12 : 16);

        table.Children.Add(CreditsKit.HeaderRow(Head, layout, i => i == 0 ? HorizontalAlignment.Left : HorizontalAlignment.Right));

        foreach (var row in rows)
        {
            table.Children.Add(RowView(row, withInfluence, current, layout, _copy));
        }

        return table;
    }

    private static Border RowView(BgsWorkRow row, bool withInfluence, string? current, TableLayout layout, Func<string, Task<bool>>? copy)
    {
        var grid = layout.Row([]);
        grid.VerticalAlignment = VerticalAlignment.Center;

        var isCurrent = row.System is not null && string.Equals(row.System, current, StringComparison.OrdinalIgnoreCase);
        var system = row.System is null
            ? CreditsKit.Line(BgsWorkTable.UnnamedSystem, Fonts.ProseFamily, TypeScale.ControlLarge, ThemeManager.GreyKey)
            : CopyGlyph.LinkBeside(
                CreditsKit.Line(row.System, Fonts.ChromeFamily, TypeScale.ControlLarge, isCurrent ? ThemeManager.CyanKey : ThemeManager.AKey),
                row.System,
                copy);
        var faction = CreditsKit.Line(row.Faction, Fonts.ProseFamily, TypeScale.ControlLarge, ThemeManager.WhiteKey);

        Place(grid, 0, new StackPanel { Children = { system, faction } });
        Place(grid, 1, Values((BgsWorkTable.MarksUpText(row.MarksUp), Key(row.MarksUp)), (BgsWorkTable.MarksDownText(row.MarksDown), row.MarksDown is null ? ThemeManager.Grey2Key : ThemeManager.RedKey)));
        Place(grid, 2, Values((BgsWorkTable.Credits(row.Bounties), Key(row.Bounties)), (BgsWorkTable.Credits(row.Bonds), Key(row.Bonds))));
        Place(grid, 3, Values((BgsWorkTable.Credits(row.Sold), Key(row.Sold)), (BgsWorkTable.Credits(row.Profit), row.Profit switch { null => ThemeManager.Grey2Key, < 0 => ThemeManager.RedKey, _ => ThemeManager.AKey })));
        Place(grid, 4, Values((BgsWorkTable.Credits(row.Cartography), Key(row.Cartography))));

        if (withInfluence)
        {
            Place(
                grid,
                BgsWorkTable.InfluenceColumn,
                row.Influence is { } across
                    ? Values((BgsWorkTable.InfluenceText(across), ThemeManager.AKey), (BgsWorkTable.VisitText(across), ThemeManager.AKey))
                    : Values((BgsWorkTable.Dash, ThemeManager.Grey2Key)));
        }

        var border = new Border
        {
            MinHeight = TypeScale.MinimumTarget,
            Padding = new Thickness(14, 6),
            Child = grid,
        };
        ThemeInk.Bind(border, Border.BackgroundProperty, ThemeManager.SlabKey);

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
            ThemeInk.Bind(block, TextBlock.ForegroundProperty, key);
            stack.Children.Add(block);
        }

        return stack;
    }
}
