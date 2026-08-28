using System.Globalization;
using D47.Core.Checklists;
using D47.Core.Knowledge;

namespace D47.Core.Loadout;

/// <summary>
/// What the Commander wants in one slot of a suit or a hand weapon (Phase 27, "The same
/// page, on foot").
/// <para>
/// <b>The slot is a mod slot, and it is read as the mod slot it is.</b> A hull's slot is a place a
/// module goes and its name is the journal's; an item on foot has a grade and up to four
/// modification slots, and nothing in the journal names them at all — so the names here are d47's
/// own, fixed, and the same for every suit and every weapon. That is the one thing this page does
/// not inherit from the ship page it is otherwise a copy of.
/// </para>
/// </summary>
/// <param name="Slot">
/// <see cref="OnFootBuild.GradeSlot"/>, or <c>Mod 1</c> through <c>Mod 4</c>. <b>The identity</b>,
/// exactly as a hardpoint is on the ship side: a slot exists as long as the item does, so this
/// needs no counter and no tombstone bookkeeping.
/// </param>
/// <param name="Grade">
/// The grade to reach, on the grade slot. <b>Not a wildcard the way a ship blueprint's is</b>: on
/// foot the grade is bought outright, so an unstated one means no upgrade rather than any upgrade.
/// </param>
/// <param name="Modification">What to have an engineer fit, on a mod slot. Permanent, once fitted.</param>
public sealed record KitPlan(string Slot, int? Grade = null, string? Modification = null)
{
    /// <summary>Whether anything is actually wanted here, or the line is an empty shell.</summary>
    public bool IsEmpty => Grade is null && string.IsNullOrWhiteSpace(Modification);

    /// <summary>One line, as the slot index shows it and as d47 says it.</summary>
    public string Describe()
    {
        if (Modification is { Length: > 0 } modification)
        {
            return modification;
        }

        return Grade is { } grade
            ? $"grade {grade.ToString(CultureInfo.InvariantCulture)}, at Pioneer Supplies"
            : "nothing planned";
    }
}

/// <summary>
/// One suit's or one weapon's build (Phase 27, "The same page, on foot").
/// <para>
/// The parallel of <see cref="Ships.ShipBuild"/> down to the vocabulary, because the page above it
/// is the same page: one build per item, a slot holds one plan, and an item the Commander does not
/// own yet is <em>intended</em> rather than absent — buying it is the plan's first step.
/// </para>
/// <para>
/// <b>What differs is the shape of the thing being planned.</b> A suit takes a grade and
/// modifications rather than modules in slots, and the grade is not one plan among several: it
/// gates the others, because a grade 1 item has no modification slots at all. So the grade has a
/// slot of its own at the top rather than being a property of the build, which is what lets the
/// index, the drill and the promotion path stay the ship page's.
/// </para>
/// </summary>
/// <param name="Id">
/// This build's own identity, stable from creation and <b>independent of <see cref="ItemId"/></b>
/// — the same load-bearing independence a ship build has, and for the same reason: buying the suit
/// binds a real id to a plan that already exists.
/// </param>
/// <param name="Equipment">
/// The suit or weapon as a Commander names it — <c>Maverick Suit</c>, <c>Karma AR-50</c>.
/// <b>The name rather than the symbol</b>, because a suit's symbol carries its grade and the whole
/// point of a plan is to change that: <c>utilitysuit_class1</c> stops being true the moment the
/// plan succeeds, and the id is what survives.
/// </param>
/// <param name="Kind">A suit or a weapon, which decides which checklist group it scopes to.</param>
/// <param name="ItemId">
/// The journal's <c>SuitID</c> or <c>SuitModuleID</c> once the Commander owns it, and null until
/// then. <b>It survives a grade change where the symbol does not</b>, which is why the checklist
/// is keyed on it.
/// </param>
/// <param name="Slots">The grade and the mod slots this build has an opinion about.</param>
public sealed record OnFootBuild(
    string Id,
    string Equipment,
    OnFootKind Kind,
    long? ItemId = null,
    IReadOnlyList<KitPlan>? Slots = null)
{
    /// <summary>The slot the grade is planned on. One per build, and always first.</summary>
    public const string GradeSlot = "Grade";

    public IReadOnlyList<KitPlan> Slots { get; init; } = Slots ?? [];

    /// <summary>Whether the Commander owns this item, or merely intends to.</summary>
    public bool IsOwned => ItemId is not null;

    public bool IsWeapon => Kind == OnFootKind.Weapon;

    /// <summary>The name of the <paramref name="index"/>th modification slot, counting from one.</summary>
    public static string ModSlot(int index) =>
        $"Mod {index.ToString(CultureInfo.InvariantCulture)}";

    /// <summary>
    /// Whether a string names a slot this build can hold. <b>Checked where the file is read</b>,
    /// because a hand edit is the one route that can invent <c>Hardpoint1</c> on a suit — and a
    /// slot nothing recognises would be planned, stored, shown and never promotable.
    /// </summary>
    public static bool IsSlot(string slot) =>
        string.Equals(slot, GradeSlot, StringComparison.OrdinalIgnoreCase)
        || Enumerable.Range(1, OnFootRules.MaxSlots)
            .Any(index => string.Equals(slot, ModSlot(index), StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// Which checklist list this build's items belong in, or null while the item is only intended
    /// — which is exactly why promotion of a prospective build has to say so rather than inventing
    /// a scope.
    /// </summary>
    public ChecklistScope? Scope => ItemId is { } id
        ? IsWeapon ? ChecklistScope.Weapon(id) : ChecklistScope.Suit(id)
        : null;

    /// <summary>The plan for one slot, or null where there is none.</summary>
    public KitPlan? For(string slot) =>
        Slots.FirstOrDefault(plan => string.Equals(plan.Slot, slot, StringComparison.OrdinalIgnoreCase));

    /// <summary>The grade this build is aiming at, where it says.</summary>
    public int? PlannedGrade => For(GradeSlot)?.Grade;

    /// <summary>Every modification planned, in slot order.</summary>
    public IReadOnlyList<string> Modifications =>
        [.. Slots
            .Where(plan => !string.Equals(plan.Slot, GradeSlot, StringComparison.OrdinalIgnoreCase))
            .Where(plan => plan.Modification is { Length: > 0 })
            .Select(plan => plan.Modification!)];

    /// <summary>The build with one slot planned, replacing whatever was there.</summary>
    public OnFootBuild With(KitPlan plan)
    {
        var slots = Slots.ToList();
        var at = slots.FindIndex(existing =>
            string.Equals(existing.Slot, plan.Slot, StringComparison.OrdinalIgnoreCase));

        if (at >= 0)
        {
            slots[at] = plan;
        }
        else
        {
            slots.Add(plan);
        }

        return this with { Slots = slots };
    }

    /// <summary>The build with one slot's plan taken out.</summary>
    public OnFootBuild Without(string slot) => this with
    {
        Slots = [.. Slots.Where(plan =>
            !string.Equals(plan.Slot, slot, StringComparison.OrdinalIgnoreCase))],
    };

    /// <summary>How a Commander hears it — "Maverick Suit", "Karma AR-50, intended".</summary>
    public string Describe() => IsOwned ? Equipment : $"{Equipment}, intended";
}
