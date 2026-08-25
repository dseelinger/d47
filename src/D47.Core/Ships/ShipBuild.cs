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

    /// <summary>
    /// What this plan asks <see cref="EngineeringPlan"/> for.
    /// <para>
    /// <b>Grade 0 becomes null across this boundary</b>, because the two types mean different
    /// things by the number: here 0 is "no engineering stated" and is never rendered, and
    /// <see cref="BuildRequest.Grade"/> is a nullable where a number is a grade the Commander
    /// asked for. Passed straight through, a module-only plan reached the checklist as *"Grade 0
    /// engineering on LargeHardpoint1"* and was refused by validation — reported 2026-08-20, four
    /// lines of it in red.
    /// </para>
    /// </summary>
    /// <param name="withModule">
    /// Whether the module travels with the request (asked for 2026-08-24). It does, and it is
    /// named here rather than passed silently because <c>ShipPlanService</c> carries a comment
    /// saying <c>BuildRequest</c> carries no module at all — true when it was written, and true of
    /// what that comment is about: <b>this is the module's name for the sentence, not a request to
    /// fit one.</b> No module checklist item comes of it.
    /// </param>
    public BuildRequest ToRequest() =>
        new(Slot, Blueprint, Grade > 0 ? Grade : null, Engineer, Experimental, Module);

    /// <summary>One line, as the slot index shows it and as d47 says it.</summary>
    /// <param name="withGrade">
    /// Whether the grade belongs in the sentence (remediation.md 17, item 11).
    /// <para>
    /// <b>Only a drawn line ever says no</b>, and only where a stepper is about to carry the grade
    /// instead. Reported as *"it is not clear that the step controls for the engineering grade are
    /// associated with it — put Grade last on the line, followed by the stepper"*, and the number
    /// sat mid-sentence with a blueprint, an effect and an engineer between it and its own buttons.
    /// </para>
    /// <para>
    /// <b>The spoken line is untouched, which is the reason this is a parameter rather than a
    /// reordering.</b> This one string is both shown and heard, and a sentence rearranged for the
    /// panel would come out of the speakers as "Heavy Duty, Deep Plating, with Selene Jean, grade
    /// 5" — changing what d47 sounds like as a side effect of moving a button, which is the
    /// failure remediation 16 item 6 caught. The default is the sentence.
    /// </para>
    /// </param>
    public string Describe(bool withGrade = true)
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
            parts.Add(withGrade
                      && Grade > 0
                      && !Knowledge.BlueprintCatalogue.HasOneGrade(blueprint, Module)
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
/// <param name="CommanderFid">
/// Whose plan this is — the Frontier id, <b>inside the document rather than in a path</b>, which is
/// <see cref="Checklists.ChecklistDocument"/>'s rule for the same untrusted input. It is half of
/// what makes <see cref="ShipId"/> mean anything: Elite's ship ids are per Commander and start
/// small, so without it one Commander's ship 7 answered for another's (the list.md Phase 44 defect
/// item). Empty for a build written before this file carried a Commander, or planned before any
/// Commander was known; the first Commander seen adopts those.
/// </param>
/// <param name="Id">
/// This build's own identity, stable from the moment it is created and <b>independent of
/// <see cref="ShipId"/></b>. That independence is load-bearing: buying the hull binds a real
/// <c>ShipID</c> to a plan that already exists, and a plan identified by the id it does not have
/// yet is a plan there is nothing to rebind. Unique across the whole file, not per Commander, so
/// nothing that holds one needs to say whose it was.
/// </param>
/// <param name="Hull">
/// The hull symbol as the journal writes it — <c>python</c>, <c>federation_corvette</c>. What a
/// <c>ShipyardBuy</c> is matched against when a prospective build is adopted.
/// </param>
/// <param name="ShipId">The journal's id once the Commander owns it, and null until then.</param>
/// <param name="Name">What the Commander calls it, where they have said.</param>
/// <param name="Slots">One entry per slot they have an opinion about, in the order stated.</param>
public sealed record ShipBuild(
    string CommanderFid,
    string Id,
    string Hull,
    int? ShipId = null,
    string? Name = null,
    IReadOnlyList<SlotPlan>? Slots = null)
{
    public IReadOnlyList<SlotPlan> Slots { get; init; } = Slots ?? [];

    /// <summary>The Commander's name at the time of writing, for a person reading a file two
    /// Commanders now share. Never the key.</summary>
    public string? CommanderName { get; init; }

    /// <summary>
    /// The disagreement between this build and the checklist that the Commander has already said
    /// no to (list.md Phase 38). Null for a build nobody has been asked about.
    /// <para>
    /// <b>On the build rather than in memory</b>, because "does not ask again for that same
    /// difference" has to survive d47 being restarted — a question re-asked every morning is a
    /// question that gets answered by switching the feature off. It holds a fingerprint of the two
    /// item sets rather than a flag, so a difference that <em>changes</em> is a new question and is
    /// asked, which is the behaviour actually wanted.
    /// </para>
    /// <para>
    /// See <see cref="ShipDriftWatch"/>, which is the only thing that writes it.
    /// </para>
    /// </summary>
    public string? Settled { get; init; }

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
    public string HullName => Knowledge.EliteSpecifications.HullSaid(Hull);

    /// <summary>How a Commander hears it — "Bad Idea (Python)", "Corsair, intended".</summary>
    public string Describe()
    {
        var said = Name is { Length: > 0 } name ? $"{name} ({HullName})" : HullName;

        return IsOwned ? said : $"{said}, intended";
    }
}
