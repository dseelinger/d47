using System.Globalization;
using D47.Core.Checklists;

namespace D47.Core.Ships;

/// <summary>
/// What the Commander wants in one slot (list.md Phase 26, "What is fitted and what you want").
/// <para>
/// The same shape as a <see cref="BuildRequest"/>, and deliberately: a slot plan <em>is</em> a
/// build request that has been written down, so promotion hands one straight to
/// <see cref="EngineeringPlan.Items"/> rather than translating between two vocabularies that
/// would then have to be kept in step.
/// </para>
/// </summary>
/// <param name="Slot">
/// The slot as the journal names it — <c>MainEngines</c>, <c>Hardpoint1</c>. <b>The identity</b>:
/// a slot exists as long as the hull does, so this needs no counter and no tombstone bookkeeping.
/// </param>
/// <param name="Blueprint">The blueprint by name, or null for "something here, I don't mind what".</param>
/// <param name="Grade">1 to 5, or null for wildcard — never "unknown".</param>
/// <param name="Engineer">Who would roll it, where the Commander has an opinion.</param>
/// <param name="Experimental">An experimental effect, which is its own item on the same slot.</param>
/// <param name="Module">The module itself, where the plan is to fit one at all.</param>
/// <param name="Grade">
/// The grade wanted, and <b>an <c>int</c> rather than an <c>int?</c></b> (remediation.md 15,
/// item 4). Null used to be a documented wildcard meaning "any grade"; the Commander's ruling is
/// that any grade is not a thing, and a nullable field is how the wildcard would come back through
/// a door nobody was watching. Meaningless where <paramref name="Blueprint"/> is null — a plan can
/// name a module and no engineering — and it holds 0 there and is never rendered.
/// </param>
public sealed record SlotPlan(
    string Slot,
    string? Blueprint = null,
    int Grade = 0,
    string? Engineer = null,
    string? Experimental = null,
    string? Module = null)
{
    /// <summary>
    /// The exact module, as the journal's symbol, where the Commander picked a size and a mount
    /// (remediation.md 13, item 8).
    /// <para>
    /// <b>Beside <see cref="Module"/> rather than instead of it.</b> "A pulse laser, I do not
    /// mind which" is a real plan and is the name alone; "a large gimballed one" is that plan made
    /// exact. The symbol is what carries the size, the rating and the mount without this record
    /// having to hold three more fields that could disagree with each other.
    /// </para>
    /// </summary>
    public string? Variant { get; init; }

    /// <summary>Whether anything is actually wanted here, or the line is an empty shell.</summary>
    public bool IsEmpty =>
        Blueprint is null && Grade == 0 && Experimental is null && Module is null;

    /// <summary>What this plan asks <see cref="EngineeringPlan"/> for.</summary>
    public BuildRequest ToRequest() =>
        new(Slot, Blueprint, Grade, Engineer, Experimental);

    /// <summary>One line, as the slot index shows it and as d47 says it.</summary>
    public string Describe()
    {
        var parts = new List<string>();

        if (Variant is { Length: > 0 } variant
            && Knowledge.EliteSpecifications.ModuleName(variant) is { Length: > 0 } exact)
        {
            parts.Add(exact);
        }
        else if (Module is { Length: > 0 } module)
        {
            parts.Add(module);
        }

        if (Blueprint is { Length: > 0 } blueprint)
        {
            // **The grade is not printed where the blueprint has only one** (remediation.md 15,
            // item 4): "grade 1 Ammo Capacity" on a Point Defence says the same thing as "Ammo
            // Capacity" and takes longer. Three of 160 module-and-blueprint pairs are in that
            // position, and the rule is keyed on *offers one grade* rather than on *the maximum is
            // 1*, which is the thing actually meant and stays right if Frontier ships a
            // single-grade-3 recipe.
            //
            // The grade stays in the data throughout — the checklist and the costing need it — and
            // is omitted only here, which is the one string that is both shown and spoken.
            parts.Add(Grade > 0 && !Knowledge.BlueprintCatalogue.HasOneGrade(blueprint, Module)
                ? $"grade {Grade.ToString(CultureInfo.InvariantCulture)} {blueprint}"
                : blueprint);
        }

        if (Experimental is { Length: > 0 } effect)
        {
            parts.Add(effect);
        }

        if (parts.Count == 0)
        {
            return "nothing planned";
        }

        var said = string.Join(", ", parts);

        return Engineer is { Length: > 0 } engineer ? $"{said}, with {engineer}" : said;
    }
}

/// <summary>
/// One ship's build (list.md Phase 26, "The fleet, and the fleet you intend").
/// <para>
/// <b>One build per ship.</b> Comparing a combat fit against an exploration fit for the same hull
/// is a planner feature this deliberately does not have — and slot identity is what makes that a
/// decision rather than an accident, because two builds would need two items in one slot and
/// there is no way to spell that.
/// </para>
/// <para>
/// <b>A hull the Commander does not own is not in the fleet</b>, it is <em>intended</em>:
/// <see cref="ShipId"/> is null, because <see cref="ChecklistScope.Ship(int)"/> takes the
/// journal's id and a Corsair nobody has bought has none. Acquiring the hull is then the plan's
/// first step rather than a precondition sitting outside it.
/// </para>
/// </summary>
/// <param name="Id">
/// This build's own identity, stable from the moment it is created and <b>independent of
/// <see cref="ShipId"/></b>. That independence is load-bearing: buying the hull binds a real
/// <c>ShipID</c> to a plan that already exists, and a plan identified by the id it does not have
/// yet is a plan there is nothing to rebind.
/// </param>
/// <param name="Hull">
/// The hull symbol as the journal writes it — <c>python</c>, <c>federation_corvette</c>. What a
/// <c>ShipyardBuy</c> is matched against when a prospective build is adopted.
/// </param>
/// <param name="ShipId">The journal's id once the Commander owns it, and null until then.</param>
/// <param name="Name">What the Commander calls it, where they have said.</param>
/// <param name="Slots">One entry per slot they have an opinion about, in the order stated.</param>
public sealed record ShipBuild(
    string Id,
    string Hull,
    int? ShipId = null,
    string? Name = null,
    IReadOnlyList<SlotPlan>? Slots = null)
{
    public IReadOnlyList<SlotPlan> Slots { get; init; } = Slots ?? [];

    /// <summary>Whether the Commander owns this hull, or merely intends to.</summary>
    public bool IsOwned => ShipId is not null;

    /// <summary>
    /// Which checklist list this build's items belong in, or null while the hull is only
    /// intended — which is exactly why promotion of a prospective build has to say so rather
    /// than inventing a scope.
    /// </summary>
    public ChecklistScope? Scope => ShipId is { } id ? ChecklistScope.Ship(id) : null;

    /// <summary>The plan for one slot, or null where there is none.</summary>
    public SlotPlan? For(string slot) =>
        Slots.FirstOrDefault(plan => string.Equals(plan.Slot, slot, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// The build with one slot planned, replacing whatever was there.
    /// <para>
    /// Replacing rather than adding, which is the slot key seen from the store's side: a slot
    /// holds one plan because a slot holds one module, and a Commander changing their mind about
    /// a hardpoint has not acquired a second hardpoint.
    /// </para>
    /// </summary>
    public ShipBuild With(SlotPlan plan)
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
    public ShipBuild Without(string slot) => this with
    {
        Slots = [.. Slots.Where(plan =>
            !string.Equals(plan.Slot, slot, StringComparison.OrdinalIgnoreCase))],
    };

    /// <summary>
    /// The hull as a person says it, through the shipped table — so a build stored against
    /// <c>krait_mkii</c> reads as "Krait Mk II" wherever it is shown. Falls back to the symbol,
    /// which is ugly and true, for a hull no table covers.
    /// </summary>
    public string HullName => Knowledge.EliteSpecifications.Ship(Hull)?.Name ?? Hull;

    /// <summary>How a Commander hears it — "Bad Idea (Python)", "Corsair, intended".</summary>
    public string Describe()
    {
        var said = Name is { Length: > 0 } name ? $"{name} ({HullName})" : HullName;

        return IsOwned ? said : $"{said}, intended";
    }
}
