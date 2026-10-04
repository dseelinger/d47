using System.Globalization;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using D47.App.Theming;
using D47.Core.Stories;

namespace D47.App.Controls;

/// <summary>
/// Five stars: filled in the theme accent up to <see cref="Value"/>, a half star filled on its left half, the rest
/// outlined in Grey2. The interactive form fills to the pointer on hover, sets the value on a click, clears it on a click
/// at the current value, and takes Left, Right and Delete.
/// </summary>
public sealed class StarRating : Control
{
    /// <summary>The stars shown, 0 to 5 in halves; 0 draws five outlines.</summary>
    public static readonly StyledProperty<double> ValueProperty =
        AvaloniaProperty.Register<StarRating, double>(nameof(Value));

    public static readonly StyledProperty<IBrush?> FillProperty =
        AvaloniaProperty.Register<StarRating, IBrush?>(nameof(Fill));

    public static readonly StyledProperty<IBrush?> EmptyProperty =
        AvaloniaProperty.Register<StarRating, IBrush?>(nameof(Empty));

    public const double ReadOnlySize = 12;

    public const double InteractiveSize = 20;

    /// <summary>One star in a unit square, scaled to each star's size.</summary>
    private static readonly StreamGeometry Star = UnitStar();

    private readonly double _size;
    private readonly double _gap;
    private int? _hover;

    static StarRating() => AffectsRender<StarRating>(ValueProperty, FillProperty, EmptyProperty);

    private StarRating(double size, bool interactive)
    {
        _size = size;
        _gap = Math.Round(size / 5);
        IsInteractive = interactive;
        Width = (5 * size) + (4 * _gap);
        Height = size;
        HorizontalAlignment = HorizontalAlignment.Left;
        VerticalAlignment = VerticalAlignment.Center;
        Focusable = interactive;
        IsTabStop = interactive;

        if (interactive)
        {
            Cursor = new Cursor(StandardCursorType.Hand);
        }

        if (Application.Current is { } app)
        {
            Bind(FillProperty, app.Resources.GetResourceObservable(ThemeManager.AKey));
            Bind(EmptyProperty, app.Resources.GetResourceObservable(ThemeManager.Grey2Key));
        }

        Describe();
    }

    /// <summary>Raised when the Commander sets or clears the value: 1 to 5, or null.</summary>
    public event Action<int?>? Rated;

    public bool IsInteractive { get; }

    public double Value
    {
        get => GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    public IBrush? Fill
    {
        get => GetValue(FillProperty);
        set => SetValue(FillProperty, value);
    }

    public IBrush? Empty
    {
        get => GetValue(EmptyProperty);
        set => SetValue(EmptyProperty, value);
    }

    public const string Unrated = "(No ratings yet)";

    /// <summary>
    /// The 12 px read-only stars of <paramref name="rating"/> with its vote count beside them in mono; five empty stars and
    /// <see cref="Unrated"/> when there is no rating.
    /// </summary>
    public static StackPanel Summary(StoryRating? rating)
    {
        var stars = new StarRating(ReadOnlySize, interactive: false) { Value = rating?.Stars ?? 0 };
        var count = new TextBlock
        {
            Text = rating is null ? Unrated : string.Create(CultureInfo.InvariantCulture, $"({rating.Count})"),
            FontSize = TypeScale.Small,
            VerticalAlignment = VerticalAlignment.Center,
        };

        if (rating is not null)
        {
            count.FontFamily = new FontFamily(Fonts.MonoFamily);
        }

        if (Application.Current is { } app)
        {
            count.Bind(TextBlock.ForegroundProperty, app.Resources.GetResourceObservable(ThemeManager.GreyKey));
        }

        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, Margin = new Thickness(0, 2, 0, 2) };
        row.Children.Add(stars);
        row.Children.Add(count);
        AutomationProperties.SetName(row, rating is null
            ? "No ratings yet"
            : string.Create(CultureInfo.InvariantCulture, $"Rated {rating.Stars} of 5 stars by {rating.Count}"));
        return row;
    }

    /// <summary>The 20 px stars the Commander rates with.</summary>
    public static StarRating Interactive(int? rating) => new(InteractiveSize, interactive: true) { Value = rating ?? 0 };

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == ValueProperty)
        {
            Describe();
        }
        else if (change.Property == IsFocusedProperty)
        {
            InvalidateVisual();
        }
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);

        if (IsInteractive)
        {
            Hover(StarAt(e.GetPosition(this).X));
        }
    }

    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        Hover(null);
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);

        if (!IsInteractive || !e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            return;
        }

        e.Handled = true;
        Focus(NavigationMethod.Pointer);

        var star = StarAt(e.GetPosition(this).X);
        Set(star == Current ? null : star);
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);

        if (!IsInteractive)
        {
            return;
        }

        int? next = e.Key switch
        {
            Key.Right => Math.Min(5, (Current ?? 0) + 1),
            Key.Left when Current is { } now => Math.Max(1, now - 1),
            Key.Delete or Key.Back when Current is not null => null,
            _ => Current,
        };

        if (next != Current)
        {
            e.Handled = true;
            Set(next);
        }
    }

    public override void Render(DrawingContext context)
    {
        var shown = _hover ?? Value;
        var outline = new Pen(Empty, 1.25 / _size);

        for (var i = 0; i < 5; i++)
        {
            var left = i * (_size + _gap);

            using (context.PushTransform(Matrix.CreateScale(_size, _size) * Matrix.CreateTranslation(left, 0)))
            {
                if (shown >= i + 1)
                {
                    context.DrawGeometry(Fill, null, Star);
                    continue;
                }

                context.DrawGeometry(null, outline, Star);

                if (shown >= i + 0.5)
                {
                    using (context.PushClip(new Rect(0, 0, 0.5, 1)))
                    {
                        context.DrawGeometry(Fill, null, Star);
                    }
                }
            }
        }

        if (IsInteractive && IsFocused && Fill is { } accent)
        {
            context.DrawRectangle(null, new Pen(accent, 1), new Rect(Bounds.Size).Inflate(2));
        }
    }

    private int? Current => Value >= 1 ? (int)Math.Round(Value) : null;

    private int StarAt(double x) => Math.Clamp((int)(x / (_size + _gap)) + 1, 1, 5);

    private void Hover(int? star)
    {
        if (_hover != star)
        {
            _hover = star;
            InvalidateVisual();
        }
    }

    private void Set(int? stars)
    {
        Value = stars ?? 0;
        Rated?.Invoke(stars);
    }

    private void Describe()
    {
        if (IsInteractive)
        {
            AutomationProperties.SetName(this, Current is { } stars
                ? string.Create(CultureInfo.InvariantCulture, $"Your rating, {stars} of 5 stars")
                : "Your rating, not rated");
        }
    }

    private static StreamGeometry UnitStar()
    {
        const double outer = 0.5;
        const double inner = outer * 0.382;
        var drop = (1 - (outer + (outer * Math.Cos(Math.PI / 5)))) / 2;
        var geometry = new StreamGeometry();

        using (var draw = geometry.Open())
        {
            for (var k = 0; k < 10; k++)
            {
                var radius = k % 2 == 0 ? outer : inner;
                var angle = (k * Math.PI / 5) - (Math.PI / 2);
                var point = new Point(0.5 + (radius * Math.Cos(angle)), 0.5 + drop + (radius * Math.Sin(angle)));

                if (k == 0)
                {
                    draw.BeginFigure(point, isFilled: true);
                }
                else
                {
                    draw.LineTo(point);
                }
            }

            draw.EndFigure(isClosed: true);
        }

        return geometry;
    }
}
