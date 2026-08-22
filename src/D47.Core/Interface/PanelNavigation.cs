namespace D47.Core.Interface;

/// <summary>
/// The panel's surfaces, as the bar shows them (list.md Phase 25, "One transcript, three
/// views").
/// <para>
/// Seven, and the collapse is what pays for them: Conversation, Technical and the log file were
/// three tabs and are three <em>modes</em> of one, because they are three readings of one
/// exchange rather than three destinations. The bar carries the surfaces below without growing.
/// Routing arrived the same way and for the same reason (list.md Phase 37) — Plan, Progress and
/// Course are three readings of one journey, so they cost one tab rather than three.
/// </para>
/// <para>
/// In Core rather than beside the view, because a tab is something the Commander can say. The
/// keyword router is the model-free path and it reaches settings the model cannot; naming the
/// tabs here means the spoken route and the drawn one read one list.
/// </para>
/// </summary>
public enum PanelTab
{
    /// <summary>The conversation, at two verbosities, and today's log file.</summary>
    Transcript,

    /// <summary>
    /// Where the Commander is going, in three readings (list.md Phase 37): the plan, the route
    /// being flown, and getting a system name into the game.
    /// <para>
    /// Second, beside the transcript rather than out among the ledgers, because it is read while
    /// flying rather than administered between trips.
    /// </para>
    /// </summary>
    Routing,

    /// <summary>What the Commander is working on, in their own order (list.md Phase 17).</summary>
    Checklist,

    /// <summary>
    /// Stories the Commander flies, told by the ship's AI (list.md Phase 47). Beside the checklist,
    /// because it is the other thing that outlives a session — and deliberately not a mode of it,
    /// because a story is not a list and the phase exists to keep it from reading as one.
    /// </summary>
    Adventures,

    /// <summary>Ships, suits and weapons, and the arithmetic between them (list.md Phases 26-27).</summary>
    Loadout,

    /// <summary>Who unlocks what, and how far away they are (list.md Phase 28).</summary>
    Engineers,

    /// <summary>Clocks, timers and alarms (list.md Phase 24).</summary>
    Utilities,

    /// <summary>
    /// The settings surface. Last, and hidden in the headset for the reason
    /// <c>PanelView.axaml</c> already records: a 1180-pixel nav column at a metre is a wall.
    /// </summary>
    Settings,
}

/// <summary>
/// One step of a drill, as an identity and as a word (list.md Phase 25, "Drill in, and find
/// your way back").
/// <para>
/// The word is not decoration. The breadcrumb is both the back affordance and the you-are-here
/// - a headset has no title bar to orient by - and <b>each crumb is a word that can be said as
/// well as pressed</b>, so the spoken route and the drawn one are the same list read two ways
/// rather than two vocabularies to keep in step.
/// </para>
/// </summary>
/// <param name="Key">
/// What this level is, to the code: stable, unique within its trail, and never shown. A ship's
/// identifier rather than its name, so two Pythons are two crumbs.
/// </param>
/// <param name="Word">What this level is, to the Commander. Shown, and matched when spoken.</param>
/// <param name="Modal">
/// Whether this level holds the panel until it is dismissed - a chooser (list.md Phase 25,
/// "Choosing takes the panel"). Drawn as a level and behaving as a modal: <b>no navigating away
/// mid-choice</b>. Held on the crumb rather than on the navigator so that popping it clears the
/// state rather than requiring somebody to remember to.
/// </param>
public sealed record NavCrumb(string Key, string Word, bool Modal = false)
{
    /// <summary>
    /// What kind of level this is, where the kind has alternatives — a ship, a slot
    /// (remediation.md 11, item 5). Null for a level that is only ever itself, which is most of
    /// them, and which nests exactly as it always did.
    /// <para>
    /// <b>Two crumbs of one level are alternatives rather than a path.</b> A wide panel shows the
    /// level you are on beside the one above it, so the list you drilled from is still on screen
    /// and still pressable — and pressing another ship there produced
    /// <c>Ships › Tulimiekka › Reaper › Cartage</c>, which reads as a route through three ships and
    /// is not a thing that can be true. Pushing a level replaces the one of its own kind that is
    /// already open, and everything that was underneath it, because a slot of one ship is not a
    /// slot of another.
    /// </para>
    /// <para>
    /// Declared on the crumb rather than worked out from the key, so it is a statement by the page
    /// that pushes rather than a rule about string prefixes that a rename would silently break.
    /// </para>
    /// </summary>
    public string? Level { get; init; }
}

/// <summary>
/// One place a surface can be sent to: a root, and the tab it is a root of (list.md Phase 46).
/// <para>
/// The destination vocabulary a switch position may name is this list, derived from what each
/// surface registered, so there is no second list to keep in step with the spoken route — the
/// invariant this file opens with. A switch that names a root nobody furnished inherits
/// <see cref="PanelNavigator.Select"/> declining to select a tab nobody furnished.
/// </para>
/// </summary>
public sealed record PanelDestination(PanelTab Tab, NavCrumb Root)
{
    /// <summary>
    /// How it reads in a list of every destination: the root's word, and the tab when the word
    /// alone would not say which tab — "Technical (Transcript)", but "Checklist" rather than
    /// "Checklist (Checklist)".
    /// </summary>
    public string Describe() =>
        string.Equals(Root.Word, Tab.ToString(), StringComparison.OrdinalIgnoreCase)
            ? Root.Word
            : $"{Root.Word} ({Tab})";
}

/// <summary>
/// Where the Commander is, per tab and per root, and every way of changing it (list.md Phase
/// 25, "Drill in, and find your way back").
/// <para>
/// <b>The tab is the root rather than the first level.</b> Fleet, Locker and Directory are
/// roots, not levels, so the breadcrumb's first crumb <em>is</em> the tab - which is what makes
/// pressing an already-selected tab a way back to it rather than a no-op.
/// </para>
/// <para>
/// <b>A tab with more than one root keeps a stack per root</b>, and drill state survives a tab
/// switch. Loadout's three modes are three roots; leaving Ships halfway down a slot and coming
/// back to it should arrive where it was left, and going to Suits meanwhile should not disturb
/// that.
/// </para>
/// <para>
/// <b>Voice jumps levels</b>, so a trail has to be able to materialise mid-drill rather than
/// only being built by walking - see <see cref="GoTo(NavCrumb[])"/>. A breadcrumb that can only
/// be produced one step at a time is a breadcrumb that is wrong the first time somebody says
/// "the third weapon slot".
/// </para>
/// <para>
/// Pure, and in Core for the reason <see cref="ZoomLadder"/> is: it is arithmetic over lists of
/// records. No window, no dispatcher, and a test can walk every route without a surface.
/// </para>
/// </summary>
public sealed class PanelNavigator
{
    /// <summary>
    /// The roots each tab offers, in the order the mode control shows them. The first is where a
    /// tab arrives when it has never been visited.
    /// </summary>
    private readonly Dictionary<PanelTab, List<NavCrumb>> _roots = [];

    /// <summary>One trail per root, keyed by the root's key. Each begins with its own root.</summary>
    private readonly Dictionary<string, List<NavCrumb>> _trails = [];

    /// <summary>Which root each tab was last on, so a tab switch returns to the mode it left.</summary>
    private readonly Dictionary<PanelTab, string> _current = [];

    /// <summary>Raised whenever the tab, the root or any trail changes. Never for a refused move.</summary>
    public event EventHandler? Changed;

    /// <summary>Which tab is showing.</summary>
    public PanelTab Tab { get; private set; } = PanelTab.Transcript;

    /// <summary>
    /// Declares a root of a tab. Called once per root at construction, in display order.
    /// <para>
    /// Idempotent by key, so a host that registers twice gets one root rather than two identical
    /// crumbs - which would present as a mode control with a repeated entry.
    /// </para>
    /// </summary>
    public void Register(PanelTab tab, NavCrumb root)
    {
        if (!_roots.TryGetValue(tab, out var roots))
        {
            roots = [];
            _roots[tab] = roots;
        }

        if (roots.Any(existing => existing.Key == root.Key))
        {
            return;
        }

        roots.Add(root);
        _trails[root.Key] = [root];

        _current.TryAdd(tab, root.Key);
    }

    /// <summary>The roots a tab offers. Empty for a tab no host has furnished.</summary>
    public IReadOnlyList<NavCrumb> Roots(PanelTab tab) =>
        _roots.TryGetValue(tab, out var roots) ? roots : [];

    /// <summary>Whether a tab has anything behind it. A bar only shows the ones that do.</summary>
    public bool Has(PanelTab tab) => Roots(tab).Count > 0;

    /// <summary>
    /// Every root of every tab this surface furnished, in bar order (list.md Phase 46). The
    /// whole of what a switch position may name, and nothing that is not also sayable.
    /// </summary>
    public IReadOnlyList<PanelDestination> Destinations =>
        [.. Enum.GetValues<PanelTab>().SelectMany(tab => Roots(tab).Select(root => new PanelDestination(tab, root)))];

    /// <summary>
    /// Puts this surface on the root with this key, whichever tab it belongs to — the tab's
    /// mode first and then the tab, so a destination on another tab arrives without a visible
    /// flick through that tab's previous mode. False when no tab here has it, when a chooser
    /// holds the panel, or when it is already showing — which is the <em>are you already
    /// there</em> answer a switch needs, given exactly rather than inferred (list.md Phase 46).
    /// </summary>
    public bool Show(string rootKey)
    {
        foreach (var tab in _roots.Keys)
        {
            if (!Roots(tab).Any(root => root.Key == rootKey))
            {
                continue;
            }

            var moved = SelectRoot(tab, rootKey);

            return Select(tab) || moved;
        }

        return false;
    }

    /// <summary>
    /// The root the current tab is on. The first crumb of the breadcrumb, and the thing pressing
    /// the tab again returns to.
    /// </summary>
    public NavCrumb Root =>
        Trail.Count > 0 ? Trail[0] : new NavCrumb(Tab.ToString(), Tab.ToString());

    /// <summary>
    /// Root first, leaf last. What the breadcrumb draws and what a spoken crumb word is matched
    /// against.
    /// </summary>
    public IReadOnlyList<NavCrumb> Trail =>
        _current.TryGetValue(Tab, out var root) && _trails.TryGetValue(root, out var trail)
            ? trail
            : [];

    /// <summary>Nothing to go back to. The mode control is visible here and nowhere else.</summary>
    public bool AtRoot => Trail.Count <= 1;

    /// <summary>
    /// A chooser is holding the panel. Every route that would navigate away is refused while it
    /// is; dismissing it is <see cref="Back"/>, which is the same affordance as any other level.
    /// </summary>
    public bool Modal => Trail.Count > 0 && Trail[^1].Modal;

    /// <summary>
    /// Shows a tab, restoring the root and the trail it was left on. False when the tab has no
    /// roots, when it is already showing, or when a chooser holds the panel.
    /// </summary>
    public bool Select(PanelTab tab)
    {
        if (Modal || tab == Tab || !Has(tab))
        {
            return false;
        }

        Tab = tab;
        Raise();
        return true;
    }

    /// <summary>
    /// Switches the current tab to another of its roots, keeping the drill state of both. False
    /// when the key is not a root of this tab, when it is already the current one, or when a
    /// chooser holds the panel.
    /// </summary>
    public bool SelectRoot(string rootKey) => SelectRoot(Tab, rootKey);

    /// <summary>
    /// The same, for a tab that is not the one showing.
    /// <para>
    /// The route that lets a host set a mode without also switching to it: the desktop window
    /// opens the log file from a menu, and doing that by first selecting the tab would mean a
    /// visible flick through whatever page was showing.
    /// </para>
    /// </summary>
    public bool SelectRoot(PanelTab tab, string rootKey)
    {
        if (Modal
            || !Roots(tab).Any(root => root.Key == rootKey)
            || (_current.TryGetValue(tab, out var showing) && showing == rootKey))
        {
            return false;
        }

        _current[tab] = rootKey;
        Raise();
        return true;
    }

    /// <summary>Which root a tab is on, whether or not it is the tab showing.</summary>
    public string RootKeyOf(PanelTab tab) =>
        _current.TryGetValue(tab, out var root) ? root : string.Empty;

    /// <summary>
    /// One level deeper. Refused while a chooser holds the panel, except by the chooser itself -
    /// which is why a modal crumb is pushed with <see cref="Take"/> rather than through here.
    /// </summary>
    public bool Drill(NavCrumb crumb) => !Modal && Push(crumb);

    /// <summary>
    /// Pushes a level that holds the panel until it is dismissed - a chooser.
    /// <para>
    /// Separate from <see cref="Drill"/> so the one route that is allowed to push onto a modal is
    /// named rather than implied: a chooser opened from a chooser is legitimate (pick the
    /// engineer, then pick the grade) and nothing else is.
    /// </para>
    /// </summary>
    public bool Take(NavCrumb crumb) => Push(crumb with { Modal = true });

    /// <summary>
    /// Back one level, and the answer to all three routes that must agree - the breadcrumb, the
    /// controller button and the phrase. False at a root, where back means nothing and the
    /// caller may want the key for something else.
    /// </summary>
    public bool Back()
    {
        var trail = Mutable();

        if (trail is null || trail.Count <= 1)
        {
            return false;
        }

        trail.RemoveAt(trail.Count - 1);
        Raise();
        return true;
    }

    /// <summary>
    /// All the way back to the root of the current tab - what pressing an already-selected tab
    /// does. False when it is already there, so a host can tell a return from a no-op.
    /// <para>
    /// Allowed through a modal: pressing the tab you are already on is unambiguous, and a
    /// chooser that cannot be escaped by the affordance a Commander reaches for first is a
    /// chooser they close by quitting.
    /// </para>
    /// </summary>
    public bool ToRoot()
    {
        var trail = Mutable();

        if (trail is null || trail.Count <= 1)
        {
            return false;
        }

        trail.RemoveRange(1, trail.Count - 1);
        Raise();
        return true;
    }

    /// <summary>
    /// Back to the crumb at this position in the trail - what pressing a breadcrumb does. Zero
    /// is the root. False for the crumb already showing and for anything out of range.
    /// </summary>
    public bool JumpTo(int index)
    {
        var trail = Mutable();

        if (trail is null || index < 0 || index >= trail.Count - 1)
        {
            return false;
        }

        trail.RemoveRange(index + 1, trail.Count - index - 1);
        Raise();
        return true;
    }

    /// <summary>
    /// Back to the crumb with this word - what a spoken crumb does. Case-insensitive, and the
    /// nearest match wins so a word that appears twice in one trail takes the Commander up one
    /// rather than all the way.
    /// </summary>
    public bool JumpTo(string word)
    {
        var trail = Trail;

        for (var index = trail.Count - 2; index >= 0; index--)
        {
            if (string.Equals(trail[index].Word, word, StringComparison.OrdinalIgnoreCase))
            {
                return JumpTo(index);
            }
        }

        return false;
    }

    /// <summary>
    /// Puts the Commander at the end of a trail they did not walk (list.md Phase 25, "Voice jumps
    /// levels").
    /// <para>
    /// This is the route that makes the breadcrumb honest. "Open the third weapon slot on the
    /// Corsair" lands three levels down, and a trail built only by walking would either show one
    /// crumb with nothing above it or show whatever the Commander happened to visit last. The
    /// caller supplies the whole chain below the root and it replaces what was there.
    /// </para>
    /// <para>
    /// The root is the tab's own and is never supplied: a jump within a tab cannot change which
    /// root it is on without saying so, and <see cref="SelectRoot"/> is how that is said.
    /// </para>
    /// </summary>
    public bool GoTo(params NavCrumb[] crumbs) => GoTo((IReadOnlyList<NavCrumb>)crumbs);

    /// <inheritdoc cref="GoTo(NavCrumb[])"/>
    public bool GoTo(IReadOnlyList<NavCrumb> crumbs)
    {
        var trail = Mutable();

        if (trail is null || Modal)
        {
            return false;
        }

        var root = trail[0];

        trail.Clear();
        trail.Add(root);

        // A caller that supplies the root anyway is not given it twice. Four Phase 47 call sites
        // did, and a trail of [root, root, level] is one a three-pane strip cannot draw: the same
        // page would be hosted in two panes, and a control has exactly one parent. The contract
        // above stands; this is the trail refusing to hold a shape nothing can render.
        trail.AddRange(crumbs.Count > 0 && crumbs[0].Key == root.Key ? crumbs.Skip(1) : crumbs);

        Raise();
        return true;
    }

    private bool Push(NavCrumb crumb)
    {
        var trail = Mutable();

        if (trail is null || trail[^1].Key == crumb.Key)
        {
            return false;
        }

        // A level with alternatives replaces the one of its kind that is already open, and takes
        // whatever was below it — see NavCrumb.Level. From index 1, because the root is the tab
        // and a tab is not an alternative to anything.
        if (crumb.Level is { Length: > 0 } level)
        {
            for (var i = 1; i < trail.Count; i++)
            {
                if (string.Equals(trail[i].Level, level, StringComparison.Ordinal))
                {
                    trail.RemoveRange(i, trail.Count - i);
                    break;
                }
            }
        }

        trail.Add(crumb);
        Raise();
        return true;
    }

    private List<NavCrumb>? Mutable() =>
        _current.TryGetValue(Tab, out var root) && _trails.TryGetValue(root, out var trail)
            ? trail
            : null;

    private void Raise() => Changed?.Invoke(this, EventArgs.Empty);
}
