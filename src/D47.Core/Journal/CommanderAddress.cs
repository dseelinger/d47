namespace D47.Core.Journal;

/// <summary>How the Commander is addressed out loud (#247): rank and surname.</summary>
public static class CommanderAddress
{
    public static string Said(string? name)
    {
        if (name is null || name.Trim() is not { Length: > 0 } whole)
        {
            return "Commander";
        }

        var at = whole.LastIndexOf(' ');

        return $"Commander {(at < 0 ? whole : whole[(at + 1)..])}";
    }
}
