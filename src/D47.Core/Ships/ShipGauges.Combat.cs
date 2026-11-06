using D47.Core.Journal;
using D47.Core.Knowledge;

namespace D47.Core.Ships;

public static partial class ShipGauges
{
    private static readonly string[] Strength = ["Optimal Strength", "Optimal Multiplier"];

    /// <summary>The hold in tons: what Elite reported, or what the racks would hold.</summary>
    private static double Hold(List<Part> parts, ShipLoadout? seen, bool modelled) =>
        !modelled && seen?.CargoCapacity is { } cargo ? cargo : Capacity(parts, "icr");

    private static CombatGauge Combat(
        ShipBuild build, List<Part> parts, ShipLoadout? seen, FigureKind kind, bool modelled)
    {
        var ship = EliteSpecifications.Ship(build.Hull) ?? EliteSpecifications.Ship(seen?.Type);
        var (raw, sustained) = Damage(parts);
        var (shields, shieldResistance) = Shields(ship, parts);
        var (hull, hullResistance) = Hull(ship, parts);

        return new CombatGauge(
            shields, hull, shieldResistance, hullResistance, raw, sustained, Hold(parts, seen, modelled), kind);
    }

    private static (double? Strength, ResistanceSet? Resistance) Shields(ShipSpecification? ship, List<Part> parts)
    {
        var generator = parts.FirstOrDefault(part => part.Spec.Type is "isg");

        if (generator is null)
        {
            return (null, null);
        }

        var boosters = parts.Where(part => part.Spec.Type is "usb").ToList();

        var resistance = new ResistanceSet(
            ShieldResistance(generator, boosters, "KineticResistance", "Kinetic Resistance", spec => spec.KineticResistance),
            ShieldResistance(generator, boosters, "ThermicResistance", "Thermal Resistance", spec => spec.ThermalResistance),
            ShieldResistance(generator, boosters, "ExplosiveResistance", "Explosive Resistance", spec => spec.ExplosiveResistance),
            ShieldResistance(generator, boosters, "CausticResistance", "Caustic Resistance", spec => spec.CausticResistance));

        if (ship is not { Shields: { } baseShield, HullMass: { } mass }
            || GeneratorMultiplier(generator, mass) is not { } multiplier)
        {
            return (null, resistance);
        }

        var boost = boosters.Sum(booster => booster.Boost(
            "DefenceModifierShieldMultiplier", ["Shield Boost", "Optimal Strength"], booster.Spec.Figure("shield boost")) ?? 0);

        var added = parts.Where(part => part.Spec.Type is "isrp").Sum(part => part.Spec.Figure("shield added") ?? 0);

        return (baseShield * multiplier * (1 + boost) + added, resistance);
    }

    /// <summary>
    /// The generator's multiplier at the hull's mass, read off the curve through its minimum, optimal and
    /// maximum mass. Engineering scales the three multipliers together; a changed optimal mass moves the
    /// minimum always and the maximum only upward.
    /// </summary>
    private static double? GeneratorMultiplier(Part generator, double hullMass)
    {
        var spec = generator.Spec;

        if (spec.OptimalMass is not { } stockOptimal
            || spec.Figure("minimum mass") is not { } stockMin
            || spec.Figure("maximum mass") is not { } stockMax
            || spec.Figure("multiplier at minimum") is not { } stockMinMul
            || spec.Figure("multiplier at optimal") is not { } stockOptMul
            || spec.Figure("multiplier at maximum") is not { } stockMaxMul
            || generator.Figure("ShieldGenOptimalMass", "Optimal Mass", stockOptimal) is not { } optimal
            || generator.Figure("ShieldGenStrength", Strength, stockOptMul, 100) is not { } optMul
            || stockOptimal <= 0 || stockOptMul <= 0)
        {
            return null;
        }

        var massRatio = optimal / stockOptimal;
        var mulRatio = optMul / stockOptMul;

        var min = stockMin * massRatio;
        var max = massRatio > 1 ? stockMax * massRatio : stockMax;
        var minMul = stockMinMul * mulRatio;
        var maxMul = stockMaxMul * mulRatio;

        if (max <= min || maxMul == minMul)
        {
            return null;
        }

        var x = Math.Clamp((max - hullMass) / (max - min), 0, 1);
        var exponent = Math.Log((optMul - minMul) / (maxMul - minMul))
                       / Math.Log(Math.Min(1, (max - optimal) / (max - min)));
        var multiplier = minMul + Math.Pow(x, exponent) * (maxMul - minMul);

        return double.IsFinite(multiplier) ? multiplier : null;
    }

    /// <summary>
    /// One damage type's shield resistance: the generator's and every booster's damage multipliers
    /// multiplied, then diminishing returns where the product falls below 70% of the generator's own.
    /// </summary>
    private static double ShieldResistance(
        Part generator,
        List<Part> boosters,
        string label,
        string attribute,
        Func<ModuleSpecification, double?> stock)
    {
        var generatorDamage = 1 - generator.Resistance(label, attribute, stock(generator.Spec));
        var combined = boosters.Aggregate(
            generatorDamage,
            (damage, booster) => damage * (1 - booster.Resistance(label, attribute, stock(booster.Spec))));

        var ceiling = generatorDamage * 0.7;
        var floor = ceiling / 2;

        return 1 - (combined < ceiling ? floor + (ceiling - floor) * (combined / ceiling) : combined);
    }

    private static (double? Strength, ResistanceSet? Resistance) Hull(ShipSpecification? ship, List<Part> parts)
    {
        var bulkhead = parts.FirstOrDefault(part => part.Spec.Type is "cbh");

        if (bulkhead is null)
        {
            return (null, null);
        }

        var packages = parts.Where(part => part.Spec.Type is "ihrp" or "imahrp").ToList();

        var resistance = new ResistanceSet(
            HullResistance(bulkhead, packages, "KineticResistance", "Kinetic Resistance", spec => spec.KineticResistance),
            HullResistance(bulkhead, packages, "ThermicResistance", "Thermal Resistance", spec => spec.ThermalResistance),
            HullResistance(bulkhead, packages, "ExplosiveResistance", "Explosive Resistance", spec => spec.ExplosiveResistance),
            HullResistance(bulkhead, packages, "CausticResistance", "Caustic Resistance", spec => spec.CausticResistance));

        if (ship?.Armour is not { } armour
            || bulkhead.Boost("DefenceModifierHealthMultiplier", ["Hull Boost"], bulkhead.Spec.HullBoost) is not { } boost)
        {
            return (null, resistance);
        }

        var added = packages.Sum(part => part.Figure(
            "DefenceModifierHealthAddition", "Hull Reinforcement", part.Spec.Figure("hull added")) ?? 0);

        // A package carries its own boost only when engineering gave it one, and it applies to the hull's base.
        var packageBoost = packages.Sum(part => part.Boost("DefenceModifierHealthMultiplier", ["Hull Boost"], 0) ?? 0);

        return (armour * (1 + boost + packageBoost) + added, resistance);
    }

    /// <summary>
    /// The bulkhead's resistance with each reinforcement package folded in, weakest first. A package
    /// over 30% counts double above 30%, a running total over 30% earns half of what it adds, and the
    /// total stops at 75%.
    /// </summary>
    private static double HullResistance(
        Part bulkhead,
        List<Part> packages,
        string label,
        string attribute,
        Func<ModuleSpecification, double?> stock)
    {
        var resistance = bulkhead.Resistance(label, attribute, stock(bulkhead.Spec));

        foreach (var package in packages
                     .Select(part => part.Resistance(label, attribute, stock(part.Spec)))
                     .Order())
        {
            var adapted = package > 0.3 ? package * 2 - 0.3 : package;
            var lower = Math.Max(0.3, resistance);
            var stacked = 1 - (1 - resistance) * (1 - adapted);
            var capped = lower + (stacked - lower) / (1 - lower) * (0.65 - lower);

            resistance = capped >= 0.3 ? capped : stacked;
        }

        return Math.Min(0.75, resistance);
    }

    /// <summary>Raw and sustained damage per second across every weapon.</summary>
    private static (double Raw, double Sustained) Damage(List<Part> parts)
    {
        double raw = 0, draw = 0;

        foreach (var weapon in parts.Where(part => part.Spec.Type is { Length: > 0 } type
                                                   && char.ToLowerInvariant(type[0]) == 'h'
                                                   && part.Spec.Figure("damage per second") is not null))
        {
            var dps = weapon.Figure(
                "DamagePerSecond", ["Damage", "Rate of Fire"], weapon.Spec.Figure("damage per second")) ?? 0;
            var distributor = weapon.Figure(
                "DistributorDraw", "Distributor Draw", weapon.Spec.Figure("distributor draw")) ?? 0;
            var interval = weapon.Spec.Figure("fire interval");
            var shots = interval is > 0
                ? weapon.Figure("RateOfFire", "Rate of Fire", 1 / interval.Value) ?? 0
                : 1;

            raw += dps;

            // The figure is per shot; a second of fire draws it once per shot.
            draw += distributor * shots;
        }

        var distributorPart = parts.FirstOrDefault(part => part.Spec.Type is "cpd");
        var recharge = distributorPart?.Figure(
            "WeaponsRecharge", "Weapons Recharge", distributorPart.Spec.Figure("weapons recharge"));

        return (raw, recharge is { } rate && draw > rate ? raw * rate / draw : raw);
    }
}
