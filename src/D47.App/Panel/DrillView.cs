using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using D47.Core.Interface;

namespace D47.App.Panel;

/// <summary>
/// A tab's drill stack, drawn as however many panes will fit (Phase 25, "Drill in, and
/// find your way back" and "The panel resizes and zooms").
/// <para>
/// <b>Drilling in and reflowing are one mechanism, and the mechanism is how many panes are
/// visible.</b> Wide shows the level you are on beside the one above it, and a third beside
/// those if there is room; narrow shows one and you drill. Same stack, same breadcrumb, same
/// phrases — so one design covers the headset's big panel, its mini panel, the desktop window
/// and all thirteen zoom rungs, rather than four arrangements that have to be kept in step.
/// </para>
/// <para>
/// That is what makes the requirement responsive rather than fixed. Zoom is a
/// <c>LayoutTransform</c>, so it re-measures and rewraps rather than merely scaling: the default
/// 1024-pixel panel presents 1365 logical at 75% and 512 at 200%, and the layout's job is to
/// survive all of it. <b>The trade is the Commander's and the layout does not have an opinion
/// about which end of it they should want.</b>
/// </para>
/// <para>
/// <b>A chooser takes the whole width.</b> A modal level replaces the panel until it is
/// dismissed, which is the point of it being a level rather than a popup — showing it beside its
/// parent would put back exactly the occlusion and the sixteen-row ceiling that taking the panel
/// exists to avoid.
/// </para>
/// </summary>
public sealed class DrillView : UserControl, IFilterablePage
{
    /// <summary>
    /// The narrowest a pane may be before the strip shows one fewer.
    /// <para>
    /// A width rather than a breakpoint list, because the space this has to survive is a
    /// continuum: the panel's pixels are a setting, the window's are a drag, and zoom re-measures
    /// on thirteen rungs. Dividing by a floor answers all three with one number, where a table of
    /// breakpoints answers whichever ones somebody thought of.
    /// </para>
    /// <para>
    /// 380 puts the default 1024 panel on two panes, its 75% rung on three, and its 200% rung on
    /// one — which is the reflow the phase describes, arrived at from the geometry rather than
    /// chosen to match it.
    /// </para>
    /// </summary>
    public const double MinimumPaneWidth = 380;

    /// <summary>The most panes ever shown. A fourth is a column nobody reads at a metre.</summary>
    public const int MostPanes = 3;

    /// <summary>
    /// How much of the gutter answers the mouse, in logical pixels (Phase 55).
    /// <para>
    /// Wider than the rule it sits on, because a one-pixel target is one nobody can hit — and
    /// narrower than the 28-pixel gutter, so a click just inside a pane is a click in that pane
    /// rather than a drag that did not move.
    /// </para>
    /// </summary>
    private const double HandleWidth = 9;

    /// <summary>
    /// Where the rule is, measured from the left edge of the pane's own column: the host border's
    /// left margin. Named rather than repeated so the handle cannot drift off the line it is a
    /// handle for — see the two <c>14</c>s in <see cref="Draw"/>, which are this same gutter.
    /// </summary>
    private const double RuleOffset = 14;

    private readonly PanelNavigator _nav;
    private readonly PanelTab _tab;
    private readonly Func<NavCrumb, Control> _build;
    private readonly Grid _strip = new();

    /// <summary>
    /// What each level drew, kept by crumb key. Going back and forward again should not rebuild a
    /// page, and a page that holds a scroll position or a partly-filled row should not lose it to
    /// the strip getting one pane wider.
    /// </summary>
    private readonly Dictionary<string, Control> _built = [];

    /// <summary>What is currently laid out, so a redraw that changes nothing does nothing.</summary>
    private IReadOnlyList<string> _showing = [];

    private int _panes = 1;

    /// <summary>
    /// Where the Commander dragged the rules, or null on every surface that was not handed a
    /// mouse (Phase 55). <b>Null is what makes this the window's alone.</b>
    /// <para>
    /// Furnished rather than branched, like <c>PanelView.EnableSearch</c> and
    /// <c>EnableTurnDetails</c>: no code here asks which surface it is on. The headset drives this
    /// same view through a geometric hit test, so a handle that existed there would be draggable
    /// by the ray — the one outcome the ask rules out — and the flat overlay is output-only. Both
    /// are covered by simply never calling <see cref="EnableDrag"/>.
    /// </para>
    /// </summary>
    private PaneWidthMemory? _widths;

    /// <param name="build">
    /// Draws one level. Called once per crumb and the result is kept, so it may be expensive; it
    /// is handed the crumb rather than a key because the word is often the whole of what the page
    /// needs to title itself.
    /// </param>
    /// <param name="tab">
    /// Which tab this strip belongs to.
    /// <para>
    /// Load-bearing rather than decoration. The navigator answers <c>Trail</c> for whichever tab
    /// is <em>showing</em>, and every furnished tab keeps a strip of its own that is still
    /// subscribed while another tab is up — so without this, switching to the transcript would
    /// have the settings strip redraw itself against the transcript's trail and build a level
    /// that belongs to somebody else. Measured: it built the settings page a second time.
    /// </para>
    /// </param>
    public DrillView(PanelNavigator nav, PanelTab tab, Func<NavCrumb, Control> build)
    {
        _nav = nav;
        _tab = tab;
        _build = build;

        Content = _strip;
    }

    /// <summary>How many panes the strip is currently showing. One, two or three.</summary>
    public int Panes => _panes;

    protected override Size ArrangeOverride(Size finalSize)
    {
        // Decided from the arranged width rather than from a window size or a settings value.
        // Everything that changes it - a drag on the window edge, a rung of the zoom ladder, a
        // resolution row, the headset switching to mini - arrives here as a different number, so
        // there is one thing to react to instead of four to subscribe to.
        var wanted = Math.Clamp(
            (int)Math.Floor(finalSize.Width / MinimumPaneWidth), 1, MostPanes);

        if (wanted != _panes)
        {
            _panes = wanted;

            // After the arrange rather than during it: rebuilding the strip's children from
            // inside a layout pass is how a layout cycle starts. The next pass draws it.
            Avalonia.Threading.Dispatcher.UIThread.Post(Draw);
        }

        return base.ArrangeOverride(finalSize);
    }

    /// <summary>
    /// Starts listening, and draws. The navigator raises nothing on the way in — the Commander was
    /// already where they are — and the arrange pass only redraws when the pane count moves, so
    /// without the draw a strip built at one pane would be shown empty until the first navigation.
    /// <para>
    /// <b>Paired with the detach below, and it has to be</b> (remediation.md 17, item 5; the same
    /// fault and the same fix as remediation.md 11 item 3 in <c>ChecklistPage</c> and 13 item 1 in
    /// <c>LoadoutPage</c>). The subscription used to be made in the constructor and dropped on
    /// detach, which is not a pair: switching tab reparents this strip, so it detached,
    /// unsubscribed, and was deaf for the rest of the session. Coming back re-attached and drew
    /// once, so the page looked right — and then a click that drilled changed the trail, raised
    /// <c>Changed</c> to nobody, and the pane never followed it. The Commander's report was that
    /// clicking a ship did nothing.
    /// </para>
    /// </summary>
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);

        _nav.Changed += OnNavigated;

        // The trail may have moved while this strip was off screen — a keyword route or a spoken
        // command navigates whichever tab it belongs to, not the one being looked at.
        Draw();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _nav.Changed -= OnNavigated;
    }

    private void OnNavigated(object? sender, EventArgs e) => Draw();

    /// <summary>
    /// Empties the strip, <b>handing each pane back before dropping the border that held it</b>.
    /// <para>
    /// A control belongs to exactly one logical tree, and these panes are kept across redraws so
    /// a level does not lose its scroll position to the strip getting one column wider. Clearing
    /// the strip alone leaves each pane still parented to the border it was in, and the next
    /// draw throws rather than reparenting.
    /// </para>
    /// </summary>
    private void Empty()
    {
        foreach (var host in _strip.Children.OfType<Border>())
        {
            host.Child = null;
        }

        _strip.Children.Clear();
        _strip.ColumnDefinitions.Clear();
    }

    /// <summary>
    /// Lays out the deepest levels that fit, root-most first, with the level the Commander is on
    /// last — which is the side a right-handed reflow grows from and the side the breadcrumb
    /// reads towards.
    /// </summary>
    private void Draw()
    {
        // Only ever against its own tab's stack. See the constructor.
        if (_nav.Tab != _tab)
        {
            return;
        }

        // A chooser is drawn by the panel itself, over this whole region, because it takes the
        // panel rather than sharing it. Dropped from the trail here rather than special-cased
        // below: the strip's job is the levels the Commander can be in two of at once, and a
        // modal is by definition not one of those (Phase 25).
        var trail = _nav.Modal
            ? _nav.Trail.Take(_nav.Trail.Count - 1).ToList()
            : _nav.Trail;

        if (trail.Count == 0)
        {
            Empty();
            _showing = [];
            return;
        }

        var window = Math.Min(_panes, trail.Count);

        var visible = trail.Skip(trail.Count - window).ToList();
        var keys = visible.Select(crumb => crumb.Key).ToList();

        if (_showing.SequenceEqual(keys))
        {
            return;
        }

        _showing = keys;

        Empty();

        // Re-applied on every draw and not only on the first, which is the trap this phase names:
        // Empty() clears the column definitions on every navigation, so a width the Commander
        // dragged would otherwise be discarded the moment they open a ship. The panes themselves
        // are already kept across redraws for the same class of reason; the widths now are too.
        var shares = _widths?.Remembered(visible.Count);

        for (var index = 0; index < visible.Count; index++)
        {
            var column = new ColumnDefinition(shares?[index] ?? 1, GridUnitType.Star);

            // The reflow's floor is the drag's floor, and it has to be the same number: otherwise
            // a Commander can drag a pane down to a sliver that ArrangeOverride still believes is
            // 380 wide, which is two mechanisms disagreeing about how much room a pane has. A drag
            // that would cross it stops at it rather than refusing - a handle that stops moving
            // says what the limit is, and one that snaps back says nothing.
            //
            // Only when there is something to drag. On the headset this would be a layout
            // constraint bought for a control that surface never draws.
            if (_widths is not null)
            {
                column.MinWidth = MinimumPaneWidth;
            }

            _strip.ColumnDefinitions.Add(column);

            var crumb = visible[index];

            if (!_built.TryGetValue(crumb.Key, out var pane))
            {
                pane = _build(crumb);
                _built[crumb.Key] = pane;
            }

            var host = new Border
            {
                Child = pane,

                // A rule between panes rather than a gap, because a gap at a metre reads as two
                // surfaces and a rule reads as two columns of one. Only between: an edge on the
                // outside would be a second border inside the pane's own.
                BorderThickness = new Thickness(index == 0 ? 0 : 1, 0, 0, 0),
                Padding = new Thickness(index == 0 ? 0 : 14, 0, 0, 0),
                Margin = new Thickness(index == 0 ? 0 : 14, 0, 0, 0),
            };

            host.Bind(
                Border.BorderBrushProperty,
                this.GetResourceObservable(Theming.ThemeManager.BorderKey));

            Grid.SetColumn(host, index);
            _strip.Children.Add(host);

            // After the pane, so it is above it in z-order and the pointer reaches it rather than
            // the page underneath.
            if (_widths is not null && index > 0)
            {
                _strip.Children.Add(Handle(index));
            }
        }
    }

    /// <summary>
    /// The grab area on one rule (Phase 55).
    /// <para>
    /// <b>In the pane's own column rather than a column of its own</b>, aligned left and sitting
    /// in the gutter the border already leaves. A splitter column would change what
    /// <c>Grid.SetColumn</c> means for every pane and put a second thing in the arithmetic
    /// <see cref="ArrangeOverride"/> does — so the reflow stays the sole authority on how many
    /// panes there are, and this only ever changes their proportions within that count.
    /// </para>
    /// <para>
    /// <b>Templated down to nothing</b>, because the page at rest has to look exactly as it does
    /// now: the theme's own splitter draws a visible bar, and what should be visible here is the
    /// hairline the border is already drawing. A transparent background is still hit-testable, so
    /// what is left is a cursor change and a drag.
    /// </para>
    /// </summary>
    private Control Handle(int index)
    {
        var handle = new GridSplitter
        {
            ResizeDirection = GridResizeDirection.Columns,

            // The splitter sits in column `index`, so "previous and current" is the pane to its
            // left and the pane it is in - the two the rule is between.
            ResizeBehavior = GridResizeBehavior.PreviousAndCurrent,

            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Stretch,
            Width = HandleWidth,

            // Centred on the rule rather than on the column edge: the border is inset by its own
            // left margin, so the line the Commander is aiming at is RuleOffset in from here.
            Margin = new Thickness(RuleOffset - (HandleWidth / 2), 0, 0, 0),

            Background = Brushes.Transparent,
            Cursor = new Cursor(StandardCursorType.SizeWestEast),
            Template = new FuncControlTemplate<GridSplitter>(
                (_, _) => new Border { Background = Brushes.Transparent }),
        };

        // On completion rather than on every delta: a drag is one decision, and writing the file
        // on each frame of it would be hundreds of writes for one choice.
        handle.DragCompleted += (_, _) => Remember();

        Grid.SetColumn(handle, index);
        return handle;
    }

    /// <summary>
    /// Writes down what a drag left, as each pane's share of the strip.
    /// <para>
    /// From the arranged widths rather than from the star coefficients, because the arranged width
    /// is what the Commander actually sees and needs no assumption about what the splitter did to
    /// the units. Normalised here so the record is proportions and the layout is free to express
    /// them however it likes.
    /// </para>
    /// </summary>
    private void Remember()
    {
        if (_widths is null)
        {
            return;
        }

        var widths = _strip.ColumnDefinitions.Select(column => column.ActualWidth).ToList();
        var total = widths.Sum();

        // A strip that has not been arranged yet measures zero, and a zero share would be a pane
        // that can never be dragged back. Nothing to record rather than something wrong.
        if (total <= 0 || widths.Any(width => !double.IsFinite(width) || width <= 0))
        {
            return;
        }

        _widths.Remember(widths.Count, widths.Select(width => width / total).ToList());
    }

    /// <summary>
    /// Gives this strip's rules a handle the mouse can drag, and remembers where they are left
    /// (Phase 55). The desktop window calls it; the headset and the flat overlay never do.
    /// <para>
    /// Redraws rather than waiting for the next navigation, because a strip that is already on
    /// screen when this arrives would otherwise have no handles until the Commander drilled
    /// somewhere — and the reason to call it at all is the page in front of them.
    /// </para>
    /// </summary>
    public void EnableDrag(PaneWidthMemory memory)
    {
        _widths = memory;

        // The short-circuit in Draw compares against what is already laid out, and the keys have
        // not changed - only whether they are draggable has.
        _showing = [];
        Draw();
    }

    /// <summary>
    /// The surface's one search box, passed down to whichever levels care about it.
    /// <para>
    /// Every built level rather than only the visible ones, and that is the point: a level the
    /// Commander scrolls back to should not be showing the filter it was left with under an empty
    /// search box, which is bugs.md 2 exactly. A level that is not filterable ignores it.
    /// </para>
    /// </summary>
    /// <summary>
    /// Whether any level currently on screen answers a query. Only the levels being shown, not
    /// every level ever built: a filterable page two steps back up the stack is not a reason to
    /// offer a search box over the one in front of the Commander.
    /// </summary>
    public bool Filters =>
        _showing
            .Select(key => _built.TryGetValue(key, out var pane) ? pane : null)
            .OfType<IFilterablePage>()
            .Any(page => page.Filters);

    public void Filter(string? query)
    {
        foreach (var pane in _built.Values.OfType<IFilterablePage>())
        {
            pane.Filter(query);
        }
    }

    /// <summary>
    /// Forgets a level's page, so the next visit rebuilds it. For a page whose content the
    /// Commander has just changed underneath — a slot whose module was swapped, a plan that was
    /// deleted — where showing what was drawn before would be showing something that is no longer
    /// true.
    /// </summary>
    public void Forget(string key)
    {
        _built.Remove(key);
        _showing = [];
        Draw();
    }
}
