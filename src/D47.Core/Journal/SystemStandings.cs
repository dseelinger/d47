using System.Collections.Immutable;

namespace D47.Core.Journal;

/// <summary>One minor faction's share of a system. <paramref name="Influence"/> is a fraction, 0.0 to 1.0.</summary>
public sealed record FactionInfluence(string Name, double Influence);

/// <summary>One side of a conflict. <paramref name="Stake"/> is empty when the side holds nothing at stake.</summary>
public sealed record ConflictSide(string Name, string Stake, int WonDays);

/// <summary>A conflict as a journal event reported it. <paramref name="Status"/> is <c>active</c>, <c>pending</c> or empty.</summary>
public sealed record SystemConflict(string WarType, string Status, ConflictSide Faction1, ConflictSide Faction2)
{
    /// <summary>A conflict with an empty status has ended.</summary>
    public bool Ended => Status.Length == 0;

    /// <summary>The side with more won days once the conflict has ended; null while it runs or when the days are equal.</summary>
    public string? Winner =>
        !Ended || Faction1.WonDays == Faction2.WonDays
            ? null
            : Faction1.WonDays > Faction2.WonDays ? Faction1.Name : Faction2.Name;
}

/// <summary>A system's factions and conflicts as one event reported them.</summary>
public sealed record SystemReading(
    DateTimeOffset SeenAt,
    IReadOnlyList<FactionInfluence> Factions,
    IReadOnlyList<SystemConflict> Conflicts)
{
    public double? InfluenceOf(string faction) =>
        Factions.FirstOrDefault(f => string.Equals(f.Name, faction, StringComparison.OrdinalIgnoreCase))?.Influence;
}

/// <summary>
/// The factions' influence and the conflicts in each system the Commander has been in, folded from the
/// <c>Factions</c> and <c>Conflicts</c> of <c>Location</c>, <c>FSDJump</c> and <c>CarrierJump</c>.
/// Each system keeps its last reading and the last reading from the UTC day before it.
/// </summary>
public sealed record SystemStandings
{
    public static readonly SystemStandings Empty = new();

    private ImmutableDictionary<string, ImmutableList<SystemReading>> Readings { get; init; } =
        ImmutableDictionary.Create<string, ImmutableList<SystemReading>>(StringComparer.OrdinalIgnoreCase);

    public bool IsKnown => !Readings.IsEmpty;

    /// <summary>The names of the systems with a reading.</summary>
    public IEnumerable<string> Systems => Readings.Keys;

    /// <summary>The newest reading of a system, or null when it has none.</summary>
    public SystemReading? Latest(string system)
    {
        ArgumentNullException.ThrowIfNull(system);

        return Readings.TryGetValue(system, out var held) ? held[^1] : null;
    }

    /// <summary>The newest reading taken on an earlier UTC day than <see cref="Latest"/>, or null.</summary>
    public SystemReading? Earlier(string system)
    {
        ArgumentNullException.ThrowIfNull(system);

        return Readings.TryGetValue(system, out var held) && held.Count > 1 ? held[0] : null;
    }

    /// <summary>
    /// The change in a faction's influence between the latest reading and the one from the earlier day;
    /// null when either reading lacks the faction.
    /// </summary>
    public double? InfluenceChange(string system, string faction) =>
        Latest(system)?.InfluenceOf(faction) is { } now && Earlier(system)?.InfluenceOf(faction) is { } before
            ? now - before
            : null;

    public SystemStandings Apply(JournalEvent journalEvent)
    {
        ArgumentNullException.ThrowIfNull(journalEvent);

        if (journalEvent.Kind is not ("FSDJump" or "Location" or "CarrierJump")
            || journalEvent.String("StarSystem") is not { Length: > 0 } system
            || !journalEvent.Raw.TryGetProperty("Factions", out _))
        {
            return this;
        }

        var factions = journalEvent.Items("Factions")
            .Select(faction => (Name: faction.String("Name"), Influence: faction.Double("Influence")))
            .Where(pair => pair.Name is not null && pair.Influence is not null)
            .Select(pair => new FactionInfluence(pair.Name!, pair.Influence!.Value))
            .ToList();

        var conflicts = journalEvent.Items("Conflicts")
            .Select(Conflict)
            .OfType<SystemConflict>()
            .ToList();

        var reading = new SystemReading(journalEvent.Timestamp, factions, conflicts);
        var held = Readings.TryGetValue(system, out var existing) ? existing : [];

        return this with { Readings = Readings.SetItem(system, Remember(held, reading)) };
    }

    private static ImmutableList<SystemReading> Remember(ImmutableList<SystemReading> held, SystemReading reading)
    {
        if (held.IsEmpty)
        {
            return [reading];
        }

        var last = held[^1];

        if (reading.SeenAt < last.SeenAt)
        {
            return held;
        }

        return reading.SeenAt.UtcDateTime.Date == last.SeenAt.UtcDateTime.Date
            ? held.SetItem(held.Count - 1, reading)
            : [last, reading];
    }

    private static SystemConflict? Conflict(System.Text.Json.JsonElement raw)
    {
        var first = Side(raw.Object("Faction1"));
        var second = Side(raw.Object("Faction2"));

        return first is null || second is null || raw.String("WarType") is not { } warType
            ? null
            : new SystemConflict(warType, raw.String("Status") ?? string.Empty, first, second);
    }

    private static ConflictSide? Side(System.Text.Json.JsonElement? raw) =>
        raw?.String("Name") is { } name
            ? new ConflictSide(name, raw.Value.String("Stake") ?? string.Empty, raw.Value.Int("WonDays") ?? 0)
            : null;
}
