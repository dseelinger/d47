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
}

/// <summary>One line of a loadout page, as content rather than as a control.</summary>
public sealed record LoadoutLine(string Text, LoadoutTone Tone = LoadoutTone.Muted);

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
public sealed record LoadoutRow(string Key, string Word, string Text, string? Aside, bool Marked);

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

    /// <summary>That item's slots, as an index.</summary>
    IReadOnlyList<LoadoutRow> Slots(string item);

    /// <summary>What the slot index says when there is nothing in it at all.</summary>
    string EmptySlots { get; }

    /// <summary>Offers the whole item to the checklist, and says what happened.</summary>
    string Promote(string item);

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
