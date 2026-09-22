using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Media;
using Avalonia.Metadata;
using Avalonia.Reactive;
using Avalonia.VisualTree;
using D47.App.Controls;
using D47.Core.Interface;
using Path = Avalonia.Controls.Shapes.Path;

namespace D47.App.Theming;

/// <summary>
/// Draws <see cref="Child"/> over one ghost copy of its shape per stop of <see cref="Tier"/>, each
/// ghost carrying that stop's glow. The ghosts are siblings, not nested, so the stops add rather
/// than blurring each other. A ghost whose stop resource is null — Light, or amount 0 — is not drawn.
/// A templated control clips to its bounds by default, so one that hosts a stack in its template sets
/// <see cref="Visual.ClipToBounds"/> false or its halo stops at its edge.
/// </summary>
public sealed class BloomStack : Control
{
    public static readonly StyledProperty<Control?> ChildProperty =
        AvaloniaProperty.Register<BloomStack, Control?>(nameof(Child));

    public static readonly StyledProperty<BloomTier> TierProperty =
        AvaloniaProperty.Register<BloomStack, BloomTier>(nameof(Tier), BloomTier.Normal);

    /// <summary>False hides every ghost; a template sets it from the control's state.</summary>
    public static readonly StyledProperty<bool> IsLitProperty =
        AvaloniaProperty.Register<BloomStack, bool>(nameof(IsLit), true);

    /// <summary>The glow's colour when it is not Accent; only a solid brush's colour is read.</summary>
    public static readonly StyledProperty<IBrush?> GlowProperty =
        AvaloniaProperty.Register<BloomStack, IBrush?>(nameof(Glow));

    /// <summary>
    /// True clips each ghost to outside the child's rectangle, so only the glow draws and not the
    /// ghost's own fill — for a child whose shape is never meant to be seen.
    /// </summary>
    public static readonly StyledProperty<bool> IsHollowProperty =
        AvaloniaProperty.Register<BloomStack, bool>(nameof(IsHollow));

    private const double HollowReach = 200;

    private readonly List<Control> _ghosts = [];

    private readonly List<IDisposable> _links = [];

    static BloomStack()
    {
        AffectsMeasure<BloomStack>(ChildProperty);
        AffectsArrange<BloomStack>(IsHollowProperty);
    }

    [Content]
    public Control? Child
    {
        get => GetValue(ChildProperty);
        set => SetValue(ChildProperty, value);
    }

    public BloomTier Tier
    {
        get => GetValue(TierProperty);
        set => SetValue(TierProperty, value);
    }

    public bool IsLit
    {
        get => GetValue(IsLitProperty);
        set => SetValue(IsLitProperty, value);
    }

    public IBrush? Glow
    {
        get => GetValue(GlowProperty);
        set => SetValue(GlowProperty, value);
    }

    public bool IsHollow
    {
        get => GetValue(IsHollowProperty);
        set => SetValue(IsHollowProperty, value);
    }

    /// <summary>The ghosts, backmost and narrowest first.</summary>
    public IReadOnlyList<Control> Ghosts => _ghosts;

    /// <summary>Whether <paramref name="visual"/> is a stack's ghost rather than content of its own.</summary>
    public static bool IsGhost(Visual visual) =>
        visual.GetVisualParent() is BloomStack stack && stack._ghosts.Contains(visual);

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == ChildProperty)
        {
            if (change.OldValue is Control old)
            {
                LogicalChildren.Remove(old);
                VisualChildren.Remove(old);
            }

            if (change.NewValue is Control added)
            {
                VisualChildren.Add(added);
                LogicalChildren.Add(added);
            }

            Rebuild();
        }
        else if (change.Property == TierProperty)
        {
            Rebuild();
        }
        else if (change.Property == IsLitProperty || change.Property == GlowProperty)
        {
            Relight();
        }
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        Rebuild();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        Unlink();
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        if (Child is not { } child)
        {
            return default;
        }

        child.Measure(availableSize);

        foreach (var ghost in _ghosts)
        {
            ghost.Measure(availableSize);
        }

        return child.DesiredSize;
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        if (Child is not { } child)
        {
            return finalSize;
        }

        child.Arrange(new Rect(finalSize));

        // Each ghost takes the child's own arranged rectangle, margin and alignment already applied.
        var hollow = IsHollow ? Hollow(child.Bounds.Size) : null;

        foreach (var ghost in _ghosts)
        {
            ghost.Arrange(child.Bounds);
            ghost.Clip = hollow;
        }

        return finalSize;
    }

    /// <summary>Everything within <see cref="HollowReach"/> of a rectangle of <paramref name="size"/>, less the rectangle.</summary>
    private static Geometry Hollow(Size size)
    {
        var inner = new Rect(size);

        return new CombinedGeometry(
            GeometryCombineMode.Exclude,
            new RectangleGeometry(inner.Inflate(HollowReach)),
            new RectangleGeometry(inner));
    }

    private void Rebuild()
    {
        Unlink();

        foreach (var ghost in _ghosts)
        {
            VisualChildren.Remove(ghost);
        }

        _ghosts.Clear();

        if (Child is not { } child || VisualRoot is null)
        {
            return;
        }

        for (var i = 0; i < BloomTiers.Table(Tier).Count; i++)
        {
            var ghost = Ghost(child);
            ghost.IsHitTestVisible = false;
            ghost.IsVisible = false;
            ghost.ClipToBounds = false;
            AutomationProperties.SetAccessibilityView(ghost, AccessibilityView.Raw);

            _ghosts.Add(ghost);
            VisualChildren.Insert(i, ghost);

            var stop = i;
            _links.Add(this.GetResourceObservable(ThemeManager.BloomStopKey(Tier, stop))
                .Subscribe(new AnonymousObserver<object?>(_ => Relight())));
        }

        InvalidateMeasure();
    }

    private void Unlink()
    {
        foreach (var link in _links)
        {
            link.Dispose();
        }

        _links.Clear();
    }

    /// <summary>Sets each ghost's effect from its stop resource and shows it only while lit.</summary>
    private void Relight()
    {
        var colour = (Glow as ISolidColorBrush)?.Color;

        for (var i = 0; i < _ghosts.Count; i++)
        {
            var effect = this.TryFindResource(ThemeManager.BloomStopKey(Tier, i), out var found)
                ? found as DropShadowEffect
                : null;

            if (effect is not null && colour is { } glow)
            {
                effect = new DropShadowEffect
                {
                    Color = glow,
                    OffsetX = 0,
                    OffsetY = 0,
                    BlurRadius = effect.BlurRadius,
                    Opacity = effect.Opacity,
                };
            }

            _ghosts[i].Effect = effect;
            _ghosts[i].IsVisible = IsLit && effect is not null;
        }
    }

    /// <summary>
    /// An empty copy of <paramref name="child"/>'s shape, its geometry, fill and outline bound to the
    /// child's. A ghost draws its own fill and outline as well as the glow, so it must sit exactly
    /// under the child's, or be <see cref="IsHollow"/>.
    /// </summary>
    private static Control Ghost(Control child)
    {
        switch (child)
        {
            case ChamferedBorder chamfered:
            {
                var ghost = new ChamferedBorder();
                Follow(ghost, ChamferedBorder.BackgroundProperty, chamfered);
                Follow(ghost, ChamferedBorder.BorderBrushProperty, chamfered);
                Follow(ghost, ChamferedBorder.BorderThicknessProperty, chamfered);
                Follow(ghost, ChamferedBorder.ChamferProperty, chamfered);
                Follow(ghost, ChamferedBorder.SkewProperty, chamfered);
                return ghost;
            }

            case Border border:
            {
                var ghost = new Border();
                Follow(ghost, Border.BackgroundProperty, border);
                Follow(ghost, Border.CornerRadiusProperty, border);
                return ghost;
            }

            case Ellipse ellipse:
            {
                var ghost = new Ellipse();
                Follow(ghost, Shape.FillProperty, ellipse);
                return ghost;
            }

            case Rectangle rectangle:
            {
                var ghost = new Rectangle();
                Follow(ghost, Shape.FillProperty, rectangle);
                Follow(ghost, Shape.StrokeProperty, rectangle);
                Follow(ghost, Shape.StrokeThicknessProperty, rectangle);
                return ghost;
            }

            case Path path:
            {
                var ghost = new Path();
                Follow(ghost, Path.DataProperty, path);
                Follow(ghost, Shape.FillProperty, path);
                Follow(ghost, Shape.StretchProperty, path);
                return ghost;
            }

            case TextBlock text:
            {
                var ghost = new TextBlock();
                Follow(ghost, TextBlock.TextProperty, text);
                Follow(ghost, TextBlock.ForegroundProperty, text);
                Follow(ghost, TextBlock.FontFamilyProperty, text);
                Follow(ghost, TextBlock.FontSizeProperty, text);
                Follow(ghost, TextBlock.FontWeightProperty, text);
                Follow(ghost, TextBlock.FontStyleProperty, text);
                Follow(ghost, TextBlock.FontStretchProperty, text);
                Follow(ghost, TextBlock.LetterSpacingProperty, text);
                Follow(ghost, TextBlock.LineHeightProperty, text);
                Follow(ghost, TextBlock.PaddingProperty, text);
                Follow(ghost, TextBlock.TextAlignmentProperty, text);
                Follow(ghost, TextBlock.TextWrappingProperty, text);
                Follow(ghost, TextBlock.TextTrimmingProperty, text);
                return ghost;
            }

            default:
                throw new NotSupportedException($"No bloom ghost for {child.GetType().Name}.");
        }
    }

    private static void Follow<T>(Control ghost, StyledProperty<T> property, Control source) =>
        ghost.Bind(property, source.GetObservable(property));
}
