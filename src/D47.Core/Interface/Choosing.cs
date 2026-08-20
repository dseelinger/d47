namespace D47.Core.Interface;

/// <summary>
/// Where a chooser is drawn (list.md Phase 25, "Choosing takes the panel").
/// <para>
/// <b>Declared per call site, never computed from list length.</b> A module picker is a page and
/// a five-item settings enum stays the in-tree layer, and which one a control gets is a property
/// of the control rather than of how many rows it happens to have today — because a control that
/// behaves differently on different days is a control nobody trusts.
/// </para>
/// </summary>
public enum ChoiceSurface
{
    /// <summary>
    /// The chooser <b>replaces the panel</b> until it is dismissed, which makes it a level of the
    /// drill stack rather than a popup.
    /// <para>
    /// Popups cannot exist in the VR path at all: setting <c>IsDropDownOpen</c> exits at
    /// <c>0xC00000FD</c> before any dispatcher work, with no exception and nothing in the log,
    /// because the panel's window is never shown and a popup has no top level to hang from.
    /// Forcing it into the window's own overlay layer is the same crash.
    /// </para>
    /// <para>
    /// A second overlay quad was the obvious alternative and is not the answer: the crash is
    /// Avalonia's rather than OpenVR's, so a new quad still needs a second never-shown window and
    /// its own render target, plus a ray priority rule between overlapping quads — and the result
    /// would be a VR-only mechanism for a control the desktop also has. Taking the panel needs
    /// none of that, is in the widget tree so both surfaces get it identically, and <b>fixes
    /// ergonomics rather than only occlusion</b>: a full panel is roughly sixteen rows at a
    /// ray-safe height and twenty-five zoomed out, where a layer over the page is fewer.
    /// </para>
    /// </summary>
    Page,

    /// <summary>
    /// Drawn over the page it was opened from, in the same tree. What a short closed vocabulary
    /// gets — a theme, a lock mode — where taking the whole panel for five rows would be a level
    /// of navigation to answer a question the Commander did not think of as one.
    /// </summary>
    Layer,
}

/// <summary>One thing that can be picked.</summary>
/// <param name="Key">What it is, to the code. Stable, unique in its list, never shown.</param>
/// <param name="Label">What it is, to the Commander. Shown, and said.</param>
/// <param name="Detail">
/// A second line, where the label alone does not decide it — a module's mass beside its name.
/// Optional, because most lists do not need one and a blank second line on every row is a list
/// that fits half as many.
/// </param>
public sealed record ChoiceOption(string Key, string Label, string? Detail = null)
{
    /// <summary>
    /// The heading this option sits under, where the list is grouped. Null on every option is a
    /// flat list, unchanged.
    /// <para>
    /// <b>On the option rather than in a list of lists</b>, which is the arrangement
    /// <see cref="LoadoutRow"/> and the engineer directory already use: the caller answers with one
    /// ordered sequence and the chooser draws a heading wherever the group changes. It means a
    /// grouped chooser needs no shape of its own, and a search that narrows the list cannot leave a
    /// heading standing over nothing.
    /// </para>
    /// <para>
    /// Asked for 2026-08-20 against the checklist's scope filter, which listed <c>derived</c>,
    /// <c>engineeringplan</c>, <c>open</c> and <c>ship</c> as though they were alternatives. They
    /// are answers to four different questions, and nothing said so.
    /// </para>
    /// </summary>
    public string? Group { get; init; }
}

/// <summary>
/// A question the panel is being asked to put to the Commander (list.md Phase 25, "Choosing takes
/// the panel").
/// <para>
/// <b>The chooser carries what it is choosing for in its header</b> — the slot, its size, and
/// what is fitted now. That is the one thing a dropdown cannot do, and it is what taking the
/// panel would otherwise cost: a list floating over a page has the page behind it for context,
/// and a list that replaced the page does not unless it brings the context with it.
/// </para>
/// </summary>
/// <param name="Key">The crumb key this becomes, when it is a page.</param>
/// <param name="Word">The crumb word. Shown in the breadcrumb, and sayable.</param>
/// <param name="Title">What is being chosen. "Weapon 3".</param>
/// <param name="Context">
/// What it is being chosen for: the slot's size, and what is fitted now. The header's second
/// line, and the reason this is a record rather than a list of strings.
/// </param>
/// <param name="Options">What can be picked, in the order they are shown.</param>
/// <param name="Current">
/// Which option is fitted now, by key, or null where nothing is. Marked rather than pre-selected:
/// a chooser that opens with a row highlighted invites a press that changes nothing.
/// </param>
/// <param name="Surface">Page or layer. See <see cref="ChoiceSurface"/>.</param>
public sealed record ChoiceRequest(
    string Key,
    string Word,
    string Title,
    string? Context,
    IReadOnlyList<ChoiceOption> Options,
    string? Current,
    ChoiceSurface Surface)
{
    /// <summary>
    /// What the option the Commander is already on is called, in words.
    /// <para>
    /// Said rather than only coloured, because a fact carried by a hue alone is a fact some of the
    /// Commanders reading it do not have. It is a parameter rather than a constant because the
    /// chooser is general and its callers are not: "fitted now" is right for a hardpoint and wrong
    /// for the reading of a page you are looking at (remediation.md 10, item 1), which is what it
    /// said when the mode control became a chooser.
    /// </para>
    /// </summary>
    public string CurrentWord { get; init; } = "fitted now";

    /// <summary>
    /// Whether the chooser narrows itself as the Commander types (remediation.md 12, item 5).
    /// <para>
    /// <b>Declared per call site</b>, like the surface above it and for the same reason. A
    /// module picker for a size 6 compartment is well over a hundred rows and a scroll hunt
    /// without one; a five-item settings enum with a search box above it is a control that draws
    /// attention to itself for nothing.
    /// </para>
    /// <para>
    /// It is a filter rather than a spelling test: it takes rows away and never refuses a value,
    /// so the Commander who types "cargo" gets every cargo rack and the one who types nothing
    /// gets the list. That is the whole difference from asking for the name — a name has to be
    /// got right, and a list only has to be got near.
    /// </para>
    /// </summary>
    public bool Searchable { get; init; }

    /// <summary>Which option a spoken label names, or null when none of them does.</summary>
    /// <remarks>
    /// Whole-label and case-insensitive, deliberately. The router's own rule applies here for the
    /// same reason: a miss costs the Commander a press, and a false positive fits the wrong
    /// module.
    /// </remarks>
    public ChoiceOption? Match(string spoken) =>
        Options.FirstOrDefault(option =>
            string.Equals(option.Label, spoken.Trim(), StringComparison.OrdinalIgnoreCase));
}
