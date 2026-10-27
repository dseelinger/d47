namespace D47.Core.Knowledge;

/// <summary>The Empire and Federation rank ladders, 0 to 14, as coriolis-data's requirements gate ships by.</summary>
public static class NavalRanks
{
    private static readonly string[] Empire =
    [
        "None", "Outsider", "Serf", "Master", "Squire", "Knight", "Lord", "Baron",
        "Viscount", "Count", "Earl", "Marquis", "Duke", "Prince", "King",
    ];

    private static readonly string[] Federation =
    [
        "None", "Recruit", "Cadet", "Midshipman", "Petty Officer", "Chief Petty Officer",
        "Warrant Officer", "Ensign", "Lieutenant", "Lieutenant Commander", "Post Commander",
        "Post Captain", "Rear Admiral", "Vice Admiral", "Admiral",
    ];

    /// <summary>The Empire rank name at this grade, or null outside 0 to 14.</summary>
    public static string? EmpireName(int rank) => Named(Empire, rank);

    /// <summary>The Federation rank name at this grade, or null outside 0 to 14.</summary>
    public static string? FederationName(int rank) => Named(Federation, rank);

    /// <summary>The navy and rank a ship's <c>empire:N</c> or <c>federation:N</c> requirement names, or null.</summary>
    public static (Navy Navy, int Rank)? Gate(string? requirement)
    {
        if (requirement is null)
        {
            return null;
        }

        foreach (var (navy, prefix) in Prefixes)
        {
            if (requirement.StartsWith(prefix, StringComparison.Ordinal)
                && int.TryParse(requirement[prefix.Length..], out var rank))
            {
                return (navy, rank);
            }
        }

        return null;
    }

    /// <summary>The navy and rank that opens this system's permit, or null when no rank does.</summary>
    public static (Navy Navy, int Rank)? PermitGate(string? system) =>
        Permits.FirstOrDefault(permit => string.Equals(permit.Name, system + " permit", StringComparison.OrdinalIgnoreCase)) is { Name: not null } found
            ? (found.Navy, found.Rank)
            : null;

    /// <summary>The rank name at this grade in this navy, or null outside 0 to 14.</summary>
    public static string? Name(Navy navy, int rank) => navy == Navy.Empire ? EmpireName(rank) : FederationName(rank);

    /// <summary>What a navy's ranks unlock, split around the rank held.</summary>
    public static NavyUnlocks UnlocksFor(Navy navy, int rank)
    {
        var all = Ordered(navy);
        var next = all.Where(unlock => unlock.Rank > rank).Select(unlock => unlock.Rank).DefaultIfEmpty(0).Min();

        return new NavyUnlocks(
            [.. all.Where(unlock => unlock.Rank <= rank)],
            next == 0 ? null : next,
            [.. all.Where(unlock => next != 0 && unlock.Rank == next)],
            [.. all.Where(unlock => next != 0 && unlock.Rank > next)],
            all.Count == 0 ? null : all[^1].Rank);
    }

    private static readonly (Navy Navy, string Prefix)[] Prefixes = [(Navy.Empire, "empire:"), (Navy.Federation, "federation:")];

    private static readonly NavyUnlock[] Permits =
    [
        new("Achenar permit", NavyUnlockKind.Permit, 4, Navy.Empire),
        new("Sol permit", NavyUnlockKind.Permit, 4, Navy.Federation),
    ];

    private static List<NavyUnlock> Unlocks(Navy navy) =>
    [
        .. EliteSpecifications.Ships
            .Select(ship => (Ship: ship, Gate: Gate(ship.Requirement)))
            .Where(gated => gated.Gate is { } gate && gate.Navy == navy)
            .Select(gated => new NavyUnlock(gated.Ship.Name, NavyUnlockKind.Ship, gated.Gate!.Value.Rank, navy)),
        .. Permits.Where(permit => permit.Navy == navy),
    ];

    private static IReadOnlyList<NavyUnlock> Ordered(Navy navy) =>
        [.. Unlocks(navy).OrderBy(unlock => unlock.Rank).ThenBy(unlock => unlock.Name, StringComparer.Ordinal)];

    private static string? Named(string[] ladder, int rank) =>
        rank >= 0 && rank < ladder.Length ? ladder[rank] : null;
}

public enum Navy
{
    Empire,
    Federation,
}

public enum NavyUnlockKind
{
    Ship,
    Permit,
}

public sealed record NavyUnlock(string Name, NavyUnlockKind Kind, int Rank, Navy Navy);

/// <summary>Unlocks at or below the rank held, at the next rank that unlocks anything, and above that.</summary>
public sealed record NavyUnlocks(
    IReadOnlyList<NavyUnlock> Unlocked,
    int? NextRank,
    IReadOnlyList<NavyUnlock> Next,
    IReadOnlyList<NavyUnlock> Later,
    int? LastRank);
