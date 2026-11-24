namespace D47.Core.Interface;

/// <summary>One transcript page across every surface (Phase 45, "One transcript, both surfaces").</summary>
public sealed class TranscriptMirror
{
    private readonly List<PanelNavigator> _navigators = [];

    /// <summary>The transcript root each navigator was last known to be on.</summary>
    private readonly Dictionary<PanelNavigator, string> _seen = [];

    /// <summary>Where each navigator was last known to be, how many moves it had made then and what it stood in for.</summary>
    private readonly Dictionary<PanelNavigator, (Position At, int Moves, PanelAddress? StandingIn)> _known = [];

    /// <summary>The navigators a chooser held when the address last moved, which go to it once the chooser is gone.</summary>
    private readonly HashSet<PanelNavigator> _behind = [];

    /// <summary>The address every navigator is on, or null before the first navigator is added.</summary>
    private Position? _shared;

    /// <summary>The navigator that last moved the address, whose history the others take.</summary>
    private PanelNavigator? _source;

    /// <summary>Set while a move of this mirror's own making is raising <c>Changed</c>.</summary>
    private bool _mirroring;

    /// <summary>The root every surface is reading, or null before the first navigator is added.</summary>
    public string? Root { get; private set; }

    /// <summary>Brings a navigator into the mirror, on the address the others are on.</summary>
    public void Add(PanelNavigator nav)
    {
        if (!Join(nav))
        {
            return;
        }

        if (_shared is not { } shared || _source is not { } source)
        {
            Root = _seen[nav];
            _shared = PositionOf(nav);
            _source = nav;
            return;
        }

        Mirroring(() =>
        {
            CatchUp(nav);
            Carry(nav, shared, source);
        });
    }

    /// <summary>
    /// Puts every navigator already added on this one's address, its transcript reading included: the window's at
    /// start-up (change-requests.md 34). After that it moves the others as any navigator does.
    /// </summary>
    public void Lead(PanelNavigator nav)
    {
        Join(nav);

        _seen[nav] = nav.RootKeyOf(PanelTab.Transcript);
        Root = _seen[nav];

        var position = PositionOf(nav);

        _shared = position;
        _source = nav;

        Mirroring(() =>
        {
            foreach (var other in _navigators.Where(other => !ReferenceEquals(other, nav)))
            {
                CatchUp(other);
                Carry(other, position, nav);
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
        _known[nav] = Known(nav);
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

        Addressed(nav);

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
    /// The address half: a move a navigator made puts every other on its address, and a navigator a chooser held
    /// goes to the address once the chooser is gone. A change that is not a move, or leaves the navigator where it
    /// was, carries nothing.
    /// </summary>
    private void Addressed(PanelNavigator nav)
    {
        var (at, moves, standingIn) = _known[nav];
        var position = PositionOf(nav);

        _known[nav] = Known(nav);

        if (nav.Held)
        {
            return;
        }

        if (nav.Moves != moves && (position != at || !Equals(nav.StandingIn, standingIn)))
        {
            _shared = position;
            _source = nav;
            _behind.Remove(nav);

            Mirroring(() =>
            {
                foreach (var other in _navigators.Where(other => !ReferenceEquals(other, nav)))
                {
                    Carry(other, position, nav);
                }
            });
        }
        else if (_behind.Contains(nav) && _shared is { } shared && _source is { } source)
        {
            Mirroring(() => Carry(nav, shared, source));
        }
    }

    /// <summary>Puts a navigator on an address and gives it the history of the navigator that moved there.</summary>
    private void Carry(PanelNavigator nav, Position position, PanelNavigator source)
    {
        if (nav.Held)
        {
            _behind.Add(nav);
            return;
        }

        _behind.Remove(nav);
        Follow(nav, position);
        nav.TakeHistory(source);
    }

    /// <summary>
    /// Puts a navigator on an address's tab, root and trail, as far down as it can draw: one without the tab stays
    /// where it is, one without the root stays on the tab's current root, and a trail stops above the first level
    /// that stays on the surface that opened it. One that falls short of the address, or reads it as another
    /// destination, stands in for it.
    /// </summary>
    private void Follow(PanelNavigator nav, Position position)
    {
        List<NavCrumb> drawn = [.. position.Trail.TakeWhile(crumb => !crumb.Local)];

        if (nav.Has(position.Tab))
        {
            // The root first and then the tab, as Show does, so the tab does not open on its previous root.
            if (drawn.Count > 0)
            {
                nav.SelectRoot(position.Tab, drawn[0].Key);
            }

            nav.Select(position.Tab);

            if (drawn.Count > 0
                && nav.Tab == position.Tab
                && nav.RootKeyOf(position.Tab) == drawn[0].Key
                && !nav.Trail.Skip(1).SequenceEqual(drawn.Skip(1)))
            {
                nav.GoTo(drawn);
            }
        }

        var there = nav.Tab == position.Tab
            && nav.OwnDestinationKey == position.DestinationKey
            && nav.Trail.Select(crumb => crumb.Key).SequenceEqual(position.Trail.Select(crumb => crumb.Key), StringComparer.Ordinal);

        nav.StandIn(there ? null : new PanelAddress(position.Tab, position.Trail, position.DestinationKey, position.RootCount));

        _seen[nav] = nav.RootKeyOf(PanelTab.Transcript);
    }

    /// <summary>A navigator's tab and the part of its trail that is carried: everything above the first chooser.</summary>
    private static Position PositionOf(PanelNavigator nav) =>
        new(nav.Tab, [.. nav.Trail.TakeWhile(crumb => !crumb.Modal)])
        {
            DestinationKey = nav.DestinationKey,
            RootCount = nav.Roots(nav.Tab).Count,
        };

    private static (Position At, int Moves, PanelAddress? StandingIn) Known(PanelNavigator nav) =>
        (PositionOf(nav), nav.Moves, nav.StandingIn);

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

            // A move of the mirror's making is not one a navigator made.
            foreach (var nav in _navigators)
            {
                _known[nav] = Known(nav);
            }
        }
    }

    /// <summary>A tab and a trail, compared by the crumbs on it.</summary>
    private sealed record Position(PanelTab Tab, IReadOnlyList<NavCrumb> Trail)
    {
        public string DestinationKey { get; init; } = string.Empty;

        public int RootCount { get; init; }

        public bool Equals(Position? other) =>
            other is not null && Tab == other.Tab && Trail.SequenceEqual(other.Trail);

        public override int GetHashCode() => HashCode.Combine(Tab, Trail.Count);
    }
}
