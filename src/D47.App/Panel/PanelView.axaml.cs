using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using D47.Core.Help;
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
    /// <remarks>
    /// <b>The key does not change when the word does</b>
    /// (<a href="https://github.com/dseelinger/d47/issues/51">#51</a>). This one has been drawn as
    /// <em>Conversation</em>, then <em>Thread</em>, then <em>Conversation</em> again, and is
    /// <em>In Ship</em> now — every one of them a <see cref="NavCrumb.Word"/> change and nothing
    /// else. These keys are internal, a host drives one by name and a test reaches for one by
    /// name, so renaming them alongside the labels would be churn with a chance of breaking
    /// something and no upside.
    /// <para>
    /// <c>transcript.technical</c> is the one that did not survive, and it is not a rename: the
    /// reading was withdrawn from the picker in #231 and the last of it went in
    /// <a href="https://github.com/dseelinger/d47/issues/260">#260</a>. A stored root naming it
    /// falls through to the conversation, which <see cref="PanelNavigator.SelectRoot"/> does for
    /// any root nobody registered.
    /// </para>
    /// </remarks>
    public const string ConversationRoot = "transcript.conversation";

    /// <inheritdoc cref="ConversationRoot"/>
    public const string LogRoot = "transcript.log";

    /// <summary>
    /// Elite's own journal, read as sentences (#51). A new key rather than a rename: the three
    /// above are internal and are cited from settings, tests and the keyword router, and only their
    /// spoken <c>Word</c> changed when this issue renamed them.
    /// </summary>
    public const string JournalRoot = "transcript.journal";

    /// <summary>The same events as the JSON Elite wrote (#51).</summary>
    public const string RawJournalRoot = "transcript.rawjournal";

    /// <summary>
    /// The help the conversation reading offers: a page about the page, rather than about any one
    /// capability (asked for 2026-08-23). Named here so the test that checks where the mark goes
    /// and the registration that sends it there cannot spell it differently.
    /// </summary>
    /// <remarks>
    /// <b>One page per reading, on the Commander's instruction</b>
    /// (<a href="https://github.com/dseelinger/d47/issues/262">#262</a>): <em>"The transcript tab
    /// should have 3 different Help pages depending on context — In Ship, Log File, Journal File.
    /// And not try to cram it all into one ELI5 page."</em>
    /// <para>
    /// There was one page for three readings and it was already too full to be about any of them:
    /// it described the conversation's bubbles, the log file's working indicator and the journal's
    /// two panes in the same four sections, so a Commander pressing <c>?</c> on the journal got
    /// three quarters of an answer about somewhere else. The mark is context-sensitive by
    /// construction — <see cref="NavCrumb.Help"/> is per crumb — so this was one page short of
    /// what the mechanism already offered.
    /// </para>
    /// </remarks>
    public const string InShipHelp = D47.Core.Help.HelpLibrary.GeneralPrefix + "in-ship";

    /// <summary>
    /// The log file reading's own page (#262). It used to open the Diagnostics capability, which
    /// is the right subject and the wrong question: that page is about log levels and where files
    /// live, and a Commander pressing <c>?</c> is asking what the thing in front of them is. It
    /// links to Diagnostics for the capability.
    /// </summary>
    public const string LogFileHelp = D47.Core.Help.HelpLibrary.GeneralPrefix + "log-file";

    /// <summary>
    /// The journal readings' page (#262), shared by both of them. Raw Journal is the same events
    /// seen another way rather than a fourth subject — the reason it is not an entry in the
    /// picker — so it is the same answer, and the page explains the switch between them.
    /// </summary>
    public const string JournalHelp = D47.Core.Help.HelpLibrary.GeneralPrefix + "journal-file";

    private PanelViewModel? _bound;

    /// <summary>Whether this surface keeps its ask line in mini. See <see cref="EnableAskInMini"/>.</summary>
    private bool _asksInMini;

    /// <summary>
    /// How this surface changes its own mode, or null where it cannot. See
    /// <see cref="EnableModeToggle"/>.
    /// </summary>
    private Action<PanelMode>? _switchMode;

    /// <summary>
    /// The tab that was showing when mini took it away, so leaving mini can put it back
    /// (Phase 51). Null whenever mini did not have to move anything.
    /// </summary>
    private PanelTab? _beforeMini;

    /// <summary>
    /// Where the Commander is on this surface: which tab, which of its roots, and how far down
    /// (Phase 25).
    /// <para>
    /// Per surface rather than per model, exactly like <see cref="Mode"/> and the scroll
    /// position, and for the same reason: the window can be three levels into a ship's slots
    /// while the headset reads the conversation, and one navigator on the shared model would
    /// drag each surface wherever the other went.
    /// </para>
    /// <para>
    /// One exception, and it is the host's rather than this view's: the Transcript tab's root is
    /// shared across every surface (Phase 45), so a press on this mode control that picks
    /// A reading chosen by voice is heard by the headset and drawn there too. The navigator still holds the value;
    /// the host's <c>TranscriptMirror</c> keeps the navigators agreeing about it.
    /// </para>
    /// </summary>
    public PanelNavigator Nav { get; } = new();

    /// <summary>
    /// The two questions this surface can put to the Commander — pick one of these, and say or
    /// type this (Phase 25). Per surface for the reason the navigator is: a chooser is a
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
    /// The glyph the log mode carries while it reads the file (Phase 12). On the mode
    /// rather than on the tab now, because the tab is three readings of one thing and only one
    /// of them touches the disk — which is exactly the asymmetry the collapse keeps.
    /// </summary>
    private readonly Controls.BusyGlyph _logBusy = new() { IsVisible = false };

    /// <summary>
    /// Whether the search affordance belongs on this surface. Only the desktop window says yes.
    /// Held rather than read off a control's visibility, because the controls it governs are now
    /// hidden and shown by the tab as well (remediation.md 10, item 2).
    /// </summary>
    private bool _searchable;

    /// <summary>
    /// What the copy button says when it is not reporting on itself. "All", because the text on
    /// this page is selectable and Ctrl+C already works on a selection — a button beside it
    /// saying "Copy" reads as copying that selection (remediation.md 10, item 3).
    /// </summary>
    private const string CopyLabel = "Copy All";

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
    /// Whether this surface is following the end of the transcript (Phase 19, "Follow
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

    /// <summary>
    /// The bubbles on the conversation page, in order, each with the offset into the page where
    /// its text begins. The offset is what lets a hit found over the whole page be scrolled to
    /// in the bubble it landed in — a block's own text layout can only answer about its own
    /// characters.
    /// </summary>
    private readonly List<(SelectableTextBlock Block, int Start)> _bubbles = [];

    /// <summary>
    /// What those bubbles were drawn from, as three comparable things each. Compared on the next
    /// redraw to decide whether a line arriving changed anything but the last turn.
    /// </summary>
    private IReadOnlyList<(TranscriptVoice Voice, bool Marker, string Text)> _shape = [];

    /// <summary>
    /// The block a selection was last made in. On a flat page it is always the transcript; on
    /// the conversation page it is whichever bubble the Commander dragged across, and Copy has
    /// to act on that one rather than on a control that holds no selection.
    /// </summary>
    private SelectableTextBlock? _selection;

    public PanelView()
    {
        InitializeComponent();

        // Set in code rather than bound, because what mini hides is three named regions and a
        // binding for each would be three expressions no test can reach. The content inside
        // them still binds - a banner is hidden in mini and also hidden when there is nothing
        // wrong, and those are different reasons.
        ModeProperty.Changed.AddClassHandler<PanelView>((view, _) =>
        {
            // Before the chrome, because it may move the tab and the chrome is drawn from it.
            // This is the one caller that remembers: what mini takes away is what leaving mini
            // gives back, and nothing else counts as having been there.
            view.SettleMini(remember: true);

            view.ApplyChrome();

            // And redrawn, because mini is not only less chrome around the conversation: the
            // bubbles themselves give back their gutter and their padding on a surface that has
            // 512 pixels to spend (asked for 2026-08-22).
            view.DrawTranscript();
        });

        // `output-only` is set by the host after construction and toggled at runtime — the headset
        // flips it on every move between the big panel and mini. A page's own chrome is taken by a
        // style and needs nothing here; the follow button is this view's own and assigns its
        // visibility from a scroll position, so it has to be re-asked when the class moves (#202).
        Classes.CollectionChanged += (_, _) => ShowFollowButton();

        // Two sheets rather than the words "Copy All" (asked for 2026-08-24). Standard enough to
        // need no learning, and it buys back a row that already carries the search box and two
        // steppers. The word stays on the tooltip and on the accessible name.
        Controls.Glyphs.Mark(
            CopyButton,
            Controls.Glyphs.Copy,
            Theming.ThemeManager.AccentKey,
            "Copy this whole page to the clipboard");

        // A banknote rather than the word "Details" (#210). Accent, as the word already was, so it
        // is consistent with the clickable-things-carry-the-accent change before that lands rather
        // than needing to move with it. The sentence it used to carry on its tooltip is the same
        // sentence, and Mark puts it on the accessible name too — which is the whole condition
        // under which replacing a word with a picture is an improvement.
        Controls.Glyphs.Mark(
            TurnDetails,
            Controls.Glyphs.Spend,
            Theming.ThemeManager.AccentKey,
            "Tokens, cost, and what this has come to over time");

        Watch(Transcript);

        // The three readings of one exchange, registered as the Transcript tab's roots. They are
        // roots rather than levels for the reason Fleet, Locker and Directory are: the tab is the
        // root, so pressing Transcript while three levels into something returns to whichever of
        // these was last being read rather than to a fixed one.
        // Help per root, because these three are three subjects: the conversation, and two
        // diagnostic readings of it. A root whose page has no band yet simply shows no mark, so
        // declaring it now is what makes the mark appear the day somebody writes one.
        //
        // The default root's help is about **the page**, not about the language model (asked for
        // 2026-08-23). It pointed at ConversationCapability, whose page is titled "Language model"
        // and is one of the three this one links to — so a Commander asking what the controls in
        // front of them do was answered with providers, cancellation and billing. A general page
        // rather than a capability's, because no capability owns a tab strip and a Copy All
        // button; the id is a HelpLibrary key rather than a registry id, which is what lets the
        // three general pages be reached the same way as the forty-five.
        // The readings say what they are, and say it plainly (#231). "Thread" and "D47 Log" were
        // words this project had got used to rather than words that answer "what am I looking
        // at"; the journal reading in particular had to say *whose* journal, because d47 keeps a
        // log of its own and the two were a word apart.
        //
        // The last two are now named for what a Commander goes there to see rather than for what
        // they are made of (#250). "Conversation" described the material; "In Ship" is where the
        // exchange happened. And once "Log File" and "Journal File" sit next to each other, the
        // journal no longer has to say whose it is — the pair reads as the distinction.
        //
        // Where the plain name is not what a Commander would say, the crumb carries a short
        // spoken alias instead of being bent to fit. See NavCrumb.Spoken: the drawn label and the
        // phrase used to be one string on purpose, and these are the cases that pulled them apart.
        Nav.Register(
            PanelTab.Transcript,
            new NavCrumb(ConversationRoot, "In Ship")
            {
                Help = InShipHelp,

                // The words this reading answered to before it was In Ship. Kept, because a
                // Commander who says one is not wrong, they are out of date, and being told
                // nothing happened is a worse answer than going where they meant.
                Spoken = ["conversation", "thread"],
            });

        Nav.Register(
            PanelTab.Transcript,
            new NavCrumb(LogRoot, "Log File")
            {
                Help = LogFileHelp,
                Spoken = ["log", "d47 log"],
            });

        // Elite's own journal, in a form that is not JSON (#51). "Elite Dangerous" came off the
        // front in #250; the alias below still answers to it, for free.
        Nav.Register(
            PanelTab.Transcript,
            new NavCrumb(JournalRoot, "Journal File")
            {
                Help = JournalHelp,
                Spoken = ["journal", "journal file", "elite dangerous journal"],
            });

        // Beside the box rather than inside it (#231). A ComboBox draws its own chevron and its
        // content is whichever reading is selected, so the spinner has to live next to the
        // control it reports on instead of within it.
        _logBusy.Bind(
            Avalonia.Controls.Shapes.Shape.StrokeProperty,
            this.GetResourceObservable(Theming.ThemeManager.AccentKey));

        ModePicker.Children.Add(_logBusy);

        Prompts = new PanelPrompts(Nav, Layer)
        {
            // Pulled rather than pushed, because the model is bound after this runs and can be
            // replaced: a value copied here would be the one that was true when the panel was
            // built (remediation.md 10, item 12).
            Waiting = () => Model?.ListeningPrompt,
        };

        _tabs[PanelTab.Transcript] = TranscriptTab;
        _tabs[PanelTab.Loadout] = LoadoutTab;
        _tabs[PanelTab.Engineers] = EngineersTab;
        _tabs[PanelTab.Checklist] = ChecklistTab;
        _tabs[PanelTab.Routing] = RoutingTab;
        _tabs[PanelTab.Adventures] = AdventuresTab;
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
                _bound.TranscriptAppended -= OnTranscriptAppended;
                _bound.TranscriptAppended -= ScrollToEnd;
                _bound.PropertyChanged -= OnModelChanged;
            }

            _bound = DataContext as PanelViewModel;

            if (_bound is not null)
            {
                // Drawn before the scroll, because scrolling to the end of text that has not
                // been written yet lands one append behind.
                _bound.TranscriptAppended += OnTranscriptAppended;
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
            //
            // **And the log page is read here rather than merely drawn** (GitHub issue 43). The
            // log is the one reading whose content does not come from the model's own buffer: it
            // is read off disk by ReadLogAsync, which does nothing at all when there is no model
            // to give the result to. A surface put on the log page *before* it was bound —
            // `new PanelView { DataContext = model, Page = TranscriptPage.Log }` is exactly that
            // order, and Avalonia may raise this event after both — therefore had its one read
            // skipped, and then drew an empty buffer for ever. Redrawing here could not fix that,
            // because there was nothing to draw.
            Reread();
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
    /// Which reading of the transcript this instantiation is showing — and, since Phase 45,
    /// every other one: the host mirrors the root between surfaces, so setting it here sets
    /// it everywhere. Settable without switching to the Transcript tab, because it says which
    /// mode that tab is on rather than which tab is.
    /// </summary>
    public TranscriptPage Page
    {
        get => Nav.RootKeyOf(PanelTab.Transcript) switch
        {
            LogRoot => TranscriptPage.Log,
            JournalRoot => TranscriptPage.Journal,
            RawJournalRoot => TranscriptPage.RawJournal,
            _ => TranscriptPage.Conversation,
        };

        set => Nav.SelectRoot(PanelTab.Transcript, value switch
        {
            TranscriptPage.Log => LogRoot,
            TranscriptPage.Journal => JournalRoot,
            TranscriptPage.RawJournal => RawJournalRoot,
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
    /// Draws what the microphone is doing (Phase 13, "Show that the microphone is open").
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
    /// to leave it out, because leaving it out is the default (Phase 12).
    /// </para>
    /// <para>
    /// Deferred to first selection because building it means constructing ninety-odd rows from
    /// the registry, and a Commander who never opens settings should not pay for them at
    /// startup.
    /// </para>
    /// </summary>
    /// <param name="reveal">
    /// How to show one section of the page, by the capability id that owns it, or null for a host
    /// that would rather not offer the jump (asked for 2026-08-23). Wired here rather than through
    /// a call of its own because the two facts are one fact: a surface that has no settings page
    /// has no section to reveal either, so a single argument cannot get them out of step.
    /// </param>
    public void EnableSettings(Func<Control> build, Action<string>? reveal = null)
    {
        _revealSetting = reveal;

        Furnish(
            PanelTab.Settings,
            _ => build(),
            new NavCrumb("settings", "Settings")
            {
                Help = D47.Core.Capabilities.Builtin.SettingsCapability.Id,
            });
    }

    /// <summary>How this surface shows one settings section, or null where it has no settings.</summary>
    private Action<string>? _revealSetting;

    /// <summary>
    /// What a help card naming a settings section does when pressed, or null when this surface
    /// cannot do it — which is the headset, and is why the card there stays an ordinary drill into
    /// the page about the same subject.
    /// <para>
    /// <b>Help is dismissed first, and all of it.</b> Every route that navigates away is refused
    /// while a modal crumb is up, so selecting the tab before popping would silently do nothing —
    /// and a card followed from a card is two modal levels rather than one, which is why this
    /// unwinds until there is no modal left instead of going back once.
    /// </para>
    /// </summary>
    private Action<string>? SettingsJump() =>
        _revealSetting is null || !Nav.Has(PanelTab.Settings)
            ? null
            : capabilityId =>
            {
                while (Nav.Modal && GoBack())
                {
                }

                Tab = PanelTab.Settings;
                _revealSetting(capabilityId);
            };

    /// <summary>
    /// Gives this surface the checklist (Phase 25, "The checklist leaves its window").
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
    /// <param name="goals">
    /// The Commander's long arcs (Phase 34). Null under the designer and in tests that are
    /// not about them, where the page draws no band at all rather than an empty one.
    /// </param>
    /// <param name="backfill">What "read my journals" does. Null where there is nowhere to run it.</param>
    /// <param name="sourcing">
    /// Where to buy everything a construction site still needs (Phase 50), as a second root
    /// on this tab — or null for a surface that does not get one.
    /// <para>
    /// <b>The desktop window only, for now</b>, and by not making the call rather than by any test
    /// of which surface this is: the carrier figure is typed, and typing wants a keyboard the
    /// headset has not got. That is the parity rule working as written rather than an exception to
    /// it, and the same reason the Market page is desktop-only.
    /// </para>
    /// </param>
    public void EnableChecklist(
        D47.Core.Checklists.ChecklistService checklists,
        D47.Core.Goals.GoalBook? goals = null,
        Action? backfill = null,
        Func<SourcingPage>? sourcing = null)
    {
        ChecklistPage? page = null;
        SourcingPage? shopping = null;

        var roots = new List<NavCrumb>
        {
            new("checklist", "Checklist")
            {
                // The suggestions level drilled from here inherits it: a proposal is still the
                // checklist's subject.
                Help = D47.Core.Capabilities.Builtin.ChecklistCapability.Id,
            },
        };

        if (sourcing is not null)
        {
            roots.Add(new NavCrumb(SourcingPage.RootKey, "Sourcing")
            {
                Help = D47.Core.Capabilities.Builtin.ColonisationCapability.Id,
            });
        }

        Furnish(
            PanelTab.Checklist,
            crumb => crumb.Key switch
            {
                SourcingPage.RootKey when sourcing is not null => shopping ??= sourcing(),
                ChecklistPage.SuggestionsKey => page?.BuildSuggestions()
                                                ?? new TextBlock { Text = "Nothing waiting." },
                _ => page = new ChecklistPage(checklists, Nav, Prompts, goals, backfill),
            },
            [.. roots]);

        // How many are still open, on the tab itself (asked for 2026-08-20). **Open rather than
        // every line**: a checklist's whole question is how much is left, and a count that never
        // falls as the Commander works is a number they learn to ignore.
        //
        // Redrawn on the service's own event rather than on a tick, so it follows a line being
        // ticked off, a plan being promoted and an import — all three change it and none of them
        // is a clock.
        void Count()
        {
            var open = checklists.Document.Items.Count(item => item.IsLive && !item.IsComplete);

            // The number is gone from the tab (#234). It could not survive the strip collapsing
            // to marks — a count beside a picture is a badge, and a badge on one of eight tabs is
            // a thing the Commander has to decode. The checklist page carries it, which is where
            // somebody wondering how much is left is going anyway.
            _ = open;
        }

        // The store's event, which is the one the page itself listens to — so the tab and the page
        // cannot come to disagree about how many there are.
        checklists.List.Changed += () => Dispatcher.UIThread.Post(Count);
        Count();
    }

    /// <summary>
    /// Gives this surface the fleet, what the Commander is wearing, and the arithmetic between
    /// them (Phase 26, "Ships"; Phase 27, "Suits and weapons, and the gap").
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
        D47.Core.Loadout.OnFootPlanService? onFoot = null,
        Func<D47.Core.Journal.ModulePower>? modulePower = null)
    {
        var shipsMode = new ShipsMode(ships, checklists, state, modulePower);

        // Kept, so the tick has something to invalidate (remediation.md 17, item 7).
        _loadoutMode = shipsMode;
        _loadoutState = state;

        var modes = new List<ILoadoutMode> { shipsMode };

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

        // Per root, because this tab's roots are three different subjects. Ships has a band;
        // Suits and Gap do not yet, so those two simply offer no mark — which is the whole
        // reason help is declared per crumb rather than per tab.
        var roots = new List<NavCrumb>
        {
            new(LoadoutPages.FleetRoot, "Ships")
            {
                Help = D47.Core.Capabilities.Builtin.ShipsCapability.Id,
            },
        };

        if (onFoot is not null)
        {
            roots.Add(new NavCrumb(OnFootMode.Root, "Suits")
            {
                Help = D47.Core.Capabilities.Builtin.OnFootCapability.Id,
            });

            roots.Add(new NavCrumb(LoadoutPages.GapRoot, "Gap")
            {
                Help = D47.Core.Capabilities.Builtin.GapCapability.Id,
            });
        }

        // The carrier, on the tab that took its name (#230). Registered whenever the tab is
        // furnished rather than only for a Commander who owns one: the page says which of "you
        // have no carrier" and "d47 has not seen one" it means, and only the second is something
        // it can know before the management panel has been opened once.
        _carrier = new CarrierSource(
            () => state()?.Carrier ?? D47.Core.Journal.CarrierState.None,
            () => state()?.SquadronCarrier ?? D47.Core.Journal.CarrierState.NoSquadron);

        // No help declared: no capability page covers the carrier yet, and a root whose page has
        // no band simply shows no mark. Declaring one that does not exist would put a question
        // mark on the page that opens nothing.
        roots.Add(new NavCrumb(LoadoutPages.CarrierRoot, "Carrier"));

        Furnish(
            PanelTab.Loadout,
            crumb => LoadoutPages.Build(crumb, modes, gap, _carrier, Nav, Prompts),
            [.. roots]);
    }

    /// <summary>
    /// Gives this surface the Commander's adventures (Phase 47). The desktop window only,
    /// for now: the editor is typing, and the reading level is one call away when the headset
    /// wants it — which is the parity rule working as written rather than an exception to it.
    /// </summary>
    public void EnableAdventures(AdventureSurface surface)
    {
        AdventuresPage? page = null;

        Furnish(
            PanelTab.Adventures,
            crumb => crumb.Key == AdventuresPage.RootKey
                ? page = new AdventuresPage(surface, Nav, Prompts)
                : page?.Build(crumb) ?? new TextBlock { Text = "Nothing here." },
            new NavCrumb(AdventuresPage.RootKey, "Adventures")
            {
                Help = D47.Core.Capabilities.Builtin.AdventureCapability.Id,
            });

        // And the same story at mini's size (asked for 2026-08-22). Built here rather than lazily,
        // because it is small and because mini has no navigation to build it on the way into — the
        // Commander switches the headset to mini and it is either there or it is not.
        MiniPane.Child = new AdventureMini(surface);
        ApplyChrome();
    }

    /// <summary>
    /// Gives this surface the engineer directory and the solver (Phase 28, "Engineers").
    /// <para>
    /// Both surfaces, like the checklist and the loadout: a Commander deciding who to fly to next
    /// is very often the Commander already sitting in the ship.
    /// </para>
    /// <para>
    /// Two roots rather than two tabs, on the same reading as Loadout's three — the Directory and
    /// the Route are two answers to <em>which engineer</em>, not two destinations. Each keeps its
    /// own drill state, so leaving the Directory inside one engineer and looking at the Route does
    /// not disturb it.
    /// </para>
    /// </summary>
    public void EnableEngineers(
        D47.Core.Engineers.EngineerPlanService unlocks,
        D47.Core.Ships.ShipPlanService ships,
        Func<D47.Core.Journal.CommanderGameState?> state,
        D47.Core.Loadout.OnFootPlanService? onFoot = null)
    {
        var source = new EngineerSource(unlocks.Report, engineer => unlocks.Promote(engineer));

        // A plan moving changes who is worth flying to, and neither store knows about this page.
        ships.Store.Changed += source.Invalidate;

        if (onFoot is not null)
        {
            onFoot.Store.Changed += source.Invalidate;
        }

        _engineers = source;
        _engineerStamp = D47.Core.Engineers.UnlockPlanner.Stamp(state());
        _engineerState = state;

        // The first tab whose help is drawn in the panel rather than opened in a browser. Declared
        // on the roots, so the engineer levels drilled from them inherit it — and both surfaces
        // reach this call, which is the point: a Commander in a headset cannot see a browser.
        var help = D47.Core.Capabilities.Builtin.EngineerCapability.Id;

        Furnish(
            PanelTab.Engineers,
            crumb => EngineersPages.Build(crumb, source, Nav),
            new NavCrumb(EngineersPages.DirectoryRoot, "Directory") { Help = help },
            new NavCrumb(EngineersPages.RouteRoot, "Route") { Help = help });
    }

    /// <summary>
    /// Redraws the engineer pages when the Commander has moved, re-fitted or unlocked somebody.
    /// <para>
    /// <b>On a change rather than on the tick.</b> Every distance on those pages is measured from
    /// where the Commander is standing, so flying makes them stale — and rebuilding thirty-eight
    /// rows every tick would cost the tab its hit-testing, because a rebuilt control has no bounds
    /// until the next layout pass and a ray aimed at a row would land on the one beside it.
    /// <see cref="D47.Core.Engineers.UnlockPlanner.Stamp"/> is what makes the question cheap to
    /// ask without computing the answer.
    /// </para>
    /// </summary>
    public bool TickEngineers()
    {
        if (_engineers is not { } source || Tab != PanelTab.Engineers)
        {
            return false;
        }

        var stamp = D47.Core.Engineers.UnlockPlanner.Stamp(_engineerState?.Invoke());

        if (string.Equals(stamp, _engineerStamp, StringComparison.Ordinal))
        {
            // It already knew this and used to say so to nobody, which is the whole of the
            // flicker: the headset marked the surface dirty on the strength of having called
            // this method at all (#23).
            return false;
        }

        _engineerStamp = stamp;
        source.Invalidate();

        return true;
    }

    private EngineerSource? _engineers;
    private string _engineerStamp = string.Empty;
    private Func<D47.Core.Journal.CommanderGameState?>? _engineerState;

    /// <summary>
    /// Redraws the Loadout tab when the journal says the ship changed (remediation.md 17, item 7).
    /// <para>
    /// The tab had no game-state signal at all: its pages redrew when the plans file was saved,
    /// which is half of what they show. Reported as a fleet whose modules read <em>not seen</em>
    /// after switching to that very ship in Elite — the page was answering with the loadout it had
    /// when it was opened.
    /// </para>
    /// <para>
    /// <b>Reference identity rather than a computed stamp.</b> <c>ShipLoadout.Apply</c> returns the
    /// same instance for every event that is not a <c>Loadout</c> or a rename, and a new record for
    /// the ones that are — so a reference comparison is exact and costs nothing ten times a second,
    /// where a stamp over thirty-odd modules would cost a string per tick to answer "no" with.
    /// </para>
    /// </summary>
    public void TickLoadout()
    {
        if (_loadoutMode is not { } mode || Tab != PanelTab.Loadout)
        {
            return;
        }

        // The carrier moves on its own events rather than with the ship, so it is compared
        // separately (#230): a jump booked while the Commander is nowhere near it changes this
        // page and changes nothing about the hull they are sitting in. Same reference test, and
        // it is exact for the same reason — CarrierState is replaced rather than mutated.
        var carrier = _loadoutState?.Invoke()?.Carrier;
        var squadron = _loadoutState?.Invoke()?.SquadronCarrier;

        if (!ReferenceEquals(carrier, _carrierSeen) || !ReferenceEquals(squadron, _squadronSeen))
        {
            _carrierSeen = carrier;
            _squadronSeen = squadron;
            _carrier?.Invalidate();
        }

        var current = _loadoutState?.Invoke()?.Ship;

        if (ReferenceEquals(current, _loadoutSeen))
        {
            return;
        }

        _loadoutSeen = current;
        mode.Invalidate();
    }

    private ShipsMode? _loadoutMode;
    private Func<D47.Core.Journal.CommanderGameState?>? _loadoutState;
    private D47.Core.Journal.ShipLoadout? _loadoutSeen;
    private D47.Core.Journal.CarrierState? _carrierSeen;
    private D47.Core.Journal.CarrierState? _squadronSeen;

    /// <summary>
    /// Gives this surface the clocks, timers and alarms (Phase 24, "Utilities").
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
            new NavCrumb("utilities", "Utilities")
            {
                Help = D47.Core.Capabilities.Builtin.UtilitiesCapability.Id,
            });
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
    /// <summary>
    /// One frame of the <em>d47 is composing</em> animation, and whether anything moved
    /// (asked for 2026-08-22).
    /// <para>
    /// Driven from the same 10 Hz tick the clocks are, and for the mirror-image reason: a clock
    /// changes with nothing having happened, and this changes because something <em>is</em>
    /// happening and has not finished. The return value is what the headset's surface sets its
    /// dirty flag from — an animation nobody is looking at must not re-rasterise the panel.
    /// </para>
    /// <para>
    /// Found by walking the tree rather than by keeping a list: the indicator appears on a card, on
    /// the reading level and on the mini panel, each rebuilt whenever the book changes, and a list
    /// of live ones is a list that goes stale in three places instead of none. Guarded on the tab
    /// first, so the walk does not happen at all on the surfaces and moments it cannot matter to.
    /// </para>
    /// </summary>
    public bool TickAdventures()
    {
        if (Tab != PanelTab.Adventures)
        {
            return false;
        }

        var moved = false;

        foreach (var pulse in this.GetVisualDescendants().OfType<AdventureThinking>())
        {
            if (pulse.IsEffectivelyVisible)
            {
                moved |= pulse.Beat();
            }
        }

        return moved;
    }

    public bool TickClocks()
    {
        if (Tab != PanelTab.Utilities)
        {
            return false;
        }

        return _utilities?.Refresh() ?? false;
    }

    private UtilitiesPage? _utilities;

    /// <summary>
    /// Gives this surface the Routing tab (Phase 37): where the Commander is going, in
    /// three readings of one journey.
    /// <para>
    /// <b>The roots are flags rather than a fixed three</b>, because the surfaces do not want the
    /// same ones. Only the desktop window furnishes this at all today; if the headset ever gets
    /// it, it gets <em>Progress</em> and only Progress — the mode that needs no keyboard and is
    /// the one worth reading at a metre while actually flying the route. That is one call with a
    /// different set of flags rather than a restructuring, which is the whole reason they are
    /// flags.
    /// </para>
    /// </summary>
    public void EnableRouting(
        RoutingSurface surface,
        bool plan = true,
        bool progress = true,
        bool course = true,
        bool market = true)
    {
        var roots = new List<NavCrumb>();

        if (plan)
        {
            roots.Add(new NavCrumb(RoutingPages.PlanRoot, "Plan")
            {
                Help = D47.Core.Capabilities.Builtin.RouteCapability.Id,
            });
        }

        if (progress)
        {
            roots.Add(new NavCrumb(RoutingPages.ProgressRoot, "Progress")
            {
                Help = D47.Core.Capabilities.Builtin.RouteCapability.Id,
            });
        }

        if (course)
        {
            roots.Add(new NavCrumb(RoutingPages.CourseRoot, "Course")
            {
                Help = D47.Core.Capabilities.Builtin.NavigationCapability.Id,
            });
        }

        // Last of the four, because it is the newest and because the three before it are the
        // journey and this is the errand (Phase 49).
        if (market && surface.Commodities is not null)
        {
            roots.Add(new NavCrumb(RoutingPages.MarketRoot, "Market")
            {
                Help = D47.Core.Capabilities.Builtin.GalaxyCapability.Id,
            });
        }

        if (roots.Count == 0)
        {
            return;
        }

        _routeState = surface.Route;
        _routeHere = surface.Here;
        _routeRange = surface.JumpRange;

        Furnish(
            PanelTab.Routing,
            crumb =>
            {
                var page = RoutingPages.Build(crumb, surface, Nav);

                // Held onto so the tick can redraw Progress and a plot made elsewhere can
                // redraw Plan. Assigned rather than accumulated: a root is rebuilt when it is
                // selected, so the last one built is the one on screen.
                _routeProgress = page as RouteProgressPage ?? _routeProgress;
                _routePlan = page as RoutePlanPage ?? _routePlan;

                return page;
            },
            [.. roots]);

        // A plot made anywhere - this tab's own button, or a spoken tool call - leaves the
        // Plan page one redraw out of date, because "show the last one" is drawn from the book.
        // The book being shared is precisely what makes this one subscription rather than a
        // guess about who plotted. It redraws the page and never navigates: a Commander reading
        // a result when a plot lands elsewhere should stay where they are.
        if (surface.Plans is { } plans)
        {
            plans.Changed += () => _routePlan?.Refresh();
        }
    }

    /// <summary>
    /// Redraws the route being flown, from the host's tick.
    /// <para>
    /// Reference identity on the route and one string comparison on where the Commander is, which
    /// is the same arrangement <see cref="TickLoadout"/> uses and for the same reason:
    /// <c>NavRouteReader</c> hands back the same record until the file's write time moves, so
    /// answering "nothing changed" costs a pointer comparison ten times a second rather than a
    /// walk over a hundred and thirty hops.
    /// </para>
    /// </summary>
    public void TickRouting()
    {
        if (Tab != PanelTab.Routing)
        {
            return;
        }

        var route = _routeState?.Invoke();
        var here = _routeHere?.Invoke();

        // And the ship's range, which the plan forms quote (#253). Watched here rather than left
        // to the two above, because a refit changes what the placeholder should say without the
        // Commander having moved or the route having changed.
        var range = _routeRange?.Invoke();

        if (ReferenceEquals(route, _routeSeen)
            && string.Equals(here, _routeWhere, StringComparison.Ordinal)
            && Nullable.Equals(range, _routeRangeSeen))
        {
            return;
        }

        _routeSeen = route;
        _routeWhere = here;
        _routeRangeSeen = range;

        // The progress page only where a host furnished one — the plan forms are furnished
        // separately, and a surface with one and not the other used to return before either.
        _routeProgress?.Refresh();

        // RefreshSupplied rather than Refresh: this fires on every jump, and rebuilding the page
        // then would throw away a half-typed destination.
        _routePlan?.RefreshSupplied();
    }

    private RouteProgressPage? _routeProgress;
    private RoutePlanPage? _routePlan;
    private Func<D47.Core.Journal.NavRoute>? _routeState;
    private Func<string?>? _routeHere;
    private Func<double?>? _routeRange;
    private D47.Core.Journal.NavRoute? _routeSeen;
    private string? _routeWhere;
    private double? _routeRangeSeen;

    /// <summary>
    /// Gives this surface a tab, built by <paramref name="build"/> the first time it is selected,
    /// with the roots it offers (Phase 25).
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
    /// the controller button and the phrase (Phase 25). Says whether there was anything
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
    /// button that copies the page (Phase 19, "Copy log").
    /// <para>
    /// Enabled by the host, like the settings page and for the same reason: the desktop window is
    /// the surface with a keyboard and a clipboard. Mini shows no strip at all and the headset
    /// has neither, and a search box the Commander cannot type into is worse than no search.
    /// </para>
    /// </summary>
    /// <summary>
    /// How tall the rows this surface keeps in mini <em>beyond the headset's</em> want to be at a
    /// given width, or zero where it keeps none (Phase 51).
    /// <para>
    /// <b>This is what makes the window's mini size measured rather than typed.</b> Mini is
    /// 512x280 in the headset for a stated reason — apparent size there is the pixel count and the
    /// quad's width in metres together, so the height is a floor under a reduced content set. The
    /// window's mini keeps things the headset's does without: the ask line, and the control that
    /// leaves mini. So its height is that floor plus whatever those actually want, and a surface
    /// that furnished neither lands back on the headset's number exactly.
    /// </para>
    /// <para>
    /// A sum rather than one row, because the list has already grown once: it was the ask line
    /// alone until the drawn way out arrived a day later, and a height that forgets a row it is
    /// drawing takes the difference out of the transcript, silently.
    /// </para>
    /// <para>
    /// Measured at the unscaled width and left to the caller to scale, because the zoom is a
    /// <c>LayoutTransform</c> outside this tree: everything in here is laid out at 100% and drawn
    /// larger, which is the whole reason a mini window at 150% is a bigger mini window rather than
    /// a clipped one.
    /// </para>
    /// </summary>
    public double MiniExtraHeight(double width) => Wanted(AskRow, width) + Wanted(ModeRow, width);

    /// <summary>
    /// What one row wants, or zero when it is not drawn.
    /// <para>
    /// Invalidated afterwards. Measuring out of band leaves the control marked as measured against
    /// a constraint the layout pass never gave it, and the arrange that follows would use it.
    /// </para>
    /// </summary>
    private static double Wanted(Control row, double width)
    {
        if (!row.IsVisible)
        {
            return 0;
        }

        row.Measure(new Size(width, double.PositiveInfinity));

        var wanted = row.DesiredSize.Height;

        row.InvalidateMeasure();

        return wanted;
    }

    /// <summary>
    /// Moves the page this surface is showing, by however much was asked for
    /// (<a href="https://github.com/dseelinger/d47/issues/34">#34</a>).
    /// <para>
    /// <b>Whichever region is on screen</b>, rather than the transcript alone: mini and the flat
    /// strip carry the checklist and the engineer pages now, and those are the ones with more in
    /// them than fits. A modal wins over all of them, because a chooser is a level of the stack and
    /// the page behind it is not what the Commander is reading.
    /// </para>
    /// <para>
    /// <b>Following is decided here rather than left to the scroll handler.</b> Scrolling up by
    /// voice means what scrolling up by hand means — stop following the newest line — and
    /// <see cref="OnTranscriptScrolled"/> would normally conclude that on its own. It cannot be
    /// relied on to: on the headset's offscreen surface the change is not raised until the next
    /// layout pass, which happens inside the render that has already called
    /// <see cref="KeepUp"/> — so the page scrolled up and was pulled straight back to the bottom,
    /// once per frame, and the Commander saw nothing move at all. A deliberate scroll says where it
    /// landed rather than waiting to be told.
    /// </para>
    /// <para>
    /// <b>Three answers rather than two</b> (#263). <see cref="PanelScrollOutcome.NothingToScroll"/>
    /// is no scroller in the region, or a layout that has not happened yet;
    /// <see cref="PanelScrollOutcome.AlreadyThere"/> is a page that exists and is at that end. They
    /// were one <c>false</c>, which the caller could only read as "not a scroll" — so a Commander
    /// at the bottom of a page had their sentence handed to the language model.
    /// </para>
    /// </summary>
    public PanelScrollOutcome Scroll(PanelScrollStep step)
    {
        if (ActiveScroller() is not { } scroller)
        {
            return PanelScrollOutcome.NothingToScroll;
        }

        var viewport = scroller.Viewport.Height;

        if (viewport <= 0 || scroller.Extent.Height <= viewport)
        {
            return PanelScrollOutcome.NothingToScroll;
        }

        // A page is a screenful less one line, so the line a Commander was reading when they said
        // it is still there when the page settles. The same overlap a browser leaves.
        var line = Transcript.FontSize * 1.4;

        var by = step switch
        {
            PanelScrollStep.PageDown => Math.Max(line, viewport - line),
            PanelScrollStep.PageUp => -Math.Max(line, viewport - line),
            PanelScrollStep.LineDown => line * PanelScroll.Lines,
            _ => -line * PanelScroll.Lines,
        };

        var was = scroller.Offset.Y;
        var wanted = Math.Clamp(was + by, 0, Math.Max(0, scroller.Extent.Height - viewport));

        if (Math.Abs(wanted - was) < 0.5)
        {
            // Already at that end, and said so rather than merely not moving: a Commander who says
            // "page down" at the bottom should hear that they are at the bottom rather than watch
            // nothing and wonder whether they were heard. Until #263 this was the same answer as
            // "there is nothing here to scroll", and both of them reached the model.
            return PanelScrollOutcome.AlreadyThere;
        }

        scroller.Offset = scroller.Offset.WithY(wanted);

        if (ReferenceEquals(scroller, TranscriptScroller))
        {
            _following = AtTheNewest();
            ShowFollowButton();
        }

        return PanelScrollOutcome.Moved;
    }

    /// <summary>
    /// The scroller for whatever region is showing, or null where the region has none.
    /// <para>
    /// Found rather than held, because which region is on screen is <see cref="ApplyChrome"/>'s
    /// answer and it changes with the tab, the mode and whether a chooser is open. Asking the
    /// visible pane is the one question that stays right as those move.
    /// </para>
    /// </summary>
    private ScrollViewer? ActiveScroller()
    {
        var pane = ModalPane.IsVisible ? ModalPane
            : TranscriptPane.IsVisible ? TranscriptPane
            : MiniPane.IsVisible ? MiniPane
            : PagePane.IsVisible ? PagePane
            : null;

        if (pane is null)
        {
            return null;
        }

        return ReferenceEquals(pane, TranscriptPane)
            ? TranscriptScroller
            : pane.GetVisualDescendants().OfType<ScrollViewer>().FirstOrDefault();
    }

    /// <summary>
    /// Draws a control that switches this surface between full and mini (asked for 2026-08-24).
    /// <para>
    /// <b>This softens a Phase 51 ruling on the Commander's instruction.</b> That phase said the
    /// way back must not live in the thing that disappears, and listed three that do not: the
    /// hotkey, the spoken phrase and the title bar. The reasoning still holds and none of those
    /// three has been taken away — but the first thing said on meeting a mini window was that
    /// there needs to be a control you can see. A way out that has to be explained is a way out
    /// half the people who need it will not have been told about, and the argument was only ever
    /// that a drawn control must not be the <em>only</em> one.
    /// </para>
    /// <para>
    /// Furnished rather than branched, like <see cref="EnableAskInMini"/> and
    /// <see cref="EnableTurnDetails"/>: the headset's mini is untouched, and the flat overlay of
    /// Phase 48 does not draw a button the pointer would pass straight through.
    /// </para>
    /// </summary>
    public void EnableModeToggle(Action<PanelMode> switchTo)
    {
        _switchMode = switchTo;
        ApplyChrome();
    }

    private void OnModeToggleClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e) =>
        _switchMode?.Invoke(Mode == PanelMode.Full ? PanelMode.Mini : PanelMode.Full);

    /// <summary>
    /// Keeps the ask line on this surface in mini (Phase 51).
    /// <para>
    /// <c>AskRow</c> is hidden in mini everywhere else, and that is right for a headset with no
    /// keyboard and wrong for the one surface whose entire point is a keyboard. <b>A mini window
    /// you cannot type into is worse than the full window in every respect</b> and would be
    /// switched off inside a day, which makes this the difference between the feature landing and
    /// the feature merely being built.
    /// </para>
    /// <para>
    /// <b>Furnished rather than branched.</b> The host that wants it says so, the way
    /// <see cref="EnableSettings"/> and <see cref="EnableChecklist"/> already work — so no code
    /// anywhere tests which surface it is on, the headset's mini is untouched, and the flat
    /// overlay (Phase 48) stays output-only by simply not calling this.
    /// </para>
    /// </summary>
    public void EnableAskInMini()
    {
        _asksInMini = true;
        ApplyChrome();
    }

    public void EnableSearch()
    {
        _searchable = true;
        ApplyChrome();
    }

    /// <summary>
    /// Opens the sharing window. Null on every surface but the one that furnished it
    /// (<a href="https://github.com/dseelinger/d47/issues/160">#160</a>).
    /// </summary>
    private Action<bool>? _donate;

    /// <summary>
    /// Offers the way into a bug report: a scrubbed window of Elite's events and d47's own log —
    /// or, where the page shows the journals, the whole history — shown or described in full and
    /// sent nowhere until the Commander says so (#160, #174, one button since #238).
    /// <para>
    /// <b>Furnished rather than branched</b>, like <see cref="EnableSearch"/> above, and here that
    /// is the same safety property <see cref="EnableRawJournal"/> records: the review step is the
    /// whole of the consent, and the headset has neither a clipboard to put the result on nor a
    /// file picker to write it with. A surface that cannot complete the act does not offer it.
    /// </para>
    /// <para>
    /// The panel holds the affordance and nothing else — what a window is and where the readings
    /// come from are the host's. The one thing the panel does say is the argument: whether the
    /// page the button was pressed on shows Elite's journals, because the history half is the
    /// journals alone and the Log page does not show them. The two consent shapes inside the
    /// window stay two shapes; what merged is the surface.
    /// </para>
    /// </summary>
    public void EnableDonation(Action<bool> open)
    {
        _donate = open;
        ApplyChrome();
    }

    /// <summary>
    /// Adds the Raw Journal reading (#51), on a surface that has somewhere useful to put it.
    /// <para>
    /// <b>Furnished rather than registered, which is what keeps it off the headset.</b> Journal is
    /// registered for every surface because a sentence at a metre is readable; a wall of JSON is
    /// not, and it is there to be selected and pasted into a bug report — an act with no meaning in
    /// mid-air. Same seam as <see cref="EnableSearch"/> and for the same reason: the surface that
    /// wants it says so, and no code anywhere tests which surface it is on.
    /// </para>
    /// </summary>
    public void EnableRawJournal()
    {
        Nav.Register(
            PanelTab.Transcript,
            new NavCrumb(RawJournalRoot, "Raw Journal") { Help = JournalHelp });

        DrawModes();
    }

    /// <summary>
    /// Keeps the Raw switch where the Commander left it, across launches (#267).
    /// <para>
    /// <b>Handed to one surface, and it governs both.</b> The transcript root is mirrored between
    /// the window and the headset (Phase 45), so a flick in mid-air arrives at this navigator too
    /// and is recorded here — and a restore made here is carried back the same way. Two
    /// surfaces each writing the one fact would be two writers of one record with nothing deciding
    /// between them.
    /// </para>
    /// </summary>
    public void RememberJournalReading(JournalReadingMemory memory) => _journalReading = memory;

    /// <summary>
    /// Puts every tab back on the reading it was left on, and keeps them there (#268).
    /// <para>
    /// <b>Called after the tabs are furnished, because a root can only be selected once it is
    /// registered.</b> <see cref="PanelNavigator.SelectRoot(PanelTab, string)"/> declines a key
    /// that is not a root of the tab named, so a renamed reading, a tab this surface never
    /// furnished and a hand-edited file all cost a first reading rather than raising.
    /// </para>
    /// <para>
    /// <b>One surface remembers.</b> The transcript root is mirrored between the window and the
    /// headset (Phase 45), so restoring here gives the headset its reading for free; every other
    /// tab's root is per-surface by design and the headset keeps its own.
    /// </para>
    /// </summary>
    public void RememberRoots(PanelRootMemory memory)
    {
        _roots = memory;

        foreach (var (tab, root) in memory.All)
        {
            if (Enum.TryParse<PanelTab>(tab, out var which))
            {
                Nav.SelectRoot(which, root);
            }
        }

        RecordRoots();
    }

    /// <summary>Where each tab was left, or null on a surface not asked to remember it.</summary>
    private PanelRootMemory? _roots;

    /// <summary>
    /// Writes down the reading every furnished tab is on (#268). Called from every navigation and
    /// costing nothing until one of them actually moves.
    /// <para>
    /// <b>Raw Journal is written down as the journal.</b> It is a root like any other to the
    /// navigator, so storing it would restore it — and the Transcript would open on a wall of JSON,
    /// which is the thing <see cref="ApplyRememberedJournalReading"/> exists to avoid. How the
    /// journal reading is drawn is the switch's fact and is kept once, by
    /// <see cref="JournalReadingMemory"/>.
    /// </para>
    /// </summary>
    private void RecordRoots()
    {
        if (_roots is null)
        {
            return;
        }

        foreach (var tab in Enum.GetValues<PanelTab>())
        {
            if (!Nav.Has(tab))
            {
                continue;
            }

            var root = Nav.RootKeyOf(tab);

            _roots.Remember(tab, root == RawJournalRoot ? JournalRoot : root);
        }
    }

    /// <summary>Where the Raw switch was left, or null on a surface not asked to remember it.</summary>
    private JournalReadingMemory? _journalReading;

    /// <summary>
    /// Which transcript reading this surface last saw, kept apart from <see cref="_showingRoot"/>
    /// because that one is about whichever tab is drawn and this question is about the Transcript
    /// tab whether or not it is the one showing.
    /// </summary>
    private string _journalReadingWas = ConversationRoot;

    /// <summary>
    /// Applies the remembered Raw position, and records it when it moves (#267). True when it
    /// navigated, which means the caller's own work is about to be redone by the change it raised.
    /// <para>
    /// <b>On arriving at the journal reading rather than at launch.</b> Raw is a root of the
    /// Transcript tab exactly like the journal is — the picker declines to list it and
    /// <see cref="DrawModes"/> normalises it away, but the navigator holds it as a root all the
    /// same. So restoring it as the tab's root would open a Commander who left d47 on raw into a
    /// wall of JSON on the tab the panel starts on. What is remembered is how the reading is drawn
    /// once it is opened.
    /// </para>
    /// <para>
    /// <b>Arriving from raw is the one move this must not undo.</b> That is the switch having just
    /// been turned off, and it reads as "the journal reading was opened" from every angle except
    /// where it was opened from — which is why the previous reading is kept rather than only
    /// the current one.
    /// </para>
    /// </summary>
    private bool ApplyRememberedJournalReading()
    {
        // Only where a host furnished the raw reading. Asked of the navigator rather than held as
        // a flag, the same way DrawRawToggle asks it.
        if (_journalReading is null || !Nav.Roots(PanelTab.Transcript).Any(root => root.Key == RawJournalRoot))
        {
            return false;
        }

        var was = _journalReadingWas;
        var now = Nav.RootKeyOf(PanelTab.Transcript);

        _journalReadingWas = now;

        if (now == RawJournalRoot)
        {
            _journalReading.Remember(true);
            return false;
        }

        if (now != JournalRoot)
        {
            return false;
        }

        if (was == RawJournalRoot)
        {
            _journalReading.Remember(false);
            return false;
        }

        return _journalReading.Raw && Nav.SelectRoot(PanelTab.Transcript, RawJournalRoot);
    }

    /// <summary>
    /// Where the Commander dragged the rules between panes, on the one surface that has a mouse
    /// (Phase 55). Null everywhere else, which is what keeps this the window's alone.
    /// </summary>
    /// <summary>The carrier the Fleet tab draws, once a host furnished the tab (#230).</summary>
    private CarrierSource? _carrier;

    private PaneWidthMemory? _paneWidths;

    /// <summary>
    /// Lets the mouse drag the rule between two panes, on every tab at once (Phase 55).
    /// <para>
    /// Furnished rather than branched, like <see cref="EnableSearch"/> and
    /// <see cref="EnableTurnDetails"/> — and here that is a safety property rather than a
    /// convention. The headset drives this same view through a geometric hit test, so a handle
    /// that existed there would be draggable by the ray, and the ask names the mouse and only the
    /// mouse. The headset simply never calls this.
    /// </para>
    /// <para>
    /// Reaches strips already built as well as ones built later, the way
    /// <see cref="ApplyChrome"/> does: whichever tab the Commander is looking at when this is
    /// called is the one they would try it on first.
    /// </para>
    /// </summary>
    public void EnableDraggablePanes(PaneWidthMemory memory)
    {
        _paneWidths = memory;

        foreach (var page in _pages.Values)
        {
            page.EnableDrag(memory);
        }
    }

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

    /// <summary>
    /// How the host opens the documentation site, when it gave a way. Null on every surface that
    /// was not handed one, which is the headset's copy: the button used to raise an event on the
    /// shared model, and the only handler opened a browser on the desktop, where a Commander in
    /// the headset could not see it (change-requests.md 24). Same seam as
    /// <see cref="EnableTurnDetails"/>, for the same reason.
    /// </summary>
    private Action<string>? _openHelp;

    /// <summary>
    /// Gives this surface a way out to the web. The desktop window calls it; the headset never
    /// does, and every link on a help page is drawn differently as a result.
    /// </summary>
    public void EnableHelp(Action<string> open)
    {
        _openHelp = open;
        ShowHelpAffordance();
    }

    /// <summary>
    /// The mark shows when this surface can do something with it: open the site, which only the
    /// desktop can, or draw the help for where the Commander is standing, which either surface
    /// can. The headset gets one for the second reason — which is the whole point, since the
    /// reason it lost the button was that the only thing behind it was a browser it could not see.
    /// </summary>
    /// <summary>
    /// Whether this instantiation is a surface nothing can be pressed on — the flat overlay, and
    /// the headset's mini panel (change-requests.md 42).
    /// <para>
    /// <b>Read from the class rather than from a second flag</b>, so there is one answer and the
    /// hosts keep setting it the one way they already do.
    /// </para>
    /// <para>
    /// <b>Why any code has to ask at all.</b> The <c>output-only</c> style hides an exact
    /// <c>Button</c> and reaches every one a furnished page brings with it, which is the whole
    /// argument for a selector over a pass. It cannot reach a button whose <c>IsVisible</c> is
    /// assigned here: in Avalonia a local value outranks a style setter, so the two buttons this
    /// class sets by hand ignored the rule and stayed on the headset's mini panel. Those two ask;
    /// nothing else needs to, and <c>MiniInTheHeadsetCarriesNoButtonsTests</c> is what catches the
    /// next one that starts setting its own visibility and forgets.
    /// </para>
    /// </summary>
    private bool OutputOnly => Classes.Contains("output-only");

    private void ShowHelpAffordance() =>
        HelpButton.IsVisible = !OutputOnly
            && (_openHelp is not null
                || HelpPageView.Exists(Nav.Help)
                || HelpPageView.Exists(HelpLevel.Index));

    /// <summary>
    /// Shows the pre-release mark beside the help glyph, or takes it away
    /// (<a href="https://github.com/dseelinger/d47/issues/92">#92</a>).
    /// <para>
    /// <b>Off on the headset's copy</b>, by the same <see cref="OutputOnly"/> lever the help button
    /// uses and for the same stated reason: a local <c>IsVisible</c> outranks a style setter, so
    /// the buttons this class furnishes by hand have to ask, and this is a third one that asks.
    /// A Commander in a headset is flying; which build they are on is not a question they are
    /// asking mid-flight, and chrome in an overlay cannot be dismissed.
    /// </para>
    /// <para>
    /// <b>Takes the mark away as readily as it puts it up</b>, because promoting a pre-release
    /// changes the answer without changing the binary — so this can be called again with a
    /// different channel and must not be a one-way door.
    /// </para>
    /// </summary>
    public void ShowChannel(D47.Core.Updates.ReleaseChannel channel)
    {
        // The wording comes from Core with the rest of it, so the badge cannot say one thing while
        // the title bar and About say another - which is the whole reason that text lives there.
        var marker = D47.Core.Updates.ReleaseChannelText.Short(channel);

        _channel = channel;
        PreReleaseBadge.IsVisible = !OutputOnly && marker is not null;

        if (marker is not null)
        {
            PreReleaseBadgeText.Text = marker.ToUpperInvariant();
        }

        ShowBuildDetailAffordance();
    }

    /// <summary>
    /// Which channel the badge is showing, so the affordance can be settled whichever order the
    /// host furnishes it in — <see cref="EnableBuildDetails"/> happens at construction and
    /// <see cref="ShowChannel"/> when GitHub has answered, and neither may depend on being second.
    /// </summary>
    private D47.Core.Updates.ReleaseChannel _channel = D47.Core.Updates.ReleaseChannel.Unknown;

    /// <summary>What opens the list of what this build worked, or null where nothing can.</summary>
    private Action? _openBuildDetails;

    /// <summary>
    /// Makes the build badge open what this build worked
    /// (<a href="https://github.com/dseelinger/d47/issues/207">#207</a>).
    /// <para>
    /// <b>Furnished by the host rather than gated here</b>, the same way <see cref="EnableHelp"/>
    /// is and for the stronger version of its reason. In the headset a click would open a browser
    /// on a monitor the Commander cannot see — the argument that took the help button off that
    /// surface — so the headset host simply does not make this call and the handler does not
    /// exist there to be found. That is a firmer gate than a visibility rule: <c>#202</c> is open
    /// precisely because a local <c>IsVisible</c> outranks a style setter, and a control nobody
    /// wired outranks both.
    /// </para>
    /// </summary>
    public void EnableBuildDetails(Action open)
    {
        _openBuildDetails = open;
        ShowBuildDetailAffordance();
    }

    /// <summary>
    /// Whether the badge is something to press, and it says so with the cursor.
    /// <para>
    /// <see cref="OutputOnly"/> is asked as well as the host having furnished anything, because
    /// the two answer different questions — one surface may never take a pointer, and one build
    /// may have nothing to show — and a badge on a published release must go on being the plain
    /// mark it has always been.
    /// </para>
    /// </summary>
    private void ShowBuildDetailAffordance()
    {
        var clickable = !OutputOnly && PreReleaseBadge.IsVisible && _openBuildDetails is not null;

        PreReleaseBadge.Cursor = clickable
            ? new Avalonia.Input.Cursor(Avalonia.Input.StandardCursorType.Hand)
            : Avalonia.Input.Cursor.Default;

        // And it says so in colour as well as in the cursor (#234). The cursor only speaks once
        // the pointer is already on it, which is no use to a Commander deciding whether to move
        // the pointer at all — so a badge that opens something carries the accent, which is what
        // everything else pressable on this surface does.
        //
        // Only when it is pressable: a badge on a published release opens nothing, and it goes on
        // being the plain mark it has always been. Bound rather than assigned, so a theme switched
        // afterwards repaints it — a brush read once at this moment would freeze the old theme's
        // colour, which is the trap Glyphs.Draw exists to refuse.
        var key = clickable
            ? Theming.ThemeManager.AccentKey
            : Theming.ThemeManager.TextMutedKey;

        PreReleaseBadge.Bind(Border.BorderBrushProperty, this.GetResourceObservable(key));
        PreReleaseBadgeText.Bind(TextBlock.ForegroundProperty, this.GetResourceObservable(key));

        // The tip is set here rather than beside the text, so it can say the badge opens something
        // — which is a fact about the host and the channel together, and neither caller knows both.
        if (D47.Core.Updates.ReleaseChannelText.Full(_channel) is { } says)
        {
            ToolTip.SetTip(
                PreReleaseBadge,
                clickable ? $"{says}. Click to see what it worked." : says);
        }
    }

    /// <summary>
    /// The badge, pressed. Nothing happens on a surface no host furnished, which is every surface
    /// but the desktop window.
    /// </summary>
    private void OnBadgePressed(object? sender, Avalonia.Input.PointerPressedEventArgs e)
    {
        if (OutputOnly || _openBuildDetails is not { } open)
        {
            return;
        }

        e.Handled = true;
        open();
    }

    /// <summary>
    /// Help over the page rather than beside it (asked for 2026-08-22): pushed as a modal level,
    /// so every route that would navigate away is refused until it is dismissed, and dismissing
    /// it is <see cref="GoBack"/> — the breadcrumb, the controller button and the spoken word,
    /// already agreeing with no special case anywhere.
    /// <para>
    /// Falls out to the site when this tab has no band and the host gave a way, which is how the
    /// desktop keeps the behaviour it has always had on the pages nobody has drawn yet.
    /// </para>
    /// </summary>
    public bool OpenHelp() => OpenHelpFor(null);

    /// <summary>
    /// The same, for a mark that is about something narrower than the tab it sits on — a settings
    /// card's question mark, which is about that capability rather than about Settings (asked for
    /// 2026-08-23).
    /// <para>
    /// <b>It draws rather than launching, and that is the whole request.</b> The card's mark used
    /// to call <c>Process.Start</c> on the site, so the Commander lost the panel to a browser and
    /// had no way back to the row they were reading — and in a headset there was no browser to
    /// lose it to. Drawn as a level instead, going back is the breadcrumb, the controller button
    /// and the spoken word, already agreeing with no special case.
    /// </para>
    /// </summary>
    public bool OpenHelpFor(string? capabilityId)
    {
        if (HelpLevel.Showing(Nav))
        {
            // Already showing. Pressing the mark again is not a request for help about help.
            // A chooser used to stop this too, which is what made the mark inert on the module
            // picker — see HelpLevel.Open.
            return false;
        }

        // Whatever was asked for, then whatever the level being looked at claims, then the index.
        // One call, so the mark, a card's mark and the spoken phrase cannot disagree about what
        // help means here.
        if (HelpLevel.Open(Nav, capabilityId))
        {
            return true;
        }

        if (_openHelp is null)
        {
            return false;
        }

        // Not even an index drawn yet, and a desktop to fall out to. The site is still the long
        // form, and this is the behaviour the window has always had.
        _openHelp(capabilityId is { Length: > 0 } id ? DocsSite.Page(id) : DocsSite.Root);
        return true;
    }

    /// <summary>What is drawn, and for which crumb. See <see cref="Modal"/>.</summary>
    private (string Key, Control Page)? _helpPane;

    /// <summary>
    /// What a level that has taken the panel draws.
    /// <para>
    /// A chooser is answered by <see cref="Prompts"/>, which knows the ones it registered. Help is
    /// not a chooser and does not pretend to be one — it is drawn from the shipped documentation,
    /// so it is answered here. Both take the content region the same way, because taking it is a
    /// property of the crumb rather than of what is on it.
    /// </para>
    /// <para>
    /// The page is kept, so a redraw while help is open does not throw away where the Commander
    /// had scrolled to. Same reason <c>DrillView</c> keeps what each of its levels drew.
    /// </para>
    /// </summary>
    private Control? Modal(NavCrumb crumb)
    {
        if (!crumb.Key.StartsWith(HelpPageView.CrumbPrefix, StringComparison.Ordinal))
        {
            return Prompts.Build(crumb);
        }

        if (_helpPane?.Key != crumb.Key)
        {
            _helpPane = (crumb.Key, HelpPageView.Build(crumb, Nav, _openHelp, SettingsJump()));
        }

        return _helpPane.Value.Page;
    }

    /// <summary>
    /// Empties the reading being looked at, leaving the record alone (remediation.md 11, item 14).
    /// <para>
    /// Refused wherever the reading is a file on disk that d47 only reads: there is nothing of
    /// d47's to clear there, and a control that appeared to empty one would be offering to delete
    /// it.
    /// </para>
    /// <para>
    /// <b>That was written as one page's name and should always have been the question</b>
    /// (<a href="https://github.com/dseelinger/d47/issues/261">#261</a>). It named the log, which
    /// was right about all three readings that existed then — and the two journal readings arrived
    /// afterwards and were never added, so the press fell through to the conversation and emptied
    /// a page the Commander was not looking at. Nothing on screen changed, because the journal is
    /// read from Elite's file: they found out on going back to In Ship.
    /// </para>
    /// </summary>
    public bool ClearTranscript()
    {
        if (!Clearable || Model is not { } model)
        {
            return false;
        }

        model.ClearTranscript();

        // The query counted matches in text that has gone, so it goes with it.
        DropSearch();
        DrawTranscript();

        return true;
    }

    /// <summary>
    /// Whether this reading is d47's own to empty (#261).
    /// <para>
    /// One question, asked in the one place, so the menu item's greying and the press itself
    /// cannot disagree — which is the shape of the fault this replaces. In Ship is held in memory
    /// and is d47's; the other three are files on disk it only reads.
    /// </para>
    /// </summary>
    private bool Clearable => Page == TranscriptPage.Conversation;

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

        // Whichever block the page is drawn in, because on the conversation the flat one is
        // hidden — and a hidden control declines focus, which left it in the search box the
        // Escape was pressed to leave.
        (Tab == PanelTab.Transcript
            ? TranscriptBlocks.FirstOrDefault() ?? Transcript
            : PagePane.Child)?.Focus();

        return true;
    }

    private void OnSurfaceKeyDown(object? sender, KeyEventArgs e)
    {
        // Above the search guard, because clearing the page is not a search affordance: a surface
        // with no search box still has a transcript, and the headset is exactly that surface
        // (remediation.md 11, item 14).
        if (e.Key == Key.L && e.KeyModifiers.HasFlag(KeyModifiers.Control))
        {
            e.Handled = ClearTranscript();
            return;
        }

        if (!_searchable)
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

        // The inlines were rebuilt a line ago, so both things the scroll depends on are stale:
        // the text layout the hit's position is measured against, and the extent the offset is
        // clamped to. Without a pass here the offset is clamped against a scroller that has not
        // measured the new content, which clamps it to zero — a step that moves the count and
        // leaves the page exactly where it was (remediation.md 10, item 6).
        TranscriptScroller.UpdateLayout();

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
        if (_hit < 0)
        {
            return;
        }

        // Which block holds the hit, and where that block sits in the page. On a flat page both
        // answers are the transcript block and zero; on the conversation page the hit is an
        // offset into the whole page and a bubble's layout can only answer about its own
        // characters, so the offset is taken back off before asking and the block's own position
        // added back afterwards.
        var (block, start) = Bubbles.IsVisible
            ? _bubbles.LastOrDefault(bubble => bubble.Start <= _matches[_hit].Start)
            : (Transcript, 0);

        if (block?.TextLayout is not { } layout)
        {
            return;
        }

        var where = layout.HitTestTextPosition(_matches[_hit].Start - start);

        if (Bubbles.IsVisible && block.TranslatePoint(new Point(0, where.Y), Bubbles) is { } placed)
        {
            where = where.WithY(placed.Y);
        }

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
        //
        // In mini it stays only where a host asked for it (Phase 51). That is the one
        // line of shared code this phase changes, and it is furnished rather than branched so
        // the headset's mini is untouched by it — see EnableAskInMini.
        AskRow.IsVisible = (full || _asksInMini) && transcript;

        // The provenance line and the microphone indicator together, because both are about the
        // transcript and no other tab has turns on it.
        StatusRow.IsVisible = transcript;

        // And the way out, on every tab this surface has, because a way out with a hole in it is
        // the failure this control exists to prevent (asked for 2026-08-24). Its words say where
        // pressing it goes rather than where it is, which is the only reading of a one-word button
        // that cannot be got backwards.
        //
        // Furnishing is the whole condition — not the tab, not the mode, and deliberately not the
        // modal below either. A chooser is exactly the state a Commander can feel stuck in, and
        // this is the one control that should never be the thing they are stuck behind.
        // A mark rather than the word (asked for 2026-08-24). The pair is the one every video
        // player and browser uses for full screen and leaving it, so it needs no learning — and
        // the word it replaced is still on the tooltip and on the name a screen reader says, which
        // is what keeps a picture from being a downgrade.
        // Furnished decides whether there is a way out at all; the tab decides which of the two
        // draws it (#194). Exactly one is ever visible, and between them they cover every tab —
        // the seat rides StatusRow, which is the transcript's and hidden elsewhere, so the row
        // takes every tab the seat cannot.
        var furnished = _switchMode is not null;

        ModeRow.IsVisible = furnished && !transcript;
        ModeToggleSeat.IsVisible = furnished && transcript;

        // Both marked from one call, which is what keeps two buttons from becoming two
        // behaviours: same glyph, same tooltip, same name a screen reader says, and one Click
        // handler between them in the markup.
        foreach (var toggle in new[] { ModeToggle, ModeToggleSeat })
        {
            Controls.Glyphs.Mark(
                toggle,
                full ? Controls.Glyphs.Shrink : Controls.Glyphs.Expand,
                Theming.ThemeManager.AccentKey,
                full ? "Shrink to the mini panel" : "Expand to the whole panel",

                // Half of the 17 this was, and smaller than the 14 every other mark takes
                // (#193). It used to be the largest glyph in the app on the reasoning that the
                // way out is the one a Commander must find rather than merely recognise; the
                // Commander's answer on meeting it was that it is too big, which settles it.
                // The tooltip and the automation name are set by this same call and are
                // unaffected by size, so nothing a screen reader says gets smaller with it.
                size: 8.5);
        }

        // Mini is "the transcript's tail and the provenance line" and nothing else, so the tabs,
        // the mode control, the breadcrumb and the search box go with the rest of the chrome. A
        // surface with 512x280 to spend does not spend it on six page selectors.
        TabStrip.IsVisible = full;
        CrumbRow.IsVisible = full && CrumbRow.Children.Count > 0;

        var modal = ModalPane.Child is not null;

        // Mini reading the Adventures tab (asked for 2026-08-22). Only this one tab, and only in
        // mini: every other tab behaves exactly as it did, which is what "keep it to transcript and
        // Adventures for now" asks for. A surface that was never furnished with adventures has no
        // child here and falls through to the transcript's tail, as it always did.
        var miniStory = !full && Tab == PanelTab.Adventures && MiniPane.Child is not null;

        // The chooser takes the region rather than sitting over it. Both panes give way, because
        // a page visible behind a modal is a page a ray can still reach and a modal is a modal.
        ModalPane.IsVisible = modal;
        MiniPane.IsVisible = miniStory && !modal;
        TranscriptPane.IsVisible = transcript && !modal && !miniStory;
        PagePane.IsVisible = !transcript && !modal && !miniStory;

        // One border for the whole content region, so the fill is a property rather than a second
        // control (remediation.md 10, item 1). The rule it carries is the one the two borders
        // carried between them: the transcript is Surface, and every furnished page is Background
        // because the cards inside a settings page are Surface and a card the colour of its page
        // is not a card.
        ContentPane.Bind(
            Border.BackgroundProperty,
            this.GetResourceObservable(transcript
                ? Theming.ThemeManager.SurfaceKey
                : Theming.ThemeManager.BackgroundKey));

        // The page's own bar. Mini takes it with the rest of the chrome, and a modal takes it
        // because a chooser is a level of the stack rather than a thing on the page.
        //
        // Copy is Transcript's alone (remediation.md 10, item 2). It had no visibility rule at
        // all, so it sat there on Checklist, Loadout, Engineers and Settings offering to copy the
        // transcript the Commander was not looking at.
        //
        // And it stays the desktop's alone, which it was by accident before: it lived inside the
        // search row, and only the window ever turns that on. A headset has no clipboard to copy
        // into — TopLevel.Clipboard is null on a window that is never shown — so the button there
        // would be one that silently does nothing.
        ShowSearch();

        // And the ask line goes with them: a chooser has one question in it and a second text box
        // underneath, pointed at the model, is a second question nobody asked.
        AskRow.IsVisible = AskRow.IsVisible && !modal;

        // The transcript's own, which is drawn from a scroll position rather than from the mode —
        // so it has to be re-asked whenever the surface changes kind, not only when it scrolls.
        ShowFollowButton();
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
    /// <summary>
    /// Whether mini can show this tab.
    /// <para>
    /// <b>Everything the surface was furnished with, except Settings</b> (asked for 2026-08-24:
    /// <em>"it should have the same tabs as the VR mini panel, including Checklist"</em>).
    /// </para>
    /// <para>
    /// <b>This narrows a Phase 51 ruling rather than reversing it, and the reason it can narrow is
    /// that half of what that ruling was defending has since been built.</b> Phase 51 said mini
    /// shows the transcript and the story and nothing else, on two grounds: the settings page does
    /// not fit — its nav collapses below <c>NavCollapsesBelow</c> of 900 and its body wants 700,
    /// against a surface 512 wide — and there was <em>no tab strip to leave by</em>. The second
    /// ground is gone: mini now draws a way out on every page it has (see
    /// <see cref="EnableModeToggle"/>). The first is not gone, and it was never general — it is one
    /// page's measured minimum width, and the checklist, the engineer pages and the clocks have no
    /// such number.
    /// </para>
    /// <para>
    /// So the exclusion is Settings alone and it is named rather than listed the other way round.
    /// A list of what mini <em>may</em> show is a list somebody has to remember to add to; a list
    /// of what it may not is one line with a measurement behind it.
    /// </para>
    /// <para>
    /// A tab nobody furnished is refused before this is ever asked — the navigator declines it —
    /// so this says what mini does with a tab the surface has, and nothing about which tabs it has.
    /// </para>
    /// </summary>
    private static bool MiniShows(PanelTab tab) => tab != PanelTab.Settings;

    /// <summary>
    /// Keeps mini on a page mini actually has, and puts back what it took (Phase 51).
    /// <para>
    /// <b>This is a hole that was already open.</b> <see cref="ApplyChrome"/> hides the tab strip
    /// in mini but leaves <c>PagePane</c> visible whenever the tab is not the transcript, so a
    /// surface put into mini while it is on Settings draws the settings page — whose nav collapses
    /// below 900 and whose body wants 700 — into a 512-wide surface <b>with no tab strip to leave
    /// by</b>. The headset can be driven into it today by setting <c>vr.mode</c> to mini on the
    /// wrong tab; the desktop would have found it on day one, because the desktop is where
    /// Settings lives.
    /// </para>
    /// <para>
    /// So it belongs here rather than in the window: both surfaces get it at once, and fixing it
    /// only for the desktop would leave the headset bug exactly where it was.
    /// </para>
    /// <para>
    /// While in mini, a move to a tab mini does not have is declined the same way a tab nobody
    /// furnished already is — <b>the constraint is mini's, not the navigator's</b>, so it is
    /// applied here rather than by unregistering anything.
    /// </para>
    /// </summary>
    /// <param name="remember">
    /// Whether the tab being left is the one to give back. True only where mini is what took it —
    /// <b>a refusal is not a destination.</b> A phrase that names Settings while the surface is
    /// mini is declined, and declining it must not queue up a jump to Settings for whenever the
    /// Commander next goes full, which would be a move they asked for minutes ago and had every
    /// reason to think had not happened.
    /// </param>
    private void SettleMini(bool remember = false)
    {
        if (Mode == PanelMode.Mini)
        {
            if (!MiniShows(Tab))
            {
                if (remember)
                {
                    _beforeMini = Tab;
                }

                Tab = PanelTab.Transcript;
            }

            return;
        }

        if (_beforeMini is not { } restore)
        {
            return;
        }

        _beforeMini = null;

        // Through the navigator, which declines a tab this surface no longer has - a host can
        // furnish a tab and nothing ever unfurnishes one, so this is belt and braces rather than
        // a case anybody has seen.
        Tab = restore;
    }

    private void ApplyNavigation()
    {
        // The Raw switch, put back where the Commander left it (#267). First, and returning when
        // it moved, because the move raises Changed and this method runs again on what it landed
        // on. Ahead of the tab checks because the reading it is about is the Transcript's whether
        // or not the Transcript is the tab showing.
        if (ApplyRememberedJournalReading())
        {
            return;
        }

        // Which reading each tab is on, kept for the next launch (#268). After the redirect
        // above, so a Transcript that is about to be sent to raw is written down as the journal
        // once rather than twice.
        RecordRoots();

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

        // And mini refuses a tab it has no reading of, whatever moved the navigator — a press, a
        // spoken phrase, or a switch (Phase 51). Returning rather than falling through,
        // because the assignment raises Changed and this method runs again on the tab it lands on.
        if (Mode == PanelMode.Mini && !MiniShows(tab))
        {
            SettleMini();
            return;
        }

        // The mark is about the page underneath it, so it comes and goes with the tab.
        ShowHelpAffordance();

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
        // Help takes it on exactly the same terms and by the same mechanism, which is the whole
        // reason it is a modal level rather than a tab of its own (asked for 2026-08-22).
        ModalPane.Child = Nav.Modal ? Modal(Nav.Trail[^1]) : null;

        ApplyChrome();

        if (tab != PanelTab.Transcript)
        {
            BuildPageOnce(tab);

            // After the page is in the pane, because whether a query would do anything is a
            // question for the page and ApplyChrome above ran before there was one to ask.
            ShowSearch();
            return;
        }

        // Read when the page is opened, and then kept up while it is open. A log nobody is looking
        // at is still not worth a file read per tick — the ticker below runs only while this page
        // is the one showing, which is the half that was missing: the page was a snapshot of the
        // moment it was opened, so watching it during a failure showed nothing arriving and the
        // only way to see the next line was to leave the page and come back (reported 2026-08-28).
        if (Page == TranscriptPage.Log)
        {
            Reading = ReadLogAsync();
            FollowLogFile(true);
            return;
        }

        FollowLogFile(false);

        // Rebuilt when the page is opened, for the reason the log is read then: the events are
        // already in memory, but projecting four thousand of them into lines is not worth doing
        // per tick for a page nobody is looking at (#51).
        if (Page is TranscriptPage.Journal or TranscriptPage.RawJournal)
        {
            Model?.RefreshJournal();
        }

        DrawTranscript();
    }

    /// <summary>
    /// The segmented control, rebuilt from the current tab's roots. Absent for a tab with one
    /// root, and absent below the root of any tab: a mode switch three levels into a ship is a
    /// question about which stack you are in, and the breadcrumb is already answering the one
    /// about where you are.
    /// </summary>
    /// <summary>
    /// Which reading of this page is showing, on the button that changes it
    /// (remediation.md 10, item 1).
    /// <para>
    /// A drop-down rather than a row of segments. The segments rode in the tab strip's own row
    /// and cost a width proportional to how many readings a tab had, which is what put three
    /// unrelated controls in competition for one row and made the strip overlap itself below a
    /// certain window size. A button costs one control's width whatever the tab offers.
    /// </para>
    /// <para>
    /// Shown at the root and nowhere else, and only where there is more than one reading to be
    /// on. Drilling hides it, because a mode switch three levels into a ship is a question about
    /// which stack you are in and the breadcrumb is already answering that one.
    /// </para>
    /// </summary>
    private void DrawModes()
    {
        // Raw Journal is a root the navigator knows and the picker does not list (#231). It is
        // still registered, so a spoken "raw journal" and a switch position that names it both
        // still arrive — but as a reading it is the same reading as the journal, seen another
        // way, so it is reached by the toggle beside the box rather than by a second entry that
        // reads as a different subject.
        var roots = Nav.Roots(Nav.Tab).Where(root => root.Key != RawJournalRoot).ToList();

        // And a Commander who is *on* raw still has the journal selected, for the same reason.
        var showing = Nav.RootKeyOf(Nav.Tab);
        showing = showing == RawJournalRoot ? JournalRoot : showing;

        ModePicker.IsVisible = !OutputOnly && roots.Count > 1 && Nav.AtRoot;

        ShowPageBar();

        if (!ModePicker.IsVisible)
        {
            return;
        }

        // Rebuilt only when the readings themselves changed, not on every navigation. Replacing
        // the items is what moves the selection, and moving the selection is what would fire
        // OnModeChanged — so a navigation that only changes which root is current must not touch
        // the list. Compared by word because that is what the box shows.
        var words = roots.Select(root => root.Word).ToList();

        if (ModeBox.ItemsSource is not IReadOnlyList<string> shown || !shown.SequenceEqual(words))
        {
            _settingMode = true;

            try
            {
                ModeBox.ItemsSource = words;
            }
            finally
            {
                _settingMode = false;
            }
        }

        var index = roots.FindIndex(root => root.Key == showing);

        // Written under the guard, because this runs on every navigation — including the one
        // OnModeChanged just caused. Without it the box would answer its own change and call
        // SelectRoot again, which is the loop a programmatic write always risks (#231).
        _settingMode = true;

        try
        {
            ModeBox.SelectedIndex = index < 0 ? 0 : index;
        }
        finally
        {
            _settingMode = false;
        }

        DrawRawToggle();
    }

    /// <summary>
    /// The journal's Raw toggle: shown on the journal reading, on a surface that was handed the
    /// raw one (#231).
    /// <para>
    /// Guarded like the box above and for the same reason — writing <c>IsChecked</c> raises the
    /// same event a press does, and a navigation would otherwise answer itself.
    /// </para>
    /// </summary>
    private void DrawRawToggle()
    {
        var journal = Page is TranscriptPage.Journal or TranscriptPage.RawJournal;

        // Only where a host furnished the raw reading. Asked of the navigator rather than held as
        // a flag, so it is the same question EnableRawJournal answers by registering.
        var furnished = Nav.Roots(PanelTab.Transcript).Any(root => root.Key == RawJournalRoot);

        // The box, not the switch: the label lives beside the knob, and hiding one without the
        // other would leave a word floating in the bar.
        RawToggleBox.IsVisible = journal && furnished && Nav.AtRoot && !OutputOnly;

        if (!RawToggleBox.IsVisible)
        {
            return;
        }

        _settingMode = true;

        try
        {
            RawToggle.IsChecked = Page == TranscriptPage.RawJournal;
        }
        finally
        {
            _settingMode = false;
        }
    }

    /// <summary>
    /// The Commander asked for the file's own JSON, or asked to go back to sentences.
    /// </summary>
    private void OnRawToggled(object? sender, RoutedEventArgs e)
    {
        if (_settingMode)
        {
            return;
        }

        Page = RawToggle.IsChecked == true
            ? TranscriptPage.RawJournal
            : TranscriptPage.Journal;
    }

    /// <summary>
    /// Whether the selection is being written by <see cref="DrawModes"/> rather than chosen by
    /// the Commander. See the guard there: a programmatic <c>SelectedIndex</c> raises the same
    /// event a press does, and the navigator would be told about a move it had just made.
    /// </summary>
    private bool _settingMode;

    /// <summary>
    /// The Commander picked a reading from the drop-down.
    /// <para>
    /// <b>A real <c>ComboBox</c>, which the panel spent a long time believing it could not have</b>
    /// (<a href="https://github.com/dseelinger/d47/issues/231">#231</a>). The belief was true of
    /// the headset's copy and got applied to both surfaces: a popup needs a top level, and the
    /// offscreen host window is never shown, so opening one there exits the process at
    /// <c>0xC00000FD</c> before any dispatcher work. This copy is in the real window and has a
    /// real top level. The headset's is covered by <c>OffscreenSurface</c>, which takes the press
    /// before a pointer event exists and draws the list on the panel itself.
    /// </para>
    /// </summary>
    private void OnModeChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_settingMode)
        {
            return;
        }

        var roots = Nav.Roots(Nav.Tab);
        var index = ModeBox.SelectedIndex;

        if (index < 0 || index >= roots.Count)
        {
            return;
        }

        // Through the navigator's own event, so a reading reached by a press and one reached
        // by a spoken phrase are one path rather than two that have to agree. Dropping the
        // search query and the follow lock is ApplyNavigation's job either way.
        Nav.SelectRoot(roots[index].Key);
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
    /// <summary>
    /// Re-reads today's log while the log page is the one showing, and stops the moment it is not.
    /// <para>
    /// <b>A second a tick, and only while somebody is looking.</b> The original reasoning holds
    /// and is why this is a ticker rather than a subscription: a log nobody has open is not worth
    /// a file read, and <see cref="Logging.LogTail"/> reads at most the last 256 KB. What it buys
    /// is the page being live during the failure it was opened to watch.
    /// </para>
    /// <para>
    /// <b>Silent, unlike the open.</b> Opening the page shows the busy glyph because a Commander
    /// pressed something; a refresh nobody asked for must not flash one every second, and must not
    /// redraw at all when the file has not moved — a redraw rebuilds every run and would fight a
    /// reader's selection for nothing.
    /// </para>
    /// </summary>
    private void FollowLogFile(bool following)
    {
        if (!following)
        {
            _logTicker?.Stop();
            return;
        }

        _logTicker ??= new DispatcherTimer(
            TimeSpan.FromSeconds(1),
            DispatcherPriority.Background,
            (_, _) => _ = KeepLogUp());

        _logTicker.Start();
    }

    /// <summary>
    /// One tick of the log page's refresh. Does nothing where nothing has changed.
    /// <para>
    /// Internal and awaitable so a test can drive a tick rather than wait a real second for one:
    /// a test that sleeps for a timer is slow and flaky about the same thing.
    /// </para>
    /// </summary>
    internal async Task RefreshLogNow() => await KeepLogUp();

    private async Task KeepLogUp()
    {
        // A read still in flight, a page that has moved on, or nothing to read into: all three are
        // "not now" rather than errors, and the next tick asks again.
        if (Page != TranscriptPage.Log || Tab != PanelTab.Transcript || _bound is not { } bound)
        {
            return;
        }

        if (!Reading.IsCompleted)
        {
            return;
        }

        try
        {
            var text = await Task.Run(bound.ReadLog);

            if (string.Equals(text, bound.LogText, StringComparison.Ordinal))
            {
                return;
            }

            // Checked again after the await: a Commander who left the page while the read was in
            // flight must not have it redrawn under whatever they went to.
            if (Page != TranscriptPage.Log || Tab != PanelTab.Transcript)
            {
                return;
            }

            bound.ShowLog(text);
            DrawTranscript();

            // Only if they are following, which ScrollToEnd already decides. A Commander who has
            // scrolled up to read something is the reason that check exists (Phase 19).
            ScrollToEnd();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // The file is being rolled, or something else has it. The next tick asks again, and a
            // log line about not being able to read the log is a loop.
        }
    }

    /// <summary>
    /// The log page's refresh while it is open. Null until the page has been opened once, so a
    /// session that never looks at it never builds one.
    /// </summary>
    private DispatcherTimer? _logTicker;

    private async Task ReadLogAsync()
    {
        // The mode button, which may not exist: it is hidden in mini and below a root, and the
        // log can be the reading a surface is on in either. With nothing drawn the read simply
        // runs unannounced — there is nothing to announce on.
        if (!ModePicker.IsVisible)
        {
            // Read off this thread, tell the page on it. RefreshLog inside the Task.Run set a
            // bound property from the worker, and a read that outlives its test then raised
            // PropertyChanged into Avalonia's binding table at exactly the moment the headless
            // harness had the global dispatcher down for reset — the flake ten tests carried
            // (bugs.md, the headless-session entry).
            if (_bound is { } bound)
            {
                bound.ShowLog(await Task.Run(bound.ReadLog));
            }

            DrawTranscript();
            ScrollToEnd();
            return;
        }

        // Both halves inside the busy window (remediation.md 10, item 5). The read was covered
        // and the draw was not, and the draw is on this thread: five hundred lines becoming runs
        // and then a layout pass is the part a Commander was watching nothing happen during.
        // The continuation resumes here, so the glyph is still up while the page is built.
        await Controls.Busy.While(ModeBox, _logBusy, async () =>
        {
            // The same split as above: the file work on a worker, the property set here.
            if (_bound is { } bound)
            {
                bound.ShowLog(await Task.Run(bound.ReadLog));
            }

            // After the read rather than before, or the page draws the log it had last time and
            // then redraws — a visible flicker on the one page opened to read something.
            DrawTranscript();

            // At the end, because a log is read newest-first and this page has always opened at
            // the top of it. The transcript pages have followed the tail since Phase 4 and this
            // one never did, which was a difference nobody chose (Phase 19).
            ScrollToEnd();
        });
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

            // A strip built after the host furnished this surface still gets handles - the tabs
            // are built on first sight rather than up front, so most of them arrive here (Phase 55).
            if (_paneWidths is not null)
            {
                page.EnableDrag(_paneWidths);
            }

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
    private void DrawTranscript() => DrawTranscript(appended: false);

    /// <summary>A line arriving, which is the one redraw that can take the short path.</summary>
    private void OnTranscriptAppended() => DrawTranscript(appended: true);

    /// <summary>
    /// Copy follows the selection (remediation.md 14, item 9). Both ends, because a selection
    /// dragged backwards moves the start and a selection cleared by a click collapses them.
    /// <para>
    /// Per block rather than once, because the conversation page is many blocks. The last one to
    /// report a selection is the one Copy acts on, which is the one the Commander is dragging
    /// across — a new drag in another bubble clears the first, so there is never a moment when
    /// two of them hold one.
    /// </para>
    /// </summary>
    private void Watch(SelectableTextBlock block) =>
        block.PropertyChanged += (sender, changed) =>
        {
            if (changed.Property != SelectableTextBlock.SelectionStartProperty
                && changed.Property != SelectableTextBlock.SelectionEndProperty)
            {
                return;
            }

            if (sender is SelectableTextBlock { SelectedText.Length: > 0 } selected)
            {
                _selection = selected;
            }

            ShowCopySelection();
        };

    /// <param name="appended">
    /// Whether this redraw is a line arriving rather than the page, the query or the theme
    /// changing. It is the licence for the short path in <see cref="DrawBubbles"/> and nothing
    /// else: a reply streams a delta at a time, and rebuilding every turn in the conversation
    /// per token is work that grows with how long the Commander has been flying.
    /// </param>
    /// <summary>
    /// The journal, as a list of sentences with the selected event's fields beside it (#51).
    /// <para>
    /// <b>This pane is Journal's alone.</b> Raw Journal is a file and is drawn as one, through the
    /// flat block the log file uses. They shared this pane in 0.81.0, differing only in their
    /// column widths — which made the two readings look identical and made the raw one not raw:
    /// both showed sentences, and both showed the same pretty-printed fields.
    /// </para>
    /// </summary>
    private void DrawJournal()
    {
        if (Model is not { } model)
        {
            JournalList.ItemsSource = null;
            JournalDetail.Text = string.Empty;
            return;
        }

        // The fields fold away, which is the Commander's amendment to the design and what keeps
        // this reading usable in one narrow column.
        JournalDetailScroller.IsVisible = model.JournalDetail;
        JournalSplitter.IsVisible = model.JournalDetail;

        // Filtered, which is this reading's answer to the search box (#232). Every other reading
        // on this tab highlights and steps; a list filters, the way the checklist and the
        // engineer directory do. The count then means what it says, which the old behaviour
        // conspicuously did not: this page returned from DrawTranscript before the search ran at
        // all, so the box showed a leftover count from whichever prose page was read last and
        // the steppers moved nothing.
        //
        // Kind as well as the drawn line, because the drawn line is a sentence and the thing a
        // Commander is hunting is frequently the event's own name: "ShieldState" appears nowhere
        // in "Shields back up", and typing it should not come back empty on the page whose whole
        // job is showing that event.
        var shown = _query.Length == 0
            ? model.Journal
            : [.. model.Journal.Where(entry =>
                entry.Line.Contains(_query, StringComparison.OrdinalIgnoreCase)
                || entry.Kind.Contains(_query, StringComparison.OrdinalIgnoreCase))];

        JournalList.ItemsSource = shown.Select(entry => entry.Line).ToList();

        // Against the filtered list rather than the whole one. The model's index counts events,
        // and with a query in the box the list no longer holds all of them — so the remembered
        // selection is resolved by identity and dropped when it filtered away.
        var selected = model.JournalSelected >= 0 && model.JournalSelected < model.Journal.Count
            ? shown.ToList().IndexOf(model.Journal[model.JournalSelected])
            : -1;

        JournalList.SelectedIndex = selected;

        JournalDetail.Text = selected >= 0 ? model.JournalDetailText : string.Empty;

        ShowJournalCount(shown.Count, model.Journal.Count);
    }

    /// <summary>
    /// What the search box says on a reading that filters (#232): how many lines are left, not
    /// which of them is current. The steppers are hidden, because there is nothing to step
    /// through — every line on screen is a hit.
    /// </summary>
    private void ShowJournalCount(int shown, int held)
    {
        var searching = _query.Length > 0;

        SearchCount.IsVisible = searching;
        SearchNext.IsVisible = false;
        SearchPrevious.IsVisible = false;

        if (searching)
        {
            SearchCount.Text = shown == 0
                ? "no lines match"
                : $"{shown} of {held}";
        }
    }

    /// <summary>
    /// A line was chosen, so the fields beside it change. Straight onto the model rather than into
    /// a field here, because the headset's copy of this panel shows the same selection and reads it
    /// from the same place.
    /// </summary>
    private void OnJournalSelected(object? sender, Avalonia.Controls.SelectionChangedEventArgs e)
    {
        if (Model is not { } model || JournalList.SelectedIndex < 0)
        {
            return;
        }

        model.JournalSelected = JournalList.SelectedIndex;
        JournalDetail.Text = model.JournalDetailText;
    }

    /// <summary>
    /// Whether the fields are drawn beside the list. Furnished by the host rather than read from a
    /// setting, so a surface that cannot show two panes simply never turns it on.
    /// </summary>
    public void ShowJournalDetail(bool shown)
    {
        if (Model is { } model)
        {
            model.JournalDetail = shown;
            DrawTranscript();
        }
    }

    /// <summary>
    /// Whether the kinds nobody reads are listed. Rebuilds rather than filters what is drawn: the
    /// list is a projection of the log and the log is where the filter belongs.
    /// </summary>
    public void ShowJournalNoise(bool shown)
    {
        if (Model is { } model)
        {
            model.JournalNoise = shown;
            model.RefreshJournal();
            DrawTranscript();
        }
    }

    private void DrawTranscript(bool appended)
    {
        if (!Dispatcher.UIThread.CheckAccess())
        {
            Dispatcher.UIThread.Post(() => DrawTranscript(appended));
            return;
        }

        // The journal is a shape of its own - a list and the fields beside it - so it takes the
        // pane rather than a presentation inside the shared scroller (#51). Drawn and returned
        // from here, because everything below this line is about runs of transcript text and none
        // of it applies.
        // **Journal alone takes the pane.** Raw Journal is a file, so it is drawn the way the other
        // file on this tab is drawn - one flat, selectable, searchable block through the scroller
        // below. It shipped in 0.81.0 sharing this pane and differing only in its column widths,
        // which made the two readings look identical and made the raw one not raw at all.
        var listed = Page == TranscriptPage.Journal;

        JournalPane.IsVisible = listed;
        TranscriptScroller.IsVisible = !listed;

        if (listed)
        {
            DrawJournal();
            return;
        }

        // Which of the two presentations this page gets. The conversation is drawn as one,
        // turn by turn; the diagnostics and the log file stay the flat block they were.
        var bubbled = Page == TranscriptPage.Conversation;

        Transcript.IsVisible = !bubbled;
        Bubbles.IsVisible = bubbled;

        if (_bound is null)
        {
            Transcript.Inlines?.Clear();
            ClearBubbles();
            _matches = [];
            _hit = -1;
            ShowSearchProgress(_query.Length > 0);
            return;
        }

        // Unframed for the conversation, because the blank line and the "> " a flat page puts
        // in front of the Commander's turn are that page's way of saying who spoke, and this
        // one says it with a side and a colour instead.
        var messages = bubbled
            ? Turns(Drawn(_bound.Segments(Page, framed: false), Page))
            : [new DrawnTurn(TranscriptVoice.Ship, Marker: false, Drawn(_bound.Segments(Page), Page))];

        // Matched against the page's text rather than against the controls, so the hits are the
        // same set whether the page has been drawn yet or not — and so the current one can be
        // re-resolved from its offset every time the log grows underneath it.
        //
        // The drawn text and not the written one, which is what keeps searching honest now that
        // the two differ: a reader looking at "A-rate thrusters" and typing it would otherwise
        // match nothing, because what is in the buffer is "**A-rate thrusters**".
        _matches = D47.Core.Interface.TextSearch.Find(
            string.Concat(messages.SelectMany(turn => turn.Segments).Select(segment => segment.Text)),
            _query);

        _hit = D47.Core.Interface.TextSearch.Track(_matches, _hitOffset);

        if (_hit >= 0)
        {
            _hitOffset = _matches[_hit].Start;
        }

        if (bubbled)
        {
            DrawBubbles(messages, appended);
        }
        else
        {
            ClearBubbles();
            Fill(Transcript, messages[0], at: 0);
        }

        ShowSearchProgress(_query.Length > 0);
    }

    /// <summary>
    /// The conversation, turn by turn: the Commander's on the right, the ship's on the left,
    /// and the panel's own notes across the middle (asked for 2026-08-22).
    /// <para>
    /// <b>The short path.</b> A reply arrives a delta at a time and every one of them redraws
    /// this. Rebuilding one bubble is work proportional to the sentence being spoken; rebuilding
    /// all of them is work proportional to how long the session has run, per token. So a redraw
    /// caused by an append, where nothing but the last turn can have changed, refills the last
    /// bubble and leaves the rest standing. It is declined while a query is live, because a hit
    /// that lands in the growing turn changes the count for the whole page — and that is a
    /// cheap thing to give up, since nobody streams a reply and searches it at the same time.
    /// </para>
    /// </summary>
    private void DrawBubbles(IReadOnlyList<DrawnTurn> turns, bool appended)
    {
        // The turns as three comparable things each, because a record holding a list compares
        // the list by reference and would call every redraw a change.
        var shape = turns
            .Select(turn => (
                turn.Voice,
                turn.Marker,
                Text: string.Concat(turn.Segments.Select(segment => segment.Text))))
            .ToArray();

        if (appended
            && _query.Length == 0
            && shape.Length > 0
            && shape.Length == _bubbles.Count
            && shape.Length == _shape.Count
            && shape.Take(shape.Length - 1).SequenceEqual(_shape.Take(_shape.Count - 1)))
        {
            Fill(_bubbles[^1].Block, turns[^1], _bubbles[^1].Start);
            _shape = shape;
            return;
        }

        ClearBubbles();

        var mini = Mode == PanelMode.Mini;
        var at = 0;

        foreach (var turn in turns)
        {
            var block = new SelectableTextBlock
            {
                FontFamily = Transcript.FontFamily,
                FontSize = Transcript.FontSize,
                TextWrapping = TextWrapping.Wrap,

                // The menu the block beside this one declares, not a second copy of it. Copy
                // acts on whichever bubble the selection is in — see ShowCopySelection — and
                // Clear is about the page rather than about any one turn.
                ContextMenu = Transcript.ContextMenu,
            };

            Watch(block);
            Fill(block, turn, at);

            Bubbles.Children.Add(Bubble(block, turn, mini));
            _bubbles.Add((block, at));

            at += turn.Segments.Sum(segment => segment.Text.Length);
        }

        _shape = shape;
    }

    /// <summary>
    /// One turn, dressed. The panel's own notes get no bubble at all — they are not a side of
    /// the conversation, and an SMS thread says the same thing the same way, across the middle
    /// and out of the run of it.
    /// <para>
    /// The width cap is a star column rather than a <c>MaxWidth</c>, so it is layout rather than
    /// arithmetic over a viewport that is not measured yet. Mini gives the gutter back: a
    /// headset panel with 512 pixels across it cannot spend a fifth of them saying which side a
    /// turn is on when the colour already does.
    /// </para>
    /// </summary>
    private Control Bubble(SelectableTextBlock block, DrawnTurn turn, bool mini)
    {
        if (turn.Marker)
        {
            block.TextAlignment = TextAlignment.Center;
            block.Margin = new Thickness(0, mini ? 3 : 6, 0, mini ? 3 : 6);

            return block;
        }

        var commander = turn.Voice == TranscriptVoice.Commander;

        var bubble = new Border
        {
            Child = block,
            CornerRadius = new CornerRadius(mini ? 6 : 10),
            Padding = mini ? new Thickness(7, 4) : new Thickness(11, 8),
            Margin = new Thickness(0, mini ? 2 : 4),
            BorderThickness = new Thickness(1),
            HorizontalAlignment = commander
                ? Avalonia.Layout.HorizontalAlignment.Right
                : Avalonia.Layout.HorizontalAlignment.Left,
        };

        // The two sides, by colour as well as by side, which is the convention every messaging
        // app on the Commander's phone already taught them. The accent is theirs because it is
        // the theme's own colour and they are the one person in the conversation.
        bubble.Bind(
            Border.BackgroundProperty,
            this.GetResourceObservable(commander
                ? Theming.ThemeManager.AccentMutedKey
                : Theming.ThemeManager.SurfaceAltKey));

        bubble.Bind(
            Border.BorderBrushProperty,
            this.GetResourceObservable(commander
                ? Theming.ThemeManager.AccentKey
                : Theming.ThemeManager.BorderKey));

        var gutter = mini ? "12*,*" : "3*,*";

        var row = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions(commander ? Reversed(gutter) : gutter),
        };

        Grid.SetColumn(bubble, commander ? 1 : 0);
        row.Children.Add(bubble);

        return row;
    }

    private static string Reversed(string columns) =>
        string.Join(',', columns.Split(',').Reverse());

    /// <summary>
    /// One turn's text into one block, as runs. <paramref name="at"/> is where this turn starts
    /// in the page, which is what lets a hit found over the whole page be drawn in the bubble it
    /// landed in.
    /// </summary>
    private void Fill(SelectableTextBlock block, DrawnTurn turn, int at)
    {
        var inlines = block.Inlines ??= [];
        inlines.Clear();

        // A hit that is not the current one is drawn in the accent with the volume down — which
        // is the Commander's own bubble fill, and would be invisible inside it. On that side the
        // quiet highlight is the surface instead. The current hit is the full accent either way
        // and stands out against both.
        var quiet = turn.Voice == TranscriptVoice.Commander && !turn.Marker && Page == TranscriptPage.Conversation
            ? Theming.ThemeManager.SurfaceKey
            : Theming.ThemeManager.AccentMutedKey;

        foreach (var segment in turn.Segments)
        {
            foreach (var (text, match) in Split(segment.Text, at))
            {
                var run = new Run(text);

                if (segment.Style.HasFlag(MarkupStyle.Strong))
                {
                    run.FontWeight = FontWeight.Bold;
                }

                if (segment.Style.HasFlag(MarkupStyle.Emphasis))
                {
                    run.FontStyle = FontStyle.Italic;
                }

                if (segment.Style.HasFlag(MarkupStyle.Code))
                {
                    // The whole transcript is already monospaced, so a code span has to be told
                    // apart some other way: a chip behind it. The hairline colour rather than the
                    // alternate surface, because that is a bubble fill now and a chip the colour
                    // of the bubble it sits in is not a chip. Bound before the search does the
                    // same property, so a hit inside a fenced block is still drawn as a hit.
                    run.Bind(
                        Avalonia.Controls.Documents.TextElement.BackgroundProperty,
                        this.GetResourceObservable(Theming.ThemeManager.BorderKey));
                }

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
                            : quiet));

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
    }

    /// <summary>
    /// Every run this surface is currently drawing the transcript with, in page order — the one
    /// block's worth on a flat page, one bubble's worth at a time on the conversation.
    /// <para>
    /// For the tests, which ask what a surface is showing and should not have to know which of
    /// the two presentations answered. It is the same question either way.
    /// </para>
    /// </summary>
    internal IEnumerable<Run> TranscriptRuns =>
        Bubbles.IsVisible
            ? _bubbles.SelectMany(bubble => bubble.Block.Inlines?.OfType<Run>() ?? [])
            : Transcript.Inlines?.OfType<Run>() ?? [];

    /// <summary>What this surface is showing, as text.</summary>
    /// <summary>
    /// Draws the transcript — or, on the log page, reads the file first (GitHub issue 43).
    /// <para>
    /// The two are not interchangeable and that is the whole of the defect: every other reading
    /// is in the model already and only wants drawing, and the log is on disk.
    /// </para>
    /// </summary>
    private void Reread()
    {
        if (Tab == PanelTab.Transcript && Page == TranscriptPage.Log)
        {
            Reading = ReadLogAsync();
            return;
        }

        DrawTranscript();
    }

    /// <summary>
    /// The log read that is in flight, or a completed task (GitHub issue 43).
    /// <para>
    /// <b>A handle on the one thing here that finishes later than the call that started it.</b>
    /// The log page reads its file on a worker and draws in the continuation, and this was
    /// <c>_ = ReadLogAsync()</c> — a task nothing could wait on, so a caller that wanted to know
    /// whether the page had been drawn had no way to ask and could only guess by pumping the
    /// dispatcher once and hoping. That guess is right almost always and wrong under load, which
    /// is what a flake is: it failed once in CI on 2026-08-25 with the page still empty, and
    /// passed on a re-run of the same commit.
    /// </para>
    /// <para>
    /// Nothing about the app's behaviour changes — the read was always going to land, and the
    /// Commander watches a busy glyph while it does. What changes is that it can now be
    /// <em>awaited</em>, so a test asserts on a page that has been drawn rather than on one that
    /// usually has been.
    /// </para>
    /// </summary>
    internal Task Reading { get; private set; } = Task.CompletedTask;

    internal string TranscriptShown => string.Concat(TranscriptRuns.Select(run => run.Text));

    /// <summary>
    /// The blocks the transcript is drawn in — one on a flat page, one per turn on the
    /// conversation. The selectable surface, which is what a test dragging across it needs.
    /// </summary>
    internal IReadOnlyList<SelectableTextBlock> TranscriptBlocks =>
        Bubbles.IsVisible ? [.. _bubbles.Select(bubble => bubble.Block)] : [Transcript];

    private void ClearBubbles()
    {
        Bubbles.Children.Clear();
        _bubbles.Clear();
        _shape = [];
    }

    /// <summary>
    /// A page's segments with the model's markdown read: the markers gone and what they meant
    /// carried as a style (Phase 19, and the transcript drawing <c>**A-rate FSD**</c>
    /// literally for as long as it has existed).
    /// <para>
    /// The log file is exempt and drawn exactly as it is on disk. It is a file rather than
    /// prose — a line of it that happens to hold an asterisk means an asterisk, and a page
    /// opened to read what was written is the last place to reformat anything.
    /// </para>
    /// <para>
    /// A marked line keeps its <see cref="TranscriptSegment.Marker"/> through the split, so the
    /// panel's own bracketed note is still accented whatever is inside it.
    /// </para>
    /// </summary>
    private static IReadOnlyList<DrawnSegment> Drawn(
        IReadOnlyList<TranscriptSegment> segments,
        TranscriptPage page) =>
        // Raw Journal joins the log here, and it is the more important of the two: a journal
        // carries other players' text verbatim, and JSON is full of asterisks and underscores.
        // Through the markup parser, a Commander who types ** would see their message reformatted
        // and could dress it up as one of d47's own lines (#51).
        page is TranscriptPage.Log or TranscriptPage.RawJournal
            ? [.. segments.Select(segment =>
                new DrawnSegment(segment.Text, segment.Marker, segment.Voice, MarkupStyle.None))]
            : [.. segments.SelectMany(segment => TranscriptMarkup
                .Parse(segment.Text)
                .Select(span => new DrawnSegment(span.Text, segment.Marker, segment.Voice, span.Style)))];

    /// <summary>
    /// The page's segments gathered into turns: consecutive stretches from one side, with the
    /// blank lines between them taken off.
    /// <para>
    /// The trimming happens here, before the search runs over the result, which is the whole
    /// reason it is not done while building the bubbles: a hit is an offset into the page as
    /// drawn, and text removed after the offsets are worked out is text the highlight lands
    /// beside rather than on.
    /// </para>
    /// <para>
    /// A turn that is nothing but whitespace is dropped rather than drawn as an empty bubble.
    /// The transcript is full of them — the separators a flat page needs and this one does not.
    /// </para>
    /// </summary>
    private static IReadOnlyList<DrawnTurn> Turns(IReadOnlyList<DrawnSegment> segments)
    {
        var gathered = new List<(TranscriptVoice Voice, bool Marker, List<DrawnSegment> Segments)>();

        foreach (var segment in segments)
        {
            if (gathered is [.., var last] && last.Voice == segment.Voice && last.Marker == segment.Marker)
            {
                last.Segments.Add(segment);
                continue;
            }

            gathered.Add((segment.Voice, segment.Marker, [segment]));
        }

        return
        [
            .. gathered
                .Select(turn => new DrawnTurn(turn.Voice, turn.Marker, Trimmed(turn.Segments)))
                .Where(turn => turn.Segments.Count > 0)
        ];
    }

    /// <summary>The turn's own words, without the whitespace that separated it from its neighbours.</summary>
    private static IReadOnlyList<DrawnSegment> Trimmed(IReadOnlyList<DrawnSegment> segments)
    {
        var trimmed = new List<DrawnSegment>(segments);

        while (trimmed.Count > 0)
        {
            var start = trimmed[0].Text.TrimStart();

            if (start.Length == 0)
            {
                trimmed.RemoveAt(0);
                continue;
            }

            trimmed[0] = trimmed[0] with { Text = start };
            break;
        }

        while (trimmed.Count > 0)
        {
            var end = trimmed[^1].Text.TrimEnd();

            if (end.Length == 0)
            {
                trimmed.RemoveAt(trimmed.Count - 1);
                continue;
            }

            trimmed[^1] = trimmed[^1] with { Text = end };
            break;
        }

        return trimmed;
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
                // A prompt is abandoned rather than obeyed (remediation.md 13, item 5). "No
                // navigating away mid-choice" is right for every gesture inside the panel and
                // wrong for the tabs: pressing Engineers while a question is up is somebody
                // saying they are done with it, so Back is taken for them and nothing is
                // committed. The button is still put back if the move is refused for any other
                // reason, rather than left showing a tab the panel is not on.
                Prompts.Abandon();

                if (!Nav.Select(tab))
                {
                    ApplyNavigation();
                }

                return;
            }
        }
    }

    /// <summary>
    /// Pressing the tab that is already selected returns to its root (Phase 25, "the tab
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
        // line that arrived, and a busy session appends several a second (Phase 19).
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
            var scroller = Scroller;

            // Laid out first. A scroll viewer scrolls to the end of the extent it currently
            // knows about, and the runs were rewritten a moment ago — so without this it goes to
            // where the end was before the line that caused it, which is the "lands one append
            // behind" the subscription order above is already fighting. Forced rather than
            // awaited because the following has to be true when this returns.
            scroller.UpdateLayout();

            // The newest line, not the bottom (#233). Vertical only: ScrollToHome would take the
            // horizontal offset with it, and the raw journal is a page a Commander scrolls
            // sideways through.
            if (NewestAtTop)
            {
                scroller.Offset = scroller.Offset.WithY(0);
            }
            else
            {
                scroller.ScrollToEnd();
            }
        }
        finally
        {
            _scrollingItself = false;
        }

        ShowFollowButton();
    }

    /// <summary>
    /// Which way this reading runs, and therefore where its newest line is (#233).
    /// <para>
    /// <b>One bit, asked once.</b> The transcript and the log file grow downwards, so their
    /// newest line is at the bottom and "the end" and "the newest" are the same place. The two
    /// journal readings are written newest-first — <c>JournalLog</c> says so of the raw one
    /// outright — so on those the newest line is at the <em>top</em>, and every part of the
    /// follow mechanism that assumed otherwise sent the Commander to the far end of the file.
    /// </para>
    /// </summary>
    private bool NewestAtTop => Page is TranscriptPage.Journal or TranscriptPage.RawJournal;

    /// <summary>
    /// The scroller the Newest button acts on, which is not always the transcript's.
    /// <para>
    /// Journal takes the pane with a list of its own and hides <see cref="TranscriptScroller"/>
    /// entirely, so the button was reading the extent of a scroller nobody could see and moving
    /// it. That is the second half of #233 and a different fault from the direction: on Raw
    /// Journal the button went the wrong way, and on Journal it went nowhere at all.
    /// </para>
    /// </summary>
    private ScrollViewer Scroller =>
        Page == TranscriptPage.Journal ? JournalListScroller : TranscriptScroller;

    /// <summary>
    /// Whether the view is at the newest line of this reading, within a line's worth.
    /// <para>
    /// A tolerance rather than an equality, because a scroll viewer's extent and its offset are
    /// laid-out doubles: a wrapped line, a font fallback or a fractional scale leaves the last
    /// pixel unreachable, and "following" would then switch itself off on a surface that is
    /// visibly at the end.
    /// </para>
    /// </summary>
    private bool AtTheNewest()
    {
        var scroller = Scroller;
        var tolerance = Transcript.FontSize;

        if (NewestAtTop)
        {
            return scroller.Offset.Y <= tolerance;
        }

        var slack = Math.Max(1, scroller.Extent.Height - scroller.Viewport.Height);

        return scroller.Offset.Y >= slack - tolerance;
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
            _following = AtTheNewest();
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
        var behind = !_following && !AtTheNewest();

        // Not on a surface nothing can be pressed on (#202). This is the assignment that beat the
        // style rule — a local value outranks a setter — and it is the transcript's own chrome
        // rather than a furnished page's, so it asks here rather than being marked.
        FollowButton.IsVisible = behind && !OutputOnly;

        if (behind)
        {
            // The arrow points where the newest line actually is (#233), which is upwards on the
            // two journal readings. A label that names a direction has to be right about it:
            // "↓ Newest" over a newest-first page is not merely unhelpful, it is untrue.
            FollowButton.Content = NewestAtTop ? "↑ Newest" : "↓ Newest";
        }
    }

    /// <summary>
    /// Copies the whole of the page being read (Phase 19, "Copy log").
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

        // Drawn rather than written, for the reason above: the same text the Commander is
        // looking at. It is also what dragging a selection and pressing Ctrl+C gives, and two
        // copy gestures on one pane handing back two different strings is the surprise.
        var text = string.Concat(Drawn(_bound.Segments(Page), Page).Select(segment => segment.Text));

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
        CopyButton.Content = CopyLabel;
    }

    /// <summary>
    /// Scrolls the tab strip (remediation.md 10, item 1).
    /// <para>
    /// By roughly one tab's width rather than by a page, because the strip is short and a page
    /// scroll on six tabs is the whole strip — which loses the Commander's place in a control
    /// whose only job is to say where they are.
    /// </para>
    /// </summary>
    private void StepTabs(double by) =>
        TabsScroller.Offset = new Vector(
            Math.Clamp(
                TabsScroller.Offset.X + by,
                0,
                Math.Max(0, TabsScroller.Extent.Width - TabsScroller.Viewport.Width)),
            TabsScroller.Offset.Y);

    private void OnTabsLeftClick(object? sender, RoutedEventArgs e) => StepTabs(-120);

    private void OnTabsRightClick(object? sender, RoutedEventArgs e) => StepTabs(120);

    /// <summary>
    /// Whether the strip needs its steppers, asked whenever it is measured.
    /// <para>
    /// Hidden when everything fits, which is the ordinary case on a desktop: a pair of dead arrows
    /// either side of six tabs is two controls that permanently do nothing. Shown together rather
    /// than one at a time — an arrow that appears and vanishes as the strip is scrolled is a
    /// control that moves the tabs while you are aiming at them.
    /// </para>
    /// </summary>
    private void OnTabsResized(object? sender, SizeChangedEventArgs e) => ShowTabSteppers();

    /// <summary>What each tab says when the strip has room for words. Fixed at build.</summary>
    private static readonly (string Name, string Word, string Glyph)[] TabMarks =
    [
        (nameof(TranscriptTab), "Transcript", Controls.Glyphs.Tabs.Transcript),
        (nameof(RoutingTab), "Routing", Controls.Glyphs.Tabs.Routing),
        (nameof(ChecklistTab), "Checklist", Controls.Glyphs.Tabs.Checklist),
        (nameof(LoadoutTab), "Fleet", Controls.Glyphs.Tabs.Fleet),
        (nameof(EngineersTab), "Engineers", Controls.Glyphs.Tabs.Engineers),
        (nameof(AdventuresTab), "Adventures", Controls.Glyphs.Tabs.Adventures),
        (nameof(UtilitiesTab), "Utilities", Controls.Glyphs.Tabs.Utilities),
        (nameof(SettingsTab), "Settings", Controls.Glyphs.Tabs.Settings),
    ];

    /// <summary>Whether the strip is currently showing marks instead of words.</summary>
    private bool _tabsCollapsed;

    /// <summary>Whether the tabs have been drawn at all yet, rather than left as the markup made them.</summary>
    private bool _tabsDrawn;

    /// <summary>
    /// How wide the strip was the last time it was drawn with words — the number the decision to
    /// expand again is made against.
    /// <para>
    /// <b>Remembered rather than re-measured, because the measurement feeds the decision.</b>
    /// Collapsing shrinks the extent; asking the shrunken extent whether the words fit gets
    /// "yes", which expands, which overflows, which collapses. The width the words wanted is the
    /// only stable thing to compare a viewport against.
    /// </para>
    /// </summary>
    private double _tabWordsWidth;

    /// <summary>
    /// Three stages, in order: words, marks, then marks that scroll (#234).
    /// <para>
    /// Scrolling is the right last resort and was the only one. A Commander on a narrow window
    /// lost whole tabs off the end of the strip while the ones still visible carried full words —
    /// so the words go first, and a tab leaves the strip only when even its mark will not fit.
    /// </para>
    /// </summary>
    private void ShowTabSteppers()
    {
        // Once, before anything is measured. The tabs carry their word from the markup, and
        // DrawTabMarks only ran when the collapsed state *changed* — so a strip that opened wide
        // and stayed wide kept the markup's bare word and never got its mark (#266). It was
        // invisible while word and mark were alternatives, because the markup's content was
        // exactly what the expanded state wanted.
        if (!_tabsDrawn)
        {
            _tabsDrawn = true;
            DrawTabMarks();
            TabsScroller.UpdateLayout();
        }

        var room = TabsScroller.Viewport.Width;

        if (room > 0)
        {
            if (!_tabsCollapsed)
            {
                _tabWordsWidth = TabsScroller.Extent.Width;
            }

            var wanted = _tabsCollapsed ? _tabWordsWidth : TabsScroller.Extent.Width;
            var collapse = wanted > room + 1;

            if (collapse != _tabsCollapsed)
            {
                _tabsCollapsed = collapse;
                DrawTabMarks();
                TabsScroller.UpdateLayout();
            }
        }

        var overflowing = TabsScroller.Extent.Width > TabsScroller.Viewport.Width + 1;

        TabsLeft.IsVisible = overflowing;
        TabsRight.IsVisible = overflowing;
    }

    /// <summary>
    /// Puts the mark and the word, or the mark alone, on every tab.
    /// <para>
    /// Through <see cref="Controls.Glyphs.Mark"/> when the word has gone, so it survives on the
    /// tooltip and on the name a screen reader says — which is the whole condition under which
    /// replacing a word with a picture counts as an improvement, and doubly so here, where the
    /// Commander did not choose to lose the word and the window merely got narrow.
    /// </para>
    /// <para>
    /// <b>The wide strip shows both since #266.</b> Word and mark used to be alternatives, so a
    /// Commander on a full-size window read eight words and never learnt the marks — and then the
    /// window narrowed and every tab was a picture they had not seen before. The marks were learnt
    /// exactly when they stopped being available to learn from. There is no longer a width at
    /// which a tab shows a word and no mark.
    /// </para>
    /// </summary>
    private void DrawTabMarks()
    {
        foreach (var (name, word, glyph) in TabMarks)
        {
            if (this.FindControl<RadioButton>(name) is not { } tab)
            {
                continue;
            }

            if (_tabsCollapsed)
            {
                Controls.Glyphs.Mark(
                    tab,
                    glyph,
                    Theming.ThemeManager.TextKey,
                    word,
                    size: 17,
                    filled: Controls.Glyphs.IsFilled(glyph));

                continue;
            }

            // Smaller than the collapsed mark on purpose: at 17 it is the tab, and beside a word
            // it is a mark next to a word. The word is what carries the tab at this width.
            Controls.Glyphs.MarkAndWord(
                tab, glyph, Theming.ThemeManager.TextKey, word, size: 15);
        }
    }

    /// <summary>
    /// Whether the page's bar exists at all.
    /// <para>
    /// <b>Only when it has something in it</b>, and that is not tidiness. It is a row, and a row
    /// costs height that the pane below it does not get — which is nothing on a desktop window and
    /// is most of the page on a 280-pixel headset panel. The first cut showed it whenever the
    /// chrome was showing, and the transcript on the small surface came out six pixels tall.
    /// </para>
    /// <para>
    /// Called from both the places that decide what is in it, because either can run last:
    /// <see cref="ApplyChrome"/> owns the search box and the copy button, and
    /// <see cref="DrawModes"/> owns the mode button.
    /// </para>
    /// </summary>
    /// <summary>
    /// Whether the surface's search box is drawn, and the copy button beside it
    /// (remediation.md 11, item 6).
    /// <para>
    /// <b>Only where a query would do something.</b> The box used to be drawn on every page of a
    /// surface that had one, so a Commander typed into it on the Ships page and watched nothing
    /// happen. What a match does is the page's business and always has been; whether there is
    /// anything for a match to do is the same question, asked one step earlier.
    /// </para>
    /// <para>
    /// The transcript is the case with no page to ask: it highlights and steps rather than
    /// filtering, which is not <see cref="IFilterablePage"/> and is still a search.
    /// </para>
    /// </summary>
    private void ShowSearch()
    {
        var transcript = Tab == PanelTab.Transcript;

        CopyButton.IsVisible = _searchable && transcript;

        // Only where there is something to cut and somewhere to put it (#160). The two diagnostic
        // readings are the two halves of an incident — Elite's events and what d47 did with them —
        // and In Ship is neither: it is the conversation, which the log already holds a more exact
        // copy of.
        //
        // Null when no host wired it, which is every surface but the desktop window. That is the
        // same rule the search box follows two lines down and for a stronger reason: a review step
        // is the whole of the consent here, and a review step on a surface with no clipboard would
        // be a Commander reading an excerpt they could not then do anything with.
        DonateButton.IsVisible = transcript
                                 && _donate is not null
                                 && Page is TranscriptPage.Log
                                     or TranscriptPage.Journal
                                     or TranscriptPage.RawJournal;

        SearchRow.IsVisible = _searchable
                              && Mode == PanelMode.Full
                              && ModalPane.Child is null
                              && (transcript || (PagePane.Child as IFilterablePage)?.Filters == true);

        // Greyed on the readings that refuse (#261), the way Copy beside it is greyed with nothing
        // to copy — and for the reason that comment gives: a control that silently does nothing is
        // indistinguishable from one that failed, so the refusal is drawn rather than only obeyed.
        ClearTranscriptItem.IsEnabled = transcript && Clearable;

        ShowPageBar();
    }

    private void ShowPageBar() =>
        PageBar.IsVisible = Mode == PanelMode.Full
                            && ModalPane.Child is null
                            && (ModePicker.IsVisible || SearchRow.IsVisible || RawToggleBox.IsVisible);

    /// <summary>
    /// Opens the sharing window (#160, #238). The panel knows nothing about what is in it — see
    /// <see cref="EnableDonation"/> — beyond the one thing only it can say: whether this page
    /// shows Elite's journals, which is what decides if the history half is on offer.
    /// </summary>
    private void OnDonateClick(object? sender, RoutedEventArgs e) =>
        _donate?.Invoke(Page is TranscriptPage.Journal or TranscriptPage.RawJournal);

    private void OnClearTranscriptClick(object? sender, RoutedEventArgs e) => ClearTranscript();

    /// <summary>
    /// Copies what is selected in the transcript (remediation.md 14, item 9).
    /// <para>
    /// <b>The selection, where the button above copies the page.</b> Two different acts that a
    /// Commander asks for in two different ways, and the reason this one went missing is that
    /// declaring a context menu replaces the one <c>SelectableTextBlock</c> comes with — Ctrl+C
    /// never stopped working, but the place a reader looks to find out that it does was taken
    /// away and given to Clear.
    /// </para>
    /// </summary>
    private void OnCopySelectionClick(object? sender, RoutedEventArgs e) => Selected()?.Copy();

    /// <summary>Whichever block holds a selection right now, or none at all.</summary>
    private SelectableTextBlock? Selected() =>
        _selection is { SelectedText.Length: > 0 } held ? held : null;

    /// <summary>
    /// Greys Copy when there is nothing to copy or nowhere to put it.
    /// <para>
    /// Two ways for it to be useless and the same answer to both: nothing is selected, or the
    /// surface has no clipboard — which is the headset, whose host window is never shown and
    /// whose <c>TopLevel.Clipboard</c> is null. A control that exists to be pressed and does
    /// nothing teaches the wrong thing about what the panel can do, which is the same call the
    /// search box makes about the pages it cannot narrow.
    /// </para>
    /// <para>
    /// <b>Off the selection rather than off the menu opening.</b> <c>ContextMenu.Opening</c> does
    /// not fire when the menu is opened in code — measured — so a rule hung there is one no test
    /// can reach and one nothing guarantees ran. The selection is what the answer depends on, so
    /// that is what it watches.
    /// </para>
    /// </summary>
    internal void ShowCopySelection() =>
        CopySelectionItem.IsEnabled =
            Selected() is not null
            && TopLevel.GetTopLevel(this)?.Clipboard is not null;

    private void OnHelpClick(object? sender, RoutedEventArgs e) => OpenHelp();

    private void OnAskClick(object? sender, RoutedEventArgs e) => Model?.Ask();

    /// <summary>
    /// Enter sends; the arrows walk what has been sent
    /// (<a href="https://github.com/dseelinger/d47/issues/224">#224</a>).
    /// <para>
    /// <b>Enter stays first and stays unambiguous.</b> A history walk that ever swallowed a send
    /// would be a much worse defect than no history, so it is answered before anything else is
    /// considered and returns rather than falling through.
    /// </para>
    /// <para>
    /// <b>The list lives on the view model</b>, which is where both roads to sending meet — the
    /// button and this key. What the view adds is the caret: a recalled line is one the Commander
    /// is about to edit or send, so the cursor belongs at the end of it rather than wherever it
    /// happened to be.
    /// </para>
    /// <para>
    /// <b>Desktop only, and no headset case to think about.</b> This row is drawn where a host
    /// furnished it and the flat overlay is click-through, so keyboard history on a control only
    /// the window has is the whole of it.
    /// </para>
    /// </summary>
    private void OnAskBoxKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            e.Handled = true;
            Model?.Ask();

            return;
        }

        if (e.Key == Key.Up && Model?.WalkBack() == true)
        {
            e.Handled = true;
            CaretToEnd();
        }
        else if (e.Key == Key.Down && Model?.WalkForward() == true)
        {
            e.Handled = true;
            CaretToEnd();
        }
    }

    /// <summary>
    /// The cursor after a recalled line, which is the end of it. Avalonia leaves the caret where
    /// it was when the text changes underneath it, so without this an Up from an empty box puts
    /// the cursor at the front of the recalled question.
    /// </summary>
    private void CaretToEnd() => AskBox.CaretIndex = AskBox.Text?.Length ?? 0;

    private void OnUpdateNowClick(object? sender, RoutedEventArgs e) => Model?.AcceptUpdate();

    private void OnUpdateLaterClick(object? sender, RoutedEventArgs e) => Model?.DismissUpdate();

    private void OnDismissErrorClick(object? sender, RoutedEventArgs e) => Model?.DismissError();
}

/// <summary>
/// One stretch of the transcript as it will be drawn: the characters a reader sees, who said
/// them, whether the panel is speaking about the conversation rather than in it, and what the
/// model's markup asked for. A <see cref="TranscriptSegment"/> after
/// <see cref="TranscriptMarkup"/> has been through it.
/// </summary>
internal readonly record struct DrawnSegment(
    string Text,
    bool Marker,
    TranscriptVoice Voice,
    MarkupStyle Style);

/// <summary>
/// One side's uninterrupted stretch of the conversation — a bubble's worth. The flat pages use
/// one of these holding the whole page, so both presentations are drawn by the same code.
/// </summary>
internal sealed record DrawnTurn(
    TranscriptVoice Voice,
    bool Marker,
    IReadOnlyList<DrawnSegment> Segments);
