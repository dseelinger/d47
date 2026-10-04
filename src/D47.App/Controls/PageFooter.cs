using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using D47.App.Theming;

namespace D47.App.Controls;

/// <summary>When the journal last moved the game state on, and a nudge when it moves again.</summary>
public sealed class JournalClock(Func<DateTimeOffset?> lastEventAt)
{
    private DateTimeOffset? _seen = lastEventAt();

    /// <summary>Raised by <see cref="Tick"/> when a journal event has been applied since the last call.</summary>
    public event Action? Changed;

    /// <summary>The timestamp of the last journal event applied, or null before the first.</summary>
    public DateTimeOffset? LastEventAt => lastEventAt();

    /// <summary>Raises <see cref="Changed"/> if <see cref="LastEventAt"/> has moved; call on the UI thread.</summary>
    public bool Tick()
    {
        var now = lastEventAt();

        if (now == _seen)
        {
            return false;
        }

        _seen = now;
        Changed?.Invoke();

        return true;
    }
}

/// <summary>
/// The line along the bottom of a brief screen, above a <c>line2</c> rule: the say-line on the left and
/// <c>KEPT CURRENT BY THE JOURNAL · HH:MM</c> on the right, the time in mono cyan. The right side is not
/// drawn before the journal has applied an event.
/// </summary>
public sealed class PageFooter : Border
{
    public const string KeptCurrent = "Kept current by the journal · ";

    private readonly JournalClock _clock;
    private readonly StackPanel _stamp;
    private readonly TextBlock _time;

    public PageFooter(string phrase, JournalClock clock)
    {
        _clock = clock;

        BorderThickness = new Thickness(0, 1, 0, 0);
        Padding = new Thickness(0, 12, 0, 0);
        Themed(this, BorderBrushProperty, ThemeManager.Line2Key);

        var said = new TextBlock
        {
            Text = $"Say: “{phrase}”",
            FontFamily = Fonts.ProseFamily,
            FontSize = TypeScale.Tip,
            VerticalAlignment = VerticalAlignment.Center,
            TextTrimming = TextTrimming.CharacterEllipsis,
        };
        Themed(said, TextBlock.ForegroundProperty, ThemeManager.GreyKey);

        var label = new TextBlock
        {
            Text = KeptCurrent.ToUpperInvariant(),
            FontFamily = Fonts.ChromeFamily,
            FontSize = TypeScale.Meta,
            FontWeight = FontWeight.Medium,
            LetterSpacing = TypeScale.Meta * Fonts.ChromeTracking,
            VerticalAlignment = VerticalAlignment.Center,
        };
        Themed(label, TextBlock.ForegroundProperty, ThemeManager.GreyKey);

        _time = new TextBlock
        {
            FontFamily = new FontFamily(Fonts.MonoFamily),
            FontSize = TypeScale.Meta,
            VerticalAlignment = VerticalAlignment.Center,
        };
        Themed(_time, TextBlock.ForegroundProperty, ThemeManager.CyanKey);

        _stamp = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Margin = new Thickness(16, 0, 0, 0),
            VerticalAlignment = VerticalAlignment.Center,
            Children = { label, _time },
        };

        var layout = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto") };
        Grid.SetColumn(_stamp, 1);
        layout.Children.Add(said);
        layout.Children.Add(_stamp);

        Child = layout;
        Refresh();
    }

    /// <summary>The time shown, or null when the right side is not drawn.</summary>
    internal string? Time => _stamp.IsVisible ? _time.Text : null;

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _clock.Changed += Refresh;
        Refresh();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        _clock.Changed -= Refresh;
        base.OnDetachedFromVisualTree(e);
    }

    private void Refresh()
    {
        var at = _clock.LastEventAt;

        _stamp.IsVisible = at is not null;
        _time.Text = at?.ToLocalTime().ToString("HH:mm", CultureInfo.InvariantCulture);
    }

    private static IDisposable Themed(StyledElement target, AvaloniaProperty property, string key) =>
        target.Bind(property, target.GetResourceObservable(key));
}
