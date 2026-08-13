using Avalonia;
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
    /// <summary>
    /// How much of the panel this instantiation shows. A property of the surface rather than of
    /// the content, so the desktop window can be full while the headset is mini and both are
    /// still showing the same transcript.
    /// </summary>
    public static readonly StyledProperty<PanelMode> ModeProperty =
        AvaloniaProperty.Register<PanelView, PanelMode>(nameof(Mode));

    private PanelViewModel? _bound;

    public PanelView()
    {
        InitializeComponent();

        // Set in code rather than bound, because what mini hides is three named regions and a
        // binding for each would be three expressions no test can reach. The content inside
        // them still binds - a banner is hidden in mini and also hidden when there is nothing
        // wrong, and those are different reasons.
        ModeProperty.Changed.AddClassHandler<PanelView>((view, _) => view.ApplyMode());
        ApplyMode();

        // Scroll position belongs to a rendered surface rather than to the text, so each
        // instance answers this for itself: the window and the overlay can be scrolled to
        // different places and still be showing the same transcript.
        DataContextChanged += (_, _) =>
        {
            if (_bound is not null)
            {
                _bound.TranscriptAppended -= ScrollToEnd;
                _bound.PropertyChanged -= OnModelChanged;
            }

            _bound = DataContext as PanelViewModel;

            if (_bound is not null)
            {
                _bound.TranscriptAppended += ScrollToEnd;

                // The avatar follows the loop state. Subscribed per instance rather than bound
                // in XAML because the control takes a state rather than exposing a settable
                // property — it has frames to load and an animation to swap, and doing that
                // from a setter the binding engine drives is how you get both on every tick.
                _bound.PropertyChanged += OnModelChanged;
                Avatar.Show(_bound.LoopState);
            }
        };
    }

    public PanelMode Mode
    {
        get => GetValue(ModeProperty);
        set => SetValue(ModeProperty, value);
    }

    private PanelViewModel? Model => DataContext as PanelViewModel;

    private void OnModelChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(PanelViewModel.LoopState) && _bound is not null)
        {
            Avatar.Show(_bound.LoopState);
        }
    }

    /// <summary>The gear, so a host can hang a tooltip naming the bound gesture on it.</summary>
    public Control SettingsAffordance => SettingsButton;

    /// <summary>Puts the cursor in the ask box. The host binds a gesture to it.</summary>
    public void FocusAsk()
    {
        AskBox.Focus();
        AskBox.SelectAll();
    }

    private void ApplyMode()
    {
        var full = Mode == PanelMode.Full;

        Header.IsVisible = full;
        Banners.IsVisible = full;
        AskRow.IsVisible = full;
    }

    private void ScrollToEnd() => TranscriptScroller.ScrollToEnd();

    private void OnSettingsClick(object? sender, RoutedEventArgs e) => Model?.OpenSettings();

    private void OnSettingsPointerEntered(object? sender, PointerEventArgs e) =>
        SettingsGlyph.Fill = this.FindResource("D47.Accent") as IBrush;

    private void OnSettingsPointerExited(object? sender, PointerEventArgs e) =>
        SettingsGlyph.Fill = this.FindResource("D47.TextMuted") as IBrush;

    private void OnHelpClick(object? sender, RoutedEventArgs e) => Model?.OpenHelp();

    private void OnHelpPointerEntered(object? sender, PointerEventArgs e) =>
        HelpGlyph.Stroke = this.FindResource("D47.Accent") as IBrush;

    private void OnHelpPointerExited(object? sender, PointerEventArgs e) =>
        HelpGlyph.Stroke = this.FindResource("D47.TextMuted") as IBrush;

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
