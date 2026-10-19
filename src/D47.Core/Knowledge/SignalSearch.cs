using D47.Core.Journal;

namespace D47.Core.Knowledge;

/// <summary>One signal an answer lists.</summary>
public sealed record SignalFinding(string Signal, string System, double Distance, DateTimeOffset LastSeen, bool OwnScan, long SystemAddress = 0);

/// <summary>The answer to a signal search: <paramref name="Total"/> found, nearest first, of which <paramref name="Signals"/> are listed.</summary>
public sealed record SignalSearchResult(string? Reference, int Total, IReadOnlyList<SignalFinding> Signals);

/// <summary>Searches the Commander's own scans and the daily index together, nearest first.</summary>
public static class SignalSearch
{
    public const string AnyRes = "Any RES";

    /// <summary>The test for what was asked: a kind's display name or "Any RES", else part of a signal's name.</summary>
    public static (Func<SignalSighting, bool> Matches, bool ByKind) Matcher(string signal)
    {
        ArgumentNullException.ThrowIfNull(signal);

        var text = string.Join(' ', signal.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));

        if (text.Equals(AnyRes, StringComparison.OrdinalIgnoreCase))
        {
            return (sighting => sighting.Kind is { } kind && SignalKind.AnyRes.Contains(kind), true);
        }

        if (SignalKind.All.FirstOrDefault(kind => kind.Name.Equals(text, StringComparison.OrdinalIgnoreCase)) is { } named)
        {
            return (sighting => sighting.Kind == named, true);
        }

        return (sighting => sighting.SignalName.Contains(text, StringComparison.OrdinalIgnoreCase), false);
    }

    /// <summary>Where a system is, from the Commander's scans first and then the index; null when neither has it.</summary>
    public static StarPosition? Place(string system, IEnumerable<SignalSighting> own, SignalIndex? index)
    {
        var scanned = own.FirstOrDefault(sighting => sighting.SystemName.Equals(system, StringComparison.OrdinalIgnoreCase));

        return scanned?.Position ?? index?.Place(system);
    }

    /// <summary>
    /// The signals within <paramref name="maxDistance"/> light years of <paramref name="origin"/> that
    /// <paramref name="matches"/> accepts. A signal in both sources appears once, with the later date; a megaship
    /// appears once, where it was last seen.
    /// </summary>
    public static SignalSearchResult Find(
        StarPosition origin,
        string? reference,
        Func<SignalSighting, bool> matches,
        double maxDistance,
        int limit,
        IEnumerable<SignalSighting> own,
        SignalIndex? index)
    {
        var found = new Dictionary<(long System, string Type, string Name), (SignalSighting Sighting, bool Own)>();

        foreach (var sighting in index?.Within(origin, maxDistance) ?? [])
        {
            if (matches(sighting))
            {
                Keep(found, sighting, own: false);
            }
        }

        foreach (var sighting in own)
        {
            if (origin.DistanceTo(sighting.Position) <= maxDistance && matches(sighting))
            {
                Keep(found, sighting, own: true);
            }
        }

        var ordered = found.Values
            .Select(pair => new SignalFinding(
                pair.Sighting.SignalName,
                pair.Sighting.SystemName,
                Math.Round(origin.DistanceTo(pair.Sighting.Position), 2),
                pair.Sighting.LastSeen,
                pair.Own,
                pair.Sighting.SystemAddress))
            .OrderBy(finding => finding.Distance)
            .ThenByDescending(finding => finding.LastSeen)
            .ThenBy(finding => finding.System, StringComparer.Ordinal)
            .ToList();

        return new SignalSearchResult(reference, ordered.Count, [.. ordered.Take(limit)]);
    }

    private static void Keep(
        Dictionary<(long System, string Type, string Name), (SignalSighting Sighting, bool Own)> found,
        SignalSighting sighting,
        bool own)
    {
        var key = sighting.Kind == SignalKind.Megaship
            ? (0L, string.Empty, sighting.SignalName)
            : (sighting.SystemAddress, sighting.SignalType, sighting.SignalName);

        if (!found.TryGetValue(key, out var held) || held.Sighting.LastSeen <= sighting.LastSeen)
        {
            found[key] = (sighting, own);
        }
    }
}
