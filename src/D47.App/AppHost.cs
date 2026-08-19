using System.Runtime.CompilerServices;
using System.Reflection;
using D47.App.Input;
using D47.App.Logging;
using D47.App.Panel;
using D47.App.Ticking;
using D47.App.Updates;
using D47.App.Voice;
using D47.Audio;
using D47.Core.Audio;
using D47.Core;
using D47.Core.Actions;
using D47.Core.Capabilities;
using D47.Core.Callouts;
using D47.Core.Capabilities.Builtin;
using D47.Core.Checklists;
using D47.Core.Ships;
using D47.Core.Utilities;
using D47.Core.Configuration;
using D47.Core.Conversation;
using D47.Core.Diagnostics;
using D47.Core.Hotas;
using D47.Core.Input;
using D47.Core.Journal;
using D47.Core.Listening;
using D47.Core.Lore;
using D47.Core.Memory;
using D47.Core.Persona;
using D47.Core.Ticking;
using D47.Llm;
using D47.Llm.OpenAi;
using D47.Stt;
using D47.Tts;
using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Extensions.Logging;

namespace D47.App;

/// <summary>
/// The composition root. Startup order matters in one place only: logging comes up before
/// anything that could fail, so a failure has somewhere to go.
/// </summary>
public sealed class AppHost : IDisposable
{
    private readonly ILoggerFactory _loggerFactory;
    private readonly ILogger<AppHost> _logger;
    private readonly WasapiAudioSink _audioSink;

    private AppHost(
        AppPaths paths,
        KeywordRouter router,
        TurnCancellation cancellation,
        ILoggerFactory loggerFactory,
        SerilogVerbosityControl verbosity,
        SettingsService settings,
        SecretStore secrets,
        ViewStateStore viewState,
        GameStateStore gameState,
        JournalSpine journal,
        TickLoop tick,
        CalloutEngine callouts,
        CapabilityRegistry capabilities,
        UpdateChecker updates,
        UpdateInstaller installer,
        TurnLoop turns,
        PersonaHost personas,
        ShipCoreService shipCores,
        LlmAvailabilityState llmAvailability,
        SpendTracker spend,
        SpendLedger spendLedger,
        WasapiAudioSink audioSink,
        AudioArbiter audio,
        CueLibrary cues,
        VoicePipeline voice,
        ListenGate gate,
        EchoCanceller echo,
        WasapiMicrophone microphone,
        PushToTalkKey pushToTalk,
        BindsWatch binds,
        HttpModelStore models,
        WhisperTranscriber transcriber,
        string version,
        string? startupError)
    {
        Paths = paths;
        Router = router;
        Cancellation = cancellation;
        _loggerFactory = loggerFactory;
        _logger = loggerFactory.CreateLogger<AppHost>();
        Verbosity = verbosity;
        Settings = settings;
        Secrets = secrets;
        ViewState = viewState;
        GameState = gameState;
        Journal = journal;
        Tick = tick;
        Callouts = callouts;
        Capabilities = capabilities;
        Updates = updates;
        Installer = installer;
        Turns = turns;
        Personas = personas;
        ShipCores = shipCores;
        LlmAvailability = llmAvailability;
        Spend = spend;
        SpendLedger = spendLedger;
        _audioSink = audioSink;
        Audio = audio;
        Cues = cues;
        Voice = voice;
        Listening = gate;
        Echo = echo;
        _binds = binds;
        _microphone = microphone;
        _pushToTalk = pushToTalk;
        Models = models;
        _transcriber = transcriber;
        Version = version;
        StartupError = startupError;

        // When each core was last aboard, from previous runs (list.md Phase 35). Without this the
        // elapsed time a gap reaction is measured against would start at zero every launch, and a
        // month-long absence — which is the only kind that earns one — spans launches by
        // definition. No session goes with it: see the field.
        foreach (var (core, at) in viewState.Load().CoresLastAboard)
        {
            _personaLastSeen[core] = (at, null);
        }
    }

    public AppPaths Paths { get; }

    public SerilogVerbosityControl Verbosity { get; }

    /// <summary>
    /// The settings surface. Everything that changes a setting goes through here, whichever
    /// surface asked, which is what makes the protected set enforceable in one place.
    /// </summary>
    public SettingsService Settings { get; }

    public SecretStore Secrets { get; }

    /// <summary>How the panel was left. A view preference, kept apart from settings.</summary>
    public ViewStateStore ViewState { get; }

    public GameStateStore GameState { get; }

    public JournalSpine Journal { get; }

    /// <summary>
    /// The ~4-10 Hz loop (architecture.md §4). Exposed because a surface that needs sampling
    /// rather than events — push-to-talk edge detection, the VR connection state machine —
    /// registers here instead of growing a timer of its own.
    /// </summary>
    public TickLoop Tick { get; }

    /// <summary>
    /// What the panel is showing. Owned here rather than by the window, because it is app
    /// state: the desktop window and the headset overlay each instantiate a view against it,
    /// and a model owned by one of them would make the other a guest (list.md Phase 9).
    /// </summary>
    public Panel.PanelViewModel Panel { get; } = new();

    /// <summary>
    /// The headset path, once Avalonia has come up. Null before that and on a run where the
    /// framework never initialises — it needs a dispatcher and a widget tree, neither of which
    /// exists when this host is built.
    /// </summary>
    public Headset.VrHost? Vr { get; set; }

    /// <summary>
    /// What d47 says without being asked (list.md Phase 8). Exposed because the panel drains it:
    /// the tick that produces an announcement must not block on synthesising it.
    /// </summary>
    public CalloutEngine Callouts { get; }

    public CapabilityRegistry Capabilities { get; }

    /// <summary>
    /// The model-free command path. Exposed because a surface has to ask it what may interrupt
    /// a turn in flight before applying its own in-flight gate — see MainWindow.AskAsync.
    /// </summary>
    public KeywordRouter Router { get; }

    /// <summary>
    /// The handle on the turn in flight. A surface must run its turns under
    /// <see cref="TurnCancellation.Begin"/>, or "cancel" has nothing to cancel and the model
    /// keeps generating — and billing — after the Commander has called it off.
    /// </summary>
    public TurnCancellation Cancellation { get; }

    public UpdateChecker Updates { get; }

    /// <summary>
    /// Records what has been exercised by hand, when this process was asked to. Null — and
    /// therefore absent from the panel too — in every normal run.
    /// </summary>
    public D47.App.Coverage.CoverageRecorder? CoverageRecorder { get; private set; }

    /// <summary>Fetches and installs what <see cref="Updates"/> found.</summary>
    public UpdateInstaller Installer { get; }

    /// <summary>
    /// Gives up this process's claim on being the only d47, so the build replacing it can start
    /// before this one has finished exiting. Set by the composition root; null under a test.
    /// </summary>
    public Action? ReleaseSingleInstance { get; set; }

    /// <summary>One turn of conversation, whichever path answers it.</summary>
    public TurnLoop Turns { get; }

    /// <summary>Which Guardian core is aboard, and what it remembers (list.md Phase 11).</summary>
    public PersonaHost Personas { get; }

    /// <summary>
    /// Which core flies which ship (list.md Phase 35). Public because the gesture reaches it:
    /// a system-wide hotkey is bound in the window and has to be able to perform the act, on
    /// exactly the same footing as the panel button and the phrase.
    /// </summary>
    public ShipCoreService ShipCores { get; }

    /// <summary>
    /// The Commander's own per-state avatar frames, if they have supplied any. Null until
    /// startup has scanned for them, and empty for almost everyone — the panel draws its own.
    /// </summary>
    public D47.Core.Interface.AvatarLibrary? Avatars { get; private set; }

    /// <summary>Whether the model is usable right now, and why not when it isn't.</summary>
    public LlmAvailabilityState LlmAvailability { get; }

    /// <summary>Per-turn cost and the running total.</summary>
    public SpendTracker Spend { get; }

    /// <summary>
    /// Every charge, kept between runs. What answers "this week" and "this month" — questions the
    /// session-scoped <see cref="Spend"/> cannot be asked.
    /// </summary>
    public SpendLedger SpendLedger { get; }

    /// <summary>
    /// The one queue every audible thing goes through (architecture.md D7). Exposed because
    /// the hotkey and the panel both need to silence it, and there is nowhere else to ask.
    /// </summary>
    public AudioArbiter Audio { get; }

    /// <summary>
    /// The cues, beds and ambience currently loaded — the shipped set plus whatever is in
    /// <c>data/audio/</c>.
    /// <para>
    /// Replaced rather than mutated when the folder changes, so anything mid-playback keeps the
    /// clip it already holds and a reload can never cut a sentence (list.md Phase 12).
    /// </para>
    /// </summary>
    public CueLibrary Cues { get; private set; }

    /// <summary>What a turn sounds like.</summary>
    public VoicePipeline Voice { get; }

    /// <summary>
    /// The gate the microphone feeds. Exposed because a surface subscribes to its utterances —
    /// the gate itself knows nothing about turns.
    /// </summary>
    public ListenGate Listening { get; }

    /// <summary>
    /// What removes d47's own voice from what the microphone hears (list.md Phase 13). Exposed
    /// because whether it is actually running is a thing the listening status answers, and
    /// "the setting is on" is not the same claim.
    /// </summary>
    public EchoCanceller Echo { get; }

    /// <summary>
    /// Whether an utterance was addressed to d47 at all, in wake-word mode. Lives on the host
    /// rather than on the gate because it decides about words, not about audio — the gate has
    /// already done its part by the time this is asked (see <see cref="WakeWordGate"/>).
    /// </summary>
    public WakeWordGate Wake { get; } = new();

    /// <summary>
    /// The Commander's Elite bindings. Read-only, and the same parse the double-bind check and
    /// Phase 10's reachability both use.
    /// <para>
    /// Asked for each time rather than held, because it is re-read whenever Elite rewrites the
    /// file — a control rebound in the game's own options menu used to need a restart of d47
    /// before anything here knew about it (remediation.md 16, item 2).
    /// </para>
    /// </summary>
    public EliteBinds Binds => _binds.Current;

    /// <summary>The Commander's macros. The panel's editor writes through this, not past it.</summary>
    public MacroStore Macros { get; private set; } = null!;

    /// <summary>The cores the Commander wrote themselves (remediation.md 11, item 9).</summary>
    public OwnPersonaStore OwnPersonas { get; private set; } = null!;

    /// <summary>
    /// The Commander's checklist, and the proposals waiting on it (list.md Phase 17). The panel
    /// writes through this like the macro editor does — and it is the surface that accepts a
    /// proposal, which is an act the model is not allowed to perform.
    /// </summary>
    public ChecklistService Checklists { get; private set; } = null!;

    /// <summary>The Commander's timers and alarms (list.md Phase 24).</summary>
    public Timekeeper Timekeeper { get; private set; } = null!;

    /// <summary>The Commander's ship builds, joined to the fleet (list.md Phase 26).</summary>
    public ShipPlanService Ships { get; private set; } = null!;

    /// <summary>Where the builds are kept, for the panel to follow and for a hand edit to reach.</summary>
    public ShipBuildStore ShipBuilds { get; private set; } = null!;

    /// <summary>
    /// The Commander's suit and weapon plans, joined to what they are wearing (list.md Phase 27).
    /// The on-foot half of the same page.
    /// </summary>
    public D47.Core.Loadout.OnFootPlanService OnFootPlans { get; private set; } = null!;

    /// <summary>Where those are kept, for the panel to follow and for a hand edit to reach.</summary>
    public D47.Core.Loadout.OnFootBuildStore OnFootBuilds { get; private set; } = null!;

    /// <summary>
    /// Which engineer to go and get next, read across both plan stores (list.md Phase 28). Owns
    /// nothing: every figure is recomputed from those two and the live game state.
    /// </summary>
    public D47.Core.Engineers.EngineerPlanService Unlocks { get; private set; } = null!;

    /// <summary>Where the alarms are kept, for the panel to follow and for a hand edit to reach.</summary>
    public AlarmStore Alarms { get; private set; } = null!;

    /// <summary>
    /// Every phrase d47 already answers to, so the macro editor can refuse one that would
    /// shadow a built-in command. Computed once: the registry is immutable.
    /// </summary>
    public IReadOnlyList<string> ReservedPhrases { get; private set; } = [];

    /// <summary>
    /// What the settings surface needs to walk and assign a HOTAS switch (list.md Phase 21).
    /// Null when nothing composed hardware, which is what the designer and a test that is not
    /// about switches get — the row's button is then absent rather than dead.
    /// </summary>
    public Settings.SwitchEditing? SwitchEditing { get; private set; }

    /// <summary>
    /// What the settings surface needs to show and write the Commander's own lore notes
    /// (list.md Phase 23). Null under the designer, where the row shows a summary and no button.
    /// </summary>
    public Settings.LoreEditing? LoreEditing { get; private set; }

    /// <summary>
    /// What d47 remembers about the Commander, and the clock a fact typed on the panel is stamped
    /// with (list.md Phase 31). Null under the designer, where the row shows a summary and no
    /// button.
    /// </summary>
    public (MemoryBook Book, Func<DateTimeOffset> Now)? Memories { get; private set; }

    /// <summary>
    /// What d47 has noticed the Commander keeps doing (list.md Phase 32). Null under the designer,
    /// where the row shows a summary and no button.
    /// </summary>
    public (D47.Core.Habits.HabitBook Book, Action? Mine)? Habits { get; private set; }

    /// <summary>
    /// The Commander's log (list.md Phase 33). Null under the designer, where the row reads a
    /// folder that does not exist and offers no way to spend anything.
    /// </summary>
    public D47.Core.Logbook.LogbookBook? Logbook { get; private set; }

    /// <summary>
    /// The Commander's long arcs (list.md Phase 34). Null under the designer, where the checklist
    /// page draws no arc band and the row shows a summary and no button.
    /// </summary>
    public (D47.Core.Goals.GoalBook Book, Action? Backfill)? Goals { get; private set; }

    /// <summary>
    /// Speech models on disk, and the way to fetch one. Exposed because the settings surface is
    /// where a model is chosen, and it shows the progress of the download that choice starts.
    /// </summary>
    public IModelStore Models { get; }

    /// <summary>Raised when an utterance has been turned into words, so a surface can run it.</summary>
    public event Action<string>? Heard;

    /// <summary>
    /// Raised with something d47 is saying that no turn produced, so the transcript can carry
    /// it too. What d47 says out loud and what the Commander can read afterwards should not be
    /// two different sets: a line that was only ever spoken is a line nobody can go back to.
    /// </summary>
    public event Action<string>? Said;

    /// <summary>
    /// Raised with something that happened to the conversation rather than something said in
    /// it — the core changing under it being the case this exists for. Separate from
    /// <see cref="Said"/> because nothing speaks this: it is the panel noting, in its own
    /// voice, why the next line sounds like somebody else.
    /// </summary>
    public event Action<string>? Noted;

    /// <summary>
    /// Raised with a line for the Technical page — in-game comms, which are neither the
    /// conversation nor a diagnostic.
    /// <para>
    /// Separate from <see cref="Said"/> because that one is d47 talking and lands on the
    /// conversation page, and a station clearing the Commander to dock is not part of a
    /// conversation with their companion. The wording comes from
    /// <see cref="Announcement.Transcript"/>, so what is written and what is heard can differ:
    /// the ear gets the words and the page gets the sender as well.
    /// </para>
    /// </summary>
    public event Action<string>? Transcribed;

    /// <summary>
    /// Raised true when a core has been chosen and has not yet worked out what to say, and
    /// false when it has (list.md Phase 12, "Anything that might take a moment says it is
    /// working").
    /// <para>
    /// A gap reaction spends a model round trip before the new core's first word, which from the
    /// settings row looks exactly like nothing happening. Raised from whichever thread the
    /// switch is being resolved on, so a subscriber that touches controls has to post.
    /// </para>
    /// </summary>
    public event Action<bool>? PersonaSettling;

    /// <summary>
    /// Raised when the Commander's audio folder was re-read and the library replaced. The
    /// settings surface listens, so a bed dropped in appears without a restart.
    /// </summary>
    public event Action? AudioReloaded;

    /// <summary>
    /// Downloads a model and loads it. Called by the settings row when the Commander picks one;
    /// the choice is the go-ahead, and the size was on the row they chose from.
    /// </summary>
    public async Task<ModelInstallResult> InstallModelAsync(
        WhisperModel model,
        IProgress<ModelProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var result = await Models
            .InstallAsync(model, progress, cancellationToken)
            .ConfigureAwait(false);

        if (result.Success)
        {
            // Load it now rather than at the next restart — the same "apply every setting
            // without a restart" rule everything else follows (list.md Phase 4).
            ApplyListeningSettings();
        }

        return result;
    }

    private readonly WhisperTranscriber _transcriber;

    private readonly BindsWatch _binds;

    private readonly WasapiMicrophone _microphone;

    /// <summary>
    /// When the Commander was last heard and understood. The evidence behind answering "can you
    /// hear me?" with a demonstration rather than an inventory of device state. A box because
    /// the listening surface closes over it during composition, before this object exists.
    /// </summary>
    private StrongBox<DateTimeOffset?>? _heardAt;

    private readonly PushToTalkKey _pushToTalk;

    public string Version { get; }

    /// <summary>For surfaces that need a logger of their own — the theme manager, so far.</summary>
    public ILoggerFactory Loggers => _loggerFactory;

    /// <summary>
    /// Set when settings could not be loaded. Surfaced on the panel rather than swallowed:
    /// starting on defaults without saying so would discard the Commander's configuration
    /// silently, which is the failure mode the two-store split exists to prevent.
    /// </summary>
    public string? StartupError { get; }

    public static AppHost Start()
    {
        var paths = AppPaths.BesideExecutable();
        paths.EnsureCreated();

        var version = BuildInfo.Full;

        // Logging first, so everything below has somewhere to report a failure.
        var verbosity = new SerilogVerbosityControl();

        // Built with logging rather than after it, because a sink has to be in the pipeline to
        // see anything. It drops events until it is pointed at a panel, which is the right
        // behaviour for the startup errors raised before there is one.
        var technicalLog = new TechnicalLogBridge();

        Log.Logger = LoggingSetup.Create(paths, verbosity, technicalLog);
        var loggerFactory = new SerilogLoggerFactory(Log.Logger);
        var logger = loggerFactory.CreateLogger<AppHost>();

        // The earliest thing written, before settings, providers or the headset exist. It is
        // deliberately thin: its job is to be the line that is there when startup dies before
        // RecordStartup can say anything fuller (remediation.md 10, item 7).
        logger.LogInformation("d47 {Version} is starting; data folder {Data}", version, paths.Data);

        var store = new SettingsStore(paths, loggerFactory.CreateLogger<SettingsStore>());
        var loaded = new D47Settings();
        string? startupError = null;
        try
        {
            loaded = store.Load();
        }
        catch (SettingsLoadException ex)
        {
            startupError = ex.Message;
            logger.LogCritical(ex, "Settings could not be loaded; continuing on defaults");
        }

        verbosity.Apply(loaded.Logging);

        var secrets = new SecretStore(
            paths,
            new DpapiSecretProtector(),
            loggerFactory.CreateLogger<SecretStore>());

        var settings = new SettingsService(store, secrets, loaded, loggerFactory.CreateLogger<SettingsService>());

        // From here a level change is live wherever it came from — panel, tool or settings file.
        verbosity.FollowSettings(settings);

        var viewState = new ViewStateStore(paths, loggerFactory.CreateLogger<ViewStateStore>());

        var journalDirectory = ResolveJournalDirectory();
        // Assigned once the bindings have been resolved, below. A holder rather than a
        // reordering: the journal readers have to be built before the tick loop, and the binds
        // are read after the audio devices, and neither of those orders is negotiable for a
        // lambda's benefit.
        Func<EliteBinds>? bindsRef = null;

        // Sampling history, which is the one derived state that has to outlive a session: the
        // spine tails the newest journal, so a run begun yesterday is otherwise simply gone
        // (list.md Phase 18).
        var sampling = new SamplingStore(
            Path.Combine(paths.Data, "sampling.json"),
            loggerFactory.CreateLogger<SamplingStore>());

        sampling.Load();

        // Systems worth remarking on, in two files with two different characters (list.md
        // Phase 23). The book is the Commander's own words, so it is polled for hand edits and
        // reports problems rather than dropping lines; the visits are derived stamps, so a bad
        // file is discarded and the worst it costs is hearing one remark twice. One set each for
        // the installation rather than per Commander: a note about a system is true whichever
        // character is flying, and the 24-hour rule is about not repeating yourself to a person.
        var lore = new LoreBook(new LoreStore(
            Path.Combine(paths.Data, "lore.json"),
            loggerFactory.CreateLogger<LoreStore>()));

        var loreVisits = new LoreVisits(
            Path.Combine(paths.Data, "lore-visits.json"),
            loggerFactory.CreateLogger<LoreVisits>());

        lore.Store.Poll();
        loreVisits.Load();

        var gameState = new GameStateStore { Restore = sampling.For };

        // The two state files Elite rewrites in place. Same folder as the journal, different
        // shape: a log is appended to and these are replaced, which is entirely inside the
        // readers.
        var status = new GameStatusReader(journalDirectory, loggerFactory.CreateLogger<GameStatusReader>());
        var route = new NavRouteReader(journalDirectory, loggerFactory.CreateLogger<NavRouteReader>());

        // After the status reader, because the spine stamps a surface position onto events that
        // carry none — organic sampling is the whole reason (list.md Phase 18).
        var journal = new JournalSpine(journalDirectory, gameState, loggerFactory, () => status.Current);

        // The Commander's checklist and the proposals waiting on it, in two files beside the
        // executable (list.md Phase 17). Two files rather than one because the trust boundary is
        // the point: the model writes proposals and never the list, and that is inspectable by
        // opening data\ rather than by reading this file.
        //
        // Built here, before the callouts, because the checklist has a callout of its own and the
        // priming tick below has to fold the journal backlog into it silently — exactly as the
        // material milestones are primed.
        var checklists = new ChecklistService(
            new ChecklistStore(
                Path.Combine(paths.Data, "checklist.json"),
                loggerFactory.CreateLogger<ChecklistStore>()),
            new ChecklistProposalStore(
                Path.Combine(paths.Data, "checklist-proposals.json"),
                loggerFactory.CreateLogger<ChecklistProposalStore>()),
            () => gameState.Active);

        // What d47 remembers about the Commander (list.md Phase 31). One file, per Commander with
        // the key inside the document, and it has both halves of the store pattern Phase 23 split
        // in two: the Commander's own words are in it, so it is polled for hand edits and reports
        // problems rather than dropping lines — and it is keyed per character, because who is
        // flying is who the facts are about.
        var memories = new MemoryStore(
            Path.Combine(paths.Data, "memories.json"),
            loggerFactory.CreateLogger<MemoryStore>());

        memories.Poll();

        var memoryBook = new MemoryBook(
            memories,
            () => gameState.Active?.Identity.FrontierId,
            () => MemorySituation.Of(gameState.Active, status.Current));

        // The only thing in the phase that writes a memory nobody asked for, and the only producer
        // of the observed tier — without it that tier would be an enum member reachable by nothing.
        var memoryObserver = new MemoryObserver(memoryBook);

        // What d47 has noticed the Commander keeps doing, from the journals already on this disk
        // (list.md Phase 32). Same store shape as memories and keyed the same way, because it is
        // about the same person — and separate from them because a claim carries a count, a window
        // and a dismissal, none of which a remembered fact has anywhere to put.
        var habits = new D47.Core.Habits.HabitStore(
            Path.Combine(paths.Data, "habits.json"),
            loggerFactory.CreateLogger<D47.Core.Habits.HabitStore>());

        habits.Poll();

        var habitBook = new D47.Core.Habits.HabitBook(
            habits,
            () => gameState.Active?.Identity.FrontierId);

        var miner = new D47.Core.Habits.HabitMiner(
            loggerFactory.CreateLogger<D47.Core.Habits.HabitMiner>());

        // Whether a pass is already running. An int rather than a bool because Interlocked has no
        // overload for one, and the guard has to be atomic: the button is a press and a Commander
        // who does not see anything happen for three seconds presses it again.
        var mining = 0;

        // Off the UI thread, because the pass is seconds long over hundreds of megabytes. Core
        // stays synchronous and clock-free and the App is what decides where it runs — the same
        // split every long-running thing in this file has. Named rather than inlined because two
        // surfaces press it: the settings row and the window behind it.
        void MineHabits()
        {
            if (Interlocked.Exchange(ref mining, 1) == 1)
            {
                return;
            }

            _ = Task.Run(() =>
            {
                try
                {
                    habits.Record(miner.Mine(journalDirectory, DateTimeOffset.Now));
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    logger.LogWarning(ex, "Could not read the journals for habits");
                }
                finally
                {
                    Interlocked.Exchange(ref mining, 0);
                }
            });
        }

        // The Commander's long arcs (list.md Phase 34). The third store keyed on the Frontier id
        // and the second walk over the same corpus — separate from habits because an arc carries a
        // definition of done, a start date and a person's decision to set it aside, none of which a
        // noticed habit has anywhere to put.
        var goals = new D47.Core.Goals.GoalStore(
            Path.Combine(paths.Data, "goals.json"),
            loggerFactory.CreateLogger<D47.Core.Goals.GoalStore>());

        goals.Poll();

        var goalMiner = new D47.Core.Goals.GoalMiner(
            loggerFactory.CreateLogger<D47.Core.Goals.GoalMiner>());

        var backfilling = 0;

        // Off the UI thread for the reason MineHabits is, and guarded the same way: the button is a
        // press, and a Commander who does not see anything happen presses it again.
        void BackfillGoals()
        {
            if (Interlocked.Exchange(ref backfilling, 1) == 1)
            {
                return;
            }

            _ = Task.Run(() =>
            {
                try
                {
                    goals.Record(goalMiner.Mine(journalDirectory, DateTimeOffset.Now));
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    logger.LogWarning(ex, "Could not read the journals for goals");
                }
                finally
                {
                    Interlocked.Exchange(ref backfilling, 0);
                }
            });
        }

        // Assigned once the ship and on-foot plans exist, below. A holder rather than a
        // reordering, exactly as bindsRef above is one: the callouts have to be built before the
        // tick loop and the engineer solver reads two stores that are built after the readers, and
        // neither of those orders is negotiable for a lambda's benefit.
        D47.Core.Engineers.EngineerPlanService? unlocksRef = null;

        // Reads the same holder the callouts do, because the engineers arc delegates its "what do I
        // do about this today" to the unlock solver rather than growing a worse one.
        var goalBook = new D47.Core.Goals.GoalBook(
            goals,
            () => gameState.Active?.Identity.FrontierId,
            () => gameState.Active,
            checklists,
            () => unlocksRef);

        var callouts = BuildCallouts(
            loaded, loggerFactory, checklists, lore, loreVisits, memoryBook, habitBook, () => unlocksRef);

        // Acting on the game without being asked (list.md Phase 10, item 2). Each member is off
        // until its own row is switched on, which is why the runner reads the setting per tick
        // rather than being told once at construction.
        var autonomous = new AutonomousActionRunner(loggerFactory.CreateLogger<AutonomousActionRunner>())
            .Add(new HonkOnArrival(
                () => settings.Current.Actions.HonkOnArrival,
                () => bindsRef!()));

        // The ~4-10 Hz loop from architecture.md §4. Registration order is load-bearing: the
        // journal and the two state files are read first, so the callouts examining them see
        // this tick's events rather than the last tick's.
        var tick = new TickLoop(loggerFactory.CreateLogger<TickLoop>());

        // This tick's journal events, for the subscribers registered after the host exists and
        // therefore too late to be inside the closure below. Read rather than re-polled: polling
        // twice would give the second reader an empty list, because the first one consumed them.
        // Registration order is what makes this safe - "journal" runs first, by design.
        IReadOnlyList<JournalEvent> arrived = [];

        tick.Add("journal", context =>
        {
            var events = journal.Poll();

            arrived = events;
            status.Poll();
            route.Poll();

            // Before the callouts and inside this subscriber, so a verdict recomputed from this
            // tick's events is announced on this tick rather than the next. Polled unconditionally
            // — the checklist callout can be switched off, and the list must stay honest anyway.
            checklists.Poll(announce: !context.IsFirst);

            var calloutContext = new CalloutContext(
                context.Now,
                IsPriming: context.IsFirst,
                gameState.Active,
                status.Current,
                route.Current,
                events);

            callouts.Tick(calloutContext);
            autonomous.Tick(calloutContext);

            // Written only when a sample actually landed, rather than every tick: this runs ten
            // times a second and the file changes a few times an hour.
            if (events.Any(journalEvent => journalEvent.Kind == "ScanOrganic"))
            {
                sampling.Save(gameState.All);
            }

            // The Commander's lore notes are hand-editable, so they are polled like the checklist
            // is; the remark stamps are written only when one was actually made, which is at most
            // a handful of times a day.
            lore.Store.Poll();


            if (loreVisits.Dirty)
            {
                loreVisits.Save();
            }
        });

        // Primed synchronously before anything reads game state, so a journal already on disk
        // when d47 starts is answered correctly, backlog and all — and so the panel's first
        // status is not a race against the first timer tick. Subscribers tell this tick apart
        // from a live one by TickContext.IsFirst, which is what keeps a backlog of past events
        // from being announced as though it had just happened — and is what the material
        // milestone tracker means by being primed from the session backlog.
        tick.Tick(DateTimeOffset.Now);

        logger.LogInformation(
            "Journal folder {Directory}; tailing {File}",
            journalDirectory,
            journal.CurrentFile ?? "(none found)");

        // Availability and spend exist before the registry because capabilities report on them;
        // the provider itself is built afterwards, from settings, by ApplyLlmSettings.
        var llmAvailability = new LlmAvailabilityState(providerConfigured: false);

        // The history behind the running totals. Read once here, so the first turn of a session
        // can already be charged against a month that started before the process did.
        var spendLedger = new SpendLedger(
            paths.SpendFile,
            SystemWallClock.Instance,
            loggerFactory.CreateLogger<SpendLedger>());

        var spend = new SpendTracker(spendLedger);

        // Clocks, timers and alarms (list.md Phase 24). Alarms are a file because they are a
        // promise about a wall-clock moment that outlives the process; timers live only in the
        // Timekeeper, because a forty-minute countdown through a crash is a question nobody can
        // answer.
        var alarms = new AlarmStore(
            Path.Combine(paths.Data, "alarms.json"),
            loggerFactory.CreateLogger<AlarmStore>());

        alarms.Poll();

        var timekeeper = new Timekeeper(alarms);

        // The Commander's ship builds (list.md Phase 26). Its own store, because the plan owns
        // what and the checklist owns when - nothing crosses between them unasked.
        var shipBuilds = new ShipBuildStore(
            Path.Combine(paths.Data, "ships.json"),
            loggerFactory.CreateLogger<ShipBuildStore>());

        shipBuilds.Poll();

        // The fleet joined to the builds. It reads the checklist service to propose promotions
        // and never writes to it directly: the plan owns what, the checklist owns when.
        var shipPlans = new ShipPlanService(shipBuilds, checklists, () => gameState.Active);

        // Which core flies which ship (list.md Phase 35). Its own file rather than a column on the
        // one above: a build is a plan, so hanging a preference off one would create a plan as a
        // side effect of stating the preference, and lose the preference when the plan was deleted.
        var shipCoreStore = new ShipCoreStore(
            Path.Combine(paths.Data, "ship-cores.json"),
            loggerFactory.CreateLogger<ShipCoreStore>());

        shipCoreStore.Poll();

        var shipCores = new ShipCoreService(shipCoreStore, () => gameState.Active);

        // And the same arrangement on foot (list.md Phase 27). Its own file rather than a second
        // array in the ship one, because the game separates ship and on-foot hard and a Commander
        // hand-editing a suit should not be reading past twenty hardpoints to find it.
        var onFootBuilds = new D47.Core.Loadout.OnFootBuildStore(
            Path.Combine(paths.Data, "on-foot.json"),
            loggerFactory.CreateLogger<D47.Core.Loadout.OnFootBuildStore>());

        onFootBuilds.Poll();

        var onFootPlans = new D47.Core.Loadout.OnFootPlanService(
            onFootBuilds, checklists, () => gameState.Active);

        // The engineer solver (list.md Phase 28). It reads both stores rather than being handed
        // their contents, because a ranking is only as current as the plans under it — and both
        // of them move while the panel is open.
        var unlocks = new D47.Core.Engineers.EngineerPlanService(
            shipBuilds, onFootBuilds, checklists, () => gameState.Active);

        // The holder declared before the callouts, filled in now. The continuity line asks for this
        // when it composes rather than when the engine was assembled, so this is in time.
        unlocksRef = unlocks;

        // Late-bound, because several things built here have to read something that does not
        // exist until the host does — the voice list, the headset report, and now the cue
        // library, which is replaced whenever the Commander drops a file into data/audio.
        AppHost? self = null;

        // A session, written up (list.md Phase 33). The one thing d47 produces that the Commander
        // takes away, so it goes to its own folder beside the executable rather than into data/logs,
        // which already holds d47's diagnostics and would make "my log" a support question.
        var logbook = new D47.Core.Logbook.LogbookBook(
            new D47.Core.Logbook.LogFolder(
                Path.Combine(paths.Data, D47.Core.Logbook.LogFolder.FolderName),
                loggerFactory.CreateLogger<D47.Core.Logbook.LogFolder>()),
            new D47.Core.Logbook.LogDigestBuilder(loggerFactory.CreateLogger<D47.Core.Logbook.LogDigestBuilder>()),
            new D47.Core.Logbook.LogWriter(loggerFactory.CreateLogger<D47.Core.Logbook.LogWriter>()),
            () => settings.Current.Logbook,

            // Read at the moment a log is asked for rather than captured now, because a Commander
            // who has been flying since launch has journals here that did not exist then.
            () => JournalsOnDisk(journalDirectory, logger),
            () => DateTimeOffset.Now,

            // The provider, the model and the persona as they are at this instant. A snapshot,
            // because all three can change between the quote and the writing, and a log priced
            // against one model and written by another would make the quote a fiction — which
            // LogbookBook.WriteAsync checks for rather than assumes.
            () => new D47.Core.Logbook.LogbookContext
            {
                Provider = self?.Turns.Provider,
                Model = self?.Turns.Model,
                PersonalityEnabled = settings.Current.Llm.PersonalityEnabled,
                Persona = self?.Personas.RenderBlock(settings.Current.Llm.PersonalityEnabled),
                AboutMe = settings.Current.Llm.AboutMe,
                Ledger = spendLedger,
                Version = version,
            },
            loggerFactory.CreateLogger<D47.Core.Logbook.LogbookBook>());

        // Audio comes up before the registry because the speech capability's settings rows read
        // the bed names and the device list from it. The sink is opened here rather than lazily
        // so a machine with no working output says so once, at startup, instead of on the first
        // turn the Commander was hoping to hear.
        //
        // The Commander's own folder is a second source beside the embedded one, drop-ins winning
        // by name (list.md Phase 12, "Custom Sound Cues"). Nothing here holds the resulting
        // library: everything that plays a cue asks the host for the current one, so a rebuild is
        // one assignment rather than a set of references to chase.
        var drops = new FolderAudioSource(paths.Audio, loggerFactory.CreateLogger<FolderAudioSource>());
        var cueLogger = loggerFactory.CreateLogger<CueLibrary>();
        var cues = CueLibrary.Load(cueLogger, new EmbeddedCueSource(typeof(CueLibrary).Assembly), drops);

        var audioSink = new WasapiAudioSink(loggerFactory.CreateLogger<WasapiAudioSink>());
        var audio = new AudioArbiter(audioSink, loggerFactory.CreateLogger<AudioArbiter>()).Start();
        var voice = new VoicePipeline(audio, () => self!.Cues, loggerFactory)
        {
            // What a voice is called, for the log line that says who spoke (remediation.md 10,
            // item 9). Asked per utterance rather than copied, because the catalogue arrives from
            // the provider after this and changes when the provider does.
            VoiceName = id => id is { Length: > 0 } ? self?.VoiceNameFor(id) : null,
        };

        // The loop settles back to idle when the arbiter goes quiet rather than when the turn
        // returns, because the turn returns while the reply is still being spoken. Wired here
        // because VoicePipeline has a primary constructor and cannot subscribe from one.
        audio.ActivityChanged += voice.Settle;

        // A track ending is how the next one is asked for. The arbiter reports the end and
        // nothing more: which track comes next is a question about situations and shuffling,
        // and a queue that answered it would be a queue that knows what a station is.
        audio.MusicFinished += () => self?.PlayNextTrack();

        try
        {
            audioSink.Open(loaded.Speech.OutputDevice);
        }
        catch (Exception ex)
        {
            // No audio output is a capability being off, not a startup failure. d47 stays
            // fully usable in text (list.md Phase 3, "Capabilities as state, not guard").
            logger.LogError(ex, "No audio output could be opened; D47 will be silent");
        }

        // Listening. The microphone runs continuously into the gate and the gate decides which
        // part of that stream was addressed to d47 — push-to-talk is a policy over the stream,
        // not a reason to start and stop the device (list.md Phase 6).
        var models = new HttpModelStore(paths, loggerFactory.CreateLogger<HttpModelStore>());
        var transcriber = new WhisperTranscriber(loggerFactory.CreateLogger<WhisperTranscriber>());
        var gate = new ListenGate(WasapiMicrophone.SampleRate, loggerFactory.CreateLogger<ListenGate>());

        // Between the microphone and the gate, consuming the arbiter's render reference tap
        // rather than a loopback capture (list.md Phase 13, architecture.md D7). The tap has
        // existed since Phase 5 with nothing subscribed to it, precisely so that this line
        // could be added without opening the component every voice path depends on.
        var echo = new EchoCanceller(
            gate,
            audioSink.ReferenceTap,
            WasapiMicrophone.SampleRate,
            loggerFactory.CreateLogger<EchoCanceller>());

        // Whether d47 is currently audible, which the gate needs only when nothing is cancelling
        // it: uncancelled, a hands-free mode with speakers is a loop where d47 hears itself,
        // transcribes itself and answers itself. With the canceller running this changes nothing
        // and the Commander can talk over it, which is the whole of Phase 13's first item.
        audio.ActivityChanged += activity => gate.FarEndActive = activity.Channel is not null;

        var microphone = new WasapiMicrophone(echo, loggerFactory.CreateLogger<WasapiMicrophone>());
        var pushToTalk = new PushToTalkKey(loggerFactory.CreateLogger<PushToTalkKey>());

        // The only thing that presses a key in the game (architecture.md D4). Built here so
        // there is exactly one, because release_all has to be able to let go of everything and
        // a second injector would hold keys the first one knows nothing about.
        var eliteWindow = new EliteWindow(loggerFactory.CreateLogger<EliteWindow>());
        var gameInput = new ScancodeInjector(eliteWindow, loggerFactory.CreateLogger<ScancodeInjector>());

        // Declared here and assigned inside the registry build below, so the capabilities and
        // the prompt's game-state block are looking at one surface rather than two that could
        // disagree about what is reachable.
        ActionSurface actionSurface;

        // Read at startup and re-read when Elite rewrites it. The old comment here said the file
        // changes only when the Commander edits their controls, "which they cannot do while d47
        // is the foreground window" — true, and beside the point: controls are edited in Elite's
        // options menu, with Elite in front, and most often right after d47 has said an action is
        // not bound (remediation.md 16, item 2). The stamp comparison is what makes polling it on
        // the tick cost nothing.
        var binds = new BindsWatch(
            BindsResolver.DefaultBindingsDirectory(),
            EliteInstallations(),
            loggerFactory.CreateLogger<AppHost>());

        bindsRef = () => binds.Current;

        // The Commander's own macros, beside the executable like everything else d47 writes.
        // Re-read on the tick, so a macro edited in a text editor is live without a restart.
        var macros = new MacroStore(
            Path.Combine(paths.Data, "macros.json"), loggerFactory.CreateLogger<MacroStore>());

        // The Commander's HOTAS switches, in the same shape and beside the same executable
        // (list.md Phase 21). The reader is the one hardware component here that stays
        // subscribed as well as being polled — hot-plug and slow enumeration are the same code
        // path, and a startup-only enumeration reports three of six devices.
        var switches = new SwitchStore(
            Path.Combine(paths.Data, "switches.json"), loggerFactory.CreateLogger<SwitchStore>());

        var controllers = new HotasControllers(loggerFactory.CreateLogger<HotasControllers>());
        var reconciler = new SwitchReconciler(loggerFactory.CreateLogger<SwitchReconciler>());

        var cancellation = new TurnCancellation(loggerFactory.CreateLogger<TurnCancellation>());

        // The cores the Commander wrote, beside the executable and polled like the macros above
        // (remediation.md 11, item 9). Pointed at the catalogue before the persona host resolves
        // the selected id, or a Commander whose chosen core is one of their own starts the app as
        // Warden and is switched a tick later.
        var ownPersonas = new OwnPersonaStore(
            Path.Combine(paths.Data, "personas.json"),
            loggerFactory.CreateLogger<OwnPersonaStore>());

        ownPersonas.Poll();

        PersonaCatalog.Own = () => [.. ownPersonas.Cores.Select(core => core.AsPersona())];

        // Built before the registry, because the persona capability declares settings rows from
        // it and which rows exist has to be settled before registration — descriptors are
        // registered once and never mutated (architecture.md D5).
        // Handed somewhere to remember its introductions, so a core says its opening line to
        // this Commander once rather than once per launch. Forgetting them is now the only way
        // back, which is what the row's help had to stop promising a restart would do.
        var personas = new PersonaHost(
            PersonaCatalog.Resolve(settings.Current.Persona.Id),
            new ViewStateIntroductions(viewState));

        // The help capability answers from the registry it is itself registered in, so the
        // accessor is filled in immediately after Build. A Func rather than a mutable property
        // on the descriptor: descriptors are registered once and never mutated (architecture.md
        // D5), and that rule is what keeps tool schemas byte-identical across turns.
        CapabilityRegistry? built = null;

        // When the Commander was last understood. Written by the turn path once the host
        // exists, read by the listening capability, so it is a box rather than a field.
        var heardAt = new StrongBox<DateTimeOffset?>(null);

        // Off unless D47_COVERAGE=1. Created before the registry because when it is on it adds a
        // row, and which rows exist has to be settled before registration — descriptors are
        // registered once and never mutated.
        var coverage = D47.App.Coverage.CoverageRecorder.Create(
            paths,
            () => DateTimeOffset.Now,
            loggerFactory.CreateLogger<D47.App.Coverage.CoverageRecorder>());

        var galaxy = new D47.Knowledge.SpanshGalaxyService(
            loggerFactory.CreateLogger<D47.Knowledge.SpanshGalaxyService>());

        var routePlanner = new D47.Knowledge.SpanshRouteService(
            loggerFactory.CreateLogger<D47.Knowledge.SpanshRouteService>());

        // The key is read on every call rather than captured here, so pasting one in or clearing
        // it takes effect without a restart — the same rule the galaxy service's setting follows.
        var communityGoals = new D47.Knowledge.InaraCommunityGoalService(
            () => secrets.TryGet(CommunityGoalCapability.KeySecretName, out var key) ? key : null,
            version,
            loggerFactory.CreateLogger<D47.Knowledge.InaraCommunityGoalService>());

        var capabilities = CapabilityRegistry.Build(
            BuiltinCapabilities.All(
                paths,
                verbosity,
                gameState,
                settings,
                llmAvailability,
                spend,
                version,
                new SpeechCapability.SpeechSurface
                {
                    Silence = audio.Silence,
                    Beds = () => [.. (self?.Cues ?? cues).BedNames],
                    BedLabel = name => (self?.Cues ?? cues).IsCustom(name) ? $"{name} (yours)" : name,
                    OutputDevices = () => [.. WasapiAudioSink.Devices().Select(device => device.Id)],
                    DeviceLabel = id => WasapiAudioSink.Devices()
                        .FirstOrDefault(device => device.Id == id).Name ?? id,

                    // Late-bound like the headset surface below, and for the same reason: the
                    // list is fetched from the provider over the network after this point.
                    Voices = () => self?.VoiceIds() ?? [],
                    VoiceLabel = id => self?.VoiceLabelFor(id) ?? id,
                    WhyNoVoices = () => self?.WhyNoVoices(),
                    SpeechSpend = () => self?.SpeechSpend,
                    HasKey = () => self is not { } host
                                   || host.HasKeyFor(TtsProviderCatalog.Selected(settings.Current.Speech.Provider)),
                    Audition = (voiceId, role, token) => self is { } host
                        ? host.AuditionVoiceAsync(voiceId, role, token)
                        : Task.CompletedTask,

                    // Late-bound like the two above, because the check is a network call made by
                    // a host that does not exist yet at this point in composition.
                    VerifyKey = (provider, token) => self is { } host
                        ? host.VerifySpeechKeyAsync(provider, token)
                        : Task.FromResult(SecretCheck.Unreachable("D47 is still starting up.")),
                },
                cancellation,
                callouts,
                () => built ?? throw new InvalidOperationException(
                    "Spoken help was asked what D47 can do before the registry finished building."),
                new ListeningCapability.ListeningSurface
                {
                    InputDevices = () => [.. WasapiMicrophone.Devices().Select(device => device.Id)],
                    DeviceLabel = id => WasapiMicrophone.Devices()
                        .FirstOrDefault(device => device.Id == id).Name ?? id,
                    CaptureState = () => (microphone.IsCapturing, microphone.Unavailable),
                    DefaultDeviceName = WasapiMicrophone.DefaultDeviceName,

                    // The demonstration beats any assertion about device state: if words
                    // arrived recently, hearing works, and that is the answer. Boxed because
                    // the surface is built before the host exists, the same late-binding the
                    // route accessor uses.
                    SinceHeard = () => heardAt.Value is { } heard ? DateTimeOffset.Now - heard : null,

                    TranscriberState = () => (
                        transcriber.IsReady,
                        transcriber.Model,
                        transcriber.Unavailable ?? "No speech model is selected."),
                    Binds = () => binds.Current,
                    InstalledModels = () => models.Installed(),

                    // What the gate policy is actually doing, which is the question a Commander
                    // running hands free is asking when they ask this one (list.md Phase 13).
                    Microphone = () => gate.State,
                    EchoState = () => (echo.IsActive, echo.Unavailable),
                    WakeWords = () => self?.Wake.Phrases ?? [],

                    // So the status says "[" where the settings row already does. Several of
                    // these values carry two names and ToString picks whichever it finds
                    // first, which is how a correctly bound key reported itself as "Oem4".
                    KeyLabel = Input.Gestures.Describe,
                },
                // Late-bound for the same reason spoken help's registry accessor is: the
                // headset path needs a dispatcher and a widget tree, so it does not exist
                // yet. What the capability reports before then is the truth anyway - d47 is
                // still looking.
                new VrCapability.HeadsetSurface
                {
                    Report = () => self?.Vr is { } vr
                        ? (vr.State, vr.Reason)
                        : (Core.Vr.VrState.Connecting, "Looking for a headset."),
                    Reanchor = () => self?.Vr?.Reanchor() ?? 0,
                },
                actionSurface = new ActionSurface
                {
                    Binds = () => binds.Current,

                    Status = () => status.Current,
                    Input = gameInput,
                    Enabled = () => settings.Current.Actions.Keyboard,
                },
                () => AutonomousCapability.Describe(autonomous),
                new NavigationSurface
                {
                    Clipboard = new DesktopClipboard(loggerFactory.CreateLogger<DesktopClipboard>()),
                    Actions = actionSurface,
                    AutoPlotEnabled = () => settings.Current.Actions.AutoPlot,
                    ConfirmPlot = (system, token) => ConfirmPlot(route, system, token),
                },
                macros,
                personas,
                checklists,
                () => (self?.Cues ?? cues).DescribeDrops(),
                coverage is null ? null : () => coverage.Report().Summary,

                // Constructed unconditionally and gated by its setting rather than by whether it
                // exists: the row that turns it on has to work without a restart, and a service
                // built only when the setting was already true could not (list.md Phase 4).
                galaxy,
                routePlanner,
                communityGoals,
                () => DateTimeOffset.Now,

                // Late-bound like the surfaces above: the check is a real network call and the
                // host that makes it does not exist yet at this point in composition.
                (provider, token) => self is { } host
                    ? host.VerifyLanguageModelKeyAsync(provider, token)
                    : Task.FromResult(SecretCheck.Unreachable("D47 is still starting up.")),

                // Where the Commander is standing, which only Status.json knows — ScanOrganic
                // carries no position at all (list.md Phase 18).
                () => status.Current,

                // The same window object the injector asks about before every key. One thing
                // knows how to find Elite, and now it also knows how to raise it — a second
                // finder would be a second answer to "is that Elite" and they would disagree.
                eliteWindow.Raise,

                new SwitchSurface
                {
                    Mappings = () => switches.Switches,
                    States = () => reconciler.States,
                    Unavailable = () => controllers.Unavailable,
                    Problems = () => switches.Problems,
                },
                lore,

                // The Commander's timers and alarms (list.md Phase 24). The store polls itself
                // from the tick below, so an alarm edited by hand is live without a restart.
                timekeeper,

                // How to present an instant locally. Asked each time rather than captured, so a
                // Commander whose machine changes zone mid-session sees it without restarting.
                () => TimeZoneInfo.Local,
                shipPlans,

                // And the suit and weapon plans beside them (list.md Phase 27). Two stores rather
                // than one, because the game separates ship and on-foot hard.
                onFootPlans,

                // Which engineer to go and get next, read across both of them (list.md Phase 28).
                unlocks,

                // The endpoint half of web search, for the egress row. Asked each time because
                // the Commander can retarget `llm.endpoint` without restarting, and the row is
                // computed at render time so it has to be able to change underneath.
                () => self?.SearchReachesTheWeb ?? true,

                // What the endpoint said it serves (list.md Phase 29). Empty until the handshake
                // answers, which is the state the model picker was designed for from Phase 4 —
                // the row accepts free text, so an empty list has always been a supported answer
                // rather than a broken one.
                () => self?.EndpointModelIds ?? [],

                // What d47 remembers about the Commander (list.md Phase 31). Read by two
                // capabilities — its own, and the privacy section, which is where emptying it lives
                // rather than in a second place to look.
                memoryBook,

                // And what it has noticed about them (list.md Phase 32). Read by two capabilities
                // as well — its own, and privacy, which is where throwing it away lives.
                habitBook,

                // What the "read my journals" button does (list.md Phase 32).
                () => MineHabits,

                // Turning a session into something worth keeping (list.md Phase 33).
                logbook,

                // The campaigns that outlive a checklist (list.md Phase 34).
                goalBook,

                // What the "read my journals" button does for the arcs' ages.
                () => BackfillGoals,

                // Which core flies which ship (list.md Phase 35). Read by the persona capability
                // for its two rows, its two protected tools, and the one sentence the model is
                // allowed to know about a binding.
                shipCores));

        built = capabilities;

        // The one late-bound edge in the composition: descriptors declare the settings rows and
        // some descriptors read settings, so the row table is supplied once the registry exists.
        settings.Bind(capabilities);

        logger.LogInformation(
            "Registered {Count} capabilities exposing {ToolCount} tools",
            capabilities.All.Count,
            capabilities.ToolNames.Count());

        var updates = new UpdateChecker(loggerFactory.CreateLogger<UpdateChecker>());
        var installer = new UpdateInstaller(paths, loggerFactory.CreateLogger<UpdateInstaller>());

        // The moment the retired build is no longer the running image is startup, so this is the
        // first chance to delete what a previous update left behind.
        if (Environment.ProcessPath is { } runningExecutable)
        {
            installer.CleanUpRetired(runningExecutable);
        }

        var router = new KeywordRouter(capabilities, () => MacroCapability.Phrases(macros));

        var turns = new TurnLoop(
            capabilities,
            router,
            llmAvailability,
            spend,
            PriceTable.Default,
            loggerFactory.CreateLogger<TurnLoop>(),
            settings: settings)
        {
            // Asked once per turn rather than assigned, so the state the model sees is the state
            // as of the moment the prompt was built — not as of whenever something last pushed
            // it in. The tick loop is folding events continuously underneath this.
            // Both halves sit at prompt position 7, below the cache breakpoint, which is what
            // lets the reachable action set change several times a minute without touching a
            // byte of the cached prefix.
            // The mode picks the profile; the setting decides whether any action tool ships at
            // all. Both asked per turn, because both change underneath this.
            ToolContext = () => actionSurface.Context,
            ActionsEnabled = () => settings.Current.Actions.Keyboard,
            WebSearchEnabled = () => settings.Current.Llm.WebSearch,

            // A proposal the Commander has not answered, stated by the store rather than by the
            // model (remediation.md 10, item 10). Asked either side of the turn, so it is silent
            // on the turn that resolves it.
            Standing = checklists.Standing,

            LiveGameState = () => Join(
                Situation.Describe(gameState.Active),
                Join(
                    ActionCapabilities.Describe(actionSurface),
                    Join(
                        MacroCapability.Live(macros),
                        Join(
                            ChecklistCapability.Live(checklists),

                            Join(
                                // Both dates, already worked out, below the cache breakpoint
                                // where a per-turn value costs nothing (list.md Phase 24). The
                                // model is never asked to add 1286 to anything.
                                UtilitiesCapability.Live(
                                    timekeeper, SystemWallClock.Instance.UtcNow, TimeZoneInfo.Local),

                                // Why d47 cannot look something up, when it cannot. Null when it
                                // can, so the Commander who has search on pays nothing for a
                                // sentence about it. Reached through `self` because the provider
                                // belongs to the turn loop this initialiser is still building.
                                ConversationCapability.LiveSearch(
                                    settings.Current.Llm.WebSearch,
                                    self?.SearchReachesTheWeb ?? true)))))),
        };

        var host = self = new AppHost(
            paths,
            router,
            cancellation,
            loggerFactory,
            verbosity,
            settings,
            secrets,
            viewState,
            gameState,
            journal,
            tick,
            callouts,
            capabilities,
            updates,
            installer,
            turns,
            personas,
            shipCores,
            llmAvailability,
            spend,
            spendLedger,
            audioSink,
            audio,
            cues,
            voice,
            gate,
            echo,
            microphone,
            pushToTalk,
            binds,
            models,
            transcriber,
            version,
            startupError);

        // Before ApplyLlmSettings, which reads the persona block it points the loop at.
        personas.Changed += host.OnPersonaChanged;
        turns.UseTranscript(personas.Transcript);

        // Speech reaches the ledger too, or the running totals would look authoritative while
        // covering only what the model cost. Wired here rather than at construction because the
        // rate a charge is priced at is read from settings at the moment it is spoken.
        host.SpeechSpend.LedgerTo(spendLedger, () => settings.Current);

        // The avatar's own imagery, if the Commander has dropped any in. Scanned once at
        // startup; the drawn face is what every state falls back to, so an empty data/avatar is
        // the normal case rather than a missing asset.
        host.Avatars = D47.Core.Interface.AvatarLibrary.Load(paths);

        // The face follows the loop. Set straight onto the view model from whichever thread the
        // state arrived on: a view model is affine to nothing, and the view marshals — which is
        // the rule the transcript scroll already follows, so the avatar does not get a second
        // one of its own.
        voice.StateEntered += state => host.Panel.LoopState = state;

        // And the Technical page gets the same transitions in words. The page's premise is the
        // conversation with the diagnostics left in, and until now the loop it most needs to show
        // reported itself only to a log file (docs/plans/change-requests.md item 6).
        var trace = new SpeechLoopTrace(host.Panel);
        voice.StateEntered += trace.Entered;

        // Errors from the speech path land there too, through the log rather than through a call
        // site: an authored list of stage lines cannot cover the failure nobody has written yet.
        technicalLog.WriteTo(line =>
            host.Panel.Append($"[error] {line}{Environment.NewLine}", TranscriptKind.Technical));

        // That the microphone is open, on both surfaces, as a property of the gate policy rather
        // than of any one capability (list.md Phase 13). Set straight onto the view model from
        // whichever thread the change arrived on, following the same rule the avatar does: a
        // view model is affine to nothing and the view marshals.
        gate.StateChanged += state => host.ShowMicrophone(state);

        // Stated once at startup as well as on every change, because the opening state is the
        // one a Commander sees for longest and nothing had raised an event yet.
        host.ShowMicrophone(gate.State);

        // A voice the provider refuses is written out of settings rather than merely skipped for
        // the turn it broke. Subscribed before the first ApplySpeechSettings, because a stored
        // voice can be refused on the very first thing d47 says.
        voice.VoiceRejected += host.ForgetTheVoice;

        host.ApplyLlmSettings();
        host.ApplySpeechSettings();
        host.ApplyListeningSettings();

        // The mixer as the file left it, before anything is audible.
        audio.Mix = loaded.Audio;

        // From here on, a setting takes effect because it changed — not because something was
        // restarted (list.md Phase 4, "Apply every setting without a restart").
        settings.Changed += host.OnSettingsChanged;

        host.Macros = macros;
        host.OwnPersonas = ownPersonas;
        host.Checklists = checklists;
        host.Timekeeper = timekeeper;
        host.Ships = shipPlans;
        host.ShipBuilds = shipBuilds;
        host.OnFootPlans = onFootPlans;
        host.Unlocks = unlocks;
        host.OnFootBuilds = onFootBuilds;
        host.Alarms = alarms;

        host.SwitchEditing = new Settings.SwitchEditing(
            switches,
            controllers,
            reconciler,
            () => DateTimeOffset.Now,
            Path.Combine(paths.Data, "switch-capture.txt"));
        // Whether a lookup is possible is asked at the moment the window opens rather than
        // captured now: the Commander can change the setting or the provider between launching
        // d47 and writing a note, and the window's own first sentence depends on the answer.
        host.LoreEditing = new Settings.LoreEditing(
            lore,
            () => LoreCapability.PlaceOf(host.GameState.Active),
            () => host.CanSearch,
            host.SearchForAsync,
            () => DateTimeOffset.Now);

        // The store and the clock, together, because a fact typed here is stamped with a real
        // instant and Core reads no clock of its own.
        host.Memories = (memoryBook, () => DateTimeOffset.Now);
        host.Habits = (habitBook, MineHabits);
        host.Logbook = logbook;
        host.Goals = (goalBook, BackfillGoals);

        host.ReservedPhrases = PhrasesAlreadyTaken(capabilities);

        host.CoverageRecorder = coverage;
        coverage?.Follow(capabilities, settings);

        // Captured audio becomes words on the thread pool, never on the audio thread that
        // produced it. Whisper on a CPU takes hundreds of milliseconds for a short clip; doing
        // that inline would stall capture and drop the next utterance.
        gate.Captured += host.TranscribeAsync;

        // The route reader lives in the tick closure, so the host reaches it through this
        // rather than owning it — proper-noun biasing wants the systems the Commander is about
        // to arrive in, and those are only in the route file.
        host._route = () => route.Current;
        host._heardAt = heardAt;

        // Push-to-talk, sampled here rather than hooked. This is the whole reason the tick runs
        // at 10 Hz rather than 4: the period is the worst-case delay before a key-down is seen,
        // and the gate's pre-roll is what absorbs it. See PushToTalkKey for why polling one
        // virtual-key code beats the three alternatives.
        tick.Add("push-to-talk", context =>
        {
            pushToTalk.Poll();

            // Whether the device is actually delivering audio, which only it knows and which is
            // half of what the panel's microphone indicator says. Sampled here rather than
            // raised, because a device disappearing does not always announce itself.
            gate.Capturing = microphone.IsCapturing;

            // Where the hands-free gate opens and closes. The detector runs on the audio thread
            // and records what it decided; this is the thread that acts on it, so a real-time
            // callback never plays a cue or hands an utterance to a transcriber (list.md
            // Phase 13, architecture.md §4).
            gate.Poll(context.Now);
        });

        pushToTalk.Pressed += () => gate.KeyDown(DateTimeOffset.Now);
        pushToTalk.Released += () => gate.KeyUp();

        // That d47 is listening, said both ways. Both signals have existed since their own
        // phases and neither was ever connected to anything: `listening.wav` ships in
        // assets\cues, the avatar has a face for the state, and nothing in the app ever entered
        // it — so holding push-to-talk, and worse, *toggling* it on, looked and sounded exactly
        // like not holding it. The gate was built expecting this: Open() raises Started outside
        // its lock precisely so a subscriber can play the cue.
        gate.Started += () => host.Voice.EnterState(Core.Audio.LoopState.Listening);

        // Only the discarded case. Captured fires before Ended and takes the loop into
        // Transcribing itself, but a press too short to be speech captures nothing at all, so
        // without this the loop would sit on Listening until the next thing to happen.
        gate.Ended += reason =>
        {
            if (reason == UtteranceEnd.TooShort)
            {
                host.Voice.EnterState(Core.Audio.LoopState.Idle);
            }
        };

        // The async half of a synchronous tick. Callouts are produced on the tick thread, which
        // must not block, and spoken here on the thread pool — so a slow TTS synthesis cannot
        // stall push-to-talk edge detection or the journal poll behind it.
        //
        // Registered after the priming tick above, which is belt and braces: the engine already
        // refuses to queue anything while priming, so there is nothing here to drain from the
        // backlog even if this ran first.
        // Registered here rather than beside the journal readers because it needs both the
        // store and the finished registry, and neither exists that early.
        tick.Add("macros", _ => macros.Poll(PhrasesAlreadyTaken(capabilities)));

        // Same shape, same reason: a file Elite owns, re-read only when it moves. A Commander who
        // rebinds a control in the game's own options menu had, until now, to restart d47 before
        // it knew (remediation.md 16, item 2).
        tick.Add("binds", _ => binds.Poll());

        // The switch path, in the tick's own shape: read the file if it changed, read the
        // hardware, decide. Nothing here presses anything — the drain below does that, on the
        // thread pool, for the same reason the autonomous drain does (list.md Phase 21).
        tick.Add("switches", context =>
        {
            switches.Poll();

            reconciler.Poll(
                new SwitchTick
                {
                    Now = context.Now,
                    Readings = controllers.Poll(),
                    Status = status.Current,
                    Binds = bindsRef!(),

                    // Gated by key injection as well as by its own row. A Commander who has not
                    // allowed d47 to press keys at all has not allowed it for switches either.
                    Enabled = settings.Current.Actions.Keyboard && settings.Current.Actions.Switches,
                },
                switches.Switches);

            // The annunciator, on whichever surfaces are up. Recomputed here rather than bound,
            // because it is a projection of two things that move independently — where the
            // switch is, and what the game says.
            host.ShowSwitches(SwitchCapability.Annunciator(reconciler.States));
        });

        // The first thing d47 does that nothing external triggers (list.md Phase 24). Every other
        // subscriber here is reacting to something that arrived; this one asks whether time has
        // passed. The now comes from the tick's own context, which is what keeps the clock rule
        // and lets the replay harness run a day of alarms in a second.
        tick.Add("reminders", context =>
        {
            alarms.Poll();
            host.SoundReminders(timekeeper.Poll(context.Now));
        });

        // Ship builds are hand-editable, and buying a hull the Commander had planned for offers
        // to adopt the plan onto it rather than making them re-point it (list.md Phase 26). Its
        // own subscriber rather than a line in the journal one, because it needs the host that
        // does not exist when that closure is written.
        // The context is unused: what this reads is the events the journal subscriber above put
        // in `arrived` a moment ago, and the store polls itself.
        tick.Add("ships", _unused =>
        {
            shipBuilds.Poll();
            onFootBuilds.Poll();

            // Both halves of the same offer. On foot the buy event carries the id, which is the
            // opposite of the ship side - ShipyardBuy names no id for the new hull at all and the
            // ShipyardNew written after it does (list.md Phase 27).
            foreach (var adopted in shipPlans.Observe(arrived).Concat(onFootPlans.Observe(arrived)))
            {
                host.Panel.Append($"{adopted}{Environment.NewLine}");
                _ = host.Voice.AnnounceAsync(adopted);
            }
        });

        // A core per ship (list.md Phase 35). The store is read first so a hand edit is live, then
        // the ship the Commander is in is compared against the one whose binding is already
        // aboard. Nothing here writes a binding: this reads what they already said.
        //
        // Registered after the loop was primed, which is deliberate — the first ship this sees is
        // "the ship d47 found them in", and that one is adopted silently rather than announced.
        tick.Add("ship cores", context =>
        {
            shipCoreStore.Poll();

            if (shipCores.Observe(context.Since) is { } due)
            {
                host.PutCoreAboard(due);
            }
        });

        // What d47 remembers, on the tick like every other store (list.md Phase 31). Four things,
        // and the order matters: the file is read first so a hand edit is live, then the journal's
        // two observations are written if they changed, then expiry runs on a coarse interval, and
        // only then is the recall block recomputed — so the block reflects this tick's store rather
        // than the last one's.
        var expiredAt = DateTimeOffset.MinValue;

        tick.Add("memory", context =>
        {
            memories.Poll();

            if (!settings.Current.Memory.Enabled)
            {
                // Off means no new writes and nothing reaching the prompt. It does not mean the
                // file is emptied — that is its own action, in the privacy section, and it says so.
                host.ApplyRecall(null);
                return;
            }

            memoryObserver.Observe(gameState.Active, context.Now);
            memoryObserver.Touch(gameState.Active, context.Now);

            // Once at startup and then rarely. Expiry is a boundary crossing, not an event, so
            // asking ten times a second would be ten times a second of reading a file to learn
            // that nothing has aged out.
            if (context.Now - expiredAt >= ExpiryEvery)
            {
                expiredAt = context.Now;
                host.ReportExpiredMemories(
                    memoryBook.Expire(context.Now, MemoryCapability.ExpiryOf(settings.Current.Memory)),
                    context.IsFirst);
            }

            host.ApplyRecall(memoryBook.Recall());
        });

        // What d47 has noticed, on the tick for the same reason the memory store is: the file is
        // hand-editable, so a claim dropped in a text editor is live without a restart. Nothing is
        // mined here — that is a button, and the whole point of item 1 is that it costs nothing
        // while flying.
        tick.Add("habits", _ => habits.Poll());

        // The arcs, on the tick for the same reason: goals.json is hand-editable, so a goal typed
        // into it is live without a restart. Nothing is walked here — that is a button.
        tick.Add("goals", _ => goals.Poll());

        tick.Add("callout-drain", _ => host.SpeakPendingCallouts());

        // Ambience follows the situation Status.json states, sampled on the tick rather than
        // hooked to a journal event: docked, supercruise and on foot are conditions rather than
        // things that happen, and the file is already being read here every tick.
        tick.Add("ambience", _ => host.FollowSituation(status.Current));

        // And the folder those tracks came from, which the Commander can add to while d47 is
        // running (list.md Phase 12, "Pick up dropped-in audio without a restart"). The same
        // shape the journal reader uses, and for the same reason: nothing here owns a thread or
        // a file watcher, so the tick drives it in production and a test calls Poll directly.
        tick.Add("audio-folder", context => host.RescanAudio(context, drops, cueLogger));

        // NPC voices are scoped to the system, so something has to notice the system changing.
        // Sampled on the tick rather than hooked to a journal event, because the state store is
        // already folding those and a second reader would be a second thing to keep in step.
        tick.Add("voice-scope", _ => host.FollowSystemForVoices());

        // After the callouts, so a honk that reports why it did not fire is spoken in the same
        // order it was decided relative to everything else this tick.
        tick.Add("autonomous-drain", _ => host.CarryOutPendingActions(autonomous, gameInput));

        // After the autonomous drain and for the same reason it exists: the tick is synchronous
        // and a key press is not. A flip decided on this tick is carried out on the thread pool.
        tick.Add("switch-drain", _ => host.CarryOutReconciles(reconciler, gameInput));

        // Last, so every subscriber registered during composition is in place before the first
        // timer-driven tick — and so a failure above happens against a loop that never started
        // rather than one already running against half-built state.
        host._ticking = new TickDriver(tick, loggerFactory.CreateLogger<TickDriver>()).Start();

        return host;
    }

    /// <summary>
    /// The callouts d47 ships with, in the order they are examined. Declaration order is
    /// announcement order within one tick, which is why danger comes first: an interdiction and
    /// a route progress report arriving together should not be spoken the other way round.
    /// </summary>
    private static CalloutEngine BuildCallouts(
        D47Settings settings,
        ILoggerFactory loggers,
        ChecklistService checklists,
        LoreBook lore,
        LoreVisits loreVisits,
        MemoryBook memories,
        D47.Core.Habits.HabitBook habits,
        Func<D47.Core.Engineers.EngineerPlanService?> unlocks)
    {
        var engine = new CalloutEngine(loggers.CreateLogger<CalloutEngine>())
            .Add(new DangerCallout())

            // Above everything except danger itself (list.md Phase 15). It is the only warning
            // here that arrives before the thing it warns about, and it has a median of six to
            // eight seconds to be useful in.
            .Add(new AnnouncedAttackCallout())
            .Add(new FuelCallout())
            .Add(new RouteCallout())
            .Add(new LongJumpCallout())
            .Add(new ArrivalCallout())

            // Capacity comes from the derived grade table. Elite reports it nowhere, so this is
            // the one place d47 carries game data — generated from the canonical id list rather
            // than written, and answering null for anything it does not recognise.
            .Add(new MaterialMilestoneCallout { Capacity = MaterialGrades.CapacityOf })

            // Phase 11. The carrier speaks for itself; incoming chat speaks for whoever sent
            // it. Both are announcements in somebody else's voice rather than d47's, which is
            // what Announcement.Voice exists to carry.
            .Add(new CarrierCallout())

            // Phase 17. A computed tick going backwards is information rather than a glitch to
            // hide, and it is said once — the recomputed verdict is written down as it is
            // announced. Below the danger family, because a plan item un-completing can wait for
            // the shooting to stop.
            .Add(new SamplingCallout())
            .Add(new ProspectorCallout())
            .Add(new CoreAsteroidCallout())
            .Add(new ChecklistCallout(checklists))

            // Low on purpose. It is a standing condition rather than news, and it stands down for
            // anything above it — a remark about enemy territory arriving as somebody opens fire
            // is worse than silence (list.md Phase 15).
            .Add(new RivalTerritoryCallout())

            // Phase 23. Below the warnings and above the ambient line: it is news, but it is news
            // about a place that will still be there in a minute.
            .Add(new LoreCallout(lore, loreVisits))

            // Phase 31, and the lowest thing here that is not the ambient line: it fires once, at
            // the start of a session, and it is about what was true before the Commander sat down.
            // Everything above it is about now.
            .Add(new ContinuityCallout(memories, checklists, unlocks))

            // Phase 32, and below the continuity line for the same reason it is below everything
            // else: it is an observation about the Commander rather than about the world, and
            // nothing d47 worked out about somebody outranks something the game just said. Off
            // until the Commander switches it on — the only callout here that ships that way.
            .Add(new HabitCallout(habits))
            .Add(new AmbientCallout())
            .Add(new IncomingMessages
            {
                Enabled = () => settings.Speech.SpeakIncomingMessages,
                IncludeNpcs = () => settings.Speech.SpeakNpcMessages,
            });

        // Elite echoes what you send back to you on the channel it went out on. Without this,
        // dictating into wing chat means hearing yourself read back in a stranger's voice.
        // Filled in on the tick rather than here, because the journal has not been read yet.

        ApplyCalloutSettings(engine, settings);
        return engine;
    }

    /// <summary>
    /// Pushes the callout settings into the engine and into the individual callouts that carry
    /// a tunable. Called at startup and on any change, so the two paths cannot drift.
    /// </summary>
    private static void ApplyCalloutSettings(CalloutEngine engine, D47Settings settings)
    {
        var callouts = settings.Callouts;

        engine.Enabled = callouts.Enabled;
        engine.SetEnabled("danger", callouts.Danger);
        engine.SetEnabled("fuel", callouts.Fuel);
        engine.SetEnabled("route", callouts.Route);
        engine.SetEnabled("long-jump", callouts.LongJump);
        engine.SetEnabled("arrival", callouts.Arrival);
        engine.SetEnabled("materials", callouts.Materials);
        engine.SetEnabled("announced-attack", callouts.AnnouncedAttack);
        engine.SetEnabled("rival-territory", callouts.RivalTerritory);
        engine.SetEnabled("sampling", callouts.Sampling);
        engine.SetEnabled("prospector", callouts.Prospector);
        engine.SetEnabled("core-asteroid", callouts.CoreAsteroid);
        engine.SetEnabled("checklist", callouts.Checklist);
        engine.SetEnabled("ambient", callouts.Ambient);
        engine.SetEnabled("continuity", callouts.Continuity);
        engine.SetEnabled("habits", callouts.Habits);

        foreach (var callout in engine.Callouts)
        {
            switch (callout)
            {
                case RouteCallout route:
                    route.EveryNJumps = callouts.RouteEveryNJumps;
                    break;

                case LongJumpCallout longJump:
                    longJump.Threshold = TimeSpan.FromSeconds(callouts.LongJumpSeconds);
                    break;

                case ArrivalCallout arrival:
                    arrival.HomeSystem = callouts.HomeSystem;
                    break;

                case LoreCallout lore:
                    // Read through the settings the switch was handed rather than captured once,
                    // so a Commander who turns the lookup off is obeyed on the next arrival
                    // rather than on the next launch — the same shape the ambient row below has.
                    lore.Remarks = () => callouts.Lore;
                    break;

                case AmbientCallout ambient:
                    ambient.Interval = TimeSpan.FromSeconds(callouts.AmbientSeconds);

                    // Silent while personality is off. The checklist puts "no ambient remarks"
                    // in that item's own acceptance criteria, which makes this the one callout
                    // the personality switch reaches.
                    ambient.Enabled = () => settings.Callouts.Ambient && settings.Llm.PersonalityEnabled;
                    break;
            }
        }
    }

    /// <summary>
    /// Where the Anthropic key lives in the secret store. DPAPI-encrypted, scoped to this
    /// Windows account, and never written to a log.
    /// </summary>
    public const string AnthropicApiKeySecret = "anthropic.apiKey";

    /// <summary>
    /// Rebuilds everything downstream of the language model settings: the provider itself, the
    /// pinned model, the standing About Me text, and whether the model capability is on at all.
    /// Called at startup and again whenever one of those settings changes, so the two paths
    /// cannot drift.
    /// </summary>
    private void ApplyLlmSettings()
    {
        var current = Settings.Current;
        var selected = LlmProviderCatalog.Selected(current.Llm.Provider);

        ILlmProvider? provider = null;
        string? reason = null;

        if (selected.Id == LlmProviderCatalog.NoneId)
        {
            reason = "No language model is selected — that is a setting, not a fault.";
        }
        else
        {
            // The key may legitimately be absent. A provider whose key is optional is a complete
            // configuration with an empty box — a model on this machine has no account to get a
            // key from — so the resolution is attempted and its absence is only fatal if the
            // factory says so (list.md Phase 29).
            var resolved = ResolveKey(selected);

            provider = LlmProviderFactory.Create(selected, resolved?.Key, current.Llm.Endpoint);

            if (provider is null)
            {
                reason = LlmProviderFactory.ReasonForNoClient(selected);
            }
            else
            {
                _logger.LogInformation(
                    "{Provider} configured from {Source}, endpoint {Endpoint}",
                    selected.Name,
                    resolved?.Source ?? "no key, which this provider does not require",
                    current.Llm.Endpoint ?? selected.DefaultEndpoint ?? "(provider default)");
            }
        }

        RefreshEndpointModels(provider, current.Llm.Endpoint);

        Turns.Provider = provider;
        Turns.Model = current.Llm.Model;
        Turns.AboutMe = current.Llm.AboutMe;

        // Position 3 of the assembled prompt, and null when personality is off. Null rather
        // than a neutral block on purpose: "off" is position 3 being absent, and the guardrails
        // at position 2 are untouched either way, which is the property that whole arrangement
        // exists to guarantee (architecture.md §6).
        Turns.Persona = Personas.RenderBlock(current.Llm.PersonalityEnabled);

        LlmAvailability.SetProviderConfigured(provider is not null, reason);
    }

    /// <summary>
    /// Puts the recall block into the prompt, and <b>only when it has actually changed</b> (list.md
    /// Phase 31, "Recall arrives above the cache breakpoint").
    /// <para>
    /// <b>The comparison is the whole point of this method existing.</b> Recall sits above the cache
    /// breakpoint, so assigning it invalidates the entire cached prefix — the 39,000-odd bytes of
    /// tool schemas serialize first and go cold with it. This runs ten times a second and almost
    /// always produces the text that is already there, so an unconditional assignment would be a
    /// cold prefix on every turn, which is the exact cost the placement was chosen to avoid.
    /// </para>
    /// <para>
    /// <see cref="MemoryRecall"/> holds up its end: the rendered text carries no system, no ship and
    /// no live figure, so flying through twenty systems d47 remembers nothing about renders
    /// identically twenty times and this assigns nothing.
    /// </para>
    /// </summary>
    private void ApplyRecall(string? recall)
    {
        if (string.Equals(Turns.Recall, recall, StringComparison.Ordinal))
        {
            return;
        }

        Turns.Recall = recall;

        _logger.LogInformation(
            "Recall block {State} ({Bytes} characters)",
            recall is null ? "cleared" : "changed",
            recall?.Length ?? 0);
    }

    /// <summary>
    /// Says what an expiry took, when it took something worth saying (list.md Phase 31, "Forgetting
    /// is said out loud when it matters").
    /// <para>
    /// <b>Only the Commander's own words.</b> An observation aging out is d47 forgetting where
    /// somebody parked four months ago, and an inference aging out is d47 forgetting something it
    /// made up — neither is worth a sentence. A fact a person typed disappearing without a word is
    /// the failure the item exists to prevent, and it is the one case where a Commander would
    /// otherwise find out by noticing d47 no longer knew something.
    /// </para>
    /// </summary>
    /// <param name="priming">
    /// True on the startup tick. The expiry still <em>happens</em> — the file is the file — but it is
    /// not announced, for the reason no callout announces a backlog: the Commander has just launched
    /// d47 and a list of things it has forgotten is not what a session should open with. It reaches
    /// the panel instead.
    /// </param>
    private void ReportExpiredMemories(IReadOnlyList<MemoryEntry> expired, bool priming)
    {
        var told = expired.Where(entry => entry.Tier == MemoryTier.Stated).ToArray();

        if (told.Length == 0)
        {
            return;
        }

        var line = told.Length == 1
            ? $"I have forgotten something you told me, because it was past its expiry: {told[0].Fact}"
            : $"I have forgotten {told.Length} things you told me, because they were past their expiry. "
              + $"The oldest was: {told[^1].Fact}";

        Panel.Append($"{line}{Environment.NewLine}");

        if (!priming)
        {
            _ = Voice.AnnounceAsync(line);
        }
    }

    /// <summary>
    /// Rebuilds everything downstream of the speech settings: the voice provider, the voice
    /// itself, the cues, the bed, the output device and the retry policy. Called at startup and
    /// again on any change, so the two paths cannot drift (list.md Phase 4, "Apply every
    /// setting without a restart").
    /// </summary>
    /// <summary>
    /// What the language-model endpoint said it serves (list.md Phase 29). Empty until a
    /// handshake has answered, and empty again for a provider with no endpoint to ask.
    /// </summary>
    internal IReadOnlyList<string> EndpointModelIds => _endpointModels;

    /// <summary>
    /// Asks the endpoint what it serves, if it is the kind of thing that can be asked and has not
    /// been asked already (list.md Phase 29).
    /// <para>
    /// <b>Fire and forget, and only on a change of address.</b> Settings are re-applied on every
    /// edit to any row — a persona switch, a volume change — and a handshake on each of those
    /// would put a network call behind moving a slider. The address is what the answer depends
    /// on, so the address is what triggers it.
    /// </para>
    /// <para>
    /// The old list is cleared first rather than left standing. A model id belongs to its
    /// endpoint's namespace, and showing one endpoint's models under another's address is the
    /// stale selection the picker's contract exists to prevent.
    /// </para>
    /// </summary>
    private void RefreshEndpointModels(ILlmProvider? provider, string? endpoint)
    {
        var asking = provider switch
        {
            ChatCompletionsLlmProvider chat => chat.ListModelsAsync,
            ResponsesLlmProvider responses => responses.ListModelsAsync,
            _ => (Func<CancellationToken, Task<EndpointModels>>?)null,
        };

        var address = $"{provider?.Id}|{endpoint}";

        if (asking is null)
        {
            _endpointModels = [];
            _endpointModelsFor = null;
            return;
        }

        if (string.Equals(_endpointModelsFor, address, StringComparison.Ordinal))
        {
            return;
        }

        _endpointModels = [];
        _endpointModelsFor = address;

        _ = Task.Run(async () =>
        {
            try
            {
                var models = await asking(CancellationToken.None).ConfigureAwait(false);

                // Only if the address has not moved again while this was in flight. A slow
                // handshake landing after the Commander retargeted the row would fill the picker
                // with the previous endpoint's models, which is the exact staleness this avoids.
                if (string.Equals(_endpointModelsFor, address, StringComparison.Ordinal))
                {
                    _endpointModels = models.Ids;
                }

                _logger.LogInformation(
                    "The endpoint answered {Reach} with {Count} models{Detail}",
                    models.Reach,
                    models.Ids.Count,
                    models.Detail is { Length: > 0 } said ? $": {said}" : string.Empty);
            }
            catch (Exception ex)
            {
                // No list is a capability being partly off rather than a failure: the model row
                // still accepts a name typed in, which is what it did before there was anybody
                // to ask.
                _logger.LogWarning(ex, "Could not ask the endpoint which models it serves");
            }
        });
    }

    /// <summary>The ids the voice picker offers.</summary>
    internal IReadOnlyList<string> VoiceIds() => [.. _voices.Voices.Select(voice => voice.Id)];

    /// <summary>
    /// How the picker labels one — "Ava — Female, en-US" rather than the raw id. Falls back to
    /// the id, so a voice the Commander typed themselves still shows as what they typed.
    /// </summary>
    /// <summary>
    /// The voice's name on its own — "George" — or null when the catalogue does not know it, which
    /// is the case for an id the Commander typed themselves and for every voice before the list
    /// has been fetched. Null rather than the id, so the caller decides what to show
    /// (remediation.md 10, item 9).
    /// </summary>
    internal string? VoiceNameFor(string id) =>
        _voices.Voices.FirstOrDefault(voice => string.Equals(voice.Id, id, StringComparison.OrdinalIgnoreCase))
            ?.Name;

    internal string VoiceLabelFor(string id) =>
        _voices.Voices.FirstOrDefault(voice => string.Equals(voice.Id, id, StringComparison.OrdinalIgnoreCase))
            ?.Label ?? id;

    /// <summary>
    /// Why the voice picker has nothing in it, when it has nothing in it (list.md Phase 19;
    /// docs/spikes/elevenlabs-voice-sources.md §3).
    /// <para>
    /// Four situations used to arrive as one empty list and one generic sentence telling the
    /// Commander to type a value — which, for a voice id, they have no way of knowing. The
    /// provider now says which of the four it was and this passes it on unchanged.
    /// </para>
    /// <para>
    /// Null while no provider is selected: the row is absent then, and a sentence for a row that
    /// is not on screen is a sentence nobody reads.
    /// </para>
    /// </summary>
    internal string? WhyNoVoices()
    {
        var provider = TtsProviderCatalog.Selected(Settings.Current.Speech.Provider);

        return provider.Speaks ? _voices.WhyEmpty(provider.Name) : null;
    }

    /// <summary>
    /// One voice per core, chosen once and written to settings (list.md Phase 11, #33).
    /// <para>
    /// Guarded by a flag rather than by "are there pairings yet", so a Commander who cleared
    /// every pairing by hand does not have them silently regenerated on the next launch. Runs
    /// after the voice list arrives and never blocks anything: picking a character must not wait
    /// on a model being reachable.
    /// </para>
    /// </summary>
    /// <summary>
    /// A voice for the core aboard, chosen now if it has none (list.md Phase 11, #33).
    /// <para>
    /// The lazy half of the pairing, and the half that answers what a Commander actually
    /// notices: selecting a core they have never used should sound like that core, not like the
    /// last one. The pass at startup covers the cast in one call, but it only ran once and only
    /// with whatever was configured at the time — no model, no key, no voice list yet — and a
    /// core it missed had no second chance before this.
    /// </para>
    /// <para>
    /// Awaited by the caller rather than fired off, because the point is the line that is about
    /// to be spoken. With no model and no named default it returns immediately, having written
    /// nothing: choosing a voice for a character is a judgement, and d47 does not guess at one.
    /// </para>
    /// </summary>
    private async Task EnsureVoiceForCurrentPersonaAsync()
    {
        var persona = Personas.Current;

        if (_voices.Count == 0 || Settings.Current.Persona.Voices.ContainsKey(persona.Id))
        {
            return;
        }

        try
        {
            var voice = await VoicePairing.ChooseOneAsync(
                persona,
                _voices.Voices,
                Settings.Current.Persona.Voices.Values,
                Turns.Provider,
                Turns.Model,
                Spend,
                PriceTable.Default,
                _logger,
                TtsProviderCatalog.Selected(Settings.Current.Speech.Provider).Id).ConfigureAwait(false);

            if (voice is null)
            {
                return;
            }

            Settings.Replace("persona.voices", current => current with
            {
                Persona = current.Persona with
                {
                    Voices = new Dictionary<string, string>(current.Persona.Voices, StringComparer.Ordinal)
                    {
                        [persona.Id] = voice,
                    },
                },
            });

            // Nothing else will notice: the pairing is not a settings row, and the core aboard
            // has just acquired the voice it is about to speak in.
            ApplySpeechSettings();
        }
        catch (Exception ex)
        {
            // A convenience, exactly like the pass at startup. Failing it means this core speaks
            // in the voice already in force, which is where it started.
            _logger.LogWarning(ex, "Could not choose a voice for {Persona}", persona.Id);
        }
    }

    /// <summary>
    /// Drops any pairing that has a core speaking in the wrong gender, once, and gives that core
    /// another voice in the same breath.
    /// </summary>
    private async Task RepairMiscastVoicesAsync()
    {
        if (Settings.Current.Persona.VoicesGenderChecked || Turns.Provider is null)
        {
            return;
        }

        var before = Settings.Current.Persona.Voices;

        var repair = await WithReplacementsAsync(
            before,
            VoicePairing.WithoutMiscastVoices(before, _voices.Voices, _logger)).ConfigureAwait(false);

        Settings.Replace("persona.voices", current => current with
        {
            Persona = current.Persona with
            {
                Voices = repair.Voices,
                VoicesGenderChecked = repair.Complete,
            },
        });

        ApplySpeechSettings();
    }

    /// <summary>
    /// One repair's result, with a voice chosen for every core the repair took one off. All of
    /// the deciding is <see cref="VoicePairing.WithReplacementsAsync"/>; what is left here is
    /// handing it the things only the root holds — the voice list, the model and the spend.
    /// </summary>
    private Task<VoicePairing.VoiceRepair> WithReplacementsAsync(
        IReadOnlyDictionary<string, string> before,
        IReadOnlyDictionary<string, string> after) =>
        VoicePairing.WithReplacementsAsync(
            before,
            after,
            _voices.Voices,
            Turns.Provider,
            Turns.Model,
            Spend,
            PriceTable.Default,
            _logger,
            TtsProviderCatalog.Selected(Settings.Current.Speech.Provider).Id);

    /// <summary>
    /// Puts every named default back where the table says it goes, once.
    /// <para>
    /// Unlike the gender repair this one needs no model — it moves a voice this provider is
    /// already offering onto the core named for it — but a core it takes a voice <em>off</em>
    /// does, so it runs on the same terms as the other and under its own flag.
    /// </para>
    /// </summary>
    private async Task RestoreNamedVoicesAsync()
    {
        if (Settings.Current.Persona.VoicesRepaired >= VoicePairing.RepairRevision || Turns.Provider is null)
        {
            return;
        }

        var provider = TtsProviderCatalog.Selected(Settings.Current.Speech.Provider).Id;
        var before = Settings.Current.Persona.Voices;

        var repair = await WithReplacementsAsync(
            before,
            VoicePairing.WithNamedDefaultsRestored(before, _voices.Voices, provider, _logger)).ConfigureAwait(false);

        Settings.Replace("persona.voices", current => current with
        {
            Persona = current.Persona with
            {
                Voices = repair.Voices,
                VoicesRepaired = repair.Complete ? VoicePairing.RepairRevision : current.Persona.VoicesRepaired,
            },
        });

        // The core aboard may have just changed voice, and nothing else will notice.
        ApplySpeechSettings();
    }

    private async Task PairPersonaVoicesAsync()
    {
        if (_voices.Count > 0)
        {
            await RepairMiscastVoicesAsync().ConfigureAwait(false);
            await RestoreNamedVoicesAsync().ConfigureAwait(false);
        }

        if (Settings.Current.Persona.VoicesPaired || _voices.Count == 0)
        {
            // The pass has run, but it may have run in a session with no model configured and
            // left the core aboard with nothing. Asking now costs nothing when it already has
            // one, and is the difference between hearing the right voice this launch and
            // hearing it after the next switch.
            await EnsureVoiceForCurrentPersonaAsync().ConfigureAwait(false);
            return;
        }

        try
        {
            var paired = await VoicePairing.ChooseAsync(
                _voices.Voices,
                Settings.Current.Persona.Voices,
                Turns.Provider,
                Turns.Model,
                Spend,
                PriceTable.Default,
                _logger,
                TtsProviderCatalog.Selected(Settings.Current.Speech.Provider).Id).ConfigureAwait(false);

            // Flagged as run even when no model was configured and only the named defaults were
            // written. The flag says this pass has happened, and the cores it could not answer
            // for are picked up one at a time as they are selected — which is the path that
            // works whenever the model arrives, rather than only at the next launch.
            Settings.Replace("persona.voices", current => current with
            {
                Persona = current.Persona with { Voices = paired, VoicesPaired = true },
            });

            // The core aboard may have just acquired a voice, and nothing else will notice.
            ApplySpeechSettings();
        }
        catch (Exception ex)
        {
            // Pairing is a convenience. Failing it means the picker still works and every core
            // uses the ship AI's voice, which is exactly where this started.
            _logger.LogWarning(ex, "Could not pair voices to personas");
        }
    }

    /// <summary>
    /// Makes the stored voices and the selected provider agree, and answers the speech settings
    /// that result. All of the deciding is <see cref="VoiceMemory.Reconciled"/>; what is left
    /// here is the write, the announcement and the log line.
    /// <para>
    /// Asked on every apply rather than only when the provider is seen to change, which is the
    /// difference that matters: the old check watched the provider held by <em>this process</em>,
    /// which is null on the first call, so a settings file that was already mismatched was
    /// trusted on every launch and every sentence failed forever. The file now says which
    /// provider its voices came from, so the question is asked of the file.
    /// </para>
    /// </summary>
    private SpeechSettings ReconcileVoicesWithProvider()
    {
        var speech = Settings.Current.Speech;
        var selected = TtsProviderCatalog.Selected(speech.Provider).Id;

        if (speech.VoicesProvider is { } chosenFor && !string.Equals(chosenFor, selected, StringComparison.Ordinal))
        {
            _logger.LogInformation(
                "The live voices were chosen for {Previous}; filing them there and taking back {Now}'s",
                chosenFor,
                selected);
        }

        // The decision itself is a pure function of settings and lives where a test can reach
        // it. Reconciled answers the same instance when there is nothing to do, so Replace's own
        // equality check turns that into no write and no announcement.
        Settings.Replace(SpeechCapability.ProviderKey, VoiceMemory.Reconciled);

        return Settings.Current.Speech;
    }

    /// <summary>Whether the selected provider has whatever credential it needs, if it needs one.</summary>
    private bool HasKeyFor(TtsProviderInfo provider) =>
        provider.KeySecretName is not { } secret || Secrets.Has(secret);

    /// <summary>
    /// Drops one voice the provider refused, everywhere it is written down.
    /// <para>
    /// The pipeline stops using it for the turn it failed on; this is what stops it coming back
    /// on the next one. A voice that fails once fails every sentence forever, because nothing
    /// else in d47 ever revisits a value the Commander is assumed to have chosen on purpose.
    /// </para>
    /// </summary>
    private void ForgetTheVoice(string voiceId)
    {
        _logger.LogInformation("{Voice} was refused by the provider; removing it", voiceId);

        Settings.Replace(
            SpeechCapability.ProviderKey,
            current => SpeechCapability.WithoutTheVoice(current, voiceId));

        ApplySpeechSettings();
    }

    private async Task LoadVoicesAsync(ITtsProvider provider)
    {
        try
        {
            _voices = await provider.ListVoicesAsync().ConfigureAwait(false);
            _logger.LogInformation(
                "The voice list has {Count} voices ({Listing})", _voices.Count, _voices.Listing);

            // The pool a re-voiced sender is drawn from. Which voices those are is decided in
            // Core, where the distinction between a locale and an accent label can be asserted —
            // this used to read ElevenLabs' accent as a locale and discard 472 of a 473-voice
            // account, leaving every NPC in a system sharing one voice.
            Cast.Pool = VoicePool.From(_voices.Voices);

            // And which of them are a woman's, so a sender whose name reads as one is given one.
            Cast.Feminine = VoicePool.Feminine(_voices.Voices);

            // Both numbers, because one of them alone is what hid that: "1 voice available" is
            // alarming beside "473 offered" and unremarkable on its own.
            _logger.LogInformation(
                "{Count} of {Offered} voices are available for re-voiced senders, {Feminine} of them women's",
                Cast.Pool.Count,
                _voices.Count,
                Cast.Feminine.Count);

            // Pairing a voice to each core needs the list, so it starts once the list arrives
            // rather than at startup. Background and best-effort: picking a character must never
            // wait on it (list.md Phase 11, #33).
            _ = PairPersonaVoicesAsync();
        }
        catch (Exception ex)
        {
            // No list is a capability being partly off, not a failure: the row still accepts a
            // voice name typed in, and speaking still works with the provider's default.
            _logger.LogWarning(ex, "Could not fetch the list of voices");
        }
    }

    /// <summary>
    /// When the core currently aboard became the core currently aboard, and what the ship's
    /// ledger looked like then. Both exist for one thing: a core reselected after time away
    /// opens with its reaction to the discontinuity rather than with a switch-in bark, and
    /// neither the elapsed time nor the delta can be reconstructed after the fact.
    /// </summary>
    /// <summary>
    /// Which situation the ambience is playing for, and which track is next. Held here because
    /// it is the only thing that spans ticks; everything it decides is a pure function of the
    /// situation and the library (list.md Phase 12).
    /// </summary>
    private readonly Ambience _ambience = new();

    /// <summary>How often the drop-in folder is looked at. See <see cref="RescanAudio"/>.</summary>
    private static readonly TimeSpan AudioScanEvery = TimeSpan.FromSeconds(2);

    private TimeSpan _sinceAudioScan = TimeSpan.Zero;

    /// <summary>
    /// How often the memory store is checked for entries past their expiry (list.md Phase 31).
    /// Coarse on purpose: an expiry is a boundary being crossed rather than something happening, so
    /// the only cost of a wide interval is that a fact lives a few minutes longer than it was asked
    /// to.
    /// </summary>
    private static readonly TimeSpan ExpiryEvery = TimeSpan.FromMinutes(10);

    private DateTimeOffset _personaSelectedAt = DateTimeOffset.Now;

    /// <summary>
    /// When each core was last aboard, and what the ship's ledger looked like then.
    /// <para>
    /// The session is null for a stamp read back from a previous run: a
    /// <see cref="SessionSummary"/> does not survive a restart, and comparing against an empty
    /// one would have a returning core remark on a delta that is an artefact of d47 having been
    /// closed rather than of anything the Commander did.
    /// </para>
    /// </summary>
    private readonly Dictionary<string, (DateTimeOffset At, SessionSummary? Session)> _personaLastSeen =
        new(StringComparer.Ordinal);

    /// <summary>
    /// What kind of switch the settings write about to arrive is (list.md Phase 35). A field
    /// rather than a parameter because the write goes through the settings row like every other
    /// caller — that is what keeps the ship-AI-name rule and the protected rule in one place —
    /// and the row cannot carry a reason. Set immediately before the write and cleared after it,
    /// which is safe because the change is raised synchronously from inside it.
    /// </summary>
    private PersonaSwitch _personaCause = PersonaSwitch.Selected;

    /// <summary>
    /// Puts the core the Commander bound to this ship aboard (list.md Phase 35, "Switching ships
    /// switches the core").
    /// <para>
    /// Through the settings row rather than straight into the host, so this is a persona change
    /// like any other: the ship AI's name follows or does not according to its own row, the
    /// voice and the wake word are re-read, and each core keeps its own transcript. What the
    /// binding decides is <em>which</em> core; nothing about the switch itself is special, which
    /// is what leaves the isolation model untouched — a core still cannot tell why it was
    /// switched on or what was on before it.
    /// </para>
    /// </summary>
    public void PutCoreAboard(ShipCoreSwitch due)
    {
        _personaCause = due.Announce ? PersonaSwitch.Ship : PersonaSwitch.Adopted;

        try
        {
            var applied = Settings.Apply(
                PersonaCapability.PersonaKey, due.Core, SettingsCaller.ShipBinding);

            _logger.LogInformation(
                "Ship {ShipId} asks for {Core}: {Status} ({Cause})",
                due.ShipId,
                due.Core,
                applied.Status,
                _personaCause);
        }
        finally
        {
            _personaCause = PersonaSwitch.Selected;
        }
    }

    /// <summary>
    /// Writes down when the core aboard stopped being aboard, so a gap reaction can be about a
    /// month rather than about an evening (list.md Phase 35). A read-modify-write, like every
    /// other writer of this file: it carries every surface's state, and a copy taken earlier and
    /// written back later would revert whatever the settings page or the headset had saved since.
    /// </summary>
    private void RememberCoreAboard(string id, DateTimeOffset at)
    {
        try
        {
            var state = ViewState.Load();

            ViewState.Save(state with
            {
                CoresLastAboard = new Dictionary<string, DateTimeOffset>(state.CoresLastAboard, StringComparer.Ordinal)
                {
                    [id] = at,
                },
            });
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Losing this costs one core one gap reaction it will not give. Losing the app over
            // it is not a trade anybody would make.
            _logger.LogDebug(ex, "Could not record when {Core} was last aboard", id);
        }
    }

    private void ApplyPersonaSettings()
    {
        var outgoing = Personas.Current;

        // Remembered before the switch, because after it there is nothing left to measure
        // against. Keyed by the core leaving, not the one arriving.
        _personaLastSeen[outgoing.Id] = (_personaSelectedAt, GameState.Active?.Session ?? SessionSummary.Empty);

        var incoming = PersonaCatalog.Resolve(Settings.Current.Persona.Id);
        var seen = _personaLastSeen.TryGetValue(incoming.Id, out var last) ? last : default;

        var away = seen.At == default ? (TimeSpan?)null : DateTimeOffset.Now - seen.At;
        var delta = seen.At != default && seen.Session is { } session
            ? TelemetryDelta.Between(session, GameState.Active?.Session, GameState.Active)
            : null;

        if (!Personas.Apply(Settings.Current.Persona, away, delta, _personaCause))
        {
            // The name may still have changed underneath an unchanged core, and that is part of
            // the persona block, so the prompt is rebuilt either way.
            Turns.Persona = Personas.RenderBlock(Settings.Current.Llm.PersonalityEnabled);
            return;
        }

        _personaSelectedAt = DateTimeOffset.Now;

        // The core that just left, written where the next session can read it. Only on a real
        // switch, which is what the early return above has already established.
        RememberCoreAboard(outgoing.Id, _personaLastSeen[outgoing.Id].At);

        // Each core owns its transcript, handed over by reference so the turns land in it
        // directly. This is the line that makes the isolation model real rather than stated:
        // without it, a core would reference something it could only have learned while another
        // was active (guardian-personas.md).
        Turns.UseTranscript(Personas.Transcript);
        Turns.Persona = Personas.RenderBlock(Settings.Current.Llm.PersonalityEnabled);
    }

    /// <summary>
    /// The new core, saying it is here. Runs off the event rather than inline in
    /// <see cref="ApplyPersonaSettings"/>, because a gap reaction asks the model for a line and
    /// a settings change must not block on a network round trip.
    /// </summary>
    private void OnPersonaChanged(PersonaChanged change)
    {
        // The ship's voice is the core aboard's, so it has to be re-read when the core changes.
        // Nothing else did it: the cast is filled in by ApplySpeechSettings, which runs when a
        // speech row changes, and selecting a core is not one — so the voice in force was
        // whichever core was aboard when the app started, for every core, forever. The lazy
        // pairing hid it, because the one path that did re-read the cast was the write a core
        // with *no* voice triggers, and that stops happening as soon as all eleven have one.
        //
        // Synchronous and ahead of the task below, because the first thing this core says is
        // spoken in there and it is the line most worth hearing in the right voice.
        ApplySpeechSettings();

        // And what d47 answers to, for the same reason and with the same failure if it is
        // skipped: the wake word defaults to the ship's AI name, so a core switch that did not
        // re-read it would leave a Commander calling the new core by the old one's name.
        ApplyWakeWords();

        _logger.LogInformation(
            "Persona changed from {Previous} to {Current} ({Arrival})",
            change.Previous?.Name ?? "(none)",
            change.Current.Name,
            change.Arrival);

        // Ahead of the line the new core is about to say, and outside the task below, so the
        // transcript reads in the order it happened: the switch, then the first thing said
        // after it. A gap reaction can take a model round trip to resolve, and the mark should
        // not wait behind it.
        Noted?.Invoke($"Switched to {change.Current.Name}");

        // A quiet arrival is a real switch with nothing to say (list.md Phase 35). The voice, the
        // wake word, the transcript and the mark above have all already changed; what is skipped
        // is a spoken line and the model call behind it. Three cases reach here — a core the
        // Commander has met arriving because they boarded the ship they bound it to, the binding
        // for the ship d47 found them already in, and a core reselected inside PersonaHost.GapAfter.
        if (change.Arrival == PersonaArrival.Quiet)
        {
            return;
        }

        _ = Task.Run(async () =>
        {
            // The Commander chose a core on a settings row and nothing has happened yet: the
            // voice has to be fetched and, on a gap, a model has to be asked for a line. Said on
            // the row they touched, because a Commander who clicked there does not look
            // elsewhere (list.md Phase 12).
            PersonaSettling?.Invoke(true);

            var line = change.Current.Intro;

            try
            {
                // Before a word is spoken, because the first thing a core says is the thing most
                // worth hearing in its own voice.
                await EnsureVoiceForCurrentPersonaAsync().ConfigureAwait(false);

                if (change is { Arrival: PersonaArrival.Gap, Gap: { } gap })
                {
                    // Authored fallback first, so there is always something to say; the model
                    // only ever replaces it.
                    line = change.Current.Return;

                    var generated = await FlavourTurn.AskAsync(
                        Turns.Provider,
                        Turns.Model,
                        Personas.RenderBlock(Settings.Current.Llm.PersonalityEnabled),
                        "You have just been switched back on after "
                        + $"{TelemetryDelta.Spoken(gap.Away)} of not running. Say one or two sentences "
                        + "reacting to the missing time, exactly as your character would. Do not greet "
                        + "the Commander formally and do not offer a list of what you can do.",
                        gap.TelemetryDelta,
                        Spend,
                        PriceTable.Default,
                        _logger).ConfigureAwait(false);

                    if (generated is not null)
                    {
                        line = generated;
                    }
                }
                else if (FlavourBriefs.Introducing(
                             change.Current.Intro,
                             Settings.Current.Llm.PersonalityEnabled) is { } brief)
                {
                    // The authored intro is a sample of how this core sounds rather than the
                    // script it reads, so it goes through the model like everything else d47 says
                    // in character. What is asked lives in FlavourBriefs with the rest of those
                    // decisions; the authored line stays the fallback, so no provider, no
                    // personality or a failed call all sound exactly as they did before.
                    var generated = await FlavourTurn.AskAsync(
                        Turns.Provider,
                        Turns.Model,
                        Personas.RenderBlock(brief.NeedsPersona),
                        brief.Instruction,
                        gameState: null,
                        Spend,
                        PriceTable.Default,
                        _logger).ConfigureAwait(false);

                    if (generated is not null)
                    {
                        line = generated;
                    }
                }
            }
            finally
            {
                // Cleared before the line is said rather than after it has been spoken aloud:
                // what the row was waiting for is d47 having something to say, and a row still
                // marked busy while the core is talking is a row describing the wrong thing.
                PersonaSettling?.Invoke(false);
            }

            // Anything d47 says without a turn behind it still belongs in the transcript, so
            // that what was heard and what can be read back are the same set. A core's first
            // words are the one line most worth having there: it is the only thing that core
            // has ever said, and a conversation whose opening is missing starts mid-thought.
            //
            // Raised before the await, so it appears as the line begins rather than after it
            // has finished being spoken — the same ordering as every other announcement.
            Said?.Invoke(line);

            await Voice.AcknowledgePersonaAsync(line).ConfigureAwait(false);
        });
    }

    /// <summary>
    /// Re-reads <c>data/audio/</c> when something in it has changed.
    /// <para>
    /// Throttled, because a scan is a directory enumeration and the loop runs at 10 Hz — six
    /// hundred of them a minute to notice a file that arrives twice a session is a cost with no
    /// buyer. Measured against the time the tick was given rather than against the clock, so it
    /// stays right if the period changes and the replay harness still runs it at 100x.
    /// </para>
    /// <para>
    /// A rebuild swaps the library reference. Anything mid-playback holds the clip it was handed
    /// rather than the library it came from, so a reload can never cut a sentence — which is the
    /// property that lets this run on a timer at all.
    /// </para>
    /// </summary>
    private void RescanAudio(TickContext context, FolderAudioSource drops, ILogger<CueLibrary> logger)
    {
        _sinceAudioScan += context.Since;

        if (_sinceAudioScan < AudioScanEvery)
        {
            return;
        }

        _sinceAudioScan = TimeSpan.Zero;

        if (!drops.Poll())
        {
            return;
        }

        Cues = CueLibrary.Load(logger, new EmbeddedCueSource(typeof(CueLibrary).Assembly), drops);

        _logger.LogInformation(
            "Reloaded the audio folder: {Count} file(s) picked up, {Skipped} skipped",
            Cues.CustomCount,
            Cues.Skipped.Count);

        // The rows that read the library — the bed picker's choices and the row saying what was
        // found — have no other way to know. The picker asks at the moment it is opened and is
        // already right; the disclosure is read on a refresh, and this is the refresh.
        AudioReloaded?.Invoke();
    }

    /// <summary>
    /// The ambience layer, following what the Commander is doing (list.md Phase 12).
    /// <para>
    /// Called every tick and almost always does nothing: <see cref="Ambience.Enter"/> answers
    /// false unless the situation actually changed, and a situation changes a handful of times
    /// an hour. Starting a track on a change rather than checking whether one is playing is what
    /// keeps this from restarting the music every hundred milliseconds.
    /// </para>
    /// </summary>
    private void FollowSituation(D47.Core.Journal.GameStatus status)
    {
        if (!_ambience.Enter(Situations.For(status)))
        {
            return;
        }

        // The old situation's track does not play out over the new one. Arriving at a station is
        // the moment the docking music is wanted, not thirty seconds later.
        Audio.StopMusic();
        PlayNextTrack();
    }

    /// <summary>
    /// Starts the next ambience track, or leaves it quiet.
    /// <para>
    /// Quiet is the normal answer: d47 ships with no music at all, so every Commander who has not
    /// dropped any into <c>data/audio/music</c> is here. Muted is quiet too, and checked here
    /// rather than left to a gain of zero — a track nobody can hear is still a file being decoded.
    /// </para>
    /// </summary>
    private void PlayNextTrack()
    {
        if (Audio.Mix.Music.Muted)
        {
            return;
        }

        if (_ambience.Next(Cues) is { } track)
        {
            Audio.Enqueue(new AudioRequest { Channel = AudioChannel.Music, Clip = track });
        }
    }

    private void ApplySpeechSettings()
    {
        var speech = ReconcileVoicesWithProvider();
        var provider = TtsProviderCatalog.Selected(speech.Provider);

        // Whether to rebuild and whether to refetch are decided in Core, where a test can reach
        // them; what to build and how to fetch it stay here, where the loggers and the secret
        // store are. Both of the faults an afternoon's hand-testing found lived on the far side
        // of that line (list.md Phase 19).
        var plan = SpeechWiring.Plan(_speechWiring, speech.Provider, HasKeyFor(provider));
        _speechWiring = plan.Next;

        if (plan.RebuildClient)
        {
            // Through the interface, so this stays correct for a provider that needs no
            // disposal. ITtsProvider deliberately does not require IDisposable: it is a text-to-
            // audio seam, and whether an implementation holds an HTTP handle is its own business.
            (_tts as IDisposable)?.Dispose();
            _voices = VoiceCatalogue.Silent;
            Cast.Reset();

            _tts = provider.Id switch
            {
                SpeechCapability.EdgeId =>
                    new EdgeNeuralTtsProvider(_loggerFactory.CreateLogger<EdgeNeuralTtsProvider>()),

                SpeechCapability.ElevenLabsId => new ElevenLabsTtsProvider(
                    () => Secrets.TryGet(ElevenLabsTtsProvider.KeySecretName, out var key) ? key : null,
                    _loggerFactory.CreateLogger<ElevenLabsTtsProvider>()),

                _ => null,
            };

            // Counted at the seam, which is the only place every caller passes — the ship's AI,
            // a callout, a re-voiced sender and a core's own introduction all converge on it,
            // and counting at any caller means missing the next one (list.md Phase 19).
            if (_tts is not null)
            {
                _tts = new MeteredTtsProvider(_tts, SpeechSpend);
            }
        }

        // Fetched in the background. The picker asks synchronously and the list comes over the
        // network, so it is cached rather than requested on open — and not awaited, because a
        // settings change must not wait on a provider being reachable.
        if (plan.RefetchVoices && _tts is not null)
        {
            _ = LoadVoicesAsync(_tts);
        }

        Voice.Tts = _tts;

        // Everyone d47 can speak as, filled in from settings. The ship AI's voice is the one
        // paired to the core aboard (#33), then whatever was chosen before voices were kept per
        // core, then the provider's own default.
        //
        // The pairing wins, and it did not use to. A Commander who had ever picked a voice by
        // hand pinned every core to it: the pairing was computed, stored, shown, and never
        // reached, so switching character changed everything about the companion except the one
        // thing you hear. `Speech.Voice` is now the fallback for a core with no pairing, and the
        // Voice row writes the core aboard — which is what PersonaSettings.Voices always said it
        // held.
        Cast.Rate = SpeechCapability.RateFor(Settings.Current);
        Cast.DefaultVoice = SpeechCapability.ShipVoiceFor(Settings.Current, Personas.Current.Id);
        Cast.Assign(VoiceRole.CarrierCaptain, speech.CarrierCaptainVoice);
        Cast.Assign(VoiceRole.TowerControl, speech.TowerVoice);

        Voice.Voice = Cast.For(VoiceRole.ShipAi);
        Voice.CuesEnabled = speech.CuesEnabled;
        Voice.BedEnabled = speech.ThinkingBedEnabled;
        Voice.Bed = speech.ThinkingBed;

        Turns.Retry = SpeechCapability.RetryFrom(speech);

        if (!string.Equals(_openDevice, speech.OutputDevice, StringComparison.Ordinal))
        {
            _openDevice = speech.OutputDevice;

            try
            {
                _audioSink.Reopen(speech.OutputDevice);
            }
            catch (Exception ex)
            {
                // A device that has gone away between being chosen and being opened. Silence
                // is the consequence, not a crash.
                _logger.LogError(ex, "Could not move audio output to {Device}", speech.OutputDevice);
            }
        }
    }

    /// <summary>
    /// Turns one captured utterance into words and hands them on. Fire-and-forget from the
    /// audio thread's point of view: it returns immediately and the work happens on the pool.
    /// </summary>
    private void TranscribeAsync(Utterance utterance)
    {
        // The microphone has closed and the words are being worked out. Its own state because it
        // is its own wait — on a large model on the CPU it is the longest part of the loop, and
        // an avatar still showing "listening" through it says the Commander should keep talking.
        Voice.EnterState(Core.Audio.LoopState.Transcribing);

        if (!_transcriber.IsReady)
        {
            // Captured but not transcribable. Said once per utterance rather than silently
            // discarded — the Commander held a key and expects something to happen.
            _logger.LogInformation(
                "Heard {Seconds:0.#}s but no speech model is loaded", utterance.Duration.TotalSeconds);

            const string Cannot = "I heard you, but I have no speech model loaded to understand it.";

            _ = Voice.AnnounceAsync(Cannot);
            Said?.Invoke(Cannot);

            // No cue: a sentence is about to be spoken saying the same thing, and a chime under
            // it is d47 telling the Commander twice.
            Voice.EnterState(Core.Audio.LoopState.Idle, cue: false);
            return;
        }

        if (utterance.IsSilent)
        {
            // Not "nothing intelligible" — nothing at all arrived. Almost always the input
            // device: Windows defaults to whatever it likes, and a virtual endpoint from VR or
            // streaming software delivers a stream of zeroes that looks exactly like a working
            // microphone right up until a turn quietly produces nothing. Named rather than
            // guessed at, because the Commander cannot see which device d47 opened.
            var device = _microphone.OpenDeviceName ?? "the selected microphone";

            _logger.LogWarning(
                "Captured {Seconds:0.#}s of digital silence from {Device}; it is sending no audio",
                utterance.Duration.TotalSeconds,
                device);

            var problem =
                $"I heard nothing at all — {device} is not sending any audio. "
                + "Check it is not muted, or pick a different microphone in Settings.";

            _ = Voice.AnnounceAsync(problem);
            Said?.Invoke(problem);
            Voice.EnterState(Core.Audio.LoopState.Idle, cue: false);
            return;
        }

        _ = Task.Run(async () =>
        {
            try
            {
                // Journal-derived and network-free. Proper nouns are where recognition fails
                // hardest and most silently, so the names of where the Commander is and what
                // they fly go in with every utterance (list.md Phase 6).
                var nouns = ProperNouns.From(GameState.Active, _route?.Invoke());

                var transcription = await _transcriber
                    .TranscribeAsync(utterance, nouns)
                    .ConfigureAwait(false);

                // A panel is asking for a value and this is the answer to it (list.md Phase 25,
                // "Say it, or type it").
                //
                // Ahead of both the empty check and the wake word, and both are deliberate.
                // Hearing nothing is one of the three failures the entry loop detects and has to
                // reach it to put the keyboard back with a reason, rather than being swallowed
                // here as an uneventful cough. And a Commander answering a question d47 just
                // asked them should not have to say its name first.
                if (Prompted(new Core.Interface.Heard(
                        transcription.Text, transcription.Confidence, Final: true)))
                {
                    Voice.EnterState(Core.Audio.LoopState.Idle, cue: false);
                    return;
                }

                if (transcription.IsEmpty)
                {
                    // Distinguished from a failure: the model ran and heard nothing worth
                    // reporting, which a Commander who coughed should not be told is an error.
                    _logger.LogInformation("Nothing intelligible in {Seconds:0.#}s", utterance.Duration.TotalSeconds);

                    // Without a cue, like every other path here that has nothing to say
                    // (remediation.md 14, item 8). A chime after a cough is d47 reporting that it
                    // noticed the room, which is the same intrusion as answering it out loud.
                    Voice.EnterState(Core.Audio.LoopState.Idle, cue: false);
                    return;
                }

                if (_heardAt is { } clock)
                {
                    clock.Value = DateTimeOffset.Now;
                }

                // The wake word, applied to the words rather than to the audio (list.md
                // Phase 13). Outside wake-word mode the policy holds no phrases and admits
                // everything, so push-to-talk and continuous listening take the same path
                // through here and neither pays for a decision that has already been made.
                var decision = Wake.Admit(transcription.Text, DateTimeOffset.Now);

                if (decision.Outcome == WakeOutcome.Ignored)
                {
                    // Somebody in the room said something that was not to d47. Logged at debug
                    // and nowhere else — this is the common case in wake-word mode, and a panel
                    // that transcribed every conversation in earshot would be a panel nobody
                    // would leave the mode switched on for.
                    _logger.LogDebug("Not addressed to me: {Text}", transcription.Text);
                    Voice.EnterState(Core.Audio.LoopState.Idle, cue: false);
                    return;
                }

                if (decision.Outcome == WakeOutcome.Woken)
                {
                    // The name and nothing after it. Acknowledging is the point — a wake word
                    // that answers nothing leaves the Commander waiting to find out whether it
                    // heard — and the window is now open for whatever they were about to ask.
                    _logger.LogInformation("Woken by name; listening for what follows");

                    // The cue on its own rather than the loop state behind it. Entering
                    // Listening would be accurate for as long as the window lasted and then
                    // stuck: nothing settles out of that state, so a Commander who said the name
                    // and then changed their mind would leave the face listening for the rest of
                    // the session. What is actually waiting is the microphone, and the microphone
                    // has its own indicator now (list.md Phase 13).
                    if (Voice.CuesEnabled)
                    {
                        Audio.Enqueue(new Core.Audio.AudioRequest
                        {
                            Channel = Core.Audio.AudioChannel.Cue,
                            Clip = Cues.For(Core.Audio.LoopState.Listening),
                        });
                    }

                    Voice.EnterState(Core.Audio.LoopState.Idle, cue: false);
                    return;
                }

                _logger.LogInformation("Heard: {Text}", transcription.Text);
                Heard?.Invoke(decision.Text);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Could not transcribe an utterance");
                Voice.EnterState(Core.Audio.LoopState.Failed);
            }
        });
    }

    /// <summary>
    /// The surfaces that may be waiting on a spoken value (list.md Phase 25, "Say it, or type
    /// it").
    /// <para>
    /// A list rather than one delegate, because there are two panels and either can have a
    /// prompt open: the desktop window and the headset overlay each have their own navigator, so
    /// which of them is asking is a fact about a surface. Registered by whoever built the
    /// surface, since the host owns the view model and not the views.
    /// </para>
    /// </summary>
    private readonly List<Func<Core.Interface.Heard, bool>> _prompts = [];

    /// <summary>Adds a surface to the list of places a spoken value may be destined for.</summary>
    public void RoutePrompts(Func<Core.Interface.Heard, bool> surface) => _prompts.Add(surface);

    /// <summary>
    /// The navigators a spoken "show me the checklist" moves (list.md Phase 25).
    /// <para>
    /// <b>Every surface, not one.</b> A phrase has no surface attached to it — the Commander said
    /// it once, into the room — and moving only the window would leave a Commander in a headset
    /// saying it twice with nothing happening either time. So both go, and the two surfaces agree
    /// about where they are for as long as neither is driven separately, which is the only reading
    /// of the phrase that is never surprising.
    /// </para>
    /// </summary>
    private readonly List<Core.Interface.PanelNavigator> _navigators = [];

    /// <summary>Adds a surface's navigator to the ones a spoken phrase moves.</summary>
    public void RouteNavigation(Core.Interface.PanelNavigator nav) => _navigators.Add(nav);

    /// <summary>
    /// Moves every surface the phrase named somewhere, and says what happened — or null when it
    /// named nowhere, which is the common case and falls through to the turn.
    /// </summary>
    public string? Navigate(string spoken)
    {
        string? said = null;

        foreach (var nav in _navigators)
        {
            // Every one of them, and the first answer is the one said out loud. Two surfaces that
            // are in different places answer differently — the window at a root and the headset
            // three levels down — and what the Commander hears should describe the move rather
            // than one surface's share of it.
            var moved = Core.Interface.PanelPhrases.Apply(spoken, nav);

            said ??= moved;
        }

        return said;
    }

    /// <summary>
    /// Offers what was heard to each surface in turn, and says whether one took it. First come,
    /// first served, and in practice there is at most one open at a time: a modal is a modal, and
    /// a Commander is wearing the headset or looking at the window rather than both.
    /// </summary>
    private bool Prompted(Core.Interface.Heard heard) =>
        _prompts.Any(surface => surface(heard));

    /// <summary>
    /// The plotted route, for proper-noun biasing. Set during composition because the reader
    /// lives in the tick closure rather than on the host.
    /// </summary>
    private Func<NavRoute>? _route;

    /// <summary>
    /// The model currently being fetched, or null. One at a time: applying listening settings
    /// happens on every change, and a second fetch of the same file over the first is bytes
    /// nobody asked for.
    /// </summary>
    private string? _fetching;

    /// <summary>
    /// Downloads a selected model that is not on disk, then loads it.
    /// <para>
    /// The selection is the go-ahead — the same rule the settings row follows when the
    /// Commander picks one there. The size and the host are still stated: on that row, and in
    /// the egress disclosure, which reports the speech-model destination whenever a model is
    /// selected rather than only while bytes are moving.
    /// </para>
    /// <para>
    /// Failure is a log line and nothing else. A Commander asking "can you hear me" is already
    /// told that no speech model is loaded, which is the answer they can act on; an
    /// announcement about a background transfer is noise on the panel. The next settings change
    /// or the next launch tries again.
    /// </para>
    /// </summary>
    private async Task FetchModelAsync(WhisperModel model)
    {
        if (Interlocked.CompareExchange(ref _fetching, model.Id, null) is not null)
        {
            return;
        }

        try
        {
            var result = await Models.InstallAsync(model).ConfigureAwait(false);

            if (result.Success)
            {
                _logger.LogInformation("{Model} downloaded", model.Id);

                // Re-applied rather than loaded directly, so a file arriving goes through the
                // one path that knows what loading a model entails.
                ApplyListeningSettings();
                return;
            }

            _logger.LogWarning(
                "{Model} could not be downloaded: {Detail}",
                model.Id,
                result.Detail ?? "no detail given");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "{Model} could not be downloaded", model.Id);
        }
        finally
        {
            _fetching = null;
        }
    }

    /// <summary>
    /// Rebuilds everything downstream of the listening settings: the device, the key, the gate
    /// policy and the pre-roll. Called at startup and on any change, so the two paths cannot
    /// drift (list.md Phase 4, "Apply every setting without a restart").
    /// </summary>
    private void ApplyListeningSettings()
    {
        var listening = Settings.Current.Listening;

        Listening.Mode = listening.Mode switch
        {
            ListeningCapability.ToggleMode => ListenMode.Toggle,
            ListeningCapability.ContinuousMode => ListenMode.VoiceActivity,
            ListeningCapability.WakeMode => ListenMode.WakeWord,
            _ => ListenMode.PushToTalk,
        };

        Listening.PreRoll = TimeSpan.FromMilliseconds(listening.PreRollMilliseconds);
        Listening.Voice.Sensitivity = listening.Sensitivity;
        Listening.Voice.Hangover = TimeSpan.FromMilliseconds(listening.SilenceMilliseconds);

        // Started before the microphone, so the first buffer off a freshly opened device is
        // already going through it. Idempotent — a canceller already running is left alone
        // rather than cycled, because throwing away a converged filter makes d47 audible to
        // itself for a second every time an unrelated listening row is touched.
        if (listening.EchoCancellation)
        {
            Echo.SuppressNoise = listening.NoiseSuppression;
            Echo.Start();
        }
        else
        {
            Echo.Stop();
        }

        // From the canceller's live state rather than from the row that asked for it. A
        // canceller that was turned on and failed to load its native library must not leave the
        // gate believing d47's own voice is being subtracted — that belief is what decides
        // whether hands-free listening stays open while d47 speaks.
        Listening.EchoCancelled = Echo.IsActive;

        ApplyWakeWords();

        // Rebinding while the key is held would leave the gate open with nothing able to close
        // it — the listening equivalent of a stranded key (architecture.md D4, rule 2).
        _pushToTalk.ForceUp();

        // The model, before the key. A Commander who binds a key and finds d47 captures but
        // cannot understand should see the reason in the status answer, not infer it.
        //
        // Which of the three happens is decided in Core, where a test can reach it; loading the
        // native model and starting the download stay here, where the file handles are.
        var model = ListeningWiring.PlanModel(listening, Models);

        switch (model.Action)
        {
            case SpeechModelAction.Load:
                _transcriber.Load(model.Path!, model.Model!.Id, model.UseGpu);
                break;

            case SpeechModelAction.Fetch:
                // Selected but not on disk, so fetch it. The selection stays where it is while
                // that happens: it is what the Commander asked for, and a row that drops to none
                // because the file has not arrived yet describes the disk rather than the choice.
                //
                // Fetched rather than offered. The offer was the wrong shape for the one case
                // that matters — a fresh install, where the answer is always yes and the question
                // is a step between the Commander and a working microphone.
                _logger.LogInformation("{Model} is selected but not installed; fetching it", model.Model!.Id);

                _transcriber.Unload();
                _ = FetchModelAsync(model.Model);
                break;

            default:
                // Unload, not Dispose: this runs on every listening.* change, and the host keeps
                // one transcriber for the life of the process.
                _transcriber.Unload();
                break;
        }

        // Deferred to the end, because writing a setting raises Changed, which re-enters this
        // method: doing it above would run the microphone and key work twice on one apply.
        // Nothing deferred here any more. The selection used to be rewritten at the end of this
        // method — cleared to none so it could be re-offered — and a write raises Changed, which
        // re-enters here; the fetch above needs no such thing, because it leaves the setting
        // exactly where the Commander put it.

        var bound = _pushToTalk.Bind(listening.PushToTalkKey);

        if (!ListeningWiring.NeedsMicrophone(listening.Mode, bound))
        {
            // No key and nothing that opens the gate by itself, so no microphone. d47 opening an
            // input device it will never read from is exactly the surprise the unset default
            // exists to avoid. Closed rather than disposed, for the same reason as the
            // transcriber above — the Commander can bind a key later, and that has to reopen the
            // device rather than fail.
            _microphone.Close();
            Listening.Capturing = false;
            return;
        }

        _microphone.Open(listening.InputDevice);
        Listening.Capturing = _microphone.IsCapturing;

        if (!bound)
        {
            // Hands free with no key bound is a legitimate configuration, and the collision
            // check below has nothing to check.
            return;
        }

        if (Binds.Using(listening.PushToTalkKey!) is { Count: > 0 } collisions)
        {
            // Logged at startup as well as answered on request: the symptom of a double-bound
            // key is that nothing happens, which reads as d47 being broken.
            _logger.LogWarning(
                "Push-to-talk {Key} is also bound in Elite ({Preset}) to {Actions}; one of the two will not work",
                listening.PushToTalkKey,
                Binds.PresetName,
                string.Join(", ", collisions.Select(binding => binding.Action).Distinct()));
        }
    }

    /// <summary>
    /// Puts what the microphone is doing in front of the Commander, on both surfaces.
    /// <para>
    /// The detail beside it is the gesture that would open the gate, which is a settings question
    /// — so it is answered here rather than by the view, which reads no settings, or by the gate,
    /// which knows about audio and not about keys.
    /// </para>
    /// </summary>
    /// <summary>
    /// The switch annunciator, on both surfaces at once (list.md Phase 21, item 6). Called every
    /// tick and setting the same value repeatedly is free — the view model raises nothing when
    /// nothing changed.
    /// </summary>
    private void ShowSwitches(string? against) => Panel.SwitchesText = against;

    /// <summary>
    /// Carries out whatever the reconciler decided this tick. The same shape as
    /// <see cref="CarryOutPendingActions"/> and for the same reason: the tick is synchronous and
    /// must never block, and a key press is neither.
    /// <para>
    /// It shares <c>_acting</c> with the autonomous drain, so a honk and a switch flip cannot be
    /// holding keys at the same time. Two callers each pressing their own binding at once is two
    /// keys down that neither of them knows about.
    /// </para>
    /// </summary>
    private void CarryOutReconciles(SwitchReconciler reconciler, IGameInput input)
    {
        var pending = reconciler.Drain();

        if (pending.Count == 0)
        {
            return;
        }

        _ = Task.Run(async () =>
        {
            await _acting.WaitAsync().ConfigureAwait(false);

            try
            {
                foreach (var reconcile in pending)
                {
                    if (reconcile.Steps.Count > 0)
                    {
                        var result = await input.SendAsync(reconcile.Steps).ConfigureAwait(false);

                        _logger.LogInformation(
                            "Switch {Name} reconciled {Label}: {Outcome}",
                            reconcile.Switch,
                            reconcile.Label,
                            result.Outcome);

                        // The Commander flipped a switch and is watching for the thing to
                        // happen, so a refusal that stayed in the log would look like the
                        // feature not working.
                        if (!result.Sent)
                        {
                            await Voice.AnnounceAsync(new Announcement(
                                reconcile.Switch,
                                $"I could not set {reconcile.Label} from {reconcile.Switch}. {result.Reason}"))
                                .ConfigureAwait(false);
                        }
                    }

                    if (reconcile.Say is { } say)
                    {
                        await Voice.AnnounceAsync(new Announcement(reconcile.Switch, say)).ConfigureAwait(false);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "A switch could not be reconciled");
            }
            finally
            {
                // Unconditional, like everywhere else that presses a key (architecture.md D4).
                input.ReleaseAll();
                _acting.Release();
            }
        });
    }

    private void ShowMicrophone(MicrophoneState state)
    {
        Panel.Microphone = state;

        var listening = Settings.Current.Listening;

        // Describing a key is the App's business — Core has no keyboard — so the gesture is
        // rendered here and the sentence is chosen in Core, where a test reads what a Commander
        // reads.
        Panel.MicrophoneDetail = MicrophoneNarration.For(
            state,
            listening.Mode,
            Wake.Phrases,
            listening.PushToTalkKey is { } key ? Input.Gestures.Describe(key) : null,
            listening.PreRollMilliseconds);

        // The same three facts, worded for a prompt that is waiting on one (remediation.md 10,
        // item 12). Set from here rather than from the prompt, so both surfaces are told once and
        // cannot disagree about one microphone.
        Panel.ListeningPrompt = MicrophoneNarration.Prompt(
            listening.Mode,
            Wake.Phrases,
            listening.PushToTalkKey is { } bound ? Input.Gestures.Describe(bound) : null);
    }

    /// <summary>
    /// Points the wake-word policy at whatever d47 currently answers to.
    /// <para>
    /// Called from the listening settings and again whenever the core or the ship's AI name
    /// changes, because an unset row means "the name the Commander gave their ship's AI" — and
    /// a wake word that goes on being the previous core's name after a switch is a wake word
    /// that stops working for a reason nothing on screen explains.
    /// </para>
    /// </summary>
    private void ApplyWakeWords()
    {
        var listening = Settings.Current.Listening;

        Wake.Window = TimeSpan.FromSeconds(listening.WakeWindowSeconds);

        Wake.Phrases = ListeningWiring.WakePhrases(listening.Mode, listening.WakeWords, Personas.ShipName);
    }

    /// <summary>
    /// Says out loud that the model is not usable, if there is a voice to say it with.
    /// <para>
    /// The whole point of the item is that a misconfigured provider currently presents as
    /// silence, and silence is indistinguishable from a model with nothing to say
    /// (list.md Phase 5). Called after the panel is up so the same message is on screen.
    /// </para>
    /// </summary>
    public async Task AnnounceStartupProblemsAsync()
    {
        if (StartupError is { } settingsError)
        {
            Voice.EnterState(Core.Audio.LoopState.Failed);
            await Voice.AnnounceAsync($"My settings could not be loaded. {settingsError}")
                .ConfigureAwait(false);
            return;
        }

        if (!LlmAvailability.CanAttemptModelTurn && LlmAvailability.Reason is { } reason)
        {
            Voice.EnterState(Core.Audio.LoopState.Unsure);
            await Voice.AnnounceAsync(
                $"I have no language model right now. {reason} I can still answer from my own capabilities.")
                .ConfigureAwait(false);
        }
    }

    private TickDriver? _ticking;

    /// <summary>
    /// Guards the callout speaker. Announcements are spoken one at a time in the order they were
    /// queued: two callouts landing on the same tick and being synthesised concurrently would
    /// arrive in whichever order the network happened to return them, and "shields are down" is
    /// not interchangeable with "route complete".
    /// </summary>
    private readonly SemaphoreSlim _speaking = new(1, 1);

    /// <summary>
    /// The voice provider in use. Typed as the seam rather than as Edge's implementation, which
    /// is the point of the seam — Phase 11's paid provider arrives without anything above this
    /// line noticing (architecture.md §2).
    /// </summary>
    private ITtsProvider? _tts;

    /// <summary>
    /// Which provider <see cref="_tts"/> is, and whether that provider had its key last time
    /// speech settings were applied. Tracked rather than inferred, because "is it null" answered
    /// "does one need building" only while there was exactly one to build — and because a key
    /// arriving is an edge rather than a level.
    /// <para>
    /// Handed to <see cref="SpeechWiring.Plan"/> and replaced with what it answers. This field
    /// and that function are the whole of the state; nothing else here remembers the last apply.
    /// </para>
    /// </summary>
    private SpeechWiringState _speechWiring = SpeechWiringState.Nothing;

    /// <summary>
    /// Everyone d47 can speak as (list.md Phase 11). Not a second audio path: it decides which
    /// voice a line is synthesised in, and the line still goes through the one arbiter, because
    /// separate paths per voice are how a line gets spoken in the wrong one (architecture.md D7).
    /// </summary>
    public VoiceCast Cast { get; } = new();

    /// <summary>
    /// What the selected provider offers, cached. Empty until the first fetch returns, which is
    /// the honest answer in the meantime: the picker allows a typed value, so an empty list is a
    /// smaller list rather than a dead end.
    /// </summary>
    private VoiceCatalogue _voices = VoiceCatalogue.Silent;

    /// <summary>What the language-model endpoint last said it serves (list.md Phase 29).</summary>
    private volatile IReadOnlyList<string> _endpointModels = [];

    /// <summary>Which provider and address that list came from, so it is asked once each.</summary>
    private volatile string? _endpointModelsFor;

    /// <summary>
    /// What the voices have cost this session (list.md Phase 19). Lives for the process, like
    /// <see cref="Spend"/>, and for the same reason: "what has this cost" is a question about a
    /// session rather than about a turn, and it must survive the provider being switched.
    /// </summary>
    public SpeechSpend SpeechSpend { get; } = new();

    /// <summary>
    /// Auditions already paid for, keyed by the provider that issued the voice, the role being
    /// cast and the voice itself (list.md Phase 19).
    /// <para>
    /// Walking back and forth over four candidates should not be four purchases each way. Keyed
    /// on the provider as well as the voice because an id means nothing outside the provider
    /// that issued it, so the same string can be two different voices across a switch.
    /// </para>
    /// <para>
    /// For the session only, and deliberately not written to disk: the clip is the Commander's
    /// core's own words in a voice they may not keep, and caching it on disk would be d47
    /// choosing to store audio nobody asked it to store.
    /// </para>
    /// </summary>
    private readonly Dictionary<(string Provider, string Voice), AudioClip> _auditions = new();

    /// <summary>The group auditions play in, so a second one drops the first mid-word.</summary>
    private const string AuditionGroup = "voice-audition";

    /// <summary>
    /// Speaks one voice so it can be judged before it is chosen (list.md Phase 19, "Hear a voice
    /// before you choose it").
    /// <para>
    /// The line is the core aboard's own opening for the ship's AI, and the role's own words for
    /// the two carrier voices, because a voice is being cast for a character and a generic sample
    /// answers a different question — including one level down, where a tower reciting Warden's
    /// introduction would be the same mistake. Through the one arbiter like
    /// everything else that makes a sound (architecture.md D7), so an audition ducks the game,
    /// is cut off by the shut-up hotkey exactly as speech is, and drops the previous audition
    /// rather than queueing behind it.
    /// </para>
    /// <para>
    /// Nothing here commits the choice. The picker is still open and the settings row is
    /// untouched; the Commander has heard a voice, which is all they asked for.
    /// </para>
    /// </summary>
    internal async Task AuditionVoiceAsync(string voiceId, VoiceRole role, CancellationToken cancellationToken)
    {
        if (_tts is not { } provider)
        {
            throw new InvalidOperationException("No voice provider is selected.");
        }

        // Before the synthesis rather than after it, so pressing the button twice in a row
        // silences the first attempt while the second is still being fetched — which on a paid
        // provider is most of the wait.
        Audio.DropGroup(AuditionGroup);

        var key = (provider.Id, $"{role}:{voiceId}");

        if (!_auditions.TryGetValue(key, out var clip))
        {
            clip = await provider.SynthesizeAsync(
                role == VoiceRole.ShipAi ? AuditionLine.For(Personas.Current) : AuditionLine.For(role),
                new VoiceSelection(voiceId, SpeechCapability.RateFor(Settings.Current)),
                cancellationToken).ConfigureAwait(false);

            // Cached after the await, so a cancelled or failed synthesis caches nothing and the
            // next press tries again.
            _auditions[key] = clip;
        }

        cancellationToken.ThrowIfCancellationRequested();

        Audio.Enqueue(new AudioRequest
        {
            Channel = AudioChannel.Speech,
            Clip = clip,
            Group = AuditionGroup,
            // The clip's name is the text it was synthesised from, which is what the caption
            // layer wants — so an audition is captioned in the headset like any other speech.
            Caption = clip.Name,
        });
    }

    /// <summary>
    /// One autonomous action at a time. The honk holds a key for six seconds, and two of them
    /// overlapping would interleave their presses on one keyboard.
    /// </summary>
    private readonly SemaphoreSlim _acting = new(1, 1);

    /// <summary>
    /// Carries out whatever the autonomous actions decided this tick. Called from the tick
    /// thread and returns immediately: the tick must never block, and this is the one caller
    /// that would block it for six seconds if it did.
    /// </summary>
    private void CarryOutPendingActions(AutonomousActionRunner runner, IGameInput input)
    {
        var pending = runner.Drain();

        if (pending.Count == 0)
        {
            return;
        }

        _ = Task.Run(async () =>
        {
            await _acting.WaitAsync().ConfigureAwait(false);

            try
            {
                foreach (var action in pending)
                {
                    if (action.Decision.Acts)
                    {
                        var result = await input.SendAsync(action.Decision.Steps).ConfigureAwait(false);

                        _logger.LogInformation(
                            "Autonomous action {Id} finished: {Outcome}", action.Id, result.Outcome);

                        // Nobody asked for this, so nobody is watching for it to fail. A
                        // refusal that stays in the log is one the Commander never learns about.
                        if (!result.Sent)
                        {
                            await Voice.AnnounceAsync(new Announcement(
                                action.Id, $"I could not use {action.Label}. {result.Reason}")).ConfigureAwait(false);
                        }
                    }

                    if (action.Decision.Say is { } say)
                    {
                        await Voice.AnnounceAsync(new Announcement(action.Id, say)).ConfigureAwait(false);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An autonomous action could not be carried out");
            }
            finally
            {
                // Unconditional, like everywhere else that presses a key. An action interrupted
                // part-way must not leave the fire button down (architecture.md D4).
                input.ReleaseAll();
                _acting.Release();
            }
        });
    }

    /// <summary>
    /// Takes whatever the callouts queued this tick and says it. Called from the tick thread and
    /// returns immediately — the speaking itself happens on the thread pool, because the tick
    /// must never block on synthesis.
    /// </summary>
    /// <summary>
    /// One announcement, in whoever's voice it belongs to.
    /// <para>
    /// Everything Phase 8 produces is the ship's AI, so this resolved to one voice until Phase
    /// 11. Now a re-voiced message carries a sender and a carrier line carries a role, and the
    /// lookup for both lives here — the callout knows whose line it is, the cast knows what
    /// that person sounds like, and neither has to know about the other.
    /// </para>
    /// </summary>
    private async Task SayAsync(Announcement announcement)
    {
        var voice = announcement.Speaker is { Length: > 0 } speaker
            ? Cast.ForSender(speaker, announcement.SpeakerIsPlayer, announcement.Voice)
            : Cast.For(announcement.Voice);

        // Written before it is spoken, and whether or not the speaking works. A message that
        // could not be synthesised is still a message that arrived, and the page is the only
        // place left to see it.
        if (announcement.Transcript is { Length: > 0 } line)
        {
            Transcribed?.Invoke(line);
        }
        else if (announcement.ConversationLine is { Length: > 0 } spoken)
        {
            // The ship's AI, saying something no turn produced — which is exactly what Said is
            // for, and what callouts were never routed through. Which announcements those are is
            // decided in Core, where it can be asserted against a callout rather than against a
            // running app.
            Said?.Invoke(spoken);
        }

        await Voice.AnnounceAsync(announcement, voice).ConfigureAwait(false);
    }

    /// <summary>
    /// How long a carrier line may spend being written before the authored one is used instead.
    /// Tight on purpose: this is decoration on a callout queue that also carries warnings, and
    /// the authored line is already correct.
    /// </summary>
    private static readonly TimeSpan FlavourBudget = TimeSpan.FromSeconds(3);

    /// <summary>
    /// The same announcement, said in character, when there is a model to ask and it is one of
    /// the lines the checklist wants varied (list.md Phase 11: "with varied LLM arrival and
    /// departure responses").
    /// <para>
    /// Only the carrier's own lines. A danger callout is never rewritten by a model: those fire
    /// on the event and say exactly what happened, and "shields are down" is not a line that
    /// benefits from personality (list.md Phase 8).
    /// </para>
    /// </summary>
    private async Task<Announcement> VaryAsync(Announcement announcement)
    {
        // Which lines are eligible and what each is asked lives in Core, where the one property
        // that matters — that a danger callout is never rewritten — can be asserted. Resolving
        // the persona block and reading the live game state stay here.
        if (Turns.Provider is null
            || FlavourBriefs.For(announcement, Settings.Current.Llm.PersonalityEnabled) is not { } brief)
        {
            return announcement;
        }

        using var budget = new CancellationTokenSource(FlavourBudget);

        var line = await FlavourTurn.AskAsync(
            Turns.Provider,
            Turns.Model,
            brief.NeedsPersona ? Personas.RenderBlock(personalityEnabled: true) : brief.Speaker,
            brief.Instruction,
            brief.NeedsGameState ? Turns.LiveGameState?.Invoke() : null,
            Spend,
            PriceTable.Default,
            _logger,
            budget.Token).ConfigureAwait(false);

        return line is null ? announcement : announcement with { Text = line };
    }

    /// <summary>
    /// Whether a web lookup could actually be run right now. <b>Both halves, and the endpoint
    /// half is not the Commander's doing</b> — pointing <c>llm.endpoint</c> at a gateway turns
    /// this off whatever the setting says, because a server-side search tool is the provider's to
    /// offer. The same pair <see cref="TurnLoop"/> checks, read the same way.
    /// </summary>
    private bool CanSearch =>
        Settings.Current.Llm.WebSearch && Turns.Provider is not null && SearchReachesTheWeb;

    /// <summary>
    /// The endpoint half on its own — whether the provider and model in use offer a server-side
    /// search at all, ignoring whether the Commander has asked for one.
    /// <para>
    /// Split out because the two halves have <b>different remedies</b> and the disclosure and the
    /// prompt both have to name the right one: the setting is a toggle the Commander owns, and
    /// the endpoint is not theirs at all. Telling somebody to flip a switch that will not help is
    /// worse than saying nothing.
    /// </para>
    /// <para>
    /// True when no provider is selected. That is not "search works" — no turn runs at all — but
    /// there is nothing about <em>search</em> to report there, and the language-model row already
    /// says the real thing.
    /// </para>
    /// </summary>
    private bool SearchReachesTheWeb =>
        Turns.Provider is not { } provider
        || provider.CapabilitiesFor(Turns.Model ?? provider.DefaultModel).SupportsWebSearch;

    /// <summary>
    /// The same lore remark, told that nothing further is coming when nothing further can.
    /// Everything that is not a lore remark, or that is owed nothing, passes through untouched.
    /// </summary>
    private Announcement Owing(Announcement announcement) =>
        LoreCallout.AddressOf(announcement.Key) is not null
        && Settings.Current.Callouts.Lore == LoreRemarks.Lookup
        && !CanSearch
            ? announcement with { Text = $"{announcement.Text} {LoreLookup.CannotSearch}" }
            : announcement;

    /// <summary>
    /// One web search about a system, for the notes window — the same call the arrival lookup
    /// makes, so a note is corroborated by exactly what a Commander would have heard.
    /// </summary>
    private Task<string?> SearchForAsync(string systemName, CancellationToken cancellationToken) =>
        FlavourTurn.AskAsync(
            Turns.Provider,
            Turns.Model,
            persona: null,
            LoreLookup.Instruction(systemName),
            gameState: null,
            Spend,
            PriceTable.Default,
            _logger,
            cancellationToken,
            webSearch: true);

    /// <summary>
    /// The second half of an arrival remark: a web search, and what it found (list.md Phase 23,
    /// "Look it up, and say where the answer came from").
    /// <para>
    /// Fire and forget by design. Nothing is waiting on it, the Commander has already been told
    /// the fact, and a result that never arrives is a result that is simply not spoken — which is
    /// the same contract every flavour line has.
    /// </para>
    /// </summary>
    private void LookUpLore(Announcement announcement)
    {
        if (LoreCallout.AddressOf(announcement.Key) is not { } address
            || Settings.Current.Callouts.Lore != LoreRemarks.Lookup
            || !CanSearch)
        {
            return;
        }

        // The name as the journal spelled it, taken now rather than when the answer lands: by
        // then the Commander may be somewhere else, and this is the system being asked about.
        var name = GameState.Active?.Location.StarSystem
                   ?? Core.Knowledge.LoreDirectory.ByAddress(address)?.Name;

        if (name is null)
        {
            return;
        }

        _ = Task.Run(async () =>
        {
            using var budget = new CancellationTokenSource(LoreLookup.Budget);

            var found = await FlavourTurn.AskAsync(
                Turns.Provider,
                Turns.Model,

                // No persona block. This is a report of what a search returned, and a core's
                // voice is for what d47 has to say rather than for what somebody else wrote —
                // which is the whole of the rule the attribution below carries.
                persona: null,
                LoreLookup.Instruction(name),
                gameState: null,
                Spend,
                PriceTable.Default,
                _logger,
                budget.Token,
                webSearch: true).ConfigureAwait(false);

            // Dropped rather than spoken when the Commander has moved on. They may be interdicted
            // or three jumps away by now, and a sentence about a system they left is worse than
            // silence.
            if (LoreLookup.Spoken(found) is not { } line)
            {
                return;
            }

            if (!LoreLookup.StillHere(address, GameState.Active?.Location.SystemAddress))
            {
                _logger.LogInformation("A lore lookup for {System} landed after the Commander had left", name);
                return;
            }

            await _speaking.WaitAsync().ConfigureAwait(false);

            try
            {
                await SayAsync(new Announcement($"{LoreCallout.KeyPrefix}search.{address}", line))
                    .ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "A lore lookup could not be spoken");
            }
            finally
            {
                _speaking.Release();
            }
        });
    }

    /// <summary>
    /// A turn the Commander addressed to a crew member. Swaps the prompt block and the voice for
    /// the duration and puts the ship's AI back afterwards, which is why it is a scope rather
    /// than two calls somebody has to remember to pair.
    /// </summary>
    public sealed class CrewTurn(AppHost host, CrewAddressed addressed, string? persona, VoiceSelection voice)
        : IDisposable
    {
        /// <summary>What to ask, with the name taken off the front.</summary>
        public string Question { get; } =
            addressed.Question.Length == 0 ? "The Commander is trying to get your attention." : addressed.Question;

        public CrewMember Member => addressed.Member;

        public void Dispose()
        {
            host.Turns.Persona = persona;
            host.Voice.Voice = voice;
        }
    }

    /// <summary>
    /// Whether this input was addressed to somebody in the fighter bay rather than to the ship's
    /// AI, and if so, everything needed to answer as them (list.md Phase 11, "Ship Crew").
    /// <para>
    /// Matched model-free against the names the journal reports, so no round trip is spent
    /// working out who a round trip is for, and so this works with no model at all — in which
    /// case the turn falls through to the keyword router as it always did, and the crew member
    /// simply has nothing to say.
    /// </para>
    /// </summary>
    public CrewTurn? BeginCrewTurn(string input)
    {
        if (GameState.Active?.Crew is not { Any: true } crew
            || CrewAddressing.Match(input, crew) is not { } addressed)
        {
            return null;
        }

        var persona = Turns.Persona;
        var voice = Voice.Voice;

        _logger.LogInformation("Turn addressed to crew member {Name}", addressed.Member.Name);

        // Not a Guardian core. The crew are human pilots hired at a station, and handing one of
        // them a persona block would put a core in two places at once.
        Turns.Persona = CrewAddressing.Brief(addressed.Member, GameState.Active?.Ship.Name);
        Voice.Voice = Cast.ForSender(addressed.Member.Name, isPlayer: false, VoiceRole.Crew);

        return new CrewTurn(this, addressed, persona, voice);
    }

    private string? _voiceScopeSystem;

    /// <summary>
    /// Drops the NPC voice assignments when the Commander arrives somewhere new. The cast turns
    /// over on a jump; a wingmate does not (list.md Phase 11, "Voices stick").
    /// </summary>
    private void FollowSystemForVoices()
    {
        // The Commander's own name, so their own messages are not read back to them. Set here
        // because the journal header is what supplies it, and that has not been read at startup.
        if (GameState.Active?.Identity.Name is { Length: > 0 } commander)
        {
            foreach (var callout in Callouts.Callouts.OfType<IncomingMessages>())
            {
                callout.CommanderName = commander;
            }
        }

        var system = GameState.Active?.Location.StarSystem;

        if (system is null || string.Equals(system, _voiceScopeSystem, StringComparison.Ordinal))
        {
            return;
        }

        // Not on the first sample. Startup is not an arrival, and there is nothing assigned yet
        // to drop.
        if (_voiceScopeSystem is not null)
        {
            Cast.EnteredSystem();
        }

        _voiceScopeSystem = system;
    }

    /// <summary>
    /// Sounds what came due, and says which (list.md Phase 24, "A timer says its own name").
    /// <para>
    /// <b>The cue says <em>something finished</em> and d47 speaks the name.</b> One shipped clip
    /// for every timer rather than a synthesised tone per timer: per-timer tones are genuinely
    /// useful in a headset where you cannot glance, but they are new machinery in the audio path
    /// for a distinction the voice already makes better.
    /// </para>
    /// <para>
    /// Through the one arbiter, like all other audio, which is what decides whether the chime
    /// waits for a sentence to end or lands on top of it.
    /// </para>
    /// <para>
    /// <b>A missed alarm gets no cue.</b> A chime says "now", and this one means "nine hours ago"
    /// — so the sentence carries it alone, which is the whole of "reported afterwards, never
    /// faked".
    /// </para>
    /// </summary>
    private void SoundReminders(IReadOnlyList<Fired> fired)
    {
        if (fired.Count == 0)
        {
            return;
        }

        var zone = TimeZoneInfo.Local;

        foreach (var (reminder, missed) in fired)
        {
            _logger.LogInformation(
                "{Kind} \"{Name}\" {What}",
                reminder.Kind,
                reminder.Name,
                missed ? "was due while d47 was closed" : "went off");

            if (!missed && Voice.CuesEnabled)
            {
                Audio.Enqueue(new Core.Audio.AudioRequest
                {
                    Channel = Core.Audio.AudioChannel.Cue,
                    Clip = Cues.For(Core.Audio.AlertCue.TimerElapsed),
                });
            }

            var said = missed ? reminder.AnnounceMissed(zone) : reminder.Announce();

            _ = Voice.AnnounceAsync(said);
            Said?.Invoke(said);
        }
    }

    private void SpeakPendingCallouts()
    {
        var pending = Callouts.Drain();

        if (pending.Count == 0)
        {
            return;
        }

        _ = Task.Run(async () =>
        {
            // Varied before the lock is taken, never while holding it. This is a network round
            // trip, and the batch behind it is where a danger callout would be waiting — an
            // alert queued behind a carrier saying hello is an alert that arrives late.
            var lines = new List<Announcement>(pending.Count);

            foreach (var announcement in pending)
            {
                lines.Add(await VaryAsync(announcement).ConfigureAwait(false));
            }

            // Whether the lore remarks in this batch are owed a second part, decided here rather
            // than inside the speaking loop: a Commander who asked for a lookup the endpoint
            // cannot run is told so in the first sentence, and the alternative is leaving them
            // waiting for something that was never coming (list.md Phase 23).
            lines = [.. lines.Select(Owing)];

            // One at a time, and in order. A previous batch still being spoken holds this one
            // until it finishes rather than talking over it.
            await _speaking.WaitAsync().ConfigureAwait(false);

            try
            {
                foreach (var announcement in lines)
                {
                    await SayAsync(announcement).ConfigureAwait(false);

                    // After the fact has been spoken, and not awaited: the search is a round trip
                    // through somebody else's index, and the rest of this batch is where a danger
                    // callout would be waiting. It takes the speaking lock again when it lands.
                    LookUpLore(announcement);
                }
            }
            catch (Exception ex)
            {
                // A callout that cannot be synthesised is a callout the Commander does not hear.
                // It is already in the log as text from the engine, so this records why it was
                // not also audible rather than losing the fact entirely.
                _logger.LogError(ex, "A callout could not be spoken");
            }
            finally
            {
                _speaking.Release();
            }
        });
    }

    private string? _openDevice;

    private void OnSettingsChanged(SettingsChanged change)
    {
        // Which subsystem a key reaches is decided in Core, where every row can be asserted
        // against the one it is supposed to re-apply. What each of them does is still here.
        var fanout = SettingsFanout.For(change.Key);

        switch (fanout.Subsystem)
        {
            case SettingsSubsystem.LanguageModel:
                ApplyLlmSettings();
                break;

            case SettingsSubsystem.Speech:
                ApplySpeechSettings();
                break;

            case SettingsSubsystem.Audio:
                // Straight onto the arbiter, which re-levels whatever is already playing. A mixer
                // that only took effect on the next clip would be a mixer the Commander cannot
                // hear themselves using.
                Audio.Mix = Settings.Current.Audio;

                // Muting the ambience stops it rather than playing it at nothing, and unmuting it
                // starts a track rather than waiting for the next time the Commander docks —
                // which could be an hour, and reads as a switch that did not work.
                if (Settings.Current.Audio.Music.Muted)
                {
                    Audio.StopMusic();
                }
                else if (!Audio.Activity.MusicPlaying)
                {
                    PlayNextTrack();
                }

                break;

            case SettingsSubsystem.Callouts:
                ApplyCalloutSettings(Callouts, Settings.Current);
                break;

            case SettingsSubsystem.Listening:
                ApplyListeningSettings();
                break;

            case SettingsSubsystem.Persona:
                ApplyPersonaSettings();
                break;

            default:
                break;
        }

        // After the apply, as it was when this was an if/else chain. The guard inside is "does
        // the core have a voice already", so a row set to a voice falls straight through and
        // nothing is asked of the model.
        if (fanout.ChooseVoiceForCoreAboard)
        {
            _ = EnsureVoiceForCurrentPersonaAsync();
        }
    }

    /// <summary>
    /// The secret store is the real home for a key. The environment variable stays supported
    /// as the way to run d47 from a shell that already has one, and the store wins when both
    /// are present.
    /// <para>
    /// Only the <em>source</em> is ever logged, never the key.
    /// </para>
    /// </summary>
    /// <summary>
    /// How long a key check may take before it is reported as unreachable rather than as wrong.
    /// Short, because this runs while somebody is looking at it — and because the failure it is
    /// distinguishing is "no network", which does not get better by waiting.
    /// </summary>
    private static readonly TimeSpan KeyCheckBudget = TimeSpan.FromSeconds(20);

    /// <summary>
    /// Tries the stored language-model key for real (list.md Phase 16, "a key is verified, not
    /// merely stored").
    /// <para>
    /// The smallest turn that proves a key: one token, no tools, no persona, no game state. It
    /// costs a fraction of a cent and it is the only thing that can tell a good key from one that
    /// is revoked, mistyped, or carrying the newline a browser copy put on the end.
    /// </para>
    /// <para>
    /// <b>Rejected and unreachable are different answers and are kept apart.</b> Telling a
    /// Commander their key is wrong when the machine is offline sends them to their account page
    /// to issue another one, which will also fail.
    /// </para>
    /// </summary>
    private async Task<SecretCheck> VerifyLanguageModelKeyAsync(string providerId, CancellationToken cancellationToken)
    {
        var selected = LlmProviderCatalog.Selected(providerId);

        var resolved = ResolveKey(selected);

        if (selected.NeedsKey && resolved is null)
        {
            return SecretCheck.Rejected($"No {selected.Name} key is stored.");
        }

        var provider = LlmProviderFactory.Create(selected, resolved?.Key, Settings.Current.Llm.Endpoint);

        if (provider is null)
        {
            return SecretCheck.Unreachable(LlmProviderFactory.ReasonForNoClient(selected));
        }

        using var budget = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        budget.CancelAfter(KeyCheckBudget);

        // An OpenAI-shaped endpoint is checked by asking it what it serves rather than by
        // spending a turn on it (list.md Phase 29), and that is the same reasoning the speech key
        // check follows: it proves the exact call d47 makes anyway rather than a proxy for it.
        //
        // Three things make it the better probe here. It works with **no key**, which is the
        // configuration this phase exists to reach and about which "is this key good" is not a
        // question. It works with **no model selected**, which a local server that serves
        // whatever was loaded into it may well be. And it does not meet the trap the one-token
        // turn below has: a reasoning model spends that budget entirely on reasoning, or rejects
        // it outright, and either would be reported as a bad key.
        if (provider is ChatCompletionsLlmProvider or ResponsesLlmProvider)
        {
            var asked = provider switch
            {
                ChatCompletionsLlmProvider chat => await chat.ListModelsAsync(budget.Token).ConfigureAwait(false),
                ResponsesLlmProvider responses => await responses.ListModelsAsync(budget.Token).ConfigureAwait(false),
                _ => EndpointModels.Unreachable(null),
            };

            return asked.Reach switch
            {
                // Reached and refused. A stored key that is wrong, or an endpoint that wants one
                // and has been given nothing — either way the Commander has something to change.
                EndpointReach.Refused => SecretCheck.Rejected(asked.Detail ?? $"{selected.Name} refused the request."),

                EndpointReach.Answered when asked.Ids.Count > 0 => SecretCheck.Works(
                    $"{selected.Name} answered — {asked.Ids.Count} models."),

                // Answered with an empty catalogue, which is a gateway's prerogative and not a
                // fault. Reported as working, because reaching it is what was being asked.
                EndpointReach.Answered => SecretCheck.Works(
                    $"{selected.Name} answered, but lists no models. Type the model name yourself."),

                _ => SecretCheck.Unreachable(asked.Detail ?? $"{selected.Name} could not be reached."),
            };
        }

        var request = new LlmRequest
        {
            Model = Settings.Current.Llm.Model ?? selected.DefaultModel ?? provider.DefaultModel,
            Prompt = new PromptAssembly
            {
                History = [new ConversationMessage(ConversationRole.User, "Reply with the single word OK.")],
            },
            Effort = ThinkingEffort.Low,

            // Enough room to say one word, rather than exactly one token. A model that thinks
            // before answering spends this budget on the thinking and stops, which arrives here
            // as a truncation and is indistinguishable from the key having worked — but a
            // gateway that validates the field would reject a budget of 1 outright, and that
            // arrives as the key having failed. This is still a fraction of a cent.
            MaxOutputTokens = 64,
        };

        try
        {
            await foreach (var step in provider.StreamAsync(request, budget.Token).ConfigureAwait(false))
            {
                // A failure the provider itself classified. Transient is the network's problem
                // and permanent is the key's, which is exactly the distinction this returns.
                if (step is LlmStreamEvent.Failed failure)
                {
                    return failure.Transient
                        ? SecretCheck.Unreachable(failure.Message)
                        : SecretCheck.Rejected(failure.Message);
                }
            }

            // Reaching the end of the stream without a failure is the provider having accepted
            // the key. Whether it said "OK" is not the question — being allowed to ask is.
            return SecretCheck.Works($"{selected.Name} accepted the key.");
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return SecretCheck.Unreachable($"{selected.Name} did not answer within {KeyCheckBudget.TotalSeconds:0} seconds.");
        }
        catch (Exception ex)
        {
            // Never the key, at any level — the message is the exception's and the exception
            // never held it.
            _logger.LogWarning(ex, "The {Provider} key check could not be completed", selected.Name);
            return SecretCheck.Unreachable(ex.Message);
        }
    }

    /// <summary>
    /// Tries the stored speech key for real, against the provider's own voice list — which is the
    /// call d47 makes anyway the moment a key lands, so this proves the exact thing that has to
    /// work rather than a proxy for it.
    /// </summary>
    private async Task<SecretCheck> VerifySpeechKeyAsync(string providerId, CancellationToken cancellationToken)
    {
        var selected = TtsProviderCatalog.Selected(providerId);

        if (selected.KeySecretName is not { } name)
        {
            return SecretCheck.Works($"{selected.Name} needs no key.");
        }

        if (!Secrets.TryGet(name, out var key))
        {
            return SecretCheck.Rejected($"No {selected.Name} key is stored.");
        }

        using var budget = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        budget.CancelAfter(KeyCheckBudget);

        // Its own instance rather than the live one, so a refusal surfaces here as a verdict
        // instead of being swallowed by the background refresh's catch. Refreshing the cache is
        // not this method's job: storing the key already raised a settings change, which
        // ApplySpeechSettings turns into a refetch on the key-presence edge.
        ITtsProvider? provider = selected.Id switch
        {
            SpeechCapability.ElevenLabsId => new ElevenLabsTtsProvider(
                () => key,
                _loggerFactory.CreateLogger<ElevenLabsTtsProvider>()),

            _ => null,
        };

        if (provider is null)
        {
            return SecretCheck.Unreachable($"D47 has no client for {selected.Name} yet.");
        }

        try
        {
            var voices = await provider.ListVoicesAsync(budget.Token).ConfigureAwait(false);

            // Read from the listing rather than from the count, which is what this check was
            // quietly doing wrong: the provider answers an empty list rather than throwing, so a
            // rejected key arrived here as "accepted the key — 0 voices" (list.md Phase 19).
            return voices.Listing switch
            {
                VoiceListing.KeyRejected => SecretCheck.Rejected(
                    $"{selected.Name} refused the key{Reason(voices.Detail)}"),

                VoiceListing.Unreachable => SecretCheck.Unreachable(
                    $"{selected.Name} could not be reached{Reason(voices.Detail)}"),

                VoiceListing.NoKey => SecretCheck.Rejected($"No {selected.Name} key is stored."),

                _ => SecretCheck.Works($"{selected.Name} accepted the key — {voices.Count} voices."),
            };
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return SecretCheck.Unreachable($"{selected.Name} did not answer within {KeyCheckBudget.TotalSeconds:0} seconds.");
        }
        catch (TtsException ex)
        {
            // The provider's own refusal, which is the one case that means the key is wrong.
            return SecretCheck.Rejected(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "The {Provider} key check could not be completed", selected.Name);
            return SecretCheck.Unreachable(ex.Message);
        }
    }

    /// <summary>The service's own words where it gave any, punctuated to finish the sentence.</summary>
    private static string Reason(string? detail) =>
        detail is { Length: > 0 } said ? $" — {said}." : ".";

    private (string Key, string Source)? ResolveKey(LlmProviderInfo provider)
    {
        if (provider.KeySecretName is not { } name)
        {
            return null;
        }

        if (Secrets.TryGet(name, out var stored))
        {
            return (stored, "the secret store");
        }

        // Only Anthropic has a conventional environment variable worth honouring.
        if (provider.Id != LlmProviderCatalog.AnthropicId)
        {
            return null;
        }

        var fromEnvironment = Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY");
        return string.IsNullOrWhiteSpace(fromEnvironment)
            ? null
            : (fromEnvironment, "the ANTHROPIC_API_KEY environment variable");
    }

    /// <summary>
    /// Where Elite might be installed, for the shipped control presets. Best effort and
    /// possibly empty: d47 does not require an Elite install to be locatable, and a Commander
    /// on a custom preset never needs one (architecture.md D4, trap 2).
    /// </summary>
    /// <summary>
    /// Joins the two halves of the game-state block, dropping whichever is absent. Both are
    /// null when there is nothing to say, and a heading with nothing under it still costs
    /// tokens on every turn.
    /// </summary>
    private static string? Join(string? situation, string? actions) =>
        (situation, actions) switch
        {
            (null, null) => null,
            (null, var only) => only,
            (var only, null) => only,
            var (both, and) => both + Environment.NewLine + Environment.NewLine + and,
        };

    /// <summary>
    /// Whether a route to the named system appeared after a plotting attempt.
    /// <para>
    /// In the app rather than in Core because it waits, and no Core component reads the clock.
    /// It polls the reader directly rather than joining the tick loop: this is a question with
    /// a beginning and an end, asked by one caller, and a tick subscriber for it would outlive
    /// the question by the rest of the session.
    /// </para>
    /// <para>
    /// Null rather than false when the file never becomes readable at all — "I cannot tell" and
    /// "it did not work" send the Commander to different places.
    /// </para>
    /// </summary>
    private static async Task<bool?> ConfirmPlot(
        NavRouteReader route,
        string system,
        CancellationToken cancellationToken)
    {
        // Elite writes NavRoute.json as the route is accepted, which is quick, but the map
        // animates first. Six seconds is long enough to cover that and short enough that a
        // Commander waiting on the answer has not already looked.
        var deadline = DateTimeOffset.Now + TimeSpan.FromSeconds(6);
        var sawTheFile = false;

        while (DateTimeOffset.Now < deadline)
        {
            route.Poll();

            if (route.Current.ReadAt is not null)
            {
                sawTheFile = true;

                if (route.Current.Hops.Count > 0 &&
                    string.Equals(route.Current.Hops[^1].StarSystem, system, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            try
            {
                await Task.Delay(TimeSpan.FromMilliseconds(250), cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                return null;
            }
        }

        return sawTheFile ? false : null;
    }

    /// <summary>
    /// Every phrase d47 already answers to. A macro may not take one of these, because a macro
    /// called "gear down" would shadow a phrase that already means something and the Commander
    /// would have no way to tell which one ran.
    /// </summary>
    /// <summary>
    /// Every journal on the disk, oldest first, for the Commander's log (list.md Phase 33).
    /// <para>
    /// Name order rather than filesystem timestamps, because Elite's filenames already encode the
    /// session start and that is what survives a copy — the same judgement
    /// <see cref="D47.Core.Journal.JournalFolder.LatestFile"/> makes and for the same reason. The
    /// window narrows this to a handful of files before a line is read; see
    /// <see cref="D47.Core.Logbook.LogRanges.FilesFor"/>.
    /// </para>
    /// </summary>
    private static IReadOnlyList<string> JournalsOnDisk(string directory, Microsoft.Extensions.Logging.ILogger logger)
    {
        try
        {
            return Directory.Exists(directory)
                ?
                [
                    .. Directory.EnumerateFiles(directory, D47.Core.Journal.JournalFolder.FilePattern)
                        .OrderBy(Path.GetFileName, StringComparer.Ordinal),
                ]
                : [];
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            logger.LogWarning(ex, "Could not list the journals in {Directory}", directory);
            return [];
        }
    }

    private static IReadOnlyList<string> PhrasesAlreadyTaken(CapabilityRegistry? registry) =>
        registry is null
            ? []
            : [
                .. registry.All.SelectMany(c => c.Descriptor.Keywords),
                .. registry.All.SelectMany(c => c.Descriptor.InterruptKeywords),
                .. registry.All.SelectMany(c => c.Descriptor.Tools).SelectMany(t => t.Commands)
                    .Select(command => command.Phrase),
                .. registry.All.SelectMany(c => c.Descriptor.Settings).SelectMany(row => row.Commands)
                    .Select(command => command.Phrase),
            ];

    private static IReadOnlyList<string> EliteInstallations()
    {
        var candidates = new List<string?>
        {
            Environment.GetEnvironmentVariable("D47_ELITE_DIR"),
        };

        foreach (var root in (ReadOnlySpan<Environment.SpecialFolder>)
                 [Environment.SpecialFolder.ProgramFilesX86, Environment.SpecialFolder.ProgramFiles])
        {
            var folder = Environment.GetFolderPath(root);

            if (folder.Length == 0)
            {
                continue;
            }

            candidates.Add(Path.Combine(folder, "Steam", "steamapps", "common", "Elite Dangerous"));
            candidates.Add(Path.Combine(folder, "Frontier", "EDLaunch", "Products"));
            candidates.Add(Path.Combine(folder, "Epic Games", "EliteDangerous"));
        }

        return [.. candidates.Where(path => !string.IsNullOrWhiteSpace(path) && Directory.Exists(path))!];
    }

    /// <summary>
    /// The real Elite Dangerous journal folder, unless overridden — useful for developing and
    /// testing d47 without needing a live game session.
    /// </summary>
    private static string ResolveJournalDirectory()
    {
        var overridePath = Environment.GetEnvironmentVariable("D47_JOURNAL_DIR");
        return string.IsNullOrWhiteSpace(overridePath) ? JournalFolder.DefaultPath() : overridePath;
    }

    /// <summary>
    /// Why d47 is stopping, for the shutdown line (remediation.md 10, item 7).
    /// <para>
    /// Set by whoever knows — the window on its way closed, the updater handing over to the build
    /// that replaces this one. The default is what is honestly knowable otherwise: a Windows
    /// shutdown and a task manager kill both unwind through here saying nothing about themselves,
    /// and a reason invented for them would be a reason a Commander might believe.
    /// </para>
    /// </summary>
    public string StoppingBecause { get; set; } = "the process is ending";

    /// <summary>
    /// What this build is, what it is pointed at, and what came up — written once, at the moment
    /// everything that can answer has (remediation.md 10, item 7).
    /// <para>
    /// Called from the composition root rather than from <see cref="Start"/>, because the headset
    /// is brought up after the framework is and <see cref="Start"/> would have to guess at it. A
    /// log that opens with this is a log that can answer "what was it even running" without the
    /// Commander being asked.
    /// </para>
    /// </summary>
    public void RecordStartup()
    {
        var current = Settings.Current;

        _logger.LogInformation(
            "d47 {Version} started. Model: {Provider}/{Model}. Speech: {Speech}. "
            + "Hearing: {Whisper}, {Listening}. Headset: {Vr}. Data: {Data}",
            Version,
            LlmProviderCatalog.Selected(current.Llm.Provider)?.Name ?? current.Llm.Provider,

            // A provider with no model chosen is the state on a fresh install, and "Anthropic/null"
            // is a line that reads as a fault rather than as a setting nobody has set yet.
            current.Llm.Model is { Length: > 0 } model ? model : "no model chosen",
            current.Speech.Provider,
            current.Listening.Model,
            current.Listening.Mode,
            Vr is { } headset
                ? $"{headset.State}{(current.Vr.Enabled ? string.Empty : " (switched off)")}"
                : "not started",
            Paths.Data);

        if (StartupError is { Length: > 0 } failure)
        {
            _logger.LogWarning("Settings did not load cleanly at startup: {Problem}", failure);
        }
    }

    public void Dispose()
    {
        // First, so the reason survives whatever the teardown below does. The matching "stopped
        // cleanly" line is the last thing written, and its absence is the marker: a shutdown that
        // says it is starting and never says it finished died on the way out.
        _logger.LogInformation("d47 {Version} is stopping: {Why}", Version, StoppingBecause);

        CoverageRecorder?.Save();

        // When the core aboard stopped being aboard, which is now (list.md Phase 35). Every other
        // core's stamp was written as it was switched away from; this is the one that never is,
        // and without it the core a Commander closes d47 on is the one core that could never earn
        // a gap reaction. A crash loses it, which costs one reaction and nothing else.
        RememberCoreAboard(Personas.Current.Id, DateTimeOffset.Now);

        Settings.Changed -= OnSettingsChanged;
        Personas.Changed -= OnPersonaChanged;

        // The loop stops before anything it polls is torn down, so a tick cannot land on a
        // disposed sink or a closed file handle on the way out.
        _ticking?.Dispose();

        // After the tick, so a serve cannot land on a destroyed overlay handle. A quad nobody
        // gave back stays floating in the cockpit after the app that put it there has gone.
        Vr?.Dispose();
        _speaking.Dispose();

        // After the tick has stopped, so a poll cannot land on a disposed capture device.
        _pushToTalk.ForceUp();
        _microphone.Dispose();
        _transcriber.Dispose();
        (Models as IDisposable)?.Dispose();

        // Stop making noise before tearing anything down. Disposing the sink under a playing
        // clip is how an exit ends in a buzz rather than in silence.
        Audio.Silence();
        Audio.Dispose();
        _audioSink.Dispose();
        (_tts as IDisposable)?.Dispose();

        // Before the factory that owns the sink it writes to. Reaching this line is the whole of
        // the clean marker -- anything that threw above it leaves the "is stopping" line standing
        // on its own, which is what tells a reader the teardown is where to look.
        _logger.LogInformation("d47 stopped cleanly");

        _loggerFactory.Dispose();
        Log.CloseAndFlush();
    }
}
