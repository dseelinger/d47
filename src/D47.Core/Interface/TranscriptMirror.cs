namespace D47.Core.Interface;

/// <summary>
/// One transcript page across every surface (Phase 45, "One transcript, both surfaces").
/// <para>
/// <b>What you are reading is shared; how it is drawn is not.</b> Each surface keeps its own
/// <see cref="PanelNavigator"/> because tabs and drill trails are per-surface — the window can be
/// three levels into a ship's slots while the headset reads the conversation — and that does not
/// change here. What changes is the root of the Transcript tab: Conversation, Technical or the
/// log file is now one choice rather than one per surface, so switching the window to the log
/// file puts the headset on it, and the reverse, with no preferred surface. Mini/full and zoom
/// are how a surface draws what it is reading and stay where they were.
/// </para>
/// <para>
/// <b>The transcript is shared with no preferred surface. The tab is not: the window leads and
/// the mini panel follows.</b> Those two sentences are the whole of this class and they are
/// deliberately different, so read them together rather than assuming the second is a bug in the
/// first (change-requests.md 34, ruled by the Commander 2026-08-24).
/// </para>
/// <para>
/// <b>This reverses what stood here, and the reasoning it reverses was written down.</b> It said:
/// <em>mirroring tabs and trails as well would acquire an except — Settings is desktop-only and
/// Loadout is withdrawn from VR, so that rule would hold only sometimes, which is the kind people
/// misremember.</em> The objection was never that mirroring tabs is hard. It was that the rule
/// would need an exception, and an exception gets misremembered.
/// </para>
/// <para>
/// <b>The Commander's request supplied the exception in the same breath</b>, which is what
/// disposes of the objection: <em>switching to a tab (and view of the tab) in the main window
/// should ALWAYS affect the mini-panel — IFF that tab/view is present on the mini panel.</em>
/// That is a stated rule with its own condition, not a rule that happens to fail sometimes. And
/// the condition needs no code: <see cref="PanelNavigator.Select"/> already declines a tab no host
/// furnished, so <em>not calling Furnish</em> — the same thing that withdrew Loadout from the
/// headset — is the whole of it.
/// </para>
/// <para>
/// <b>One-way, and that is the half that protects the Commander.</b> The follower may be moved
/// independently and keeps where it was put until the window next moves, so somebody in a headset
/// can put their mini panel on something and have it stay there. It also leaves
/// Phase 48 standing untouched — <em>what must not follow is the overlay's tab
/// dragging the window's</em> — which was the strongest argument for this shape rather than a
/// symmetrical one.
/// </para>
/// <para>
/// <b>This is the one mechanism that carries a transcript root between surfaces.</b> A spoken
/// phrase and a switch flip are applied to every navigator by the host, because a phrase has no
/// surface attached to it and because tabs are per-surface — but they <em>initiate</em> a move
/// the way a press on the mode control does; they do not keep the surfaces agreeing. The first
/// navigator they reach raises <see cref="PanelNavigator.Changed"/>, this carries the root to the
/// rest, and the loop then finds each of them already there and is declined — the same
/// <em>are you already there</em> answer the switch path was built on. Two mechanisms holding one
/// invariant would eventually disagree about it; this one holds it alone.
/// </para>
/// <para>
/// <b>The echo is stopped on purpose.</b> Moving B from A's change raises B's
/// <see cref="PanelNavigator.Changed"/>, which arrives back here while A's is still being
/// handled. Without a guard that terminated only because <see cref="PanelNavigator.SelectRoot"/>
/// declines a root it is already on; <see cref="_mirroring"/> makes it terminate because this
/// code says so. Direction is decided by what each navigator was last known to be on rather than
/// by comparing the two: a surface whose chooser refused the move is <em>behind</em>, not a
/// second source, and is caught up on its next change rather than dragging the other one back.
/// </para>
/// <para>
/// Synchronous, and in Core because it is arithmetic over navigators: no thread, no dispatcher.
/// Every navigator here belongs to one thread — the headset's copy is built on the window's
/// (architecture.md D1) and every cross-thread caller posts to it — so a move can be made in the
/// handler rather than posted, which is what makes the guard a guard. Two posted moves in flight
/// could cross, and each would read the other's arrival as a fresh change.
/// </para>
/// </summary>
public sealed class TranscriptMirror
{
    private readonly List<PanelNavigator> _navigators = [];

    /// <summary>
    /// The transcript root each navigator was last known to be on. A change that leaves this
    /// alone was a tab or a trail, which is nobody else's business; one that differs from it is
    /// this surface moving the transcript, and becomes everybody's.
    /// </summary>
    private readonly Dictionary<PanelNavigator, string> _seen = [];

    /// <summary>
    /// The tab each navigator was last known to be on, which is to the tab half what
    /// <see cref="_seen"/> is to the transcript half: the way a move made <em>by this surface</em>
    /// is told apart from one it was given.
    /// </summary>
    private readonly Dictionary<PanelNavigator, PanelTab> _tabs = [];

    /// <summary>
    /// The navigator whose tab the others follow, or null where nobody leads and the tab half is
    /// simply off. The window's, set by <see cref="Lead"/>.
    /// </summary>
    private PanelNavigator? _leader;

    /// <summary>Set while a move of this mirror's own making is raising <c>Changed</c>.</summary>
    private bool _mirroring;

    /// <summary>
    /// The root every surface is reading, or null before the first navigator is added. The
    /// key of a Transcript root — <c>transcript.technical</c> — never a word.
    /// </summary>
    public string? Root { get; private set; }

    /// <summary>
    /// Brings a navigator into the mirror. The first one's transcript root becomes the shared
    /// one; a later one is put on it straight away, so a surface built after the Commander has
    /// already moved the other arrives agreeing rather than a step behind. Adding one twice is a
    /// no-op.
    /// </summary>
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

    /// <summary>
    /// Names the navigator the others follow — the window's (change-requests.md 34). Adds it if it
    /// is not already in, so a host has one call to make rather than two in an order that matters.
    /// <para>
    /// <b>Leading is not a second mirror.</b> The same re-entrancy guard, the same last-seen
    /// bookkeeping and the same <em>decline a move you have already made</em> answer carry it, for
    /// the reason the transcript half states: two mechanisms holding one invariant eventually
    /// disagree about it.
    /// </para>
    /// </summary>
    public void Lead(PanelNavigator nav)
    {
        Add(nav);
        _leader = nav;
    }

    private void OnChanged(PanelNavigator nav)
    {
        // The echo: a move this mirror made, announcing itself. Explicitly nothing.
        if (_mirroring)
        {
            return;
        }

        Led(nav);

        var root = nav.RootKeyOf(PanelTab.Transcript);

        if (root != _seen[nav])
        {
            // This surface moved the transcript — from its mode control, its menu, a phrase
            // applied to it or a switch. It is the source, and the rest follow.
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
            // This surface is behind: a chooser held it when the others moved, and whatever it
            // just did — most likely dismissing that chooser — is a chance to bring it level.
            Mirroring(() => CatchUp(nav));
        }
    }

    /// <summary>
    /// The tab half: where the leader moved, the followers go, and where anybody else moved,
    /// nothing happens.
    /// <para>
    /// <b>The follower's own move is recorded and not acted on</b>, which is what "may be moved
    /// independently and keeps where it was put" means in code. Nothing else has to remember that
    /// it was moved: the next time the window moves, the leader leads again.
    /// </para>
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

        // The view of the tab as well as the tab, which is what was asked for: switching the
        // window to a tab it is already on and changing only the root still carries.
        var root = nav.RootKeyOf(tab);

        Mirroring(() =>
        {
            foreach (var other in _navigators.Where(other => !ReferenceEquals(other, nav)))
            {
                // Declined outright by a surface that never furnished this tab, which is the
                // Commander's IFF and costs no special case. Both calls are made either way:
                // a follower already on the tab still wants the root.
                other.Select(tab);
                other.SelectRoot(tab, root);

                _tabs[other] = other.Tab;
            }
        });
    }

    /// <summary>
    /// Puts one navigator on the shared root, and records it only if the move was taken. A
    /// refusal — a chooser holding the panel — leaves it known to be behind.
    /// </summary>
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
