namespace D47.Core.Persona;

/// <summary>A status row's block, and the grey line under it where there is one.</summary>
public sealed record StatusText(string Block, string? Second = null);

/// <summary>
/// The text of the status rows under Core and character: a count, the first three items in the list's
/// order, and a count of the rest. Never the whole list.
/// </summary>
public static class StatusRows
{
    public const int Named = 3;

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

    public const string NoShipHasACore =
        "No ship has its own core. Until one does, changing ship changes nothing about who answers you.";

    public const string FleetNotRead = "Dock somewhere with a shipyard to read the rest.";

    /// <param name="bound">One entry per bound ship, in the order the fleet lists them.</param>
    /// <param name="fleet">How many ships the fleet holds, or null where it has not been read.</param>
    public static StatusText ShipCores(IReadOnlyList<string> bound, int? fleet)
    {
        if (bound.Count == 0)
        {
            return new StatusText(NoShipHasACore);
        }

        var has = bound.Count == 1 ? "has" : "have";

        return fleet is { } total
            ? new StatusText($"{bound.Count} of {total} {(total == 1 ? "ship" : "ships")} {has} a core: {FirstThree(bound)}")
            : new StatusText($"{bound.Count} {(bound.Count == 1 ? "ship" : "ships")} {has} a core: {FirstThree(bound)}", FleetNotRead);
    }

    /// <summary>"A", "A and B", "A, B and C", then "A, B, C and 4 more".</summary>
    public static string FirstThree(IReadOnlyList<string> items)
    {
        if (items.Count <= Named)
        {
            return items.Count switch
            {
                0 => string.Empty,
                1 => items[0],
                _ => $"{string.Join(", ", items.Take(items.Count - 1))} and {items[^1]}",
            };
        }

        return $"{string.Join(", ", items.Take(Named))} and {items.Count - Named} more";
    }
}
