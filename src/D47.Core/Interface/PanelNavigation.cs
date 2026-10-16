namespace D47.Core.Interface;

/// <summary>The panel's surfaces, as the bar shows them (Phase 25, "One transcript, three views").</summary>
public enum PanelTab
{
    /// <summary>The conversation, at two verbosities, and today's log file.</summary>
    Transcript,

    /// <summary>Looking things up: the star system page first, more kinds of search after it.</summary>
    Search,

    /// <summary>Stories the Commander flies, told by the ship's AI (Phase 47).</summary>
    Stories,

    /// <summary>The Commander's own record, starting with the checklist (#805).</summary>
    Commander,

    /// <summary>Ships, suits, the carrier, materials and engineers (Phases 26-28).</summary>
    Assets,

    /// <summary>
    /// Where the Commander is going: Plan with the route being flown beside it, bookmarks, markets and
    /// trade routes.
    /// </summary>
    Navigation,

    /// <summary>The settings surface.</summary>
    Settings,
}

/// <summary>Reads a tab's saved name, including the names of tabs since renamed.</summary>
public static class PanelTabNames
{
    private static readonly Dictionary<string, PanelTab> Former = new(StringComparer.Ordinal)
    {
        ["Adventures"] = PanelTab.Stories,
        ["Checklist"] = PanelTab.Commander,
        ["Loadout"] = PanelTab.Assets,
        ["Routing"] = PanelTab.Navigation,
    };

    /// <summary>The tab a saved name means, or null for a name that is no tab.</summary>
    public static PanelTab? Parse(string? name) =>
        name is null ? null
        : Former.TryGetValue(name, out var renamed) ? renamed
        : Enum.TryParse<PanelTab>(name, out var tab) && Enum.IsDefined(tab) ? tab
        : null;
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

    /// <summary>Whether this level takes the whole strip rather than sharing it with the levels above (#469).</summary>
    public bool Whole { get; init; }

    /// <summary>Whether this level stays on the surface that opened it rather than being carried to the surfaces following it.</summary>
    public bool Local { get; init; }
}

/// <summary>
/// One place a surface can be sent to: a root and the tab it is a root of, or a fixed sub-page of that root
/// (Phase 46, #1149).
/// </summary>
public sealed record PanelDestination(PanelTab Tab, NavCrumb Root, NavCrumb? SubPage = null)
{
    /// <summary>The key a switch position stores: the sub-page's where there is one, else the root's.</summary>
    public string Key => SubPage?.Key ?? Root.Key;

    /// <summary>What the Commander calls it.</summary>
    public string Word => SubPage?.Word ?? Root.Word;

    /// <summary>The level whose word and spoken aliases name it.</summary>
    public NavCrumb Page => SubPage ?? Root;

    /// <summary>
    /// How it reads in a list of every destination: the root's word, and the tab when the word alone
    /// would not say which tab — "Technical (Transcript)", but "Stories" rather than "Stories
    /// (Stories)". A sub-page reads with its root: "Catalogue (Stories)".
    /// </summary>
    public string Describe() =>
        SubPage is { } page ? $"{page.Word} ({Root.Word})"
        : string.Equals(Root.Word, Tab.ToString(), StringComparison.OrdinalIgnoreCase) ? Root.Word
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

    /// <summary>The fixed sub-pages each root declared, keyed by the root's key, in declaration order.</summary>
    private readonly Dictionary<string, List<SubPage>> _subPages = [];

    /// <summary>Every declared sub-page's key, so no root or sub-page can take one again.</summary>
    private readonly HashSet<string> _subPageKeys = new(StringComparer.Ordinal);

    /// <summary>Declares a root of a tab. A key a sub-page already holds is ignored.</summary>
    public void Register(PanelTab tab, NavCrumb root)
    {
        if (_subPageKeys.Contains(root.Key))
        {
            return;
        }

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

    /// <summary>Root keys withheld from <see cref="Roots"/> until shown again.</summary>
    private readonly HashSet<string> _hidden = new(StringComparer.Ordinal);

    /// <summary>The roots a tab offers, without the hidden ones.</summary>
    public IReadOnlyList<NavCrumb> Roots(PanelTab tab) =>
        _roots.TryGetValue(tab, out var roots) ? [.. roots.Where(root => !_hidden.Contains(root.Key))] : [];

    /// <summary>
    /// Hides or shows a root, keeping its place in the order. A hidden root that was showing gives way to
    /// the tab's first visible root.
    /// </summary>
    public bool SetVisible(PanelTab tab, string rootKey, bool visible)
    {
        if (!_roots.TryGetValue(tab, out var all) || !all.Any(root => root.Key == rootKey))
        {
            return false;
        }

        if (visible ? !_hidden.Remove(rootKey) : !_hidden.Add(rootKey))
        {
            return false;
        }

        if (!visible)
        {
            _trails[rootKey] = [all.First(root => root.Key == rootKey)];

            if (_current.TryGetValue(tab, out var showing) && showing == rootKey)
            {
                var next = Roots(tab);

                if (next.Count > 0)
                {
                    _current[tab] = next[0].Key;
                }
                else
                {
                    _current.Remove(tab);
                }
            }
        }

        Raise();
        return true;
    }

    /// <summary>Whether a tab has anything behind it.</summary>
    public bool Has(PanelTab tab) => Roots(tab).Count > 0;

    /// <summary>
    /// Declares a page one level below a registered root as a destination of its own (#1149).
    /// <paramref name="canOpen"/> is asked each time it is shown; a page it refuses stays listed. Refused when
    /// the root is not registered or the key is already a root's or a sub-page's.
    /// </summary>
    public bool Declare(string rootKey, NavCrumb page, Func<bool>? canOpen = null)
    {
        if (!_trails.ContainsKey(rootKey) || _trails.ContainsKey(page.Key) || !_subPageKeys.Add(page.Key))
        {
            return false;
        }

        if (!_subPages.TryGetValue(rootKey, out var pages))
        {
            pages = [];
            _subPages[rootKey] = pages;
        }

        pages.Add(new SubPage(page, canOpen));
        Raise();
        return true;
    }

    /// <summary>Every root of every tab this surface furnished, each followed by its sub-pages, in bar order (Phase 46).</summary>
    public IReadOnlyList<PanelDestination> Destinations =>
    [
        .. Enum.GetValues<PanelTab>().SelectMany(tab => Roots(tab).SelectMany(root => (PanelDestination[])
        [
            new PanelDestination(tab, root),
            .. SubPagesOf(root.Key).Select(sub => new PanelDestination(tab, root, sub.Page)),
        ])),
    ];

    /// <summary>
    /// The destination this surface is on: a declared sub-page when the trail is its root and it alone,
    /// else the root.
    /// </summary>
    public string DestinationKey
    {
        get
        {
            var trail = Trail;

            return trail.Count == 2 && SubPagesOf(trail[0].Key).Any(sub => sub.Page.Key == trail[1].Key)
                ? trail[1].Key
                : Root.Key;
        }
    }

    /// <summary>Whether <see cref="Show(string)"/> would find this key: a visible root, or a sub-page that can open.</summary>
    public bool CanShow(string key) =>
        _roots.Keys.Any(tab => Roots(tab).Any(root =>
            root.Key == key || SubPagesOf(root.Key).Any(sub => sub.Page.Key == key && sub.CanOpen?.Invoke() != false)));

    /// <summary>
    /// Puts this surface on the destination with this key, whichever tab it belongs to — the tab's mode
    /// first and then the tab, so a destination on another tab arrives without a visible flick through that
    /// tab's previous mode. A sub-page leaves the trail at its root and it, and is refused when its page
    /// cannot be opened.
    /// </summary>
    public bool Show(string key)
    {
        foreach (var tab in _roots.Keys)
        {
            foreach (var root in Roots(tab))
            {
                if (root.Key == key)
                {
                    var moved = SelectRoot(tab, key);

                    return Select(tab) || moved;
                }

                if (SubPagesOf(root.Key).FirstOrDefault(sub => sub.Page.Key == key) is { } page)
                {
                    return Show(tab, root, page);
                }
            }
        }

        return false;
    }

    private bool Show(PanelTab tab, NavCrumb root, SubPage page)
    {
        var trail = _trails[root.Key];

        if (Modal || trail[^1].Modal || page.CanOpen?.Invoke() == false)
        {
            return false;
        }

        // The trail is set before the root and the tab are selected, so the root's old trail is never drawn.
        var drilled = trail.Count != 2 || trail[1].Key != page.Page.Key;

        if (drilled)
        {
            trail.RemoveRange(1, trail.Count - 1);
            trail.Add(page.Page);
        }

        var moved = SelectRoot(tab, root.Key);
        var selected = Select(tab);

        if (drilled && !moved && !selected)
        {
            Raise();
        }

        return drilled || moved || selected;
    }

    private IReadOnlyList<SubPage> SubPagesOf(string rootKey) =>
        _subPages.TryGetValue(rootKey, out var pages) ? pages : [];

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
    public IReadOnlyList<NavCrumb> Trail => TrailOf(Tab);

    /// <summary>The trail a tab is on, whether or not it is the tab showing.</summary>
    public IReadOnlyList<NavCrumb> TrailOf(PanelTab tab) => (IReadOnlyList<NavCrumb>?)Mutable(tab) ?? [];

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

    /// <summary>
    /// Shows the next tab along the bar (<paramref name="direction"/> 1) or the previous one (-1), wrapping and
    /// skipping tabs with nothing registered.
    /// </summary>
    public bool Step(int direction)
    {
        var tabs = Enum.GetValues<PanelTab>();
        var at = Array.IndexOf(tabs, Tab);

        for (var distance = 1; distance < tabs.Length; distance++)
        {
            var tab = tabs[(((at + (direction * distance)) % tabs.Length) + tabs.Length) % tabs.Length];

            if (Has(tab))
            {
                return Select(tab);
            }
        }

        return false;
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

    /// <summary>One level deeper on a tab's current root, whether or not that tab is showing.</summary>
    public bool Drill(PanelTab tab, NavCrumb crumb) =>
        Mutable(tab) is { } trail && !trail[^1].Modal && Push(trail, crumb);

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

    /// <summary>Cuts a tab's trail back to its root when any level below the root matches, unless the tab is waiting on a chooser.</summary>
    public bool ToRootIfAny(PanelTab tab, Func<NavCrumb, bool> match)
    {
        var trail = Mutable(tab);

        if (trail is null || trail[^1].Modal || trail.FindIndex(1, crumb => match(crumb)) < 0)
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
    public bool GoTo(IReadOnlyList<NavCrumb> crumbs) => GoTo(Tab, crumbs);

    /// <summary>Puts a tab's current root at the end of a trail, whether or not that tab is showing.</summary>
    public bool GoTo(PanelTab tab, params NavCrumb[] crumbs) => GoTo(tab, (IReadOnlyList<NavCrumb>)crumbs);

    /// <inheritdoc cref="GoTo(PanelTab, NavCrumb[])"/>
    public bool GoTo(PanelTab tab, IReadOnlyList<NavCrumb> crumbs)
    {
        var trail = Mutable(tab);

        if (trail is null || trail[^1].Modal)
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

    private bool Push(NavCrumb crumb) => Push(Mutable(), crumb);

    private bool Push(List<NavCrumb>? trail, NavCrumb crumb)
    {
        if (trail is null)
        {
            return false;
        }

        // The level already on top is this one again — a second plot of the same kind, say (#212). Refusing
        // outright would leave the breadcrumb reading the plan that was replaced, so the crumb is swapped in
        // place rather than pushed; an identical crumb is still a no-op.
        if (trail[^1].Key == crumb.Key)
        {
            if (trail[^1] == crumb)
            {
                return false;
            }

            trail[^1] = crumb;
            Raise();
            return true;
        }

        // A key already lower in the trail is returned to, not stacked again: a drill strip cannot show one
        // level's page in two panes.
        var earlier = trail.FindIndex(open => open.Key == crumb.Key);

        if (earlier >= 0)
        {
            trail.RemoveRange(earlier + 1, trail.Count - earlier - 1);
            trail[earlier] = crumb;
            Raise();
            return true;
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

    private List<NavCrumb>? Mutable() => Mutable(Tab);

    private List<NavCrumb>? Mutable(PanelTab tab) =>
        _current.TryGetValue(tab, out var root) && _trails.TryGetValue(root, out var trail)
            ? trail
            : null;

    private void Raise() => Changed?.Invoke(this, EventArgs.Empty);

    private sealed record SubPage(NavCrumb Page, Func<bool>? CanOpen);
}
