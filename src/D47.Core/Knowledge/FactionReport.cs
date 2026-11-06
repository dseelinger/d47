namespace D47.Core.Knowledge;

/// <summary>Where one minor faction is present, as the galaxy service last had it reported.</summary>
/// <param name="Name">The faction's name as the service spells it.</param>
public sealed record FactionReport(
    string Name,
    string? Government,
    string? Allegiance,
    IReadOnlyList<FactionSystem> Systems,
    IReadOnlyList<FactionStation> Stations);

/// <summary>One system the faction is present in.</summary>
public sealed record FactionSystem
{
    public required string Name { get; init; }

    public long? SystemAddress { get; init; }

    public long? Population { get; init; }

    /// <summary>The faction's influence here, as a fraction of 1.</summary>
    public double? Influence { get; init; }

    public IReadOnlyList<string> ActiveStates { get; init; } = [];

    public IReadOnlyList<string> PendingStates { get; init; } = [];

    public IReadOnlyList<string> RecoveringStates { get; init; } = [];

    /// <summary>Whether the faction controls the system.</summary>
    public bool Controls { get; init; }

    /// <summary>Every other faction present.</summary>
    public IReadOnlyList<FactionNeighbour> Others { get; init; } = [];

    /// <summary>When the service last had a report of this system.</summary>
    public DateTimeOffset? ReportedAt { get; init; }
}

/// <summary>Another faction present in the same system, with its influence as a fraction of 1.</summary>
public sealed record FactionNeighbour(string Name, double? Influence)
{
    public IReadOnlyList<string> ActiveStates { get; init; } = [];

    public IReadOnlyList<string> PendingStates { get; init; } = [];
}

/// <summary>A station the faction controls. A null pad means the service lists no pads for it.</summary>
public sealed record FactionStation(
    string Name,
    string? Type,
    PadSize? LargestPad,
    double? DistanceToArrival,
    string System);
