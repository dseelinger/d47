namespace D47.Core.Seats;

/// <summary>What the Crew page checks before it writes a seat, and what it says when a hull has none.</summary>
public static class CrewSeatRules
{
    /// <summary>The roles in the order the page lists them, Custom last.</summary>
    public static IReadOnlyList<CrewRole> Roles { get; } = Enum.GetValues<CrewRole>();

    public static string RoleLabel(CrewRole role) => role switch
    {
        CrewRole.FirstOfficer => "First officer",
        CrewRole.ScienceOfficer => "Science officer",
        CrewRole.SecurityOfficer => "Security officer",
        var other => other.ToString(),
    };

    public static string NoSeats(string hullSaid) => $"The {hullSaid} has no seat besides yours.";

    public static string UnknownSeats(string hullSaid) => $"d47 does not know how many seats a {hullSaid} has.";

    /// <summary>
    /// Why <paramref name="seat"/> cannot be stored beside <paramref name="others"/>, or null. A name must be
    /// unlike every other seat's and every name in <paramref name="reserved"/>; a standard role may be held once.
    /// </summary>
    public static string? Refusal(CrewSeat seat, IEnumerable<CrewSeat> others, IEnumerable<string> reserved)
    {
        var name = seat.Name.Trim();

        if (name.Length is 0 or > CrewSeat.MaxName)
        {
            return $"A name must be 1 to {CrewSeat.MaxName} characters.";
        }

        if (seat.Role == CrewRole.Custom && seat.Title?.Trim() is not { Length: > 0 and <= CrewSeat.MaxTitle })
        {
            return $"A custom role needs a title of 1 to {CrewSeat.MaxTitle} characters.";
        }

        var rest = others.Where(other => other.Id != seat.Id).ToList();

        if (rest.Any(other => Same(other.Name, name)))
        {
            return $"Another seat on this ship is already called {name}.";
        }

        if (reserved.Any(taken => Same(taken, name)))
        {
            return $"{name} is already a name aboard: a hired pilot, the ship AI or the carrier captain.";
        }

        return seat.Role != CrewRole.Custom && rest.Any(other => other.Role == seat.Role)
            ? $"The ship already has a {RoleLabel(seat.Role).ToLowerInvariant()} seat."
            : null;
    }

    /// <summary>
    /// <paramref name="seats"/> with the defaults added to fill the hull's empty seats. A default whose role or
    /// name is already taken is skipped, and the seats already there are untouched.
    /// </summary>
    public static IReadOnlyList<CrewSeat> Offered(
        string? hull, int shipId, IReadOnlyList<CrewSeat> seats, IEnumerable<string> reserved)
    {
        var room = (CrewSeats.CountFor(hull) ?? 0) - seats.Count;
        var all = seats.ToList();
        var names = reserved.ToList();

        foreach (var offer in CrewDefaults.Offer(hull, shipId))
        {
            if (room <= 0)
            {
                break;
            }

            if (Refusal(offer, all, names) is not null)
            {
                continue;
            }

            all.Add(offer);
            room--;
        }

        return all;
    }

    private static bool Same(string left, string right) =>
        string.Equals(left.Trim(), right.Trim(), StringComparison.OrdinalIgnoreCase);
}
