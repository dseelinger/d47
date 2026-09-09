using System.Globalization;
using D47.Core.Checklists;

namespace D47.Core.Ships;

/// <summary>What the Commander wants in one slot (Phase 26, "What is fitted and what you want").</summary>
/// <param name="Slot">
/// The slot as the journal names it — <c>MainEngines</c>, <c>Hardpoint1</c>.
/// </param>
/// <param name="Blueprint">
/// The blueprint by name, or null for "something here, I don't mind what".
/// </param>
/// <param name="Grade">1 to 5, or null for wildcard — never "unknown".</param>
/// <param name="Engineer">Who would roll it, where the Commander has an opinion.</param>
/// <param name="Experimental">
/// An experimental effect, which is its own item on the same slot.
/// </param>
/// <param name="Module">The module itself, where the plan is to fit one at all.</param>
/// <param name="Grade">
/// The grade wanted, and an <c>int</c> rather than an <c>int?</c> (remediation.md 15, item 4).
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
    /// </summary>
    public string? Variant { get; init; }

    /// <summary>Whether anything is actually wanted here, or the line is an empty shell.</summary>
    public bool IsEmpty =>
        Blueprint is null && Grade == 0 && Experimental is null && Module is null;

    /// <summary>What this plan asks <see cref="EngineeringPlan"/> for.</summary>
    /// <param name="withModule">
    /// Whether the module travels with the request (asked for 2026-08-24).
    /// </param>
    public BuildRequest ToRequest() =>
        new(Slot, Blueprint, Grade > 0 ? Grade : null, Engineer, Experimental, Module);

    /// <summary>One line, as the slot index shows it and as d47 says it.</summary>
    /// <param name="withGrade">
    /// Whether the grade belongs in the sentence (remediation.md 17, item 11).
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
            // **The grade is not printed where the blueprint has only one** (remediation.md 15, item 4):
            // "grade 1 Ammo Capacity" on a Point Defence says the same thing as "Ammo Capacity" and takes
            // longer.
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

/// <summary>One ship's build (Phase 26, "The fleet, and the fleet you intend").</summary>
/// <param name="CommanderFid">
/// Whose plan this is — the Frontier id, inside the document rather than in a path, which is <see
/// cref="Checklists.ChecklistDocument"/>'s rule for the same untrusted input.
/// </param>
/// <param name="Id">
/// This build's own identity, stable from the moment it is created and independent of <see
/// cref="ShipId"/>.
/// </param>
/// <param name="Hull">
/// The hull symbol as the journal writes it — <c>python</c>, <c>federation_corvette</c>.
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

    /// <summary>
    /// The Commander's name at the time of writing, for a person reading a file two Commanders now
    /// share.
    /// </summary>
    public string? CommanderName { get; init; }

    /// <summary>
    /// The disagreement between this build and the checklist that the Commander has already said no to
    /// (Phase 38).
    /// </summary>
    public string? Settled { get; init; }

    /// <summary>Whether the Commander owns this hull, or merely intends to.</summary>
    public bool IsOwned => ShipId is not null;

    /// <summary>
    /// Which checklist list this build's items belong in, or null while the hull is only intended —
    /// which is exactly why promotion of a prospective build has to say so rather than inventing a
    /// scope.
    /// </summary>
    public ChecklistScope? Scope => ShipId is { } id ? ChecklistScope.Ship(id) : null;

    /// <summary>The plan for one slot, or null where there is none.</summary>
    public SlotPlan? For(string slot) =>
        Slots.FirstOrDefault(plan => string.Equals(plan.Slot, slot, StringComparison.OrdinalIgnoreCase));

    /// <summary>The build with one slot planned, replacing whatever was there.</summary>
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
    /// <c>krait_mkii</c> reads as "Krait Mk II" wherever it is shown.
    /// </summary>
    public string HullName => Knowledge.EliteSpecifications.HullSaid(Hull);

    /// <summary>How a Commander hears it — "Bad Idea (Python)", "Corsair, intended".</summary>
    public string Describe()
    {
        var said = Name is { Length: > 0 } name ? $"{name} ({HullName})" : HullName;

        return IsOwned ? said : $"{said}, intended";
    }
}
