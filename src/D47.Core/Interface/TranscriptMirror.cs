namespace D47.Core.Interface;

/// <summary>One transcript page across every surface (Phase 45, "One transcript, both surfaces").</summary>
public sealed class TranscriptMirror
{
    private readonly List<PanelNavigator> _navigators = [];

    /// <summary>The transcript root each navigator was last known to be on.</summary>
    private readonly Dictionary<PanelNavigator, string> _seen = [];

    /// <summary>
    /// The tab each navigator was last known to be on, which is to the tab half what <see
    /// cref="_seen"/> is to the transcript half: the way a move made by this surface is told apart from
    /// one it was given.
    /// </summary>
    private readonly Dictionary<PanelNavigator, PanelTab> _tabs = [];

    /// <summary>
    /// The navigator whose tab the others follow, or null where nobody leads and the tab half is simply
    /// off.
    /// </summary>
    private PanelNavigator? _leader;

    /// <summary>Set while a move of this mirror's own making is raising <c>Changed</c>.</summary>
    private bool _mirroring;

    /// <summary>The root every surface is reading, or null before the first navigator is added.</summary>
    public string? Root { get; private set; }

    /// <summary>Brings a navigator into the mirror.</summary>
    public void Add(PanelNavigator nav)
    {
        if (_navigators.Contains(nav))
        {
            return;
        }

        _navigators.Add(nav);
        _seen[nav] = nav.RootKeyOf(PanelTab.Transcript);
        _tabs[nav] = nav.Tab;

        if (Root is null)
        {
            Root = _seen[nav];
        }
        else
        {
            Mirroring(() => CatchUp(nav));
        }

        nav.Changed += (_, _) => OnChanged(nav);
    }

    /// <summary>Names the navigator the others follow — the window's (change-requests.md 34).</summary>
    public void Lead(PanelNavigator nav)
    {
        Add(nav);
        _leader = nav;
    }

    private void OnChanged(PanelNavigator nav)
    {
        // The echo: a move this mirror made, announcing itself.
        if (_mirroring)
        {
            return;
        }

        Led(nav);

        var root = nav.RootKeyOf(PanelTab.Transcript);

        if (root != _seen[nav])
        {
            // This surface moved the transcript — from its mode control, its menu, a phrase applied to it or
            // a switch.
            _seen[nav] = root;
            Root = root;

            Mirroring(() =>
            {
                foreach (var other in _navigators)
                {
                    if (!ReferenceEquals(other, nav))
                    {
                        CatchUp(other);
                    }
                }
            });
        }
        else if (root != Root)
        {
            // This surface is behind: a chooser held it when the others moved, and whatever it just did —
            // most likely dismissing that chooser — is a chance to bring it level.
            Mirroring(() => CatchUp(nav));
        }
    }

    /// <summary>
    /// The tab half: where the leader moved, the followers go, and where anybody else moved, nothing
    /// happens.
    /// </summary>
    private void Led(PanelNavigator nav)
    {
        var tab = nav.Tab;

        if (_tabs.TryGetValue(nav, out var was) && was == tab)
        {
            return;
        }

        _tabs[nav] = tab;

        if (!ReferenceEquals(nav, _leader))
        {
            return;
        }

        // The view of the tab as well as the tab, which is what was asked for: switching the window to a tab
        // it is already on and changing only the root still carries.
        var root = nav.RootKeyOf(tab);

        Mirroring(() =>
        {
            foreach (var other in _navigators.Where(other => !ReferenceEquals(other, nav)))
            {
                // Declined outright by a surface that never furnished this tab, which is the Commander's IFF
                // and costs no special case.
                other.Select(tab);
                other.SelectRoot(tab, root);

                _tabs[other] = other.Tab;
            }
        });
    }

    /// <summary>Puts one navigator on the shared root, and records it only if the move was taken.</summary>
    private void CatchUp(PanelNavigator nav)
    {
        if (Root is { } root && nav.SelectRoot(PanelTab.Transcript, root))
        {
            _seen[nav] = root;
        }
    }

    private void Mirroring(Action move)
    {
        _mirroring = true;

        try
        {
            move();
        }
        finally
        {
            _mirroring = false;
        }
    }
}
