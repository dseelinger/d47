using System.Collections.Immutable;
using D47.Core.Journal;

namespace D47.Core.Adventures;

/// <summary>A conflict the Commander contributed to for a beat, and how it ended once an arrival showed it had.</summary>
public sealed record ConflictPart(string System, string WarType, string Side, string Against, bool Ended = false, string? Winner = null)
{
    public bool Won => Ended && string.Equals(Winner, Side, StringComparison.OrdinalIgnoreCase);

    /// <summary>The result in words: won, lost, ended level or still running.</summary>
    public string Result =>
        !Ended ? "has not been seen to end"
        : Winner is null ? "ended with neither side ahead"
        : Won ? "was won by the Commander's side" : $"was won by {Winner}";

    public string Describe() =>
        $"The Commander took part in the {WarWord(WarType)} in {System} for {Side} against {Against}; it {Result}.";

    private static string WarWord(string warType) => JournalJson.Symbol(warType) switch
    {
        "civilwar" => "civil war",
        "election" => "election",
        _ => "war",
    };
}

/// <summary>The systems the Commander has seen and the one they are in, folded from the journal for the conflict beats.</summary>
public sealed record AdventureWorld
{
    public static readonly AdventureWorld Empty = new();

    private ImmutableDictionary<long, string> Names { get; init; } = ImmutableDictionary<long, string>.Empty;

    /// <summary>The name of the system the Commander is in.</summary>
    public string? System { get; init; }

    public SystemStandings Standings { get; init; } = SystemStandings.Empty;

    /// <summary>Every body scanned, and where a first footfall was taken.</summary>
    public BodyScans Bodies { get; init; } = BodyScans.Empty;

    /// <summary>Whether the Commander is on foot: from a <c>Disembark</c> or <c>DropshipDeploy</c> to the next <c>Embark</c>, or as a system entry's <c>OnFoot</c> says.</summary>
    public bool IsOnFoot { get; init; }

    public AdventureWorld Apply(JournalEvent journalEvent)
    {
        ArgumentNullException.ThrowIfNull(journalEvent);

        switch (journalEvent.Kind)
        {
            case "Disembark":
                return this with { Bodies = Bodies.Apply(journalEvent), IsOnFoot = true };
            case "Scan":
                return this with { Bodies = Bodies.Apply(journalEvent) };
            case "DropshipDeploy":
                return this with { IsOnFoot = true };
            case "Embark":
                return this with { IsOnFoot = false };
        }

        if (!BodyScans.IsSystemEntry(journalEvent))
        {
            return this;
        }

        var withBodies = this with { Bodies = Bodies.Apply(journalEvent), IsOnFoot = journalEvent.Bool("OnFoot") };

        if (journalEvent.String("StarSystem") is not { Length: > 0 } name)
        {
            return withBodies;
        }

        var names = journalEvent.Raw.Long("SystemAddress") is { } address ? Names.SetItem(address, name) : Names;

        return withBodies with { System = name, Names = names, Standings = Standings.Apply(journalEvent) };
    }

    /// <summary>Whether <paramref name="journalEvent"/> is a <c>Disembark</c> that took the first footfall on its body.</summary>
    public bool IsFirstFootfall(JournalEvent journalEvent) =>
        journalEvent.Kind is "Disembark"
        && journalEvent.Long("SystemAddress") is { } address
        && journalEvent.Int("BodyID") is { } body
        && Bodies.For(address, body)?.FootfallTakenAt == journalEvent.Timestamp;

    /// <summary>The address of a system the Commander has been in, from its name.</summary>
    public long? AddressOf(string system) =>
        Names.Where(pair => string.Equals(pair.Value, system, StringComparison.OrdinalIgnoreCase)).Select(pair => (long?)pair.Key).FirstOrDefault();

    /// <summary>The wars, civil wars and elections of the system the Commander is in that are active or pending.</summary>
    public IEnumerable<SystemConflict> Here() => Open(System);

    /// <summary>The conflicts of a system, as it was last read, that are active or pending.</summary>
    public IEnumerable<SystemConflict> Open(string? system) =>
        system is null || Standings.Latest(system) is not { } reading
            ? []
            : reading.Conflicts.Where(conflict => conflict.Status is "active" or "pending");

    /// <summary>The conflict a part was taken in, as it was last read.</summary>
    public SystemConflict? Of(ConflictPart part) =>
        Standings.Latest(part.System)?.Conflicts.FirstOrDefault(conflict =>
            string.Equals(conflict.WarType, part.WarType, StringComparison.OrdinalIgnoreCase)
            && Takes(conflict, part.Side) && Takes(conflict, part.Against));

    /// <summary>Whether <paramref name="faction"/> is one side of the conflict.</summary>
    public static bool Takes(SystemConflict conflict, string? faction) =>
        faction is not null
        && (string.Equals(conflict.Faction1.Name, faction.Trim(), StringComparison.OrdinalIgnoreCase)
            || string.Equals(conflict.Faction2.Name, faction.Trim(), StringComparison.OrdinalIgnoreCase));

    /// <summary>The side the conflict has against <paramref name="faction"/>.</summary>
    public static string Against(SystemConflict conflict, string faction) =>
        string.Equals(conflict.Faction1.Name, faction.Trim(), StringComparison.OrdinalIgnoreCase) ? conflict.Faction2.Name : conflict.Faction1.Name;
}
