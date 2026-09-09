namespace D47.Core.Knowledge;

/// <summary>
/// d47's own short names for modules, so a slot row can carry two columns
/// (docs/plans/change-requests.md 38).
/// </summary>
public static class ShortNames
{
    /// <summary>
    /// The table, read longest key first — see <see cref="Ordered"/> — so that a name containing
    /// another is matched by its own entry rather than by the shorter one inside it.
    /// </summary>
    private static readonly (string Long, string Short)[] Table =
    [
        ("Sub-Surface Displacement Missile", "Sub-Surface Displacement"),
        ("Sub-Surface Extraction Missile", "Sub-Surface Extraction"),
        ("Remote Release Flechette Launcher", "Flechette Launcher"),
        ("Experimental Weapon Stabiliser", "Weapon Stabiliser"),
        ("Intermediate Discovery Scanner", "IDS"),
        ("Corrosion Resistant Cargo Rack", "Corrosion Cargo Rack"),
        ("Enhanced Performance Thrusters", "Enhanced Thrusters"),
        ("Meta Alloy Hull Reinforcement", "Meta Alloy HRP"),
        ("Guardian Hull Reinforcement", "Guardian HRP"),
        ("Guardian Module Reinforcement", "Guardian MRP"),
        ("Guardian Shield Reinforcement", "Guardian SRP"),
        ("Shutdown Field Neutraliser", "Shutdown Neutraliser"),
        ("Remote Release Flak Launcher", "Flak Launcher"),
        ("Module Reinforcement Package", "MRP"),
        ("Advanced Discovery Scanner", "ADS"),
        ("Electronic Countermeasure", "ECM"),
        ("Auto Field-Maintenance Unit", "AFMU"),
        ("Hull Reinforcement Package", "HRP"),
        ("Prismatic Shield Generator", "Prismatic Shield Gen."),
        ("Bi-Weave Shield Generator", "Bi-Weave Shield Gen."),
        ("Detailed Surface Scanner", "DSS"),
        ("Planetary Vehicle Hangar", "PVH"),
        ("Planetary Approach Suite", "PAS"),
        ("Basic Discovery Scanner", "BDS"),
        ("Frame Shift Drive Interdictor", "FSD Interdictor"),
        ("Frame Shift Wake Scanner", "Wake Scanner"),
        ("Kill Warrant Scanner", "KWS"),
        ("Pulse Wave Analyser", "Pulse Wave"),
        ("Plasma Accelerator", "Plasma Acc."),
        ("Shield Generator", "Shield Gen."),
        ("Power Distributor", "Power Dist."),
        ("Frame Shift Drive", "FSD"),
        ("Fragment Cannon", "Frag Cannon"),
        ("Caustic Sink Launcher", "Caustic Sink"),
        ("Heat Sink Launcher", "Heat Sink"),
        ("Point Defence", "Point Def."),
        ("Chaff Launcher", "Chaff"),
        ("Shield Cell Bank", "SCB"),
        ("Shield Booster", "SB"),
    ];

    /// <summary>The table by longest name first, so the order it is written in cannot matter.</summary>
    private static readonly (string Long, string Short)[] Ordered =
        [.. Table.OrderByDescending(entry => entry.Long.Length)];

    /// <summary>The two families where the pattern says it rather than a row per member.</summary>
    private static readonly (string Ending, string Instead)[] Endings =
    [
        (" Multi-Limpet Controller", " Multi-Limpet"),
        (" Multi Limpet Controller", " Multi-Limpet"),
        (" Limpet Controller", " Limpet"),
        (" Class Passenger Cabin", " Cabin"),
    ];

    /// <summary>The short form, or the name unchanged where there is nothing shorter worth saying.</summary>
    public static string Of(string? name)
    {
        if (name is not { Length: > 0 })
        {
            return string.Empty;
        }

        foreach (var (whole, said) in Ordered)
        {
            var at = name.IndexOf(whole, StringComparison.OrdinalIgnoreCase);

            if (at >= 0)
            {
                return string.Concat(name.AsSpan(0, at), said, name.AsSpan(at + whole.Length));
            }
        }

        foreach (var (ending, instead) in Endings)
        {
            if (name.EndsWith(ending, StringComparison.OrdinalIgnoreCase))
            {
                return string.Concat(name.AsSpan(0, name.Length - ending.Length), instead);
            }
        }

        return name;
    }

    /// <summary>
    /// The blueprint with the module struck off the end of it, where the row already says which module
    /// this is (docs/plans/change-requests.md 38).
    /// </summary>
    /// <param name="blueprint">The blueprint's readable name.</param>
    /// <param name="module">The module the row names, in Frontier's own words.</param>
    public static string? Bare(string? blueprint, string? module)
    {
        if (blueprint is not { Length: > 0 } || module is not { Length: > 0 })
        {
            return blueprint;
        }

        foreach (var tail in Tails(module))
        {
            if (blueprint.Length <= tail.Length
                || !blueprint.EndsWith(tail, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var kept = blueprint[..^tail.Length].TrimEnd();

            // A blueprint that is *only* the module's name keeps it: "Hull Reinforcement" alone says nothing
            // about the roll, and an empty cell would read as no roll at all.
            if (kept.Length > 0)
            {
                return kept;
            }
        }

        return blueprint;
    }

    /// <summary>
    /// The spellings of a module that a blueprint might end with: its name, its name without the
    /// generic word some of them carry, and its short form — <c>Shielded FSD</c> ends with the short
    /// one and nothing else.
    /// </summary>
    private static IEnumerable<string> Tails(string module)
    {
        yield return module;

        foreach (var generic in new[] { " Package", " Unit" })
        {
            if (module.EndsWith(generic, StringComparison.OrdinalIgnoreCase))
            {
                yield return module[..^generic.Length];
            }
        }

        var brief = Of(module);

        if (!string.Equals(brief, module, StringComparison.Ordinal))
        {
            yield return brief;
        }
    }
}
