namespace D47.Core.Journal;

/// <summary>How the Commander is addressed out loud (#247): rank and surname.</summary>
public static class CommanderAddress
{
    public static string Said(string? name) =>
        Surname(name) is { } surname ? $"Commander {surname}" : "Commander";

    /// <summary>The last word of the Commander's name, as spoken after "Commander" — or null with no name.</summary>
    public static string? Surname(string? name)
    {
        if (name is null || name.Trim() is not { Length: > 0 } whole)
        {
            return null;
        }

        var at = whole.LastIndexOf(' ');

        return at < 0 ? whole : whole[(at + 1)..];
    }
}
