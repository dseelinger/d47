using System.Globalization;
using D47.Core.Checklists;
using D47.Core.Interface;
using D47.Core.Journal;
using D47.Core.Knowledge;
using D47.Core.Loadout;

namespace D47.App.Panel;

/// <summary>
/// The Loadout tab's Suits and weapons mode (list.md Phase 27, "The same page, on foot").
/// <para>
/// <b>The Ships page instantiated against <see cref="OnFootPlan"/></b> — the same index, the same
/// drill, the same promote path, the same say-lines along the bottom. Nothing about the layout is
/// redrawn, which is the item's whole claim: one page kind built once and shown twice.
/// </para>
/// <para>
/// <b>What differs is the shape of the thing being planned.</b> A suit takes a grade and
/// modifications rather than modules in slots, so a slot here is a <em>mod</em> slot and the grade
/// has a slot of its own above them. And the numbers on those slots are d47's own: Elite reports
/// an item's modifications as a set with no positions in it, so a mod slot orders the Commander's
/// plan and claims nothing about the item.
/// </para>
/// </summary>
public sealed class OnFootMode(OnFootPlanService kit, Func<CommanderGameState?> state) : ILoadoutMode
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

    public event Action? Changed
    {
        add => kit.Store.Changed += value;
        remove => kit.Store.Changed -= value;
    }

    public string EmptyIndex =>
        "I have not seen you on foot yet. Step out of the ship and I will read what you are "
        + "wearing — or plan a suit you do not own, and buying one will point the plan at it.";

    public string EmptySlots =>
        "Nothing is planned, and I cannot see this item — Elite only reports the loadout you are "
        + "in. Plan the grade and it will appear here.";

    public string NewLabel => "Plan a suit or weapon you do not own";

    public string PromoteLabel => "Put this plan on my checklist";

    public string SayAtIndex => "what have I planned on foot";

    public string SayAtItem => "put that on my checklist";

    public string SayAtSlot(string slot) =>
        string.Equals(slot, OnFootBuild.GradeSlot, StringComparison.OrdinalIgnoreCase)
            ? "take my Maverick to grade 5"
            : "plan night vision on my suit";

    public IReadOnlyList<LoadoutRow> Items() =>
    [
        .. kit.Kit().Select(entry =>
        {
            var planned = entry.Planned;

            return new LoadoutRow(
                Key(entry),
                entry.Equipment,
                entry.Equipment,
                planned > 0
                    ? $"{entry.Where()} · {planned.ToString(CultureInfo.InvariantCulture)} planned"
                    : entry.Where(),
                planned > 0);
        }),
    ];

    /// <summary>
    /// Something the Commander does not own. Voice first, exactly as a hull is: a suit is a name,
    /// and a name is far easier said than hunted for one key at a time.
    /// </summary>
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
                    : EntryVerdict.Ok),
            equipment =>
            {
                kit.Intend(equipment);
                done();
            });

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

        // The grade first, because it is the line the rest of the page depends on: a grade 1 item
        // has no modification slots at all, so a trip to an engineer before Pioneer Supplies is a
        // wasted trip.
        return grade is { } known
            ? $"{build.Equipment}, grade {known.ToString(CultureInfo.InvariantCulture)}. "
              + $"{OnFootRules.SlotsAt(known).ToString(CultureInfo.InvariantCulture)} modification "
              + "slots at that grade, and every one of them permanent."
            : $"{build.Equipment}. Modifications are permanent: four at most, and a wrong one is "
              + "recoverable only by buying and re-upgrading a fresh item.";
    }

    public IReadOnlyList<LoadoutRow> Slots(string item)
    {
        if (Resolve(item) is not { } build)
        {
            return [];
        }

        var rows = new List<LoadoutRow>();
        var current = Current(build);

        rows.Add(Row(build, OnFootBuild.GradeSlot, current));

        // The slots the grade being aimed at will have, and any the Commander planned above that
        // — a plan is never hidden because the item has not caught up with it yet.
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
            return
            [
                new LoadoutLine(
                    worn.Grade is { } grade
                        ? $"Grade {grade.ToString(CultureInfo.InvariantCulture)}"
                        : "No grade — the flight suit carries none and cannot be upgraded.",
                    LoadoutTone.Body),

                new LoadoutLine(worn.Grade is { } known
                    ? $"{OnFootRules.SlotsAt(known).ToString(CultureInfo.InvariantCulture)} "
                      + "modification slots at this grade."
                    : "Nothing can be fitted to it."),
            ];
        }

        // Every modification on the item, on every mod slot's page, and the reason said outright.
        // Elite writes them as a set with no positions, so drawing the third one against "Mod 3"
        // would be inventing an order the game does not have.
        var lines = new List<LoadoutLine>
        {
            new(
                worn.Modifications.Count == 0
                    ? "Nothing."
                    : string.Join(", ", worn.Modifications.Select(modification => modification.Speak())),
                LoadoutTone.Body),
        };

        if (worn.Modifications.Count > 0)
        {
            lines.Add(new LoadoutLine(
                "Elite reports these as a set with no slot numbers, so the numbers here order your "
                + "plan rather than the item."));
        }

        return lines;
    }

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
                verdict?.Reason
                ?? "Nothing can be said about this right now — Elite reports the loadout you are in "
                   + "and no other.",
                verdict is { } answered && ChecklistNextAction.IsWrong(answered.State)
                    ? LoadoutTone.Danger
                    : LoadoutTone.Muted));
        }

        lines.AddRange(Cost(items));

        return lines;
    }

    /// <summary>
    /// Asks for the grade, or for a modification. Keyboard for the number and voice for the name,
    /// which is the per-call-site declaration Phase 25 asks for.
    /// </summary>
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
                    : EntryVerdict.No($"I have no on-foot modification called “{value}”.")),
            modification =>
            {
                kit.Plan(build.Id, new KitPlan(slot, Modification: modification.Trim()));
                done();
            });
    }

    /// <summary>
    /// What this plan costs. <b>Exact rather than hedged</b>: nothing on foot is rolled, so a
    /// total is a sum rather than a product with a roll count in it.
    /// </summary>
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

    /// <summary>
    /// Whether a spoken word names a modification anything shipped knows. <b>Permissive on
    /// purpose</b>: the recipe table and the journal spell these differently, so the name table
    /// and the recipe list are both accepted and a miss beyond that is reported by the costing
    /// rather than refused at the prompt.
    /// </summary>
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
    /// The item as it stands right now, or null when it is not the one the Commander is in — which
    /// is a "nothing can be said" rather than a "not done".
    /// </summary>
    private Carried? Worn(OnFootBuild build)
    {
        if (state()?.OnFoot is not { IsKnown: true } loadout || build.ItemId is not { } id)
        {
            return null;
        }

        if (!build.IsWeapon)
        {
            return loadout.SuitId == id
                ? new Carried(loadout.Grade, loadout.SuitModifications)
                : null;
        }

        var weapon = loadout.Weapons.FirstOrDefault(carried => carried.ModuleId == id);

        return weapon is null ? null : new Carried(weapon.Grade, weapon.Modifications);
    }

    private static string Key(KitEntry entry) =>
        entry.Build?.Id
        ?? (entry.ItemId is { } id
            ? Unplanned + id.ToString(CultureInfo.InvariantCulture)
            : entry.Equipment);

    /// <summary>
    /// The build a crumb key means, started if the item has none yet — the same arrangement the
    /// fleet has, and for the same reason: something the journal reports and nothing has planned
    /// for has no build id to key a crumb on.
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

    /// <summary>A suit and a hand weapon answer the same two questions, so they share a shape.</summary>
    private readonly record struct Carried(int? Grade, IReadOnlyList<FittedModification> Modifications);
}
