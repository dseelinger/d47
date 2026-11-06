using D47.Core.Journal;
using D47.Core.Knowledge;

namespace D47.Core.Ships;

/// <summary>
/// Finds a build for a <see cref="BuildGoal"/> by a hill climb over single-slot moves. It makes no model
/// call and reads no clock or random source, so the same input gives the same build. A slot holding a
/// module in the seen loadout can be replaced but not planned empty, because a plan has no way to say
/// so.
/// </summary>
public static class BuildSearch
{
    /// <summary>The most builds one climb reads before it stops.</summary>
    public const int MostEvaluations = 6000;

    /// <summary>How many builds <see cref="GoalMode.AnyGain"/> carries from one number of changes to the next.</summary>
    public const int BeamWidth = 8;

    /// <summary>The share of hull and module cost held back for the rebuy, as Elite charges it.</summary>
    public const double RebuyShare = 0.05;

    /// <summary>The module a goal that weights exploration keeps, except in <see cref="GoalMode.Only"/>.</summary>
    public const string FuelScoop = "Fuel Scoop";

    /// <summary>Survivability's and damage's shares of a combat score, by profile.</summary>
    public static (double Survivability, double Damage) CombatMix(CombatProfile profile) =>
        profile == CombatProfile.Bounty ? (0.5, 0.5) : (0.6, 0.4);

    /// <summary>The best build the search reaches; <paramref name="cancellationToken"/> is checked before each build is read.</summary>
    public static BuildResult Find(
        ShipBuild start, ShipLoadout? seen, BuildGoal goal, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(start);
        ArgumentNullException.ThrowIfNull(goal);

        if (goal.Mode != GoalMode.Weighted && goal.Target is null)
        {
            throw new ArgumentException($"A goal in {goal.Mode} mode needs a target role.", nameof(goal));
        }

        return new Search(start, seen, goal, cancellationToken).Run();
    }

    private const double Tolerance = 1e-9;

    private const string PowerPlant = "PowerPlant";

    private const string MainEngines = "MainEngines";

    /// <summary>Module types a role scores, beyond the core slots and the weapons.</summary>
    private static readonly HashSet<string> Defences = new(["isg", "usb", "isrp", "ihrp", "imahrp"], StringComparer.OrdinalIgnoreCase);

    private sealed record Slot(ShipSlot Layout, IReadOnlyList<ModuleSpecification> Offers, bool Emptiable)
    {
        public string Name => Layout.Name;
    }

    private sealed record Candidate(
        ShipBuild Build, ShipGauges.SearchReading Reading, double Penalty, IReadOnlyDictionary<string, string> Reasons);

    private sealed class Search
    {
        private readonly ShipBuild start;
        private readonly ShipLoadout? seen;
        private readonly BuildGoal goal;
        private readonly CancellationToken token;
        private readonly Dictionary<string, ShipModule> fitted;
        private readonly HashSet<string> owned;
        private readonly List<Slot> slots = [];
        private readonly ModuleSpecification?[] startModules;
        private readonly ShipGauges.SearchReading startReading;
        private readonly List<(BuildRole Role, double Weight)> roles;
        private readonly List<BuildFloor> floors;
        private readonly List<(string Name, int Count)> mustHaves = [];
        private readonly List<ModuleSpecification> plants;
        private readonly List<ModuleSpecification> thrusters;
        private readonly double hullCost;
        private (double Survivability, double Damage) combatBest = (1, 1);
        private int evaluations;

        public Search(ShipBuild start, ShipLoadout? seen, BuildGoal goal, CancellationToken token)
        {
            this.start = start;
            this.seen = seen;
            this.goal = goal;
            this.token = token;

            fitted = (seen?.Modules ?? [])
                .GroupBy(module => module.Slot, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);

            owned = new HashSet<string>(
                fitted.Values.Select(module => module.Item)
                    .Concat(start.Slots.Select(plan => plan.Variant).OfType<string>()),
                StringComparer.OrdinalIgnoreCase);

            var layout = EliteSpecifications.Slots(start.Hull);

            startModules = [.. layout.Select(slot => Module(start, slot.Name))];
            startReading = ShipGauges.ReadForSearch(start, seen);
            hullCost = seen?.HullValue ?? EliteSpecifications.Ship(start.Hull)?.Cost ?? 0;

            roles = goal.Mode == GoalMode.Weighted
                ? Normalised(goal.Weights)
                : [(goal.Target!.Value, 1)];

            floors = [.. goal.Floors.Where(floor => floor.AtLeast > 0)];

            if (goal.Mode == GoalMode.KeepRest)
            {
                foreach (var figure in Enum.GetValues<BuildFigure>())
                {
                    if (!Owns(goal.Target!.Value, figure) && FigureOf(startReading, figure) is var at and > 0)
                    {
                        floors.Add(new BuildFloor(figure, at));
                    }
                }
            }

            Require(goal.MustHaves.Where(name => !string.IsNullOrWhiteSpace(name)).Select(name => name.Trim()));

            if (goal.Mode != GoalMode.Only && roles.Any(role => role.Role == BuildRole.Exploration))
            {
                Require([FuelScoop]);
            }

            if (goal.Mode is GoalMode.KeepRest or GoalMode.AnyGain)
            {
                Require(layout
                    .Select((slot, index) => (slot, module: startModules[index]))
                    .Where(pair => pair.slot.Kind != ShipSlotKind.Core && pair.module is { } module && !Scored(module))
                    .Select(pair => pair.module!.Name));
            }

            foreach (var slot in layout)
            {
                var offers = EliteSpecifications.ModulesFor(slot).Where(Offered);

                if (slot.Kind != ShipSlotKind.Core)
                {
                    var own = fitted.TryGetValue(slot.Name, out var module) ? EliteSpecifications.Module(module.Item)?.Type : null;

                    offers = offers.Where(offer => Relevant(offer)
                                                   || (own is not null && string.Equals(offer.Type, own, StringComparison.OrdinalIgnoreCase))
                                                   || mustHaves.Any(must => string.Equals(must.Name, offer.Name, StringComparison.OrdinalIgnoreCase)));
                }

                slots.Add(new Slot(slot, [.. offers], slot.Kind != ShipSlotKind.Core && !fitted.ContainsKey(slot.Name)));
            }

            plants = LightestFirst(PowerPlant);
            thrusters = LightestFirst(MainEngines);
        }

        public BuildResult Run()
        {
            var first = Evaluate(start, new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase));

            if (slots.Count == 0)
            {
                return Result(first);
            }

            var placed = PlaceMustHaves(first);

            var found = goal.Mode switch
            {
                GoalMode.AnyGain => AnyGain(placed),
                _ when roles.Count == 0 => Climb(placed, _ => 0, "Meets a floor."),
                _ when roles.Count == 1 => RoleAlone(placed, roles[0].Role),
                _ => Mixed(placed),
            };

            return Result(found);
        }

        // ---- the climbs ---------------------------------------------------------------------

        private Candidate RoleAlone(Candidate from, BuildRole role)
        {
            switch (role)
            {
                case BuildRole.Exploration:
                    return Climb(from, reading => FigureOf(reading, BuildFigure.LadenJump), "Raises the laden jump range.");

                case BuildRole.Cargo:
                    return Climb(from, reading => FigureOf(reading, BuildFigure.Cargo), "Adds to the hold.");

                default:
                    var tough = Climb(from, Survivability, "Adds to survivability.");
                    var armed = Climb(from, Damage, "Adds to sustained damage.");

                    combatBest = (Survivability(tough.Reading), Damage(armed.Reading));

                    return Best([.. new[] { from, tough, armed }.Select(seed => Climb(seed, CombatFigure, "Adds to combat strength."))], CombatFigure);
            }
        }

        /// <summary>Each role's score is its figure over the best that role reaches alone; the bests also seed the climb.</summary>
        private Candidate Mixed(Candidate from)
        {
            var bests = roles.Select(role => (role.Role, role.Weight, Best: RoleAlone(from, role.Role))).ToList();
            var tops = bests.Select(best => RoleFigure(best.Role, best.Best.Reading)).ToList();

            double Score(ShipGauges.SearchReading reading) =>
                bests.Select((best, index) => best.Weight * Ratio(RoleFigure(best.Role, reading), tops[index])).Sum();

            var seeds = new[] { from }.Concat(bests.Select(best => best.Best));

            return Best([.. seeds.Select(seed => Climb(seed, Score, "Serves the weighted goal."))], Score);
        }

        /// <summary>Single-slot moves, the best per slot taken in layout order, until a pass raises nothing.</summary>
        private Candidate Climb(Candidate from, Func<ShipGauges.SearchReading, double> score, string reason)
        {
            evaluations = 0;

            var current = from;
            var currentScore = score(from.Reading);
            var improved = true;

            while (improved && evaluations < MostEvaluations)
            {
                improved = false;

                foreach (var slot in slots)
                {
                    Candidate? best = null;
                    var bestScore = currentScore;

                    foreach (var move in Moves(current.Build, slot))
                    {
                        if (evaluations >= MostEvaluations)
                        {
                            break;
                        }

                        var next = Repaired(Apply(current, slot.Name, move, reason));
                        var nextScore = score(next.Reading);

                        if (Better(next, nextScore, best ?? current, bestScore))
                        {
                            best = next;
                            bestScore = nextScore;
                        }
                    }

                    if (best is not null)
                    {
                        current = best;
                        currentScore = bestScore;
                        improved = true;
                    }
                }
            }

            return current;
        }

        /// <summary>The fewest changes, then the lowest cost, that meet every limit and raise the target.</summary>
        private Candidate AnyGain(Candidate from)
        {
            var target = goal.Target!.Value;

            combatBest = (Positive(Survivability(startReading)), Positive(Damage(startReading)));

            var floor = RoleFigure(target, startReading);

            bool Gains(Candidate candidate) =>
                candidate.Penalty <= Tolerance
                && RoleFigure(target, candidate.Reading) > floor + Tolerance * Math.Max(1, Math.Abs(floor));

            if (Gains(from))
            {
                return from;
            }

            evaluations = 0;

            var frontier = new List<Candidate> { from };
            var reason = target switch
            {
                BuildRole.Exploration => "Raises the laden jump range.",
                BuildRole.Cargo => "Adds to the hold.",
                _ => "Adds to combat strength.",
            };

            for (var depth = 0; depth < slots.Count && evaluations < MostEvaluations && frontier.Count > 0; depth++)
            {
                var found = new List<Candidate>();
                var pool = new List<Candidate>();

                foreach (var origin in frontier)
                {
                    foreach (var slot in slots)
                    {
                        foreach (var move in Moves(origin.Build, slot))
                        {
                            if (evaluations >= MostEvaluations)
                            {
                                break;
                            }

                            var next = Repaired(Apply(origin, slot.Name, move, reason));

                            (Gains(next) ? found : pool).Add(next);
                        }
                    }
                }

                if (found.Count > 0)
                {
                    return found.OrderBy(candidate => Changed(candidate.Build)).ThenBy(candidate => Bought(Modules(candidate.Build))).First();
                }

                frontier = [.. pool
                    .OrderBy(candidate => candidate.Penalty)
                    .ThenByDescending(candidate => RoleFigure(target, candidate.Reading))
                    .Take(BeamWidth)];
            }

            return from;
        }

        private static Candidate Best(IReadOnlyList<Candidate> candidates, Func<ShipGauges.SearchReading, double> score)
        {
            var best = candidates[0];

            foreach (var candidate in candidates.Skip(1))
            {
                if (Better(candidate, score(candidate.Reading), best, score(best.Reading)))
                {
                    best = candidate;
                }
            }

            return best;
        }

        /// <summary>Fewer broken limits first; the score only between builds that break them equally.</summary>
        private static bool Better(Candidate next, double nextScore, Candidate than, double thanScore)
        {
            if (Math.Abs(next.Penalty - than.Penalty) > Tolerance)
            {
                return next.Penalty < than.Penalty;
            }

            return nextScore > thanScore + Tolerance * Math.Max(1, Math.Abs(thanScore));
        }

        // ---- moves --------------------------------------------------------------------------

        /// <summary>Every move for one slot: each module it takes, empty, and with engineering each blueprint and experimental.</summary>
        private IEnumerable<SlotPlan?> Moves(ShipBuild build, Slot slot)
        {
            var modules = Modules(build);
            var current = modules[slots.IndexOf(slot)];

            foreach (var offer in slot.Offers)
            {
                if (current is not null && Same(offer.Symbol, current.Symbol))
                {
                    continue;
                }

                if (OverLimit(modules, slot, offer))
                {
                    continue;
                }

                yield return PlanFor(build, slot.Name, offer);
            }

            if (slot.Emptiable && current is not null)
            {
                yield return null;
            }

            if (!goal.Engineering || current is null || BlueprintCatalogue.For(current) is not { Count: > 0 } recipes)
            {
                yield break;
            }

            var (blueprint, grade, experimental) = Engineering(build, slot.Name);

            foreach (var group in recipes
                         .Where(recipe => recipe.Kind == BlueprintKind.Modification && recipe.Grade is not null)
                         .GroupBy(recipe => recipe.Name, StringComparer.Ordinal))
            {
                var top = group.Max(recipe => recipe.Grade!.Value);

                if (string.Equals(group.Key, blueprint, StringComparison.OrdinalIgnoreCase) && grade == top)
                {
                    continue;
                }

                yield return Engineered(build, slot.Name, group.Key, top, experimental);
            }

            if (blueprint is null)
            {
                yield break;
            }

            foreach (var effect in recipes
                         .Where(recipe => recipe.Kind == BlueprintKind.Experimental)
                         .Select(recipe => recipe.Name)
                         .Distinct(StringComparer.Ordinal))
            {
                if (!string.Equals(effect, experimental, StringComparison.OrdinalIgnoreCase))
                {
                    yield return Engineered(build, slot.Name, blueprint, grade, effect);
                }
            }
        }

        /// <summary>The plan that puts a module in a slot, or null where it is the module already fitted there.</summary>
        private SlotPlan? PlanFor(ShipBuild build, string slot, ModuleSpecification module) =>
            fitted.TryGetValue(slot, out var there) && Same(there.Item, module.Symbol)
                ? null
                : new SlotPlan(slot, Module: module.Name) { Variant = module.Symbol, Priority = Priority(build, slot) };

        private SlotPlan Engineered(ShipBuild build, string slot, string blueprint, int grade, string? experimental)
        {
            var plan = build.For(slot);

            // Without a Variant the plan rolls the fitted module, and a Module name alone would read as too vague to cost.
            return plan?.Variant is { Length: > 0 } variant
                ? new SlotPlan(slot, blueprint, grade, experimental, plan.Module) { Variant = variant, Priority = plan.Priority }
                : new SlotPlan(slot, blueprint, grade, experimental) { Priority = Priority(build, slot) };
        }

        /// <summary>The blueprint, grade and experimental a slot carries, planned or fitted.</summary>
        private (string? Blueprint, int Grade, string? Experimental) Engineering(ShipBuild build, string slot)
        {
            if (build.For(slot) is { } plan && (plan.Blueprint is not null || plan.Experimental is not null || plan.Variant is not null))
            {
                return (plan.Blueprint, plan.Grade, plan.Experimental);
            }

            return fitted.TryGetValue(slot, out var module) && module.Blueprint is { } symbol
                ? (BlueprintCatalogue.NameOf(symbol) ?? symbol, module.BlueprintLevel ?? 0, BlueprintCatalogue.NameOf(module.ExperimentalSymbol))
                : (null, 0, null);
        }

        private int Priority(ShipBuild build, string slot) =>
            build.For(slot)?.Priority
            ?? (fitted.TryGetValue(slot, out var module) ? module.Priority : null)
            ?? 1;

        private bool OverLimit(ModuleSpecification?[] modules, Slot slot, ModuleSpecification offer)
        {
            if (offer.Limit is not { Length: > 0 } group || EliteSpecifications.MostOf(group) is not { } most)
            {
                return false;
            }

            var count = 0;

            for (var index = 0; index < slots.Count; index++)
            {
                if (slots[index] != slot && string.Equals(modules[index]?.Limit, group, StringComparison.OrdinalIgnoreCase))
                {
                    count++;
                }
            }

            return count >= most;
        }

        // ---- repairs and must-haves ---------------------------------------------------------

        /// <summary>A build over its power budget gets the lightest plant that carries it, and one over its thrusters' maximum mass the lightest thrusters that carry it.</summary>
        private Candidate Repaired(Candidate candidate) => RepairPower(RepairThrust(RepairPower(candidate)));

        private Candidate RepairPower(Candidate candidate)
        {
            if (candidate.Reading.Gauges.Power is not { Fits: false } power)
            {
                return candidate;
            }

            var current = Module(candidate.Build, PowerPlant);

            foreach (var plant in plants)
            {
                if (plant.PowerCapacity >= power.Deployed && (current is null || !Same(plant.Symbol, current.Symbol)))
                {
                    return Apply(candidate, PowerPlant, PlanFor(candidate.Build, PowerPlant, plant), "Carries the power the build draws.");
                }
            }

            return candidate;
        }

        private Candidate RepairThrust(Candidate candidate)
        {
            var current = Module(candidate.Build, MainEngines);

            if (candidate.Reading.Mass is not { } mass
                || current?.Figure("maximum mass") is not { } most
                || mass <= most)
            {
                return candidate;
            }

            var rest = mass - (current.Mass ?? 0);

            foreach (var thruster in thrusters)
            {
                if (thruster.Figure("maximum mass") is { } carries
                    && carries >= rest + (thruster.Mass ?? 0)
                    && !Same(thruster.Symbol, current.Symbol))
                {
                    return Apply(candidate, MainEngines, PlanFor(candidate.Build, MainEngines, thruster), "Carries the build's mass.");
                }
            }

            return candidate;
        }

        /// <summary>Each must-have in the smallest slot that takes it, at the cheapest variant that fits.</summary>
        private Candidate PlaceMustHaves(Candidate candidate)
        {
            var bySize = slots.OrderBy(slot => slot.Layout.Size).ToList();

            foreach (var (name, count) in mustHaves)
            {
                while (Count(Modules(candidate.Build), name) < count)
                {
                    var modules = Modules(candidate.Build);
                    var placed = false;

                    foreach (var slot in bySize)
                    {
                        var current = modules[slots.IndexOf(slot)];

                        if (current is not null && IsMustHave(current.Name))
                        {
                            continue;
                        }

                        var offer = slot.Offers
                            .Where(module => string.Equals(module.Name, name, StringComparison.OrdinalIgnoreCase)
                                             && !OverLimit(modules, slot, module))
                            .OrderBy(module => module.Cost ?? long.MaxValue)
                            .FirstOrDefault();

                        if (offer is null)
                        {
                            continue;
                        }

                        candidate = Repaired(Apply(candidate, slot.Name, PlanFor(candidate.Build, slot.Name, offer), "Fits a must-have."));
                        placed = true;
                        break;
                    }

                    if (!placed)
                    {
                        break;
                    }
                }
            }

            return candidate;
        }

        private bool IsMustHave(string name) =>
            mustHaves.Any(must => string.Equals(must.Name, name, StringComparison.OrdinalIgnoreCase));

        private void Require(IEnumerable<string> names)
        {
            foreach (var group in names.GroupBy(name => name, StringComparer.OrdinalIgnoreCase))
            {
                var at = mustHaves.FindIndex(must => string.Equals(must.Name, group.Key, StringComparison.OrdinalIgnoreCase));
                var count = group.Count();

                if (at < 0)
                {
                    mustHaves.Add((group.Key, count));
                }
                else if (mustHaves[at].Count < count)
                {
                    mustHaves[at] = (mustHaves[at].Name, count);
                }
            }
        }

        // ---- reading a build ----------------------------------------------------------------

        private Candidate Apply(Candidate from, string slot, SlotPlan? plan, string reason)
        {
            var build = plan is null ? from.Build.Without(slot) : from.Build.With(plan);

            return Evaluate(build, new Dictionary<string, string>(from.Reasons, StringComparer.OrdinalIgnoreCase) { [slot] = reason });
        }

        private Candidate Evaluate(ShipBuild build, IReadOnlyDictionary<string, string> reasons)
        {
            token.ThrowIfCancellationRequested();
            evaluations++;

            var reading = ShipGauges.ReadForSearch(build, seen);

            return new Candidate(build, reading, Penalty(build, reading), reasons);
        }

        /// <summary>How far a build breaks its limits, each as a share of the limit; zero for one that keeps them all.</summary>
        private double Penalty(ShipBuild build, ShipGauges.SearchReading reading)
        {
            var modules = Modules(build);
            var penalty = 0.0;

            if (reading.Gauges.Power is { Overage: { } over, Capacity: { } made })
            {
                penalty += over / Math.Max(made, 1);
            }

            if (reading.Mass is { } mass
                && Module(build, MainEngines)?.Figure("maximum mass") is { } most
                && mass > most)
            {
                penalty += (mass - most) / most;
            }

            if (goal.Credits is { } credits)
            {
                var needed = Bought(modules) + RebuyShare * Value(modules);

                if (needed > credits)
                {
                    penalty += (needed - credits) / Math.Max(credits, 1);
                }
            }

            foreach (var floor in floors)
            {
                penalty += Shortfall(reading, floor) / floor.AtLeast;
            }

            foreach (var (name, count) in mustHaves)
            {
                penalty += Math.Max(0, count - Count(modules, name));
            }

            return penalty;
        }

        private static double Shortfall(ShipGauges.SearchReading reading, BuildFloor floor)
        {
            var shortfall = floor.AtLeast - FigureOf(reading, floor.Figure);

            return shortfall > 1e-6 * Math.Max(1, floor.AtLeast) ? shortfall : 0;
        }

        /// <summary>The module in each slot, in layout order, or null where it is empty or planned too loosely to look up.</summary>
        private ModuleSpecification?[] Modules(ShipBuild build)
        {
            var modules = new ModuleSpecification?[slots.Count];

            for (var index = 0; index < slots.Count; index++)
            {
                modules[index] = Module(build, slots[index].Name);
            }

            return modules;
        }

        private ModuleSpecification? Module(ShipBuild build, string slot)
        {
            var plan = build.For(slot);

            if (plan?.Variant is { Length: > 0 } variant)
            {
                return EliteSpecifications.Module(variant);
            }

            if (plan?.Module is { Length: > 0 })
            {
                return null;
            }

            return fitted.TryGetValue(slot, out var module) ? EliteSpecifications.Module(module.Item) : null;
        }

        private static int Count(ModuleSpecification?[] modules, string name) =>
            modules.Count(module => module is not null && string.Equals(module.Name, name, StringComparison.OrdinalIgnoreCase));

        /// <summary>What the modules not in the start build cost.</summary>
        private double Bought(ModuleSpecification?[] modules)
        {
            var bought = 0.0;

            for (var index = 0; index < modules.Length; index++)
            {
                if (modules[index] is { } module
                    && (startModules[index] is not { } before || !Same(before.Symbol, module.Symbol)))
                {
                    bought += module.Cost ?? 0;
                }
            }

            return bought;
        }

        /// <summary>The hull and every module, at what was paid for the fitted ones.</summary>
        private double Value(ModuleSpecification?[] modules)
        {
            var value = hullCost;

            for (var index = 0; index < modules.Length; index++)
            {
                if (modules[index] is not { } module)
                {
                    continue;
                }

                value += fitted.TryGetValue(slots[index].Name, out var there) && Same(there.Item, module.Symbol)
                    ? there.Value ?? module.Cost ?? 0
                    : module.Cost ?? 0;
            }

            return value;
        }

        private int Changed(ShipBuild build) =>
            slots.Count(slot => !Equals(build.For(slot.Name), start.For(slot.Name)));

        // ---- figures ------------------------------------------------------------------------

        private static double FigureOf(ShipGauges.SearchReading reading, BuildFigure figure) => figure switch
        {
            BuildFigure.LadenJump => reading.Gauges.Jump?.Worst ?? 0,
            BuildFigure.UnladenJump => reading.Gauges.Jump?.Best ?? 0,
            BuildFigure.Cargo => reading.Gauges.Combat?.Cargo ?? 0,
            BuildFigure.Shields => reading.Gauges.Combat?.Shields ?? 0,
            BuildFigure.Hull => reading.Gauges.Combat?.Hull ?? 0,
            _ => reading.Gauges.Combat?.SustainedDps ?? 0,
        };

        private double RoleFigure(BuildRole role, ShipGauges.SearchReading reading) => role switch
        {
            BuildRole.Exploration => FigureOf(reading, BuildFigure.LadenJump),
            BuildRole.Cargo => FigureOf(reading, BuildFigure.Cargo),
            _ => CombatFigure(reading),
        };

        /// <summary>Survivability and damage, each over its own best, mixed by the profile.</summary>
        private double CombatFigure(ShipGauges.SearchReading reading)
        {
            var (survivability, damage) = CombatMix(goal.Combat);

            return survivability * Ratio(Survivability(reading), combatBest.Survivability)
                   + damage * Ratio(Damage(reading), combatBest.Damage);
        }

        /// <summary>Shields plus hull, each divided by the damage it still takes over the profile's mix.</summary>
        private double Survivability(ShipGauges.SearchReading reading) =>
            reading.Gauges.Combat is not { } combat
                ? 0
                : Effective(combat.Shields, combat.ShieldResistance) + Effective(combat.Hull, combat.HullResistance);

        private double Effective(double? strength, ResistanceSet? resistance)
        {
            if (strength is not { } points || points <= 0)
            {
                return 0;
            }

            var resisted = resistance is null
                ? 0
                : goal.Combat == CombatProfile.Thargoid
                    ? (resistance.Caustic + resistance.Thermal) / 2
                    : (resistance.Kinetic + resistance.Thermal + resistance.Explosive) / 3;

            return points / Math.Max(0.05, 1 - resisted);
        }

        private double Damage(ShipGauges.SearchReading reading) =>
            goal.Combat == CombatProfile.Thargoid ? reading.XenoDps : reading.Gauges.Combat?.SustainedDps ?? 0;

        private static double Ratio(double figure, double best) => best > 0 ? figure / best : 0;

        private static double Positive(double figure) => figure > 0 ? figure : 1;

        /// <summary>Whether a figure is part of what a role scores, and so not held by <see cref="GoalMode.KeepRest"/>.</summary>
        private static bool Owns(BuildRole role, BuildFigure figure) => role switch
        {
            BuildRole.Exploration => figure == BuildFigure.LadenJump,
            BuildRole.Cargo => figure == BuildFigure.Cargo,
            _ => figure is BuildFigure.Shields or BuildFigure.Hull or BuildFigure.SustainedDps,
        };

        // ---- what is offered ----------------------------------------------------------------

        /// <summary>
        /// Leaves out modules marked removed, and unless one is fitted or planned now, a pledge or Guardian
        /// unlock and anything with no price.
        /// </summary>
        private bool Offered(ModuleSpecification module)
        {
            if (string.Equals(module.Entitlement, "removed", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            if (owned.Contains(module.Symbol))
            {
                return true;
            }

            var guardian = module.Entitlement?.Contains("_GUARDIAN_", StringComparison.OrdinalIgnoreCase) == true;

            return !module.NeedsPledge && !guardian && module.Cost is { } cost && (cost > 0 || module.IsBulkhead);
        }

        /// <summary>Whether a module in an optional, utility or hardpoint slot raises anything the goal reads.</summary>
        private bool Relevant(ModuleSpecification module)
        {
            var type = module.Type ?? string.Empty;
            var weapon = type.StartsWith('h') && module.Figure("damage per second") is not null;

            foreach (var (role, _) in roles)
            {
                var useful = role switch
                {
                    BuildRole.Exploration => type is "ifsdb",
                    BuildRole.Cargo => type is "icr",
                    _ => Defences.Contains(type)
                         || (weapon && (goal.Combat != CombatProfile.Thargoid || ShipGauges.XenoWeapons.Contains(type))),
                };

                if (useful)
                {
                    return true;
                }
            }

            return floors.Any(floor => floor.Figure switch
            {
                BuildFigure.LadenJump or BuildFigure.UnladenJump => type is "ifsdb",
                BuildFigure.Cargo => type is "icr",
                BuildFigure.Shields => type is "isg" or "usb" or "isrp",
                BuildFigure.Hull => type is "ihrp" or "imahrp",
                _ => weapon,
            });
        }

        /// <summary>Whether any role scores a module; one none scores is kept by <see cref="GoalMode.KeepRest"/> and <see cref="GoalMode.AnyGain"/>.</summary>
        private static bool Scored(ModuleSpecification module) =>
            module.Type is "icr" or "ifsdb" || Defences.Contains(module.Type ?? string.Empty)
            || module.Figure("damage per second") is not null;

        private List<ModuleSpecification> LightestFirst(string slot) =>
            slots.FirstOrDefault(candidate => candidate.Name == slot) is { } found
                ? [.. found.Offers.OrderBy(module => module.Mass ?? double.MaxValue).ThenBy(module => module.Cost ?? long.MaxValue).ThenBy(module => module.Symbol, StringComparer.Ordinal)]
                : [];

        private static List<(BuildRole Role, double Weight)> Normalised(IReadOnlyDictionary<BuildRole, double> weights)
        {
            var positive = Enum.GetValues<BuildRole>()
                .Select(role => (Role: role, Weight: weights.TryGetValue(role, out var weight) ? weight : 0))
                .Where(pair => pair.Weight > 0)
                .ToList();

            var total = positive.Sum(pair => pair.Weight);

            return [.. positive.Select(pair => (pair.Role, pair.Weight / total))];
        }

        private static bool Same(string left, string right) => string.Equals(left, right, StringComparison.OrdinalIgnoreCase);

        private BuildResult Result(Candidate found)
        {
            var modules = Modules(found.Build);

            var changes = slots
                .Where(slot => !Equals(found.Build.For(slot.Name), start.For(slot.Name)))
                .Select(slot => new SlotChange(
                    slot.Name,
                    start.For(slot.Name),
                    found.Build.For(slot.Name) ?? new SlotPlan(slot.Name),
                    found.Reasons.TryGetValue(slot.Name, out var reason) ? reason : "Serves the goal."))
                .ToList();

            var missing = mustHaves
                .SelectMany(must => Enumerable.Repeat(must.Name, Math.Max(0, must.Count - Count(modules, must.Name))))
                .ToList();

            return new BuildResult(
                found.Build,
                ShipGauges.Read(found.Build, seen),
                ShipGauges.Read(start, seen),
                changes,
                (long)Bought(modules),
                floors.FirstOrDefault(floor => Shortfall(found.Reading, floor) > 0),
                missing);
        }
    }
}
