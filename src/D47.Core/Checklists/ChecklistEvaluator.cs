using System.Globalization;
using D47.Core.Journal;
using D47.Core.Knowledge;

namespace D47.Core.Checklists;

/// <summary>Where an item stands, and the sentence that says so.</summary>
public readonly record struct ChecklistVerdict(ChecklistState State, string Reason);

/// <summary>
/// What the journal says about a derived item (list.md Phase 17, "Per ship build
/// planning/tracking" and "A colonisation plan writes the checklist").
/// <para>
/// <b>Nobody ever types in what they have already done.</b> A derived item's progress is a diff
/// against live state — the <c>Loadout</c> for a ship, <c>ColonisationConstructionDepot</c> for a
/// system — which is the whole reason a derived item refuses a manual tick.
/// </para>
/// <para>
/// <b>Answering null is a real answer and it is used often.</b> It means <i>nothing can be said
/// right now</i>: the Commander is flying a different ship, or has not visited the site. The
/// stored verdict then stands unchanged and is reported as of when it was taken, rather than being
/// silently reset to open by the absence of evidence.
/// </para>
/// <para>
/// Reads no clock and owns no thread, like everything else in Core. Freshness comes off the
/// journal event that carried it.
/// </para>
/// </summary>
public static class ChecklistEvaluator
{
    /// <summary>
    /// Where an item stands now, or null when nothing can be said. Authored items always answer
    /// null: a sentence nobody can compute is not something a journal read gets a vote on.
    /// </summary>
    public static ChecklistVerdict? Evaluate(ChecklistItem item, CommanderGameState? state)
    {
        if (item.Kind != ChecklistItemKind.Derived || item.Intent is not { } intent || !item.IsLive)
        {
            return null;
        }

        if (state is null)
        {
            return null;
        }

        return intent.Kind switch
        {
            ChecklistIntentKind.Blueprint => Ship(item, intent, state),
            ChecklistIntentKind.Experimental => Ship(item, intent, state),
            ChecklistIntentKind.Module => Ship(item, intent, state),
            ChecklistIntentKind.EngineerAccess => Access(intent, state),
            ChecklistIntentKind.Facility => Facility(item, intent, state),
            ChecklistIntentKind.Commodity => Commodity(item, intent, state),
            ChecklistIntentKind.Grade => OnFootGrade(item, intent, state),
            ChecklistIntentKind.Modification => OnFootModification(item, intent, state),
            _ => null,
        };
    }

    // ---------------------------------------------------------------- ships

    private static ChecklistVerdict? Ship(ChecklistItem item, ChecklistIntent intent, CommanderGameState state)
    {
        var loadout = state.Ship;

        // Loadout describes the ship the Commander is in and no other, so a plan for a ship in
        // another dock cannot be diffed at all. Saying nothing is the honest answer; resetting
        // the item to open because the evidence is absent would be an answer d47 invented.
        if (!IsActive(item.Scope, loadout))
        {
            return null;
        }

        // A ShipID now reporting a different hull makes the list stale and says so, rather than
        // quietly diffing an exploration Krait against a Cutter.
        if (item.Hull is { } hull
            && loadout.Type is { } type
            && !string.Equals(hull, type, StringComparison.OrdinalIgnoreCase))
        {
            return new ChecklistVerdict(
                ChecklistState.Stale,
                $"That ship id now reports a {loadout.TypeName ?? type}, and this plan was written for a {hull}.");
        }

        var module = Fitted(loadout, intent.Subject);

        if (module is null)
        {
            return Missing(intent, state);
        }

        return intent.Kind switch
        {
            ChecklistIntentKind.Module => new ChecklistVerdict(
                ChecklistState.Done,
                $"{Describe(module)} is fitted in {intent.Subject}."),

            // Elite localises the experimental effect and never the blueprint, so this one
            // compares exactly and the blueprint below sometimes cannot. That asymmetry is
            // Frontier's rather than d47's — see ChecklistNaming.
            ChecklistIntentKind.Experimental => Experimental(intent, module),

            _ => Blueprint(intent, module, state),
        };
    }

    private static ChecklistVerdict Experimental(ChecklistIntent intent, ShipModule module)
    {
        if (module.Experimental is not { } applied)
        {
            return new ChecklistVerdict(
                ChecklistState.Open, $"{Describe(module)} has no experimental effect on it.");
        }

        return ChecklistNaming.Confirms(intent.Detail, applied) == true
            ? new ChecklistVerdict(ChecklistState.Done, $"{applied} is on {Describe(module)}.")
            : new ChecklistVerdict(
                ChecklistState.Open, $"{Describe(module)} carries {applied} rather than {intent.Detail}.");
    }

    private static ChecklistVerdict Blueprint(ChecklistIntent intent, ShipModule module, CommanderGameState state)
    {
        var rank = RankFor(intent, module, state);

        if (module.Blueprint is not { } blueprint)
        {
            return Unengineered(intent, module, rank);
        }

        var held = module.BlueprintLevel;
        var wanted = intent.Grade;

        // A wildcard grade is met by any engineering at all. Null means wildcard here and
        // everywhere else in this phase — never "unknown", which would make it unmeetable.
        if (wanted is null)
        {
            return Named(intent, module, blueprint, $"{Describe(module)} carries {ChecklistNaming.Readable(blueprint)}");
        }

        if (held is null)
        {
            return new ChecklistVerdict(
                ChecklistState.Unverified,
                $"{Describe(module)} is engineered and the journal does not say to what grade.");
        }

        if (held < wanted)
        {
            // The gate is checked here rather than earlier, because a Commander part-way up a
            // blueprint has already proved they can reach the grades below.
            if (rank is { } known && EngineeringRules.RollsFor(wanted.Value, known) is null)
            {
                return Gated(intent, wanted.Value, known);
            }

            return new ChecklistVerdict(
                ChecklistState.Open,
                $"{Describe(module)} is at grade {held} of {wanted}."
                + (rank is { } r ? $" {Remaining(wanted.Value, r, 0)}" : string.Empty));
        }

        // Level is the fact — it names the grade the module has — and Quality is progress within
        // it. 0.85 and above is finished, not 1.0: of 994 grades measured, the 45 the game let a
        // Commander move on from all sat at 0.85 or above, and testing for 1.0 would tell them a
        // module they can see is finished is not (EngineeringRules.CompleteAt).
        if (held == wanted && module.Quality is { } quality && quality < EngineeringRules.CompleteAt)
        {
            var left = rank is { } known ? EngineeringRules.RollsRemaining(wanted.Value, known, quality) : null;

            return new ChecklistVerdict(
                ChecklistState.Open,
                $"{Describe(module)} is part-way through grade {wanted}"
                + (left is { } rolls ? $", about {Rolls(rolls)} short." : "."));
        }

        return Named(
            intent,
            module,
            blueprint,
            $"{Describe(module)} is at grade {held} and finished");
    }

    /// <summary>
    /// The last check, and the one d47 cannot always make. Everything measurable has passed; what
    /// is left is whether the blueprint in the slot is the blueprint the plan named, and the two
    /// spellings do not join.
    /// </summary>
    private static ChecklistVerdict Named(
        ChecklistIntent intent,
        ShipModule module,
        string blueprint,
        string sentence)
    {
        _ = module;

        return ChecklistNaming.Confirms(intent.Detail, blueprint) == true
            ? new ChecklistVerdict(ChecklistState.Done, sentence + ".")
            : new ChecklistVerdict(
                ChecklistState.Unverified,
                sentence + ". " + ChecklistNaming.CannotConfirm(intent.Detail ?? "that", blueprint));
    }

    private static ChecklistVerdict Unengineered(ChecklistIntent intent, ShipModule module, int? rank)
    {
        if (intent.Grade is { } grade && rank is { } known && EngineeringRules.RollsFor(grade, known) is null)
        {
            return Gated(intent, grade, known);
        }

        var total = intent.Grade is { } wanted && rank is { } reachable
            ? EngineeringRules.RollsFor(wanted, reachable)
            : null;

        return new ChecklistVerdict(
            ChecklistState.Open,
            // "is not currently engineered", because this verdict is a reading taken at a moment
            // and not a property of the module. The plan carries the journal's verdict with its
            // date, standing as of when it was taken, and it ships as ChecklistState.Open — still
            // to do. "Point Defence is not engineered" reads as a fact about Point Defence; the
            // only thing d47 knows is a fact about right now. Remediation 15 item 5.
            $"{Describe(module)} is not currently engineered."
            + (total is { } rolls ? $" Grade {intent.Grade} is {Rolls(rolls)} from here." : string.Empty));
    }

    /// <summary>
    /// A rank gate, which is not a slow route but no route at all — and the block has a published
    /// price, so this says what it costs to clear rather than naming a blocker and shrugging at it.
    /// </summary>
    private static ChecklistVerdict Gated(ChecklistIntent intent, int grade, int rank)
    {
        var who = intent.Engineer is { } engineer ? $" with {engineer}" : string.Empty;

        var price = EngineeringRules.ReputationCost(grade) is { } cost
            ? $" Reaching grade {grade} takes {cost.ToString("N0", CultureInfo.InvariantCulture)} credits of profit "
              + "sold at that workshop."
            : string.Empty;

        return new ChecklistVerdict(
            ChecklistState.Blocked,
            $"Grade {grade} cannot be rolled at rank {rank}{who} at all — no amount of gathering fixes that." + price);
    }

    /// <summary>
    /// Nothing in the slot. <b><see cref="ChecklistState.Elsewhere"/> is its own state</b>, because
    /// "you own that already, it is in Deciat, and moving it costs 2.1 million" is a completely
    /// different next action from "go and grind it".
    /// </summary>
    private static ChecklistVerdict Missing(ChecklistIntent intent, CommanderGameState state)
    {
        var wanted = intent.Detail ?? intent.Subject;

        var stored = state.Modules.Matching(wanted).FirstOrDefault(module => module.IsEngineered)
                     ?? state.Modules.Matching(wanted).FirstOrDefault();

        if (stored is not null)
        {
            var cost = stored.TransferCost is { } credits
                ? $", and moving it costs {credits.ToString("N0", CultureInfo.InvariantCulture)} credits"
                : string.Empty;

            return new ChecklistVerdict(
                ChecklistState.Elsewhere,
                $"You already own {stored.Name} — it is in {stored.StarSystem}{cost}.");
        }

        return new ChecklistVerdict(ChecklistState.Open, $"Nothing is fitted in {intent.Subject}.");
    }

    // ------------------------------------------------------------ engineers

    private static ChecklistVerdict? Access(ChecklistIntent intent, CommanderGameState state)
    {
        if (EngineerDirectory.ByName(intent.Detail ?? intent.Subject) is not { } engineer)
        {
            return null;
        }

        if (state.Engineers.For(engineer.Id) is not { } standing)
        {
            return new ChecklistVerdict(
                ChecklistState.Open, $"{engineer.Name} is not in your engineer progress yet.");
        }

        if (!standing.IsUnlocked)
        {
            return new ChecklistVerdict(ChecklistState.Open, $"{engineer.Name}: {standing.Describe()}");
        }

        var wanted = intent.Grade ?? 1;
        var rank = standing.Rank ?? 0;

        if (rank >= wanted)
        {
            return new ChecklistVerdict(ChecklistState.Done, $"{engineer.Name} is at rank {rank}.");
        }

        var price = EngineeringRules.ReputationCost(wanted) is { } cost
            ? $" Rank {wanted} takes {cost.ToString("N0", CultureInfo.InvariantCulture)} credits of profit sold there."
            : string.Empty;

        return new ChecklistVerdict(
            ChecklistState.Open, $"{engineer.Name} is at rank {rank} of {wanted}.{price}");
    }

    // ----------------------------------------------------------- colonising

    private static ChecklistVerdict? Commodity(ChecklistItem item, ChecklistIntent intent, CommanderGameState state)
    {
        if (SiteFor(item, intent, state) is not { } site)
        {
            return new ChecklistVerdict(
                ChecklistState.Open,
                $"I have not seen a depot for {intent.Subject}. That event only arrives while you are docked there.");
        }

        var wanted = ChecklistKeys.Compact(intent.Detail);

        var row = site.Resources.FirstOrDefault(resource => ChecklistKeys.Compact(resource.Name) == wanted);

        if (row is null)
        {
            return new ChecklistVerdict(
                ChecklistState.Open,
                $"{site.Where} is not asking for {intent.Detail} as of your last visit.");
        }

        var seen = $" As of your last visit, {site.SeenAt:yyyy-MM-dd}.";

        return row.IsMet
            ? new ChecklistVerdict(ChecklistState.Done, $"{row.Name} is delivered at {site.Where}.{seen}")
            : new ChecklistVerdict(
                ChecklistState.Open,
                $"{row.Name}: {row.Remaining} of {row.Required} still to deliver to {site.Where}.{seen}");
    }

    private static ChecklistVerdict? Facility(ChecklistItem item, ChecklistIntent intent, CommanderGameState state)
    {
        if (SiteFor(item, intent, state) is not { } site)
        {
            // No cost table exists for facilities, licence-clean or otherwise
            // (docs/spikes/colonisation-sources.md), so d47 has nothing to predict from and says
            // what it knows: that it has not seen this one.
            return new ChecklistVerdict(
                ChecklistState.Open,
                $"I have not seen a depot for {intent.Subject} yet.");
        }

        if (site.Failed)
        {
            return new ChecklistVerdict(ChecklistState.Blocked, $"{site.Where} reports the construction failed.");
        }

        var seen = $" As of your last visit, {site.SeenAt:yyyy-MM-dd}.";

        // The flag says finished, never "the events stopped" — a completed site keeps reporting.
        return site.Complete
            ? new ChecklistVerdict(ChecklistState.Done, $"{site.Where} is complete.{seen}")
            : new ChecklistVerdict(
                ChecklistState.Open,
                $"{site.Where} is {(site.Progress * 100).ToString("0.#", CultureInfo.InvariantCulture)}% built, "
                + $"{site.Outstanding.Count} commodities outstanding.{seen}");
    }

    private static ConstructionSite? SiteFor(ChecklistItem item, ChecklistIntent intent, CommanderGameState state)
    {
        if (state.Colonisation.Named(intent.Subject) is { } named)
        {
            return named;
        }

        var inSystem = state.Colonisation.InSystem(item.Scope.Key);

        return inSystem.Count == 1 ? inSystem[0] : null;
    }

    // ---------------------------------------------------------------- parts

    // ------------------------------------------------------------- on foot

    /// <summary>
    /// A grade on a suit or a hand weapon. <b>Nothing is rolled</b>, so this is a comparison rather
    /// than the fill-and-rank arithmetic a ship blueprint needs.
    /// </summary>
    private static ChecklistVerdict? OnFootGrade(
        ChecklistItem item, ChecklistIntent intent, CommanderGameState state)
    {
        if (Worn(item, state) is not { } current)
        {
            return null;
        }

        if (intent.Grade is not { } wanted)
        {
            return null;
        }

        if (current.Grade is not { } grade)
        {
            // The flight suit, which has no class at all and cannot be upgraded. Said, because the
            // Commander is otherwise waiting for a Pioneer Supplies trip that does not exist.
            return new ChecklistVerdict(
                ChecklistState.Blocked,
                $"{current.Name} has no grade and cannot be upgraded.");
        }

        return grade >= wanted
            ? new ChecklistVerdict(ChecklistState.Done, $"{current.Name} is at grade {grade}.")
            : new ChecklistVerdict(
                ChecklistState.Open,
                $"{current.Name} is at grade {grade} of {wanted}, "
                + $"{wanted - grade} step{(wanted - grade == 1 ? "" : "s")} to go at Pioneer Supplies.");
    }

    /// <summary>
    /// A modification on a suit or a hand weapon.
    /// <para>
    /// <b>Two ways this can fail to be checkable, and they are different answers.</b> Elite writes
    /// a fitted modification as a symbol and the recipe table names it in words; where the shipped
    /// map joins them the check is exact, and where it does not this answers
    /// <see cref="ChecklistState.Unverified"/> and says so — the same contract
    /// <see cref="ChecklistNaming"/> has for ship blueprints, and for the same reason.
    /// </para>
    /// <para>
    /// A grade 1 item is <see cref="ChecklistState.Blocked"/> rather than open: it has zero slots,
    /// so this is an ordering problem no amount of gathering fixes.
    /// </para>
    /// </summary>
    private static ChecklistVerdict? OnFootModification(
        ChecklistItem item, ChecklistIntent intent, CommanderGameState state)
    {
        if (Worn(item, state) is not { } current)
        {
            return null;
        }

        var fitted = current.Modifications;

        if (fitted.Any(modification =>
                modification.Name is { } name
                && ChecklistKeys.Compact(name) == ChecklistKeys.Compact(intent.Detail)))
        {
            return new ChecklistVerdict(ChecklistState.Done, $"{intent.Detail} is fitted to {current.Name}.");
        }

        if (current.Grade is null or < Knowledge.OnFootRules.ModifiableFrom)
        {
            return new ChecklistVerdict(
                ChecklistState.Blocked,
                $"{current.Name} is grade {current.Grade?.ToString(CultureInfo.InvariantCulture) ?? "none"} "
                + "and carries no modification slots. Upgrade it at Pioneer Supplies first — an "
                + "engineer's base has none.");
        }

        // Every slot spent and this one not among them. Permanent, so it is the one shortfall on
        // this list that gathering cannot fix.
        if (fitted.Count >= Knowledge.OnFootRules.SlotsAt(current.Grade.Value))
        {
            return new ChecklistVerdict(
                ChecklistState.Blocked,
                $"{current.Name} has all {fitted.Count} of its slots filled and modifications cannot "
                + "be removed. This needs a fresh item, bought and re-upgraded.");
        }

        // A symbol nothing joins is not evidence of absence. Said once, per the Noted flag.
        var unnamed = fitted.Where(modification => !modification.IsNamed).ToArray();

        if (unnamed.Length > 0)
        {
            return new ChecklistVerdict(
                ChecklistState.Unverified,
                $"I cannot confirm whether {intent.Detail} is fitted: {current.Name} carries "
                + $"{string.Join(", ", unnamed.Select(modification => modification.Speak()))}, which "
                + "Elite writes as symbols my table does not name.");
        }

        return new ChecklistVerdict(
            ChecklistState.Open,
            $"{intent.Detail} is not fitted to {current.Name}; "
            + $"{Knowledge.OnFootRules.SlotsAt(current.Grade.Value) - fitted.Count} slot"
            + $"{(Knowledge.OnFootRules.SlotsAt(current.Grade.Value) - fitted.Count == 1 ? "" : "s")} free.");
    }

    /// <summary>
    /// The suit or weapon an item is about, as it stands right now, or null when it is not the one
    /// the Commander is in — which is a "nothing can be said" rather than a "not done".
    /// </summary>
    private static OnFootSubject? Worn(ChecklistItem item, CommanderGameState state)
    {
        var loadout = state.OnFoot;

        if (!loadout.IsKnown || item.Scope.Key is not { Length: > 0 } key)
        {
            return null;
        }

        if (item.Scope.Group == ChecklistGroup.Suit)
        {
            return loadout.SuitId?.ToString(CultureInfo.InvariantCulture) == key
                ? new OnFootSubject(loadout.Speak(), loadout.Grade, loadout.SuitModifications)
                : null;
        }

        if (item.Scope.Group != ChecklistGroup.Weapon)
        {
            return null;
        }

        var weapon = loadout.Weapons.FirstOrDefault(carried =>
            carried.ModuleId?.ToString(CultureInfo.InvariantCulture) == key);

        return weapon is null
            ? null
            : new OnFootSubject(weapon.Speak(), weapon.Grade, weapon.Modifications);
    }

    /// <summary>A suit and a hand weapon answer the same three questions, so they share a shape.</summary>
    private readonly record struct OnFootSubject(
        string Name, int? Grade, IReadOnlyList<FittedModification> Modifications);

    private static bool IsActive(ChecklistScope scope, ShipLoadout loadout) =>
        scope.Group == ChecklistGroup.Ship
        && loadout.ShipId is { } id
        && string.Equals(scope.Key, id.ToString(CultureInfo.InvariantCulture), StringComparison.Ordinal);

    /// <summary>
    /// The module an intent's subject names — the slot first, then the fitted item, because a
    /// Commander says "thrusters" and a plan built from the ship says "MainEngines".
    /// </summary>
    private static ShipModule? Fitted(ShipLoadout loadout, string subject)
    {
        var wanted = ChecklistKeys.Compact(subject);

        if (wanted.Length == 0)
        {
            return null;
        }

        var bySlot = loadout.Modules.Where(module => ChecklistKeys.Compact(module.Slot) == wanted).ToList();

        if (bySlot.Count == 1)
        {
            return bySlot[0];
        }

        var byItem = loadout.Modules
            .Where(module => ChecklistKeys.Compact(ModuleNames.Readable(module.Item))
                .Contains(wanted, StringComparison.Ordinal))
            .ToList();

        // One match or nothing. Acting on the wrong module of two is worse than saying the slot
        // is empty, because only one of those is a mistake the Commander can see.
        return byItem.Count == 1 ? byItem[0] : null;
    }

    private static string Describe(ShipModule module) =>
        EliteSpecifications.Module(module.Item)?.Name ?? ModuleNames.Readable(module.Item);

    /// <summary>
    /// The Commander's rank with whoever would roll this: the engineer the plan names, or the one
    /// who rolled the module already. Null when neither is known, and the sentence then quotes no
    /// roll count rather than one computed against a rank d47 guessed.
    /// </summary>
    private static int? RankFor(ChecklistIntent intent, ShipModule module, CommanderGameState state)
    {
        var engineer = EngineerDirectory.ByName(intent.Engineer)
                       ?? (module.Engineer is { } rolled ? EngineerDirectory.ByName(rolled) : null);

        return engineer is null ? null : state.Engineers.For(engineer.Id)?.Rank;
    }

    private static string Remaining(int grade, int rank, double quality) =>
        EngineeringRules.RollsRemaining(grade, rank, quality) is { } rolls
            ? $"About {Rolls(rolls)} at your rank."
            : string.Empty;

    private static string Rolls(int count) => count == 1 ? "1 roll" : $"{count} rolls";
}
