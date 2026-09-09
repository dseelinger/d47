namespace D47.Core.Knowledge;

/// <summary>The four kinds of slot the outfitting screen has (remediation.md 12, items 1 and 6).</summary>
public enum ShipSlotKind
{
    /// <summary>Weapons.</summary>
    Hardpoint,

    /// <summary>Shield boosters, heat sinks, scanners.</summary>
    Utility,

    /// <summary>Armour and the seven sockets a ship cannot fly without.</summary>
    Core,

    /// <summary>The compartments, `Slot01_Size6` and the restricted ones beside them.</summary>
    Optional,
}

/// <summary>One slot of one hull, as the journal names it (remediation.md 12, item 3).</summary>
/// <param name="Hull">The hull symbol, lower case, as <c>Loadout</c> writes it.</param>
/// <param name="Name">The slot name, spelled exactly as the journal spells it.</param>
/// <param name="Kind">Which block of the outfitting screen it belongs to.</param>
/// <param name="Size">The class of module it takes. 0 for a utility mount.</param>
/// <param name="Restrict">
/// The module types this slot accepts, where it accepts only some — a military compartment, a Panther's
/// cargo-only holds, a Prospector's limpet bay.
/// </param>
public sealed record ShipSlot(
    string Hull,
    string Name,
    ShipSlotKind Kind,
    int Size,
    IReadOnlyList<string> Restrict)
{
    /// <summary>The heading this slot sits under, in the outfitting screen's own words.</summary>
    public static string Heading(ShipSlotKind kind) => kind switch
    {
        ShipSlotKind.Hardpoint => "Hardpoints",
        ShipSlotKind.Utility => "Utility Mounts",
        ShipSlotKind.Core => "Core Internal",
        _ => "Optional Internal",
    };

    /// <summary>The slot as a Commander would say it: the size and the ordinal, rather than the symbol.</summary>
    public string Describe() => Kind switch
    {
        ShipSlotKind.Core or ShipSlotKind.Hardpoint => Spaced(Name),
        ShipSlotKind.Utility => Ordinal(Name) is { } utility
            ? $"Utility Mount {utility}"
            : Spaced(Name),

        // The size is in the compartment's own name — `Slot01_Size6` — so it is read off the part before the
        // underscore and then said once, in words, rather than twice in Frontier's spelling.
        _ when Ordinal(Trim(Name)) is { } compartment => Restrict.Count > 0
            ? $"{Spaced(Letters(Trim(Name)))} {compartment} (size {Size})"
            : $"Compartment {compartment} (size {Size})",

        _ => Spaced(Name),
    };

    /// <summary>
    /// The same slot in a column narrow enough to sit beside two others (docs/plans/change-requests.md
    /// 38).
    /// </summary>
    public string Short() => Kind switch
    {
        ShipSlotKind.Core => ShortNames.Of(Spaced(Name)),

        // "Large Hardpoint 1" under a heading that already says Hardpoints, so: "Large 1".
        ShipSlotKind.Hardpoint => Spaced(Name).Replace("Hardpoint ", string.Empty, StringComparison.Ordinal),

        ShipSlotKind.Utility => Ordinal(Name) is { } utility
            ? utility.ToString(System.Globalization.CultureInfo.InvariantCulture)
            : Spaced(Name),

        _ when Ordinal(Trim(Name)) is { } compartment => Restrict.Count > 0
            ? $"{Spaced(Letters(Trim(Name)))} {compartment} ({Size})"
            : $"{compartment} ({Size})",

        _ => Spaced(Name),
    };

    /// <summary>The trailing number, where the name carries one.</summary>
    private static int? Ordinal(string name)
    {
        var at = name.Length;

        while (at > 0 && char.IsAsciiDigit(name[at - 1]))
        {
            at--;
        }

        return at < name.Length && int.TryParse(name[at..], out var value) ? value : null;
    }

    /// <summary>`Military01` back to `Military`, so the ordinal is not printed twice.</summary>
    private static string Letters(string name) =>
        name.TrimEnd('0', '1', '2', '3', '4', '5', '6', '7', '8', '9');

    /// <summary>`Slot01_Size6` back to `Slot01`, so the size is said once rather than twice.</summary>
    private static string Trim(string name)
    {
        var at = name.IndexOf('_');

        return at < 0 ? name : name[..at];
    }

    /// <summary>`MainEngines` to "Main Engines", `LargeHardpoint1` to "Large Hardpoint 1".</summary>
    private static string Spaced(string name)
    {
        var said = new System.Text.StringBuilder(name.Length + 4);

        for (var at = 0; at < name.Length; at++)
        {
            var character = name[at];

            if (at > 0
                && (char.IsAsciiLetterUpper(character) || char.IsAsciiDigit(character))
                && !char.IsAsciiDigit(name[at - 1])
                && name[at - 1] != ' ')
            {
                said.Append(' ');
            }

            said.Append(character == '_' ? ' ' : character);
        }

        return said.ToString();
    }
}
