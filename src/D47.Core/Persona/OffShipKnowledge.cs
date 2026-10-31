using D47.Core.Journal;

namespace D47.Core.Persona;

/// <summary>What a speaker who is not aboard the Commander's ship can know of the game: the system, and the station when docked.</summary>
public static class OffShipKnowledge
{
    public static string Of(JournalLocation? location) =>
        Describe(location?.StarSystem, location is { Docked: true } ? location.StationName : null);

    public static string Describe(string? system, string? station)
    {
        var lines = new List<string>();

        if (system is { Length: > 0 })
        {
            lines.Add($"The Commander is in the {system} system.");
        }

        if (station is { Length: > 0 })
        {
            lines.Add($"The Commander is docked at {station}.");
        }

        return lines.Count == 0 ? "Where the Commander is, you do not know." : string.Join(' ', lines);
    }
}
