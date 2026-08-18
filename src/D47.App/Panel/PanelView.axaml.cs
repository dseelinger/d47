using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Threading;
using D47.Core.Interface;

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
    /// The root key of each transcript mode. The three modes of the Transcript tab are roots in
    /// the navigator's sense — a tab with more than one root — so they are named once here and
    /// the enum is mapped onto them rather than the other way round.
    /// <para>
    /// Public because the segmented control is built from the navigator at runtime rather than
    /// declared in the markup, so these keys are the only stable name a mode has: a host driving
    /// one, and a test reaching for one, have nothing else to ask for.
    /// </para>
    /// </summary>
    public const string ConversationRoot = "transcript.conversation";

    /// <inheritdoc cref="ConversationRoot"/>
    public const string TechnicalRoot = "transcript.technical";

    /// <inheritdoc cref="ConversationRoot"/>
    public const string LogRoot = "transcript.log";

    private PanelViewModel? _bound;

    /// <summary>
    /// Where the Commander is on this surface: which tab, which of its roots, and how far down
    /// (list.md Phase 25).
    /// <para>
    /// Per surface rather than per model, exactly like <see cref="Mode"/> and the scroll
    /// position, and for the same reason: the window can be three levels into a ship's slots
    /// while the headset reads the conversation, and one navigator on the shared model would
    /// drag each surface wherever the other went.
    /// </para>
    /// </summary>
    public PanelNavigator Nav { get; } = new();

    /// <summary>
    /// The two questions this surface can put to the Commander — pick one of these, and say or
    /// type this (list.md Phase 25). Per surface for the reason the navigator is: a chooser is a
    /// level of one surface's stack, and the other surface should be able to go on reading the
    /// transcript while it is up.
    /// </summary>
    public PanelPrompts Prompts { get; }

    /// <summary>
    /// How the host builds each tab's surface, for the tabs it gave. A tab with no builder is a
    /// tab this surface does not have — which is what makes the headset's copy structurally
    /// unable to show settings rather than merely unlikely to, and what lets the phases that
    /// fill Loadout, Engineers and Utilities land one at a time without a dead tab in between.
    /// </summary>
    private readonly Dictionary<PanelTab, Func<NavCrumb, Control>> _builders = [];

    /// <summary>
    /// The drill strip each furnished tab is drawn in, kept. Building a settings page means
    /// ninety-odd rows from the registry, and a Commander flipping between two tabs should pay
    /// for each of them once - which the strip's own cache of levels then extends downwards.
    /// </summary>
    private readonly Dictionary<PanelTab, DrillView> _pages = [];

    /// <summary>The tab buttons, by tab, so a bar can be driven from the navigator.</summary>
    private readonly Dictionary<PanelTab, RadioButton> _tabs = [];

    /// <summary>
    /// The glyph the log mode carries while it reads the file (list.md Phase 12). On the mode
    /// rather than on the tab now, because the tab is three readings of one thing and only one
    /// of them touches the disk — which is exactly the asymmetry the collapse keeps.
    /// </summary>
    private Controls.BusyGlyph _logBusy = new() { IsVisible = false };

    /// <summary>The button that opened the log mode, so <see cref="Controls.Busy"/> can shut it.</summary>
    private RadioButton? _logMode;

    /// <summary>Which roots the segmented control is currently drawn for. See DrawModes.</summary>
    private IReadOnlyList<NavCrumb> _modeRoots = [];

    /// <summary>
    /// How the host shows the turn's figures, when it gave a way. Null on every surface that was
    /// not handed one — which is what makes the headset's copy unable to open a desktop dialog
    /// rather than merely unlikely to.
    /// </summary>
    private Action? _showTurnDetails;

    /// <summary>
    /// The tab to come back to when a furnished one is left. The one the Commander was reading,
    /// not a fixed default: Escape out of settings should put back what Escape into them covered
    /// up.
    /// </summary>
    private PanelTab _lastTab = PanelTab.Transcript;

    /// <summary>What was last drawn, so a change of tab can name the one being left.</summary>
    private PanelTab _showing = PanelTab.Transcript;

    /// <summary>
    /// Which page was last drawn, as tab and root together. What tells a redraw of the same page
    /// from a move to a different one — the search query and the follow lock belong to the page,
    /// and neither should be reset by a rebuild the Commander did not ask for.
    /// </summary>
    private PanelTab _showingTab = PanelTab.Transcript;

    private string _showingRoot = ConversationRoot;

    /// <summary>
    /// Set while the bar is being driven from the navigator, so the handlers that hear a tab
    /// check do not read it back as the Commander having pressed one.
    /// </summary>
    private bool _drivingBar;

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

    /// <summary>
    /// Whether this surface is following the end of the transcript (list.md Phase 19, "Follow
    /// the live log, or stop following it").
    /// <para>
    /// A property of the surface rather than of the model, like the scroll position it is about:
    /// the Commander can be reading back through history on the desktop window while the headset
    /// stays on the newest line.
    /// </para>
    /// <para>
    /// True to begin with, because a panel beside a running game is a live view by default. It
    /// goes false the moment the reader scrolls away from the bottom and true again when they
    /// come back — the lock is inferred from where they are looking rather than being a mode
    /// they have to remember to leave.
    /// </para>
    /// </summary>
    private bool _following = true;

    /// <summary>
    /// Set while this view is doing the scrolling, so the handler below does not read its own
    /// <see cref="ScrollToEnd"/> as the Commander having moved. Without it, following would be
    /// re-decided from a position the reader did not choose.
    /// </summary>
    private bool _scrollingItself;

    public PanelView()
    {
        InitializeComponent();

        // Set in code rather than bound, because what mini hides is three named regions and a
        // binding for each would be three expressions no test can reach. The content inside
        // them still binds - a banner is hidden in mini and also hidden when there is nothing
        // wrong, and those are different reasons.
        ModeProperty.Changed.AddClassHandler<PanelView>((view, _) => view.ApplyChrome());

        // The three readings of one exchange, registered as the Transcript tab's roots. They are
        // roots rather than levels for the reason Fleet, Locker and Directory are: the tab is the
        // root, so pressing Transcript while three levels into something returns to whichever of
        // these was last being read rather than to a fixed one.
        Nav.Register(PanelTab.Transcript, new NavCrumb(ConversationRoot, "Conversation"));
        Nav.Register(PanelTab.Transcript, new NavCrumb(TechnicalRoot, "Technical"));
        Nav.Register(PanelTab.Transcript, new NavCrumb(LogRoot, "Log file"));

        Prompts = new PanelPrompts(Nav, Layer);

        _tabs[PanelTab.Transcript] = TranscriptTab;
        _tabs[PanelTab.Checklist] = ChecklistTab;
        _tabs[PanelTab.Loadout] = LoadoutTab;
        _tabs[PanelTab.Engineers] = EngineersTab;
        _tabs[PanelTab.Utilities] = UtilitiesTab;
        _tabs[PanelTab.Settings] = SettingsTab;

        Nav.Changed += (_, _) => ApplyNavigation();

        ApplyNavigation();

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
                ApplyMicrophone();
                ApplyAskHint();
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

    /// <summary>
    /// Which tab this instantiation is showing. A property of the surface: the window can be on
    /// Settings while the headset reads the conversation.
    /// <para>
    /// Setting it to a tab this surface was not furnished with does nothing, which is what stops
    /// a stale property or a hand-edited state putting the headset on an empty pane with no way
    /// back.
    /// </para>
    /// </summary>
    public PanelTab Tab
    {
        get => Nav.Tab;
        set => Nav.Select(value);
    }

    /// <summary>
    /// Which reading of the transcript this instantiation is showing. Settable without switching
    /// to the Transcript tab, because it says which mode that tab is on rather than which tab is.
    /// </summary>
    public TranscriptPage Page
    {
        get => Nav.RootKeyOf(PanelTab.Transcript) switch
        {
            TechnicalRoot => TranscriptPage.Technical,
            LogRoot => TranscriptPage.Log,
            _ => TranscriptPage.Conversation,
        };

        set => Nav.SelectRoot(PanelTab.Transcript, value switch
        {
            TranscriptPage.Technical => TechnicalRoot,
            TranscriptPage.Log => LogRoot,
            _ => ConversationRoot,
        });
    }

    private PanelViewModel? Model => DataContext as PanelViewModel;

    private void OnModelChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (_bound is null)
        {
            return;
        }

        // Marshalled here for the same reason ScrollToEnd is, and following the same rule: the
        // view owns thread affinity, so a new caller does not have to learn it separately. Loop
        // states are raised from the turn's own thread and from the audio path, and neither is
        // the one that owns these controls. Posted only when it has to be.
        switch (e.PropertyName)
        {
            case nameof(PanelViewModel.LoopState):
                if (Dispatcher.UIThread.CheckAccess())
                {
                    Avatar.Show(_bound.LoopState);
                    return;
                }

                var state = _bound.LoopState;
                Dispatcher.UIThread.Post(() => Avatar.Show(state));
                return;

            case nameof(PanelViewModel.Microphone):
            case nameof(PanelViewModel.MicrophoneDetail):
                if (Dispatcher.UIThread.CheckAccess())
                {
                    ApplyMicrophone();
                    return;
                }

                Dispatcher.UIThread.Post(ApplyMicrophone);
                return;

            case nameof(PanelViewModel.HasAsked):
                if (Dispatcher.UIThread.CheckAccess())
                {
                    ApplyAskHint();
                    return;
                }

                Dispatcher.UIThread.Post(ApplyAskHint);
                return;
        }
    }

    /// <summary>
    /// The ask box's placeholder: a worked example until the Commander has asked something, and
    /// a plain label for ever after (docs/plans/change-requests.md item 5).
    /// <para>
    /// Set in code rather than bound, and the reason is threading rather than taste. A binding
    /// on the view model subscribes to <c>PropertyChanged</c> for <em>every</em> property and
    /// filters by name — so a control bound here is woken on each streamed delta, from the
    /// turn's thread, which is not the thread that owns it. Everything else on this control
    /// that a background path can raise is marshalled through the switch above for exactly that
    /// reason; a binding would be the one route out of it.
    /// </para>
    /// </summary>
    private void ApplyAskHint()
    {
        if (_bound is not null)
        {
            AskBox.PlaceholderText = _bound.AskHint;
        }
    }

    /// <summary>
    /// Draws what the microphone is doing (list.md Phase 13, "Show that the microphone is open").
    /// <para>
    /// Set in code rather than bound through three converters, for the same reason the chrome is:
    /// one state decides four things about one control — the shape, the colour, the border and
    /// the words — and four bindings would be four expressions no test can reach, which have to
    /// agree with each other or the indicator says one thing and means another.
    /// </para>
    /// <para>
    /// The three states are distinguished by shape before colour. <em>Open</em> is a filled
    /// microphone in the accent, ringed by a border, and it is the only one that is: what is
    /// arriving right now will be transcribed. <em>Armed</em> is the same shape hollow, in the
    /// information colour, because d47 is deciding for itself and the Commander should be able
    /// to see it doing so at a glance. <em>Idle</em> is muted grey.
    /// </para>
    /// <para>
    /// The words are short because they are read at a glance beside a running game, and two of
    /// the three can name the mode outright because the state already implies it: <em>Idle</em>
    /// occurs only under push-to-talk and <em>Armed</em> only without it, so "PTT Ready" and
    /// "Listening..." are exact rather than shorthand. <em>Open</em> is the one state both modes
    /// reach, so it says "MIC ON" — a held key and a gate d47 opened for itself are the same
    /// fact about the microphone, and naming push-to-talk there would be false half the time.
    /// </para>
    /// <para>
    /// <em>Idle</em> used to spell out that nothing was being kept. That is still what it does —
    /// the handle is held open, audio runs into a half-second ring and is overwritten — but the
    /// label led with the alarming half of the sentence and went on leading with it for as long
    /// as d47 was running. The claim it was making is the ordinary case; <em>Armed</em> is the
    /// state worth a Commander's suspicion, and that one is still distinguished by shape,
    /// colour and word.
    /// </para>
    /// </summary>
    private void ApplyMicrophone()
    {
        if (_bound is null)
        {
            return;
        }

        var state = _bound.Microphone;
        var detail = _bound.MicrophoneDetail;

        var (key, label) = state switch
        {
            D47.Core.Listening.MicrophoneState.Open => (Theming.ThemeManager.AccentKey, "MIC ON"),
            D47.Core.Listening.MicrophoneState.Armed => ("D47.Info", "Listening..."),
            _ => ("D47.TextMuted", "PTT Ready"),
        };

        MicrophoneGlyph.Bind(Avalonia.Controls.Shapes.Shape.StrokeProperty, this.GetResourceObservable(key));
        MicrophoneLabel.Bind(TextBlock.ForegroundProperty, this.GetResourceObservable(key));

        // Filled only while it is open. A shape difference is what a glance reads first, and it
        // is what still says "open" for a Commander who cannot tell the two colours apart.
        if (state == D47.Core.Listening.MicrophoneState.Open)
        {
            MicrophoneGlyph.Bind(
                Avalonia.Controls.Shapes.Shape.FillProperty, this.GetResourceObservable(key));

            MicrophoneRow.Bind(Border.BorderBrushProperty, this.GetResourceObservable(key));
        }
        else
        {
            MicrophoneGlyph.Fill = null;
            MicrophoneRow.BorderBrush = null;
        }

        MicrophoneLabel.Text = label;

        // The detail — which key to hold, or which name to say — is a tooltip rather than more
        // text on the row. It is one line of chrome on a panel meant to sit beside a running
        // game, and the state is the part that has to be readable without stopping to read.
        ToolTip.SetTip(
            MicrophoneRow,
            string.IsNullOrWhiteSpace(detail) ? label : $"{label} — {detail}");
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
    public void EnableSettings(Func<Control> build) =>
        Furnish(PanelTab.Settings, _ => build(), new NavCrumb("settings", "Settings"));

    /// <summary>
    /// Gives this surface the checklist (list.md Phase 25, "The checklist leaves its window").
    /// <para>
    /// <b>Both surfaces</b>, unlike settings. A <c>Window</c> cannot appear in the headset at
    /// all, so a Commander in VR could not see their checklist before this — which is the whole
    /// headline of the item, and would be undone by furnishing only the desktop one.
    /// </para>
    /// <para>
    /// It has one root and one level below it, and the level is the suggestions page: everything
    /// <c>ChecklistProposals</c> is holding, in one place, rather than arriving as an
    /// interruption.
    /// </para>
    /// </summary>
    public void EnableChecklist(D47.Core.Checklists.ChecklistService checklists)
    {
        ChecklistPage? page = null;

        Furnish(
            PanelTab.Checklist,
            crumb => crumb.Key == ChecklistPage.SuggestionsKey
                ? page?.BuildSuggestions() ?? new TextBlock { Text = "Nothing waiting." }
                : page = new ChecklistPage(checklists, Nav, Prompts),
            new NavCrumb("checklist", "Checklist"));
    }

    /// <summary>
    /// Gives this surface the fleet, what the Commander is wearing, and the arithmetic between
    /// them (list.md Phase 26, "Ships"; Phase 27, "Suits and weapons, and the gap").
    /// <para>
    /// Three roots of one tab rather than three tabs, because they are three readings of one
    /// question — <em>what am I building</em> — rather than three destinations. The mode control
    /// Phase 25 built is what carries them, and each keeps its own drill state, so leaving Ships
    /// halfway down a slot and looking at the gap does not disturb it.
    /// </para>
    /// <para>
    /// <paramref name="onFoot"/> is null under the designer and in tests that are not about it,
    /// and the tab then has the one root it had in Phase 26 rather than two that answer nothing.
    /// </para>
    /// </summary>
    public void EnableLoadout(
        D47.Core.Ships.ShipPlanService ships,
        D47.Core.Checklists.ChecklistService checklists,
        Func<D47.Core.Journal.CommanderGameState?> state,
        D47.Core.Loadout.OnFootPlanService? onFoot = null)
    {
        var modes = new List<ILoadoutMode> { new ShipsMode(ships, checklists, state) };

        if (onFoot is not null)
        {
            modes.Add(new OnFootMode(onFoot, state));
        }

        // Recomputed on every draw rather than cached: it is a subtraction over two stores and the
        // live inventory, all three of which move under the page.
        GapSource? gap = null;

        if (onFoot is not null)
        {
            gap = new GapSource(intended => D47.Core.Loadout.PlanGap.Of(
                ships.Store.Builds,
                onFoot.Store.Builds,
                state(),
                intended,
                checklists.SlotFor));

            // Either store moving changes the subtraction, and neither knows about the other.
            ships.Store.Changed += gap.Invalidate;
            onFoot.Store.Changed += gap.Invalidate;
        }

        var roots = new List<NavCrumb> { new(LoadoutPages.FleetRoot, "Ships") };

        if (onFoot is not null)
        {
            roots.Add(new NavCrumb(OnFootMode.Root, "Suits"));
            roots.Add(new NavCrumb(LoadoutPages.GapRoot, "Gap"));
        }

        Furnish(
            PanelTab.Loadout,
            crumb => LoadoutPages.Build(crumb, modes, gap, Nav, Prompts),
            [.. roots]);
    }

    /// <summary>
    /// Gives this surface the clocks, timers and alarms (list.md Phase 24, "Utilities").
    /// <para>
    /// Both surfaces, like the checklist: a Commander in a headset is exactly the Commander who
    /// cannot glance at a wall clock.
    /// </para>
    /// </summary>
    public void EnableUtilities(
        D47.Core.Utilities.Timekeeper timekeeper,
        D47.Core.Utilities.AlarmStore alarms,
        Func<DateTimeOffset> now,
        Func<TimeZoneInfo> zone)
    {
        Furnish(
            PanelTab.Utilities,
            _ => _utilities = new UtilitiesPage(timekeeper, alarms, now, zone, Prompts),
            new NavCrumb("utilities", "Utilities"));
    }

    /// <summary>
    /// Redraws the clocks, from the host's tick.
    /// <para>
    /// Pushed rather than pulled, because a clock is the one page whose content changes with
    /// nothing having happened — which is the same reason the timers themselves are the first
    /// thing d47 does that nothing external triggers. Nothing at all until the tab has been
    /// opened once, so a Commander who never looks at it pays no ticks for it.
    /// </para>
    /// </summary>
    public void TickClocks()
    {
        if (Tab == PanelTab.Utilities)
        {
            _utilities?.Refresh();
        }
    }

    private UtilitiesPage? _utilities;

    /// <summary>
    /// Gives this surface a tab, built by <paramref name="build"/> the first time it is selected,
    /// with the roots it offers (list.md Phase 25).
    /// <para>
    /// The general form of what <see cref="EnableSettings"/> has done since Phase 12, and the
    /// generalisation is the point: a tab is a capability the host grants, so the surfaces
    /// Phases 24 and 26-28 add appear when they are built and the bar carries no dead tab in the
    /// meantime. One registration line each.
    /// </para>
    /// <para>
    /// <paramref name="roots"/> are the tab's modes, in the order the segmented control shows
    /// them. One root means no mode control at all; several means a stack per root, so leaving
    /// Ships halfway down a slot and coming back arrives where it was left.
    /// </para>
    /// </summary>
    public void Furnish(PanelTab tab, Func<NavCrumb, Control> build, params NavCrumb[] roots)
    {
        if (roots.Length == 0)
        {
            throw new ArgumentException("A tab needs at least one root.", nameof(roots));
        }

        _builders[tab] = build;

        foreach (var root in roots)
        {
            Nav.Register(tab, root);
        }

        if (_tabs.TryGetValue(tab, out var button))
        {
            button.IsVisible = true;
        }

        ApplyNavigation();
    }

    /// <summary>
    /// Back, and the one method all three routes that must agree go through — the breadcrumb,
    /// the controller button and the phrase (list.md Phase 25). Says whether there was anything
    /// to go back from, so Escape with nothing to leave stays available to whatever else wants
    /// the key.
    /// <para>
    /// One level at a time while there is a trail, and out of a furnished tab to the one it
    /// covered up when there is not. Those are the same gesture from the Commander's side: the
    /// tab is the root, so leaving the root is leaving the tab.
    /// </para>
    /// </summary>
    public bool GoBack()
    {
        if (Nav.Back())
        {
            return true;
        }

        if (Tab == PanelTab.Transcript)
        {
            return false;
        }

        // Never back into a tab that is no longer furnished, which is a state a surface handed
        // one builder and then another could otherwise reach.
        Tab = Nav.Has(_lastTab) && _lastTab != Tab ? _lastTab : PanelTab.Transcript;
        return true;
    }

    /// <summary>
    /// Gives this surface the tools that belong to the page being read — the search box, and the
    /// button that copies the page (list.md Phase 19, "Copy log").
    /// <para>
    /// Enabled by the host, like the settings page and for the same reason: the desktop window is
    /// the surface with a keyboard and a clipboard. Mini shows no strip at all and the headset
    /// has neither, and a search box the Commander cannot type into is worse than no search.
    /// </para>
    /// </summary>
    public void EnableSearch() => SearchRow.IsVisible = true;

    /// <summary>
    /// Offers the turn's figures behind a link, for a host that has somewhere to show them
    /// (docs/plans/change-requests.md item 2).
    /// <para>
    /// Handed in like the settings builder rather than opened here, because this view hosts no
    /// window and opens no dialog — that is what lets one view definition serve the desktop
    /// window and the headset. The headset's copy is handed nothing, so it structurally has no
    /// link rather than merely being unlikely to be clicked in mid-air.
    /// </para>
    /// </summary>
    public void EnableTurnDetails(Action show)
    {
        _showTurnDetails = show;
        TurnDetails.IsVisible = true;
    }

    private void OnTurnDetailsClick(object? sender, RoutedEventArgs e) => _showTurnDetails?.Invoke();

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

        (Tab == PanelTab.Transcript ? Transcript : PagePane.Child)?.Focus();

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
    /// <para>
    /// The settings page is told directly rather than through the box. Emptying the box raises
    /// <c>TextChanged</c>, and <see cref="ApplySearch"/> hands the empty query to whatever page
    /// is <em>current</em> - which by the time this runs is the page being arrived at, not the
    /// one being left. Settings therefore kept the filter it was last given, and the Commander
    /// came back to an empty search box over four sections of eighteen, with nothing they could
    /// type that would bring the rest back (bugs.md 2). Unconditional, because a page that is
    /// not filtered answers an empty query with a comparison and a return.
    /// </para>
    /// </summary>
    private void DropSearch()
    {
        SearchInput.Text = string.Empty;

        // Every built page rather than only the one showing. The bug this guards against is
        // exactly the one bugs.md 2 recorded: the page being left keeps the filter it was last
        // given, and a Commander coming back to it finds four sections of eighteen with an empty
        // search box above them and nothing they can type to bring the rest back. With more than
        // one filterable tab there is more than one page that can be in that state.
        foreach (var page in _pages.Values)
        {
            (page as IFilterablePage)?.Filter(string.Empty);
        }
    }

    /// <summary>
    /// The cross inside the box. Clears by emptying the box rather than by calling the page
    /// directly, so the query goes back through <see cref="OnSearchChanged"/> and the filtered
    /// page is restored — blanking the text and stopping there would leave settings showing four
    /// sections of eighteen with an empty box above them, which is bugs.md 2 in a new coat.
    /// <para>
    /// Focus stays in the field, where Escape hands it back to the page. Pressing Escape says the
    /// Commander is done searching; clicking the cross is usually the start of the next query.
    /// </para>
    /// </summary>
    private void OnSearchClearClick(object? sender, RoutedEventArgs e)
    {
        if (_query.Length == 0)
        {
            return;
        }

        SearchInput.Text = string.Empty;
        SearchInput.Focus();
    }

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
        // Here rather than in ShowSearchProgress, which is about the count and the steppers and
        // is therefore only true on a page that highlights. There is something to clear on a
        // filtered page too — arguably more, since a filter hides the rest of the page.
        SearchClear.IsVisible = _query.Length > 0;

        if (Tab != PanelTab.Transcript)
        {
            (PagePane.Child as IFilterablePage)?.Filter(_query);
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

        // Deliberately not guarded by _scrollingItself. Stepping to a match is moving away from
        // the end on purpose, and following has to stop — otherwise the next line to arrive
        // yanks the reader off the hit they just asked to be shown.
        //
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
        var transcript = Tab == PanelTab.Transcript;

        Header.IsVisible = full;
        Banners.IsVisible = full;

        // A furnished tab brings its own footer — the settings surface has the storage line,
        // About and the data folder — so the ask line and the provenance line give way to it
        // rather than sitting under it saying nothing about a page with no turns on it.
        AskRow.IsVisible = full && transcript;

        // The provenance line and the microphone indicator together, because both are about the
        // transcript and no other tab has turns on it.
        StatusRow.IsVisible = transcript;

        // Mini is "the transcript's tail and the provenance line" and nothing else, so the tabs,
        // the mode control, the breadcrumb and the search box go with the rest of the chrome. A
        // surface with 512x280 to spend does not spend it on six page selectors.
        TabStrip.IsVisible = full;
        CrumbRow.IsVisible = full && CrumbRow.Children.Count > 0;

        var modal = ModalPane.Child is not null;

        // The chooser takes the region rather than sitting over it. Both panes give way, because
        // a page visible behind a modal is a page a ray can still reach and a modal is a modal.
        ModalPane.IsVisible = modal;
        TranscriptPane.IsVisible = transcript && !modal;
        PagePane.IsVisible = !transcript && !modal;

        // And the ask line goes with them: a chooser has one question in it and a second text box
        // underneath, pointed at the model, is a second question nobody asked.
        AskRow.IsVisible = AskRow.IsVisible && !modal;
    }

    /// <summary>
    /// Draws everything the navigator decides: which tab is checked, which modes the segmented
    /// control offers, what the breadcrumb says, and which page is in the slot.
    /// <para>
    /// One method rather than one per concern, because they are four readings of one state and
    /// four handlers each owning part of it is how one of them ends up putting a region back
    /// that another had just taken away — the same argument <see cref="ApplyChrome"/> already
    /// makes for mode and tab together.
    /// </para>
    /// </summary>
    private void ApplyNavigation()
    {
        // A surface that was never given a tab cannot be put on one, whether by a stale
        // property, a host that forgot to furnish it, or a hand-edited state. The navigator
        // refuses the move, so this is only ever reached with a tab that exists — except for
        // Transcript, which every surface has by construction.
        var tab = Tab;

        if (tab != PanelTab.Transcript && !_builders.ContainsKey(tab))
        {
            Tab = PanelTab.Transcript;
            return;
        }

        _drivingBar = true;

        try
        {
            foreach (var (which, button) in _tabs)
            {
                button.IsChecked = which == tab;
            }
        }
        finally
        {
            _drivingBar = false;
        }

        // What to put back when this one is left. The tab the Commander came from, not a fixed
        // default: leaving settings should uncover what opening them covered up.
        if (tab != _showing)
        {
            _lastTab = _showing;
            _showing = tab;
        }

        // The page being read has changed - a different tab, or a different mode of the same one.
        //
        // The query belongs to the page and is dropped with it. One string that filters here and
        // highlights there is a control that behaves differently depending on where the Commander
        // last clicked. And a page arrived at is a page opened at its newest line: carrying
        // "I have scrolled up" from the conversation to the log file would open the log at the
        // top of a file with nothing in view.
        //
        // Here rather than in the click handler, because a page can change without one - the
        // window opens settings from a hotkey, and a spoken phrase moves the tab with nothing
        // pressed at all.
        var root = Nav.RootKeyOf(tab);

        if (tab != _showingTab || root != _showingRoot)
        {
            _showingTab = tab;
            _showingRoot = root;

            DropSearch();
            _following = true;
        }

        DrawModes();
        DrawCrumbs();

        // A chooser takes the content region, over whichever tab it was opened from - so the tab
        // underneath keeps its state and comes back to where it was rather than to its root.
        ModalPane.Child = Nav.Modal ? Prompts.Build(Nav.Trail[^1]) : null;

        ApplyChrome();

        if (tab != PanelTab.Transcript)
        {
            BuildPageOnce(tab);
            return;
        }

        // Read when the page is opened rather than on a timer. A log nobody is looking at is
        // not worth a file read per tick, and one being looked at is being looked at because
        // something has already gone wrong.
        if (Page == TranscriptPage.Log)
        {
            _ = ReadLogAsync();
            return;
        }

        DrawTranscript();
    }

    /// <summary>
    /// The segmented control, rebuilt from the current tab's roots. Absent for a tab with one
    /// root, and absent below the root of any tab: a mode switch three levels into a ship is a
    /// question about which stack you are in, and the breadcrumb is already answering the one
    /// about where you are.
    /// </summary>
    private void DrawModes()
    {
        var roots = Nav.Roots(Nav.Tab);
        var showing = Nav.RootKeyOf(Nav.Tab);

        ModeRow.IsVisible = roots.Count > 1 && Nav.AtRoot;

        // Rebuilt only when the roots themselves change - a different tab - and otherwise just
        // re-checked. Tearing the row down and building it again on every navigation would be
        // three controls discarded to change one boolean, and it moves the buttons: a rebuilt
        // control has no bounds until the next layout pass, so a ray or a pointer aimed at where
        // a mode was a moment ago lands on a control that has not been measured yet.
        if (!_modeRoots.SequenceEqual(roots))
        {
            Rebuild(roots);
        }

        foreach (var button in Modes.Children.OfType<RadioButton>())
        {
            _drivingBar = true;

            try
            {
                button.IsChecked = (string?)button.Tag == showing;
            }
            finally
            {
                _drivingBar = false;
            }
        }
    }

    /// <summary>Builds the segmented control's buttons for a tab's roots, in their order.</summary>
    private void Rebuild(IReadOnlyList<NavCrumb> roots)
    {
        Modes.Children.Clear();

        _logMode = null;
        _modeRoots = [.. roots];

        foreach (var root in roots)
        {
            var button = new RadioButton
            {
                Theme = this.FindResource("D47.Segment") as ControlTheme,
                Tag = root.Key,
            };

            // The log mode carries the glyph, because it is the one of the three that reads a
            // file off disk. On the affordance that was touched, so a Commander who pressed here
            // does not look elsewhere (list.md Phase 12).
            if (root.Key == LogRoot)
            {
                // A fresh glyph each rebuild. A control belongs to exactly one visual tree, and
                // this row is rebuilt when the tab changes - so a kept instance is one that the
                // previous button's content panel is still holding, and adding it to the new one
                // throws rather than reparenting.
                _logBusy = new Controls.BusyGlyph { IsVisible = false };

                button.Content = new StackPanel
                {
                    Orientation = Avalonia.Layout.Orientation.Horizontal,
                    Spacing = 7,
                    Children =
                    {
                        new TextBlock
                        {
                            Text = root.Word,
                            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
                        },
                        _logBusy,
                    },
                };

                _logBusy.Bind(
                    Avalonia.Controls.Shapes.Shape.StrokeProperty,
                    this.GetResourceObservable(Theming.ThemeManager.AccentKey));

                _logMode = button;
            }
            else
            {
                button.Content = root.Word;
            }

            button.IsCheckedChanged += OnModeChecked;
            Modes.Children.Add(button);
        }
    }

    private void OnModeChecked(object? sender, RoutedEventArgs e)
    {
        if (_drivingBar || sender is not RadioButton { IsChecked: true, Tag: string key })
        {
            return;
        }

        // The search query and the follow lock are dropped by ApplyNavigation, which the
        // navigator's own event brings us back through - so a mode reached by a click and a mode
        // reached by a phrase are one path rather than two that have to agree.
        Nav.SelectRoot(key);
    }

    /// <summary>
    /// The trail, rebuilt. Every crumb but the last is pressable and the last is where you are;
    /// each is a word that can be said as well as pressed, which is why the word is carried on
    /// the crumb rather than derived from whatever the page happens to be titled.
    /// </summary>
    private void DrawCrumbs()
    {
        CrumbRow.Children.Clear();

        var trail = Nav.Trail;

        if (trail.Count <= 1)
        {
            return;
        }

        for (var index = 0; index < trail.Count; index++)
        {
            if (index > 0)
            {
                var separator = new TextBlock
                {
                    Text = "›",
                    Margin = new Thickness(2, 0, 2, 0),
                    VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
                    FontSize = Theming.TypeScale.Body,
                };

                separator.Bind(
                    TextBlock.ForegroundProperty,
                    this.GetResourceObservable(Theming.ThemeManager.TextMutedKey));

                CrumbRow.Children.Add(separator);
            }

            var at = index;
            var last = index == trail.Count - 1;

            var crumb = new Button
            {
                Theme = this.FindResource("D47.Crumb") as ControlTheme,
                Content = trail[index].Word,
                IsEnabled = !last,
            };

            if (!last)
            {
                crumb.Click += (_, _) => Nav.JumpTo(at);
            }

            CrumbRow.Children.Add(crumb);
        }
    }

    /// <summary>
    /// Reads the log, saying so on the tab if it takes long enough to be worth saying.
    /// <para>
    /// Off the UI thread, which it was not: a log file is whatever length this session has made
    /// it, and reading one on the thread that draws is how a click on a tab becomes a frozen
    /// window. Announcing it is the other half — this reads a file off disk, and a tab that
    /// looks unchanged for a second reads as a tab that did not take the click.
    /// </para>
    /// </summary>
    private async Task ReadLogAsync()
    {
        // The mode button, which may not exist: the segmented control is not drawn in mini, and
        // the log can be the mode a surface is on there. The helper wants something to shut, so
        // with nothing drawn the read simply runs unannounced — there is nothing to announce on.
        if (_logMode is null)
        {
            await Task.Run(() => _bound?.RefreshLog());
            DrawTranscript();
            ScrollToEnd();
            return;
        }

        await Controls.Busy.While(_logMode, _logBusy, () => Task.Run(() => _bound?.RefreshLog()));

        // After the read rather than before, or the page draws the log it had last time and
        // then redraws — which is a visible flicker on the one page opened to read something.
        DrawTranscript();

        // At the end, because a log is read newest-first and this page has always opened at the
        // top of it. The transcript pages have followed the tail since Phase 4 and this one
        // never did, which was a difference nobody chose (list.md Phase 19).
        ScrollToEnd();
    }

    /// <summary>
    /// Puts a furnished tab's drill strip in the pane, building it the first time.
    /// <para>
    /// Built and cached in one synchronous step, and that is load-bearing rather than tidy: this
    /// used to shut the tab behind a <see cref="Controls.Busy"/> await while it ran, and an await
    /// here means a second press arriving before the first finished builds the page twice. The
    /// cache is the shutter now, and it closes before anything can be awaited.
    /// </para>
    /// </summary>
    private void BuildPageOnce(PanelTab tab)
    {
        if (!_pages.TryGetValue(tab, out var page))
        {
            if (!_builders.TryGetValue(tab, out var build))
            {
                return;
            }

            // A drill strip rather than the page itself, so drilling in and reflowing are one
            // mechanism for every tab at once: a tab with no levels is a strip of one pane, which
            // is exactly the page, and a tab that grows levels needs nothing added here. The
            // levels themselves are built by the strip, on first sight, which is what keeps a
            // Commander who never opens Suggestions from paying for it.
            page = new DrillView(Nav, tab, build);
            _pages[tab] = page;
        }

        PagePane.Child = page;
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
        ApplyNavigation();
    }

    private void OnTabChecked(object? sender, RoutedEventArgs e)
    {
        // Fires for the tab being cleared as well as the one being set, and only the set one
        // says anything about which page to show. And never while the bar is being driven from
        // the navigator, or checking a button would ask the navigator for the move it is already
        // making.
        if (_drivingBar || sender is not RadioButton { IsChecked: true } button)
        {
            return;
        }

        foreach (var (tab, candidate) in _tabs)
        {
            if (ReferenceEquals(candidate, button))
            {
                // Refused while a chooser holds the panel — which is what "no navigating away
                // mid-choice" is, and why the button is put back rather than left showing a tab
                // the panel is not on.
                if (!Nav.Select(tab))
                {
                    ApplyNavigation();
                }

                return;
            }
        }
    }

    /// <summary>
    /// Pressing the tab that is already selected returns to its root (list.md Phase 25, "the tab
    /// is the root rather than the first level").
    /// <para>
    /// <b>This has to be added rather than inherited.</b> The tabs are <c>RadioButton</c>s and
    /// re-pressing a checked one announces nothing at all — no <c>IsCheckedChanged</c>, because
    /// nothing changed — so the one gesture a Commander three levels into a ship reaches for
    /// first would otherwise be the one gesture that does nothing. Tapped rather than Click,
    /// since it is the press on the already-checked control that has to be heard.
    /// </para>
    /// </summary>
    private void OnTabTapped(object? sender, TappedEventArgs e)
    {
        if (sender is not RadioButton button
            || !_tabs.TryGetValue(Nav.Tab, out var current)
            || !ReferenceEquals(current, button))
        {
            return;
        }

        Nav.ToRoot();
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
        // Only while following. This used to be unconditional, which is the whole of the
        // complaint: reading back through history meant being dragged to the bottom by every
        // line that arrived, and a busy session appends several a second (list.md Phase 19).
        if (!_following)
        {
            return;
        }

        // Posted only when it has to be. Marshalling unconditionally would put the scroll behind
        // the append that caused it even on the UI thread, which is a visible lag for nothing.
        if (Dispatcher.UIThread.CheckAccess())
        {
            Follow();
            return;
        }

        Dispatcher.UIThread.Post(Follow);
    }

    /// <summary>
    /// Goes to the newest line, without the trip through the handler deciding whether the
    /// Commander meant to move.
    /// </summary>
    /// <summary>
    /// Re-asserts following, for a surface whose layout only happens when it is drawn.
    /// <para>
    /// <see cref="Follow"/> calls <c>UpdateLayout</c> so it scrolls to a current extent, and on a
    /// window that is never shown that call does nothing — so the headset's copy scrolled to the
    /// end of an extent equal to its viewport, which is the top, and stayed there. This is called
    /// between that surface's layout pass and its rasterise, which is the one moment the extent
    /// is right (remediation.md, "The Newest button in VR does not appear to work").
    /// </para>
    /// </summary>
    public void KeepUp()
    {
        if (_following)
        {
            Follow();
        }
    }

    private void Follow()
    {
        _scrollingItself = true;

        try
        {
            // Laid out first. A scroll viewer scrolls to the end of the extent it currently
            // knows about, and the runs were rewritten a moment ago — so without this it goes to
            // where the end was before the line that caused it, which is the "lands one append
            // behind" the subscription order above is already fighting. Forced rather than
            // awaited because the following has to be true when this returns.
            TranscriptScroller.UpdateLayout();
            TranscriptScroller.ScrollToEnd();
        }
        finally
        {
            _scrollingItself = false;
        }

        ShowFollowButton();
    }

    /// <summary>
    /// Whether the view is at the end of the text, within a line's worth.
    /// <para>
    /// A tolerance rather than an equality, because a scroll viewer's extent and its offset are
    /// laid-out doubles: a wrapped line, a font fallback or a fractional scale leaves the last
    /// pixel unreachable, and "following" would then switch itself off on a surface that is
    /// visibly at the bottom.
    /// </para>
    /// </summary>
    private bool AtTheEnd()
    {
        var slack = Math.Max(1, TranscriptScroller.Extent.Height - TranscriptScroller.Viewport.Height);

        return TranscriptScroller.Offset.Y >= slack - Transcript.FontSize;
    }

    /// <summary>
    /// The Commander moved. Following is inferred from where they left the view rather than
    /// being a mode: scrolling up stops it and scrolling back to the bottom starts it again,
    /// which is what every log viewer they have used already does.
    /// </summary>
    private void OnTranscriptScrolled(object? sender, ScrollChangedEventArgs e)
    {
        if (_scrollingItself)
        {
            return;
        }

        // Only when the offset actually moved, and deliberately not when the viewport or the
        // extent did. Those two change on the first layout pass — the viewport grows from zero
        // while the offset is still at the top — and reading that as a gesture switched
        // following off on every surface the moment it was shown, which is every surface.
        if (e.OffsetDelta.Y != 0)
        {
            _following = AtTheEnd();
        }

        ShowFollowButton();
    }

    private void OnFollowClick(object? sender, RoutedEventArgs e)
    {
        _following = true;
        Follow();
    }

    /// <summary>
    /// Shows the jump-to-latest control, and says how far behind the reader is.
    /// <para>
    /// Hidden while following, because a control that does nothing sitting over the text it does
    /// nothing to is worse than no control. Hidden with nothing to scroll for the same reason.
    /// </para>
    /// </summary>
    private void ShowFollowButton()
    {
        var behind = !_following && !AtTheEnd();

        FollowButton.IsVisible = behind;

        if (behind)
        {
            FollowButton.Content = "↓ Newest";
        }
    }

    /// <summary>
    /// Copies the whole of the page being read (list.md Phase 19, "Copy log").
    /// <para>
    /// The page as currently shown, which is what "as currently filtered" means here: the
    /// conversation without the diagnostics, or with them, or the log file — the same text the
    /// Commander is looking at, and not a fourth thing assembled for the clipboard.
    /// </para>
    /// <para>
    /// A search query highlights on these pages rather than filtering, so it deliberately does
    /// not narrow what is copied. Copying only the matches would be a different feature and a
    /// surprising one: the Commander asked for the log.
    /// </para>
    /// </summary>
    private async void OnCopyClick(object? sender, RoutedEventArgs e)
    {
        // Its own visual root's clipboard, which is a thing a control may ask for — unlike a
        // window, a dialog or a browser, none of which this view knows about. A surface with no
        // clipboard behind it, which is what the headset's never-shown host is, answers null and
        // the button does nothing rather than throwing on a quad a metre away.
        if (_bound is null || TopLevel.GetTopLevel(this)?.Clipboard is not { } clipboard)
        {
            return;
        }

        var text = string.Concat(_bound.Segments(Page).Select(segment => segment.Text));

        // Said on the button rather than in a banner. It is a one-word confirmation of a
        // one-click action, and a fault here — no clipboard, another application holding it —
        // is otherwise indistinguishable from having worked.
        try
        {
            await clipboard.SetTextAsync(text);
            CopyButton.Content = "Copied";
        }
        catch (Exception)
        {
            CopyButton.Content = "Could not copy";
        }

        await Task.Delay(TimeSpan.FromSeconds(2));
        CopyButton.Content = "Copy";
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
