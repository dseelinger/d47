using System.Security.Cryptography;

namespace D47.Core.Seats;

public enum CrewRole
{
    FirstOfficer,
    Helm,
    Comms,
    ScienceOfficer,
    SecurityOfficer,
    Navigation,
    Custom,
}

/// <summary>One crew seat on one ship. Storage is append-only: add properties, never rename one.</summary>
/// <param name="Id">Eight hex characters, generated once and never reused.</param>
/// <param name="Title">The Commander's word for a <see cref="CrewRole.Custom"/> role; null for any other.</param>
public sealed record CrewSeat(string Id, CrewRole Role, string? Title, string Name)
{
    public const int MaxTitle = 24;

    public const int MaxName = 32;

    public static string NewId() => Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(4));

    public static bool IsId(string? id) =>
        id is { Length: 8 } && id.All(char.IsAsciiHexDigitLower);
}

/// <summary>The seats on one ship, which is one record per Commander and ship id.</summary>
/// <param name="Hull">The hull symbol as the journal writes it.</param>
public sealed record ShipSeats(string CommanderFid, int ShipId, string? Hull, IReadOnlyList<CrewSeat> Seats);

/// <summary>One seat or ship the file was asked to hold, and why it was refused.</summary>
public sealed record CrewSeatProblem(string Where, string Reason);
