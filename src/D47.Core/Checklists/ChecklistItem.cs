namespace D47.Core.Checklists;

/// <summary>
/// The third axis, orthogonal to kind and to state (Phase 17, "One surface, two kinds, three groups").
/// </summary>
public enum ChecklistGroup
{
    /// <summary>Belongs to the Commander rather than to anything they are flying or building.</summary>
    Universal,

    /// <summary>Belongs to one ship, and follows it.</summary>
    Ship,

    /// <summary>Belongs to one star system.</summary>
    System,

    /// <summary>Belongs to one suit, and follows it through every upgrade.</summary>
    Suit,

    /// <summary>One hand weapon, keyed by the journal's <c>SuitModuleID</c>.</summary>
    Weapon,
}

/// <summary>Which list an item is in: a group, plus the thing that group is about.</summary>
/// <param name="Group">Universal, this ship, this system.</param>
/// <param name="Key">
/// The <c>ShipID</c> for a ship and the system name for a system; null for universal, which is about
/// nothing in particular.
/// </param>
public sealed record ChecklistScope(ChecklistGroup Group, string? Key = null)
{
    public static readonly ChecklistScope Universal = new(ChecklistGroup.Universal);

    public static ChecklistScope Ship(int shipId) =>
        new(ChecklistGroup.Ship, shipId.ToString(global::System.Globalization.CultureInfo.InvariantCulture));

    public static ChecklistScope System(string name) => new(ChecklistGroup.System, name.Trim());

    public static ChecklistScope Suit(long suitId) =>
        new(ChecklistGroup.Suit, suitId.ToString(global::System.Globalization.CultureInfo.InvariantCulture));

    public static ChecklistScope Weapon(long moduleId) =>
        new(ChecklistGroup.Weapon, moduleId.ToString(global::System.Globalization.CultureInfo.InvariantCulture));

    public bool Same(ChecklistScope other) =>
        Group == other.Group
        && string.Equals(Key ?? string.Empty, other.Key ?? string.Empty, StringComparison.OrdinalIgnoreCase);

    /// <summary>How a Commander hears it — "your Krait's list", "the Sol list", "your list".</summary>
    public override string ToString() => Group switch
    {
        ChecklistGroup.Ship => $"ship {Key}",
        ChecklistGroup.System => Key ?? "a system",
        ChecklistGroup.Suit => $"suit {Key}",
        ChecklistGroup.Weapon => $"weapon {Key}",
        _ => Word(Group),
    };

    /// <summary>
    /// What a Commander calls a group — the word on the filter, in the tool schema and in anything d47
    /// says out loud.
    /// </summary>
    public static string Word(ChecklistGroup group) => group switch
    {
        ChecklistGroup.Ship => "ship",
        ChecklistGroup.System => "system",
        ChecklistGroup.Suit => "suit",
        ChecklistGroup.Weapon => "weapon",
        _ => "custom",
    };
}

/// <summary>
/// How an item's "done" is decided — a property of where the item came from, not something the
/// Commander picks (Phase 17).
/// </summary>
public enum ChecklistItemKind
{
    /// <summary>A sentence nobody can compute — buy limpets.</summary>
    Authored,

    /// <summary>A structured intent.</summary>
    Derived,
}

/// <summary>What wrote a derived item.</summary>
public enum ChecklistSource
{
    Commander,
    EngineeringPlan,
    ColonisationPlan,
    OnFootPlan,
}

/// <summary>Where an item stands.</summary>
public enum ChecklistState
{
    Open,

    Done,

    /// <summary>The Commander already owns this and it is somewhere else.</summary>
    Elsewhere,

    /// <summary>
    /// Nothing the Commander can do about it yet: grade N cannot be rolled below rank N, so the answer
    /// is an ordering problem rather than a shortfall (<see
    /// cref="Knowledge.EngineeringRules.RollsFor"/>).
    /// </summary>
    Blocked,

    /// <summary>The journal agrees with everything d47 can check and one thing it cannot.</summary>
    Unverified,

    /// <summary>
    /// The list is about something that is no longer there — a <c>ShipID</c> now reporting a different
    /// hull.
    /// </summary>
    Stale,
}

/// <summary>
/// What each state means the Commander should do next (Phase 25, "The checklist leaves its window").
/// </summary>
public static class ChecklistNextAction
{
    /// <summary>What to do about an item in this state, or null when there is nothing to say.</summary>
    public static string? For(ChecklistState state) => state switch
    {
        ChecklistState.Elsewhere => "You own this. Transfer it rather than grinding for another.",
        ChecklistState.Blocked => "Nothing to do here until your rank with the engineer reaches the grade.",
        ChecklistState.Unverified =>
            "Your journal has something in this slot and I cannot confirm it is this one, so I will not claim it.",
        ChecklistState.Stale => "This is about a hull that is not there any more.",
        _ => null,
    };

    /// <summary>Whether this state is something being wrong rather than something being underway.</summary>
    public static bool IsWrong(ChecklistState state) =>
        state is ChecklistState.Stale or ChecklistState.Unverified;
}

/// <summary>Why an item is no longer live.</summary>
public enum ChecklistTombstone
{
    None,

    /// <summary>A revision dropped it while it was still open.</summary>
    Abandoned,

    /// <summary>It was done and then designed out.</summary>
    Superseded,
}

/// <summary>
/// Where the wording came from, which survives into how d47 says it (Phase 17, "LLM Ship AI may propose
/// that a checklist item is done").
/// </summary>
public enum ChecklistProvenance
{
    /// <summary>A shipped table confirmed it.</summary>
    Asserted,

    /// <summary>A conversation settled on it.</summary>
    Quoted,

    /// <summary>The Commander's own note.</summary>
    Attributed,
}

/// <summary>What a derived item is about, as structure rather than as a sentence.</summary>
public enum ChecklistIntentKind
{
    /// <summary>A blueprint at a grade, in a slot.</summary>
    Blueprint,

    /// <summary>An experimental effect, in a slot.</summary>
    Experimental,

    /// <summary>A module fitted in a slot at all, whatever is done to it afterwards.</summary>
    Module,

    /// <summary>Access to an engineer — the unlock, or the rank a grade needs.</summary>
    EngineerAccess,

    /// <summary>A facility at a place in a system.</summary>
    Facility,

    /// <summary>A commodity a construction site is asking for.</summary>
    Commodity,

    /// <summary>A grade on a suit or a hand weapon, bought at Pioneer Supplies.</summary>
    Grade,

    /// <summary>An on-foot modification on a suit or a hand weapon.</summary>
    Modification,
}

/// <summary>A structured intent, which is half of an item's identity.</summary>
/// <param name="Kind">Which of the six shapes this is.</param>
/// <param name="Subject">
/// The slot for a ship, and the place for a system — a body name or an orbital slot.
/// </param>
public sealed record ChecklistIntent(ChecklistIntentKind Kind, string Subject)
{
    /// <summary>
    /// The blueprint, effect, module, facility or commodity, in whatever spelling the intent was stated
    /// in.
    /// </summary>
    public string? Detail { get; init; }

    /// <summary>1 to 5 for a blueprint.</summary>
    public int? Grade { get; init; }

    /// <summary>How many, for a commodity.</summary>
    public int? Quantity { get; init; }

    /// <summary>Who would roll it, where an intent names one.</summary>
    public string? Engineer { get; init; }

    /// <summary>
    /// The module the plan means to put in this slot, where it says — Shield Booster, Hull
    /// Reinforcement Package (asked for 2026-08-24).
    /// </summary>
    public string? Module { get; init; }
}

/// <summary>One item's identity: the list it is in, and what it is within that list.</summary>
public readonly record struct ChecklistItemId(ChecklistScope Scope, string Key)
{
    public bool Same(ChecklistItemId other) =>
        Scope.Same(other.Scope) && string.Equals(Key, other.Key, StringComparison.OrdinalIgnoreCase);

    public override string ToString() => $"{Scope}/{Key}";
}

/// <summary>One line of the Commander's list, authored or derived (Phase 17).</summary>
public sealed record ChecklistItem
{
    /// <summary>Unique within <see cref="Scope"/>.</summary>
    public required string Key { get; init; }

    public required ChecklistScope Scope { get; init; }

    public required ChecklistItemKind Kind { get; init; }

    /// <summary>What it says, in words.</summary>
    public required string Text { get; init; }

    public ChecklistSource Source { get; init; } = ChecklistSource.Commander;

    /// <summary>Null on an authored item, and that is what makes it authored.</summary>
    public ChecklistIntent? Intent { get; init; }

    public ChecklistState State { get; init; } = ChecklistState.Open;

    public ChecklistTombstone Tombstone { get; init; } = ChecklistTombstone.None;

    public ChecklistProvenance Provenance { get; init; } = ChecklistProvenance.Attributed;

    /// <summary>The hull the ship reported when a ship-scoped item was written.</summary>
    public string? Hull { get; init; }

    /// <summary>
    /// Whether d47 has already said the one thing it can only say once about this item — that nothing
    /// it ships can confirm the name.
    /// </summary>
    public bool Noted { get; init; }

    /// <summary>
    /// The arc this line came from, where one proposed it (Phase 34, "The checklist points at the
    /// arc").
    /// </summary>
    public string? Goal { get; init; }

    public ChecklistItemId Id => new(Scope, Key);

    public bool IsComplete => State == ChecklistState.Done;

    public bool IsLive => Tombstone == ChecklistTombstone.None;

    /// <summary>Whether a person is allowed to tick this.</summary>
    public bool TicksByHand => Kind == ChecklistItemKind.Authored;
}

/// <summary>Why a line of the file was refused, in words that name the line and the problem.</summary>
public sealed record ChecklistProblem(string Where, string Reason);
