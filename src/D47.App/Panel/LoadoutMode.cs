using D47.Core.Loadout;

namespace D47.App.Panel;

/// <summary>
/// Where the gap page gets its arithmetic, and how it knows to redo it (Phase 27, "Gap analysis").
/// </summary>
public sealed class GapSource(Func<bool, GapReport> report)
{
    /// <summary>Raised when either store changed.</summary>
    public event Action? Changed;

    public GapReport Of(bool includeIntended) => report(includeIntended);

    public void Invalidate() => Changed?.Invoke();
}

/// <summary>How a line of a loadout page is drawn.</summary>
public enum LoadoutTone
{
    /// <summary>A fact, at body size.</summary>
    Body,

    /// <summary>A qualification, an aside, a cost.</summary>
    Muted,

    /// <summary>Something that is wrong, or a gate nothing will get past.</summary>
    Danger,

    /// <summary>The head of a block — "Fitted", "Planned", "What it costs".</summary>
    Heading,

    /// <summary>What a module has been engineered with (remediation.md 15, item 10).</summary>
    Engineered,
}

/// <summary>One line of a loadout page, as content rather than as a control.</summary>
public sealed record LoadoutLine(string Text, LoadoutTone Tone = LoadoutTone.Muted)
{
    /// <summary>The grade this line's plan is at, where the line carries one that can be stepped.</summary>
    public LoadoutStep? Step { get; init; }

    /// <summary>
    /// One value on this line the Commander can take away with them, where the line carries one.
    /// </summary>
    public LoadoutCopy? Copy { get; init; }
}

/// <summary>A number on a line that the Commander can move, and what happens when they do.</summary>
/// <param name="Value">Where it is now.</param>
/// <param name="Offered">What it may be, highest first — the stepper clamps to these.</param>
/// <param name="Set">Applies a new value.</param>
public sealed record LoadoutStep(int Value, IReadOnlyList<int> Offered, Action<int> Set);

/// <summary>Something on a line worth putting on the clipboard, and what to say it is.</summary>
/// <param name="Value">Exactly what goes on the clipboard.</param>
/// <param name="Tip">What the pointer says the glyph will do.</param>
public sealed record LoadoutCopy(string Value, string Tip);

/// <summary>A second figure on a gauge's bar, drawn as a mark rather than as fill (Phase 38).</summary>
/// <param name="At">Where it sits, 0 to 1 of the bar's width.</param>
/// <param name="Label">What it is, for the reading under the bar.</param>
public readonly record struct LoadoutMark(double At, string Label);

/// <summary>
/// One gauge at the head of a ship's slot list — power, or jump range (Phase 38, "A build you can
/// watch").
/// </summary>
/// <param name="Name">"Power", "Jump range".</param>
/// <param name="Reading">The figures, in the shortest form that stays true.</param>
/// <param name="Fill">How much of the bar is filled, 0 to 1.</param>
/// <param name="Tone">
/// <see cref="LoadoutTone.Danger"/> for a build that does not fit, and <see cref="LoadoutTone.Body"/>
/// for one that does.
/// </param>
public sealed record LoadoutGauge(string Name, string Reading, double Fill, LoadoutTone Tone)
{
    /// <summary>The other figures on the same bar.</summary>
    public IReadOnlyList<LoadoutMark> Marks { get; init; } = [];

    /// <summary>
    /// Figures written under the point on the bar they belong to, rather than joined into a sentence
    /// beneath it (the Commander's instruction, 2026-09-01).
    /// </summary>
    public IReadOnlyList<LoadoutMark> Scale { get; init; } = [];

    /// <summary>What the figures mean, or what is wrong with them.</summary>
    public string? Note { get; init; }

    /// <summary>Whether this figure was worked out rather than read off the game (Phase 38).</summary>
    public bool Modelled { get; init; }
}

/// <summary>
/// A question waiting on the Commander, drawn at the head of the tab (Phase 38, "Ask before the plan
/// and the checklist drift apart").
/// </summary>
/// <param name="Text">The question, in the sentence the Commander is agreeing to.</param>
/// <param name="Yes">Accepts it, and answers with what happened.</param>
/// <param name="No">Declines it, and answers with what happened.</param>
public sealed record LoadoutNotice(string Text, Func<string> Yes, Func<string> No);

/// <summary>One pressable line of a loadout index.</summary>
/// <param name="Key">What the crumb below this row is keyed on.</param>
/// <param name="Word">What the breadcrumb calls it.</param>
/// <param name="Text">The line itself.</param>
/// <param name="Aside">The right-hand note: where it is, or what is planned there.</param>
/// <param name="Marked">Whether there is outstanding work here.</param>
public sealed record LoadoutRow(string Key, string Word, string Text, string? Aside, bool Marked)
{
    /// <summary>Whether the thing this row names has been engineered (remediation.md 15, item 10).</summary>
    public bool Engineered { get; init; }

    /// <summary>The heading this row sits under, where the index is grouped (remediation.md 12, item 1).</summary>
    public string? Group { get; init; }

    /// <summary>
    /// The row broken into the parts a slot row is drawn from, or null for a row that is just a line of
    /// text — a ship in the fleet, a suit, a gap.
    /// </summary>
    public LoadoutParts? Parts { get; init; }

    /// <summary>Whether the Commander actually has this thing, and whether they are in it right now.</summary>
    public LoadoutStanding Standing { get; init; }

    /// <summary>
    /// The hull symbol this row is about, lower case as the journal writes it, or null for a row that
    /// is not a ship.
    /// </summary>
    public string? Hull { get; init; }
}

/// <summary>
/// Where a row's subject stands with the Commander: owned, owned and currently in use, or wanted and
/// not bought.
/// </summary>
public enum LoadoutStanding
{
    /// <summary>Owned, and sitting somewhere.</summary>
    Owned,

    /// <summary>The one the Commander is in right now.</summary>
    Active,

    /// <summary>Planned for, and not bought.</summary>
    Wanted,
}

/// <summary>A switch at the head of an index, with somewhere to remember itself.</summary>
/// <param name="Label">What the switch says.</param>
/// <param name="On">Where it is now.</param>
/// <param name="Set">Where to put it, and where to remember it.</param>
public sealed record LoadoutToggle(string Label, bool On, Action<bool> Set);

/// <summary>
/// One side of a slot row: what is in the slot, or what the plan asks for
/// (docs/plans/change-requests.md 38).
/// </summary>
/// <param name="Module">
/// What is there, or what is wanted, in <see cref="D47.Core.Knowledge.ShortNames"/>'s words.
/// </param>
/// <param name="Long">
/// The same module in Frontier's words, for the tooltip — null where the short form is the long one.
/// </param>
/// <param name="Blueprint">
/// The roll, with the module struck off the end of it: Heavy Duty Hull Reinforcement on a row already
/// saying HRP reads Heavy Duty, which is shorter and — the part worth more than the width — comparable
/// straight down the column.
/// </param>
/// <param name="Grade">Its grade, shown as G5.</param>
/// <param name="Experimental">The experimental effect, where there is one.</param>
/// <param name="Effects">What the roll actually did, biggest first.</param>
public sealed record LoadoutSide(
    string? Module,
    string? Long,
    string? Blueprint,
    int? Grade,
    string? Experimental,
    IReadOnlyList<string> Effects)
{
    /// <summary>
    /// Whether the module this side names is one a Powerplay pledge is needed to buy (Phase 38).
    /// </summary>
    public bool Gated { get; init; }

    /// <summary>Whether this side has nothing to say: an empty slot, or a slot with no plan.</summary>
    public bool Silent => Module is not { Length: > 0 }
                          && Blueprint is not { Length: > 0 }
                          && Experimental is not { Length: > 0 };
}

/// <summary>
/// A slot row: the slot, what is fitted in it, and what the plan asks for — one row with two columns
/// rather than an index with a mark (docs/plans/change-requests.md 38).
/// </summary>
/// <param name="Size">
/// The class of module the slot takes, or null where saying it adds nothing — a utility mount is size 0
/// by definition, so the 0 is noise on every one of them.
/// </param>
/// <param name="Slot">
/// The slot itself, short enough for a column: the heading above already says which block this is, so
/// what is left is the ordinal, the size, or a core internal's name.
/// </param>
/// <param name="Current">What the journal says is in the slot.</param>
/// <param name="Plan">What <c>ships.json</c> says is wanted there, or null where nothing is.</param>
/// <param name="Vacant">
/// The word for a slot with nothing in it, which is not always "empty": empty is a fact about the slot
/// and it is only a fact when d47 can see the ship.
/// </param>
public sealed record LoadoutParts(
    int? Size,
    string Slot,
    LoadoutSide Current,
    LoadoutSide? Plan,
    string Vacant)
{
    /// <summary>
    /// Whether the hull already matches the plan, in which case the second column collapses to a tick
    /// and stops.
    /// </summary>
    public bool Met { get; init; }
}

/// <summary>
/// One mode of the Loadout tab — Ships, or Suits and weapons (Phase 27, "The same page, on foot").
/// </summary>
public interface ILoadoutMode
{
    /// <summary>The crumb key of this mode's root, and the word the mode control shows.</summary>
    string RootKey { get; }

    string RootWord { get; }

    /// <summary>How an item's crumb and a slot's crumb are keyed, so a page rebuilds from a trail.</summary>
    string ItemPrefix { get; }

    string SlotPrefix { get; }

    /// <summary>Which capability's help explains a slot of this mode, or null for none.</summary>
    string? SlotHelp { get; }

    /// <summary>Raised when anything this mode draws has changed underneath.</summary>
    event Action? Changed;

    /// <summary>The index: every ship, or everything the Commander wears and carries.</summary>
    IReadOnlyList<LoadoutRow> Items();

    /// <summary>What the index says when it is empty.</summary>
    string EmptyIndex { get; }

    /// <summary>The button that plans something not owned yet, and what pressing it does.</summary>
    string NewLabel { get; }

    void New(PanelPrompts prompts, Action done);

    /// <summary>The heading at the top of an item's page — what the page is about, said once (#289).</summary>
    string? Title(string item) => null;

    /// <summary>A sentence under the heading, or null when there is nothing to say.</summary>
    string? Summary(string item);

    /// <summary>What the item is, under the summary line (remediation.md 13, item 2).</summary>
    IReadOnlyList<LoadoutLine> Details(string item);

    /// <summary>
    /// The gauges at the head of this item's slot list, or empty for a mode with none (Phase 38).
    /// </summary>
    IReadOnlyList<LoadoutGauge> Gauges(string item) => [];

    /// <summary>The hull symbol behind one item, or null for a mode whose items are not ships (#289).</summary>
    string? HullOf(string item) => null;

    /// <summary>Whether the index is a grid of cards rather than a list of rows (asked for 2026-09-03).</summary>
    bool Cards => false;

    /// <summary>A switch for the index itself, or null for a mode with nothing to switch.</summary>
    LoadoutToggle? IndexToggle => null;

    /// <summary>A question waiting on the Commander, or null when nothing is (Phase 38).</summary>
    LoadoutNotice? Notice() => null;

    /// <summary>That item's slots, as an index.</summary>
    IReadOnlyList<LoadoutRow> Slots(string item);

    /// <summary>What the slot index says when there is nothing in it at all.</summary>
    string EmptySlots { get; }

    /// <summary>Offers the whole item to the checklist, and says what happened.</summary>
    string Promote(string item);

    /// <summary>
    /// Whether a plan may be dragged from one slot to another, without moving it (remediation.md 15,
    /// item 1).
    /// </summary>
    bool CanCopy(string item, string from, string to);

    /// <summary>Copies a slot's plan onto another slot, and says what happened.</summary>
    string Copy(string item, string from, string to);

    string PromoteLabel { get; }

    /// <summary>
    /// The label for dropping this item's plan, or null where there is nothing to drop (remediation.md
    /// 11, item 7).
    /// </summary>
    string? DropLabel(string item);

    /// <summary>Drops it, and says what happened.</summary>
    string Drop(string item);

    /// <summary>What is actually there, from the journal.</summary>
    IReadOnlyList<LoadoutLine> Fitted(string item, string slot);

    /// <summary>What the Commander wants, with the journal's verdict and what it costs.</summary>
    IReadOnlyList<LoadoutLine> Planned(string item, string slot);

    /// <summary>Whether this slot has a plan, which decides what the buttons say.</summary>
    bool HasPlan(string item, string slot);

    /// <summary>Asks for a plan for one slot, in this mode's own vocabulary.</summary>
    void Ask(string item, string slot, PanelPrompts prompts, Action done);

    /// <summary>Takes a slot's plan out.</summary>
    void Clear(string item, string slot);

    /// <summary>The phrase for what the Commander is looking at, per level.</summary>
    string SayAtIndex { get; }

    string SayAtItem { get; }

    string SayAtSlot(string slot);
}
