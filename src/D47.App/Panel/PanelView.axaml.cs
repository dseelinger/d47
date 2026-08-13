using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;

namespace D47.App.Panel;

/// <summary>
/// The panel, as a view. It hosts no window, opens no dialog and starts no turn — every one of
/// those is a property of where it is being shown, and the whole point of extracting it is
/// that it does not know.
/// <para>
/// The desktop window instantiates one of these and the VR overlay instantiates another, both
/// against one <see cref="PanelViewModel"/>. What is left in here is what a control genuinely
/// owns: pointer feedback on its own glyph, the Enter key in its own text box, and scrolling
/// its own scroll viewer.
/// </para>
/// </summary>
public partial class PanelView : UserControl
{
    private PanelViewModel? _bound;

    public PanelView()
    {
        InitializeComponent();

        // Scroll position belongs to a rendered surface rather than to the text, so each
        // instance answers this for itself: the window and the overlay can be scrolled to
        // different places and still be showing the same transcript.
        DataContextChanged += (_, _) =>
        {
            if (_bound is not null)
            {
                _bound.TranscriptAppended -= ScrollToEnd;
            }

            _bound = DataContext as PanelViewModel;

            if (_bound is not null)
            {
                _bound.TranscriptAppended += ScrollToEnd;
            }
        };
    }

    private PanelViewModel? Model => DataContext as PanelViewModel;

    /// <summary>The gear, so a host can hang a tooltip naming the bound gesture on it.</summary>
    public Control SettingsAffordance => SettingsButton;

    /// <summary>Puts the cursor in the ask box. The host binds a gesture to it.</summary>
    public void FocusAsk()
    {
        AskBox.Focus();
        AskBox.SelectAll();
    }

    private void ScrollToEnd() => TranscriptScroller.ScrollToEnd();

    private void OnSettingsClick(object? sender, RoutedEventArgs e) => Model?.OpenSettings();

    private void OnSettingsPointerEntered(object? sender, PointerEventArgs e) =>
        SettingsGlyph.Fill = this.FindResource("D47.Accent") as IBrush;

    private void OnSettingsPointerExited(object? sender, PointerEventArgs e) =>
        SettingsGlyph.Fill = this.FindResource("D47.TextMuted") as IBrush;

    private void OnAskClick(object? sender, RoutedEventArgs e) => Model?.Ask();

    private void OnAskBoxKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            e.Handled = true;
            Model?.Ask();
        }
    }

    private void OnUpdateNowClick(object? sender, RoutedEventArgs e) => Model?.AcceptUpdate();

    private void OnUpdateLaterClick(object? sender, RoutedEventArgs e) => Model?.DismissUpdate();
}
