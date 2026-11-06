using D47.Core.Journal;

namespace D47.Core.Knowledge;

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

/// <summary>Builds the rows of a faction's systems from a galaxy report and the Commander's own readings.</summary>
public static class FactionRows
{
    /// <summary>
    /// One row per system, most influence first. A system the Commander has seen since Spansh's report, or that
    /// Spansh has no time for, takes its influence and states from that reading.
    /// </summary>
    public static IReadOnlyList<FactionRow> Of(FactionReport report, SystemStandings standings)
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
}
