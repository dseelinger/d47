using System.Globalization;
using D47.Core.Checklists;
using D47.Core.Journal;
using D47.Core.Knowledge;

namespace D47.Core.Loadout;

/// <summary>
/// One line of the on-foot index: a suit or a weapon, whatever is planned for it, and whether it
/// is on the Commander right now (list.md Phase 27, "The same page, on foot").
/// </summary>
/// <param name="Build">The build, or null for something carried that has no plan yet.</param>
/// <param name="Equipment">What it is called.</param>
/// <param name="Grade">The grade it is at now, where that is known.</param>
/// <param name="IsCarried">Whether the Commander is wearing or carrying it.</param>
public sealed record KitEntry(
    OnFootBuild? Build,
    OnFootKind Kind,
    string Equipment,
    long? ItemId,
    int? Grade,
    bool IsCarried)
{
    /// <summary>
    /// <b>Owned is derived and intended is authored</b>, the same rule the fleet page draws and
    /// the checklist drew before it.
    /// </summary>
    public bool IsOwned => ItemId is not null;

    public bool IsWeapon => Kind == OnFootKind.Weapon;

    /// <summary>How many slots this build has an opinion about. Zero for an unplanned item.</summary>
    public int Planned => Build?.Slots.Count(plan => !plan.IsEmpty) ?? 0;

    /// <summary>
    /// Where it is, in one phrase, or what it is waiting on.
    /// <para>
    /// <b>Elite reports the loadout the Commander is in and no other</b>, so there is no on-foot
    /// analogue of the ship racks: a suit in the locker is invisible until it is worn. Saying so
    /// is the honest answer, and it is why an item stops being reported the moment they change.
    /// </para>
    /// </summary>
    public string Where()
    {
        if (!IsOwned)
        {
            return "not bought yet";
        }

        if (!IsCarried)
        {
            return "not on you now";
        }

        return Grade is { } grade
            ? $"on you, grade {grade.ToString(CultureInfo.InvariantCulture)}"
            : "on you";
    }

    /// <summary>The line as the index shows it and as d47 says it.</summary>
    public string Describe() => $"{Equipment} — {Where()}";
}

/// <summary>
/// The Commander's suit and weapon builds, joined to what they are actually wearing (list.md
/// Phase 27, "The same page, on foot").
/// <para>
/// <b>The parallel of <see cref="Ships.ShipPlanService"/>, deliberately down to the method
/// names</b>, because the page above the two is one page instantiated twice. The plan owns
/// <em>what</em> and the checklist owns <em>when</em>; nothing here writes to the checklist, and
/// promotion goes through <see cref="ChecklistService.ProposePlan"/> where accepting stays the
/// Commander's own act.
/// </para>
/// <para>
/// <b>What is not the same is what is being planned.</b> A hull's slot is the journal's own name
/// for a place a module goes; an item on foot has a grade that has to be bought first and up to
/// four permanent modification slots, and the ordering between those is a routing fact rather than
/// a preference — an engineer's base has no Pioneer Supplies. So the grade is a slot of its own,
/// first, and <see cref="OnFootPlan"/> emits it as the first step when the build promotes.
/// </para>
/// <para>
/// Clock-free and thread-free like everything else in Core.
/// </para>
/// </summary>
public sealed class OnFootPlanService(
    OnFootBuildStore store,
    ChecklistService checklists,
    Func<CommanderGameState?> state)
{
    public OnFootBuildStore Store => store;

    /// <summary>
    /// The suit and weapons as the page shows them: what the Commander has on, then everything
    /// else they have planned.
    /// <para>
    /// The suit first and its weapons under it, because the suit's grade decides how many
    /// modification slots anything has — it is the line the rest of the page depends on.
    /// </para>
    /// </summary>
    public IReadOnlyList<KitEntry> Kit()
    {
        var loadout = state()?.OnFoot;
        var builds = store.Builds;
        var entries = new List<KitEntry>();
        var claimed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (loadout is { IsKnown: true })
        {
            if (loadout.SuitId is { } suitId)
            {
                var build = builds.FirstOrDefault(candidate =>
                    candidate.Kind == OnFootKind.Suit && candidate.ItemId == suitId);

                entries.Add(new KitEntry(
                    build,
                    OnFootKind.Suit,
                    loadout.Suit?.Name ?? ModuleNames.Readable(loadout.SuitSymbol),
                    suitId,
                    loadout.Grade,
                    IsCarried: true));

                if (build is not null)
                {
                    claimed.Add(build.Id);
                }
            }

            foreach (var weapon in loadout.Weapons)
            {
                if (weapon.ModuleId is not { } moduleId)
                {
                    continue;
                }

                var build = builds.FirstOrDefault(candidate =>
                    candidate.Kind == OnFootKind.Weapon && candidate.ItemId == moduleId);

                entries.Add(new KitEntry(
                    build,
                    OnFootKind.Weapon,
                    weapon.Entry?.Name ?? ModuleNames.Readable(weapon.Symbol),
                    moduleId,
                    weapon.Grade,
                    IsCarried: true));

                if (build is not null)
                {
                    claimed.Add(build.Id);
                }
            }
        }

        // Everything else the Commander has planned, including a build bound to an id they are
        // not currently carrying — which on foot is the ordinary case rather than the odd one,
        // since Elite reports one loadout and a second suit in the locker is invisible.
        foreach (var build in builds)
        {
            if (!claimed.Contains(build.Id))
            {
                entries.Add(new KitEntry(
                    build,
                    build.Kind,
                    build.Equipment,
                    build.ItemId,
                    OnFootCatalogue.Named(build.Equipment)?.Grade,
                    IsCarried: false));
            }
        }

        return entries;
    }

    /// <summary>The entry for one build, or null.</summary>
    public KitEntry? Entry(string buildId) =>
        Kit().FirstOrDefault(entry => entry.Build?.Id == buildId);

    /// <summary>
    /// The build a phrase means, made if it does not exist yet: the item named, the suit being
    /// worn when nothing is named, or a fresh intended build for something the Commander has not
    /// bought.
    /// <para>
    /// <b>Null on nought and on several rather than the first match</b>, the rule every other
    /// matcher in this repository follows.
    /// </para>
    /// </summary>
    public static OnFootBuild? Which(OnFootPlanService kit, string? named, bool weapon = false)
    {
        var carried = kit.Kit();

        if (string.IsNullOrWhiteSpace(named))
        {
            // Nothing named means the thing there is only one of: the suit being worn, or the one
            // weapon being carried. Two weapons and no name is a question rather than a guess.
            var candidates = carried
                .Where(entry => entry.IsCarried && entry.IsWeapon == weapon)
                .ToList();

            return candidates.Count == 1 ? kit.Existing(candidates[0]) : null;
        }

        var asked = ChecklistKeys.Compact(named);

        var byName = carried.Where(entry => ChecklistKeys.Compact(entry.Equipment) == asked).ToList();

        if (byName.Count == 1)
        {
            return kit.Existing(byName[0]);
        }

        // Nothing owned answers to that name. Something the shipped table knows is an intent; a
        // word it does not know is a question, and answering it with a new plan would be
        // inventing a suit.
        return byName.Count == 0 ? kit.Intend(named) : null;
    }

    /// <summary>The build behind an entry, started if the item has none yet.</summary>
    private OnFootBuild? Existing(KitEntry entry) => entry switch
    {
        { Build: { } build } => build,
        { ItemId: { } id } => BuildFor(entry.Kind, id, entry.Equipment),
        _ => null,
    };

    /// <summary>
    /// Starts a build for something the Commander owns, or hands back the one already there.
    /// <b>One build per item</b>, so this never makes a second.
    /// </summary>
    public OnFootBuild BuildFor(OnFootKind kind, long itemId, string equipment)
    {
        if (store.ForItem(kind, itemId) is { } existing)
        {
            return existing;
        }

        var build = new OnFootBuild(NextId(), equipment, kind, itemId);

        store.Save([.. store.Builds, build]);
        return build;
    }

    /// <summary>
    /// Starts a build for a suit or weapon the Commander does not own (list.md Phase 27).
    /// <para>
    /// The on-foot reading of <em>a hull you do not own is not in the fleet</em>: no
    /// <c>SuitID</c>, so acquiring the item is the plan's first step rather than a precondition
    /// sitting outside it. Null when the name is not a suit or a weapon — the shipped table is
    /// what answers that.
    /// </para>
    /// </summary>
    public OnFootBuild? Intend(string equipment)
    {
        if (OnFootCatalogue.Named(equipment) is not { } entry)
        {
            return null;
        }

        var build = new OnFootBuild(NextId(), entry.Name, entry.Kind, ItemId: null);

        store.Save([.. store.Builds, build]);
        return build;
    }

    /// <summary>
    /// Plans one slot, replacing whatever was planned there. Says nothing to the checklist: the
    /// plan owns what, and reaching the list is a separate act.
    /// </summary>
    public bool Plan(string buildId, KitPlan slot)
    {
        if (store.Find(buildId) is not { } build || !OnFootBuild.IsSlot(slot.Slot))
        {
            return false;
        }

        Replace(build.With(slot));
        return true;
    }

    /// <summary>Takes a slot's plan out, leaving the rest of the build alone.</summary>
    public bool Clear(string buildId, string slot)
    {
        if (store.Find(buildId) is not { } build || build.For(slot) is null)
        {
            return false;
        }

        Replace(build.Without(slot));
        return true;
    }

    /// <summary>
    /// The first modification slot with nothing in it, or null when all four are spoken for.
    /// <para>
    /// What a phrase naming a modification and no slot means. <b>The count is the grade's</b>, and
    /// the planned grade counts rather than the current one: planning a modification on a grade 1
    /// suit that the same build is taking to grade 4 is an ordinary thing to want, and refusing it
    /// because the suit has no slots <em>yet</em> would be refusing the plan for being a plan.
    /// </para>
    /// </summary>
    public string? FirstFreeSlot(OnFootBuild build, int? currentGrade)
    {
        var grade = build.PlannedGrade ?? currentGrade ?? 1;

        for (var index = 1; index <= Math.Max(OnFootRules.SlotsAt(grade), 0); index++)
        {
            if (build.For(OnFootBuild.ModSlot(index)) is null)
            {
                return OnFootBuild.ModSlot(index);
            }
        }

        return null;
    }

    /// <summary>
    /// Drops a build, and says what it left behind.
    /// <para>
    /// <b>What was already promoted is kept rather than vanished</b>, exactly as a ship build's
    /// is: the Commander ordered their list around those lines, and silently removing them is a
    /// history that lies.
    /// </para>
    /// </summary>
    public string Delete(string buildId)
    {
        if (store.Find(buildId) is not { } build)
        {
            return "There is no such build.";
        }

        store.Save([.. store.Builds.Where(other => other.Id != buildId)]);

        if (build.Scope is not { } scope)
        {
            return $"Dropped the plan for a {build.Equipment}.";
        }

        var promoted = checklists.Document.Items
            .Any(item => item.IsLive
                         && item.Scope.Same(scope)
                         && item.Source == ChecklistSource.OnFootPlan);

        if (!promoted)
        {
            return $"Dropped the plan for {build.Describe()}.";
        }

        return $"Dropped the plan for {build.Describe()}. What it already put on your checklist is "
               + "still there, on the Checklist tab, because you ordered your list around it.";
    }

    /// <summary>
    /// Offers a build to the checklist (list.md Phase 27).
    /// <para>
    /// <b>The grade first and the modifications after it, always</b>, which is
    /// <see cref="OnFootPlan.Items"/>'s own ordering rather than something imposed here: a grade 1
    /// item has no modification slots, and an engineer's base has no Pioneer Supplies, so the
    /// order is a routing fact that cannot be fixed on arrival.
    /// </para>
    /// </summary>
    public string Promote(string buildId)
    {
        if (store.Find(buildId) is not { } build)
        {
            return "There is no such build.";
        }

        if (build.Scope is not { } scope)
        {
            // No SuitID, so there is no list for its items to be in. Said rather than invented.
            return $"You do not own that {build.Equipment} yet, so there is no list to put it on. "
                   + "Buy one and I will offer to adopt this plan onto it.";
        }

        var items = OnFootPlan.Items(
            scope,
            new OnFootRequest(build.Equipment, build.PlannedGrade, build.Modifications));

        if (items.Count == 0)
        {
            return $"Nothing is planned for {build.Describe()} yet.";
        }

        // Every item of this plan shares one subject — the equipment — so the whole build is what
        // is replaced, and a slot the Commander cleared drops off the list rather than standing
        // because nothing mentioned it.
        return checklists.ProposePlan(scope, ChecklistSource.OnFootPlan, items, [build.Equipment]);
    }

    /// <summary>
    /// Watches for the Commander buying something they had planned for, and adopts the plan onto
    /// it (list.md Phase 27).
    /// <para>
    /// <b>On foot the buy event carries the id, which is the opposite of the ship side.</b>
    /// <c>ShipyardBuy</c> names the hull and no id for the new ship at all, so Phase 26 had to
    /// match on the <c>ShipyardNew</c> written after it; measured against the same journals,
    /// <c>BuySuit</c> carries <c>SuitID</c> and <c>BuyWeapon</c> carries <c>SuitModuleID</c> in
    /// the buy itself. So this is one event rather than two, and it is worth stating because the
    /// symmetry everywhere else in this file would suggest otherwise.
    /// </para>
    /// <para>
    /// Only ever the <em>first</em> matching prospective build, and only on an exact name match.
    /// Two Mavericks planned and one bought is a question rather than a guess.
    /// </para>
    /// </summary>
    public IReadOnlyList<string> Observe(IEnumerable<JournalEvent> events)
    {
        var said = new List<string>();

        foreach (var journalEvent in events)
        {
            var (kind, idField) = journalEvent.Kind switch
            {
                "BuySuit" => (OnFootKind.Suit, "SuitID"),
                "BuyWeapon" => (OnFootKind.Weapon, "SuitModuleID"),
                _ => (OnFootKind.Unknown, string.Empty),
            };

            if (kind == OnFootKind.Unknown)
            {
                continue;
            }

            // The symbol, never Name_Localised: Frontier's own localisation reports every suit
            // above grade 1 as Class1, which would adopt the plan onto the wrong grade's name.
            if (OnFootCatalogue.Find(journalEvent.String("Name")) is not { } entry
                || journalEvent.Long(idField) is not { } itemId)
            {
                continue;
            }

            var candidates = store.Builds
                .Where(build => !build.IsOwned
                                && build.Kind == kind
                                && string.Equals(build.Equipment, entry.Name, StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (candidates.Count != 1)
            {
                continue;
            }

            said.Add(Adopt(candidates[0].Id, itemId));
        }

        return said;
    }

    /// <summary>
    /// Binds a prospective build to a real journal id. The plan keeps its own identity throughout,
    /// which is what there was to rebind.
    /// </summary>
    public string Adopt(string buildId, long itemId)
    {
        if (store.Find(buildId) is not { } build)
        {
            return "There is no such build.";
        }

        if (store.ForItem(build.Kind, itemId) is { } already && already.Id != buildId)
        {
            return $"Item {itemId.ToString(CultureInfo.InvariantCulture)} already has a build, and an item has one.";
        }

        Replace(build with { ItemId = itemId });

        return $"That {build.Equipment} is yours now, and the plan you had for one is pointed at it.";
    }

    private void Replace(OnFootBuild build) =>
        store.Save([.. store.Builds.Select(other => other.Id == build.Id ? build : other)]);

    /// <summary>
    /// The next build identity: the lowest number nothing is using. Deterministic, because Core
    /// reads no clock and owns no randomness — so a replay produces the same file every run.
    /// </summary>
    private string NextId()
    {
        var taken = new HashSet<int>();

        foreach (var build in store.Builds)
        {
            if (build.Id.StartsWith("kit-", StringComparison.OrdinalIgnoreCase)
                && int.TryParse(build.Id[4..], CultureInfo.InvariantCulture, out var number))
            {
                taken.Add(number);
            }
        }

        var next = 1;

        while (taken.Contains(next))
        {
            next++;
        }

        return "kit-" + next.ToString(CultureInfo.InvariantCulture);
    }
}
