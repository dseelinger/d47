using D47.Core.Journal;
using D47.Core.Knowledge;

namespace D47.Core.Ships;

/// <summary>
/// Whether a figure was read off the game or worked out from the shipped tables
/// (Phase 38).
/// <para>
/// <b>The distinction is the feature, not a caveat on it.</b> <c>ShipsMode.Parted</c> states that
/// the effects on a slot row are the fitted module's own and never a plan's, because showing
/// modelled figures beside measured ones is the failure the specification table is built the way
/// it is to avoid. That rule is right about a slot row and wrong about a design gauge: a power
/// budget the Commander cannot see until after the materials are spent is not a budget. Phase 38
/// narrows it to these two gauges and pays for the narrowing by keeping the two kinds apart
/// everywhere they meet.
/// </para>
/// </summary>
public enum FigureKind
{
    /// <summary>Elite reported it. The <c>Loadout</c> event, or <c>ModulesInfo.json</c>.</summary>
    Measured,

    /// <summary>
    /// d47 worked it out, because the Commander is planning something they have not bought or
    /// rolled yet. Exact for a <em>finished</em> grade — see <see cref="RollModel"/>.
    /// </summary>
    Modelled,
}

/// <summary>
/// What a build draws against what its plant makes, retracted and deployed
/// (Phase 38).
/// </summary>
/// <param name="Retracted">Megawatts drawn with the hardpoints in.</param>
/// <param name="Deployed">
/// Megawatts drawn with them out, which is the figure a build has to fit inside. Never less than
/// <paramref name="Retracted"/>: the split is which modules are counted, not two different sums.
/// </param>
/// <param name="Capacity">What the plant makes, or null for a build with no plant d47 can see.</param>
/// <param name="Kind">Measured or modelled. See <see cref="FigureKind"/>.</param>
public sealed record PowerGauge(double Retracted, double Deployed, double? Capacity, FigureKind Kind)
{
    /// <summary>The retracted draw as a fraction of capacity, or null with no plant.</summary>
    public double? RetractedShare => Capacity is > 0 ? Retracted / Capacity : null;

    /// <summary>The deployed draw as a fraction of capacity, or null with no plant.</summary>
    public double? DeployedShare => Capacity is > 0 ? Deployed / Capacity : null;

    /// <summary>
    /// Megawatts over the plant's output with the hardpoints out, or null for a build that fits.
    /// <para>
    /// <b>The number the Commander acts on.</b> "104%" says a build is over and "0.31 MW over"
    /// says by how much, which is what decides whether the answer is a bigger plant or one fewer
    /// shield booster. Asked for on 2026-08-20: <i>"both a percent and MW overage (if any) and
    /// I'll deal with it in-game"</i>.
    /// </para>
    /// </summary>
    public double? Overage => Capacity is { } made && Deployed > made ? Deployed - made : null;

    /// <summary>Whether the build fits with its hardpoints out, which is the question.</summary>
    public bool Fits => Capacity is not { } made || Deployed <= made;
}

/// <summary>
/// A jump range at three masses, because a Commander flies at all three
/// (Phase 38).
/// </summary>
/// <param name="Best">
/// Unladen plus <b>one jump's fuel</b>, which is Frontier's own <c>MaxJumpRange</c> — measured
/// across 2,876 <c>Loadout</c> events at a median error of 0.000%. "Empty fuel" is not a state
/// anybody jumps in, so this is the honest top of the range and it is checkable in-game.
/// </param>
/// <param name="Middle">Unladen plus a full tank: what the first jump of a trip actually does.</param>
/// <param name="Worst">Full tank and every rack full.</param>
/// <param name="Kind">Measured or modelled. See <see cref="FigureKind"/>.</param>
public sealed record JumpGauge(double Best, double Middle, double Worst, FigureKind Kind);

/// <summary>
/// The two gauges at the head of a ship's slot list, and what d47 could not work out
/// (Phase 38).
/// </summary>
/// <param name="Power">The power budget, or null where there is no build to total.</param>
/// <param name="Jump">The jump range, or null where no drive can be found.</param>
/// <param name="Unmodelled">
/// How many slots carry a plan too vague to cost. <b>Counted rather than ignored</b>: "a pulse
/// laser, I do not mind which" is a real plan and it has no mass and no power draw, so those
/// slots contribute what is <em>fitted</em> and the gauge says how many did. A gauge that quietly
/// dropped them would read as a complete answer to a question it had not finished.
/// </param>
/// <param name="Silent">
/// Why there are no gauges at all, in a sentence, or null when there are. A ship d47 has never
/// been inside has no modules to total, and saying so is the answer — drawing a figure it cannot
/// stand behind is not.
/// </param>
public sealed record BuildGauges(PowerGauge? Power, JumpGauge? Jump, int Unmodelled, string? Silent)
{
    /// <summary>Nothing to draw, and the reason.</summary>
    public static BuildGauges Nothing(string why) => new(null, null, 0, why);

    /// <summary>Whether anything is worth drawing.</summary>
    public bool IsEmpty => Power is null && Jump is null;
}

/// <summary>
/// The arithmetic behind the two gauges (Phase 38, "A build you can watch").
/// <para>
/// <b>Pure, and in Core, because it is checkable.</b> Both figures are functions of the shipped
/// tables and one <c>Loadout</c> event, so the jump half was validated by replaying every
/// <c>Loadout</c> in the 918-journal corpus and comparing against the figure Frontier wrote —
/// which is a thing a panel cannot do and a static method can. See
/// <c>docs/spikes/build-gauges.md</c>.
/// </para>
/// <para>
/// <b>What it will not do.</b> No damage, no shields, no DPS: a build that needs more than two
/// numbers belongs in Coriolis, and d47 pretending otherwise would be an outfitting simulator
/// nobody asked for.
/// </para>
/// </summary>
public static class ShipGauges
{
    /// <summary>
    /// What a Commander is told when the ship has never been boarded — the same fact
    /// <c>ShipsMode.Flying</c> already reports, in the shorter form a gauge has room for.
    /// </summary>
    public const string Unseen =
        "I have not been inside this ship, so I cannot total what is in it.";

    /// <summary>
    /// Both gauges for one build.
    /// </summary>
    /// <param name="build">The plans. Slots with none fall through to what is fitted.</param>
    /// <param name="seen">
    /// The last <c>Loadout</c> for this ship, live or remembered, or null for one d47 has never
    /// been inside.
    /// </param>
    /// <param name="measured">
    /// Per-slot draw from <c>ModulesInfo.json</c>, keyed by slot, where the file has been read for
    /// <em>this</em> ship. Elite computes those with the engineering already in them, so they win
    /// over the table wherever both have an answer — and where they disagree that is reported
    /// rather than averaged away.
    /// </param>
    public static BuildGauges Read(
        ShipBuild build,
        ShipLoadout? seen,
        IReadOnlyDictionary<string, double>? measured = null)
    {
        var parts = Parts(build, seen);

        if (parts.Count == 0)
        {
            return BuildGauges.Nothing(seen is null ? Unseen : "There is nothing in this ship yet.");
        }

        var modelled = parts.Any(part => part.IsPlanned);
        var kind = modelled ? FigureKind.Modelled : FigureKind.Measured;

        return new BuildGauges(
            Power(parts, kind, measured),
            Jump(build, parts, seen, kind, modelled),
            parts.Count(part => part.IsVague),
            null);
    }

    /// <summary>
    /// Every slot that has something in it or planned for it, as one list.
    /// <para>
    /// <b>The hull's layout leads where there is one</b>, exactly as the slot list itself does, so
    /// a planned module in an empty socket counts. Where there is no layout — the state a hull
    /// Frontier has just shipped is in for a release or two — the fitted modules are the list, and
    /// the cosmetics fall out of it on their own by having no row in the specification table.
    /// </para>
    /// </summary>
    private static List<Part> Parts(ShipBuild build, ShipLoadout? seen)
    {
        var fitted = (seen?.Modules ?? [])
            .GroupBy(module => module.Slot, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);

        var layout = EliteSpecifications.Slots(build.Hull);

        var names = layout.Count > 0
            ? layout.Select(slot => slot.Name)
            : fitted.Keys.Concat(build.Slots.Select(plan => plan.Slot)).Distinct(StringComparer.OrdinalIgnoreCase);

        var parts = new List<Part>();

        foreach (var name in names)
        {
            var plan = build.For(name);
            var module = fitted.GetValueOrDefault(name);

            // What the plan asks for, where it asks for a module exactly enough to look up. A
            // plan naming a *kind* — "a pulse laser, I do not mind which" — has no figures at
            // all, so the slot keeps what is in it and is counted as unmodelled.
            var wanted = plan?.Variant is { Length: > 0 } variant
                ? EliteSpecifications.Module(variant)
                : null;

            var vague = plan?.Module is { Length: > 0 } && wanted is null;

            var spec = wanted ?? EliteSpecifications.Module(module?.Item);

            if (spec is null)
            {
                continue;
            }

            // Planned figures where the plan changes the module or asks for a roll; the fitted
            // module's own where it does neither.
            var planned = wanted is not null
                          || (!vague && plan is { Blueprint.Length: > 0 })
                          || (!vague && plan is { Experimental.Length: > 0 });

            parts.Add(new Part(name, spec, module, planned ? plan : null, vague));
        }

        return parts;
    }

    /// <summary>
    /// Draw summed twice — with the hardpoints in and with them out — against what the plant makes.
    /// <para>
    /// <b>Retracted and deployed need no new data.</b> It falls out of the first letter of
    /// <see cref="ModuleSpecification.Type"/>: <c>h*</c> is a hardpoint weapon and draws only
    /// while it is deployed, and <c>u*</c>, <c>c*</c> and <c>i*</c> draw all the time. That is the
    /// distinction hardest to keep in your head by hand — a shield booster sits in a
    /// <c>TinyHardpoint</c> slot and is <c>usb</c>, so it draws whether or not the guns are out.
    /// </para>
    /// <para>
    /// <b>A blank draw means zero and that is correct rather than convenient.</b> The 350 modules
    /// with no <c>power</c> column are bulkheads, plants, cargo racks, cabins, reinforcement and
    /// fuel tanks, every one of which genuinely draws nothing.
    /// </para>
    /// </summary>
    private static PowerGauge? Power(
        List<Part> parts, FigureKind kind, IReadOnlyDictionary<string, double>? measured)
    {
        double retracted = 0, deployed = 0;
        double? capacity = null;

        foreach (var part in parts)
        {
            if (part.Spec.PowerCapacity is not null)
            {
                capacity = (capacity ?? 0) + part.Figure(
                    "PowerCapacity", "Power Generation", part.Spec.PowerCapacity);

                continue;
            }

            // Elite's own figure for this slot wins where it has one — it is computed by the game
            // with the engineering already in it. Never for a planned slot: the file describes
            // what is on the hull, and a plan is by definition not that yet.
            var draw = part.IsPlanned || measured is null || !measured.TryGetValue(part.Slot, out var live)
                ? part.Figure("PowerDraw", "Power Draw", part.Spec.Power) ?? 0
                : live;

            deployed += draw;

            if (part.Spec.Type is not { Length: > 0 } type || char.ToLowerInvariant(type[0]) != 'h')
            {
                retracted += draw;
            }
        }

        return deployed == 0 && capacity is null
            ? null
            : new PowerGauge(retracted, deployed, capacity, kind);
    }

    /// <summary>
    /// The three needles.
    /// <para>
    /// <c>(max_fuel / fuel_multiplier) ^ (1 / fuel_power) × optimal_mass / mass</c>, plus a
    /// Guardian booster's flat bonus. Every constant is in the drive's own row; the mass is the
    /// hull's plus everything in it.
    /// </para>
    /// <para>
    /// <b>Measured mass where nothing is planned.</b> A <c>Loadout</c> carries <c>UnladenMass</c>,
    /// <c>FuelCapacity</c> and <c>CargoCapacity</c> outright, and using them keeps the best needle
    /// equal to the figure the Commander can read in the outfitting screen. Summing the table
    /// reproduces all three — unladen mass to a median 0.000% over 2,392 comparable events — which
    /// is what makes the modelled half trustworthy when a plan means there is nothing to read.
    /// </para>
    /// </summary>
    private static JumpGauge? Jump(
        ShipBuild build, List<Part> parts, ShipLoadout? seen, FigureKind kind, bool modelled)
    {
        var drive = parts.FirstOrDefault(part => part.Spec.IsDrive);

        if (drive is null)
        {
            return null;
        }

        var optimal = drive.Figure("FSDOptimalMass", "Optimal Mass", drive.Spec.OptimalMass);
        var perJump = drive.Figure("MaxFuelPerJump", "Max Fuel Per Jump", drive.Spec.MaxFuelPerJump);

        if (optimal is not { } optimalMass
            || perJump is not { } fuelPerJump
            || drive.Spec.FuelPower is not { } power
            || drive.Spec.FuelMultiplier is not { } multiplier
            || fuelPerJump <= 0 || multiplier <= 0 || power == 0)
        {
            return null;
        }

        // Flat, and added after the division. Every one of the 19 events in the corpus where the
        // formula misses is a Frontier-side inconsistency in the other direction: their own figure
        // omits this bonus, always with an SCO drive fitted. See docs/spikes/build-gauges.md.
        var boost = parts.Sum(part => part.Spec.JumpBoost ?? 0);

        var unladen = !modelled && seen?.UnladenMass is { } reported
            ? reported
            : Mass(build, parts);

        var tank = !modelled && seen?.FuelCapacity is { } fuel ? fuel : Capacity(parts, "cft");
        var hold = !modelled && seen?.CargoCapacity is { } cargo ? cargo : Capacity(parts, "icr");

        if (unladen is not { } empty || empty <= 0)
        {
            return null;
        }

        double At(double mass) =>
            Math.Pow(fuelPerJump / multiplier, 1 / power) * optimalMass / mass + boost;

        return new JumpGauge(
            At(empty + fuelPerJump),
            At(empty + Math.Max(tank, fuelPerJump)),
            At(empty + Math.Max(tank, fuelPerJump) + hold),
            kind);
    }

    /// <summary>
    /// The hull plus everything in it, for a build there is no measured mass for.
    /// <para>
    /// Null where the hull itself has no row — four hulls have slots and no figures — because a
    /// jump range computed off a mass that is missing its hull would be confidently wrong rather
    /// than absent.
    /// </para>
    /// </summary>
    private static double? Mass(ShipBuild build, List<Part> parts)
    {
        if (EliteSpecifications.Ship(build.Hull)?.HullMass is not { } hull)
        {
            return null;
        }

        return parts.Aggregate(
            (double)hull,
            (total, part) => total + (part.Figure("Mass", "Mass", part.Spec.Mass) ?? 0));
    }

    /// <summary>Everything one kind of module holds — <c>cft</c> is a fuel tank, <c>icr</c> a rack.</summary>
    private static double Capacity(List<Part> parts, string type) =>
        parts
            .Where(part => string.Equals(part.Spec.Type, type, StringComparison.OrdinalIgnoreCase))
            .Sum(part => part.Spec.Figure("capacity") ?? 0);

    /// <summary>
    /// One slot as the gauges see it: a module, and either what Elite reported about it or what
    /// the Commander wants done to it.
    /// </summary>
    /// <param name="Slot">The journal's own name for it, which is how the measured file is keyed.</param>
    /// <param name="Spec">The module in the slot, planned where a plan names one exactly.</param>
    /// <param name="Fitted">What Elite reported here, or null for an empty or unseen slot.</param>
    /// <param name="Plan">The plan whose figures this slot takes, or null to take the fitted ones.</param>
    /// <param name="IsVague">Whether a plan named a module too loosely to look up.</param>
    private sealed record Part(
        string Slot,
        ModuleSpecification Spec,
        ShipModule? Fitted,
        SlotPlan? Plan,
        bool IsVague)
    {
        public bool IsPlanned => Plan is not null;

        /// <summary>
        /// One figure for this slot: modelled from the plan, measured off the roll, or stock.
        /// <para>
        /// <b>In that order, and the order is the whole rule.</b> A slot the Commander is planning
        /// answers with what the roll would land on; a slot they are not answers with what Elite
        /// says it actually did; a module nobody has touched answers with its own row.
        /// </para>
        /// </summary>
        /// <param name="label">The <c>Modifiers</c> label Elite writes — <c>PowerDraw</c>.</param>
        /// <param name="attribute">The blueprint table's name for the same thing — "Power Draw".</param>
        /// <param name="stock">The unengineered figure off the module's own row.</param>
        public double? Figure(string label, string attribute, double? stock)
        {
            if (Plan is { } plan)
            {
                return RollModel.Apply(stock, attribute, Spec, plan.Blueprint, plan.Grade, plan.Experimental);
            }

            foreach (var modifier in Fitted?.Modifiers ?? [])
            {
                if (string.Equals(modifier.Label, label, StringComparison.OrdinalIgnoreCase)
                    && modifier.Value is { } value)
                {
                    return value;
                }
            }

            return stock;
        }
    }
}
