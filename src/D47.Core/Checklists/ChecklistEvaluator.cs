using System.Globalization;
using D47.Core.Journal;
using D47.Core.Knowledge;

namespace D47.Core.Checklists;

/// <summary>Where an item stands, and the sentence that says so.</summary>
/// <param name="Reason">
/// What is true of <em>this line</em>: the module, the slot, the grade it is at and the grade it
/// wants. Per item, because a line has to be able to say why it is blocked on its own — spoken
/// alone, read through <c>get_checklist</c>, or seen on a filtered page where its neighbours are
/// not there.
/// </param>
public readonly record struct ChecklistVerdict(ChecklistState State, string Reason)
{
    /// <summary>
    /// What is true of the Commander's <em>relationship with an engineer</em>, kept apart from the
    /// reason rather than appended to it
    /// (<a href="https://github.com/dseelinger/d47/issues/26">#26</a>).
    /// <para>
    /// <b>The state is per line; the explanation is per engineer.</b> "Grade 5 cannot be crafted at
    /// rank 1" is a fact about this module's plan. "Rank rises by working with them, and it
    /// compounds" is a fact about the engineer, and it is the same sentence however many modules
    /// are waiting on it — which is what made it read as canned when six lines said it in a row
    /// (<a href="https://github.com/dseelinger/d47/issues/33">#33</a>).
    /// </para>
    /// <para>
    /// <b>Separating it changes nothing a Commander sees today</b>, and that is deliberate:
    /// <see cref="Says"/> composes the two in the order they were always written, so every drawn
    /// line, spoken answer and tool result is byte-identical to before. What it buys is that the
    /// two can now be told apart by anything that wants to — a surface that shows the explanation
    /// once for a page rather than once per line, or a route that hands it to a model as a fact to
    /// put in its own words instead of a finished sentence to repeat.
    /// </para>
    /// <para>
    /// <b>Core stays deterministic and knows no model.</b> This is a field, not a seam: it carries
    /// a sentence the rules already produced, and where that sentence is varied — if it is — is a
    /// decision for the layer that speaks, which is where <c>FlavourBriefs</c> already lives.
    /// </para>
    /// </summary>
    public string? Advice { get; init; }

    /// <summary>
    /// The whole verdict as one sentence, which is what every surface has always drawn. Reason
    /// first, then the explanation, separated by a single space — exactly the string that used to
    /// be built by appending one to the other at the point it was decided.
    /// </summary>
    public string Says => Advice is { Length: > 0 } advice ? $"{Reason} {advice}" : Reason;
}

/// <summary>
/// What the journal says about a derived item (Phase 17, "Per ship build
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
        var aboard = IsActive(item.Scope, state.Ship);

        // <b>A ship in another dock is diffed from the loadout d47 remembers.</b> The comment that
        // stood here said it "cannot be diffed at all", and that was true until Phase 37
        // started remembering them — after which a line about a parked ship read out its module by
        // name, from the remembered loadout, over a verdict that had refused to look at the same
        // place. The line and the verdict beneath it read different sources, and one report of that
        // asymmetry arrived as d47 "not being able to see" a ship it could describe.
        //
        // <b>A remembered loadout is a fact about a moment</b>, so every verdict from one says
        // which moment. That is the one way this can do harm: a month-old snapshot presented as
        // current is worse than the silence it replaces. Still silent where the ship has never been
        // seen at all, which is the honest answer rather than an invented one.
        var remembered = aboard ? null : Remembered(item, state);
        var loadout = aboard ? state.Ship : remembered?.Loadout;

        if (loadout is null)
        {
            return null;
        }

        var verdict = Ship(item, intent, state, loadout);

        return verdict is not { } said || remembered is not { } seen
            ? verdict
            : said with { Reason = $"{said.Reason} As last seen, {Seen(seen)}." };
    }

    /// <summary>
    /// The remembered loadout for a ship-scoped item, or null where the ship is not one d47 has
    /// been aboard. Same read as <c>EngineerAtHand.LoadoutFor</c>, which is what the line's own
    /// wording already comes from.
    /// </summary>
    private static RememberedShip? Remembered(ChecklistItem item, CommanderGameState state) =>
        item.Scope.Group == ChecklistGroup.Ship
        && int.TryParse(item.Scope.Key, NumberStyles.Integer, CultureInfo.InvariantCulture, out var shipId)
            ? state.Loadouts.For(shipId)
            : null;

    /// <summary>
    /// When the snapshot was taken, off the journal event that carried it. <b>A date and never
    /// "three days ago"</b>: no Core component reads the clock, and a relative age computed from
    /// one would be a different sentence every tick.
    /// </summary>
    private static string Seen(RememberedShip ship) =>
        ship.SeenAt.UtcDateTime.ToString("d MMM yyyy", CultureInfo.InvariantCulture);

    private static ChecklistVerdict? Ship(
        ChecklistItem item,
        ChecklistIntent intent,
        CommanderGameState state,
        ShipLoadout loadout)
    {

        // A ShipID now reporting a different hull makes the list stale and says so, rather than
        // quietly diffing an exploration Krait against a Cutter.
        //
        // **Compared as hulls and not as strings** (reported 2026-08-20). A plan can carry either
        // spelling — `StoredShips` writes `ShipType_Localised` where Frontier supplies one, so a
        // build started from the fleet holds "Panther Clipper Mk II" where one started from a
        // Loadout holds `panthermkii`. Matched by text those are two different ships, and every
        // slot of that plan reported itself stale against the ship it was written for.
        if (item.Hull is { } hull
            && loadout.Type is { } type
            && !SameHull(hull, type))
        {
            return new ChecklistVerdict(
                ChecklistState.Stale,
                $"That ship id now reports a {loadout.TypeName ?? type}, and this plan was written for a {hull}.");
        }

        var module = Fitted(loadout, intent.Subject);

        if (module is null)
        {
            return Missing(intent, state, loadout);
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
                + (left is { } rolls ? $", about {Crafts(rolls)} short." : "."));
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
            + (total is { } rolls ? $" Grade {intent.Grade} is {Crafts(rolls)} from here." : string.Empty));
    }

    /// <summary>
    /// A rank gate, which is not a slow route but no route at all — and the block has a published
    /// price, so this says what it costs to clear rather than naming a blocker and shrugging at it.
    /// </summary>
    private static ChecklistVerdict Gated(ChecklistIntent intent, int grade, int rank)
    {
        var who = intent.Engineer is { } engineer ? $" with {engineer}" : string.Empty;

        // The explanation rides its own field rather than the sentence (#26): it is a fact about
        // the engineer, not about this module, and Says composes the two exactly as they read
        // before.
        return new ChecklistVerdict(
            ChecklistState.Blocked,
            $"Grade {grade} cannot be crafted at rank {rank}{who} at all — no amount of gathering fixes that.")
        {
            Advice = EngineeringRules.RankRises,
        };
    }

    /// <summary>
    /// Nothing in the slot. <b><see cref="ChecklistState.Elsewhere"/> is its own state</b>, because
    /// "you own that already, it is in Deciat, and moving it costs 2.1 million" is a completely
    /// different next action from "go and grind it".
    /// </summary>
    /// <param name="loadout">
    /// The ship this slot is on, so the slot can be named as a Commander says it rather than as
    /// the journal spells it (asked for 2026-08-24).
    /// <para>
    /// <b>This line became load-bearing the moment the title stopped carrying the slot.</b> The
    /// title now names the module — <em>Grade 5 Heavy Duty on Shield Booster</em> — which is what
    /// was asked for, and which means this sentence is the only thing left saying <em>which</em> of
    /// eight utility mounts. <c>TinyHardpoint8</c> is not that thing; <em>Utility Mount 8</em> is.
    /// </para>
    /// </param>
    private static ChecklistVerdict Missing(
        ChecklistIntent intent, CommanderGameState state, ShipLoadout loadout)
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

        var where = Knowledge.EliteSpecifications.Slot(loadout.Type, intent.Subject)?.Describe()
                    ?? intent.Subject;

        return new ChecklistVerdict(ChecklistState.Open, $"Nothing is fitted in {where}.");
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

        return new ChecklistVerdict(
            ChecklistState.Open, $"{engineer.Name} is at rank {rank} of {wanted}.")
        {
            Advice = EngineeringRules.RankRises,
        };
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

    /// <summary>
    /// Whether two hull spellings name the same ship. Text first, because that is the common case
    /// and costs nothing; the table behind it, because a display name and a symbol are the same
    /// hull and only <see cref="EliteSpecifications.Ship"/> knows it.
    /// <para>
    /// A spelling the table does not carry falls back to the text comparison, so an unknown hull
    /// is still compared rather than being quietly called a match.
    /// </para>
    /// </summary>
    private static bool SameHull(string planned, string flying) =>
        string.Equals(planned, flying, StringComparison.OrdinalIgnoreCase)
        || (EliteSpecifications.Ship(planned) is { } was
            && EliteSpecifications.Ship(flying) is { } now
            && string.Equals(was.Symbol, now.Symbol, StringComparison.OrdinalIgnoreCase));

    // Internal rather than private since Phase 42: the ordering asks the same question — is this
    // ship-scoped line about the ship being flown — and two answers to it would drift.
    internal static bool IsActive(ChecklistScope scope, ShipLoadout loadout) =>
        scope.Group == ChecklistGroup.Ship
        && loadout.ShipId is { } id
        && string.Equals(scope.Key, id.ToString(CultureInfo.InvariantCulture), StringComparison.Ordinal);

    /// <summary>
    /// The module an intent's subject names — the slot first, then the fitted item, because a
    /// Commander says "thrusters" and a plan built from the ship says "MainEngines".
    /// <para>
    /// Internal rather than private so <see cref="ChecklistWording"/> names the same module on the
    /// line that this names in the verdict under it. Two matching rules for one question is two
    /// things to keep in step.
    /// </para>
    /// </summary>
    internal static ShipModule? Fitted(ShipLoadout loadout, string subject)
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

    /// <summary>
    /// The module as a Commander says it: the size and rating in front of the name, which is the
    /// order outfitting lists it in.
    /// <para>
    /// <b>The size is part of the name here</b> (reported 2026-08-21). A ship carries several
    /// shield generators over its life and a plan is about one of them, so "Shield Generator" in
    /// the verdict under a line reading "on 7A Shield Generator" leaves the Commander to work out
    /// whether the two are the same module.
    /// </para>
    /// <para>
    /// A bulkhead has neither class nor rating, and <see cref="ModuleSpecification.Size"/> falls
    /// back to the name for exactly that case — so it is asked separately rather than said twice.
    /// </para>
    /// <para>
    /// Internal so <see cref="ChecklistWording"/> spells the module the same way on the line as
    /// this does in the verdict under it, which is the whole of what was reported.
    /// </para>
    /// </summary>
    internal static string Describe(ShipModule module)
    {
        if (EliteSpecifications.Module(module.Item) is not { } specification)
        {
            return ModuleNames.Readable(module.Item);
        }

        return specification.IsBulkhead
            ? specification.Name
            : $"{specification.Size} {specification.Name}";
    }

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
            ? $"About {Crafts(rolls)} at your rank."
            : string.Empty;

    private static string Crafts(int count) => count == 1 ? "1 craft" : $"{count} crafts";
}
