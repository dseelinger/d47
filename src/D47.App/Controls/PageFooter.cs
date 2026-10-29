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

    /// <summary>A brief page's root: <paramref name="body"/> scrolled above the footer, in a <paramref name="maxWidth"/> column when given.</summary>
    public static DockPanel Shell(Control body, string phrase, JournalClock clock, double? maxWidth = null) =>
        Shell(null, body, phrase, clock, maxWidth);

    /// <summary>As above, with <paramref name="sidebar"/> docked to the left of the scrolled body.</summary>
    public static DockPanel Shell(Control? sidebar, Control body, string phrase, JournalClock clock, double? maxWidth = null)
    {
        var root = new DockPanel { Margin = new Thickness(14) };
        var footer = new PageFooter(phrase, clock) { Margin = new Thickness(0, 12, 0, 0) };
        DockPanel.SetDock(footer, Dock.Bottom);
        root.Children.Add(footer);

        var scrolled = D47.App.Panel.LoadoutPages.Scrolling(maxWidth is { } width
            ? new Grid
            {
                ColumnDefinitions = [new ColumnDefinition(GridLength.Star) { MaxWidth = width }, new ColumnDefinition(GridLength.Auto)],
                Children = { body },
            }
            : body);

        if (sidebar is null)
        {
            root.Children.Add(scrolled);
            return root;
        }

        var columns = new DockPanel();
        DockPanel.SetDock(sidebar, Dock.Left);
        columns.Children.Add(sidebar);
        columns.Children.Add(scrolled);
        root.Children.Add(columns);

        return root;
    }

    public PageFooter(string phrase, JournalClock clock)
    {
        _clock = clock;

        BorderThickness = new Thickness(0, 1, 0, 0);
        Padding = new Thickness(0, 12, 0, 0);
        ThemeInk.Bind(this, BorderBrushProperty, ThemeManager.Line2Key);

        var said = new TextBlock
        {
            Text = $"Say: “{phrase}”",
            FontFamily = Fonts.ProseFamily,
            FontSize = TypeScale.Tip,
            VerticalAlignment = VerticalAlignment.Center,
            TextTrimming = TextTrimming.CharacterEllipsis,
        };
        ThemeInk.Bind(said, TextBlock.ForegroundProperty, ThemeManager.GreyKey);

        var label = new TextBlock
        {
            Text = KeptCurrent.ToUpperInvariant(),
            FontFamily = Fonts.ChromeFamily,
            FontSize = TypeScale.Meta,
            FontWeight = FontWeight.Medium,
            LetterSpacing = TypeScale.Meta * Fonts.ChromeTracking,
            VerticalAlignment = VerticalAlignment.Center,
        };
        ThemeInk.Bind(label, TextBlock.ForegroundProperty, ThemeManager.GreyKey);

        _time = new TextBlock
        {
            FontFamily = new FontFamily(Fonts.MonoFamily),
            FontSize = TypeScale.Meta,
            VerticalAlignment = VerticalAlignment.Center,
        };
        ThemeInk.Bind(_time, TextBlock.ForegroundProperty, ThemeManager.CyanKey);

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

}
