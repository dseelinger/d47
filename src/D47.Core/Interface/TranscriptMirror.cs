namespace D47.Core.Interface;

/// <summary>One transcript page across every surface (Phase 45, "One transcript, both surfaces").</summary>
public sealed class TranscriptMirror
{
    private readonly List<PanelNavigator> _navigators = [];

    /// <summary>The transcript root each navigator was last known to be on.</summary>
    private readonly Dictionary<PanelNavigator, string> _seen = [];

    /// <summary>
    /// The navigator whose position the others follow, or null where nobody leads and the position half is
    /// simply off.
    /// </summary>
    private PanelNavigator? _leader;

    /// <summary>Where the leader was last known to be, so a move it made is told apart from one it was given.</summary>
    private Position? _led;

    /// <summary>Set while a move of this mirror's own making is raising <c>Changed</c>.</summary>
    private bool _mirroring;

    /// <summary>The root every surface is reading, or null before the first navigator is added.</summary>
    public string? Root { get; private set; }

    /// <summary>Brings a navigator into the mirror.</summary>
    public void Add(PanelNavigator nav)
    {
        if (!Join(nav))
        {
            return;
        }

        if (Root is null)
        {
            Root = _seen[nav];
        }
        else
        {
            Mirroring(() =>
            {
                CatchUp(nav);

                if (_leader is not null)
                {
                    Follow(nav, PositionOf(_leader));
                }
            });
        }
    }

    /// <summary>
    /// Names the navigator the others follow — the window's (change-requests.md 34) — and brings every
    /// navigator already added to where it is, its transcript reading included.
    /// </summary>
    public void Lead(PanelNavigator nav)
    {
        Join(nav);

        _leader = nav;
        _seen[nav] = nav.RootKeyOf(PanelTab.Transcript);
        Root = _seen[nav];

        var position = PositionOf(nav);

        Mirroring(() =>
        {
            foreach (var other in _navigators.Where(other => !ReferenceEquals(other, nav)))
            {
                CatchUp(other);
                Follow(other, position);
            }
        });
    }

    /// <summary>Starts listening to a navigator; false where it was already in the mirror.</summary>
    private bool Join(PanelNavigator nav)
    {
        if (_navigators.Contains(nav))
        {
            return false;
        }

        _navigators.Add(nav);
        _seen[nav] = nav.RootKeyOf(PanelTab.Transcript);
        nav.Changed += (_, _) => OnChanged(nav);

        return true;
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
    /// The position half: where the leader moved, the followers go, and where anybody else moved, nothing
    /// happens.
    /// </summary>
    private void Led(PanelNavigator nav)
    {
        if (!ReferenceEquals(nav, _leader))
        {
            return;
        }

        var position = PositionOf(nav);

        if (position == _led)
        {
            return;
        }

        Mirroring(() =>
        {
            foreach (var other in _navigators.Where(other => !ReferenceEquals(other, nav)))
            {
                Follow(other, position);
            }
        });
    }

    /// <summary>
    /// Puts a follower on the leader's tab, root and trail, as far down as it can go: a follower without
    /// the tab stays where it is, and one without the root stays on the tab's current root.
    /// </summary>
    private void Follow(PanelNavigator nav, Position position)
    {
        if (!nav.Has(position.Tab))
        {
            return;
        }

        // The root first and then the tab, as Show does, so the tab does not open on its previous root.
        if (position.Trail.Count > 0)
        {
            nav.SelectRoot(position.Tab, position.Trail[0].Key);
        }

        nav.Select(position.Tab);

        if (position.Trail.Count > 0
            && nav.Tab == position.Tab
            && nav.RootKeyOf(position.Tab) == position.Trail[0].Key
            && !nav.Trail.Skip(1).SequenceEqual(position.Trail.Skip(1)))
        {
            nav.GoTo(position.Trail);
        }

        _seen[nav] = nav.RootKeyOf(PanelTab.Transcript);
    }

    /// <summary>
    /// A navigator's tab and the part of its trail that is carried: everything above the first level that
    /// holds the panel or stays on its own surface.
    /// </summary>
    private static Position PositionOf(PanelNavigator nav) =>
        new(nav.Tab, [.. nav.Trail.TakeWhile(crumb => !crumb.Modal && !crumb.Local)]);

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

            // A move of the mirror's making can reach the leader too, by the transcript half.
            if (_leader is not null)
            {
                _led = PositionOf(_leader);
            }
        }
    }

    /// <summary>A tab and a trail, compared by the crumbs on it.</summary>
    private sealed record Position(PanelTab Tab, IReadOnlyList<NavCrumb> Trail)
    {
        public bool Equals(Position? other) =>
            other is not null && Tab == other.Tab && Trail.SequenceEqual(other.Trail);

        public override int GetHashCode() => HashCode.Combine(Tab, Trail.Count);
    }
}
