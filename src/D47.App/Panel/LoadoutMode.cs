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
/// A second figure on a gauge's bar, drawn as a mark rather than as fill
/// (list.md Phase 38).
/// </summary>
/// <param name="At">Where it sits, 0 to 1 of the bar's width.</param>
/// <param name="Label">What it is, for the reading under the bar.</param>
public readonly record struct LoadoutMark(double At, string Label);

/// <summary>
/// One gauge at the head of a ship's slot list — power, or jump range
/// (list.md Phase 38, "A build you can watch").
/// <para>
/// <b>A horizontal bar rather than a dial</b> (the Commander's call, 2026-08-20). It reads at
/// overlay resolution, it survives a narrow panel, and it needs no drawing code of its own: a
/// filled rectangle inside another one, with the other figures as marks along it.
/// </para>
/// <para>
/// <b>Content rather than a control</b>, like every other shape in this file, so what a gauge says
/// can be asserted without a visual tree.
/// </para>
/// </summary>
/// <param name="Name">"Power", "Jump range". The row's own label.</param>
/// <param name="Reading">The figures, in the shortest form that stays true.</param>
/// <param name="Fill">How much of the bar is filled, 0 to 1. Clamped by the drawing.</param>
/// <param name="Tone">
/// <see cref="LoadoutTone.Danger"/> for a build that does not fit, and
/// <see cref="LoadoutTone.Body"/> for one that does.
/// </param>
public sealed record LoadoutGauge(string Name, string Reading, double Fill, LoadoutTone Tone)
{
    /// <summary>The other figures on the same bar. Empty for a gauge with one number.</summary>
    public IReadOnlyList<LoadoutMark> Marks { get; init; } = [];

    /// <summary>What the figures mean, or what is wrong with them. Null where the reading says it.</summary>
    public string? Note { get; init; }

    /// <summary>
    /// Whether this figure was worked out rather than read off the game (list.md Phase 38).
    /// <para>
    /// <b>Drawn differently, and that is the condition on which the gauges exist at all.</b>
    /// <c>ShipsMode.Parted</c> holds that a modelled figure must never sit beside a measured one
    /// looking the same; the amendment narrows that rule to these two gauges and pays for it by
    /// marking every modelled reading.
    /// </para>
    /// </summary>
    public bool Modelled { get; init; }
}

/// <summary>
/// A question waiting on the Commander, drawn at the head of the tab
/// (list.md Phase 38, "Ask before the plan and the checklist drift apart").
/// <para>
/// <b>The banner is the half that cannot be missed.</b> The question is asked out loud at the
/// moment it becomes true — boarding the ship — because that is when the Commander is in the
/// cockpit and not looking at a window. This is what is left behind if they did not answer, so a
/// question asked while they were busy is still answerable an hour later.
/// </para>
/// <para>
/// <b>Two buttons and no third state.</b> Yes revises, no drops it, and there is no dismiss —
/// dismissing a question is answering no while pretending not to have.
/// </para>
/// </summary>
/// <param name="Text">The question, in the sentence the Commander is agreeing to.</param>
/// <param name="Yes">Accepts it, and answers with what happened.</param>
/// <param name="No">Declines it, and answers with what happened.</param>
public sealed record LoadoutNotice(string Text, Func<string> Yes, Func<string> No);

/// <summary>
/// One pressable line of a loadout index.
/// <para>
/// <b>An index rather than a table</b>: one line each, a mark, and everything else in the pane
/// that opens. That still holds for every list here <em>except</em> a ship's slots, which became
/// a table on 2026-08-25 (docs/plans/change-requests.md 38) because one line carrying both what
/// is fitted and what is planned had to choose between them, and either choice describes
/// something that is not there. A row with <see cref="Parts"/> is that table's; every other row
/// is the index it always was.
/// </para>
/// </summary>
/// <param name="Key">What the crumb below this row is keyed on.</param>
/// <param name="Word">What the breadcrumb calls it.</param>
/// <param name="Text">The line itself.</param>
/// <param name="Aside">The right-hand note: where it is, or what is planned there.</param>
/// <param name="Marked">
/// Whether there is outstanding work here. A mark, never a column — and on a slot row it means
/// <b>the hull does not match the plan</b> rather than <em>a plan exists</em>, which is why it
/// now goes out when the work is done (GitHub issue 38).
/// </param>
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
/// One side of a slot row: what is in the slot, or what the plan asks for
/// (docs/plans/change-requests.md 38).
/// <para>
/// <b>Current is the journal and Plan is <c>ships.json</c>, and neither ever borrows from the
/// other.</b> That one rule is the whole of this change. A row used to carry a single blended
/// description — <c>Planned(plan) ?? fitted</c> for the module, <c>plan?.Blueprint ??
/// module?.Blueprint</c> for the roll — and every value it produced was true of *something* while
/// describing nothing that existed: a planned Shield Booster in an empty mount drew exactly like
/// the five fitted ones beside it, and a power distributor planned Weapon Focused and rolled
/// Priority Systems read as finished. Both were reported on 2026-08-24, and both are the same
/// sentence: one column had to choose which truth to tell, and there was never a good answer.
/// </para>
/// <para>
/// <b>Effects are only ever Current's.</b> Elite reports what a roll actually landed — 3,384
/// modifiers across the corpus, each with the figure before it — and a planned roll has no
/// figures at all, so a Plan side is built with none and cannot grow any.
/// </para>
/// </summary>
/// <param name="Module">
/// What is there, or what is wanted, in <see cref="D47.Core.Knowledge.ShortNames"/>'s words.
/// Null where the slot is empty, which is the state that had no representation at all.
/// </param>
/// <param name="Long">
/// The same module in Frontier's words, for the tooltip — null where the short form <em>is</em>
/// the long one. A short name is never the only name (the Commander's ruling, 2026-08-25).
/// </param>
/// <param name="Blueprint">
/// The roll, with the module struck off the end of it: <i>Heavy Duty Hull Reinforcement</i> on a
/// row already saying <i>HRP</i> reads <b>Heavy Duty</b>, which is shorter and — the part worth
/// more than the width — comparable straight down the column.
/// </param>
/// <param name="Grade">Its grade, shown as G5. Null where there is no roll.</param>
/// <param name="Experimental">The experimental effect, where there is one.</param>
/// <param name="Effects">
/// What the roll actually did, biggest first. Drawn until the column runs out of room and cut at a
/// whole effect rather than mid-word. Empty on a Plan side, always.
/// </param>
public sealed record LoadoutSide(
    string? Module,
    string? Long,
    string? Blueprint,
    int? Grade,
    string? Experimental,
    IReadOnlyList<string> Effects)
{
    /// <summary>
    /// Whether the module this side names is one a Powerplay pledge is needed to buy
    /// (list.md Phase 38).
    /// <para>
    /// <b>Nineteen modules</b>, named by <c>outfitting.csv</c>'s own <c>entitlement</c> column
    /// rather than by a list anybody maintains — the Prismatic Shield Generator in all eight
    /// sizes, and eleven weapons. Drawn as a coin beside the name, in the danger hue, because it
    /// is a gate rather than a property — and per side, since planning one you have not got is
    /// exactly when the gate is worth knowing about.
    /// </para>
    /// </summary>
    public bool Gated { get; init; }

    /// <summary>Whether this side has nothing to say: an empty slot, or a slot with no plan.</summary>
    public bool Silent => Module is not { Length: > 0 }
                          && Blueprint is not { Length: > 0 }
                          && Experimental is not { Length: > 0 };
}

/// <summary>
/// A slot row: the slot, what is fitted in it, and what the plan asks for — one row with two
/// columns rather than an index with a mark (docs/plans/change-requests.md 38).
/// <para>
/// <b>This overturns a stated Phase 26 ruling rather than filling a gap</b>, and the ruling is in
/// the item's own words: <i>"the slot list an index rather than a table: one line each, a mark
/// where a plan exists, and everything else in the pane"</i>. The product description already
/// promised both facts; what is overturned is the shape, and the shape is what produced an evening
/// of the page being read wrongly in three different ways.
/// </para>
/// <para>
/// <b>Four states, where the row had two.</b> Nothing planned, planned and met, planned and not
/// rolled, and <b>planned with the slot empty</b> — the fourth had no representation of its own
/// and is the one that misled. Each now falls out of the two sides rather than out of a flag:
/// nothing planned is a silent Plan side, met is <see cref="Met"/>, not rolled is two sides that
/// differ, and an empty slot is a silent Current side.
/// </para>
/// <para>
/// <b>Parts rather than one string</b>, because they are drawn differently — the module is bold,
/// "empty" is greyed, the gear is a glyph — and because the effects are trimmed to whatever room
/// is left, which cannot be decided until the column is measured.
/// </para>
/// </summary>
/// <param name="Size">
/// The class of module the slot takes, or null where saying it adds nothing — a utility mount is
/// size 0 by definition, so the 0 is noise on every one of them.
/// </param>
/// <param name="Slot">
/// The slot itself, short enough for a column: the heading above already says which block this is,
/// so what is left is the ordinal, the size, or a core internal's name.
/// </param>
/// <param name="Current">What the journal says is in the slot.</param>
/// <param name="Plan">What <c>ships.json</c> says is wanted there, or null where nothing is.</param>
/// <param name="Vacant">
/// The word for a slot with nothing in it, which is <b>not always "empty"</b>: empty is a fact
/// about the slot and it is only a fact when d47 can see the ship. For one the Commander is not
/// sitting in, the row names the fix instead — boarding it is what makes Elite write the
/// <c>Loadout</c> the page is waiting for.
/// </param>
public sealed record LoadoutParts(
    int? Size,
    string Slot,
    LoadoutSide Current,
    LoadoutSide? Plan,
    string Vacant)
{
    /// <summary>
    /// Whether the hull already matches the plan, in which case <b>the second column collapses</b>
    /// to a tick and stops.
    /// <para>
    /// <b>Repeating identical words in two columns is noise</b>, and worse than noise: it is the
    /// eye being asked to compare two strings that were never going to differ. This is also the
    /// answer to <i>"these have been engineered, the orange circles should be gone, right?"</i> —
    /// the marker means <em>disagreement</em> rather than <em>a plan exists</em>, which is the
    /// thing worth an eye-catching colour.
    /// </para>
    /// </summary>
    public bool Met { get; init; }
}

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

    /// <summary>
    /// Which capability's help explains a slot of this mode, or null for none.
    /// <para>
    /// On the mode rather than on the crumb factory, because <c>SlotCrumb</c> is shared and the
    /// two modes mean different things by a slot: a ship's is where a blueprint gets chosen, and
    /// a suit's engineering is a different page entirely. Guessing from the key prefix would work
    /// today and be wrong the first time a third mode arrives.
    /// </para>
    /// </summary>
    string? SlotHelp { get; }

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

    /// <summary>
    /// The gauges at the head of this item's slot list, or empty for a mode with none
    /// (list.md Phase 38).
    /// <para>
    /// <b>Defaulted rather than added to every mode.</b> Power and jump range are facts about a
    /// hull; a suit has neither, and a mode that has nothing to gauge should not have to say so in
    /// code. So Ships overrides this and On foot inherits the empty answer.
    /// </para>
    /// </summary>
    IReadOnlyList<LoadoutGauge> Gauges(string item) => [];

    /// <summary>
    /// A question waiting on the Commander, or null when nothing is (list.md Phase 38).
    /// <para>
    /// Defaulted for the same reason the gauges are: a mode with nothing to ask should not have to
    /// say so in code.
    /// </para>
    /// </summary>
    LoadoutNotice? Notice() => null;

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
