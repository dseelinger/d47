using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using D47.Core.Interface;

namespace D47.App.Panel;

/// <summary>
/// A tab's drill stack, drawn as however many panes will fit (Phase 25, "Drill in, and find your way
/// back" and "The panel resizes and zooms").
/// </summary>
public sealed class DrillView : UserControl, IFilterablePage
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

    private readonly PanelNavigator _nav;
    private readonly PanelTab _tab;
    private readonly Func<NavCrumb, Control> _build;
    private readonly Grid _strip = new();

    /// <summary>What each level drew, kept by crumb key.</summary>
    private readonly Dictionary<string, Control> _built = [];

    /// <summary>What is currently laid out, so a redraw that changes nothing does nothing.</summary>
    private IReadOnlyList<string> _showing = [];

    private int _panes = 1;

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

    /// <summary>How many panes the strip is currently showing.</summary>
    public int Panes => _panes;

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

        // Re-applied on every draw and not only on the first, which is the trap this phase names: Empty()
        // clears the column definitions on every navigation, so a width the Commander dragged would otherwise
        // be discarded the moment they open a ship.
        var shares = _widths?.Remembered(visible.Count);

        for (var index = 0; index < visible.Count; index++)
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

            var crumb = visible[index];

            if (!_built.TryGetValue(crumb.Key, out var pane))
            {
                pane = _build(crumb);
                _built[crumb.Key] = pane;
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
                this.GetResourceObservable(Theming.ThemeManager.BorderKey));

            Grid.SetColumn(host, index);
            _strip.Children.Add(host);

            // After the pane, so it is above it in z-order and the pointer reaches it rather than the page
            // underneath.
            if (_widths is not null && index > 0)
            {
                _strip.Children.Add(Handle(index));
            }
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
        handle.DragCompleted += (_, _) => Remember();

        Grid.SetColumn(handle, index);
        return handle;
    }

    /// <summary>Writes down what a drag left, as each pane's share of the strip.</summary>
    private void Remember()
    {
        if (_widths is null)
        {
            return;
        }

        var widths = _strip.ColumnDefinitions.Select(column => column.ActualWidth).ToList();
        var total = widths.Sum();

        // A strip that has not been arranged yet measures zero, and a zero share would be a pane that can
        // never be dragged back.
        if (total <= 0 || widths.Any(width => !double.IsFinite(width) || width <= 0))
        {
            return;
        }

        _widths.Remember(widths.Count, widths.Select(width => width / total).ToList());
    }

    /// <summary>
    /// Gives this strip's rules a handle the mouse can drag, and remembers where they are left (Phase
    /// 55).
    /// </summary>
    public void EnableDrag(PaneWidthMemory memory)
    {
        _widths = memory;

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

    /// <summary>Forgets a level's page, so the next visit rebuilds it.</summary>
    public void Forget(string key)
    {
        _built.Remove(key);
        _showing = [];
        Draw();
    }
}
