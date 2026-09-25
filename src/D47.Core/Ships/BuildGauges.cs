using D47.Core.Journal;
using D47.Core.Knowledge;

namespace D47.Core.Ships;

/// <summary>Whether a figure was read off the game or worked out from the shipped tables (Phase 38).</summary>
public enum FigureKind
{
    /// <summary>Elite reported it.</summary>
    Measured,

    /// <summary>
    /// d47 worked it out, because the Commander is planning something they have not bought or rolled
    /// yet.
    /// </summary>
    Modelled,
}

/// <summary>What one slot draws, and whether that figure was read off the game or worked out (#252).</summary>
public sealed record SlotDraw(double Megawatts, FigureKind Kind);

/// <summary>What a build draws against what its plant makes, retracted and deployed (Phase 38).</summary>
/// <param name="Retracted">Megawatts drawn with the hardpoints in.</param>
/// <param name="Deployed">
/// Megawatts drawn with them out, which is the figure a build has to fit inside.
/// </param>
/// <param name="Capacity">
/// What the plant makes, or null for a build with no plant d47 can see.
/// </param>
/// <param name="Kind">Measured or modelled.</param>
/// <param name="Draw">
/// What each slot that draws power contributes, keyed by the journal's slot name — the same figures
/// summed into <see cref="Deployed"/>, so a slot row and this gauge cannot disagree (#252). Carries no
/// entry for a power plant, a slot too vague to cost, or a module that draws nothing.
/// </param>
public sealed record PowerGauge(
    double Retracted, double Deployed, double? Capacity, FigureKind Kind, IReadOnlyDictionary<string, SlotDraw> Draw)
{
    /// <summary>The retracted draw as a fraction of capacity, or null with no plant.</summary>
    public double? RetractedShare => Capacity is > 0 ? Retracted / Capacity : null;

    /// <summary>The deployed draw as a fraction of capacity, or null with no plant.</summary>
    public double? DeployedShare => Capacity is > 0 ? Deployed / Capacity : null;

    /// <summary>
    /// Megawatts over the plant's output with the hardpoints out, or null for a build that fits.
    /// </summary>
    public double? Overage => Capacity is { } made && Deployed > made ? Deployed - made : null;

    /// <summary>Whether the build fits with its hardpoints out, which is the question.</summary>
    public bool Fits => Capacity is not { } made || Deployed <= made;

    /// <summary>
    /// Each priority group's cumulative deployed draw, 1 to 5, keyed by group — or null where a
    /// drawing slot's group could not be told, which happens only for a ship boarded before #253 and
    /// not boarded since (#253).
    /// </summary>
    public IReadOnlyDictionary<int, double>? Groups { get; init; }

    /// <summary>Each slot in <see cref="Draw"/>, in slot-layout order (#467).</summary>
    public IReadOnlyList<PowerModule> Modules { get; init; } = [];
}

/// <summary>A jump range at three masses, because a Commander flies at all three (Phase 38).</summary>
/// <param name="Best">
/// Unladen plus one jump's fuel, which is Frontier's own <c>MaxJumpRange</c> — measured across 2,876
/// <c>Loadout</c> events at a median error of 0.000%. "Empty fuel" is not a state anybody jumps in, so
/// this is the real top of the range and it is checkable in-game.
/// </param>
/// <param name="Middle">
/// Unladen plus a full tank: what the first jump of a trip actually does.
/// </param>
/// <param name="Worst">Full tank and every rack full.</param>
/// <param name="Kind">Measured or modelled.</param>
public sealed record JumpGauge(double Best, double Middle, double Worst, FigureKind Kind);

/// <summary>
/// The two gauges at the head of a ship's slot list, and what d47 could not work out (Phase 38).
/// </summary>
/// <param name="Power">The power budget, or null where there is no build to total.</param>
/// <param name="Jump">The jump range, or null where no drive can be found.</param>
/// <param name="Unmodelled">How many slots carry a plan too vague to cost.</param>
/// <param name="Silent">
/// Why there are no gauges at all, in a sentence, or null when there are.
/// </param>
public sealed record BuildGauges(PowerGauge? Power, JumpGauge? Jump, int Unmodelled, string? Silent)
{
    /// <summary>Nothing to draw, and the reason.</summary>
    public static BuildGauges Nothing(string why) => new(null, null, 0, why);

    /// <summary>Whether anything is worth drawing.</summary>
    public bool IsEmpty => Power is null && Jump is null;
}

/// <summary>The arithmetic behind the two gauges (Phase 38, "A build you can watch").</summary>
public static class ShipGauges
{
    /// <summary>
    /// What a Commander is told when the ship has never been boarded — the same fact
    /// <c>ShipsMode.Flying</c> already reports, in the shorter form a gauge has room for.
    /// </summary>
    public const string Unseen =
        "I have not been inside this ship, so I cannot total what is in it.";

    /// <summary>Both gauges for one build.</summary>
    /// <param name="build">The plans.</param>
    /// <param name="seen">
    /// The last <c>Loadout</c> for this ship, live or remembered, or null for one d47 has never been
    /// inside.
    /// </param>
    /// <param name="measured">
    /// Per-slot draw from <c>ModulesInfo.json</c>, keyed by slot, where the file has been read for this
    /// ship.
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
    /// The flown ship's laden jump range — full tank, full hold — or null where no drive can be found.
    /// </summary>
    public static double? LadenRange(ShipLoadout loadout)
    {
        if (!loadout.IsKnown)
        {
            return null;
        }

        var build = new ShipBuild(string.Empty, string.Empty, loadout.Type!);

        return Read(build, loadout).Jump?.Worst;
    }

    /// <summary>Every slot that has something in it or planned for it, as one list.</summary>
    private static List<Part> Parts(ShipBuild build, ShipLoadout? seen)
    {
        var fitted = (seen?.Modules ?? [])
            .GroupBy(module => module.Slot, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);

        var layout = EliteSpecifications.Slots(build.Hull);

        // The cargo hatch has no slot in the layout — `EliteSpecifications.Slots` deliberately
        // leaves it out — so a fitted module with no layout slot of its own is still counted
        // here as long as it has a specification to count.
        var beyondLayout = fitted
            .Where(pair => !layout.Any(slot => string.Equals(slot.Name, pair.Key, StringComparison.OrdinalIgnoreCase))
                           && EliteSpecifications.Module(pair.Value.Item) is not null)
            .Select(pair => pair.Key);

        var names = layout.Count > 0
            ? layout.Select(slot => slot.Name).Concat(beyondLayout)
            : fitted.Keys.Concat(build.Slots.Select(plan => plan.Slot)).Distinct(StringComparer.OrdinalIgnoreCase);

        var parts = new List<Part>();

        foreach (var name in names)
        {
            var plan = build.For(name);
            var module = fitted.GetValueOrDefault(name);

            // What the plan asks for, where it asks for a module exactly enough to look up.
            var wanted = plan?.Variant is { Length: > 0 } variant
                ? EliteSpecifications.Module(variant)
                : null;

            var vague = plan?.Module is { Length: > 0 } && wanted is null;

            var spec = wanted ?? EliteSpecifications.Module(module?.Item);

            if (spec is null)
            {
                continue;
            }

            // Planned figures where the plan changes the module or asks for a roll; the fitted module's own
            // where it does neither.
            var planned = wanted is not null
                          || (!vague && plan is { Blueprint.Length: > 0 })
                          || (!vague && plan is { Experimental.Length: > 0 });

            parts.Add(new Part(name, spec, module, planned ? plan : null, vague));
        }

        return parts;
    }

    /// <summary>
    /// Draw summed twice — with the hardpoints in and with them out — against what the plant makes.
    /// </summary>
    private static PowerGauge? Power(
        List<Part> parts, FigureKind kind, IReadOnlyDictionary<string, double>? measured)
    {
        double retracted = 0, deployed = 0;
        double? capacity = null;
        var draws = new Dictionary<string, SlotDraw>(StringComparer.OrdinalIgnoreCase);
        var groups = new Dictionary<int, double>();
        var modules = new List<PowerModule>();
        var everyGroupKnown = true;

        foreach (var part in parts)
        {
            if (part.Spec.PowerCapacity is not null)
            {
                capacity = (capacity ?? 0) + part.Figure(
                    "PowerCapacity", "Power Generation", part.Spec.PowerCapacity);

                continue;
            }

            // Elite's own figure for this slot wins where it has one — it is computed by the game with the
            // engineering already in it.
            var live = !part.IsPlanned && measured is not null && measured.TryGetValue(part.Slot, out var reported)
                ? reported
                : (double?)null;

            var draw = live ?? part.Figure("PowerDraw", "Power Draw", part.Spec.Power);

            deployed += draw ?? 0;

            var hardpoint = part.Spec.Type is { Length: > 0 } type && char.ToLowerInvariant(type[0]) == 'h';

            if (!hardpoint)
            {
                retracted += draw ?? 0;
            }

            // No figure for a plan too vague to cost, or a module that draws nothing at all.
            if (draw is { } megawatts && megawatts != 0 && !part.IsVague)
            {
                draws[part.Slot] = new SlotDraw(megawatts, part.IsPlanned ? FigureKind.Modelled : FigureKind.Measured);

                // A planned slot counts in its plan's group; a fitted, unplanned slot counts in the
                // game's own group (#253).
                var group = part.IsPlanned ? part.Plan!.Priority : part.Fitted?.Priority;

                modules.Add(new PowerModule(
                    part.Slot, part.Spec.Name, megawatts, hardpoint, group, PowerModule.RoleOf(part.Spec.Type)));

                if (group is { } known)
                {
                    groups[known] = groups.GetValueOrDefault(known) + megawatts;
                }
                else
                {
                    everyGroupKnown = false;
                }
            }
        }

        return deployed == 0 && capacity is null
            ? null
            : new PowerGauge(retracted, deployed, capacity, kind, draws)
            {
                Groups = everyGroupKnown ? groups : null,
                Modules = modules,
            };
    }

    /// <summary>The three needles.</summary>
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

        // Flat, and added after the division.
        var boost = parts.Sum(part => part.Spec.JumpBoost ?? 0);

        var unladen = !modelled && seen?.UnladenMass is { } reported
            ? reported
            : Mass(build, parts, seen);

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

    /// <summary>The hull plus everything in it, for a build there is no measured mass for.</summary>
    private static double? Mass(ShipBuild build, List<Part> parts, ShipLoadout? seen)
    {
        var hull = (seen is { IsKnown: true } ? HullMass(seen) : null)
                   ?? EliteSpecifications.Ship(build.Hull)?.HullMass;

        if (hull is not { } known)
        {
            return null;
        }

        return parts.Aggregate(
            (double)known,
            (total, part) => total + (part.Figure("Mass", "Mass", part.Spec.Mass) ?? 0));
    }

    /// <summary>
    /// The hull's own mass, worked out as <c>UnladenMass</c> less every fitted module inside the hull's
    /// slot layout, using the engineered figure where the module carries one and the stock figure
    /// otherwise. Null where the loadout carries no <c>UnladenMass</c>, or a module inside the layout
    /// has no mass to subtract — a hull the shipped table has no figure for either.
    /// </summary>
    private static double? HullMass(ShipLoadout seen)
    {
        if (seen.UnladenMass is not { } unladen)
        {
            return null;
        }

        var layout = EliteSpecifications.Slots(seen.Type);
        var fitted = 0.0;

        foreach (var module in seen.Modules)
        {
            // Outside the layout — a cosmetic, the cargo hatch, a ship-kit part — never counts toward
            // the outfitting screen's own mass, so it weighs nothing here no matter what specification
            // it happens to have.
            if (!layout.Any(slot => string.Equals(slot.Name, module.Slot, StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            if (FittedMass(module) is not { } mass)
            {
                return null;
            }

            fitted += mass;
        }

        return unladen - fitted;
    }

    /// <summary>One fitted module's mass: engineered where <c>Modifiers</c> carries one, stock otherwise.</summary>
    private static double? FittedMass(ShipModule module)
    {
        foreach (var modifier in module.Modifiers)
        {
            if (string.Equals(modifier.Label, "Mass", StringComparison.OrdinalIgnoreCase) && modifier.Value is { } value)
            {
                return value;
            }
        }

        return EliteSpecifications.Module(module.Item)?.Mass;
    }

    /// <summary>Everything one kind of module holds — <c>cft</c> is a fuel tank, <c>icr</c> a rack.</summary>
    private static double Capacity(List<Part> parts, string type) =>
        parts
            .Where(part => string.Equals(part.Spec.Type, type, StringComparison.OrdinalIgnoreCase))
            .Sum(part => part.Spec.Figure("capacity") ?? 0);

    /// <summary>
    /// One slot as the gauges see it: a module, and either what Elite reported about it or what the
    /// Commander wants done to it.
    /// </summary>
    /// <param name="Slot">
    /// The journal's own name for it, which is how the measured file is keyed.
    /// </param>
    /// <param name="Spec">The module in the slot, planned where a plan names one exactly.</param>
    /// <param name="Fitted">What Elite reported here, or null for an empty or unseen slot.</param>
    /// <param name="Plan">
    /// The plan whose figures this slot takes, or null to take the fitted ones.
    /// </param>
    /// <param name="IsVague">Whether a plan named a module too loosely to look up.</param>
    private sealed record Part(
        string Slot,
        ModuleSpecification Spec,
        ShipModule? Fitted,
        SlotPlan? Plan,
        bool IsVague)
    {
        public bool IsPlanned => Plan is not null;

        /// <summary>One figure for this slot: modelled from the plan, measured off the roll, or stock.</summary>
        /// <param name="label">The <c>Modifiers</c> label Elite writes — <c>PowerDraw</c>.</param>
        /// <param name="attribute">
        /// The blueprint table's name for the same thing — "Power Draw".
        /// </param>
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
