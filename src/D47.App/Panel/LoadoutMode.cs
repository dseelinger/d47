using D47.Core.Loadout;

namespace D47.App.Panel;

/// <summary>
/// Where the gap page gets its arithmetic, and how it knows to redo it (list.md Phase 27, "Gap
/// analysis").
/// <para>
/// A source rather than a report, because the answer moves under the page in three separate ways
/// — a ship build, an on-foot build, and what the Commander is carrying — and because whether
/// hulls they do not own count is a filter they flip rather than a decision taken once on their
/// behalf.
/// </para>
/// </summary>
public sealed class GapSource(Func<bool, GapReport> report)
{
    /// <summary>Raised when either store changed. The page redraws; nothing else cares.</summary>
    public event Action? Changed;

    public GapReport Of(bool includeIntended) => report(includeIntended);

    public void Invalidate() => Changed?.Invoke();
}

/// <summary>How a line of a loadout page is drawn. Four, and no more, is the point.</summary>
public enum LoadoutTone
{
    /// <summary>A fact, at body size.</summary>
    Body,

    /// <summary>A qualification, an aside, a cost. Most of a page.</summary>
    Muted,

    /// <summary>Something that is wrong, or a gate nothing will get past.</summary>
    Danger,

    /// <summary>The head of a block — "Fitted", "Planned", "What it costs".</summary>
    Heading,

    /// <summary>
    /// What a module has been engineered with (remediation.md 15, item 10).
    /// <para>
    /// Its own tone rather than <see cref="Body"/> because it answers a different question from the
    /// rest of the block: the module's name says what is fitted, and this says what was done to it.
    /// Asked for as "the engineering font in a different colour in the details pane".
    /// </para>
    /// </summary>
    Engineered,
}

/// <summary>One line of a loadout page, as content rather than as a control.</summary>
public sealed record LoadoutLine(string Text, LoadoutTone Tone = LoadoutTone.Muted)
{
    /// <summary>
    /// The grade this line's plan is at, where the line carries one that can be stepped.
    /// <para>
    /// <b>A stepper rather than a link</b> (remediation.md 15, item 4). Changing the grade changes
    /// what is underneath it — the roll count, and so the whole "What it costs" block — so the
    /// control belongs where the numbers are and moves them in place. A link that reopened a
    /// chooser got the Commander back where they started.
    /// </para>
    /// <para>
    /// Null on every other line, and null on a blueprint offering one grade, because there is
    /// nothing to step.
    /// </para>
    /// </summary>
    public LoadoutStep? Step { get; init; }

    /// <summary>
    /// One value on this line the Commander can take away with them, where the line carries one.
    /// Null on every other line.
    /// </summary>
    public LoadoutCopy? Copy { get; init; }
}

/// <summary>
/// A number on a line that the Commander can move, and what happens when they do.
/// </summary>
/// <param name="Value">Where it is now.</param>
/// <param name="Offered">What it may be, highest first — the stepper clamps to these.</param>
/// <param name="Set">Applies a new value.</param>
public sealed record LoadoutStep(int Value, IReadOnlyList<int> Offered, Action<int> Set);

/// <summary>
/// Something on a line worth putting on the clipboard, and what to say it is.
/// <para>
/// <b>The value, not the sentence.</b> A ship's whereabouts reads "Parked at BNH-T2F, Laksak." and
/// what the Commander wants out of it is <c>Laksak</c> — the system name, on the clipboard, to
/// paste into Elite's Galaxy Map search. Copying the line they can see would hand them a sentence
/// the Galaxy Map will not find.
/// </para>
/// </summary>
/// <param name="Value">Exactly what goes on the clipboard.</param>
/// <param name="Tip">What the pointer says the glyph will do.</param>
public sealed record LoadoutCopy(string Value, string Tip);

/// <summary>
/// One pressable line of a loadout index.
/// <para>
/// <b>An index rather than a table</b>: one line each, a mark where a plan exists, and everything
/// else in the pane that opens.
/// </para>
/// </summary>
/// <param name="Key">What the crumb below this row is keyed on.</param>
/// <param name="Word">What the breadcrumb calls it.</param>
/// <param name="Text">The line itself.</param>
/// <param name="Aside">The right-hand note: where it is, or what is planned there.</param>
/// <param name="Marked">Whether a plan exists here. A mark, never a column.</param>
public sealed record LoadoutRow(string Key, string Word, string Text, string? Aside, bool Marked)
{
    /// <summary>
    /// Whether the thing this row names has been engineered (remediation.md 15, item 10).
    /// <para>
    /// <b>Independent of <c>Marked</c>, and the two are read at a glance.</b> The dot means a plan
    /// exists and this means a roll has been done, so a row carries neither, either or both — in
    /// the reported screenshot the Power Distributor was engineered with no plan while the Power
    /// Plant was both.
    /// </para>
    /// </summary>
    public bool Engineered { get; init; }

    /// <summary>
    /// The heading this row sits under, where the index is grouped (remediation.md 12, item 1).
    /// <para>
    /// <b>On the row rather than in a list of lists.</b> A mode answers with one ordered sequence
    /// and the page draws a heading wherever the group changes, which is the same arrangement the
    /// engineer directory already uses — and it means an index with no grouping needs no shape of
    /// its own. Null on every row is a flat list, unchanged.
    /// </para>
    /// </summary>
    public string? Group { get; init; }

    /// <summary>
    /// The row broken into the parts a slot row is drawn from, or null for a row that is just a
    /// line of text — a ship in the fleet, a suit, a gap.
    /// <para>
    /// Asked for 2026-08-20: the slot's <em>name</em> was leading the row and taking the width,
    /// and it is not the primary information about a loadout. What is fitted, what was rolled on
    /// it and what that did are.
    /// </para>
    /// </summary>
    public LoadoutParts? Parts { get; init; }
}

/// <summary>
/// A slot row, in the order it reads: size, then the plan dot, then the module, then what was
/// done to it (asked for 2026-08-20).
/// <para>
/// <b>Parts rather than one string</b>, because they are drawn differently — the size and the
/// module are bold, "empty" is greyed, the gear is a glyph — and because the effects are trimmed
/// to whatever room is left, which cannot be decided until the row is measured.
/// </para>
/// </summary>
/// <param name="Size">
/// The class of module the slot takes, or null where saying it adds nothing — a utility mount is
/// size 0 by definition, so the 0 is noise on every one of them.
/// </param>
/// <param name="Module">What is fitted, or null where nothing is.</param>
/// <param name="Vacant">
/// The word for a slot with nothing in it, which is <b>not always "empty"</b>: empty is a fact
/// about the slot and it is only a fact when d47 can see the ship. For one the Commander is not
/// sitting in, the honest word is that it has not been seen.
/// </param>
/// <param name="Blueprint">The roll, where one has been done or planned.</param>
/// <param name="Grade">Its grade, shown as (G5). Null where there is no roll.</param>
/// <param name="Experimental">The experimental effect, where there is one.</param>
/// <param name="Effects">
/// What the roll actually did, biggest first. Drawn until the row runs out of room and cut at a
/// whole effect rather than mid-word.
/// </param>
public sealed record LoadoutParts(
    int? Size,
    string? Module,
    string Vacant,
    string? Blueprint,
    int? Grade,
    string? Experimental,
    IReadOnlyList<string> Effects);

/// <summary>
/// One mode of the Loadout tab — Ships, or Suits and weapons (list.md Phase 27, "The same page, on
/// foot").
/// <para>
/// <b>This is what "one page kind built once and shown twice" means here.</b> The pages in
/// <see cref="LoadoutPages"/> know about an index, an item and a slot; they know nothing about
/// hulls, suits, blueprints or grades. A mode is the whole of the difference, so the drill stack,
/// the reflow, the breadcrumb, the say-lines and the promote path are one implementation rather
/// than two that have to be kept in step — the same spirit as one widget tree rendering to two
/// surfaces.
/// </para>
/// <para>
/// <b>It stays a second mode rather than a second tab</b>, because the game separates ship and
/// on-foot hard and so does its vocabulary. Nothing about the layout is redrawn.
/// </para>
/// <para>
/// Content rather than controls, deliberately: a mode answers in <see cref="LoadoutLine"/>s and
/// <see cref="LoadoutRow"/>s, so what a page says can be asserted without a visual tree.
/// </para>
/// </summary>
public interface ILoadoutMode
{
    /// <summary>The crumb key of this mode's root, and the word the mode control shows.</summary>
    string RootKey { get; }

    string RootWord { get; }

    /// <summary>How an item's crumb and a slot's crumb are keyed, so a page rebuilds from a trail.</summary>
    string ItemPrefix { get; }

    string SlotPrefix { get; }

    /// <summary>Raised when anything this mode draws has changed underneath.</summary>
    event Action? Changed;

    /// <summary>The index: every ship, or everything the Commander wears and carries.</summary>
    IReadOnlyList<LoadoutRow> Items();

    /// <summary>What the index says when it is empty. A page that says nothing teaches nothing.</summary>
    string EmptyIndex { get; }

    /// <summary>The button that plans something not owned yet, and what pressing it does.</summary>
    string NewLabel { get; }

    void New(PanelPrompts prompts, Action done);

    /// <summary>The line at the top of an item's page, or null when the item is gone.</summary>
    string? Summary(string item);

    /// <summary>
    /// What the item <em>is</em>, under the summary line (remediation.md 13, item 2).
    /// <para>
    /// <b>Facts about the thing rather than about its plan.</b> A ship the Commander is not flying
    /// had a page carrying one sentence about builds and a list of slots reading "not seen", which
    /// is less than the row they pressed to get there told them. Where it is, what it is worth,
    /// what the hull can do — the questions somebody opens a ship to ask.
    /// </para>
    /// <para>
    /// Empty is a real answer, and the block is not drawn at all for a mode that has nothing
    /// extra to say.
    /// </para>
    /// </summary>
    IReadOnlyList<LoadoutLine> Details(string item);

    /// <summary>That item's slots, as an index.</summary>
    IReadOnlyList<LoadoutRow> Slots(string item);

    /// <summary>What the slot index says when there is nothing in it at all.</summary>
    string EmptySlots { get; }

    /// <summary>Offers the whole item to the checklist, and says what happened.</summary>
    string Promote(string item);

    /// <summary>
    /// Whether a plan may be dragged from one slot to another, without moving it
    /// (remediation.md 15, item 1). Asked while the mouse is still down, so an invalid target can
    /// simply not highlight rather than accepting the drop and explaining afterwards.
    /// </summary>
    bool CanCopy(string item, string from, string to);

    /// <summary>
    /// Copies a slot's plan onto another slot, and says what happened. Overwrites whatever the
    /// target held, because that is what dragging means.
    /// </summary>
    string Copy(string item, string from, string to);

    string PromoteLabel { get; }

    /// <summary>
    /// The label for dropping this item's plan, or null where there is nothing to drop
    /// (remediation.md 11, item 7).
    /// <para>
    /// <b>Only for something the Commander does not own.</b> An owned ship comes out of the
    /// journal and is not d47's to remove — the plan attached to it is, but the ship is a fact.
    /// An intended hull is entirely authored, and a Commander who changes their mind about buying
    /// a Python had no way at all to say so: the button that made it is on the index and nothing
    /// undid it.
    /// </para>
    /// <para>
    /// Null rather than a disabled button, because a control that exists to be refused teaches
    /// the wrong thing about what the page can do.
    /// </para>
    /// </summary>
    string? DropLabel(string item);

    /// <summary>Drops it, and says what happened. Only ever called where the label is not null.</summary>
    string Drop(string item);

    /// <summary>What is actually there, from the journal. The truth block.</summary>
    IReadOnlyList<LoadoutLine> Fitted(string item, string slot);

    /// <summary>
    /// What the Commander wants, with the journal's verdict and what it costs. Its own block, and
    /// never merged into the one above.
    /// </summary>
    IReadOnlyList<LoadoutLine> Planned(string item, string slot);

    /// <summary>Whether this slot has a plan, which decides what the buttons say.</summary>
    bool HasPlan(string item, string slot);

    /// <summary>Asks for a plan for one slot, in this mode's own vocabulary.</summary>
    void Ask(string item, string slot, PanelPrompts prompts, Action done);

    /// <summary>Takes a slot's plan out.</summary>
    void Clear(string item, string slot);

    /// <summary>
    /// The phrase for what the Commander is looking at, per level. <b>How they learn it</b>: the
    /// ray points and the voice edits, so a page that offers no phrase is a page whose faster half
    /// is invisible.
    /// </summary>
    string SayAtIndex { get; }

    string SayAtItem { get; }

    string SayAtSlot(string slot);
}
