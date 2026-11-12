using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.TextFormatting;

namespace D47.App.Panel;

/// <summary>
/// Mini's one-line trail. Fits its words to the room it is measured in and draws them itself, so a layout pass
/// with no layout manager behind it, as on the headset's offscreen surface, still draws every word.
/// </summary>
public sealed class MiniTrailBar : Control
{
    public static readonly StyledProperty<IBrush?> EarlierProperty =
        AvaloniaProperty.Register<MiniTrailBar, IBrush?>(nameof(Earlier));

    public static readonly StyledProperty<IBrush?> SeparatorProperty =
        AvaloniaProperty.Register<MiniTrailBar, IBrush?>(nameof(Separator));

    public static readonly StyledProperty<IBrush?> CurrentProperty =
        AvaloniaProperty.Register<MiniTrailBar, IBrush?>(nameof(Current));

    private static readonly Typeface Face = new(new FontFamily(Theming.Fonts.ChromeFamily), FontStyle.Normal, FontWeight.Medium);

    private static double TextSize => Theming.TypeScale.MetaSmall;

    private IReadOnlyList<string> _words = [];

    private List<(TextLayout Layout, double X)> _drawn = [];

    static MiniTrailBar()
    {
        AffectsRender<MiniTrailBar>(EarlierProperty, SeparatorProperty, CurrentProperty);
        AffectsMeasure<MiniTrailBar>(EarlierProperty, SeparatorProperty, CurrentProperty);
    }

    public IBrush? Earlier
    {
        get => GetValue(EarlierProperty);
        set => SetValue(EarlierProperty, value);
    }

    public IBrush? Separator
    {
        get => GetValue(SeparatorProperty);
        set => SetValue(SeparatorProperty, value);
    }

    public IBrush? Current
    {
        get => GetValue(CurrentProperty);
        set => SetValue(CurrentProperty, value);
    }

    /// <summary>The control to the trail's left; the trail keeps 12 px clear of it.</summary>
    public Control? Beside { get; set; }

    /// <summary>Shows <paramref name="words"/>, tab first, from <see cref="MiniTrail.Words"/>, and names the trail for UI Automation.</summary>
    public void Show(IReadOnlyList<string> words)
    {
        if (_words.SequenceEqual(words))
        {
            return;
        }

        _words = [.. words];
        AutomationProperties.SetName(this, string.Join($" {MiniTrail.Separator} ", _words));
        InvalidateMeasure();
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var room = double.IsInfinity(availableSize.Width)
            ? double.PositiveInfinity
            : availableSize.Width - Reserved();

        var x = 0.0;
        var height = 0.0;

        _drawn = [];

        foreach (var part in MiniTrail.Fit(_words, room, WidthOf))
        {
            var separator = part.Ink == MiniTrailInk.Separator && part.Text == MiniTrail.Separator;
            var layout = Layout(part.Text, part.Ink switch
            {
                MiniTrailInk.Earlier => Earlier,
                MiniTrailInk.Separator => Separator,
                _ => Current,
            });

            x += separator ? MiniTrail.SeparatorGap : 0;
            _drawn.Add((layout, x));
            x += layout.WidthIncludingTrailingWhitespace + (separator ? MiniTrail.SeparatorGap : 0);
            height = Math.Max(height, layout.Height);
        }

        InvalidateVisual();

        return new Size(x, height);
    }

    public override void Render(DrawingContext context)
    {
        foreach (var (layout, x) in _drawn)
        {
            layout.Draw(context, new Point(x, (Bounds.Height - layout.Height) / 2));
        }
    }

    private double Reserved() =>
        Beside is { IsVisible: true } beside ? beside.Margin.Left + beside.DesiredSize.Width + 12 : 0;

    private static TextLayout Layout(string text, IBrush? ink) =>
        new(text, Face, TextSize, ink ?? Brushes.Transparent, letterSpacing: TextSize * 0.08);

    private static double WidthOf(string text)
    {
        using var layout = Layout(text, null);
        return layout.WidthIncludingTrailingWhitespace;
    }
}
