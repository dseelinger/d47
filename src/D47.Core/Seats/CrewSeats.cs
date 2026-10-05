using D47.Core.Knowledge;

namespace D47.Core.Seats;

public static class CrewSeats
{
    /// <summary>
    /// The seats d47 offers on a hull: its crew figure less the Commander's own seat. Null when the table
    /// has no crew figure for the hull.
    /// </summary>
    public static int? CountFor(string? hull) =>
        EliteSpecifications.Ship(hull)?.Crew is { } crew ? Math.Max(crew - 1, 0) : null;
}
