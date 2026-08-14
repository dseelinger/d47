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

    /// <summary>
    /// How the host builds the settings surface, when it gave one. Null on every surface that
    /// was not handed one — which is what makes the headset's copy structurally unable to show
    /// settings rather than merely unlikely to.
    /// </summary>
    private Func<Control>? _buildSettings;

    /// <summary>
    /// The page to come back to when settings are left. The one the Commander was reading, not
    /// a fixed default: Escape out of settings should put back what Escape into them covered up.
    /// </summary>
    private TranscriptPage _lastTranscriptPage = TranscriptPage.Conversation;

    /// <summary>What is being searched for on this surface. Empty when nothing is.</summary>
    private string _query = string.Empty;

    /// <summary>The hits in the current page, recomputed on every redraw.</summary>
    private IReadOnlyList<D47.Core.Interface.SearchMatch> _matches = [];

    /// <summary>
    /// Which hit is current, and where it starts in the page.
    /// <para>
    /// The offset is the record and the index is derived from it. That is what keeps a hit found
    /// in the live log found as lines arrive underneath: "the third match" becomes a different
    /// piece of text the moment a line is appended, and "the match at character 4,812" does not.
    /// </para>
    /// </summary>
    private int _hit = -1;

    private int _hitOffset;

    public PanelView()
    {
        InitializeComponent();

        // Set in code rather than bound, because what mini hides is three named regions and a
        // binding for each would be three expressions no test can reach. The content inside
        // them still binds - a banner is hidden in mini and also hidden when there is nothing
        // wrong, and those are different reasons.
        ModeProperty.Changed.AddClassHandler<PanelView>((view, _) => view.ApplyChrome());

        PageProperty.Changed.AddClassHandler<PanelView>((view, change) =>
        {
            // The query belongs to the page and is dropped when the page changes. One string
            // that filters here and highlights there is a control that behaves differently
            // depending on where the Commander last clicked.
            if (!Equals(change.OldValue, change.NewValue))
            {
                view.DropSearch();
            }

            view.ApplyPage();
        });

        ApplyPage();

        // Tunnelling, because both gestures belong to the surface rather than to whatever has
        // focus: Ctrl+F has to reach the box from inside the ask box, and Escape has to be taken
        // before the window decides there is nothing left to close.
        AddHandler(KeyDownEvent, OnSurfaceKeyDown, RoutingStrategies.Tunnel);

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

    /// <summary>
    /// The Settings tab, so a host can hang a tooltip naming the bound gesture on it. It is the
    /// only way in now that the gear is gone, so the gesture is named where the affordance is.
    /// </summary>
    public Control SettingsAffordance => SettingsTab;

    /// <summary>
    /// Gives this surface a settings page, built by <paramref name="build"/> the first time it
    /// is selected.
    /// <para>
    /// A capability of the host rather than of the view, and the asymmetry is the point: the
    /// desktop window calls this and the headset never does, so an overlay sized for a quad a
    /// metre away cannot be put on a surface that opens at 1180 pixels. Nobody has to remember
    /// to leave it out, because leaving it out is the default (list.md Phase 12).
    /// </para>
    /// <para>
    /// Deferred to first selection because building it means constructing ninety-odd rows from
    /// the registry, and a Commander who never opens settings should not pay for them at
    /// startup.
    /// </para>
    /// </summary>
    public void EnableSettings(Func<Control> build)
    {
        _buildSettings = build;
        SettingsTab.IsVisible = true;
    }

    /// <summary>
    /// Leaves the settings page for the one it covered up, and says whether there was anything
    /// to leave. The host binds Escape to it.
    /// </summary>
    public bool LeaveSettings()
    {
        if (Page != TranscriptPage.Settings)
        {
            return false;
        }

        Page = _lastTranscriptPage;
        return true;
    }

    /// <summary>
    /// Gives this surface the search box.
    /// <para>
    /// Enabled by the host, like the settings page and for the same reason: the desktop window
    /// is the surface with a keyboard. Mini shows no strip at all and the headset has none, and
    /// a search box the Commander cannot type into is worse than no search at all.
    /// </para>
    /// </summary>
    public void EnableSearch() => SearchRow.IsVisible = true;

    /// <summary>Puts the cursor in the search box. Ctrl+F does this; a host may too.</summary>
    public void FocusSearch()
    {
        SearchInput.Focus();
        SearchInput.SelectAll();
    }

    /// <summary>Puts the cursor in the ask box. The host binds a gesture to it.</summary>
    public void FocusAsk()
    {
        AskBox.Focus();
        AskBox.SelectAll();
    }

    /// <summary>
    /// Empties the search box and gives the page back, and says whether there was anything to
    /// empty — so Escape with no query in it stays available to whatever else wants the key.
    /// </summary>
    public bool ClearSearch()
    {
        if (_query.Length == 0)
        {
            return false;
        }

        SearchInput.Text = string.Empty;

        (Page == TranscriptPage.Settings ? SettingsPane.Child : Transcript)?.Focus();

        return true;
    }

    private void OnSurfaceKeyDown(object? sender, KeyEventArgs e)
    {
        if (!SearchRow.IsVisible)
        {
            return;
        }

        if (e.Key == Key.F && e.KeyModifiers.HasFlag(KeyModifiers.Control))
        {
            e.Handled = true;
            FocusSearch();
            return;
        }

        if (e.Key == Key.Escape && ClearSearch())
        {
            e.Handled = true;
        }
    }

    /// <summary>
    /// Drops the query without moving focus. Used when the page changes, where there is nothing
    /// to give back — the Commander is already looking somewhere else.
    /// </summary>
    private void DropSearch() => SearchInput.Text = string.Empty;

    private void OnSearchChanged(object? sender, TextChangedEventArgs e)
    {
        _query = SearchInput.Text ?? string.Empty;

        // A new query starts at the top of the page rather than near the last hit: the offset
        // that was being tracked belongs to the string that is no longer being searched for.
        _hitOffset = 0;

        ApplySearch();
    }

    private void OnSearchKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter)
        {
            return;
        }

        e.Handled = true;
        StepSearch(e.KeyModifiers.HasFlag(KeyModifiers.Shift) ? -1 : 1);
    }

    private void OnSearchNextClick(object? sender, RoutedEventArgs e) => StepSearch(1);

    private void OnSearchPreviousClick(object? sender, RoutedEventArgs e) => StepSearch(-1);

    private void StepSearch(int by)
    {
        if (_matches.Count == 0)
        {
            return;
        }

        _hit = D47.Core.Interface.TextSearch.Step(_matches.Count, _hit, by);
        _hitOffset = _matches[_hit].Start;

        DrawTranscript();
        ScrollToHit();
    }

    /// <summary>
    /// Hands the query to whatever is showing. The panel holds one string and one gesture; what
    /// a match <em>does</em> is the page's business.
    /// </summary>
    private void ApplySearch()
    {
        if (Page == TranscriptPage.Settings)
        {
            (SettingsPane.Child as IFilterablePage)?.Filter(_query);
            ShowSearchProgress(stepping: false);
            return;
        }

        DrawTranscript();
    }

    /// <summary>
    /// The count and the steppers, which only mean something where a match highlights rather
    /// than filters. On a filtered page, "3 of 17" describes rows that are already all on
    /// screen, and a next button has nothing to step to.
    /// </summary>
    private void ShowSearchProgress(bool stepping)
    {
        SearchCount.IsVisible = stepping;
        SearchNext.IsVisible = stepping;
        SearchPrevious.IsVisible = stepping;

        if (stepping)
        {
            SearchCount.Text = D47.Core.Interface.TextSearch.Describe(_matches.Count, _hit);
        }
    }

    /// <summary>
    /// Puts the current hit on screen.
    /// <para>
    /// Through the text layout rather than by asking a run where it is: an <c>Inline</c> has no
    /// bounds of its own, and the layout is the one thing that knows where a character offset
    /// landed once the text wrapped.
    /// </para>
    /// </summary>
    private void ScrollToHit()
    {
        if (_hit < 0 || Transcript.TextLayout is not { } layout)
        {
            return;
        }

        var where = layout.HitTestTextPosition(_matches[_hit].Start);

        // A third of the way down rather than at the very top, so the lines around the hit are
        // visible: reading back a conversation means reading what was around it.
        TranscriptScroller.Offset = new Vector(
            TranscriptScroller.Offset.X,
            Math.Max(0, where.Y - (TranscriptScroller.Viewport.Height / 3)));
    }

    /// <summary>
    /// What this surface shows, computed from <see cref="Mode"/> and <see cref="Page"/> together.
    /// <para>
    /// One method rather than one per property, because the two decide the same set of regions
    /// between them: the ask line is hidden in mini <em>and</em> on the settings page, and two
    /// handlers each owning half of that is how one of them ends up putting a region back that
    /// the other had just taken away.
    /// </para>
    /// </summary>
    private void ApplyChrome()
    {
        var full = Mode == PanelMode.Full;
        var settings = Page == TranscriptPage.Settings;

        Header.IsVisible = full;
        Banners.IsVisible = full;

        // The settings surface brings its own footer — the storage line, About, the data folder
        // — so the ask line and the provenance line give way to it rather than sitting under it
        // saying nothing about a page with no turns on it.
        AskRow.IsVisible = full && !settings;
        TurnLine.IsVisible = !settings;

        // Mini is "the transcript's tail and the provenance line" and nothing else, so the tabs
        // and the search box go with the rest of the chrome. A surface with 640x280 to spend
        // does not spend it on four page selectors.
        TabStrip.IsVisible = full;

        TranscriptPane.IsVisible = !settings;
        SettingsPane.IsVisible = settings;
    }

    /// <summary>
    /// Points the transcript at the page this surface is showing, and checks the tab that says
    /// so - which keeps a page set in code and a page set by a click on one path.
    /// </summary>
    private void ApplyPage()
    {
        // A surface that was never given a settings page cannot be put on one, whether by a
        // stale property, a host that forgot to enable it, or a hand-edited state. It goes back
        // to the page it was on rather than showing an empty pane.
        if (Page == TranscriptPage.Settings && _buildSettings is null)
        {
            Page = _lastTranscriptPage;
            return;
        }

        var tab = Page switch
        {
            TranscriptPage.Technical => TechnicalTab,
            TranscriptPage.Log => LogTab,
            TranscriptPage.Settings => SettingsTab,
            _ => ConversationTab,
        };

        tab.IsChecked = true;

        if (Page != TranscriptPage.Settings)
        {
            _lastTranscriptPage = Page;
        }

        ApplyChrome();

        if (Page == TranscriptPage.Settings)
        {
            BuildSettingsOnce();
            return;
        }

        // Read when the page is opened rather than on a timer. A log nobody is looking at is
        // not worth a file read per tick, and one being looked at is being looked at because
        // something has already gone wrong.
        if (Page == TranscriptPage.Log)
        {
            _bound?.RefreshLog();
        }

        DrawTranscript();
    }

    private void BuildSettingsOnce()
    {
        if (SettingsPane.Child is not null || _buildSettings is not { } build)
        {
            return;
        }

        SettingsPane.Child = build();
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
            _matches = [];
            _hit = -1;
            ShowSearchProgress(_query.Length > 0);
            return;
        }

        var segments = _bound.Segments(Page);

        // Matched against the page's text rather than against the controls, so the hits are the
        // same set whether the page has been drawn yet or not — and so the current one can be
        // re-resolved from its offset every time the log grows underneath it.
        _matches = D47.Core.Interface.TextSearch.Find(
            string.Concat(segments.Select(segment => segment.Text)),
            _query);

        _hit = D47.Core.Interface.TextSearch.Track(_matches, _hitOffset);

        if (_hit >= 0)
        {
            _hitOffset = _matches[_hit].Start;
        }

        var at = 0;

        foreach (var segment in segments)
        {
            foreach (var (text, match) in Split(segment.Text, at))
            {
                var run = new Run(text);

                if (segment.Marker)
                {
                    run.Bind(
                        Avalonia.Controls.Documents.TextElement.ForegroundProperty,
                        this.GetResourceObservable(Theming.ThemeManager.AccentKey));
                    run.FontWeight = FontWeight.SemiBold;
                }

                if (match >= 0)
                {
                    // Every hit is marked and the current one is accented, which is what makes
                    // stepping legible: the count says where you are in the set and the colour
                    // says which one of them you are looking at.
                    run.Bind(
                        Avalonia.Controls.Documents.TextElement.BackgroundProperty,
                        this.GetResourceObservable(match == _hit
                            ? Theming.ThemeManager.AccentKey
                            : Theming.ThemeManager.AccentMutedKey));

                    if (match == _hit)
                    {
                        run.Bind(
                            Avalonia.Controls.Documents.TextElement.ForegroundProperty,
                            this.GetResourceObservable(Theming.ThemeManager.BackgroundKey));
                    }
                }

                inlines.Add(run);
            }

            at += segment.Text.Length;
        }

        ShowSearchProgress(_query.Length > 0);
    }

    /// <summary>
    /// One segment, cut at the boundaries of any hits inside it, each piece carrying the index
    /// of the hit it belongs to or -1.
    /// <para>
    /// Cut here rather than searched here: a hit can straddle two segments — a query spanning
    /// the join between a marked line and the reply after it — and only offsets into the whole
    /// page can say that. Each half is drawn highlighted in its own segment's style.
    /// </para>
    /// </summary>
    private IEnumerable<(string Text, int Match)> Split(string text, int at)
    {
        var cursor = 0;

        for (var i = 0; i < _matches.Count; i++)
        {
            var start = _matches[i].Start - at;
            var end = _matches[i].End - at;

            if (end <= cursor)
            {
                continue;
            }

            if (start >= text.Length)
            {
                break;
            }

            var from = Math.Max(start, cursor);
            var to = Math.Min(end, text.Length);

            if (from > cursor)
            {
                yield return (text[cursor..from], -1);
            }

            yield return (text[from..to], i);
            cursor = to;
        }

        if (cursor < text.Length)
        {
            yield return (text[cursor..], -1);
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
            : tab == SettingsTab ? TranscriptPage.Settings
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

    private void OnDismissErrorClick(object? sender, RoutedEventArgs e) => Model?.DismissError();
}
