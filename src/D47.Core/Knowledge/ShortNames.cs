namespace D47.Core.Knowledge;

/// <summary>
/// d47's own short names for modules, so a slot row can carry two columns
/// (docs/plans/change-requests.md 38).
/// <para>
/// <b>This table is this repository's, and that is the whole reason it can exist.</b> Module names
/// are Frontier's and are derived by a generator with its provenance recorded; <c>HRP</c> is not a
/// name Frontier publishes, it is what a Commander writes when the column is too narrow for
/// <i>Hull Reinforcement Package</i>. So this is hand-written on purpose and is the one table here
/// that is allowed to be — which is a lighter licence question than the specification data rather
/// than the same one answered differently.
/// </para>
/// <para>
/// <b>An initialism where it is unique, and truncated words where it would collide</b> (the
/// Commander's ruling, 2026-08-25). <c>PD</c> is Point Defence and it is also the Power
/// Distributor, and a rule to remember which is a rule a Commander should not have to hold: so
/// both become <b>Point Def.</b> and <b>Power Dist.</b> and the collision does not arise.
/// <c>ANoTwoModulesShareAShortNameTests</c> asserts that across every module in the specification
/// table, so a future entry cannot bring it back quietly.
/// </para>
/// <para>
/// <b>A short name is never the only name.</b> The long form stays on the row's tooltip and is
/// written out in full on the slot drill, which is the same test the glyph work of 2026-08-24
/// passed: a shorter thing is an improvement only while the word it replaced is still reachable.
/// </para>
/// </summary>
public static class ShortNames
{
    /// <summary>
    /// The table, read longest key first — see <see cref="Ordered"/> — so that a name containing
    /// another is matched by its own entry rather than by the shorter one inside it.
    /// <para>
    /// Matched as a <b>substring</b> rather than as the whole string, because the name reaching
    /// here is composed — <c>6D Hull Reinforcement Package</c>, <c>3E Pulse Laser, gimballed</c> —
    /// and the size, the rating and the mount are not this table's business. It also means one
    /// entry covers the variants: <i>Guardian Hybrid Power Distributor</i> and <i>Power
    /// Distributor (free)</i> both come out of the <c>Power Distributor</c> row.
    /// </para>
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

    /// <summary>
    /// The table by longest name first, so the order it is <em>written</em> in cannot matter.
    /// <para>
    /// <b>Sorted rather than hand-ordered on purpose.</b> <i>Prismatic Shield Generator</i> has to
    /// beat <i>Shield Generator</i> and <i>Frame Shift Drive Interdictor</i> has to beat <i>Frame
    /// Shift Drive</i>; leaving that to whoever adds the next row is a defect waiting for a
    /// careless afternoon, and it would show up as one module quietly wearing another's name.
    /// </para>
    /// </summary>
    private static readonly (string Long, string Short)[] Ordered =
        [.. Table.OrderByDescending(entry => entry.Long.Length)];

    /// <summary>
    /// The two families where the pattern says it rather than a row per member. Applied only when
    /// the table above has nothing, so a named entry always wins.
    /// </summary>
    private static readonly (string Ending, string Instead)[] Endings =
    [
        (" Multi-Limpet Controller", " Multi-Limpet"),
        (" Multi Limpet Controller", " Multi-Limpet"),
        (" Limpet Controller", " Limpet"),
        (" Class Passenger Cabin", " Cabin"),
    ];

    /// <summary>
    /// The short form, or the name unchanged where there is nothing shorter worth saying.
    /// <para>
    /// <b>Most modules are not in the table and that is the design.</b> Abbreviating everything
    /// buys width and spends legibility, and a column only needs to be as narrow as its longest
    /// row: <i>Pulse Laser</i>, <i>Cargo Rack</i> and <i>Life Support</i> are already short and an
    /// initialism for one of them is a puzzle where a name used to be.
    /// </para>
    /// </summary>
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
    /// The blueprint with the module struck off the end of it, where the row already says which
    /// module this is (docs/plans/change-requests.md 38).
    /// <para>
    /// <b>The module is usually said twice and neither saying is short.</b> <i>Heavy Duty Hull
    /// Reinforcement</i> sits on a row already reading <i>Hull Reinforcement Package</i>, so the
    /// blueprint becomes <b>Heavy Duty</b> — shorter, and <b>comparable down the column</b>, which
    /// is the part that is worth more than the width: "this whole ship is Heavy Duty" becomes
    /// something an eye can see rather than something to be worked out line by line.
    /// </para>
    /// <para>
    /// <b>Only off the end, and only the module this row is about.</b> <i>Increased FSD Range</i>
    /// keeps every word on a Frame Shift Drive, because the drive is not the last thing it says —
    /// the range is, and dropping it would leave a blueprint name that means something else.
    /// </para>
    /// </summary>
    /// <param name="blueprint">The blueprint's readable name.</param>
    /// <param name="module">
    /// The module the row names, in Frontier's own words. Null where the slot is empty and there
    /// is nothing being said twice.
    /// </param>
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

            // A blueprint that is *only* the module's name keeps it: "Hull Reinforcement" alone
            // says nothing about the roll, and an empty cell would read as no roll at all.
            if (kept.Length > 0)
            {
                return kept;
            }
        }

        return blueprint;
    }

    /// <summary>
    /// The spellings of a module that a blueprint might end with: its name, its name without the
    /// generic word some of them carry, and its short form — <c>Shielded FSD</c> ends with the
    /// short one and nothing else.
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
