using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.VisualTree;
using D47.App.Theming;
using D47.Core.Interface;

namespace D47.App.Panel;

/// <summary>
/// A tab's drill stack, drawn as however many panes will fit (Phase 25, "Drill in, and find your way
/// back" and "The panel resizes and zooms").
/// </summary>
public sealed class DrillView : UserControl, IFilterablePage, IPageChrome, IPageSummary, IPageSection
{
    /// <summary>The narrowest a pane may be before the strip shows one fewer.</summary>
    public const double MinimumPaneWidth = 380;

    /// <summary>The most panes ever shown.</summary>
    public const int MostPanes = 3;

    /// <summary>How much of the gutter answers the mouse, in logical pixels (Phase 55).</summary>
    private const double HandleWidth = 9;

    /// <summary>
    /// Where the rule is, measured from the left edge of the pane's own column: the host border's left
    /// margin.
    /// </summary>
    private const double RuleOffset = 14;

    /// <summary>A leftmost rule released nearer the strip's left edge than this hides the leftmost pane.</summary>
    private const double CollapseThreshold = MinimumPaneWidth / 2;

    /// <summary>How wide a rule the headset's ray is near is drawn, in logical pixels.</summary>
    private const double RayRuleWidth = 6;

    /// <summary>The class a rule the headset's ray can take hold of carries, which lights it while the ray is near.</summary>
    internal const string RayRuleClass = "pane-rule";

    private readonly PanelNavigator _nav;
    private readonly PanelTab _tab;
    private readonly Func<NavCrumb, Control> _build;
    private readonly Grid _strip = new();

    /// <summary>What each level drew, kept by crumb key.</summary>
    private readonly Dictionary<string, Control> _built = [];

    /// <summary>The row that had focus when a deeper level was opened, and its place among the level's rows, by crumb key.</summary>
    private readonly Dictionary<string, (Control Row, int Index)> _focusedRows = [];

    /// <summary>How many levels the trail had at the last draw.</summary>
    private int _depth;

    /// <summary>What is currently laid out, so a redraw that changes nothing does nothing.</summary>
    private IReadOnlyList<string> _showing = [];

    /// <summary>
    /// Raised whenever a draw changes what is showing, so a search row decided before the first draw
    /// (cold start attaches the strip after the pane already asked) gets asked again.
    /// </summary>
    public event EventHandler? Drawn;

    public event EventHandler? SummaryChanged;

    public event EventHandler? OpenSectionChanged;

    private int _panes = 1;

    /// <summary>Whether the Commander has hidden the leftmost pane.</summary>
    private bool _hidden;

    /// <summary>Whether the last draw left the leftmost pane out.</summary>
    private bool _hiding;

    /// <summary>The crumb key of the pane the last draw left out, or null.</summary>
    private string? _hiddenKey;

    /// <summary>The pointer's last x in the strip during a drag of the leftmost rule.</summary>
    private double _pointerX = double.NaN;

    /// <summary>
    /// The rules the headset's ray can take hold of, each with the column it is the left edge of; column 0
    /// is the strip's edge while the leftmost pane is hidden.
    /// </summary>
    private readonly Dictionary<Control, int> _rayRules = [];

    /// <summary>Each column's width while the ray holds a rule, or null.</summary>
    private List<double>? _dragWidths;

    /// <summary>How far right of the ray the held rule sits.</summary>
    private double _grabOffset;

    /// <summary>
    /// Where the Commander dragged the rules, or null on every surface that was not handed a mouse
    /// (Phase 55).
    /// </summary>
    private PaneWidthMemory? _widths;

    /// <summary><param name="build"> Draws one level.</summary>
    /// <param name="build">Draws one level.</param>
    /// <param name="tab">Which tab this strip belongs to.</param>
    public DrillView(PanelNavigator nav, PanelTab tab, Func<NavCrumb, Control> build)
    {
        _nav = nav;
        _tab = tab;
        _build = build;

        Content = _strip;
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        // Decided from the arranged width rather than from a window size or a settings value.
        var wanted = Math.Clamp(
            (int)Math.Floor(finalSize.Width / MinimumPaneWidth), 1, MostPanes);

        if (wanted != _panes)
        {
            _panes = wanted;

            // After the arrange rather than during it: rebuilding the strip's children from inside a layout
            // pass is how a layout cycle starts.
            Avalonia.Threading.Dispatcher.UIThread.Post(Draw);
        }

        return base.ArrangeOverride(finalSize);
    }

    /// <summary>Starts listening, and draws.</summary>
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);

        _nav.Changed += OnNavigated;

        // The trail may have moved while this strip was off screen — a keyword route or a spoken command
        // navigates whichever tab it belongs to, not the one being looked at.
        Draw();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _nav.Changed -= OnNavigated;
    }

    private void OnNavigated(object? sender, EventArgs e) => Draw();

    /// <summary>Empties the strip, handing each pane back before dropping the border that held it.</summary>
    private void Empty()
    {
        foreach (var host in _strip.Children.OfType<Border>())
        {
            host.Child = null;
        }

        _strip.Children.Clear();
        _strip.ColumnDefinitions.Clear();
        _rayRules.Clear();
        _dragWidths = null;
    }

    /// <summary>
    /// Lays out the deepest levels that fit, root-most first, with the level the Commander is on last —
    /// which is the side a right-handed reflow grows from and the side the breadcrumb reads towards.
    /// </summary>
    private void Draw()
    {
        // Only ever against its own tab's stack.
        if (_nav.Tab != _tab)
        {
            return;
        }

        // A chooser is drawn by the panel itself, over this whole region, because it takes the panel rather
        // than sharing it.
        var trail = _nav.Modal
            ? _nav.Trail.Take(_nav.Trail.Count - 1).ToList()
            : _nav.Trail;

        if (trail.Count == 0)
        {
            _depth = 0;
            _hiding = false;
            _hiddenKey = null;
            Empty();
            _showing = [];
            Drawn?.Invoke(this, EventArgs.Empty);
            return;
        }

        var start = trail.Count - 1;

        while (start > 0 && !trail[start].Starts)
        {
            start--;
        }

        var window = trail[^1].Whole ? 1 : Math.Min(_panes, trail.Count - start);

        var first = trail.Count - window;
        var visible = trail.Skip(first).ToList();

        // The level the Commander is on is last, so it always draws; a hidden pane is dropped, not replaced.
        var hide = _widths is not null && _hidden && visible.Count > 1;
        var drawn = hide ? visible.Skip(1).ToList() : visible;
        var keys = drawn.Select(crumb => crumb.Key).ToList();

        var opened = trail.Count > _depth;
        var wentBack = trail.Count < _depth;
        _depth = trail.Count;

        if (_showing.SequenceEqual(keys) && hide == _hiding)
        {
            return;
        }

        if (opened)
        {
            RememberFocusedRows();
        }

        _showing = keys;
        _hiding = hide;
        _hiddenKey = hide ? visible[0].Key : null;

        Empty();

        // Re-applied on every draw and not only on the first, which is the trap this phase names: Empty()
        // clears the column definitions on every navigation, so a width the Commander dragged would otherwise
        // be discarded the moment they open a ship.
        var shares = _widths?.Remembered(visible.Count)?.Skip(hide ? 1 : 0).ToList();

        for (var index = 0; index < drawn.Count; index++)
        {
            var column = new ColumnDefinition(shares?[index] ?? 1, GridUnitType.Star);

            // The reflow's floor is the drag's floor, and it has to be the same number: otherwise a Commander
            // can drag a pane down to a sliver that ArrangeOverride still believes is 380 wide, which is two
            // mechanisms disagreeing about how much room a pane has.
            if (_widths is not null)
            {
                column.MinWidth = MinimumPaneWidth;
            }

            _strip.ColumnDefinitions.Add(column);

            var crumb = drawn[index];

            if (!_built.TryGetValue(crumb.Key, out var pane))
            {
                pane = _build(crumb);
                _built[crumb.Key] = pane;

                if (pane is IPageSummary summary)
                {
                    summary.SummaryChanged += (_, _) => SummaryChanged?.Invoke(this, EventArgs.Empty);
                }

                if (pane is IPageSection section)
                {
                    section.OpenSectionChanged += (_, _) => OpenSectionChanged?.Invoke(this, EventArgs.Empty);
                }
            }

            var host = new Border
            {
                Child = pane,

                // A rule between panes rather than a gap, because a gap at a metre reads as two surfaces and
                // a rule reads as two columns of one.
                BorderThickness = new Thickness(index == 0 ? 0 : 1, 0, 0, 0),
                Padding = new Thickness(index == 0 ? 0 : 14, 0, 0, 0),
                Margin = new Thickness(index == 0 ? 0 : 14, 0, 0, 0),
            };

            host.Bind(
                Border.BorderBrushProperty,
                this.GetResourceObservable(Theming.ThemeManager.Line2Key));

            Grid.SetColumn(host, index);
            _strip.Children.Add(host);

            // After the pane, so it is above it in z-order and the pointer reaches it rather than the page
            // underneath.
            if (_widths is not null && index > 0)
            {
                _strip.Children.Add(_widths.Headset ? RayRule(index) : Handle(index));
            }
        }

        if (hide)
        {
            _strip.Children.Add(_widths is { Headset: true } ? RayRule(0) : EdgeHandle());
        }

        if (wentBack)
        {
            RefocusRemembered(keys[^1]);
        }

        Drawn?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Records, for each level showing, the row that has keyboard focus, and drops the record where none has.</summary>
    private void RememberFocusedRows()
    {
        var focused = TopLevel.GetTopLevel(this)?.FocusManager?.GetFocusedElement() as Control;

        // A row focused by the pointer is not remembered: only one reached from the keyboard draws its focus.
        if (focused is not null && !focused.Classes.Contains(":focus-visible"))
        {
            focused = null;
        }

        foreach (var key in _showing)
        {
            var at = focused is not null && _built.TryGetValue(key, out var pane) ? ListRow.Within(pane).IndexOf(focused) : -1;

            if (at >= 0)
            {
                _focusedRows[key] = (focused!, at);
            }
            else
            {
                _focusedRows.Remove(key);
            }
        }
    }

    /// <summary>Focuses the remembered row of a level that is now the deepest, or the row at its place when that row is gone.</summary>
    private void RefocusRemembered(string key)
    {
        if (!_focusedRows.Remove(key, out var kept) || !_built.TryGetValue(key, out var pane))
        {
            return;
        }

        var rows = ListRow.Within(pane);

        if (rows.Count > 0)
        {
            (rows.Contains(kept.Row) ? kept.Row : rows[Math.Min(kept.Index, rows.Count - 1)]).Focus(NavigationMethod.Directional);
        }
    }

    /// <summary>The grab area on one rule (Phase 55).</summary>
    private Control Handle(int index)
    {
        var handle = new GridSplitter
        {
            ResizeDirection = GridResizeDirection.Columns,

            // The splitter sits in column `index`, so "previous and current" is the pane to its left and the
            // pane it is in - the two the rule is between.
            ResizeBehavior = GridResizeBehavior.PreviousAndCurrent,

            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Stretch,
            Width = HandleWidth,

            // Centred on the rule rather than on the column edge: the border is inset by its own left margin,
            // so the line the Commander is aiming at is RuleOffset in from here.
            Margin = new Thickness(RuleOffset - (HandleWidth / 2), 0, 0, 0),

            Background = Brushes.Transparent,
            Cursor = new Cursor(StandardCursorType.SizeWestEast),
            Template = new FuncControlTemplate<GridSplitter>(
                (_, _) => new Border { Background = Brushes.Transparent }),
        };

        // On completion rather than on every delta: a drag is one decision, and writing the file on each
        // frame of it would be hundreds of writes for one choice.
        if (index == 1)
        {
            handle.DragStarted += (_, _) => _pointerX = double.NaN;
            handle.AddHandler(
                PointerMovedEvent,
                (_, e) => _pointerX = e.GetPosition(_strip).X,
                Avalonia.Interactivity.RoutingStrategies.Tunnel | Avalonia.Interactivity.RoutingStrategies.Bubble,
                handledEventsToo: true);

            handle.DragCompleted += (_, _) =>
            {
                var x = _pointerX;

                // After the splitter's own event has finished: a hide redraws the strip it sits in.
                Avalonia.Threading.Dispatcher.UIThread.Post(() => ReleaseLeftmostRule(x));
            };
        }
        else
        {
            handle.DragCompleted += (_, _) => Remember();
        }

        Grid.SetColumn(handle, index);
        return handle;
    }

    /// <summary>The grab area at the strip's left edge while the leftmost pane is hidden.</summary>
    private Control EdgeHandle()
    {
        var handle = new Avalonia.Controls.Panel
        {
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Stretch,
            Width = HandleWidth,
            Background = Brushes.Transparent,
            Cursor = new Cursor(StandardCursorType.SizeWestEast),
        };

        handle.PointerPressed += (_, e) =>
        {
            e.Pointer.Capture(handle);
            e.Handled = true;
        };

        handle.PointerReleased += (_, e) =>
        {
            if (!ReferenceEquals(e.Pointer.Captured, handle))
            {
                return;
            }

            e.Pointer.Capture(null);
            ReleaseEdgeHandle(e.GetPosition(_strip).X);
        };

        Grid.SetColumn(handle, 0);
        return handle;
    }

    /// <summary>
    /// A rule the headset's ray can take hold of, at the left of column <paramref name="index"/>, drawn only
    /// while the ray is near it.
    /// </summary>
    private Border RayRule(int index)
    {
        var rule = new Border
        {
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Stretch,
            Width = RayRuleWidth,
            Margin = new Thickness(index == 0 ? 0 : RuleOffset - (RayRuleWidth / 2), 0, 0, 0),
            IsHitTestVisible = false,
        };

        rule.Classes.Add(RayRuleClass);
        Grid.SetColumn(rule, index);
        _rayRules[rule] = index;

        return rule;
    }

    /// <summary>The rules the headset's ray can take hold of.</summary>
    internal IReadOnlyCollection<Control> RayRules => _rayRules.Keys;

    /// <summary>
    /// Takes hold of a rule at a point given in <paramref name="from"/>'s space, keeping the distance between
    /// the ray and the rule for the rest of the drag.
    /// </summary>
    internal void TakeRule(Control rule, Visual from, Point at)
    {
        _dragWidths = [.. _strip.ColumnDefinitions.Select(column => column.ActualWidth)];
        _grabOffset = 0;

        if (_rayRules.TryGetValue(rule, out var index) && index > 0 && index < _dragWidths.Count
            && from.TranslatePoint(at, _strip) is { } local)
        {
            _grabOffset = _dragWidths.Take(index).Sum() + RuleOffset - local.X;
        }
    }

    /// <summary>Moves a rule the ray holds to a point given in <paramref name="from"/>'s space.</summary>
    internal void DragRule(Control rule, Visual from, Point at)
    {
        if (_rayRules.TryGetValue(rule, out var index) && index > 0 && from.TranslatePoint(at, _strip) is { } local)
        {
            PlaceRule(index, local.X + _grabOffset);
        }
    }

    /// <summary>
    /// Lets go of a rule the ray holds, at a point given in <paramref name="from"/>'s space. Without
    /// <paramref name="decide"/>, the widths are kept and no pane is hidden or shown.
    /// </summary>
    internal void ReleaseRule(Control rule, Visual from, Point at, bool decide = true)
    {
        if (_rayRules.TryGetValue(rule, out var index) && from.TranslatePoint(at, _strip) is { } local)
        {
            // The ray's own x, not the rule's: the rule stops at the pane's minimum, and the hide is decided
            // past it.
            switch (index)
            {
                case 0 when decide:
                    ReleaseEdgeHandle(local.X);
                    break;
                case 0:
                    break;
                case 1 when decide:
                    ReleaseLeftmostRule(local.X);
                    break;
                default:
                    Remember();
                    break;
            }
        }

        _dragWidths = null;
    }

    /// <summary>
    /// Puts the rule at the left of column <paramref name="index"/> at <paramref name="x"/> in the strip,
    /// keeping the panes either side of it at least <see cref="MinimumPaneWidth"/> wide.
    /// </summary>
    private void PlaceRule(int index, double x)
    {
        var columns = _strip.ColumnDefinitions;

        if (_dragWidths is not { } widths || widths.Count != columns.Count || index < 1 || index >= columns.Count)
        {
            return;
        }

        var before = widths.Take(index - 1).Sum();
        var pair = widths[index - 1] + widths[index];

        if (pair < 2 * MinimumPaneWidth)
        {
            return;
        }

        // The rule is drawn RuleOffset in from the edge of the column it starts.
        var left = Math.Clamp(x - RuleOffset - before, MinimumPaneWidth, pair - MinimumPaneWidth);

        widths[index - 1] = left;
        widths[index] = pair - left;

        for (var column = 0; column < columns.Count; column++)
        {
            columns[column].Width = new GridLength(widths[column], GridUnitType.Star);
        }
    }

    /// <summary>Each column's width: as the ray left it during a drag, otherwise as laid out.</summary>
    private List<double> Widths() =>
        _dragWidths is { } dragged && dragged.Count == _strip.ColumnDefinitions.Count
            ? [.. dragged]
            : [.. _strip.ColumnDefinitions.Select(column => column.ActualWidth)];

    /// <summary>Decides a drag of the leftmost rule from where the pointer was released.</summary>
    internal void ReleaseLeftmostRule(double x)
    {
        if (_widths is null)
        {
            return;
        }

        if (x < CollapseThreshold && !_hiding)
        {
            _hidden = true;
            _widths.RememberLeftmostHidden(true);
            _showing = [];
            Draw();
            return;
        }

        Remember();
    }

    /// <summary>The crumb key of the leftmost pane while it is hidden, or null.</summary>
    internal string? HiddenKey => _hiding ? _hiddenKey : null;

    /// <summary>Shows the hidden pane again when the edge handle is released right of the threshold.</summary>
    internal void ReleaseEdgeHandle(double x)
    {
        if (_widths is null || !_hiding || x < CollapseThreshold)
        {
            return;
        }

        ShowHiddenPane(x);
    }

    /// <summary>Shows the hidden pane again at its remembered share.</summary>
    internal bool ShowHiddenPane()
    {
        if (_widths is null || !_hiding)
        {
            return false;
        }

        ShowHiddenPane(null);
        return true;
    }

    private void ShowHiddenPane(double? x)
    {
        if (_widths is null)
        {
            return;
        }

        var count = _strip.ColumnDefinitions.Count + 1;
        var total = _strip.Bounds.Width;
        var others = _widths.Remembered(count)?.Skip(1).ToList() ?? [.. Enumerable.Repeat(1.0, count - 1)];
        var othersSum = others.Sum();

        if (x is { } at && total > 0)
        {
            var widest = Math.Max(MinimumPaneWidth, total - (MinimumPaneWidth * (count - 1)));
            var share = Math.Clamp(at, MinimumPaneWidth, widest) / total;
            var shares = new List<double> { share };
            shares.AddRange(others.Select(other => (1 - share) * other / othersSum));
            _widths.Remember(count, shares);
        }

        _hidden = false;
        _widths.RememberLeftmostHidden(false);
        _showing = [];
        Draw();
    }

    /// <summary>Writes down what a drag left, as each pane's share of the strip.</summary>
    private void Remember()
    {
        if (_widths is null)
        {
            return;
        }

        var widths = Widths();
        var total = widths.Sum();

        // A strip that has not been arranged yet measures zero, and a zero share would be a pane that can
        // never be dragged back.
        if (total <= 0 || widths.Any(width => !double.IsFinite(width) || width <= 0))
        {
            return;
        }

        var shares = widths.Select(width => width / total).ToList();

        // With the leftmost pane hidden, the columns share what the hidden pane's share leaves.
        if (_hiding)
        {
            var count = shares.Count + 1;
            var hiddenShare = _widths.Remembered(count)?[0] ?? 1.0 / count;
            shares = [hiddenShare, .. shares.Select(share => share * (1 - hiddenShare))];
        }

        _widths.Remember(shares.Count, shares);
    }

    /// <summary>
    /// Gives this strip's rules a handle the mouse can drag, or the ray when the memory is the headset's,
    /// and remembers where they are left.
    /// </summary>
    public void EnableDrag(PaneWidthMemory memory)
    {
        _widths = memory;
        _hidden = memory.LeftmostHidden;

        // The short-circuit in Draw compares against what is already laid out, and the keys have not changed
        // - only whether they are draggable has.
        _showing = [];
        Draw();
    }

    /// <summary>The surface's one search box, passed down to whichever levels care about it.</summary>
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

    /// <summary>The deepest level showing, whose chrome the strip passes on.</summary>
    private Control? Deepest => _showing.Count > 0 && _built.TryGetValue(_showing[^1], out var pane) ? pane : null;

    public string FilterPlaceholder => (Deepest as IFilterablePage)?.FilterPlaceholder ?? "Search this page";

    public double? FilterWidth => (Deepest as IFilterablePage)?.FilterWidth;

    public Control? BarTool => (Deepest as IPageChrome)?.BarTool;

    public string? HelpTopic => (Deepest as IPageChrome)?.HelpTopic;

    /// <summary>The rightmost pane showing.</summary>
    internal Control? DeepestPane => Deepest;

    /// <summary>The pane showing that holds <paramref name="visual"/>, or null.</summary>
    internal Control? PaneContaining(Visual visual) =>
        _showing
            .Select(key => _built.TryGetValue(key, out var pane) ? pane : null)
            .OfType<Control>()
            .FirstOrDefault(pane => ReferenceEquals(pane, visual) || pane.IsVisualAncestorOf(visual));

    /// <summary>The summary of the deepest level showing that has one.</summary>
    public string Summary =>
        _showing
            .Select(key => _built.TryGetValue(key, out var pane) ? pane : null)
            .OfType<IPageSummary>()
            .LastOrDefault()
            ?.Summary ?? string.Empty;

    /// <summary>The open section of the deepest level showing, or empty when that level has none.</summary>
    public string OpenSection =>
        _showing.Count > 0 && _built.TryGetValue(_showing[^1], out var pane) && pane is IPageSection section
            ? section.OpenSection
            : string.Empty;

    /// <summary>Forgets every level whose key matches.</summary>
    public void ForgetWhere(Func<string, bool> match)
    {
        foreach (var key in _built.Keys.Where(match).ToList())
        {
            _built.Remove(key);
            _focusedRows.Remove(key);
        }

        _showing = [];
        Draw();
    }

    /// <summary>Forgets every level's page and redraws the ones showing.</summary>
    public void ForgetAll() => ForgetWhere(_ => true);

    /// <summary>Forgets a level's page, so the next visit rebuilds it.</summary>
    public void Forget(string key)
    {
        _built.Remove(key);
        _focusedRows.Remove(key);
        _showing = [];
        Draw();
    }
}
