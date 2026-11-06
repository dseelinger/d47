using System.Globalization;
using D47.App.Theming;
using D47.Core.Journal;
using D47.Core.Knowledge;

namespace D47.App.Panel;

/// <summary>A faction state's name as the page spells it, and the group the Overview counts it under.</summary>
public static class FactionStates
{
    public const string Economy = "Economy";
    public const string Security = "Security";
    public const string Conflict = "Conflict";
    public const string Other = "Other";

    private static readonly (string Group, string Description, string[] States)[] Groups =
    [
        (Economy, "The faction's economy in a system.", ["Boom", "Bust", "Famine", "Investment"]),
        (Security, "Order in a system.", ["Lockdown", "Civil unrest", "Civil liberty"]),
        (Conflict, "Wars, civil wars and elections.", ["War", "Civil war", "Election"]),
        (Other, "Expansion, retreat, and the rest.", [
            "Expansion", "Retreat", "Outbreak", "Infrastructure failure", "Natural disaster", "Blight", "Drought",
            "Pirate attack", "Terrorist attack", "Public holiday", "Incursion", "Infested"]),
    ];

    private static readonly Dictionary<string, (string Display, string Group)> Known = Groups
        .SelectMany(group => group.States.Select(state => (Key: Key(state), Display: state, group.Group)))
        .ToDictionary(entry => entry.Key, entry => (entry.Display, entry.Group), StringComparer.Ordinal);

    public static IReadOnlyList<(string Group, string Description)> GroupNames { get; } =
        [.. Groups.Select(group => (group.Group, group.Description))];

    /// <summary>The state in sentence case with spaces, whether it came from the journal (<c>CivilUnrest</c>) or Spansh (<c>Civil Unrest</c>).</summary>
    public static string Display(string state)
    {
        if (Known.TryGetValue(Key(state), out var known))
        {
            return known.Display;
        }

        var words = new List<string>();
        var word = new System.Text.StringBuilder();

        foreach (var letter in state.Trim())
        {
            if ((char.IsUpper(letter) || letter == ' ') && word.Length > 0)
            {
                words.Add(word.ToString());
                word.Clear();
            }

            if (letter != ' ')
            {
                word.Append(char.ToLowerInvariant(letter));
            }
        }

        if (word.Length > 0)
        {
            words.Add(word.ToString());
        }

        var text = string.Join(' ', words);

        return text.Length == 0 ? text : char.ToUpperInvariant(text[0]) + text[1..];
    }

    public static string GroupOf(string state) =>
        Known.TryGetValue(Key(state), out var known) ? known.Group : Other;

    /// <summary>Whether the state is a war, civil war or election.</summary>
    public static bool IsConflict(string state) => GroupOf(state) == Conflict;

    private static string Key(string state) =>
        new([.. state.Where(char.IsLetter).Select(char.ToLowerInvariant)]);
}

/// <summary>One system of the faction as the Systems section draws it.</summary>
public sealed record FactionRow(
    string System,
    long? SystemAddress,
    double? Influence,
    IReadOnlyList<string> Active,
    IReadOnlyList<string> Pending,
    IReadOnlyList<string> Recovering,
    bool Controls,
    DateTimeOffset? Updated,
    bool OwnReading)
{
    /// <summary>The faction has a war, civil war or election here, active or pending.</summary>
    public bool InConflict => Active.Concat(Pending).Any(FactionStates.IsConflict);

    public double? AgeMinutes(DateTimeOffset now) =>
        Updated is { } at ? Math.Max(0, (now - at).TotalMinutes) : null;
}

/// <summary>One state and the systems in it.</summary>
public sealed record FactionStateCount(string Name, int Count, string Share);

/// <summary>The states of one Overview group, largest count first; zero counts are left out.</summary>
public sealed record FactionStateGroup(string Name, string Description, IReadOnlyList<FactionStateCount> States);

/// <summary>What the Overview section shows.</summary>
public sealed record FactionOverview(
    string Government,
    string Allegiance,
    int Present,
    int Controlled,
    int Stations,
    int InConflict,
    IReadOnlyList<FactionStateGroup> Groups);

/// <summary>A faction report turned into the rows and counts the Faction page draws.</summary>
public static class FactionModel
{
    /// <summary>A Spansh report older than this is marked old.</summary>
    public static readonly TimeSpan OldAfter = TimeSpan.FromDays(30);

    /// <summary>Rows the headset shows to a page.</summary>
    public const int PageSize = 25;

    public const string Dash = "—";

    /// <summary>
    /// One row per system, most influence first. A system the Commander has seen since Spansh's report, or that
    /// Spansh has no time for, takes its influence and states from that reading.
    /// </summary>
    public static IReadOnlyList<FactionRow> Rows(FactionReport report, SystemStandings standings)
    {
        ArgumentNullException.ThrowIfNull(report);
        ArgumentNullException.ThrowIfNull(standings);

        return
        [
            .. report.Systems
                .Select(system => RowOf(report.Name, system, standings))
                .OrderByDescending(row => row.Influence.HasValue)
                .ThenByDescending(row => row.Influence)
                .ThenBy(row => row.System, StringComparer.CurrentCultureIgnoreCase),
        ];
    }

    private static FactionRow RowOf(string faction, FactionSystem system, SystemStandings standings)
    {
        var reading = standings.Latest(system.Name);
        var own = reading?.Factions.FirstOrDefault(f => string.Equals(f.Name, faction, StringComparison.OrdinalIgnoreCase));

        if (reading is not null && own is not null && (system.ReportedAt is not { } reported || reading.SeenAt > reported))
        {
            return new FactionRow(
                system.Name,
                system.SystemAddress,
                own.Influence,
                Names(own.ActiveStates),
                Names(own.PendingStates),
                Names(own.RecoveringStates),
                system.Controls,
                reading.SeenAt,
                true);
        }

        return new FactionRow(
            system.Name,
            system.SystemAddress,
            system.Influence,
            Names(system.ActiveStates),
            Names(system.PendingStates),
            Names(system.RecoveringStates),
            system.Controls,
            system.ReportedAt,
            false);
    }

    private static IReadOnlyList<string> Names(IEnumerable<string> states) =>
        [.. states.Select(FactionStates.Display).Distinct(StringComparer.Ordinal)];

    public static FactionOverview Overview(FactionReport report, IReadOnlyList<FactionRow> rows)
    {
        ArgumentNullException.ThrowIfNull(report);
        ArgumentNullException.ThrowIfNull(rows);

        var counts = rows
            .SelectMany(row => row.Active.Distinct(StringComparer.Ordinal))
            .GroupBy(state => state, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.Count(), StringComparer.Ordinal);

        var groups = FactionStates.GroupNames
            .Select(group => new FactionStateGroup(
                group.Group,
                group.Description,
                [
                    .. counts
                        .Where(pair => FactionStates.GroupOf(pair.Key) == group.Group)
                        .OrderByDescending(pair => pair.Value)
                        .ThenBy(pair => pair.Key, StringComparer.Ordinal)
                        .Select(pair => new FactionStateCount(pair.Key, pair.Value, Share(pair.Value, rows.Count))),
                ]))
            .ToList();

        return new FactionOverview(
            report.Government ?? Dash,
            report.Allegiance ?? Dash,
            rows.Count,
            rows.Count(row => row.Controls),
            report.Stations.Count,
            rows.Count(row => row.InConflict),
            groups);
    }

    /// <summary>The share of <paramref name="present"/> as a whole percent, or <c>&lt;1%</c> below one.</summary>
    public static string Share(int count, int present)
    {
        var percent = present == 0 ? 0 : count * 100.0 / present;

        return percent < 1 ? "<1%" : Math.Round(percent, MidpointRounding.AwayFromZero).ToString("0", CultureInfo.InvariantCulture) + "%";
    }

    /// <summary>The rows whose system name contains <paramref name="query"/>, ignoring case.</summary>
    public static IReadOnlyList<FactionRow> Matching(IReadOnlyList<FactionRow> rows, string? query) =>
        string.IsNullOrWhiteSpace(query)
            ? rows
            : [.. rows.Where(row => row.System.Contains(query.Trim(), StringComparison.OrdinalIgnoreCase))];

    /// <summary>The Systems columns, each sortable from its heading.</summary>
    public static IReadOnlyList<SortColumn<FactionRow>> Heads(Func<DateTimeOffset> now) =>
    [
        SortColumn<FactionRow>.Text("System", row => row.System),
        SortColumn<FactionRow>.Figure("Influence", row => row.Influence, SortDirection.Descending),
        SortColumn<FactionRow>.Text("Active", row => row.Active.FirstOrDefault()),
        SortColumn<FactionRow>.Text("Pending", row => row.Pending.FirstOrDefault()),
        SortColumn<FactionRow>.Text("Recovering", row => row.Recovering.FirstOrDefault()),
        SortColumn<FactionRow>.Figure<int>("Controls", row => row.Controls ? 1 : 0, SortDirection.Descending),
        SortColumn<FactionRow>.Figure("Updated", row => row.AgeMinutes(now()), SortDirection.Ascending),
    ];

    /// <summary>The order the rows come in, drawn as Influence ▼.</summary>
    public static readonly SortOrder DefaultOrder = new(1, SortDirection.Descending);

    /// <summary>Whether a row's Spansh report is older than <see cref="OldAfter"/>; the Commander's own readings never are.</summary>
    public static bool IsOld(FactionRow row, DateTimeOffset now) =>
        !row.OwnReading && row.Updated is { } at && now - at > OldAfter;

    /// <summary>The Updated cell: <c>1 hour ago</c>, or <c>you saw it 2 hours ago</c> for the Commander's own reading.</summary>
    public static string Age(FactionRow row, DateTimeOffset now) => row.Updated is not { } at
        ? Dash
        : row.OwnReading ? "you saw it " + Ago(now - at) : Ago(now - at);

    /// <summary>"less than a minute ago", "5 minutes ago", "1 hour ago", "14 days ago".</summary>
    public static string Ago(TimeSpan age)
    {
        var minutes = (long)Math.Max(0, Math.Floor(age.TotalMinutes));

        return minutes switch
        {
            < 1 => "less than a minute ago",
            < 60 => Plural(minutes, "minute"),
            < 1440 => Plural((long)Math.Round(minutes / 60.0, MidpointRounding.AwayFromZero), "hour"),
            _ => Plural((long)Math.Round(minutes / 1440.0, MidpointRounding.AwayFromZero), "day"),
        } + (minutes < 1 ? string.Empty : " ago");
    }

    /// <summary>"Galaxy search data, fetched 4 minutes ago."</summary>
    public static string FetchedText(TimeSpan age) => "Galaxy search data, fetched " + Ago(age) + ".";

    /// <summary>"590 SYSTEMS", or "4 OF 590 SYSTEMS" while filtered.</summary>
    public static string CountText(int shown, int all) =>
        (shown == all ? $"{all} systems" : $"{shown} of {all} systems").ToUpperInvariant();

    /// <summary>The page index kept within the pages <paramref name="total"/> rows fill.</summary>
    public static int ClampPage(int page, int total) => Math.Clamp(page, 0, Math.Max(0, (total - 1) / PageSize));

    /// <summary>"1–25 of 590".</summary>
    public static string PageReadout(int page, int total) =>
        total == 0
            ? string.Empty
            : $"{page * PageSize + 1}–{Math.Min(total, (page + 1) * PageSize)} of {total}";

    private static string Plural(long count, string unit) => $"{count} {unit}{(count == 1 ? string.Empty : "s")}";
}
