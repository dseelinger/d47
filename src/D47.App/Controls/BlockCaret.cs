using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Media;
using Avalonia.Threading;

namespace D47.App.Controls;

/// <summary>
/// A 9x21 filled block drawn over a <see cref="TextPresenter"/>'s caret position, replacing
/// Avalonia's own thin line caret (#350). Blinks on and off at <see cref="Interval"/> with no
/// fade. Hidden while there is a selection, matching <see cref="TextPresenter"/>'s own caret.
/// </summary>
public sealed class BlockCaret : Control
{
    private static readonly Size BlockSize = new(9, 21);

    public static readonly StyledProperty<TextPresenter?> TargetProperty =
        AvaloniaProperty.Register<BlockCaret, TextPresenter?>(nameof(Target));

    public static readonly StyledProperty<IBrush?> BrushProperty =
        AvaloniaProperty.Register<BlockCaret, IBrush?>(nameof(Brush));

    public static readonly StyledProperty<TimeSpan> IntervalProperty =
        AvaloniaProperty.Register<BlockCaret, TimeSpan>(nameof(Interval), TimeSpan.FromSeconds(1.1));

    public static readonly StyledProperty<bool> IsActiveProperty =
        AvaloniaProperty.Register<BlockCaret, bool>(nameof(IsActive));

    private DispatcherTimer? _timer;
    private bool _visible;

    static BlockCaret()
    {
        AffectsRender<BlockCaret>(BrushProperty);
        IsActiveProperty.Changed.AddClassHandler<BlockCaret>((c, _) => c.OnIsActiveChanged());
        IntervalProperty.Changed.AddClassHandler<BlockCaret>((c, _) => c.OnIsActiveChanged());
        TargetProperty.Changed.AddClassHandler<BlockCaret>((c, e) => c.OnTargetChanged(e.OldValue as TextPresenter, e.NewValue as TextPresenter));
    }

    public TextPresenter? Target
    {
        get => GetValue(TargetProperty);
        set => SetValue(TargetProperty, value);
    }

    public IBrush? Brush
    {
        get => GetValue(BrushProperty);
        set => SetValue(BrushProperty, value);
    }

    public TimeSpan Interval
    {
        get => GetValue(IntervalProperty);
        set => SetValue(IntervalProperty, value);
    }

    public bool IsActive
    {
        get => GetValue(IsActiveProperty);
        set => SetValue(IsActiveProperty, value);
    }

    public override void Render(DrawingContext context)
    {
        if (!_visible || !IsActive || Target is not { } target || Brush is not { } brush)
        {
            return;
        }

        if (target.SelectionStart != target.SelectionEnd)
        {
            return;
        }

        var hit = target.TextLayout.HitTestTextPosition(target.CaretIndex);
        context.FillRectangle(brush, new Rect(hit.Position, BlockSize));
    }

    private void OnTargetChanged(TextPresenter? oldTarget, TextPresenter? newTarget)
    {
        if (oldTarget is not null)
        {
            oldTarget.CaretBoundsChanged -= OnCaretMoved;
        }

        if (newTarget is not null)
        {
            newTarget.CaretBoundsChanged += OnCaretMoved;
        }
    }

    private void OnCaretMoved(object? sender, EventArgs e)
    {
        _visible = true;
        RestartTimer();
        InvalidateVisual();
    }

    private void OnIsActiveChanged()
    {
        if (IsActive)
        {
            _visible = true;
            RestartTimer();
        }
        else
        {
            _timer?.Stop();
            _timer = null;
            _visible = false;
        }

        InvalidateVisual();
    }

    private void RestartTimer()
    {
        _timer?.Stop();

        if (!IsActive)
        {
            return;
        }

        _timer = new DispatcherTimer { Interval = Interval };
        _timer.Tick += (_, _) =>
        {
            _visible = !_visible;
            InvalidateVisual();
        };
        _timer.Start();
    }
}
