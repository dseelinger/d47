using System.Globalization;
using D47.Core.Checklists;
using D47.Core.Engineers;
using D47.Core.Interface;
using D47.Core.Journal;
using D47.Core.Knowledge;
using D47.Core.Loadout;

namespace D47.App.Panel;

/// <summary>The Loadout tab's Suits and weapons mode (Phase 27, "The same page, on foot").</summary>
public sealed class OnFootMode(
    OnFootPlanService kit,
    ChecklistService checklists,
    Func<CommanderGameState?> state) : ILoadoutMode
{
    /// <summary>The Loadout tab's Suits root.</summary>
    public const string Root = "loadout.onfoot";

    /// <summary>How an item's crumb is keyed, and a mod slot's below it.</summary>
    public const string KitPrefix = "loadout.kit:";

    public const string KitSlotPrefix = "loadout.kitslot:";

    /// <summary>A row for something carried that nothing has planned for yet.</summary>
    private const string Unplanned = "new:";

    public string RootKey => Root;

    public string RootWord => "Suits";

    public string ItemPrefix => KitPrefix;

    public string SlotPrefix => KitSlotPrefix;

    public string? SlotHelp => D47.Core.Capabilities.Builtin.OnFootCapability.Id;

    public event Action? Changed
    {
        add
        {
            kit.Store.Changed += value;
            checklists.Proposals.Changed += value;
            _invalidated += value;
        }

        remove
        {
            kit.Store.Changed -= value;
            checklists.Proposals.Changed -= value;
            _invalidated -= value;
        }
    }

    private event Action? _invalidated;

    /// <summary>The journal moved under the page — the Commander jumped, or an engineer ranked up (#195).</summary>
    public void Invalidate() => _invalidated?.Invoke();

    public string EmptyIndex =>
        "I have not seen you on foot yet. Step out of the ship and I will read what you are "
        + "wearing — or plan a suit you do not own, and buying one will point the plan at it.";

    public string EmptySlots =>
        "Nothing is planned, and I cannot see this item — Elite only reports the loadout you are "
        + "in. Plan the grade and it will appear here.";

    public string NewLabel => "Plan a suit or weapon you do not own";

    public string PromoteLabel => "Put this plan on my checklist";

    /// <summary>Never.</summary>
    public bool CanCopy(string item, string from, string to) => false;

    public string Copy(string item, string from, string to) => "That cannot go there.";

    public string SayAtIndex => "what have I planned on foot";

    public string SayAtItem => "put that on my checklist";

    public string SayAtSlot(string slot) =>
        string.Equals(slot, OnFootBuild.GradeSlot, StringComparison.OrdinalIgnoreCase)
            ? "take my Maverick to grade 5"
            : "plan night vision on my suit";

    public bool Cards => true;

    public IReadOnlyList<LoadoutRow> Items() =>
    [
        .. kit.Kit().Select(entry => new LoadoutRow(
            Key(entry),
            entry.Equipment,
            entry.Equipment,
            Aside(entry),
            entry.Planned > 0)
        {
            Standing = entry.IsCarried
                ? LoadoutStanding.Active
                : entry.IsOwned
                    ? LoadoutStanding.Owned
                    : LoadoutStanding.Wanted,
        }),
    ];

    /// <summary>A suit or weapon's card lines: kind and grade, where it is, and how its plans stand —
    /// each its own line, as the fleet's cards are (#297).</summary>
    private static string Aside(KitEntry entry) =>
        Lines(KindLine(entry), WhereLine(entry), PlannedLine(entry.Planned));

    private static string KindLine(KitEntry entry) =>
        entry.Grade is { } grade
            ? $"{Kind(entry)}, grade {grade.ToString(CultureInfo.InvariantCulture)}"
            : Kind(entry);

    private static string Kind(KitEntry entry) => entry.IsWeapon ? "Weapon" : "Suit";

    private static string WhereLine(KitEntry entry)
    {
        if (!entry.IsOwned)
        {
            return "not bought yet";
        }

        if (entry.IsCarried)
        {
            return "on you";
        }

        return entry.SeenAt is { } seen
            ? $"last seen {seen.UtcDateTime.ToString("d MMM yyyy", CultureInfo.InvariantCulture)}"
            : "not on you now";
    }

    private static string? PlannedLine(int planned) =>
        planned > 0 ? $"{planned.ToString(CultureInfo.InvariantCulture)} planned" : null;

    /// <summary>Every non-empty line, joined for <see cref="LoadoutPages.Card"/> to draw one apiece.</summary>
    private static string Lines(params string?[] lines) =>
        string.Join('\n', lines.Where(line => line is { Length: > 0 }));

    /// <summary>Something the Commander does not own.</summary>
    public void New(PanelPrompts prompts, Action done) =>
        prompts.Enter(
            new EntryRequest(
                "loadout.intendkit",
                "Suit or weapon",
                "Which suit or weapon do you intend to buy?",
                "Buying one is the plan's first step rather than a precondition sitting outside it.",
                string.Empty,
                EntrySurface.Voice,
                value => OnFootCatalogue.Named(value) is null
                    ? EntryVerdict.No($"I do not know a suit or weapon called “{value}”.")
                    : EntryVerdict.Ok,
                Equipment,
                "Plan this"),
            equipment =>
            {
                kit.Intend(equipment);
                done();
            });

    /// <summary>
    /// Every suit and weapon a build could name, alphabetical and each once — the flight suit left off
    /// because it is never bought (#298).
    /// </summary>
    private static IReadOnlyList<string> Equipment { get; } =
    [
        .. OnFootCatalogue.Equipment
            .Where(name => !string.Equals(name, "Flight Suit", StringComparison.OrdinalIgnoreCase)),
    ];

    public string? Summary(string item)
    {
        if (Resolve(item) is not { } build)
        {
            return null;
        }

        if (!build.IsOwned)
        {
            return $"{build.Describe()}. Buying one will point this plan at it.";
        }

        var grade = Current(build);

        // The grade first, because it is the line the rest of the page depends on: a grade 1 item has no
        // modification slots at all, so a trip to an engineer before Pioneer Supplies is a wasted trip.
        return grade is { } known
            ? $"{build.Equipment}, grade {known.ToString(CultureInfo.InvariantCulture)}. "
              + $"{OnFootRules.SlotsAt(known).ToString(CultureInfo.InvariantCulture)} modification "
              + "slots at that grade, and every one of them permanent."
            : $"{build.Equipment}. Modifications are permanent: four at most, and a wrong one is "
              + "recoverable only by buying and re-upgrading a fresh item.";
    }

    /// <summary>Nothing beyond the summary.</summary>
    public IReadOnlyList<LoadoutLine> Details(string item) => [];

    public IReadOnlyList<LoadoutRow> Slots(string item)
    {
        if (Resolve(item) is not { } build)
        {
            return [];
        }

        var rows = new List<LoadoutRow>();
        var current = Current(build);

        rows.Add(Row(build, OnFootBuild.GradeSlot, current));

        // The slots the grade being aimed at will have, and any the Commander planned above that — a plan is
        // never hidden because the item has not caught up with it yet.
        var slots = Math.Max(
            OnFootRules.SlotsAt(build.PlannedGrade ?? current ?? 1),
            build.Slots.Count(plan => !string.Equals(
                plan.Slot, OnFootBuild.GradeSlot, StringComparison.OrdinalIgnoreCase)));

        for (var index = 1; index <= Math.Min(slots, OnFootRules.MaxSlots); index++)
        {
            rows.Add(Row(build, OnFootBuild.ModSlot(index), current));
        }

        return rows;
    }

    public string Promote(string item) =>
        Resolve(item) is { } build ? kit.Promote(build.Id) : "That plan is not there any more.";

    /// <summary>The question left on the tab when Promote proposed a plan and it was not answered (#299),
    /// the same shape as the fleet's (Phase 38, "Ask before the plan and the checklist drift apart").</summary>
    public LoadoutNotice? Notice()
    {
        var waiting = checklists.Proposals
            .PendingFor(checklists.Document.CommanderFid)
            .FirstOrDefault(proposal => proposal.Kind == ProposalKind.Plan
                                        && proposal.Source == ChecklistSource.OnFootPlan
                                        && proposal.Scope.Group is ChecklistGroup.Suit or ChecklistGroup.Weapon);

        if (waiting is null)
        {
            return null;
        }

        var id = waiting.Id;

        return new LoadoutNotice(
            $"{waiting.Summary.TrimEnd('.')}?",
            () => checklists.Accept(id),
            () => checklists.Decline(id));
    }

    /// <summary>The same rule as the fleet's: what was authored can be dropped, what is worn cannot.</summary>
    public string? DropLabel(string item) =>
        Resolve(item) is { IsOwned: false } ? "Drop this plan" : null;

    public string Drop(string item) =>
        Resolve(item) is { } build ? kit.Delete(build.Id) : "That plan is not there any more.";

    public bool HasPlan(string item, string slot) => Resolve(item)?.For(slot) is not null;

    public void Clear(string item, string slot)
    {
        if (Resolve(item) is { } build)
        {
            kit.Clear(build.Id, slot);
        }
    }

    public IReadOnlyList<LoadoutLine> Fitted(string item, string slot)
    {
        if (Resolve(item) is not { } build)
        {
            return [new LoadoutLine("That plan is not there any more.")];
        }

        if (Worn(build) is not { } worn)
        {
            return
            [
                new LoadoutLine(
                    "Elite reports the loadout you are in and no other, so I cannot say what this "
                    + "item is right now."),
            ];
        }

        if (IsGrade(slot))
        {
            var text = worn.Grade is { } grade
                ? $"Grade {grade.ToString(CultureInfo.InvariantCulture)}"
                : "No grade — the flight suit carries none and cannot be upgraded.";

            if (worn.SeenAt is { } seenGrade)
            {
                text += $" As last seen, {Seen(seenGrade)}.";
            }

            return
            [
                new LoadoutLine(text, LoadoutTone.Body),

                new LoadoutLine(worn.Grade is { } known
                    ? $"{OnFootRules.SlotsAt(known).ToString(CultureInfo.InvariantCulture)} "
                      + "modification slots at this grade."
                    : "Nothing can be fitted to it."),
            ];
        }

        // Every modification on the item, on every mod slot's page, and the reason said outright.
        var modifications = worn.Modifications.Count == 0
            ? "Nothing."
            : string.Join(", ", worn.Modifications.Select(modification => modification.Speak()));

        if (worn.SeenAt is { } seenMods)
        {
            modifications += $" As last seen, {Seen(seenMods)}.";
        }

        var lines = new List<LoadoutLine> { new(modifications, LoadoutTone.Body) };

        if (worn.Modifications.Count > 0)
        {
            lines.Add(new LoadoutLine(
                "Elite reports these as a set with no slot numbers, so the numbers here order your "
                + "plan rather than the item."));
        }

        return lines;
    }

    /// <summary>When the ledger last saw an item, in the wording <c>ChecklistEvaluator</c> uses for parked ships.</summary>
    private static string Seen(DateTimeOffset seenAt) =>
        seenAt.UtcDateTime.ToString("d MMM yyyy", CultureInfo.InvariantCulture);

    public IReadOnlyList<LoadoutLine> Planned(string item, string slot)
    {
        if (Resolve(item) is not { } build)
        {
            return [];
        }

        if (build.For(slot) is not { IsEmpty: false } plan)
        {
            return
            [
                new LoadoutLine(IsGrade(slot)
                    ? "No grade planned. On foot an unstated grade means no upgrade rather than any "
                      + "upgrade — the grade is bought outright, not rolled."
                    : "Nothing planned for this slot."),
            ];
        }

        var lines = new List<LoadoutLine> { new(plan.Describe(), LoadoutTone.Body) };

        if (build.Scope is not { } scope)
        {
            lines.Add(new LoadoutLine(
                "You do not own one yet, so there is nothing to check this against."));

            return lines;
        }

        var items = OnFootPlan.Items(
            scope,
            new OnFootRequest(
                build.Equipment,
                IsGrade(slot) ? plan.Grade : null,
                plan.Modification is { Length: > 0 } modification ? [modification] : null));

        foreach (var costed in items)
        {
            var verdict = ChecklistEvaluator.Evaluate(costed, state());

            lines.Add(new LoadoutLine(
                verdict?.Says
                ?? "Nothing can be said about this right now — Elite reports the loadout you are in "
                   + "and no other.",
                verdict is { } answered && ChecklistNextAction.IsWrong(answered.State)
                    ? LoadoutTone.Danger
                    : LoadoutTone.Muted));
        }

        lines.AddRange(Cost(items));

        return lines;
    }

    public IReadOnlyList<LoadoutLine> Engineers(string item, string slot)
    {
        if (IsGrade(slot)
            || Resolve(item) is not { } build
            || build.For(slot) is not { IsEmpty: false } plan
            || plan.Modification is not { Length: > 0 } modification)
        {
            return [];
        }

        var named = ModificationEngineers(modification);

        return named.Count == 0
            ? []
            : EngineerLines.For(PlanEngineers.For(
                named, grade: null, state()?.Engineers, state()?.Location.StarPos));
    }

    /// <summary>Who the table names for a suit or weapon modification — every one of them (#195).</summary>
    private static IReadOnlyList<string> ModificationEngineers(string modification) =>
    [
        .. BlueprintCatalogue.All
            .Where(blueprint => blueprint.Kind is BlueprintKind.Suit or BlueprintKind.Weapon)
            .Where(blueprint => ChecklistKeys.Compact(blueprint.Name) == ChecklistKeys.Compact(modification))
            .SelectMany(blueprint => blueprint.Engineers)
            .Distinct(StringComparer.OrdinalIgnoreCase),
    ];

    /// <summary>Asks for the grade, or for a modification.</summary>
    public void Ask(string item, string slot, PanelPrompts prompts, Action done)
    {
        if (Resolve(item) is not { } build)
        {
            return;
        }

        if (IsGrade(slot))
        {
            prompts.Enter(
                new EntryRequest(
                    "loadout.kitgrade",
                    "Grade",
                    $"Which grade of {build.Equipment}?",
                    "2 to 5, bought at Pioneer Supplies. It is the first step of the plan, because "
                    + "a grade 1 item has no modification slots.",
                    build.PlannedGrade?.ToString(CultureInfo.InvariantCulture) ?? string.Empty,
                    EntrySurface.Keyboard,
                    value => int.TryParse(value.Trim(), out var grade) && grade is >= 2 and <= 5
                        ? EntryVerdict.Ok
                        : EntryVerdict.No("A grade to buy is 2 to 5.")),
                grade =>
                {
                    kit.Plan(
                        build.Id,
                        new KitPlan(
                            OnFootBuild.GradeSlot,
                            int.TryParse(grade.Trim(), out var wanted) ? wanted : null));

                    done();
                });

            return;
        }

        prompts.Enter(
            new EntryRequest(
                "loadout.kitmod",
                "Modification",
                $"What do you want on {slot}?",
                "Permanent, once an engineer has fitted it. It does not reach your checklist until "
                + "you promote the plan.",
                build.For(slot)?.Modification ?? string.Empty,
                EntrySurface.Voice,
                value => IsModification(value)
                    ? EntryVerdict.Ok
                    : EntryVerdict.No($"I have no on-foot modification called “{value}”."),
                ModificationsFor(build, slot),
                "Plan this"),
            modification =>
            {
                kit.Plan(build.Id, new KitPlan(slot, Modification: modification.Trim()));
                done();
            });
    }

    /// <summary>
    /// The modifications this build's kind takes, minus anything already planned on another slot of
    /// the same build (#298).
    /// </summary>
    private static IReadOnlyList<string> ModificationsFor(OnFootBuild build, string slot)
    {
        var kind = build.IsWeapon ? OnFootKind.WeaponModification : OnFootKind.SuitModification;

        var takenElsewhere = build.Slots
            .Where(plan => !string.Equals(plan.Slot, slot, StringComparison.OrdinalIgnoreCase))
            .Where(plan => plan.Modification is { Length: > 0 })
            .Select(plan => plan.Modification!)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        return
        [
            .. OnFootCatalogue.All
                .Where(entry => entry.Kind == kind)
                .Select(entry => entry.Name)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Where(name => !takenElsewhere.Contains(name))
                .Order(StringComparer.OrdinalIgnoreCase),
        ];
    }

    /// <summary>What this plan costs.</summary>
    private IReadOnlyList<LoadoutLine> Cost(IReadOnlyList<ChecklistItem> items)
    {
        var costing = OnFootPlan.Cost(items, state());
        var lines = new List<LoadoutLine>();

        foreach (var gate in costing.Gates)
        {
            lines.Add(new LoadoutLine(gate, LoadoutTone.Danger));
        }

        foreach (var unknown in costing.Uncovered)
        {
            lines.Add(new LoadoutLine(unknown));
        }

        if (costing.Ingredients.Count == 0)
        {
            return lines;
        }

        lines.Add(new LoadoutLine("What it costs", LoadoutTone.Heading));

        foreach (var ingredient in costing.Ingredients.OrderByDescending(entry => entry.Short))
        {
            lines.Add(new LoadoutLine(
                $"{ingredient.Material.Name}: {ingredient.Held} of {ingredient.Needed}"
                + (ingredient.Short > 0 ? $", {ingredient.Short} short" : string.Empty)));
        }

        return lines;
    }

    private LoadoutRow Row(OnFootBuild build, string slot, int? current)
    {
        var plan = build.For(slot);

        var aside = plan is { IsEmpty: false }
            ? plan.Describe()
            : IsGrade(slot) && current is { } grade
                ? $"grade {grade.ToString(CultureInfo.InvariantCulture)} now"
                : null;

        return new LoadoutRow($"{build.Id}|{slot}", slot, slot, aside, plan is { IsEmpty: false });
    }

    private static bool IsGrade(string slot) =>
        string.Equals(slot, OnFootBuild.GradeSlot, StringComparison.OrdinalIgnoreCase);

    /// <summary>Whether a spoken word names a modification anything shipped knows.</summary>
    private static bool IsModification(string value)
    {
        var asked = ChecklistKeys.Compact(value);

        if (asked.Length == 0)
        {
            return false;
        }

        return OnFootCatalogue.All.Any(entry =>
                   entry.IsModification && ChecklistKeys.Compact(entry.Name) == asked)
               || BlueprintCatalogue.All.Any(blueprint =>
                   blueprint.Kind is BlueprintKind.Suit or BlueprintKind.Weapon
                   && ChecklistKeys.Compact(Bare(blueprint.Name)) == asked);
    }

    /// <summary>A recipe name with the manufacturer in brackets taken off.</summary>
    private static string Bare(string name)
    {
        var bracket = name.IndexOf('(', StringComparison.Ordinal);

        return bracket < 0 ? name : name[..bracket].TrimEnd();
    }

    /// <summary>The grade the item is at now, where the Commander has it on them.</summary>
    private int? Current(OnFootBuild build) => Worn(build)?.Grade;

    /// <summary>
    /// The item as it stands right now, from the loadout Elite last reported when the Commander is in
    /// it, or from the ledger when they are not — which for something owned and unworn is the ordinary
    /// case on foot rather than a "nothing can be said".
    /// </summary>
    private Carried? Worn(OnFootBuild build)
    {
        if (build.ItemId is not { } id)
        {
            return null;
        }

        if (state()?.OnFoot is { IsKnown: true } loadout)
        {
            if (!build.IsWeapon && loadout.SuitId == id)
            {
                return new Carried(loadout.Grade, loadout.SuitModifications);
            }

            if (build.IsWeapon
                && loadout.Weapons.FirstOrDefault(carried => carried.ModuleId == id) is { } weapon)
            {
                return new Carried(weapon.Grade, weapon.Modifications);
            }
        }

        var owned = state()?.Kit;

        if (!build.IsWeapon && owned?.Suits.GetValueOrDefault(id) is { } suit)
        {
            return new Carried(suit.Grade, suit.Modifications, suit.SeenAt);
        }

        if (build.IsWeapon && owned?.Weapons.GetValueOrDefault(id) is { } ownedWeapon)
        {
            return new Carried(ownedWeapon.Grade, ownedWeapon.Modifications, ownedWeapon.SeenAt);
        }

        return null;
    }

    private static string Key(KitEntry entry) =>
        entry.Build?.Id
        ?? (entry.ItemId is { } id
            ? Unplanned + id.ToString(CultureInfo.InvariantCulture)
            : entry.Equipment);

    /// <summary>
    /// The build a crumb key means, started if the item has none yet — the same arrangement the fleet
    /// has, and for the same reason: something the journal reports and nothing has planned for has no
    /// build id to key a crumb on.
    /// </summary>
    private OnFootBuild? Resolve(string key)
    {
        if (kit.Store.Find(key) is { } build)
        {
            return build;
        }

        if (!key.StartsWith(Unplanned, StringComparison.Ordinal)
            || !long.TryParse(key[Unplanned.Length..], CultureInfo.InvariantCulture, out var itemId))
        {
            return null;
        }

        var carried = kit.Kit().FirstOrDefault(entry => entry.ItemId == itemId);

        return carried is null ? null : kit.BuildFor(carried.Kind, itemId, carried.Equipment);
    }

    /// <summary>
    /// A suit and a hand weapon answer the same two questions, so they share a shape. <see
    /// cref="SeenAt"/> is null for what the Commander is in right now, and set for what the ledger
    /// last saw of something they are not.
    /// </summary>
    private readonly record struct Carried(
        int? Grade,
        IReadOnlyList<FittedModification> Modifications,
        DateTimeOffset? SeenAt = null);
}
