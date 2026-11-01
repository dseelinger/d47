namespace D47.Core.Audio;

/// <summary>How long music takes to fade between two folders.</summary>
public static class Crossfade
{
    private static readonly TimeSpan Map = TimeSpan.FromSeconds(0.75);
    private static readonly TimeSpan Combat = TimeSpan.FromSeconds(0.5);
    private static readonly TimeSpan Bed = TimeSpan.FromSeconds(3);

    private static readonly HashSet<string> Maps = new(StringComparer.OrdinalIgnoreCase)
    {
        "galaxy-map", "system-map", "scanner", "codex", "squadrons", "powerplay",
        "fleet-carrier", "main-menu", "cqc-menu",
    };

    /// <summary>Short when either folder is a map or menu, short into combat or an interdiction, otherwise long.</summary>
    public static TimeSpan Between(string from, string to)
    {
        if (Maps.Contains(from) || Maps.Contains(to))
        {
            return Map;
        }

        return to.StartsWith("combat-", StringComparison.OrdinalIgnoreCase)
            || string.Equals(to, "interdiction", StringComparison.OrdinalIgnoreCase)
            ? Combat
            : Bed;
    }
}
