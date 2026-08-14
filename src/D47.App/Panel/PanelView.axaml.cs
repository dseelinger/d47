using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Threading;

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

    /// <summary>
    /// Which page of the transcript this instantiation is showing. Also a property of the
    /// surface: the window can be reading the log while the headset shows the conversation.
    /// </summary>
    public static readonly StyledProperty<TranscriptPage> PageProperty =
        AvaloniaProperty.Register<PanelView, TranscriptPage>(nameof(Page));

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

        PageProperty.Changed.AddClassHandler<PanelView>((view, _) => view.ApplyPage());
        ApplyPage();

        // Scroll position belongs to a rendered surface rather than to the text, so each
        // instance answers this for itself: the window and the overlay can be scrolled to
        // different places and still be showing the same transcript.
        DataContextChanged += (_, _) =>
        {
            if (_bound is not null)
            {
                _bound.TranscriptAppended -= DrawTranscript;
                _bound.TranscriptAppended -= ScrollToEnd;
                _bound.PropertyChanged -= OnModelChanged;
            }

            _bound = DataContext as PanelViewModel;

            if (_bound is not null)
            {
                // Drawn before the scroll, because scrolling to the end of text that has not
                // been written yet lands one append behind.
                _bound.TranscriptAppended += DrawTranscript;
                _bound.TranscriptAppended += ScrollToEnd;

                // The avatar follows the loop state. Subscribed per instance rather than bound
                // in XAML because the control takes a state rather than exposing a settable
                // property — it has frames to load and an animation to swap, and doing that
                // from a setter the binding engine drives is how you get both on every tick.
                _bound.PropertyChanged += OnModelChanged;
                Avatar.Show(_bound.LoopState);
            }

            // The model handed over is rarely empty — the window binds one that has already
            // been written to — and nothing else would redraw until the next append.
            DrawTranscript();
        };
    }

    public PanelMode Mode
    {
        get => GetValue(ModeProperty);
        set => SetValue(ModeProperty, value);
    }

    public TranscriptPage Page
    {
        get => GetValue(PageProperty);
        set => SetValue(PageProperty, value);
    }

    private PanelViewModel? Model => DataContext as PanelViewModel;

    private void OnModelChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(PanelViewModel.LoopState) || _bound is null)
        {
            return;
        }

        // Marshalled here for the same reason ScrollToEnd is, and following the same rule: the
        // view owns thread affinity, so a new caller does not have to learn it separately. Loop
        // states are raised from the turn's own thread and from the audio path, and neither is
        // the one that owns these controls. Posted only when it has to be.
        if (Dispatcher.UIThread.CheckAccess())
        {
            Avatar.Show(_bound.LoopState);
            return;
        }

        var state = _bound.LoopState;
        Dispatcher.UIThread.Post(() => Avatar.Show(state));
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

        // Mini is "the transcript's tail and the provenance line" and nothing else, so the tabs
        // go with the rest of the chrome. A surface with 640x280 to spend does not spend it on
        // three page selectors.
        TranscriptTabs.IsVisible = full;
    }

    /// <summary>
    /// Points the transcript at the page this surface is showing, and checks the tab that says
    /// so - which keeps a page set in code and a page set by a click on one path.
    /// </summary>
    private void ApplyPage()
    {
        var tab = Page switch
        {
            TranscriptPage.Technical => TechnicalTab,
            TranscriptPage.Log => LogTab,
            _ => ConversationTab,
        };

        tab.IsChecked = true;

        // Read when the page is opened rather than on a timer. A log nobody is looking at is
        // not worth a file read per tick, and one being looked at is being looked at because
        // something has already gone wrong.
        if (Page == TranscriptPage.Log)
        {
            _bound?.RefreshLog();
        }

        DrawTranscript();
    }

    /// <summary>
    /// Writes the current page into the transcript block, as one run per stretch that is drawn
    /// the same way.
    /// <para>
    /// Runs rather than a bound string, which is what this was. A marked line — the panel
    /// noting that the core changed — has to be drawn differently from the conversation around
    /// it, and one <c>Text</c> binding has no way to say that. The colour is taken as a
    /// resource observable rather than read once, so a marker written under one theme is still
    /// the accent after the Commander switches to another.
    /// </para>
    /// </summary>
    private void DrawTranscript()
    {
        if (!Dispatcher.UIThread.CheckAccess())
        {
            Dispatcher.UIThread.Post(DrawTranscript);
            return;
        }

        var inlines = Transcript.Inlines ??= [];
        inlines.Clear();

        if (_bound is null)
        {
            return;
        }

        foreach (var segment in _bound.Segments(Page))
        {
            var run = new Run(segment.Text);

            if (segment.Marker)
            {
                run.Bind(
                    Avalonia.Controls.Documents.TextElement.ForegroundProperty,
                    this.GetResourceObservable(Theming.ThemeManager.AccentKey));
                run.FontWeight = FontWeight.SemiBold;
            }

            inlines.Add(run);
        }
    }

    /// <summary>
    /// Re-asserts the page once this view is actually on screen.
    /// <para>
    /// Set in the constructor as well, and that should be enough. It was not: a Commander
    /// reported the window opening with no tab marked at all until one was clicked, and this
    /// view is built, reparented by the zoom host, and instantiated a second time by the
    /// headset before any of it is shown - all before the strip is ever rendered. Rather than
    /// guess which of those drops it, the page is stated again at the one moment that is
    /// definitely after all of them.
    /// </para>
    /// <para>
    /// Cheap and idempotent: it checks the tab that is already meant to be checked and rebinds
    /// the transcript to the property it is already bound to.
    /// </para>
    /// </summary>
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        ApplyPage();
    }

    private void OnPageTabChecked(object? sender, RoutedEventArgs e)
    {
        // Fires for the tab being cleared as well as the one being set, and only the set one
        // says anything about which page to show.
        if (sender is not RadioButton { IsChecked: true } tab)
        {
            return;
        }

        Page = tab == TechnicalTab ? TranscriptPage.Technical
            : tab == LogTab ? TranscriptPage.Log
            : TranscriptPage.Conversation;
    }

    /// <summary>
    /// Follows the transcript, from whichever thread grew it.
    /// <para>
    /// A turn's events do not arrive on the UI thread. <c>VoicePipeline</c> consumes them with
    /// <c>ConfigureAwait(false)</c>, so once the first network await has suspended, every delta
    /// after it is delivered on a thread pool thread — and a scroll viewer is thread-affine, so
    /// calling it there threw and took the whole turn down with it. The reply was already on
    /// screen when it happened, because the transcript is written before this is raised.
    /// </para>
    /// <para>
    /// The view marshals rather than the model, because thread affinity is the view's property:
    /// a view model is not affine to anything, and every other caller of <c>Append</c> — the VR
    /// surface, callouts — gets the same protection for free.
    /// </para>
    /// </summary>
    private void ScrollToEnd()
    {
        // Posted only when it has to be. Marshalling unconditionally would put the scroll behind
        // the append that caused it even on the UI thread, which is a visible lag for nothing.
        if (Dispatcher.UIThread.CheckAccess())
        {
            TranscriptScroller.ScrollToEnd();
            return;
        }

        Dispatcher.UIThread.Post(TranscriptScroller.ScrollToEnd);
    }

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
