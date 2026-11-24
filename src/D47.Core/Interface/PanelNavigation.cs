namespace D47.Core.Interface;

/// <summary>The panel's surfaces, as the bar shows them (Phase 25, "One transcript, three views").</summary>
public enum PanelTab
{
    /// <summary>The conversation, at two verbosities, and today's log file.</summary>
    Transcript,

    /// <summary>Looking things up: the star system page first, more kinds of search after it.</summary>
    Search,

    /// <summary>The Commander's own record: the checklist, the assets, the ledgers and the rest (#805).</summary>
    Commander,

    /// <summary>
    /// Where the Commander is going: Plan with the route being flown beside it, bookmarks, markets and
    /// trade routes.
    /// </summary>
    Astrometrics,

    /// <summary>Stories the Commander flies, told by the ship's AI (Phase 47).</summary>
    Stories,

    /// <summary>Music, podcasts, books and movies (#2651).</summary>
    Media,

    /// <summary>The installed modules and the pages they draw; furnished only when a module is installed (#2083).</summary>
    Modules,

    /// <summary>The settings surface.</summary>
    Settings,
}

/// <summary>Reads a tab's saved name, including the names of tabs since renamed.</summary>
public static class PanelTabNames
{
    /// <summary>The key of Commander › Assets, the sub-tab whose pages are the ships, the carrier, the materials and the rest.</summary>
    public const string AssetsRoot = "assets";

    private static readonly Dictionary<string, (PanelTab Tab, string? Root)> Former = new(StringComparer.Ordinal)
    {
        ["Adventures"] = (PanelTab.Stories, null),
        ["Assets"] = (PanelTab.Commander, AssetsRoot),
        ["Checklist"] = (PanelTab.Commander, null),
        ["Loadout"] = (PanelTab.Commander, AssetsRoot),
        ["Navigation"] = (PanelTab.Astrometrics, null),
        ["Routing"] = (PanelTab.Astrometrics, null),
    };

    /// <summary>The tab a saved name means, or null for a name that is no tab.</summary>
    public static PanelTab? Parse(string? name) =>
        name is null ? null
        : Former.TryGetValue(name, out var renamed) ? renamed.Tab
        : Enum.TryParse<PanelTab>(name, out var tab) && Enum.IsDefined(tab) && tab.ToString() == name ? tab
        : null;

    /// <summary>The root a former tab's saved name now opens, or null where the name means a tab and no root.</summary>
    public static string? RootOf(string? name) =>
        name is not null && Former.TryGetValue(name, out var renamed) ? renamed.Root : null;
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

    /// <summary>Whether voice matches only <see cref="Spoken"/>, not the drawn word.</summary>
    public bool SpokenOnly { get; init; }

    /// <summary>Whether this level takes the whole strip rather than sharing it with the levels above (#469).</summary>
    public bool Whole { get; init; }

    /// <summary>Whether the levels under this one share the strip with it but not with the levels above it.</summary>
    public bool Starts { get; init; }

    /// <summary>Whether this level stays on the surface that opened it rather than being carried to the surfaces following it.</summary>
    public bool Local { get; init; }
}

/// <summary>
/// One place a surface can be sent to: a root and the tab it is a root of, or a fixed sub-page of that root
/// (Phase 46, #1149).
/// </summary>
public sealed record PanelDestination(PanelTab Tab, NavCrumb Root, NavCrumb? SubPage = null, NavCrumb? Parent = null)
{
    /// <summary>The page a sub-page sits under: its parent page where it has one, else the root.</summary>
    public NavCrumb Above => Parent ?? Root;

    /// <summary>The key a switch position stores: the sub-page's where there is one, else the root's.</summary>
    public string Key => SubPage?.Key ?? Root.Key;

    /// <summary>What the Commander calls it.</summary>
    public string Word => SubPage?.Word ?? Root.Word;

    /// <summary>The level whose word and spoken aliases name it.</summary>
    public NavCrumb Page => SubPage ?? Root;

    /// <summary>
    /// How it reads in a list of every destination: the root's word, and the tab when the word alone
    /// would not say which tab — "Technical (Transcript)", but "Stories" rather than "Stories
    /// (Stories)". A sub-page reads with the page above it: "Catalogue (Stories)".
    /// </summary>
    public string Describe() =>
        SubPage is { } page ? $"{page.Word} ({Above.Word})"
        : string.Equals(Root.Word, Tab.ToString(), StringComparison.OrdinalIgnoreCase) ? Root.Word
        : $"{Root.Word} ({Tab})";
}

/// <summary>
/// A place on another surface: its tab, its trail, the destination it is, and how many roots its tab has there.
/// Trails compare by their crumbs.
/// </summary>
public sealed record PanelAddress(PanelTab Tab, IReadOnlyList<NavCrumb> Trail, string DestinationKey, int RootCount)
{
    public bool Equals(PanelAddress? other) =>
        other is not null
        && Tab == other.Tab
        && DestinationKey == other.DestinationKey
        && RootCount == other.RootCount
        && Trail.SequenceEqual(other.Trail);

    public override int GetHashCode() => HashCode.Combine(Tab, DestinationKey, Trail.Count);
}

/// <summary>
/// Where the Commander is, per tab and per root, and every way of changing it (Phase 25, "Drill in, and
/// find your way back").
/// </summary>
public sealed class PanelNavigator
{
    /// <summary>
    /// The address this surface shows in place of a page it cannot draw, or null. Set by <see cref="TranscriptMirror"/>;
    /// a move of this surface's own on the tab it stands in for clears it.
    /// </summary>
    public PanelAddress? StandingIn { get; private set; }

    /// <summary>Stands in for an address, or stops with null; false when that is already so.</summary>
    public bool StandIn(PanelAddress? address)
    {
        if (Equals(StandingIn, address))
        {
            return false;
        }

        StandingIn = address;
        Raise();
        return true;
    }

    /// <summary>Stops standing in after a move: any move with no tab, or a move on the showing tab that the stand-in is on.</summary>
    private void Moved(PanelTab? tab = null)
    {
        if (StandingIn is { } address && (tab is not { } on || (on == Tab && address.Tab == on)))
        {
            StandingIn = null;
        }
    }

    /// <summary>The roots each tab offers, in the order the mode control shows them.</summary>
    private readonly Dictionary<PanelTab, List<NavCrumb>> _roots = [];

    /// <summary>One trail per root, keyed by the root's key.</summary>
    private readonly Dictionary<string, List<NavCrumb>> _trails = [];

    /// <summary>Which root each tab was last on, so a tab switch returns to the mode it left.</summary>
    private readonly Dictionary<PanelTab, string> _current = [];

    /// <summary>The places before this one, oldest first; where the panel is now is not among them.</summary>
    private readonly List<Visit> _back = [];

    /// <summary>The places <see cref="Previous"/> left, newest last; any recorded move clears it.</summary>
    private readonly List<Visit> _forward = [];

    /// <summary>The most places <see cref="Previous"/> can go back through.</summary>
    private const int MostRemembered = 100;

    /// <summary>Which place each tab is showing, for the tabs that have places inside them.</summary>
    private readonly Dictionary<PanelTab, string> _places = [];

    /// <summary>Raised whenever the tab, the root or any trail changes.</summary>
    public event EventHandler? Changed;

    /// <summary>Raised after <see cref="Previous"/> or <see cref="Next"/> returns to a place inside a tab, with that tab and the place's id.</summary>
    public event Action<PanelTab, string>? PlaceRestored;

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

    /// <summary>The order each tab's roots are listed in, where one was set; roots it does not name follow in registration order.</summary>
    private readonly Dictionary<PanelTab, List<string>> _order = [];

    /// <summary>The roots a tab offers, without the hidden ones, in its order where one was set.</summary>
    public IReadOnlyList<NavCrumb> Roots(PanelTab tab)
    {
        if (!_roots.TryGetValue(tab, out var roots))
        {
            return [];
        }

        var visible = roots.Where(root => !_hidden.Contains(root.Key));

        if (_order.TryGetValue(tab, out var order))
        {
            visible = visible.OrderBy(root => order.IndexOf(root.Key) is var at and >= 0 ? at : int.MaxValue);
        }

        return [.. visible];
    }

    /// <summary>Sets the order a tab's roots are listed in, whenever they are registered.</summary>
    public void Order(PanelTab tab, IReadOnlyList<string> keys)
    {
        _order[tab] = [.. keys];
        Raise();
    }

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

            Forget(visit => visit.RootKey == rootKey);
        }

        Raise();
        return true;
    }

    /// <summary>Whether a tab has anything behind it.</summary>
    public bool Has(PanelTab tab) => Roots(tab).Count > 0;

    /// <summary>
    /// Declares a page one level below a registered root, or one level below a page declared under a root, as a
    /// destination of its own (#1149). <paramref name="canOpen"/> is asked each time it is shown; a page it refuses
    /// stays listed. Refused when the root or page above is not declared or the key is already a root's or a
    /// sub-page's.
    /// </summary>
    public bool Declare(string aboveKey, NavCrumb page, Func<bool>? canOpen = null)
    {
        (string Root, SubPage Sub)? under = null;

        if (!_trails.ContainsKey(aboveKey))
        {
            foreach (var (root, subs) in _subPages)
            {
                if (subs.FirstOrDefault(sub => sub.Page.Key == aboveKey && sub.Under is null) is { } above)
                {
                    under = (root, above);
                    break;
                }
            }

            if (under is null)
            {
                return false;
            }
        }

        if (_trails.ContainsKey(page.Key) || !_subPageKeys.Add(page.Key))
        {
            return false;
        }

        var rootKey = under?.Root ?? aboveKey;

        if (!_subPages.TryGetValue(rootKey, out var pages))
        {
            pages = [];
            _subPages[rootKey] = pages;
        }

        pages.Add(new SubPage(page, canOpen, under?.Sub.Page));
        Raise();
        return true;
    }

    /// <summary>The roots that hold pages: the order their pages are listed in.</summary>
    private readonly Dictionary<string, IReadOnlyList<string>> _holdsPages = new(StringComparer.Ordinal);

    /// <summary>The page each page-holding root was last on.</summary>
    private readonly Dictionary<string, string> _lastPage = new(StringComparer.Ordinal);

    /// <summary>
    /// Makes a registered root one that holds its declared sub-pages as a page sidebar: it is never on its own,
    /// opening on the page last open or else the first of <paramref name="order"/> that can open, and
    /// <see cref="PagesOf"/> lists them in that order, any others after.
    /// </summary>
    public bool HoldPages(string rootKey, IReadOnlyList<string> order)
    {
        if (!_trails.ContainsKey(rootKey))
        {
            return false;
        }

        _holdsPages[rootKey] = order;
        Raise();
        return true;
    }

    /// <summary>Whether this root holds its sub-pages as a page sidebar.</summary>
    public bool HoldsPages(string rootKey) => _holdsPages.ContainsKey(rootKey);

    /// <summary>The pages a page-holding root can open now, in its order; empty for any other root.</summary>
    public IReadOnlyList<NavCrumb> PagesOf(string rootKey)
    {
        if (!_holdsPages.TryGetValue(rootKey, out var order))
        {
            return [];
        }

        var open = SubPagesOf(rootKey).Where(sub => sub.Under is null && sub.CanOpen?.Invoke() != false).Select(sub => sub.Page).ToList();

        return
        [
            .. order.SelectMany(key => open.Where(page => page.Key == key)),
            .. open.Where(page => !order.Contains(page.Key)),
        ];
    }

    /// <summary>
    /// Opens a page-holding root on its last page, or its first, when nothing is open on it, and leaves a page that
    /// can no longer open for that one.
    /// </summary>
    private void Land()
    {
        foreach (var (tab, rootKey) in _current)
        {
            if (!_holdsPages.ContainsKey(rootKey) || _hidden.Contains(rootKey) || !_trails.TryGetValue(rootKey, out var trail))
            {
                continue;
            }

            if (trail.Count >= 2 && SubPagesOf(rootKey).FirstOrDefault(sub => sub.Page.Key == trail[1].Key) is { } open)
            {
                if (open.CanOpen?.Invoke() != false)
                {
                    _lastPage[rootKey] = trail[1].Key;
                    continue;
                }

                trail.RemoveRange(1, trail.Count - 1);
            }

            if (trail.Count >= 2)
            {
                continue;
            }

            if (Landing(rootKey) is { } landing && tab == Tab)
            {
                trail.Add(landing);
            }
        }
    }

    /// <summary>The page a page-holding root opens on: the page last open, or the first that can open.</summary>
    private NavCrumb? Landing(string rootKey)
    {
        var pages = PagesOf(rootKey);

        return pages.FirstOrDefault(page => page.Key == _lastPage.GetValueOrDefault(rootKey)) ?? pages.FirstOrDefault();
    }

    /// <summary>Re-asks every page whether it can open, after something it depends on has changed, and redraws.</summary>
    public void Refresh() => Raise();

    /// <summary>The page a tab is on: the open page of a page-holding root, else the root; empty for a tab with no root.</summary>
    public string PageKeyOf(PanelTab tab) =>
        TrailOf(tab) is [var root, var page, ..] && _holdsPages.ContainsKey(root.Key) ? page.Key : RootKeyOf(tab);

    /// <summary>The fewest levels the trail keeps: a page-holding root always has a page open.</summary>
    private int Floor(List<NavCrumb> trail) =>
        _holdsPages.ContainsKey(trail[0].Key) && PagesOf(trail[0].Key).Count > 0 ? 2 : 1;

    /// <summary>Every root of every tab this surface furnished, each followed by its sub-pages, in bar order (Phase 46).</summary>
    public IReadOnlyList<PanelDestination> Destinations =>
    [
        .. Enum.GetValues<PanelTab>().SelectMany(tab => Roots(tab).SelectMany(root => (PanelDestination[])
        [
            new PanelDestination(tab, root),
            .. SubPagesOf(root.Key).Where(sub => sub.Under is null).SelectMany(sub => (PanelDestination[])
            [
                new PanelDestination(tab, root, sub.Page),
                .. SubPagesOf(root.Key)
                    .Where(nested => nested.Under?.Key == sub.Page.Key)
                    .Select(nested => new PanelDestination(tab, root, nested.Page, sub.Page)),
            ]),
        ])),
    ];

    /// <summary>
    /// The destination this surface is on: a declared sub-page when the trail is its root and it alone, or its root,
    /// the page above it and it; under a page-holding root, the page open at any depth; else the root. While standing
    /// in, the address's.
    /// </summary>
    public string DestinationKey => StandingIn?.DestinationKey ?? OwnDestinationKey;

    /// <summary>The destination this surface's own trail is on, whether or not it stands in for another.</summary>
    internal string OwnDestinationKey
    {
        get
        {
            var trail = Trail;

            if (trail.Count < 2 || !SubPagesOf(trail[0].Key).Any(sub => sub.Page.Key == trail[1].Key && sub.Under is null))
            {
                return Root.Key;
            }

            if (trail.Count == 2)
            {
                return trail[1].Key;
            }

            if (trail.Count == 3 && SubPagesOf(trail[0].Key).Any(sub => sub.Page.Key == trail[2].Key && sub.Under?.Key == trail[1].Key))
            {
                return trail[2].Key;
            }

            return _holdsPages.ContainsKey(trail[0].Key) ? trail[1].Key : Root.Key;
        }
    }

    /// <summary>Whether <see cref="Show(string)"/> would find this key: a visible root, or a sub-page that can open.</summary>
    public bool CanShow(string key) =>
        _roots.Keys.Any(tab => Roots(tab).Any(root =>
            root.Key == key || SubPagesOf(root.Key).Any(sub => sub.Page.Key == key && Opens(root.Key, sub))));

    /// <summary>Whether a sub-page can open, and the page above it where it has one.</summary>
    private bool Opens(string rootKey, SubPage sub) =>
        sub.CanOpen?.Invoke() != false
        && (sub.Under is not { } under
            || SubPagesOf(rootKey).FirstOrDefault(above => above.Page.Key == under.Key)?.CanOpen?.Invoke() != false);

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

        if (Modal || trail[^1].Modal || !Opens(root.Key, page))
        {
            return false;
        }

        var before = Here();
        NavCrumb[] levels = page.Under is { } under ? [under, page.Page] : [page.Page];

        // The trail is set before the root and the tab are selected, so the root's old trail is never drawn.
        var drilled = !trail.Skip(1).Select(crumb => crumb.Key).SequenceEqual(levels.Select(level => level.Key), StringComparer.Ordinal);

        if (drilled)
        {
            trail.RemoveRange(1, trail.Count - 1);
            trail.AddRange(levels);
        }

        var moved = SelectRoot(tab, root.Key);
        var selected = Select(tab);

        if (drilled && !moved && !selected)
        {
            Remember(before);
            Moved(tab);
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

    /// <summary>The level that shows the sub-tab row: the root, or one page of a page-holding root.</summary>
    public bool AtPage => Trail.Count <= (Mutable() is { } trail ? Floor(trail) : 1);

    /// <summary>A chooser is holding the panel.</summary>
    public bool Modal => Trail.Count > 0 && Trail[^1].Modal;

    /// <summary>Shows a tab, restoring the root and the trail it was left on.</summary>
    public bool Select(PanelTab tab)
    {
        if (Modal || tab == Tab || !Has(tab))
        {
            return false;
        }

        var before = Here();

        Tab = tab;
        Remember(before);
        Moved();
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

        var before = Here();

        _current[tab] = rootKey;
        Remember(before);
        Moved(tab);
        Raise();
        return true;
    }

    /// <summary>Which root a tab is on, whether or not it is the tab showing.</summary>
    public string RootKeyOf(PanelTab tab) =>
        _current.TryGetValue(tab, out var root) ? root : string.Empty;

    /// <summary>One level deeper.</summary>
    public bool Drill(NavCrumb crumb) => !Modal && Push(Tab, crumb, remember: true);

    /// <summary>One level deeper on a tab's current root, whether or not that tab is showing.</summary>
    public bool Drill(PanelTab tab, NavCrumb crumb) =>
        Mutable(tab) is { } trail && !trail[^1].Modal && Push(tab, crumb, remember: true);

    /// <summary>Pushes a level that holds the panel until it is dismissed - a chooser.</summary>
    public bool Take(NavCrumb crumb) => Push(Tab, crumb with { Modal = true }, remember: false);

    /// <summary>
    /// Back one level, and the answer to all three routes that must agree - the breadcrumb, the
    /// controller button and the phrase.
    /// </summary>
    public bool Back()
    {
        var trail = Mutable();

        if (trail is null || trail.Count <= Floor(trail))
        {
            return false;
        }

        trail.RemoveAt(trail.Count - 1);
        Settle();
        Moved(Tab);
        Raise();
        return true;
    }

    /// <summary>
    /// All the way back to the root of the current tab - what pressing an already-selected tab does.
    /// </summary>
    public bool ToRoot()
    {
        var trail = Mutable();

        if (trail is null || trail.Count <= Floor(trail))
        {
            return false;
        }

        trail.RemoveRange(Floor(trail), trail.Count - Floor(trail));
        Settle();
        Moved(Tab);
        Raise();
        return true;
    }

    /// <summary>
    /// Cuts a tab's trail back to its root when any level below the root matches, unless the tab is waiting on a
    /// chooser, and forgets every remembered place on the tab with a matching level or on the root with a level it cut.
    /// </summary>
    public bool ToRootIfAny(PanelTab tab, Func<NavCrumb, bool> match)
    {
        var trail = Mutable(tab);

        if (trail is null || trail[^1].Modal)
        {
            return false;
        }

        var root = trail[0].Key;
        HashSet<string> removed = trail.FindIndex(1, crumb => match(crumb)) < 0
            ? []
            : new(trail.Skip(1).Select(crumb => crumb.Key), StringComparer.Ordinal);

        if (removed.Count > 0)
        {
            trail.RemoveRange(1, trail.Count - 1);
        }

        Forget(visit =>
            (visit.Tab == tab && visit.Trail.Skip(1).Any(match))
            || (visit.RootKey == root && visit.Trail.Any(crumb => removed.Contains(crumb.Key))));

        if (removed.Count == 0)
        {
            return false;
        }

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
        Settle();
        Moved(Tab);
        Raise();
        return true;
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
        var before = Here();

        // A caller that supplies the root anyway is not given it twice.
        var below = crumbs.Count > 0 && crumbs[0].Key == root.Key ? crumbs.Skip(1).ToList() : [.. crumbs];

        // A page-holding root keeps its page open unless the crumbs start at one of its pages.
        var page = !_holdsPages.ContainsKey(root.Key)
            || (below.Count > 0 && SubPagesOf(root.Key).Any(sub => sub.Under is null && sub.Page.Key == below[0].Key))
            ? null
            : trail.Count >= 2 ? trail[1] : Landing(root.Key);

        trail.Clear();
        trail.Add(root);

        if (page is not null)
        {
            trail.Add(page);
        }

        trail.AddRange(below);

        Remember(before);
        Moved(tab);
        Raise();
        return true;
    }

    /// <summary>
    /// Back through the places this navigator has shown, skipping any that can no longer be opened; false when
    /// there is none to go back to or a chooser is holding the panel.
    /// </summary>
    public bool Previous()
    {
        if (Modal || Older() is not { } at)
        {
            return false;
        }

        var visit = _back[at];
        var left = Here();

        _back.RemoveRange(at, _back.Count - at);

        if (left is not null)
        {
            _forward.Add(left);
        }

        Restore(visit);
        Settle();
        Moved();
        Raise();
        RaisePlace(visit);
        return true;
    }

    /// <summary>
    /// Forward again through the places <see cref="Previous"/> left, skipping any that can no longer be opened; false
    /// when there is none or a chooser is holding the panel.
    /// </summary>
    public bool Next()
    {
        if (Modal || Newer() is not { } at)
        {
            return false;
        }

        var visit = _forward[at];
        var left = Here();

        _forward.RemoveRange(at, _forward.Count - at);

        if (left is not null && !(_back.Count > 0 && _back[^1].Same(left)))
        {
            _back.Add(left);
        }

        Restore(visit);
        Moved();
        Raise();
        RaisePlace(visit);
        return true;
    }

    /// <summary>Sets the trail, root, tab and place before the one Raise, so no place in between is drawn.</summary>
    private void Restore(Visit visit)
    {
        var trail = _trails[visit.RootKey];

        trail.Clear();
        trail.AddRange(visit.Trail);
        _current[visit.Tab] = visit.RootKey;
        Tab = visit.Tab;

        if (visit.Place is { } place)
        {
            _places[visit.Tab] = place;
        }
    }

    private void RaisePlace(Visit visit)
    {
        if (visit.Place is { } restored)
        {
            PlaceRestored?.Invoke(visit.Tab, restored);
        }
    }

    /// <summary>
    /// Says which place inside a tab is showing. A change from a place already showing is a move
    /// <see cref="Previous"/> can reverse; the first place, or <paramref name="record"/> false, only notes where
    /// the tab is.
    /// </summary>
    public bool SetPlace(PanelTab tab, string placeId, bool record = true)
    {
        if (_places.TryGetValue(tab, out var showing) && showing == placeId)
        {
            return false;
        }

        var before = record && showing is not null ? Here() : null;

        _places[tab] = placeId;
        Remember(before);
        return true;
    }

    /// <summary>Whether <see cref="Previous"/> has a place to go back to.</summary>
    public bool CanGoPrevious => !Modal && Older() is not null;

    /// <summary>Whether <see cref="Next"/> has a place to go forward to.</summary>
    public bool CanGoNext => !Modal && Newer() is not null;

    /// <summary>The newest place <see cref="Previous"/> left that differs from this one and can still be opened, or null.</summary>
    private int? Newer()
    {
        var here = Here();

        for (var at = _forward.Count - 1; at >= 0; at--)
        {
            if (!_forward[at].Same(here) && CanRestore(_forward[at]))
            {
                return at;
            }
        }

        return null;
    }

    /// <summary>The newest remembered place that differs from this one and can still be opened, or null.</summary>
    private int? Older()
    {
        var here = Here();

        for (var at = _back.Count - 1; at >= 0; at--)
        {
            if (!_back[at].Same(here) && CanRestore(_back[at]))
            {
                return at;
            }
        }

        return null;
    }

    private bool CanRestore(Visit visit) =>
        Roots(visit.Tab).Any(root => root.Key == visit.RootKey)
        && visit.Trail.Skip(1).All(crumb =>
            SubPagesOf(visit.RootKey).FirstOrDefault(sub => sub.Page.Key == crumb.Key) is not { } sub
            || Opens(visit.RootKey, sub));

    /// <summary>Where the panel is now, without a chooser, or null when the showing tab has no root.</summary>
    private Visit? Here() =>
        Mutable() is { } trail
            ? new Visit(Tab, trail[0].Key, [.. trail.Where(crumb => !crumb.Modal)], _places.GetValueOrDefault(Tab))
            : null;

    /// <summary>Records a move away from <paramref name="before"/>; a move that left the place as it was records nothing.</summary>
    private void Remember(Visit? before)
    {
        if (before is null || before.Same(Here()) || (_back.Count > 0 && _back[^1].Same(before)))
        {
            return;
        }

        _back.Add(before);
        _forward.Clear();

        if (_back.Count > MostRemembered)
        {
            _back.RemoveAt(0);
        }
    }

    /// <summary>Treats the move as replacing the place before it: drops the last remembered place when it is where the panel is now.</summary>
    private void Settle()
    {
        if (_back.Count > 0 && _back[^1].Same(Here()))
        {
            _back.RemoveAt(_back.Count - 1);
        }
    }

    /// <summary>Drops the remembered places that match, then joins neighbours left equal.</summary>
    private void Forget(Func<Visit, bool> match)
    {
        _back.RemoveAll(visit => match(visit));
        _forward.RemoveAll(visit => match(visit));

        for (var at = _back.Count - 1; at > 0; at--)
        {
            if (_back[at].Same(_back[at - 1]))
            {
                _back.RemoveAt(at);
            }
        }

        Settle();
    }

    private bool Push(PanelTab tab, NavCrumb crumb, bool remember)
    {
        var trail = Mutable(tab);

        if (trail is null)
        {
            return false;
        }

        var before = remember ? Here() : null;

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

            if (remember)
            {
                Settle();
            }

            Moved(tab);
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

            if (remember)
            {
                Settle();
            }

            Moved(tab);
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
        Remember(before);
        Moved(tab);
        Raise();
        return true;
    }

    private List<NavCrumb>? Mutable() => Mutable(Tab);

    private List<NavCrumb>? Mutable(PanelTab tab) =>
        _current.TryGetValue(tab, out var root) && _trails.TryGetValue(root, out var trail)
            ? trail
            : null;

    private void Raise()
    {
        Land();
        Changed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>A declared sub-page: directly under its root, or under the page <paramref name="Under"/>.</summary>
    private sealed record SubPage(NavCrumb Page, Func<bool>? CanOpen, NavCrumb? Under = null);


    /// <summary>One place the panel was: a tab, its root, that root's trail and the place inside the tab, if it has one.</summary>
    private sealed record Visit(PanelTab Tab, string RootKey, IReadOnlyList<NavCrumb> Trail, string? Place = null)
    {
        /// <summary>The same place by keys, whatever the crumbs' words.</summary>
        public bool Same(Visit? other) =>
            other is not null
            && other.Tab == Tab
            && other.RootKey == RootKey
            && other.Place == Place
            && other.Trail.Select(crumb => crumb.Key).SequenceEqual(Trail.Select(crumb => crumb.Key), StringComparer.Ordinal);
    }
}
