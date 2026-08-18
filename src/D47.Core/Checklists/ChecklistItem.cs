namespace D47.Core.Checklists;

/// <summary>
/// The third axis, orthogonal to kind and to state (list.md Phase 17, "One surface, two kinds,
/// three groups").
/// <para>
/// Derived items belong to whatever produced them. Authored ones file anywhere, which is what
/// lets <i>"ask Jim about the Krait build"</i> sit beside the Krait's plan.
/// </para>
/// </summary>
public enum ChecklistGroup
{
    /// <summary>Belongs to the Commander rather than to anything they are flying or building.</summary>
    Universal,

    /// <summary>Belongs to one ship, and follows it. Keyed by the journal's <c>ShipID</c>.</summary>
    Ship,

    /// <summary>Belongs to one star system. Keyed by its name.</summary>
    System,

    /// <summary>
    /// Belongs to one suit, and follows it through every upgrade. Keyed by the journal's
    /// <c>SuitID</c>, which survives a grade change where the symbol does not.
    /// <para>
    /// Its own group rather than a ship's, because a Maverick is not attached to whatever hull
    /// happened to be under the Commander when they talked about it.
    /// </para>
    /// </summary>
    Suit,

    /// <summary>One hand weapon, keyed by the journal's <c>SuitModuleID</c>.</summary>
    Weapon,
}

/// <summary>
/// Which list an item is in: a group, plus the thing that group is about.
/// </summary>
/// <param name="Group">Universal, this ship, this system.</param>
/// <param name="Key">
/// The <c>ShipID</c> for a ship and the system name for a system; null for universal, which is
/// about nothing in particular. Compared case-insensitively, because a system name arrives from
/// the journal in one casing and from a Commander's mouth in another.
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
        _ => "universal",
    };
}

/// <summary>
/// How an item's "done" is decided — <b>a property of where the item came from, not something the
/// Commander picks</b> (list.md Phase 17).
/// <para>
/// Mixing the two in one bucket gives a list where some ticks are computed and some are opinions,
/// with nothing on screen saying which. That is the failure this repo already names about keyword
/// lists: a list whose entries mean different things depending on a flag is a list that gets read
/// wrong.
/// </para>
/// </summary>
public enum ChecklistItemKind
{
    /// <summary>A sentence nobody can compute — <i>buy limpets</i>. A person says when it is done.</summary>
    Authored,

    /// <summary>A structured intent. Done is a diff against live journal state, never a tick.</summary>
    Derived,
}

/// <summary>What wrote a derived item. Authored items are always <see cref="Commander"/>.</summary>
public enum ChecklistSource
{
    Commander,
    EngineeringPlan,
    ColonisationPlan,
    OnFootPlan,
}

/// <summary>
/// Where an item stands. Only <see cref="Done"/> is complete; everything else is a different
/// reason for not being.
/// <para>
/// <b>The states past Open exist because "not done" is several different next actions.</b> A
/// module sitting in storage in Deciat is not the same problem as one that has to be ground for,
/// and a grade the Commander's rank cannot reach is not a problem gathering fixes at all.
/// </para>
/// </summary>
public enum ChecklistState
{
    Open,

    Done,

    /// <summary>
    /// The Commander already owns this and it is somewhere else. A completely different next
    /// action from "go and grind it" — <c>get_stored_modules</c> already carries the transfer cost.
    /// </summary>
    Elsewhere,

    /// <summary>
    /// Nothing the Commander can do about it yet: grade <i>N</i> cannot be rolled below rank
    /// <i>N</i>, so the answer is an ordering problem rather than a shortfall
    /// (<see cref="Knowledge.EngineeringRules.RollsFor"/>).
    /// </summary>
    Blocked,

    /// <summary>
    /// The journal agrees with everything d47 can check and one thing it cannot. See
    /// <see cref="ChecklistNaming"/> — Frontier writes <c>Engine_Dirty</c> and the shipped table
    /// says "Dirty Drive Tuning", and nothing d47 ships joins them.
    /// </summary>
    Unverified,

    /// <summary>
    /// The list is about something that is no longer there — a <c>ShipID</c> now reporting a
    /// different hull. Said, rather than quietly diffing an exploration Krait against a Cutter.
    /// </summary>
    Stale,
}

/// <summary>
/// What each state means the Commander should do next (list.md Phase 25, "The checklist leaves
/// its window").
/// <para>
/// <b>A state is a next action rather than a badge</b>, which is what <see cref="ChecklistState"/>
/// asks for in its own summary: the states past Open exist because "not done" is several
/// different next actions, and a page that printed the enum name back would be spending a line
/// per item to say nothing the Commander can act on.
/// </para>
/// <para>
/// Here rather than beside the page, so the drawn line and the spoken one are the same sentence.
/// A state a Commander reads one way and hears another is two behaviours to keep in step.
/// </para>
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

    /// <summary>
    /// Whether this state is something being <em>wrong</em> rather than something being underway.
    /// <para>
    /// What decides where colour is spent, and it is spent nowhere else. A page where six things
    /// are coloured is a page where none of them is noticed, and Open is the ordinary condition
    /// of every item on a list that runs for weeks.
    /// </para>
    /// </summary>
    public static bool IsWrong(ChecklistState state) =>
        state is ChecklistState.Stale or ChecklistState.Unverified;
}

/// <summary>
/// Why an item is no longer live. <b>Completing is not removing and abandoning is not deleting</b>
/// (list.md Phase 17).
/// </summary>
public enum ChecklistTombstone
{
    None,

    /// <summary>A revision dropped it while it was still open.</summary>
    Abandoned,

    /// <summary>
    /// It was <em>done</em> and then designed out. Kept as its own answer rather than folded into
    /// <see cref="Abandoned"/>, because the Commander really did spend that fortnight and a
    /// history that forgets it is a history that lies.
    /// </summary>
    Superseded,
}

/// <summary>
/// Where the wording came from, which survives into how d47 says it (list.md Phase 17, "LLM Ship
/// AI may propose that a checklist item is done").
/// <para>
/// Provenance in the sentence rather than in a flag nobody reads. The three are not
/// interchangeable: one of them is checkable, one of them is a conversation, and one of them is
/// somebody's note.
/// </para>
/// </summary>
public enum ChecklistProvenance
{
    /// <summary>A shipped table confirmed it. Said in d47's own voice.</summary>
    Asserted,

    /// <summary>A conversation settled on it. Said back as the conversation had it.</summary>
    Quoted,

    /// <summary>The Commander's own note. Said as theirs.</summary>
    Attributed,
}

/// <summary>What a derived item is <em>about</em>, as structure rather than as a sentence.</summary>
public enum ChecklistIntentKind
{
    /// <summary>A blueprint at a grade, in a slot.</summary>
    Blueprint,

    /// <summary>An experimental effect, in a slot. The one name Elite does localise.</summary>
    Experimental,

    /// <summary>A module fitted in a slot at all, whatever is done to it afterwards.</summary>
    Module,

    /// <summary>Access to an engineer — the unlock, or the rank a grade needs.</summary>
    EngineerAccess,

    /// <summary>A facility at a place in a system.</summary>
    Facility,

    /// <summary>A commodity a construction site is asking for.</summary>
    Commodity,

    /// <summary>
    /// A grade on a suit or a hand weapon, bought at Pioneer Supplies.
    /// <para>
    /// Its own kind rather than a <see cref="Blueprint"/> with a grade, because nothing about it
    /// behaves the same way: no roll count, no engineer, no rank gate, and it is the thing every
    /// <see cref="Modification"/> on the same item is blocked behind.
    /// </para>
    /// </summary>
    Grade,

    /// <summary>
    /// An on-foot modification on a suit or a hand weapon. Ungraded, and <b>permanent</b>.
    /// </summary>
    Modification,
}

/// <summary>
/// A structured intent, which is half of an item's identity.
/// <para>
/// <b>Intents, not a target state.</b> A Krait has around twenty slots and a conversation about a
/// build produces opinions about six, so a target loadout means inventing the other fourteen and
/// then reporting "6 of 20" where the honest number is "6 of 9 things you asked for".
/// </para>
/// </summary>
/// <param name="Kind">Which of the six shapes this is.</param>
/// <param name="Subject">
/// The slot for a ship, and the place for a system — a body name or an orbital slot. Part of the
/// key, so it is what makes two intents about the same thing the same item.
/// </param>
public sealed record ChecklistIntent(ChecklistIntentKind Kind, string Subject)
{
    /// <summary>
    /// The blueprint, effect, module, facility or commodity, in whatever spelling the intent was
    /// stated in. Part of the key: swapping burst lasers for multi-cannons in one slot is a
    /// tombstone and a new item, not the same item changing its mind.
    /// </summary>
    public string? Detail { get; init; }

    /// <summary>
    /// 1 to 5 for a blueprint. <b>Null is wildcard, not unknown</b> — "grade 5 dirty drives and I
    /// don't care which thrusters" has to be expressible, or it is permanently unmeetable.
    /// </summary>
    public int? Grade { get; init; }

    /// <summary>
    /// How many, for a commodity. Never invented: it comes off the construction depot or out of
    /// the Commander's mouth, because there is no licence-clean facility cost table to predict it
    /// from (<c>docs/spikes/colonisation-sources.md</c>).
    /// </summary>
    public int? Quantity { get; init; }

    /// <summary>Who would roll it, where an intent names one.</summary>
    public string? Engineer { get; init; }
}

/// <summary>
/// One item's identity: the list it is in, and what it is within that list.
/// <para>
/// <b>This is the decision the whole phase rests on.</b> Two plans can only be diffed if an item
/// knows what it is independently of its position in a list. Get this wrong and every revision
/// reads as <i>everything removed, everything added</i> — which would wipe a fortnight of progress
/// the first time somebody changed one weapon, and quietly kill the reason completed items are
/// kept at all.
/// </para>
/// </summary>
public readonly record struct ChecklistItemId(ChecklistScope Scope, string Key)
{
    public bool Same(ChecklistItemId other) =>
        Scope.Same(other.Scope) && string.Equals(Key, other.Key, StringComparison.OrdinalIgnoreCase);

    public override string ToString() => $"{Scope}/{Key}";
}

/// <summary>
/// One line of the Commander's list, authored or derived (list.md Phase 17).
/// <para>
/// <b>No number is ever stored here.</b> Every figure a plan quotes is recomputed from a table
/// with a generator behind it, because a stored number gets recited in October with nothing left
/// to say where it came from. <see cref="State"/> is the one exception and it is not a number: it
/// is the last computed verdict, kept so that a verdict going backwards can be announced exactly
/// once instead of on every tick.
/// </para>
/// </summary>
public sealed record ChecklistItem
{
    /// <summary>Unique within <see cref="Scope"/>. Minted by <see cref="ChecklistKeys"/>.</summary>
    public required string Key { get; init; }

    public required ChecklistScope Scope { get; init; }

    public required ChecklistItemKind Kind { get; init; }

    /// <summary>What it says, in words. The whole of an authored item; a label on a derived one.</summary>
    public required string Text { get; init; }

    public ChecklistSource Source { get; init; } = ChecklistSource.Commander;

    /// <summary>Null on an authored item, and that is what makes it authored.</summary>
    public ChecklistIntent? Intent { get; init; }

    public ChecklistState State { get; init; } = ChecklistState.Open;

    public ChecklistTombstone Tombstone { get; init; } = ChecklistTombstone.None;

    public ChecklistProvenance Provenance { get; init; } = ChecklistProvenance.Attributed;

    /// <summary>
    /// The hull the ship reported when a ship-scoped item was written. Carried so that a
    /// <c>ShipID</c> now reporting a different hull makes the list <see cref="ChecklistState.Stale"/>
    /// and says so, rather than silently diffing an exploration Krait against a Cutter.
    /// </summary>
    public string? Hull { get; init; }

    /// <summary>
    /// Whether d47 has already said the one thing it can only say once about this item — that
    /// nothing it ships can confirm the name. A <em>contradicted</em> item is said every time; an
    /// <em>unknown</em> one is said once, because nagging about every line d47 has no table for is
    /// how a Commander learns to stop listening.
    /// </summary>
    public bool Noted { get; init; }

    public ChecklistItemId Id => new(Scope, Key);

    public bool IsComplete => State == ChecklistState.Done;

    public bool IsLive => Tombstone == ChecklistTombstone.None;

    /// <summary>
    /// Whether a person is allowed to tick this. False for every derived item, and trying says
    /// why rather than quietly doing nothing — the next journal read would either undo it or,
    /// worse, leave it standing and lying.
    /// </summary>
    public bool TicksByHand => Kind == ChecklistItemKind.Authored;
}

/// <summary>
/// Why a line of the file was refused, in words that name the line and the problem.
/// <para>
/// The same contract <see cref="Actions.MacroStore"/> has, for the same reason: a checklist that
/// quietly loses a line is worse than one that refuses it out loud. One bad item must not cost
/// somebody their other forty.
/// </para>
/// </summary>
public sealed record ChecklistProblem(string Where, string Reason);
