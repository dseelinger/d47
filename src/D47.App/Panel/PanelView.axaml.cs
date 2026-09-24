using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using D47.Core.Help;
using D47.Core.Interface;
using System.Globalization;

namespace D47.App.Panel;

/// <summary>The panel, as a view.</summary>
public partial class PanelView : UserControl
{
    /// <summary>How much of the panel this instantiation shows.</summary>
    public static readonly StyledProperty<PanelMode> ModeProperty =
        AvaloniaProperty.Register<PanelView, PanelMode>(nameof(Mode));

    /// <summary>The root key of each transcript mode.</summary>
    public const string ConversationRoot = "transcript.conversation";

    /// <inheritdoc cref="ConversationRoot"/>
    public const string LogRoot = "transcript.log";

    /// <summary>Elite's own journal, read as sentences (#51).</summary>
    public const string JournalRoot = "transcript.journal";

    /// <summary>The same events as the JSON Elite wrote (#51).</summary>
    public const string RawJournalRoot = "transcript.rawjournal";

    /// <summary>
    /// The help the conversation reading offers: a page about the page, rather than about any one
    /// capability (asked for 2026-08-23).
    /// </summary>
    public const string InShipHelp = D47.Core.Help.HelpLibrary.GeneralPrefix + "in-ship";

    /// <summary>The log file reading's own page (#262).</summary>
    public const string LogFileHelp = D47.Core.Help.HelpLibrary.GeneralPrefix + "log-file";

    /// <summary>The journal readings' page (#262), shared by both of them.</summary>
    public const string JournalHelp = D47.Core.Help.HelpLibrary.GeneralPrefix + "journal-file";

    private PanelViewModel? _bound;

    /// <summary>
    /// The tab that was showing when mini took it away, so leaving mini can put it back (Phase 51).
    /// </summary>
    private PanelTab? _beforeMini;

    /// <summary>
    /// Where the Commander is on this surface: which tab, which of its roots, and how far down (Phase
    /// 25).
    /// </summary>
    public PanelNavigator Nav { get; } = new();

    /// <summary>
    /// The two questions this surface can put to the Commander — pick one of these, and say or type
    /// this (Phase 25).
    /// </summary>
    public PanelPrompts Prompts { get; }

    /// <summary>How the host builds each tab's surface, for the tabs it gave.</summary>
    private readonly Dictionary<PanelTab, Func<NavCrumb, Control>> _builders = [];

    /// <summary>The drill strip each furnished tab is drawn in, kept.</summary>
    private readonly Dictionary<PanelTab, DrillView> _pages = [];

    /// <summary>The tab buttons, by tab, so a bar can be driven from the navigator.</summary>
    private readonly Dictionary<PanelTab, RadioButton> _tabs = [];

    /// <summary>The glyph the log mode carries while it reads the file (Phase 12).</summary>
    private readonly Controls.BusyGlyph _logBusy = new() { IsVisible = false, Margin = new Thickness(7, 0, 0, 0) };

    /// <summary>Whether the search affordance belongs on this surface.</summary>
    private bool _searchable;

    /// <summary>How the host shows the turn's figures, when it gave a way.</summary>
    private Func<Task>? _showTurnDetails;

    /// <summary>The tab to come back to when a furnished one is left.</summary>
    private PanelTab _lastTab = PanelTab.Transcript;

    /// <summary>What was last drawn, so a change of tab can name the one being left.</summary>
    private PanelTab _showing = PanelTab.Transcript;

    /// <summary>Which page was last drawn, as tab and root together.</summary>
    private PanelTab _showingTab = PanelTab.Transcript;

    /// <summary>Where a thread proposal card checks whether it is still waiting, and settles it (#277).</summary>
    private D47.Core.Checklists.ChecklistService? _checklists;

    private string _showingRoot = ConversationRoot;

    /// <summary>
    /// Set while the bar is being driven from the navigator, so the handlers that hear a tab check do
    /// not read it back as the Commander having pressed one.
    /// </summary>
    private bool _drivingBar;

    /// <summary>What is being searched for on this surface.</summary>
    private string _query = string.Empty;

    /// <summary>The hits in the current page, recomputed on every redraw.</summary>
    private IReadOnlyList<D47.Core.Interface.SearchMatch> _matches = [];

    /// <summary>Which hit is current, and where it starts in the page.</summary>
    private int _hit = -1;

    private int _hitOffset;

    /// <summary>
    /// Whether this surface is following the end of the transcript (Phase 19, "Follow the live log, or
    /// stop following it").
    /// </summary>
    private bool _following = true;

    /// <summary>
    /// Set while this view is doing the scrolling, so the handler below does not read its own <see
    /// cref="ScrollToEnd"/> as the Commander having moved.
    /// </summary>
    private bool _scrollingItself;

    /// <summary>
    /// Where the fold is, and on which reading (#413): the height the content had when Ctrl+L was last
    /// pressed.
    /// </summary>
    private (TranscriptPage Page, double Mark, string? Anchor, double Settled)? _fold;

    /// <summary>Set while the fold is being applied, so its own layout pass does not re-enter it.</summary>
    private bool _folding;

    /// <summary>
    /// The bubbles on the conversation page, in order, each with the offset into the page where its
    /// text begins and the strip of system-name chips beneath it, where one is drawn (#159).
    /// </summary>
    private readonly List<(SelectableTextBlock Block, int Start, WrapPanel? Strip)> _bubbles = [];

    /// <summary>What those bubbles were drawn from, as comparable things each.</summary>
    private IReadOnlyList<(TranscriptVoice Voice, bool Marker, string Text, string Direction)> _shape = [];

    /// <summary>The block a selection was last made in.</summary>
    private SelectableTextBlock? _selection;

    /// <summary>The bubble a drag began in, or -1 while no drag is under way (#114).</summary>
    private int _dragAnchor = -1;

    /// <summary>The character offset within <see cref="_dragAnchor"/> where the drag began (#114).</summary>
    private int _dragAnchorOffset;

    /// <summary>The bubbles a selection spans, first index to last inclusive, once a drag has crossed
    /// from one into another (#114).</summary>
    private (int First, int Last)? _span;

    public PanelView()
    {
        InitializeComponent();

        // Set in code rather than bound, because what mini hides is three named regions and a binding for
        // each would be three expressions no test can reach.
        ModeProperty.Changed.AddClassHandler<PanelView>((view, _) =>
        {
            // Before the chrome, because it may move the tab and the chrome is drawn from it.
            view.SettleMini(remember: true);

            view.ApplyChrome();

            // And redrawn, because mini is not only less chrome around the conversation: the bubbles
            // themselves give back their gutter and their padding on a surface that has 512 pixels to spend
            // (asked for 2026-08-22).
            view.DrawTranscript();
        });

        // `output-only` is set by the host after construction and toggled at runtime — the headset flips it
        // on every move between the big panel and mini.
        Classes.CollectionChanged += (_, _) =>
        {
            ShowFollowButton();
            ShowResizeButton();
        };

        // A drag that crosses from one bubble into another keeps both ends: no single SelectableTextBlock
        // sees a pointer that has left its own bounds, so the crossing is tracked here instead (#114).
        Bubbles.AddHandler(PointerPressedEvent, OnBubblesPointerPressed, handledEventsToo: true);
        Bubbles.AddHandler(PointerMovedEvent, OnBubblesPointerMoved, handledEventsToo: true);

        PageBar.SizeChanged += (_, _) => SizeSearchRow();

        Controls.Glyphs.Quiet(CopyButton, Controls.CopyWord.Word, "Copy this whole page to the clipboard");
        Controls.Glyphs.Quiet(TurnDetails, "SPEND", "Tokens, cost, and what this has come to over time");
        Controls.Glyphs.Quiet(ResizeButton, "RESIZE", "Resize the panel");
        Controls.Glyphs.Quiet(HelpButton, "HELP", "Open the documentation");

        Watch(Transcript);

        // Whether this theme has scanlines at all; the brush itself is built for this surface's scaling.
        this.GetResourceObservable(Theming.ThemeManager.ScanlinesKey)
            .Subscribe(new Avalonia.Reactive.AnonymousObserver<object?>(_ => DrawScanlines()));

        // Built once, checking `_copy` at render time rather than caching it, so a page opened before
        // `EnableCopy` runs still draws the glyph once it does (#158).
        JournalList.ItemTemplate = new Avalonia.Controls.Templates.FuncDataTemplate<D47.Core.Journal.JournalEntry>(
            (entry, _) => JournalRow(entry));

        // The three readings of one exchange, registered as the Transcript tab's roots.
        Nav.Register(
            PanelTab.Transcript,
            new NavCrumb(ConversationRoot, "In Ship")
            {
                Help = InShipHelp,

                // The words this reading answered to before it was In Ship.
                Spoken = ["conversation", "thread"],
            });

        Nav.Register(
            PanelTab.Transcript,
            new NavCrumb(LogRoot, "Log File")
            {
                Help = LogFileHelp,
                Spoken = ["log", "d47 log"],
            });

        // Elite's own journal, in a form that is not JSON (#51). "Elite Dangerous" came off the front in
        // #250; the alias below still answers to it, for free.
        Nav.Register(
            PanelTab.Transcript,
            new NavCrumb(JournalRoot, "Journal File")
            {
                Help = JournalHelp,
                Spoken = ["journal", "journal file", "elite dangerous journal"],
            });

        // Beside the box rather than inside it (#231).
        _logBusy.Bind(
            Avalonia.Controls.Shapes.Shape.StrokeProperty,
            this.GetResourceObservable(Theming.ThemeManager.AKey));

        ModePicker.Children.Add(_logBusy);

        Prompts = new PanelPrompts(Nav, Layer)
        {
            // Pulled rather than pushed, because the model is bound after this runs and can be replaced: a
            // value copied here would be the one that was true when the panel was built (remediation.md 10,
            // item 12).
            Waiting = () => Model?.ListeningPrompt,
        };

        // A layer chooser hides the page bar the same way a page chooser does, but nothing else notices
        // it opening or closing (#325).
        Prompts.LayerChanged += ShowSearch;

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

        // Tunnelling, because these gestures belong to the surface rather than to whatever has focus: Ctrl+F
        // has to reach the box from inside the ask box, and Escape has to be taken before the window decides
        // there is nothing left to close.
        AddHandler(KeyDownEvent, OnSurfaceKeyDown, RoutingStrategies.Tunnel);

        // The mouse Back button, ahead of a list row or a bubble taking the press (#342).
        AddHandler(PointerPressedEvent, OnSurfacePointerPressed, RoutingStrategies.Tunnel);

        // Scroll position belongs to a rendered surface rather than to the text, so each instance answers
        // this for itself: the window and the overlay can be scrolled to different places and still be
        // showing the same transcript.
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
                // Drawn before the scroll, because scrolling to the end of text that has not been written yet
                // lands one append behind.
                _bound.TranscriptAppended += OnTranscriptAppended;
                _bound.TranscriptAppended += ScrollToEnd;

                // The avatar follows the loop state.
                _bound.PropertyChanged += OnModelChanged;
                Avatar.Show(_bound.LoopState);
                ApplyMicrophone();
                ApplyAskHint();
            }

            // The model handed over is rarely empty — the window binds one that has already been written to —
            // and nothing else would redraw until the next append. **And the log page is read here rather
            // than merely drawn** (GitHub issue 43).
            Reread();
        };
    }

    public PanelMode Mode
    {
        get => GetValue(ModeProperty);
        set => SetValue(ModeProperty, value);
    }

    /// <summary>Which tab this instantiation is showing.</summary>
    public PanelTab Tab
    {
        get => Nav.Tab;
        set => Nav.Select(value);
    }

    /// <summary>
    /// Which reading of the transcript this instantiation is showing — and, since Phase 45, every other
    /// one: the host mirrors the root between surfaces, so setting it here sets it everywhere.
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

        // Marshalled here for the same reason ScrollToEnd is, and following the same rule: the view owns
        // thread affinity, so a new caller does not have to learn it separately.
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
            case nameof(PanelViewModel.ModelLoading):
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

            // The turn line is written as each turn completes, after its cost is recorded.
            case nameof(PanelViewModel.TurnLine):
                if (Dispatcher.UIThread.CheckAccess())
                {
                    ApplySessionSpend();
                    return;
                }

                Dispatcher.UIThread.Post(ApplySessionSpend);
                return;
        }
    }

    /// <summary>
    /// The ask box's placeholder: a worked example until the Commander has asked something, and a plain
    /// label for ever after (docs/plans/change-requests.md item 5).
    /// </summary>
    private void ApplyAskHint()
    {
        if (_bound is not null)
        {
            AskBox.PlaceholderText = _bound.AskHint;
        }
    }

    /// <summary>The ask box's tallest: three lines of body text, then it scrolls.</summary>
    public const double AskBoxMaxHeight = 80;

    /// <summary>
    /// Draws what the microphone is doing (Phase 13, "Show that the microphone is open"). The dot carries
    /// the state — cyan when ready, Warn while listening or loading, Danger when nothing is open — and is
    /// hollow unless a device is open; the words stay ink-3 in every state.
    /// </summary>
    private void ApplyMicrophone()
    {
        if (_bound is null)
        {
            return;
        }

        var state = _bound.Microphone;
        var loading = _bound.ModelLoading && state != D47.Core.Listening.MicrophoneState.Open;

        var (key, label, filled) = state switch
        {
            D47.Core.Listening.MicrophoneState.Open => (Theming.ThemeManager.AKey, "MIC ON", true),
            D47.Core.Listening.MicrophoneState.Armed => (Theming.ThemeManager.AKey, "LISTENING", true),
            D47.Core.Listening.MicrophoneState.Idle => (Theming.ThemeManager.CyanKey, "PTT READY", true),
            _ => (Theming.ThemeManager.RedKey, "MIC OFF", false),
        };

        // An open gate is open whatever the model is doing. Every other state would otherwise report that
        // the microphone is ready while the model is still loading (#147).
        if (loading)
        {
            (key, label, filled) = (Theming.ThemeManager.AKey, "LOADING MODEL", false);
        }

        MicrophoneGlyph.Bind(Avalonia.Controls.Shapes.Shape.StrokeProperty, this.GetResourceObservable(key));

        if (filled)
        {
            MicrophoneGlyph.Bind(Avalonia.Controls.Shapes.Shape.FillProperty, this.GetResourceObservable(key));
        }
        else
        {
            MicrophoneGlyph.Fill = null;
        }

        // A filled dot glows in its own colour.
        MicrophoneBloom.Bind(Theming.BloomStack.GlowProperty, this.GetResourceObservable(key));
        MicrophoneBloom.IsLit = filled;

        MicrophoneLabel.Text = label;

        // Only a ready microphone lights its words; the other states stay grey.
        MicrophoneLabel.Bind(
            TextBlock.ForegroundProperty,
            this.GetResourceObservable(key == Theming.ThemeManager.CyanKey && !loading
                ? Theming.ThemeManager.CyanKey
                : Theming.ThemeManager.Grey2Key));
    }

    /// <summary>The Settings tab, so a host can hang a tooltip naming the bound gesture on it.</summary>
    public Control SettingsAffordance => SettingsTab;

    /// <summary>
    /// Gives this surface a settings page, built by <paramref name="build"/> the first time it is
    /// selected.
    /// </summary>
    /// <param name="build">The settings page itself.</param>
    /// <param name="reveal">Jumps the settings page to one capability's card.</param>
    /// <param name="learnedPhrases">
    /// What the flying Commander has taught D47 stands for a declared phrase (#171), as a second root on
    /// this tab — or null for a surface that does not get one.
    /// </param>
    public void EnableSettings(
        Func<Control> build,
        Action<string>? reveal = null,
        Func<LearnedPhrasesPage>? learnedPhrases = null)
    {
        _revealSetting = reveal;

        LearnedPhrasesPage? phrases = null;

        var roots = new List<NavCrumb>
        {
            new("settings", "Settings")
            {
                Help = D47.Core.Capabilities.Builtin.SettingsCapability.Id,
            },
        };

        if (learnedPhrases is not null)
        {
            roots.Add(new NavCrumb(LearnedPhrasesPage.RootKey, "Learned phrases")
            {
                Help = D47.Core.Capabilities.Builtin.LearnedPhrasesCapability.Id,
            });
        }

        Furnish(
            PanelTab.Settings,
            crumb => crumb.Key switch
            {
                LearnedPhrasesPage.RootKey when learnedPhrases is not null => phrases ??= learnedPhrases(),
                _ => build(),
            },
            [.. roots]);
    }

    /// <summary>How this surface shows one settings section, or null where it has no settings.</summary>
    private Action<string>? _revealSetting;

    /// <summary>
    /// What a help card naming a settings section does when pressed, or null when this surface cannot
    /// do it — which is the headset, and is why the card there stays an ordinary drill into the page
    /// about the same subject.
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

    /// <summary>Gives this surface the checklist (Phase 25, "The checklist leaves its window").</summary>
    /// <param name="goals">The Commander's long arcs (Phase 34).</param>
    /// <param name="backfill">What "read my journals" does.</param>
    public void EnableChecklist(
        D47.Core.Checklists.ChecklistService checklists,
        D47.Core.Goals.GoalBook? goals = null,
        Action? backfill = null)
    {
        // Held for the thread's own proposal cards: whether one is still pending is looked up here rather
        // than trusted from what drew it last (#277).
        _checklists = checklists;

        ChecklistPage? page = null;

        Furnish(
            PanelTab.Checklist,
            crumb => crumb.Key switch
            {
                ChecklistPage.SuggestionsKey => page?.BuildSuggestions()
                                                ?? new TextBlock { Text = "Nothing waiting." },
                _ => page = new ChecklistPage(checklists, Nav, Prompts, goals, backfill),
            },
            new NavCrumb("checklist", "Checklist")
            {
                // The suggestions level drilled from here inherits it: a proposal is still the checklist's
                // subject.
                Help = D47.Core.Capabilities.Builtin.ChecklistCapability.Id,
            });

        // How many are still open, on the tab itself (asked for 2026-08-20). **Open rather than every line**:
        // a checklist's whole question is how much is left, and a count that never falls as the Commander
        // works is a number they learn to ignore.
        void Count()
        {
            var open = checklists.Document.Items.Count(item => !item.IsComplete);

            // The number is gone from the tab (#234).
            _ = open;
        }

        // The store's event, which is the one the page itself listens to — so the tab and the page cannot
        // come to disagree about how many there are.
        checklists.List.Changed += () => Dispatcher.UIThread.Post(Count);
        Count();
    }

    /// <summary>
    /// Gives this surface the fleet, what the Commander is wearing, and the arithmetic between them
    /// (Phase 26, "Ships"; Phase 27, "Suits and weapons, and the gap").
    /// </summary>
    /// <summary>
    /// Gives this surface a clipboard, so every system name the panel draws can carry a copy glyph
    /// (#157).
    /// </summary>
    public void EnableCopy(D47.Core.Capabilities.Builtin.IClipboard clipboard) =>
        _copy = text => clipboard.SetTextAsync(text);

    private Func<string, Task<bool>>? _copy;

    /// <summary>
    /// Gives this surface the system names d47 already holds, so a conversation turn that names one draws a
    /// copy chip beneath it (#159), and the Commander's current system, whose chip is drawn in cyan.
    /// </summary>
    public void EnableSystemNames(D47.Core.Knowledge.SystemsInPlay known, Func<string?>? current = null)
    {
        _systemsInPlay = known;
        _currentSystem = current;
    }

    private D47.Core.Knowledge.SystemsInPlay? _systemsInPlay;

    private Func<string?>? _currentSystem;

    /// <summary>Gives this surface the Commander's name, which heads their turns.</summary>
    public void EnableCommanderName(Func<string?> name) => _commanderName = name;

    private Func<string?>? _commanderName;

    public void EnableLoadout(
        D47.Core.Ships.ShipPlanService ships,
        D47.Core.Checklists.ChecklistService checklists,
        Func<D47.Core.Journal.CommanderGameState?> state,
        D47.Core.Loadout.OnFootPlanService? onFoot = null,
        Func<D47.Core.Journal.ModulePower>? modulePower = null,
        Func<bool>? hullArt = null,

        // Ships' own settings, on the tab they only affect (#218).
        Func<Control?>? settingsStrip = null,

        // The captain and tower's own settings, on the tab they only affect (#218, #305).
        Func<Control?>? carrierSettingsStrip = null)
    {
        var shipsMode = new ShipsMode(ships, checklists, state, modulePower, hullArt);

        // Kept, so the tick has something to invalidate (remediation.md 17, item 7).
        _loadoutMode = shipsMode;
        _loadoutState = state;
        _loadoutEngineerStamp = D47.Core.Engineers.UnlockPlanner.Stamp(state());

        var modes = new List<ILoadoutMode> { shipsMode };

        if (onFoot is not null)
        {
            var onFootMode = new OnFootMode(onFoot, checklists, state);

            _onFootMode = onFootMode;
            modes.Add(onFootMode);
        }

        // Recomputed on every draw rather than cached: it is a subtraction over two stores and the live
        // inventory, all three of which move under the page.
        GapSource? gap = null;

        if (onFoot is not null)
        {
            gap = new GapSource(
                () => D47.Core.Loadout.PlanGap.Of(
                    ships.Store.Builds,
                    onFoot.Store.Builds,
                    state(),
                    includeIntended: true,
                    checklists.SlotFor),
                state);

            // Either store moving changes the subtraction, and neither knows about the other.
            ships.Store.Changed += gap.Invalidate;
            onFoot.Store.Changed += gap.Invalidate;
        }

        // Per root, because this tab's roots are three different subjects.
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

            roots.Add(new NavCrumb(LoadoutPages.GapRoot, "Materials")
            {
                Help = D47.Core.Capabilities.Builtin.GapCapability.Id,
            });
        }

        // The carrier, on the tab that took its name (#230).
        _carrier = new CarrierSource(
            () => state()?.Carrier ?? D47.Core.Journal.CarrierState.None,
            () => state()?.SquadronCarrier ?? D47.Core.Journal.CarrierState.NoSquadron,
            () => state()?.Hold is { IsShip: true } hold ? hold.Of("tritium") : 0,
            () => state()?.Location.StarSystem);

        // No help declared: no capability page covers the carrier yet, and a root whose page has no band
        // simply shows no mark.
        roots.Add(new NavCrumb(LoadoutPages.CarrierRoot, "Carrier"));

        Furnish(
            PanelTab.Loadout,
            crumb => LoadoutPages.Build(
                crumb, modes, gap, _carrier, Nav, Prompts, _copy, settingsStrip, carrierSettingsStrip),
            [.. roots]);
    }

    /// <summary>Gives this surface the Commander's adventures (Phase 47).</summary>
    /// <param name="settingsStrip">Adventures' own settings, on the tab they only affect (#218).</param>
    public void EnableAdventures(AdventureSurface surface, Func<Control?>? settingsStrip = null)
    {
        AdventuresPage? page = null;

        Furnish(
            PanelTab.Adventures,
            crumb => crumb.Key == AdventuresPage.RootKey
                ? page = new AdventuresPage(surface, Nav, Prompts, settingsStrip?.Invoke())
                : page?.Build(crumb) ?? new TextBlock { Text = "Nothing here." },
            new NavCrumb(AdventuresPage.RootKey, "Adventures")
            {
                Help = D47.Core.Capabilities.Builtin.AdventureCapability.Id,
            });

        // And the same story at mini's size (asked for 2026-08-22).
        _adventureMini = new AdventureMini(surface);
        ApplyChrome();
    }

    /// <summary>Gives this surface the engineer directory and the solver (Phase 28, "Engineers").</summary>
    public void EnableEngineers(
        D47.Core.Engineers.EngineerPlanService unlocks,
        D47.Core.Ships.ShipPlanService ships,
        Func<D47.Core.Journal.CommanderGameState?> state,
        D47.Core.Loadout.OnFootPlanService? onFoot = null,
        EngineerDirectoryMemory? memory = null,
        D47.Core.Checklists.ChecklistService? checklists = null)
    {
        var source = new EngineerSource(
            unlocks.Report,
            checklists is null ? null : checklists.IsPinned,
            checklists is null ? null : checklists.Pin,
            unlocks.AddPrerequisites);

        // A plan moving changes who is worth flying to, and neither store knows about this page.
        ships.Store.Changed += source.Invalidate;

        if (onFoot is not null)
        {
            onFoot.Store.Changed += source.Invalidate;
        }

        // A pin set or cleared here is exactly what this page exists to show (#113).
        if (checklists is not null)
        {
            checklists.PinnedChanged += source.Invalidate;
        }

        _engineers = source;
        _engineerStamp = D47.Core.Engineers.UnlockPlanner.Stamp(state());
        _engineerState = state;

        // The first tab whose help is drawn in the panel rather than opened in a browser.
        var help = D47.Core.Capabilities.Builtin.EngineerCapability.Id;

        Furnish(
            PanelTab.Engineers,
            crumb => EngineersPages.Build(crumb, source, Nav, memory, _copy),
            new NavCrumb(EngineersPages.DirectoryRoot, "Directory") { Help = help },
            new NavCrumb(EngineersPages.RouteRoot, "Route") { Help = help });
    }

    /// <summary>Redraws the engineer pages when the Commander has moved, re-fitted or unlocked somebody.</summary>
    public bool TickEngineers()
    {
        if (_engineers is not { } source || Tab != PanelTab.Engineers)
        {
            return false;
        }

        var stamp = D47.Core.Engineers.UnlockPlanner.Stamp(_engineerState?.Invoke());

        if (string.Equals(stamp, _engineerStamp, StringComparison.Ordinal))
        {
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
    /// </summary>
    public bool TickLoadout()
    {
        if (_loadoutMode is not { } mode || Tab != PanelTab.Loadout)
        {
            return false;
        }

        var changed = false;

        // The carrier moves on its own events rather than with the ship, so it is compared separately (#230):
        // a jump booked while the Commander is nowhere near it changes this page and changes nothing about
        // the hull they are sitting in.
        var carrier = _loadoutState?.Invoke()?.Carrier;
        var squadron = _loadoutState?.Invoke()?.SquadronCarrier;
        var hold = _loadoutState?.Invoke()?.Hold;

        if (!ReferenceEquals(carrier, _carrierSeen)
            || !ReferenceEquals(squadron, _squadronSeen)
            || !ReferenceEquals(hold, _holdSeen))
        {
            _carrierSeen = carrier;
            _squadronSeen = squadron;
            _holdSeen = hold;
            _carrier?.Invalidate();
            changed = true;
        }

        var current = _loadoutState?.Invoke()?.Ship;

        if (!ReferenceEquals(current, _loadoutSeen))
        {
            _loadoutSeen = current;
            mode.Invalidate();
            changed = true;
        }

        // Neither store above knows about a jump or a rank-up, and the engineers block on a slot page reads
        // both — the same stamp the Engineers tab already compares itself against (#195).
        var engineerStamp = D47.Core.Engineers.UnlockPlanner.Stamp(_loadoutState?.Invoke());

        if (!string.Equals(engineerStamp, _loadoutEngineerStamp, StringComparison.Ordinal))
        {
            _loadoutEngineerStamp = engineerStamp;
            mode.Invalidate();
            _onFootMode?.Invalidate();
            _carrier?.Invalidate();
            changed = true;
        }

        return changed;
    }

    /// <summary>Redraws an open Ships page after the Hull pictures setting changes (#247).</summary>
    public void InvalidateLoadout() => _loadoutMode?.Invalidate();

    private ShipsMode? _loadoutMode;
    private OnFootMode? _onFootMode;
    private Func<D47.Core.Journal.CommanderGameState?>? _loadoutState;
    private D47.Core.Journal.ShipLoadout? _loadoutSeen;
    private D47.Core.Journal.CarrierState? _carrierSeen;
    private D47.Core.Journal.CarrierState? _squadronSeen;
    private D47.Core.Journal.CargoHold? _holdSeen;
    private string _loadoutEngineerStamp = string.Empty;

    /// <summary>Gives this surface the clocks, timers and alarms (Phase 24, "Utilities").</summary>
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

    /// <summary>Redraws the clocks, from the host's tick.</summary>
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
    /// Gives this surface the Routing tab (Phase 37): where the Commander is going, in three readings
    /// of one journey.
    /// </summary>
    public void EnableRouting(
        RoutingSurface surface,
        bool plan = true,
        bool progress = true,
        bool course = true,
        bool market = true,
        bool communityGoal = true,
        bool trade = true,

        // Community Goal's own settings, on the tab they only affect (#218).
        Func<Control?>? settingsStrip = null)
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

        // Last of the four, because it is the newest and because the three before it are the journey and this
        // is the errand (Phase 49).
        if (market && surface.Commodities is not null)
        {
            roots.Add(new NavCrumb(RoutingPages.MarketRoot, "Market")
            {
                Help = D47.Core.Capabilities.Builtin.GalaxyCapability.Id,
            });
        }

        // After Market, because it is the errand's special case (#296): one saved question and the ledger of
        // what it earned.
        if (communityGoal && surface.Commodities is { } commodities && surface.CommunityGoal is { } goal)
        {
            roots.Add(new NavCrumb(RoutingPages.CommunityGoalRoot, "Community Goal")
            {
                Help = D47.Core.Capabilities.Builtin.CommunityGoalCapability.Id,
            });

            // An answer or a sale landing anywhere — by voice, from the page's own button, from the journal —
            // leaves the Community Goal page one redraw out of date, for the reason the plan book below is
            // one subscription (#296).
            commodities.Posted += () => _routeCommunityGoal?.Refresh();
            goal.Ledger.Changed += () => _routeCommunityGoal?.Refresh();
        }

        // After Community Goal, because it is the newest of the five (#311): its own saved hops, jumps and
        // switches, split out of the Plan page's Trade run card.
        if (trade && surface.Plans is not null && surface.Settings is not null)
        {
            roots.Add(new NavCrumb(RoutingPages.TradeRoot, "Trade route")
            {
                Help = D47.Core.Capabilities.Builtin.RouteCapability.Id,
            });
        }

        if (roots.Count == 0)
        {
            return;
        }

        _routeState = surface.Route;
        _routeHere = surface.Here;
        _routeRange = surface.JumpRange;

        // Mini's own reading of the Plan root, wherever there is a book to read it from (#197).
        if (surface.Plans is { } miniPlans)
        {
            _routeMini = new RouteMini(miniPlans, surface.Route, surface.Here);
        }

        Furnish(
            PanelTab.Routing,
            crumb =>
            {
                var page = RoutingPages.Build(crumb, surface, Nav, settingsStrip);

                // Held onto so the tick can redraw Progress and a plot made elsewhere can redraw Plan.
                _routeProgress = page as RouteProgressPage ?? _routeProgress;
                _routePlan = page as RoutePlanPage ?? _routePlan;
                _routeTrade = page as RouteTradePage ?? _routeTrade;
                _routeCommunityGoal = page as RouteCommunityGoalPage ?? _routeCommunityGoal;
                _routeResult = page as RoutePlanResultPage ?? _routeResult;

                return page;
            },
            [.. roots]);

        // A plot made anywhere - this tab's own button, or a spoken tool call - leaves the Plan page one
        // redraw out of date, because "Show most recent" is drawn from the book. Mini reads the same
        // book, so it moves with the same event.
        if (surface.Plans is { } plans)
        {
            plans.Changed += () =>
            {
                _routePlan?.Refresh();
                _routeTrade?.Refresh();
                _routeMini?.Refresh();
                _routeResult?.Refresh();
            };
        }
    }

    /// <summary>
    /// Redraws the route being flown, from the host's tick, and says whether anything on it moved — the
    /// headset serves a frame for a true and holds the last one for a false (#52).
    /// </summary>
    public bool TickRouting()
    {
        if (Tab != PanelTab.Routing)
        {
            return false;
        }

        var route = _routeState?.Invoke();
        var here = _routeHere?.Invoke();

        // And the ship's range, which the plan forms quote (#253).
        var range = _routeRange?.Invoke();

        if (ReferenceEquals(route, _routeSeen)
            && string.Equals(here, _routeWhere, StringComparison.Ordinal)
            && Nullable.Equals(range, _routeRangeSeen))
        {
            return false;
        }

        _routeSeen = route;
        _routeWhere = here;
        _routeRangeSeen = range;

        // The progress page only where a host furnished one — the plan forms are furnished separately, and a
        // surface with one and not the other used to return before either.
        _routeProgress?.Refresh();

        // RefreshSupplied rather than Refresh: this fires on every jump, and rebuilding the page then would
        // throw away a half-typed destination.
        _routePlan?.RefreshSupplied();

        // The mark on mini's waypoint list is read from the same route and position (#197).
        _routeMini?.Refresh();

        // The plan's reached stop moves on the same arrival that moves the route and position, so it rides
        // this same guard (#200).
        _routeResult?.Refresh();

        return true;
    }

    private RouteProgressPage? _routeProgress;
    private RoutePlanPage? _routePlan;
    private RouteTradePage? _routeTrade;
    private RouteCommunityGoalPage? _routeCommunityGoal;
    private RoutePlanResultPage? _routeResult;
    private AdventureMini? _adventureMini;
    private RouteMini? _routeMini;
    private Func<D47.Core.Journal.NavRoute>? _routeState;
    private Func<string?>? _routeHere;
    private Func<double?>? _routeRange;
    private D47.Core.Journal.NavRoute? _routeSeen;
    private string? _routeWhere;
    private double? _routeRangeSeen;

    /// <summary>
    /// Gives this surface a tab, built by <paramref name="build"/> the first time it is selected, with
    /// the roots it offers (Phase 25).
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
    /// Back, and the one method all three routes that must agree go through — the breadcrumb, the
    /// controller button and the phrase (Phase 25).
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

        // Never back into a tab that is no longer furnished, which is a state a surface handed one builder
        // and then another could otherwise reach.
        Tab = Nav.Has(_lastTab) && _lastTab != Tab ? _lastTab : PanelTab.Transcript;
        return true;
    }

    /// <summary>Moves the page this surface is showing, by however much was asked for (#34).</summary>
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

        // A page is a screenful less one line, so the line a Commander was reading when they said it is still
        // there when the page settles.
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
            // Already at that end, and said so rather than merely not moving: a Commander who says "page
            // down" at the bottom should hear that they are at the bottom rather than watch nothing and
            // wonder whether they were heard.
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

    /// <summary>The scroller for whatever region is showing, or null where the region has none.</summary>
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

    public void EnableSearch()
    {
        _searchable = true;
        ApplyChrome();
    }

    /// <summary>Opens the sharing window.</summary>
    private Action? _donate;

    /// <summary>
    /// Offers the way into a bug report: a scrubbed window of Elite's events and d47's own log, or the
    /// whole journal history — shown or described in full and sent nowhere until the Commander says so
    /// (#160, one button since #238).
    /// </summary>
    public void EnableDonation(Action open)
    {
        _donate = open;
        ApplyChrome();
    }

    private Func<Control?>? _logSettingsStrip;

    /// <summary>The Log file page's own settings — log levels — on the tab they only affect (#283).</summary>
    public void EnableLog(Func<Control?>? settingsStrip)
    {
        _logSettingsStrip = settingsStrip;
        ShowLogSettingsStrip();
    }

    /// <summary>Adds the Raw Journal reading (#51), on a surface that has somewhere useful to put it.</summary>
    public void EnableRawJournal()
    {
        Nav.Register(
            PanelTab.Transcript,
            new NavCrumb(RawJournalRoot, "Raw Journal") { Help = JournalHelp });

        DrawModes();
    }

    /// <summary>Keeps the Raw switch where the Commander left it, across launches (#267).</summary>
    public void RememberJournalReading(JournalReadingMemory memory) => _journalReading = memory;

    /// <summary>Puts every tab back on the reading it was left on, and keeps them there (#268).</summary>
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

    /// <summary>Puts this surface back on the tab it was left on, and keeps it there (#276).</summary>
    public void RememberTab(PanelTabMemory memory)
    {
        _tabMemory = memory;

        if (memory.Remembered() is { } tab)
        {
            Tab = tab;
        }

        RecordRoots();
    }

    /// <summary>Which tab this surface was left on, or null on a surface not asked to remember it.</summary>
    private PanelTabMemory? _tabMemory;

    /// <summary>Writes down the reading every furnished tab is on (#268).</summary>
    private void RecordRoots()
    {
        // Which tab this surface is on (#276), independent of whether roots are being remembered here at all
        // — a surface can be asked to remember one without the other.
        _tabMemory?.Remember(Nav.Tab);

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
    /// because that one is about whichever tab is drawn and this question is about the Transcript tab
    /// whether or not it is the one showing.
    /// </summary>
    private string _journalReadingWas = ConversationRoot;

    /// <summary>Applies the remembered Raw position, and records it when it moves (#267).</summary>
    private bool ApplyRememberedJournalReading()
    {
        // Only where a host furnished the raw reading.
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
    /// Where the Commander dragged the rules between panes, on the one surface that has a mouse (Phase
    /// 55).
    /// </summary>
    private CarrierSource? _carrier;

    private PaneWidthMemory? _paneWidths;

    /// <summary>Lets the mouse drag the rule between two panes, on every tab at once (Phase 55).</summary>
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
    /// </summary>
    /// <remarks>
    /// <paramref name="session"/> is the session's model spend, shown beside the link and read again after
    /// each turn and when the figures close.
    /// </remarks>
    public void EnableTurnDetails(Func<Task> show, Func<decimal> session)
    {
        _showTurnDetails = show;
        _sessionSpend = session;
        SpendRow.IsVisible = true;
        ApplySessionSpend();
    }

    private void OnTurnDetailsClick(object? sender, RoutedEventArgs e)
    {
        if (_showTurnDetails is { } show)
        {
            _ = ShowTurnDetailsAsync(show);
        }
    }

    private async Task ShowTurnDetailsAsync(Func<Task> show)
    {
        await show();
        ApplySessionSpend();
    }

    private Func<decimal>? _sessionSpend;

    private void ApplySessionSpend()
    {
        if (_sessionSpend is { } session)
        {
            SessionFigure.Text = session().ToString("C4", System.Globalization.CultureInfo.CurrentCulture);
        }
    }

    /// <summary>How the host opens the documentation site, when it gave a way.</summary>
    private Action<string>? _openHelp;

    /// <summary>Gives this surface a way out to the web.</summary>
    public void EnableHelp(Action<string> open)
    {
        _openHelp = open;
        ShowHelpAffordance();
    }

    /// <summary>
    /// The mark shows when this surface can do something with it: open the site, which only the desktop
    /// can, or draw the help for where the Commander is standing, which either surface can.
    /// </summary>
    private bool OutputOnly => Classes.Contains("output-only");

    private void ShowHelpAffordance() =>
        HelpButton.IsVisible = !OutputOnly
            && (_openHelp is not null
                || HelpPageView.Exists(Nav.Help)
                || HelpPageView.Exists(HelpLevel.Index));

    /// <summary>How this surface enters resize mode, when a host has given it a way (#190).</summary>
    private Action? _enterResize;

    /// <summary>Whether the motion controllers a resize drag needs are switched on.</summary>
    private bool _resizeAvailable;

    /// <summary>Gives this surface a way into resize mode, so the mark beside Help can offer it.</summary>
    public void EnableResize(Action enter)
    {
        _enterResize = enter;
        ShowResizeButton();
    }

    /// <summary>Whether resizing by hand is possible right now, read from the headset's own settings.</summary>
    public void SetControllersOn(bool on)
    {
        if (_resizeAvailable == on)
        {
            return;
        }

        _resizeAvailable = on;
        ShowResizeButton();
    }

    /// <summary>
    /// The mark shows only on the headset's full panel, with a way in and the controllers to use it —
    /// mini carries no buttons at all, and there is nothing for a ray to press without a controller.
    /// </summary>
    private void ShowResizeButton() =>
        ResizeButton.IsVisible = !OutputOnly
            && Classes.Contains("headset")
            && _resizeAvailable
            && _enterResize is not null;

    private void OnResizeClick(object? sender, RoutedEventArgs e) => _enterResize?.Invoke();

    /// <summary>Shows the pre-release mark beside the help glyph, or takes it away (#92).</summary>
    public void ShowChannel(D47.Core.Updates.ReleaseChannel channel)
    {
        // The wording comes from Core with the rest of it, so the badge cannot say one thing while the title
        // bar and About say another - which is the whole reason that text lives there.
        var marker = D47.Core.Updates.ReleaseChannelText.Short(channel);

        PreReleaseBadge.IsVisible = !OutputOnly && marker is not null;

        if (marker is not null)
        {
            PreReleaseBadgeText.Text = marker.ToUpperInvariant();
        }
    }

    /// <summary>
    /// Help over the page rather than beside it (asked for 2026-08-22): pushed as a modal level, so
    /// every route that would navigate away is refused until it is dismissed, and dismissing it is <see
    /// cref="GoBack"/> — the breadcrumb, the controller button and the spoken word, already agreeing
    /// with no special case anywhere.
    /// </summary>
    public bool OpenHelp() =>
        OpenHelpFor(Tab == PanelTab.Transcript || Nav.Modal ? null : (PagePane.Child as IPageChrome)?.HelpTopic);

    /// <summary>
    /// The same, for a mark that is about something narrower than the tab it sits on — a settings
    /// card's question mark, which is about that capability rather than about Settings (asked for
    /// 2026-08-23).
    /// </summary>
    public bool OpenHelpFor(string? capabilityId)
    {
        if (HelpLevel.Showing(Nav))
        {
            // Already showing.
            return false;
        }

        // Whatever was asked for, then whatever the level being looked at claims, then the index.
        if (HelpLevel.Open(Nav, capabilityId))
        {
            return true;
        }

        if (_openHelp is null)
        {
            return false;
        }

        // Not even an index drawn yet, and a desktop to fall out to.
        _openHelp(capabilityId is { Length: > 0 } id ? DocsSite.Page(id) : DocsSite.Root);
        return true;
    }

    /// <summary>What is drawn, and for which crumb.</summary>
    private (string Key, Control Page)? _helpPane;

    /// <summary>What a level that has taken the panel draws.</summary>
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

    /// <summary>Puts what is on the page above the top of the view, and deletes nothing (#413).</summary>
    public bool ScrollPastReading()
    {
        // Only where there is a reading to fold, which is the same question the menu item's greying already
        // asks.
        if (Tab != PanelTab.Transcript)
        {
            return false;
        }

        var scroller = Scroller;

        // The thread's own height rather than the viewport's, since the mark is where the reading ends and
        // new bubbles have to grow the extent past it. AnchorThread leaves it off while the fold holds.
        TranscriptContent.MinHeight = 0;

        // Laid out first, for the reason Follow gives: the mark is the height of the content as it is now,
        // and a run appended a moment ago is not in the extent until this returns.
        scroller.UpdateLayout();

        if (scroller.Viewport.Height <= 0)
        {
            return false;
        }

        var content = Math.Max(0, scroller.Extent.Height - PadOn(FoldPad));
        var anchor = FoldAnchor();

        // What the anchor already reads as, taken off every later reading of it.
        var arrived = anchor is null ? null : FoldArrived(anchor, content);

        _fold = (Page, content, arrived is null ? null : anchor, arrived ?? 0);
        ApplyFold(reassert: true);

        return true;
    }

    /// <summary>How much of the reading's newest end is remembered as the fold's anchor (#413).</summary>
    private const int FoldAnchorLength = 256;

    /// <summary>
    /// The text the fold is set against, on the readings drawn as a flat block — which are the two that
    /// are files, and so the two that trim (#413).
    /// </summary>
    private string? FoldAnchor()
    {
        if (!Transcript.IsVisible || Page == TranscriptPage.Journal)
        {
            return null;
        }

        // The runs rather than Text: the flat block is written as inlines from code-behind, so the Text
        // property is empty and the character offsets the layout answers about are these.
        var text = TranscriptShown;

        if (text.Length == 0)
        {
            return null;
        }

        var take = Math.Min(FoldAnchorLength, text.Length);

        return NewestAtTop ? text[..take] : text[^take..];
    }

    /// <summary>
    /// How much reading sits past the fold's anchor — below it where the reading grows downwards, above
    /// it on the two newest-first ones (#413).
    /// </summary>
    private double? FoldArrived(string anchor, double content)
    {
        if (Transcript.TextLayout is not { } layout)
        {
            return null;
        }

        var text = TranscriptShown;

        // The last occurrence on a reading that grows downwards and the first on one that grows upwards:
        // either way the one nearest the end the anchor was taken from.
        var at = NewestAtTop
            ? text.IndexOf(anchor, StringComparison.Ordinal)
            : text.LastIndexOf(anchor, StringComparison.Ordinal);

        if (at < 0)
        {
            return null;
        }

        // Through the text layout for the reason ScrollToHit gives: it is the one thing that knows where a
        // character offset landed once the text wrapped.
        var where = layout.HitTestTextPosition(NewestAtTop ? at : at + anchor.Length - 1);

        return NewestAtTop ? Math.Max(0, where.Top) : Math.Max(0, content - where.Bottom);
    }

    /// <summary>
    /// Where the fold's empty space is added: the control inside whichever scroller this reading uses,
    /// so the padding moves with <see cref="Scroller"/> rather than being a fifth thing to keep in
    /// step.
    /// </summary>
    private Control FoldPad =>
        Page == TranscriptPage.Journal ? JournalList : TranscriptContent;

    private static double PadOn(Control pad) => pad.Margin.Top + pad.Margin.Bottom;

    /// <summary>Holds the fold, and gives the space back as the reading grows into it (#413).</summary>
    private void ApplyFold(bool reassert)
    {
        // Not from inside itself: the padding it sets changes the extent, and the extent changing is one of
        // the two things that call this.
        if (_folding)
        {
            return;
        }

        var pad = FoldPad;

        if (_fold is not { } fold || fold.Page != Page)
        {
            DropFold(pad);
            return;
        }

        var scroller = Scroller;
        var viewport = scroller.Viewport.Height;
        var content = Math.Max(0, scroller.Extent.Height - PadOn(pad));
        var was = fold.Mark;
        var mark = was;
        var grown = Math.Max(0, content - was);

        // Where the anchor is now, on a reading that trims its front: the lines the press was set against
        // move up under a mark that would otherwise stay where it was put, and nothing there would ever count
        // as having grown (#413).
        if (fold.Anchor is { } anchor)
        {
            if (FoldArrived(anchor, content) is not { } arrived)
            {
                DropFold(pad);
                return;
            }

            grown = Math.Max(0, arrived - fold.Settled);
            mark = Math.Max(0, content - grown);
            _fold = (fold.Page, mark, anchor, fold.Settled);
        }

        // Whether the view is still sitting on the fold, asked before the mark is allowed to move it.
        var onTheFold = NewestAtTop
            ? scroller.Offset.Y <= Transcript.FontSize
            : Math.Abs(scroller.Offset.Y - was) <= Transcript.FontSize;

        var wanted = Math.Max(0, viewport - grown);

        _folding = true;

        try
        {
            var margin = NewestAtTop
                ? new Thickness(0, wanted, 0, 0)
                : new Thickness(0, 0, 0, wanted);

            if (pad.Margin != margin)
            {
                pad.Margin = margin;

                // Laid out here rather than left to the next pass, because the caller's very next act is
                // usually a scroll — <see cref="Follow"/>'s — and it would go to the end of an extent still
                // holding the empty space this call has just given back.
                scroller.UpdateLayout();
            }
        }
        finally
        {
            _folding = false;
        }

        // On the press, and again whenever the reading has shifted under a view that was still sitting on the
        // fold: the fold is a place in the text rather than a number of pixels down the page, so a reading
        // that trims its front takes the view with it (#413).
        if (!reassert && (mark == was || !onTheFold))
        {
            return;
        }

        _scrollingItself = true;

        try
        {
            // The newest end goes to the top of the view.
            scroller.Offset = scroller.Offset.WithY(
                NewestAtTop
                    ? 0
                    : Math.Clamp(mark, 0, Math.Max(0, scroller.Extent.Height - viewport)));
        }
        finally
        {
            _scrollingItself = false;
        }

        ShowFollowButton();
    }

    /// <summary>
    /// Gives the fold's empty space back and forgets it — on leaving the reading it belongs to, and
    /// once the reading has trimmed the lines it was set against away entirely (#413).
    /// </summary>
    private void DropFold(Control pad)
    {
        _fold = null;

        if (PadOn(pad) > 0)
        {
            pad.Margin = default;
        }

        AnchorThread();
    }

    /// <summary>
    /// Holds the conversation's content at least as tall as the view, so its bubbles, aligned to the
    /// bottom, sit beside the ask box — except while a fold holds, which measures the thread's own height.
    /// </summary>
    private void AnchorThread()
    {
        var height = Bubbles.IsVisible && _fold is null ? TranscriptScroller.Viewport.Height : 0;

        if (TranscriptContent.MinHeight != height)
        {
            TranscriptContent.MinHeight = height;
        }
    }

    /// <summary>
    /// Whether none of the reading is inside the view — what the fold is for, asked of the drawn
    /// controls rather than of the model (#413).
    /// </summary>
    internal bool ReadingIsAboveTheFold
    {
        get
        {
            var scroller = Scroller;
            var pad = FoldPad;
            var top = pad.Margin.Top;
            var content = Math.Max(0, scroller.Extent.Height - PadOn(pad));

            var from = scroller.Offset.Y;
            var to = from + scroller.Viewport.Height;

            return Math.Min(to, top + content) - Math.Max(from, top) <= 0.5;
        }
    }

    /// <summary>Puts the cursor in the search box.</summary>
    public void FocusSearch()
    {
        SearchInput.Focus();
        SearchInput.SelectAll();
    }

    /// <summary>Puts the cursor in the ask box.</summary>
    public void FocusAsk()
    {
        AskBox.Focus();
        AskBox.SelectAll();
    }

    /// <summary>
    /// Empties the search box and gives the page back, and says whether there was anything to empty —
    /// so Escape with no query in it stays available to whatever else wants the key.
    /// </summary>
    public bool ClearSearch()
    {
        if (_query.Length == 0)
        {
            return false;
        }

        SearchInput.Text = string.Empty;

        // Whichever block the page is drawn in, because on the conversation the flat one is hidden — and a
        // hidden control declines focus, which left it in the search box the Escape was pressed to leave.
        (Tab == PanelTab.Transcript
            ? TranscriptBlocks.FirstOrDefault() ?? Transcript
            : PagePane.Child)?.Focus();

        return true;
    }

    private void OnSurfaceKeyDown(object? sender, KeyEventArgs e)
    {
        // Above the search guard, because folding the page is not a search affordance: a surface with no
        // search box still has a transcript, and the headset is exactly that surface (remediation.md 11, item
        // 14). **This is also where Ctrl+L stops being the ask box's** (#413).
        if (e.Key == Key.L && e.KeyModifiers.HasFlag(KeyModifiers.Control))
        {
            e.Handled = ScrollPastReading();
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

    /// <summary>The mouse's Back button goes back, the same as the breadcrumb (#342). Forward does nothing —
    /// there is no forward level to go to.</summary>
    private void OnSurfacePointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.GetCurrentPoint(this).Properties.PointerUpdateKind == PointerUpdateKind.XButton1Pressed && GoBack())
        {
            e.Handled = true;
        }
    }

    /// <summary>Drops the query without moving focus.</summary>
    private void DropSearch()
    {
        SearchInput.Text = string.Empty;

        // Every built page rather than only the one showing.
        foreach (var page in _pages.Values)
        {
            (page as IFilterablePage)?.Filter(string.Empty);
        }
    }

    /// <summary>The cross inside the box.</summary>
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

        // A new query starts at the top of the page rather than near the last hit: the offset that was being
        // tracked belongs to the string that is no longer being searched for.
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

        // The inlines were rebuilt a line ago, so both things the scroll depends on are stale: the text
        // layout the hit's position is measured against, and the extent the offset is clamped to.
        TranscriptScroller.UpdateLayout();

        ScrollToHit();
    }

    /// <summary>Hands the query to whatever is showing.</summary>
    private void ApplySearch()
    {
        // Here rather than in ShowSearchProgress, which is about the count and the steppers and is therefore
        // only true on a page that highlights.
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
    /// The count and the steppers, which only mean something where a match highlights rather than
    /// filters.
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
        SizeSearchRow();
    }

    /// <summary>Puts the current hit on screen.</summary>
    private void ScrollToHit()
    {
        if (_hit < 0)
        {
            return;
        }

        // Which block holds the hit, and where that block sits in the page.
        var (block, start) = Bubbles.IsVisible
            ? _bubbles
                .Select(bubble => (bubble.Block, bubble.Start))
                .LastOrDefault(bubble => bubble.Start <= _matches[_hit].Start)
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

        // Deliberately not guarded by _scrollingItself.
        TranscriptScroller.Offset = new Vector(
            TranscriptScroller.Offset.X,
            Math.Max(0, where.Y - (TranscriptScroller.Viewport.Height / 3)));
    }

    /// <summary>
    /// What this surface shows, computed from <see cref="Mode"/> and <see cref="Page"/> together.
    /// </summary>
    private void ApplyChrome()
    {
        var full = Mode == PanelMode.Full;
        var transcript = Tab == PanelTab.Transcript;

        Header.IsVisible = full;
        Banners.IsVisible = full;

        // A furnished tab brings its own footer — the settings surface has the storage line, About and the
        // data folder — so the ask line and the provenance line give way to it rather than sitting under it
        // saying nothing about a page with no turns on it.
        AskRow.IsVisible = full && transcript;

        // The provenance line, because it is about the transcript and no other tab has turns on it.
        StatusRow.IsVisible = transcript;

        // The footer shares that rule but not the ask row's: mini and the headset take the ask box away and
        // keep the microphone's state, since continuous capture with no visible state is the thing a
        // Commander is right to distrust.
        Footer.IsVisible = transcript;

        // Mini is "the transcript's tail and the provenance line" and nothing else, so the tabs, the mode
        // control, the breadcrumb and the search box go with the rest of the chrome.
        TabStrip.IsVisible = full;
        TabStripRule.IsVisible = full;
        CrumbRow.IsVisible = full && CrumbRow.Children.Count > 0;

        var modal = ModalPane.Child is not null;

        // Mini reading the Adventures tab (asked for 2026-08-22) or the Routing tab's Plan root (#197) — the
        // rest of Routing keeps drawing its full-size page even at mini's size.
        Control? miniControl = Tab switch
        {
            PanelTab.Adventures => _adventureMini,
            PanelTab.Routing when Nav.RootKeyOf(Tab) == RoutingPages.PlanRoot => _routeMini,
            _ => null,
        };

        MiniPane.Child = miniControl;

        var miniStory = !full && miniControl is not null;

        // The chooser takes the region rather than sitting over it.
        ModalPane.IsVisible = modal;
        MiniPane.IsVisible = miniStory && !modal;
        TranscriptPane.IsVisible = transcript && !modal && !miniStory;
        PagePane.IsVisible = !transcript && !modal && !miniStory;

        // The transcript has no frame of its own: its edge is the window's, and its bar and list sit at the
        // panel's padding.
        ContentPane.BorderThickness = transcript ? default : new Thickness(1);
        PageBar.Margin = transcript ? default : new Thickness(14, 12, 14, 0);

        // The page's own bar.
        ShowSearch();

        // And the ask line goes with them: a chooser has one question in it and a second text box underneath,
        // pointed at the model, is a second question nobody asked.
        AskRow.IsVisible = AskRow.IsVisible && !modal;

        // The transcript's own, which is drawn from a scroll position rather than from the mode — so it has
        // to be re-asked whenever the surface changes kind, not only when it scrolls.
        ShowFollowButton();
    }

    /// <summary>
    /// Draws everything the navigator decides: which tab is checked, which modes the segmented control
    /// offers, what the breadcrumb says, and which page is in the slot.
    /// </summary>
    private static bool MiniShows(PanelTab tab) => tab != PanelTab.Settings;

    /// <summary>Keeps mini on a page mini actually has, and puts back what it took (Phase 51).</summary>
    /// <param name="remember">Whether the tab being left is the one to give back.</param>
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

        // Through the navigator, which declines a tab this surface no longer has - a host can furnish a tab
        // and nothing ever unfurnishes one, so this is belt and braces rather than a case anybody has seen.
        Tab = restore;
    }

    private void ApplyNavigation()
    {
        // Before any of the returns below, because every one of them is a way onto or off the log page and
        // none of them used to say so (#294).
        SettleLogFollow();

        // The Raw switch, put back where the Commander left it (#267).
        if (ApplyRememberedJournalReading())
        {
            return;
        }

        // Which reading each tab is on, kept for the next launch (#268).
        RecordRoots();

        // A surface that was never given a tab cannot be put on one, whether by a stale property, a host that
        // forgot to furnish it, or a hand-edited state.
        var tab = Tab;

        if (tab != PanelTab.Transcript && !_builders.ContainsKey(tab))
        {
            Tab = PanelTab.Transcript;
            return;
        }

        // And mini refuses a tab it has no reading of, whatever moved the navigator — a press, a spoken
        // phrase, or a switch (Phase 51).
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

        // What to put back when this one is left.
        if (tab != _showing)
        {
            _lastTab = _showing;
            _showing = tab;
        }

        // The page being read has changed - a different tab, or a different mode of the same one.
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

        // A chooser takes the content region, over whichever tab it was opened from - so the tab underneath
        // keeps its state and comes back to where it was rather than to its root.
        ModalPane.Child = Nav.Modal ? Modal(Nav.Trail[^1]) : null;

        ApplyChrome();

        if (tab != PanelTab.Transcript)
        {
            BuildPageOnce(tab);

            // After the page is in the pane, because whether a query would do anything is a question for the
            // page and ApplyChrome above ran before there was one to ask.
            ShowSearch();
            return;
        }

        // Read when the page is opened.
        if (Page == TranscriptPage.Log)
        {
            Reading = ReadLogAsync();
            return;
        }

        // Rebuilt when the page is opened, for the reason the log is read then: the events are already in
        // memory, but projecting four thousand of them into lines is not worth doing per tick for a page
        // nobody is looking at (#51).
        if (Page is TranscriptPage.Journal or TranscriptPage.RawJournal)
        {
            Model?.RefreshJournal();
        }

        DrawTranscript();
    }

    /// <summary>The readings row, rebuilt from the current tab's roots.</summary>
    private void DrawModes()
    {
        // Raw Journal is a root the navigator knows and the picker does not list (#231).
        var roots = Nav.Roots(Nav.Tab).Where(root => root.Key != RawJournalRoot).ToList();

        // And a Commander who is *on* raw still has the journal selected, for the same reason.
        var showing = Nav.RootKeyOf(Nav.Tab);
        showing = showing == RawJournalRoot ? JournalRoot : showing;

        ModePicker.IsVisible = !OutputOnly && roots.Count > 1 && Nav.AtRoot;

        ShowPageBar();
        ShowLogSettingsStrip();

        // Ahead of the early return below, which answers only the picker: Fleet's Ships root has one reading
        // and never shows the picker, but a Commander parked on Raw Journal must not see its toggle survive
        // the move there anyway (#277).
        DrawRawToggle();

        if (!ModePicker.IsVisible)
        {
            return;
        }

        // Rebuilt only when the readings themselves changed, not on every navigation.
        var words = roots.Select(root => root.Word).ToList();

        if (!ModeReadings.ItemsSource.SequenceEqual(words))
        {
            _settingMode = true;

            try
            {
                ModeReadings.ItemsSource = words;
            }
            finally
            {
                _settingMode = false;
            }
        }

        var index = roots.FindIndex(root => root.Key == showing);

        // Written under the guard, because this runs on every navigation — including the one OnModeChanged
        // just caused.
        _settingMode = true;

        try
        {
            ModeReadings.SelectedIndex = index < 0 ? 0 : index;
        }
        finally
        {
            _settingMode = false;
        }
    }

    /// <summary>
    /// The journal's Raw toggle: shown on the journal reading, on a surface that was handed the raw one
    /// (#231).
    /// </summary>
    private void DrawRawToggle()
    {
        var journal = Page is TranscriptPage.Journal or TranscriptPage.RawJournal;

        // Only where a host furnished the raw reading.
        var furnished = Nav.Roots(PanelTab.Transcript).Any(root => root.Key == RawJournalRoot);

        // The box, not the switch: the label lives beside the knob, and hiding one without the other would
        // leave a word floating in the bar.
        RawToggleBox.IsVisible = journal && furnished && Nav.AtRoot && !OutputOnly
            && Nav.Tab == PanelTab.Transcript;

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

    /// <summary>The Commander asked for the file's own JSON, or asked to go back to sentences.</summary>
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
    /// Whether the selection is being written by <see cref="DrawModes"/> rather than chosen by the
    /// Commander.
    /// </summary>
    private bool _settingMode;

    /// <summary>The Commander pressed a reading.</summary>
    private void OnModeChanged(object? sender, EventArgs e) => ChooseReading(ModeReadings.SelectedIndex);

    /// <summary>Goes to the reading at <paramref name="index"/>, unless the picker is being written rather than pressed.</summary>
    private void ChooseReading(int index)
    {
        if (_settingMode)
        {
            return;
        }

        var roots = Nav.Roots(Nav.Tab);

        if (index < 0 || index >= roots.Count)
        {
            return;
        }

        // Through the navigator's own event, so a reading reached by a press and one reached by a spoken
        // phrase are one path rather than two that have to agree.
        Nav.SelectRoot(roots[index].Key);
    }

    /// <summary>The trail, rebuilt.</summary>
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
                    this.GetResourceObservable(Theming.ThemeManager.GreyKey));

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

    /// <summary>Reads the log, saying so on the tab if it takes long enough to be worth saying.</summary>
    private void SettleLogFollow() =>
        FollowLogFile(Tab == PanelTab.Transcript && Page == TranscriptPage.Log);

    /// <summary>Whether the log is being followed.</summary>
    internal bool FollowingLog => _logTicker?.IsEnabled == true;

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

    /// <summary>One tick of the log page's refresh.</summary>
    internal async Task RefreshLogNow() => await KeepLogUp();

    private async Task KeepLogUp()
    {
        // A read still in flight, a page that has moved on, or nothing to read into: all three are "not now"
        // rather than errors, and the next tick asks again.
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

            // Checked again after the await: a Commander who left the page while the read was in flight must
            // not have it redrawn under whatever they went to.
            if (Page != TranscriptPage.Log || Tab != PanelTab.Transcript)
            {
                return;
            }

            bound.ShowLog(text);
            DrawTranscript();

            // Only if they are following, which ScrollToEnd already decides.
            ScrollToEnd();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
        // The file is being rolled, or something else has it.
        }
    }

    /// <summary>The log page's refresh while it is open.</summary>
    private DispatcherTimer? _logTicker;

    private async Task ReadLogAsync()
    {
        // The mode button, which may not exist: it is hidden in mini and below a root, and the log can be the
        // reading a surface is on in either.
        if (!ModePicker.IsVisible)
        {
            // Read off this thread, tell the page on it.
            if (_bound is { } bound)
            {
                bound.ShowLog(await Task.Run(bound.ReadLog));
            }

            DrawTranscript();
            ScrollToEnd();
            return;
        }

        // Both halves inside the busy window (remediation.md 10, item 5).
        await Controls.Busy.While(ModeReadings, _logBusy, async () =>
        {
            // The same split as above: the file work on a worker, the property set here.
            if (_bound is { } bound)
            {
                bound.ShowLog(await Task.Run(bound.ReadLog));
            }

            // After the read rather than before, or the page draws the log it had last time and then redraws
            // — a visible flicker on the one page opened to read something.
            DrawTranscript();

            // At the end, because a log is read newest-first and this page has always opened at the top of
            // it.
            ScrollToEnd();
        });
    }

    /// <summary>Puts a furnished tab's drill strip in the pane, building it the first time.</summary>
    private void BuildPageOnce(PanelTab tab)
    {
        if (!_pages.TryGetValue(tab, out var page))
        {
            if (!_builders.TryGetValue(tab, out var build))
            {
                return;
            }

            // A drill strip rather than the page itself, so drilling in and reflowing are one mechanism for
            // every tab at once: a tab with no levels is a strip of one pane, which is exactly the page, and
            // a tab that grows levels needs nothing added here.
            page = new DrillView(Nav, tab, build);

            // The strip's first draw runs on attachment to the visual tree, which on a cold start happens
            // after this method returns and ShowSearch() has already asked whether the page filters.
            page.Drawn += (_, _) => ShowSearch();

            // A strip built after the host furnished this surface still gets handles - the tabs are built on
            // first sight rather than up front, so most of them arrive here (Phase 55).
            if (_paneWidths is not null)
            {
                page.EnableDrag(_paneWidths);
            }

            _pages[tab] = page;
        }

        PagePane.Child = page;
    }

    /// <summary>
    /// Writes the current page into the transcript block, as one run per stretch that is drawn the same
    /// way.
    /// </summary>
    private void DrawTranscript() => DrawTranscript(appended: false);

    /// <summary>A line arriving, which is the one redraw that can take the short path.</summary>
    private void OnTranscriptAppended() => DrawTranscript(appended: true);

    private void OnBubblesPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.GetCurrentPoint(Bubbles).Properties.IsLeftButtonPressed)
        {
            BeginBubbleDrag(e.GetPosition(Bubbles));
        }
    }

    private void OnBubblesPointerMoved(object? sender, PointerEventArgs e)
    {
        if (_dragAnchor >= 0 && e.GetCurrentPoint(Bubbles).Properties.IsLeftButtonPressed)
        {
            ContinueBubbleDrag(e.GetPosition(Bubbles));
        }
    }

    /// <summary>Where a drag on the conversation page begins, character-exact, so a later move across a
    /// bubble boundary picks the selection up mid-turn rather than snapping to its edge. <paramref
    /// name="point"/> is in <see cref="Bubbles"/>'s own coordinate space (#114).</summary>
    internal void BeginBubbleDrag(Point point)
    {
        if (HitBubble(point) is not { } hit)
        {
            return;
        }

        _dragAnchor = hit.Index;
        _dragAnchorOffset = hit.Offset;
        _span = null;

        // A fresh drag starts empty everywhere but where it lands.
        for (var i = 0; i < _bubbles.Count; i++)
        {
            if (i != hit.Index)
            {
                _bubbles[i].Block.SelectionStart = 0;
                _bubbles[i].Block.SelectionEnd = 0;
            }
        }
    }

    /// <summary>
    /// Extends the selection across every bubble a drag has crossed. A drag that never leaves the
    /// bubble it began in is left to that block's own selection handling — this only takes over once
    /// the pointer has crossed a boundary. <paramref name="point"/> is in <see cref="Bubbles"/>'s own
    /// coordinate space (#114).
    /// </summary>
    internal void ContinueBubbleDrag(Point point)
    {
        if (_dragAnchor < 0 || HitBubble(point) is not { } hit)
        {
            return;
        }

        if (hit.Index == _dragAnchor && _span is null)
        {
            return;
        }

        if (hit.Index == _dragAnchor)
        {
            // Back inside the bubble the drag began in, having left it and returned.
            _bubbles[_dragAnchor].Block.SelectionStart = _dragAnchorOffset;
            _bubbles[_dragAnchor].Block.SelectionEnd = hit.Offset;

            for (var i = 0; i < _bubbles.Count; i++)
            {
                if (i != _dragAnchor)
                {
                    _bubbles[i].Block.SelectionStart = 0;
                    _bubbles[i].Block.SelectionEnd = 0;
                }
            }

            _span = null;
            return;
        }

        var down = hit.Index > _dragAnchor;
        var low = Math.Min(_dragAnchor, hit.Index);
        var high = Math.Max(_dragAnchor, hit.Index);

        for (var i = 0; i < _bubbles.Count; i++)
        {
            var block = _bubbles[i].Block;

            if (i < low || i > high)
            {
                block.SelectionStart = 0;
                block.SelectionEnd = 0;
                continue;
            }

            var length = block.Inlines?.Text?.Length ?? 0;

            (block.SelectionStart, block.SelectionEnd) = i switch
            {
                _ when i == _dragAnchor && down => (_dragAnchorOffset, length),
                _ when i == _dragAnchor => (0, _dragAnchorOffset),
                _ when i == hit.Index && down => (0, hit.Offset),
                _ when i == hit.Index => (hit.Offset, length),
                _ => (0, length),
            };
        }

        _span = (low, high);
    }

    /// <summary>A bubble a point falls over, and where in its text — clamped to the nearest bubble and
    /// character when the point lies past every edge, so a drag that reaches past the transcript still
    /// resolves to something (#114).</summary>
    private (int Index, int Offset)? HitBubble(Point point)
    {
        if (_bubbles.Count == 0)
        {
            return null;
        }

        var index = 0;

        for (var i = 1; i < _bubbles.Count; i++)
        {
            var top = _bubbles[i].Block.TranslatePoint(new Point(0, 0), Bubbles)?.Y ?? double.MaxValue;

            if (point.Y < top)
            {
                break;
            }

            index = i;
        }

        var block = _bubbles[index].Block;
        var length = block.Inlines?.Text?.Length ?? 0;

        if (length == 0)
        {
            return (index, 0);
        }

        var origin = block.TranslatePoint(new Point(0, 0), Bubbles) ?? default;
        var local = point - origin;
        var offset = block.TextLayout.HitTestPoint(local).TextPosition;

        return (index, Math.Clamp(offset, 0, length));
    }

    /// <summary>Copy follows the selection (remediation.md 14, item 9).</summary>
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

    /// <summary>The journal, as a list of sentences with the selected event's fields beside it (#51).</summary>
    /// <param name="appended">
    /// Whether this redraw is a line arriving rather than the page, the query or the theme changing.
    /// </param>
    private void DrawJournal()
    {
        if (Model is not { } model)
        {
            JournalList.ItemsSource = null;
            JournalDetail.Text = string.Empty;
            return;
        }

        // The fields fold away, which is the Commander's amendment to the design and what keeps this reading
        // usable in one narrow column.
        JournalDetailScroller.IsVisible = model.JournalDetail;
        JournalSplitter.IsVisible = model.JournalDetail;

        // Filtered, which is this reading's answer to the search box (#232).
        var shown = (_query.Length == 0
            ? model.Journal
            : model.Journal.Where(entry =>
                entry.Line.Contains(_query, StringComparison.OrdinalIgnoreCase)
                || entry.Kind.Contains(_query, StringComparison.OrdinalIgnoreCase))).ToList();

        JournalList.ItemsSource = shown;

        // Against the filtered list rather than the whole one.
        var selected = model.JournalSelected >= 0 && model.JournalSelected < model.Journal.Count
            ? shown.IndexOf(model.Journal[model.JournalSelected])
            : -1;

        JournalList.SelectedIndex = selected;

        JournalDetail.Text = selected >= 0 ? model.JournalDetailText : string.Empty;

        ShowJournalCount(shown.Count, model.Journal.Count);
    }

    /// <summary>
    /// A journal row: the line, with a copy glyph beside it where the event names a system (#158).
    /// Built for a null entry too — Avalonia clears a recycled container's content before it is reused.
    /// </summary>
    private Control JournalRow(D47.Core.Journal.JournalEntry? entry)
    {
        var text = new TextBlock { Text = entry?.Line ?? string.Empty };

        if (entry?.StarSystem is not { Length: > 0 } system || _copy is not { } copy)
        {
            return text;
        }

        return new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 6,
            Children = { text, Controls.CopyWord.For(system, copy) },
        };
    }

    /// <summary>
    /// What the search box says on a reading that filters (#232): how many lines are left, not which of
    /// them is current.
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
        SizeSearchRow();
    }

    /// <summary>A line was chosen, so the fields beside it change.</summary>
    private void OnJournalSelected(object? sender, Avalonia.Controls.SelectionChangedEventArgs e)
    {
        if (Model is not { } model || JournalList.SelectedIndex < 0)
        {
            return;
        }

        model.JournalSelected = JournalList.SelectedIndex;
        JournalDetail.Text = model.JournalDetailText;
    }

    /// <summary>Whether the fields are drawn beside the list.</summary>
    public void ShowJournalDetail(bool shown)
    {
        if (Model is { } model)
        {
            model.JournalDetail = shown;
            DrawTranscript();
        }
    }

    /// <summary>Whether the kinds nobody reads are listed.</summary>
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

        // The journal is a shape of its own - a list and the fields beside it - so it takes the pane rather
        // than a presentation inside the shared scroller (#51).
        var listed = Page == TranscriptPage.Journal;

        // A fold belongs to the reading it was pressed on, so leaving that reading gives its empty space back
        // (#413).
        ApplyFold(reassert: false);

        JournalPane.IsVisible = listed;
        TranscriptScroller.IsVisible = !listed;

        if (listed)
        {
            DrawJournal();
            return;
        }

        // Which of the two presentations this page gets.
        var bubbled = Page == TranscriptPage.Conversation;

        Transcript.IsVisible = !bubbled;
        Bubbles.IsVisible = bubbled;
        EmptyConversation.IsVisible = false;
        AnchorThread();

        if (_bound is null)
        {
            EmptyConversation.IsVisible = bubbled;
            Transcript.Inlines?.Clear();
            ClearBubbles();
            _matches = [];
            _hit = -1;
            ShowSearchProgress(_query.Length > 0);
            return;
        }

        // Unframed for the conversation, because the blank line and the "> " a flat page puts in front of the
        // Commander's turn are that page's way of saying who spoke, and this one says it with a side and a
        // colour instead.
        var messages = bubbled
            ? Turns(Drawn(_bound.Segments(Page, framed: false), Page))
            : [new DrawnTurn(
                TranscriptVoice.Ship, Marker: false, Drawn(_bound.Segments(Page), Page),
                Speaker: "D47", SourceKey: null, Time: default)];

        // Matched against the page's text rather than against the controls, so the hits are the same set
        // whether the page has been drawn yet or not — and so the current one can be re-resolved from its
        // offset every time the log grows underneath it.
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
            EmptyConversation.IsVisible = messages.Count == 0;
        }
        else
        {
            ClearBubbles();
            Fill(Transcript, messages[0], at: 0);
        }

        ShowSearchProgress(_query.Length > 0);
    }

    /// <summary>
    /// The conversation, turn by turn: the Commander's on the right, the ship's on the left, and the
    /// panel's own notes across the middle (asked for 2026-08-22).
    /// </summary>
    private void DrawBubbles(IReadOnlyList<DrawnTurn> turns, bool appended)
    {
        // The turns as comparable things each, because a record holding a list compares the list by
        // reference and would call every redraw a change.
        var shape = turns
            .Select(turn => (
                turn.Voice,
                turn.Marker,
                Text: string.Concat(turn.Segments.Select(segment => segment.Text)),
                Direction: string.Join(' ', turn.Direction)))
            .ToArray();

        // One snapshot of what d47 already knows, shared by every chip this call draws, so a name that
        // arrives mid-draw does not make one turn's chips disagree with another's (#159).
        var known = _systemsInPlay?.Snapshot();
        var current = _currentSystem?.Invoke();

        if (appended
            && _query.Length == 0
            && shape.Length > 0
            && shape.Length == _bubbles.Count
            && shape.Length == _shape.Count

            // A proposal card's buttons live on the bubble this fast path never rebuilds — settling the
            // newest one has to go through the full redraw below to lose them (#277).
            && turns[^1].Kind != TranscriptRunKind.Proposal

            // The head is not redrawn here, so a delivery tag arriving mid-reply needs the full redraw.
            && shape[^1].Direction == _shape[^1].Direction
            && shape.Take(shape.Length - 1).SequenceEqual(_shape.Take(_shape.Count - 1)))
        {
            Fill(_bubbles[^1].Block, turns[^1], _bubbles[^1].Start);
            FillStrip(_bubbles[^1].Strip, turns[^1], known, current);
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
                FontFamily = Theming.Fonts.ProseFamily,
                FontSize = Transcript.FontSize,
                TextWrapping = TextWrapping.Wrap,

                LineHeight = Transcript.FontSize * 1.5,

                // The menu the block beside this one declares, not a second copy of it.
                ContextMenu = Transcript.ContextMenu,
            };

            Watch(block);
            Fill(block, turn, at);

            var strip = turn.Marker || known is null || _copy is null ? null : new WrapPanel
            {
                Margin = new Thickness(0, mini ? 2 : 4, 0, 0),
                ItemSpacing = mini ? 6 : 8,
                LineSpacing = 4,
            };

            FillStrip(strip, turn, known, current);

            Bubbles.Children.Add(Bubble(block, turn, strip));
            _bubbles.Add((block, at, strip));

            at += turn.Segments.Sum(segment => segment.Text.Length);
        }

        _shape = shape;
    }

    /// <summary>
    /// The strip's chips, one per distinct system name <paramref name="turn"/> mentions, in first-appearance
    /// order (#159): a slab tile with the name in uppercase Saira, cyan for <paramref name="current"/> and
    /// A for any other, and its copy button beside it.
    /// </summary>
    private void FillStrip(WrapPanel? strip, DrawnTurn turn, IReadOnlyCollection<string>? known, string? current)
    {
        if (strip is null || known is null || _copy is not { } copy)
        {
            return;
        }

        strip.Children.Clear();

        var text = string.Concat(turn.Segments.Select(segment => segment.Text));
        var names = new List<string>();

        foreach (var hit in D47.Core.Knowledge.SystemNameFinder.Find(text, known))
        {
            if (!names.Contains(hit.Name, StringComparer.OrdinalIgnoreCase))
            {
                names.Add(hit.Name);
            }
        }

        foreach (var name in names)
        {
            var label = new TextBlock
            {
                Text = name.ToUpperInvariant(),
                FontFamily = ChromeFamily,
                FontSize = Theming.TypeScale.Tip,
                FontWeight = FontWeight.Medium,
                VerticalAlignment = VerticalAlignment.Center,
            };

            label.Bind(
                TextBlock.ForegroundProperty,
                this.GetResourceObservable(string.Equals(name, current, StringComparison.OrdinalIgnoreCase)
                    ? Theming.ThemeManager.CyanKey
                    : Theming.ThemeManager.AKey));

            var chip = new Border { MinHeight = 32, Padding = new Thickness(12, 0), Child = label };

            chip.Bind(Border.BackgroundProperty, this.GetResourceObservable(Theming.ThemeManager.SlabKey));

            strip.Children.Add(new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 2,
                Children = { chip, Controls.CopyWord.For(name, copy) },
            });
        }

        strip.IsVisible = strip.Children.Count > 0;
    }

    /// <summary>
    /// One turn, dressed as a message: the ship's and every in-ship speaker's on the left behind a 3px A bar,
    /// the Commander's on the right behind a 3px cyan bar on a cyan ground. Each is at most
    /// <see cref="TurnShare"/> of the list's width, its text left-aligned. Hovering lays slab under it.
    /// </summary>
    private Control Bubble(SelectableTextBlock block, DrawnTurn turn, WrapPanel? strip)
    {
        if (turn.Marker)
        {
            block.TextAlignment = TextAlignment.Center;

            return block;
        }

        block.Bind(TextBlock.ForegroundProperty, this.GetResourceObservable(Theming.ThemeManager.WhiteKey));
        block.HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Left;
        block.TextAlignment = TextAlignment.Left;

        var content = new StackPanel { Spacing = 5, Children = { Head(turn), block } };

        // The buttons only while the proposal is still waiting — looked up live rather than trusted from
        // whatever this run's own tag last said, so a settlement this surface missed still takes them away
        // (#277).
        if (turn.Kind == TranscriptRunKind.Proposal
            && turn.ProposalId is { Length: > 0 } proposalId
            && IsProposalPending(proposalId))
        {
            content.Children.Add(ProposalButtons(proposalId));
        }

        if (strip is not null)
        {
            content.Children.Add(strip);
        }

        var commander = turn.Voice == TranscriptVoice.Commander;

        var row = new TurnBorder
        {
            Child = content,
            BorderThickness = commander ? new Thickness(0, 0, 3, 0) : new Thickness(3, 0, 0, 0),
            Padding = new Thickness(12, 10, 14, 12),
            HorizontalAlignment = commander
                ? Avalonia.Layout.HorizontalAlignment.Right
                : Avalonia.Layout.HorizontalAlignment.Left,
            MaxWidth = TurnWidth(),
        };

        row.Bind(
            Border.BorderBrushProperty,
            this.GetResourceObservable(commander ? Theming.ThemeManager.CyanKey : Theming.ThemeManager.AKey));

        void Rest()
        {
            if (commander)
            {
                row.Bind(Border.BackgroundProperty, this.GetResourceObservable(Theming.ThemeManager.CyanGroundKey));
            }
            else
            {
                row.Background = Brushes.Transparent;
            }
        }

        Rest();

        row.PointerEntered += (_, _) =>
            row.Bind(Border.BackgroundProperty, this.GetResourceObservable(Theming.ThemeManager.SlabKey));

        row.PointerExited += (_, _) => Rest();

        return row;
    }

    /// <summary>The widest a turn is, as a share of the list's width.</summary>
    internal const double TurnShare = 0.80;

    /// <summary>A turn's widest at the list's current width, or no cap before the list has one.</summary>
    private double TurnWidth() =>
        Bubbles.Bounds.Width > 0 ? Math.Floor(Bubbles.Bounds.Width * TurnShare) : double.PositiveInfinity;

    /// <summary>Recaps every turn when the list's width changes.</summary>
    private void OnBubblesResized(object? sender, SizeChangedEventArgs e)
    {
        if (e.WidthChanged)
        {
            var width = TurnWidth();

            foreach (var row in Bubbles.Children.OfType<TurnBorder>())
            {
                row.MaxWidth = width;
            }
        }
    }

    /// <summary>
    /// Who spoke, what it was about, how it was delivered and when — atop every turn but the panel's own note
    /// (#276). The time follows the last tag after <see cref="HeadTimeGap"/>; the rest wraps when the turn is
    /// narrow.
    /// </summary>
    private Control Head(DrawnTurn turn)
    {
        var said = new WrapPanel { ItemSpacing = 10, LineSpacing = 2, VerticalAlignment = VerticalAlignment.Center };

        said.Children.Add(SpeakerName(turn));

        if (turn.SourceKey is { Length: > 0 } key)
        {
            said.Children.Add(Faint(key));
        }

        foreach (var direction in turn.Direction)
        {
            said.Children.Add(Faint($"[{direction}]"));
        }

        var time = Faint(turn.Time.ToString("HH:mm", System.Globalization.CultureInfo.InvariantCulture));

        time.Margin = new Thickness(HeadTimeGap, 0, 0, 0);
        time.VerticalAlignment = VerticalAlignment.Top;
        Grid.SetColumn(time, 1);

        return new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,Auto"),
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Left,
            Children = { said, time },
        };
    }

    /// <summary>The gap between a turn head's last tag and its time.</summary>
    internal const double HeadTimeGap = 12;

    /// <summary>
    /// The speaker's name in uppercase Saira 13/600, in the colour of the turn's bar: cyan for the Commander,
    /// A for the ship and every other in-ship voice. A Commander turn carries the Commander's name when one is
    /// known.
    /// </summary>
    private TextBlock SpeakerName(DrawnTurn turn)
    {
        var said = turn.Voice == TranscriptVoice.Commander
                   && turn.Speaker == "CMDR"
                   && _commanderName?.Invoke() is { Length: > 0 } commander
            ? $"CMDR {commander}"
            : turn.Speaker;

        var name = new TextBlock
        {
            Text = said.ToUpperInvariant(),
            FontFamily = ChromeFamily,
            FontSize = Theming.TypeScale.Small,
            FontWeight = FontWeight.SemiBold,
            LetterSpacing = 1.04,
            VerticalAlignment = VerticalAlignment.Center,
        };

        name.Bind(
            TextBlock.ForegroundProperty,
            this.GetResourceObservable(turn.Voice == TranscriptVoice.Commander
                ? Theming.ThemeManager.CyanKey
                : Theming.ThemeManager.AKey));

        return name;
    }

    /// <summary>A turn's intent, delivery or time: JetBrains Mono 12 in grey, never wrapped.</summary>
    private TextBlock Faint(string text)
    {
        var label = new TextBlock
        {
            Text = text,
            FontFamily = MonospaceFamily,
            FontSize = Theming.TypeScale.Caption,
            TextWrapping = TextWrapping.NoWrap,
            VerticalAlignment = VerticalAlignment.Center,
        };

        label.Bind(TextBlock.ForegroundProperty, this.GetResourceObservable(Theming.ThemeManager.GreyKey));

        return label;
    }

    /// <summary>The face for times and keys — readouts, which is all monospace is kept for.</summary>
    private static readonly FontFamily MonospaceFamily = new(Theming.Fonts.MonoFamily);

    private static readonly FontFamily ChromeFamily = new(Theming.Fonts.ChromeFamily);

    /// <summary>Whether a proposal is still waiting on the Commander, read from the store rather than a run's own say-so (#277).</summary>
    private bool IsProposalPending(string proposalId) =>
        _checklists?.Proposals.Pending.Any(
            proposal => string.Equals(proposal.Id, proposalId, StringComparison.OrdinalIgnoreCase)) ?? false;

    /// <summary>
    /// The thread's own Accept and Decline, reaching the same <see cref="D47.Core.Checklists.ChecklistService"/>
    /// the Checklist page's card does — settling either settles both, through
    /// <see cref="D47.Core.Checklists.ChecklistService.ProposalSettled"/> (#277).
    /// </summary>
    private Control ProposalButtons(string proposalId) =>
        ProposalActions.Build(
            () => { _checklists?.Accept(proposalId); },
            () => { _checklists?.Decline(proposalId); },
            "or say \"accept the proposal\"");

    /// <summary>One turn's text into one block, as runs.</summary>
    private void Fill(SelectableTextBlock block, DrawnTurn turn, int at)
    {
        var inlines = block.Inlines ??= [];
        inlines.Clear();

        // A message body is prose, so machine text inside one switches face; a flat page is monospace
        // throughout and marks a code span with a ground instead.
        var prose = !turn.Marker && Page == TranscriptPage.Conversation;

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
                    if (prose)
                    {
                        run.FontFamily = MonospaceFamily;
                        run.FontSize = Theming.TypeScale.Tip;
                    }
                    else
                    {
                        run.Bind(
                            Avalonia.Controls.Documents.TextElement.BackgroundProperty,
                            this.GetResourceObservable(Theming.ThemeManager.Line2Key));
                    }
                }

                if (segment.Marker)
                {
                    run.Bind(
                        Avalonia.Controls.Documents.TextElement.ForegroundProperty,
                        this.GetResourceObservable(Theming.ThemeManager.AKey));
                    run.FontWeight = FontWeight.SemiBold;
                }

                if (match >= 0)
                {
                    // Every hit is marked and the current one is accented, which is what makes stepping
                    // legible: the count says where you are in the set and the colour says which one of them
                    // you are looking at.
                    run.Bind(
                        Avalonia.Controls.Documents.TextElement.BackgroundProperty,
                        this.GetResourceObservable(match == _hit
                            ? Theming.ThemeManager.AKey
                            : Theming.ThemeManager.LineKey));

                    if (match == _hit)
                    {
                        run.Bind(
                            Avalonia.Controls.Documents.TextElement.ForegroundProperty,
                            this.GetResourceObservable(Theming.ThemeManager.BgKey));
                    }
                }

                inlines.Add(run);
            }

            at += segment.Text.Length;
        }
    }

    /// <summary>
    /// Every run this surface is currently drawing the transcript with, in page order — the one block's
    /// worth on a flat page, one bubble's worth at a time on the conversation.
    /// </summary>
    internal IEnumerable<Run> TranscriptRuns =>
        Bubbles.IsVisible
            ? _bubbles.SelectMany(bubble => bubble.Block.Inlines?.OfType<Run>() ?? [])
            : Transcript.Inlines?.OfType<Run>() ?? [];

    /// <summary>What this surface is showing, as text.</summary>
    private void Reread()
    {
        // A binding handed over is an arrival on whatever page this surface is already on, so the follow is
        // settled here too — this route read the log once and never followed it (#294).
        SettleLogFollow();

        if (Tab == PanelTab.Transcript && Page == TranscriptPage.Log)
        {
            Reading = ReadLogAsync();
            return;
        }

        DrawTranscript();
    }

    /// <summary>The log read that is in flight, or a completed task (GitHub issue 43).</summary>
    internal Task Reading { get; private set; } = Task.CompletedTask;

    internal string TranscriptShown => string.Concat(TranscriptRuns.Select(run => run.Text));

    /// <summary>
    /// The blocks the transcript is drawn in — one on a flat page, one per turn on the conversation.
    /// </summary>
    internal IReadOnlyList<SelectableTextBlock> TranscriptBlocks =>
        Bubbles.IsVisible ? [.. _bubbles.Select(bubble => bubble.Block)] : [Transcript];

    private void ClearBubbles()
    {
        Bubbles.Children.Clear();
        _bubbles.Clear();
        _shape = [];
        _dragAnchor = -1;
        _span = null;
    }

    /// <summary>
    /// A page's segments with the model's markdown read: the markers gone and what they meant carried
    /// as a style (Phase 19, and the transcript drawing <c>**A-rate FSD**</c> literally for as long as
    /// it has existed).
    /// </summary>
    private static IReadOnlyList<DrawnSegment> Drawn(
        IReadOnlyList<TranscriptSegment> segments,
        TranscriptPage page) =>
        // Raw Journal joins the log here, and it is the more important of the two: a journal carries other
        // players' text verbatim, and JSON is full of asterisks and underscores.
        page is TranscriptPage.Log or TranscriptPage.RawJournal
            ? [.. segments.Select(segment => new DrawnSegment(
                segment.Text, segment.Marker, segment.Voice, MarkupStyle.None,
                segment.Speaker ?? "D47", segment.SourceKey, segment.Time, segment.Kind, segment.ProposalId))]
            : [.. segments.SelectMany(segment => Spans(segment, page))];

    /// <summary>
    /// One segment's markup spans. On the conversation, delivery direction such as <c>[calm]</c> is taken out
    /// of the drawn text and carried on the span for the turn's head; the text sent to speech is not this text.
    /// </summary>
    private static IEnumerable<DrawnSegment> Spans(TranscriptSegment segment, TranscriptPage page)
    {
        var direction = page == TranscriptPage.Conversation && !segment.Marker
            ? D47.Core.Audio.AudioTags.In(segment.Text)
            : [];

        foreach (var span in TranscriptMarkup.Parse(segment.Text))
        {
            var text = direction.Count > 0 ? D47.Core.Audio.AudioTags.Remove(span.Text) : span.Text;

            if (text.Length == 0)
            {
                continue;
            }

            yield return new DrawnSegment(
                text, segment.Marker, segment.Voice, span.Style,
                segment.Speaker ?? "D47", segment.SourceKey, segment.Time, segment.Kind, segment.ProposalId,
                direction);
        }
    }

    /// <summary>
    /// The page's segments gathered into turns: consecutive stretches from one side, with the blank
    /// lines between them taken off. A turn also breaks on a new speaker or source, so two callouts
    /// spoken back to back stay two bubbles even when both are the ship's own voice.
    /// </summary>
    private static IReadOnlyList<DrawnTurn> Turns(IReadOnlyList<DrawnSegment> segments)
    {
        var gathered = new List<(
            TranscriptVoice Voice, bool Marker, string Speaker, string? SourceKey, DateTimeOffset Time,
            TranscriptRunKind Kind, string? ProposalId, List<DrawnSegment> Segments)>();

        foreach (var segment in segments)
        {
            if (gathered is [.., var last]
                && last.Voice == segment.Voice
                && last.Marker == segment.Marker
                && last.Speaker == segment.Speaker
                && last.SourceKey == segment.SourceKey
                && last.Kind == segment.Kind
                && last.ProposalId == segment.ProposalId)
            {
                last.Segments.Add(segment);
                continue;
            }

            gathered.Add((
                segment.Voice, segment.Marker, segment.Speaker, segment.SourceKey, segment.Time,
                segment.Kind, segment.ProposalId, [segment]));
        }

        return
        [
            .. gathered
                .Select(turn => new DrawnTurn(
                    turn.Voice, turn.Marker, Trimmed(turn.Segments), turn.Speaker, turn.SourceKey, turn.Time,
                    turn.Kind, turn.ProposalId)
                {
                    Direction =
                    [
                        .. turn.Segments
                            .SelectMany(segment => segment.Direction ?? [])
                            .Distinct(StringComparer.OrdinalIgnoreCase),
                    ],
                })
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
    /// One segment, cut at the boundaries of any hits inside it, each piece carrying the index of the
    /// hit it belongs to or -1.
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

    /// <summary>Re-asserts the page once this view is actually on screen.</summary>
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);

        _root = e.RootVisual as Interactive;
        _root?.AddHandler(KeyDownEvent, OnSurfaceKeyDown, RoutingStrategies.Tunnel);
        _root?.AddHandler(PointerPressedEvent, OnSurfacePointerPressed, RoutingStrategies.Tunnel);

        _topLevel = TopLevel.GetTopLevel(this);

        if (_topLevel is not null)
        {
            _topLevel.ScalingChanged += OnScalingChanged;
        }

        DrawScanlines();
        ApplyNavigation();

        // PROBE-TABCLIP: temporary, remove before commit.
        Avalonia.Threading.DispatcherTimer.RunOnce(ProbeTabClip, TimeSpan.FromSeconds(4));
    }

    // PROBE-TABCLIP: temporary, remove before commit.
    private void ProbeTabClip()
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"=== {DateTime.Now:O} scaling={_topLevel?.RenderScaling} root={_topLevel?.GetType().Name}");
        foreach (var (tab, button) in _tabs)
        {
            if (!button.IsVisible)
            {
                continue;
            }

            sb.AppendLine($"tab {tab} checked={button.IsChecked} content='{button.Content}' bounds={button.Bounds} fs={button.FontSize} fw={button.FontWeight} ls={button.LetterSpacing} ff={button.FontFamily}");
            foreach (var v in button.GetVisualDescendants())
            {
                sb.AppendLine($"   {v.GetType().Name}#{(v as Control)?.Name} bounds={v.Bounds} clip={v.Clip is not null} clipBounds={v.ClipToBounds} effect={v.Effect?.GetType().Name} visible={v.IsVisible} zi={v.ZIndex}");
                if (v is TextBlock t)
                {
                    var l = t.TextLayout;
                    sb.AppendLine($"      text='{t.Text}' inlines={t.Inlines?.Count} fs={t.FontSize} fw={t.FontWeight} ls={t.LetterSpacing} wrap={t.TextWrapping} trim={t.TextTrimming} desired={t.DesiredSize} layoutW={l.Width} layoutWT={l.WidthIncludingTrailingWhitespace} maxW={l.MaxWidth} lines={l.TextLines.Count}");
                    foreach (var line in l.TextLines)
                    {
                        sb.AppendLine($"      line start={line.FirstTextSourceIndex} len={line.Length} width={line.Width} wt={line.WidthIncludingTrailingWhitespace} trimmed={line.HasCollapsed} overflow={line.HasOverflowed} runs={line.TextRuns.Count}");
                        foreach (var run in line.TextRuns)
                        {
                            sb.AppendLine($"         run {run.GetType().Name} len={run.Length} text='{run.Text}'");
                        }
                    }
                }
            }
        }

        System.IO.File.AppendAllText(
            System.IO.Path.Combine(System.IO.Path.GetTempPath(), "claude", "C--dev-d47", "be2c68a4-1271-4dc9-b2a5-59fb138ffb8d", "scratchpad", "live-tab.txt"),
            sb.ToString());
    }

    /// <summary>The window or overlay host this view is drawn in, held for its render scaling.</summary>
    private TopLevel? _topLevel;

    private void OnScalingChanged(object? sender, EventArgs e) => DrawScanlines();

    /// <summary>
    /// The scanline layer, as a tile of device pixels at this surface's scaling — or nothing, on a theme
    /// whose resource says it has none (#281).
    /// </summary>
    private void DrawScanlines()
    {
        var wanted = this.TryFindResource(Theming.ThemeManager.ScanlinesKey, out var brush) && brush is not null;

        Scanlines.Background = wanted
            ? Theming.ThemeManager.Scanlines(_topLevel?.RenderScaling ?? 1)
            : null;
    }

    /// <summary>
    /// The root this view's surface gestures are registered on, held so they can be taken off it again:
    /// the zoom host reparents this view, and a handler left on a window it no longer belongs to would
    /// answer for a panel that is not there (#413).
    /// </summary>
    private Interactive? _root;

    /// <summary>
    /// Stops the log follow when this surface leaves the screen — a closed window, a reparenting zoom
    /// host, an overlay that has been torn down (#294).
    /// </summary>
    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        FollowLogFile(false);

        _root?.RemoveHandler(KeyDownEvent, OnSurfaceKeyDown);
        _root?.RemoveHandler(PointerPressedEvent, OnSurfacePointerPressed);
        _root = null;

        if (_topLevel is not null)
        {
            _topLevel.ScalingChanged -= OnScalingChanged;
            _topLevel = null;
        }

        base.OnDetachedFromVisualTree(e);
    }

    private void OnTabChecked(object? sender, RoutedEventArgs e)
    {
        // Fires for the tab being cleared as well as the one being set, and only the set one says anything
        // about which page to show.
        if (_drivingBar || sender is not RadioButton { IsChecked: true } button)
        {
            return;
        }

        foreach (var (tab, candidate) in _tabs)
        {
            if (ReferenceEquals(candidate, button))
            {
                // A prompt is abandoned rather than obeyed (remediation.md 13, item 5). "No navigating away
                // mid-choice" is right for every gesture inside the panel and wrong for the tabs: pressing
                // Engineers while a question is up is somebody saying they are done with it, so Back is taken
                // for them and nothing is committed.
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
    /// Pressing the tab that is already selected returns to its root (Phase 25, "the tab is the root
    /// rather than the first level").
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

    /// <summary>Follows the transcript, from whichever thread grew it.</summary>
    private void ScrollToEnd()
    {
        // Only while following.
        if (!_following)
        {
            return;
        }

        // Posted only when it has to be.
        if (Dispatcher.UIThread.CheckAccess())
        {
            Follow();
            return;
        }

        Dispatcher.UIThread.Post(Follow);
    }

    /// <summary>
    /// Goes to the newest line, without the trip through the handler deciding whether the Commander
    /// meant to move.
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

            // Laid out first.
            scroller.UpdateLayout();

            // And the fold gives back as much of its empty space as the new lines have taken (#413), before
            // the scroll below reads the extent — otherwise the end of the extent is the empty space rather
            // than the newest line, and following would land in it.
            ApplyFold(reassert: false);

            // The newest line, not the bottom (#233).
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

    /// <summary>Which way this reading runs, and therefore where its newest line is (#233).</summary>
    private bool NewestAtTop => Page is TranscriptPage.Journal or TranscriptPage.RawJournal;

    /// <summary>The scroller the Newest button acts on, which is not always the transcript's.</summary>
    private ScrollViewer Scroller =>
        Page == TranscriptPage.Journal ? JournalListScroller : TranscriptScroller;

    /// <summary>Whether the view is at the newest line of this reading, within a line's worth.</summary>
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

    /// <summary>The Commander moved.</summary>
    private void OnTranscriptScrolled(object? sender, ScrollChangedEventArgs e)
    {
        if (_scrollingItself)
        {
            return;
        }

        // The reading grew, or the view resized: the fold gives back as much of its empty space as the new
        // lines have taken (#413).
        if (e.ExtentDelta.Y != 0 || e.ViewportDelta.Y != 0)
        {
            ApplyFold(reassert: false);
        }

        if (e.ViewportDelta.Y != 0)
        {
            AnchorThread();

            // A resize keeps the offset, so a reader who was following would lose the newest line off the
            // bottom; posted, because this runs inside a layout pass.
            if (_following)
            {
                Dispatcher.UIThread.Post(Follow);
            }
        }

        // Only when the offset actually moved, and deliberately not when the viewport or the extent did.
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

    /// <summary>Shows the jump-to-latest control, and says how far behind the reader is.</summary>
    private void ShowFollowButton()
    {
        var behind = !_following && !AtTheNewest();

        // Not on a surface nothing can be pressed on (#202).
        FollowButton.IsVisible = behind && !OutputOnly;

        if (behind)
        {
            // The arrow points where the newest line actually is (#233), which is upwards on the two journal
            // readings.
            FollowButton.Content = NewestAtTop ? "↑ Newest" : "↓ Newest";
        }
    }

    /// <summary>Copies the whole of the page being read (Phase 19, "Copy log").</summary>
    private async void OnCopyClick(object? sender, RoutedEventArgs e)
    {
        // Its own visual root's clipboard, which is a thing a control may ask for — unlike a window, a dialog
        // or a browser, none of which this view knows about.
        if (_bound is null || TopLevel.GetTopLevel(this)?.Clipboard is not { } clipboard)
        {
            return;
        }

        // Drawn rather than written, for the reason above: the same text the Commander is looking at.
        var text = string.Concat(Drawn(_bound.Segments(Page), Page).Select(segment => segment.Text));

        bool worked;

        try
        {
            await clipboard.SetTextAsync(text);
            worked = true;
        }
        catch (Exception)
        {
            worked = false;
        }

        Controls.CopyWord.Show(CopyButton, worked);
    }

    /// <summary>Whether the page's bar exists at all.</summary>
    private void ShowSearch()
    {
        var transcript = Tab == PanelTab.Transcript;

        CopyButton.IsVisible = _searchable && transcript;

        // Only where there is something to cut and somewhere to put it (#160).
        DonateButton.IsVisible = transcript
                                 && _donate is not null
                                 && Page is TranscriptPage.Log
                                     or TranscriptPage.Journal
                                     or TranscriptPage.RawJournal;

        var filterable = transcript ? null : PagePane.Child as IFilterablePage;
        var field = _searchable && (transcript || filterable?.Filters == true);

        SearchInput.IsVisible = field;
        SearchInput.PlaceholderText = filterable?.FilterPlaceholder ?? "Search this page";
        SearchInput.Classes.Set("filter", filterable?.FilterWidth is not null);

        var tool = transcript ? null : (PagePane.Child as IPageChrome)?.BarTool;

        if (!ReferenceEquals(PageTool.Content, tool))
        {
            PageTool.Content = tool;
        }

        PageTool.IsVisible = tool is not null;

        SearchRow.IsVisible = Mode == PanelMode.Full
                              && ModalPane.Child is null
                              && !Layer.IsVisible
                              && (field || tool is not null);

        // On all four readings now (#413).
        ScrollPastReadingItem.IsEnabled = transcript;

        ShowPageBar();
        ShowLogSettingsStrip();
    }

    private void ShowPageBar()
    {
        PageBar.IsVisible = Mode == PanelMode.Full
                            && ModalPane.Child is null
                            && !Layer.IsVisible
                            && (ModePicker.IsVisible || SearchRow.IsVisible || RawToggleBox.IsVisible);

        // The rule and the 16px below it belong to the transcript's bar.
        PageBarRule.IsVisible = PageBar.IsVisible && Tab == PanelTab.Transcript;
        TranscriptPane.Padding = new Thickness(0, PageBarRule.IsVisible ? 16 : 0, 0, 0);

        SizeSearchRow();
    }

    /// <summary>The search field's width on the Transcript.</summary>
    public const double TranscriptSearchWidth = 340;

    /// <summary>
    /// The search field's width, <see cref="TranscriptSearchWidth"/> on the Transcript and otherwise the page's
    /// filter width or clamp(240, 32% of the bar, 420), beside the readings when the readings,
    /// the row's actions and the field all fit across the bar, and on a line of its own below them when
    /// they do not — where it narrows to what is left, down to 90.
    /// </summary>
    private void SizeSearchRow()
    {
        var bar = PageBar.Bounds.Width;

        if (!SearchRow.IsVisible || bar <= 0)
        {
            return;
        }

        var actions = 0.0;

        foreach (var child in SearchRow.Children)
        {
            if (child != SearchInput && child.IsVisible)
            {
                child.Measure(Size.Infinity);
                actions += child.DesiredSize.Width;
            }
        }

        var readings = 0.0;

        if (ModePicker.IsVisible)
        {
            ModePicker.Measure(Size.Infinity);
            readings = ModePicker.DesiredSize.Width;
        }

        const double Gap = 16;
        var field = Tab == PanelTab.Transcript
            ? TranscriptSearchWidth
            : (PagePane.Child as IFilterablePage)?.FilterWidth ?? Math.Clamp(bar * 0.32, 240, 420);

        if (!SearchInput.IsVisible)
        {
            field = 0;
        }
        var beside = readings == 0 || readings + Gap + actions + field <= bar;

        var dock = beside ? Dock.Right : Dock.Bottom;
        var margin = beside ? new Thickness(readings == 0 ? 0 : Gap, 0, 0, 0) : new Thickness(0, 8, 0, 0);
        var width = Math.Max(90, Math.Min(field, bar - actions));

        if (DockPanel.GetDock(SearchRow) == dock && SearchRow.Margin == margin && SearchInput.Width == width)
        {
            return;
        }

        DockPanel.SetDock(SearchRow, dock);
        SearchRow.Margin = margin;
        SearchInput.Width = width;

        // Called from PageBar.SizeChanged, inside a layout pass, where the row's new size does not reach the
        // bar's arrange on its own (#424).
        PageBar.InvalidateMeasure();
    }

    /// <summary>Built lazily the first time it would show, and never rebuilt after (#283).</summary>
    private void ShowLogSettingsStrip()
    {
        var show = Tab == PanelTab.Transcript && Page == TranscriptPage.Log && Mode == PanelMode.Full;

        if (show && LogSettingsStrip.Content is null)
        {
            var strip = _logSettingsStrip?.Invoke();
            LogSettingsStrip.Content = strip;

            if (strip is not null)
            {
                ContentPane.CapStripHeight(strip);
            }
        }

        LogSettingsStrip.IsVisible = show && LogSettingsStrip.Content is not null;
    }

    /// <summary>Opens the sharing window (#160, #238).</summary>
    private void OnDonateClick(object? sender, RoutedEventArgs e) => _donate?.Invoke();

    private void OnScrollPastReadingClick(object? sender, RoutedEventArgs e) => ScrollPastReading();

    /// <summary>Copies what is selected in the transcript — the whole of a drag that spans several
    /// bubbles, in the order it is shown (#114; remediation.md 14, item 9).</summary>
    private void OnCopySelectionClick(object? sender, RoutedEventArgs e)
    {
        if (SpanText() is { Length: > 0 } joined)
        {
            _ = TopLevel.GetTopLevel(this)?.Clipboard?.SetTextAsync(joined);
            return;
        }

        Selected()?.Copy();
    }

    /// <summary>Whichever block holds a selection right now, or none at all.</summary>
    private SelectableTextBlock? Selected() =>
        _selection is { SelectedText.Length: > 0 } held ? held : null;

    /// <summary>What a selection spanning several bubbles holds, or null with no such span.</summary>
    private string? SpanText() =>
        _span is { } span
            ? string.Join(
                Environment.NewLine,
                Enumerable.Range(span.First, span.Last - span.First + 1)
                    .Select(i => _bubbles[i].Block.SelectedText)
                    .Where(text => !string.IsNullOrEmpty(text)))
            : null;

    /// <summary>Greys Copy when there is nothing to copy or nowhere to put it.</summary>
    internal void ShowCopySelection() =>
        CopySelectionItem.IsEnabled =
            (Selected() is not null || !string.IsNullOrEmpty(SpanText()))
            && TopLevel.GetTopLevel(this)?.Clipboard is not null;

    private void OnHelpClick(object? sender, RoutedEventArgs e) => OpenHelp();

    private void OnAskClick(object? sender, RoutedEventArgs e) => Model?.Ask();

    /// <summary>Enter sends; the arrows walk what has been sent (#224).</summary>
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

    /// <summary>The cursor after a recalled line, which is the end of it.</summary>
    private void CaretToEnd() => AskBox.CaretIndex = AskBox.Text?.Length ?? 0;

    private void OnUpdateNowClick(object? sender, RoutedEventArgs e) => Model?.AcceptUpdate();

    private void OnUpdateLaterClick(object? sender, RoutedEventArgs e) => Model?.DismissUpdate();

    private void OnDismissErrorClick(object? sender, RoutedEventArgs e) => Model?.DismissError();
}

/// <summary>
/// One stretch of the transcript as it will be drawn: the characters a reader sees, who said them,
/// whether the panel is speaking about the conversation rather than in it, and what the model's markup
/// asked for.
/// </summary>
internal readonly record struct DrawnSegment(
    string Text,
    bool Marker,
    TranscriptVoice Voice,
    MarkupStyle Style,
    string Speaker,
    string? SourceKey,
    DateTimeOffset Time,
    TranscriptRunKind Kind = TranscriptRunKind.Text,
    string? ProposalId = null,
    IReadOnlyList<string>? Direction = null);

/// <summary>One side's uninterrupted stretch of the conversation — a bubble's worth.</summary>
internal sealed record DrawnTurn(
    TranscriptVoice Voice,
    bool Marker,
    IReadOnlyList<DrawnSegment> Segments,
    string Speaker,
    string? SourceKey,
    DateTimeOffset Time,
    TranscriptRunKind Kind = TranscriptRunKind.Text,
    string? ProposalId = null)
{
    /// <summary>The delivery direction taken out of the turn's text, drawn in its head.</summary>
    public IReadOnlyList<string> Direction { get; init; } = [];
}
