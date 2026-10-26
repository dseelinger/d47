namespace D47.Core.Persona;

/// <summary>A reply in a member's own language, a line holding only <c>///</c>, then its English.</summary>
public static class BilingualReply
{
    public const string Marker = "///";

    /// <summary>The instruction that asks for a line in <paramref name="tongue"/> followed by its English.</summary>
    public static string Shape(string tongue) =>
        $"Write the line in {tongue}, then a line holding only {Marker}, then the same line in English. Nothing else goes before, between or after them.";

    /// <summary>The part to speak and its English; the whole reply and a null translation when no line holds only the marker.</summary>
    public static (string Spoken, string? Translation) Split(string reply)
    {
        ArgumentNullException.ThrowIfNull(reply);

        var lines = reply.ReplaceLineEndings("\n").Split('\n');
        var at = Array.FindIndex(lines, line => line.Trim() == Marker);

        if (at < 0)
        {
            return (reply.Trim(), null);
        }

        var translation = string.Join('\n', lines[(at + 1)..]).Trim();

        return (string.Join('\n', lines[..at]).Trim(), translation.Length == 0 ? null : translation);
    }
}
