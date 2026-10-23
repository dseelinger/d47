namespace D47.Core.Persona;

/// <summary>A status row's block, and the grey line under it where there is one.</summary>
public sealed record StatusText(string Block, string? Second = null);

/// <summary>
/// The text of the status rows under Core and character: a count of what has happened or exists, never the whole list.
/// </summary>
public static class StatusRows
{
    public static StatusText Introductions(int introduced) => introduced switch
    {
        0 => new StatusText("No core has introduced itself yet"),
        1 => new StatusText("Every core introduces itself again", "1 has so far"),
        _ => new StatusText("Every core introduces itself again", $"{introduced} have so far"),
    };

    public static string OwnCores(IReadOnlyList<string> names) =>
        names.Count == 0
            ? "None yet. A core needs a name and a paragraph saying what it is like."
            : "";
}
