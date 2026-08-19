namespace D47.Core.Knowledge;

/// <summary>
/// The four kinds of slot the outfitting screen has (remediation.md 12, items 1 and 6).
/// <para>
/// <b>Frontier's grouping, not d47's.</b> A Commander reads a module list looking for the block
/// they already know from the outfitting screen — hardpoints, then utility mounts, then the core
/// internals, then the optional ones — and a flat list of thirty-odd journal slot names in
/// whatever order the game happened to write them is not that list.
/// </para>
/// <para>
/// A <c>TinyHardpoint</c> is a <see cref="Utility"/> and not a <see cref="Hardpoint"/>. The
/// journal's name for it says hardpoint and the game's does not, and the game's is the one the
/// Commander is holding in their head.
/// </para>
/// </summary>
public enum ShipSlotKind
{
    /// <summary>Weapons. The journal's `SmallHardpoint1` up to `HugeHardpoint1`.</summary>
    Hardpoint,

    /// <summary>Shield boosters, heat sinks, scanners. Every `TinyHardpoint`.</summary>
    Utility,

    /// <summary>Armour and the seven sockets a ship cannot fly without.</summary>
    Core,

    /// <summary>The compartments, `Slot01_Size6` and the restricted ones beside them.</summary>
    Optional,
}

/// <summary>
/// One slot of one hull, as the journal names it (remediation.md 12, item 3).
/// <para>
/// <b>This is the only source for a slot that is empty.</b> A <c>Loadout</c> event lists fitted
/// modules and nothing else, so without a table of what a hull has, an empty hardpoint does not
/// exist as far as anything downstream is concerned — which is exactly what a Commander looking
/// for somewhere to put a weapon wants to see.
/// </para>
/// </summary>
/// <param name="Hull">The hull symbol, lower case, as <c>Loadout</c> writes it.</param>
/// <param name="Name">
/// The slot name, spelled exactly as the journal spells it. <b>The identity</b>: a plan is keyed
/// on this, so it has to be the game's own string rather than anything derived that merely looks
/// like one.
/// </param>
/// <param name="Kind">Which block of the outfitting screen it belongs to.</param>
/// <param name="Size">The class of module it takes. 0 for a utility mount.</param>
/// <param name="Restrict">
/// The module types this slot accepts, where it accepts only some — a military compartment, a
/// Panther's cargo-only holds, a Prospector's limpet bay. Empty means everything its kind allows.
/// </param>
public sealed record ShipSlot(
    string Hull,
    string Name,
    ShipSlotKind Kind,
    int Size,
    IReadOnlyList<string> Restrict)
{
    /// <summary>
    /// The heading this slot sits under, in the outfitting screen's own words.
    /// <para>
    /// Singular "Core Internal" and "Optional Internal" rather than plurals, because that is what
    /// the game calls the sections and a Commander comparing the two screens should not have to
    /// translate.
    /// </para>
    /// </summary>
    public static string Heading(ShipSlotKind kind) => kind switch
    {
        ShipSlotKind.Hardpoint => "Hardpoints",
        ShipSlotKind.Utility => "Utility Mounts",
        ShipSlotKind.Core => "Core Internal",
        _ => "Optional Internal",
    };

    /// <summary>
    /// The slot as a Commander would say it: the size and the ordinal, rather than the symbol.
    /// <para>
    /// <c>Slot01_Size6</c> is the journal's identity for a compartment and is nobody's name for
    /// one. The size is the part that decides what fits, so it leads.
    /// </para>
    /// </summary>
    public string Describe() => Kind switch
    {
        ShipSlotKind.Core or ShipSlotKind.Hardpoint => Spaced(Name),
        ShipSlotKind.Utility => Ordinal(Name) is { } utility
            ? $"Utility Mount {utility}"
            : Spaced(Name),

        // The size is in the compartment's own name — `Slot01_Size6` — so it is read off the
        // part before the underscore and then said once, in words, rather than twice in
        // Frontier's spelling.
        _ when Ordinal(Trim(Name)) is { } compartment => Restrict.Count > 0
            ? $"{Spaced(Letters(Trim(Name)))} {compartment} (size {Size})"
            : $"Compartment {compartment} (size {Size})",

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
