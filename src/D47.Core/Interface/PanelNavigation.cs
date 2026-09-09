namespace D47.Core.Interface;

/// <summary>The panel's surfaces, as the bar shows them (Phase 25, "One transcript, three views").</summary>
public enum PanelTab
{
    /// <summary>The conversation, at two verbosities, and today's log file.</summary>
    Transcript,

    /// <summary>Ships, suits and weapons, and the arithmetic between them (Phases 26-27).</summary>
    Loadout,

    /// <summary>Who unlocks what, and how far away they are (Phase 28).</summary>
    Engineers,

    /// <summary>What the Commander is working on, in their own order (Phase 17).</summary>
    Checklist,

    /// <summary>
    /// Where the Commander is going, in three readings (Phase 37): the plan, the route being flown, and
    /// getting a system name into the game.
    /// </summary>
    Routing,

    /// <summary>Stories the Commander flies, told by the ship's AI (Phase 47).</summary>
    Adventures,

    /// <summary>Clocks, timers and alarms (Phase 24).</summary>
    Utilities,

    /// <summary>The settings surface.</summary>
    Settings,
}

/// <summary>
/// One step of a drill, as an identity and as a word (Phase 25, "Drill in, and find your way back").
/// </summary>
/// <param name="Key">
/// What this level is, to the code: stable, unique within its trail, and never shown.
/// </param>
/// <param name="Word">What this level is, to the Commander.</param>
/// <param name="Modal">
/// Whether this level holds the panel until it is dismissed - a chooser (Phase 25, "Choosing takes the
/// panel").
/// </param>
public sealed record NavCrumb(string Key, string Word, bool Modal = false)
{
    /// <summary>
    /// What kind of level this is, where the kind has alternatives — a ship, a slot (remediation.md 11,
    /// item 5).
    /// </summary>
    public string? Level { get; init; }

    /// <summary>
    /// Which capability's help explains this level, or null to inherit whatever the level above
    /// declared (asked for 2026-08-22).
    /// </summary>
    public string? Help { get; init; }

    /// <summary>
    /// What the Commander says to reach this level, where saying the drawn word is not something
    /// anybody would do (#231).
    /// </summary>
    public IReadOnlyList<string> Spoken { get; init; } = [];
}

/// <summary>One place a surface can be sent to: a root, and the tab it is a root of (Phase 46).</summary>
public sealed record PanelDestination(PanelTab Tab, NavCrumb Root)
{
    /// <summary>
    /// How it reads in a list of every destination: the root's word, and the tab when the word alone
    /// would not say which tab — "Technical (Transcript)", but "Checklist" rather than "Checklist
    /// (Checklist)".
    /// </summary>
    public string Describe() =>
        string.Equals(Root.Word, Tab.ToString(), StringComparison.OrdinalIgnoreCase)
            ? Root.Word
            : $"{Root.Word} ({Tab})";
}

/// <summary>
/// Where the Commander is, per tab and per root, and every way of changing it (Phase 25, "Drill in, and
/// find your way back").
/// </summary>
public sealed class PanelNavigator
{
    /// <summary>The roots each tab offers, in the order the mode control shows them.</summary>
    private readonly Dictionary<PanelTab, List<NavCrumb>> _roots = [];

    /// <summary>One trail per root, keyed by the root's key.</summary>
    private readonly Dictionary<string, List<NavCrumb>> _trails = [];

    /// <summary>Which root each tab was last on, so a tab switch returns to the mode it left.</summary>
    private readonly Dictionary<PanelTab, string> _current = [];

    /// <summary>Raised whenever the tab, the root or any trail changes.</summary>
    public event EventHandler? Changed;

    /// <summary>Which tab is showing.</summary>
    public PanelTab Tab { get; private set; } = PanelTab.Transcript;

    /// <summary>Declares a root of a tab.</summary>
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

    /// <summary>The roots a tab offers.</summary>
    public IReadOnlyList<NavCrumb> Roots(PanelTab tab) =>
        _roots.TryGetValue(tab, out var roots) ? roots : [];

    /// <summary>Whether a tab has anything behind it.</summary>
    public bool Has(PanelTab tab) => Roots(tab).Count > 0;

    /// <summary>Every root of every tab this surface furnished, in bar order (Phase 46).</summary>
    public IReadOnlyList<PanelDestination> Destinations =>
        [.. Enum.GetValues<PanelTab>().SelectMany(tab => Roots(tab).Select(root => new PanelDestination(tab, root)))];

    /// <summary>
    /// Puts this surface on the root with this key, whichever tab it belongs to — the tab's mode first
    /// and then the tab, so a destination on another tab arrives without a visible flick through that
    /// tab's previous mode.
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
    /// Which capability's help explains where the Commander is standing, or null where nothing on the
    /// trail claims one (asked for 2026-08-22).
    /// </summary>
    public string? Help
    {
        get
        {
            var trail = Trail;

            for (var level = trail.Count - 1; level >= 0; level--)
            {
                if (trail[level].Help is { Length: > 0 } capability)
                {
                    return capability;
                }
            }

            return null;
        }
    }

    /// <summary>The root the current tab is on.</summary>
    public NavCrumb Root =>
        Trail.Count > 0 ? Trail[0] : new NavCrumb(Tab.ToString(), Tab.ToString());

    /// <summary>Root first, leaf last.</summary>
    public IReadOnlyList<NavCrumb> Trail =>
        _current.TryGetValue(Tab, out var root) && _trails.TryGetValue(root, out var trail)
            ? trail
            : [];

    /// <summary>Nothing to go back to.</summary>
    public bool AtRoot => Trail.Count <= 1;

    /// <summary>A chooser is holding the panel.</summary>
    public bool Modal => Trail.Count > 0 && Trail[^1].Modal;

    /// <summary>Shows a tab, restoring the root and the trail it was left on.</summary>
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

    /// <summary>Switches the current tab to another of its roots, keeping the drill state of both.</summary>
    public bool SelectRoot(string rootKey) => SelectRoot(Tab, rootKey);

    /// <summary>The same, for a tab that is not the one showing.</summary>
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

    /// <summary>One level deeper.</summary>
    public bool Drill(NavCrumb crumb) => !Modal && Push(crumb);

    /// <summary>Pushes a level that holds the panel until it is dismissed - a chooser.</summary>
    public bool Take(NavCrumb crumb) => Push(crumb with { Modal = true });

    /// <summary>
    /// Back one level, and the answer to all three routes that must agree - the breadcrumb, the
    /// controller button and the phrase.
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
    /// All the way back to the root of the current tab - what pressing an already-selected tab does.
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

    /// <summary>Back to the crumb at this position in the trail - what pressing a breadcrumb does.</summary>
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

    /// <summary>Back to the crumb with this word - what a spoken crumb does.</summary>
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
    /// Puts the Commander at the end of a trail they did not walk (Phase 25, "Voice jumps levels").
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

        // A caller that supplies the root anyway is not given it twice.
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

        // A level with alternatives replaces the one of its kind that is already open, and takes whatever was
        // below it — see NavCrumb.Level.
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
