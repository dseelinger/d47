using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using D47.Core.Interface;

namespace D47.App.Panel;

/// <summary>
/// A tab's drill stack, drawn as however many panes will fit (list.md Phase 25, "Drill in, and
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

        _nav.Changed += OnNavigated;
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
    /// The first draw. The navigator raises nothing on the way in — the Commander was already
    /// where they are — and the arrange pass only redraws when the pane count moves, so without
    /// this a strip built at one pane would be shown empty until the first navigation.
    /// </summary>
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
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
        // modal is by definition not one of those (list.md Phase 25).
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

        for (var index = 0; index < visible.Count; index++)
        {
            _strip.ColumnDefinitions.Add(new ColumnDefinition(1, GridUnitType.Star));

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
        }
    }

    /// <summary>
    /// The surface's one search box, passed down to whichever levels care about it.
    /// <para>
    /// Every built level rather than only the visible ones, and that is the point: a level the
    /// Commander scrolls back to should not be showing the filter it was left with under an empty
    /// search box, which is bugs.md 2 exactly. A level that is not filterable ignores it.
    /// </para>
    /// </summary>
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
