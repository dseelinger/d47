using D47.Core.Journal;

namespace D47.Core.Knowledge;

/// <summary>What the navigation panel's selection in Status.json refers to.</summary>
public abstract record NavTarget
{
    private NavTarget()
    {
    }

    /// <summary>Nothing is selected.</summary>
    public sealed record Nothing : NavTarget;

    /// <summary>A system, which Elite writes as body 0, with its galaxy record.</summary>
    public sealed record StarSystem(StarSystemProfile Record) : NavTarget;

    /// <summary>A star or planet, with whatever the Commander's scans and the galaxy record hold for it.</summary>
    public sealed record Body(
        long SystemAddress,
        int BodyId,
        string Name,
        BodyScan? Scan,
        BodyBiology? Signals,
        BodyProfile? Record,
        IReadOnlyList<StationProfile> Stations) : NavTarget;

    /// <summary>A station, carrier or settlement in the galaxy record.</summary>
    public sealed record Station(StationProfile Record) : NavTarget;

    /// <summary>A selection with no record; <paramref name="Name"/> is null when it cannot be spoken.</summary>
    public sealed record Unknown(string? Name) : NavTarget;

    /// <summary>
    /// The target <paramref name="selected"/> names: a system for body 0, then a body matched by id in
    /// the scans or the galaxy record, then a station matched by name, otherwise unknown.
    /// </summary>
    /// <param name="record">The galaxy record of the destination's system, or null where none was read.</param>
    public static NavTarget Resolve(
        StatusDestination? selected,
        BodyScans scans,
        BodySignals signals,
        StarSystemProfile? record)
    {
        if (selected is not { } destination)
        {
            return new Nothing();
        }

        var name = Sayable(destination.Name);

        if (record is not null && record.SystemAddress != destination.System)
        {
            record = null;
        }

        if (destination.Body == 0)
        {
            return record is null ? new Unknown(name) : new StarSystem(record);
        }

        var scan = destination.Body is > 0 and <= int.MaxValue
            ? scans.For(destination.System, (int)destination.Body)
            : null;
        var body = record?.Bodies.FirstOrDefault(candidate => candidate.BodyId == destination.Body)
                   ?? (name is null
                       ? null
                       : record?.Bodies.FirstOrDefault(candidate => Same(candidate.Name, name)));

        if (scan is not null || body is not null)
        {
            var bodyName = scan?.BodyName ?? body!.Name;
            var bodyId = scan?.BodyId ?? body!.BodyId;

            return new Body(
                destination.System,
                bodyId,
                bodyName,
                scan,
                SignalsFor(signals, destination.System, bodyId, bodyName),
                body,
                [.. record?.Stations.Where(station => Same(station.Body, body?.Name ?? bodyName)) ?? []]);
        }

        if (name is not null
            && (record?.Stations.FirstOrDefault(station => Same(station.Name, name))
                ?? record?.Stations.FirstOrDefault(station => Callsign(station, name))) is { } found)
        {
            return new Station(found);
        }

        return new Unknown(name);
    }

    /// <summary>The signals record of the body <paramref name="selected"/> names, or null where it is not a body in the Commander's signals.</summary>
    public static BodyBiology? TargetedSignals(StatusDestination? selected, BodySignals signals) =>
        selected is { Body: > 0 and <= int.MaxValue } destination
            ? signals.All.FirstOrDefault(body => body.SystemAddress == destination.System && body.BodyId == (int)destination.Body)
            : null;

    /// <summary>The name of the body <paramref name="selected"/> names in the Commander's scans or signals, or null.</summary>
    public static string? TargetedName(StatusDestination? selected, BodyScans scans, BodySignals signals) =>
        selected is { Body: > 0 and <= int.MaxValue } destination
            ? scans.For(destination.System, (int)destination.Body)?.BodyName
              ?? TargetedSignals(selected, signals)?.BodyName
            : null;

    /// <summary>A destination name fit to say, or null where Elite wrote a <c>$</c> placeholder or nothing.</summary>
    public static string? Sayable(string? name) =>
        string.IsNullOrWhiteSpace(name) || name.TrimStart().StartsWith('$') ? null : name.Trim();

    private static BodyBiology? SignalsFor(BodySignals signals, long systemAddress, int bodyId, string bodyName) =>
        signals.All.FirstOrDefault(body => body.SystemAddress == systemAddress && body.BodyId == bodyId)
        ?? signals.All.FirstOrDefault(body => body.SystemAddress is null && Same(body.BodyName, bodyName));

    /// <summary>A carrier the galaxy record names by its callsign, selected under its name and callsign.</summary>
    private static bool Callsign(StationProfile station, string name) =>
        station.Kind == StationKind.FleetCarrier
        && name.EndsWith(" " + station.Name.Trim(), StringComparison.OrdinalIgnoreCase);

    private static bool Same(string? left, string? right) =>
        left is not null && right is not null && string.Equals(left.Trim(), right.Trim(), StringComparison.OrdinalIgnoreCase);
}
