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
using D47.Core.Debrief;
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
        D47.Core.Hotas.BoundButton pushToTalkButton,
        D47.Core.Hotas.PushToTalkSources pushToTalkSources,
        BindsWatch binds,
        ScancodeInjector gameInput,
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
        _pushToTalkButton = pushToTalkButton;
        _pushToTalkSources = pushToTalkSources;
        _gameInput = gameInput;
        Models = models;
        _transcriber = transcriber;
        Version = version;
        StartupError = startupError;

        // When each core was last aboard, from previous runs (Phase 35). Without this the
        // elapsed time a gap reaction is measured against would start at zero every launch, and a
        // month-long absence — which is the only kind that earns one — spans launches by
        // definition. No session goes with it: see the field.
        foreach (var (core, at) in viewState.Load().CoresLastAboard)
        {
            _personaLastSeen[core] = (at, null);
        }
    }

    /// <summary>
    /// Shows the changelog that shipped inside this build (#50). Filled by <c>MainWindow</c>,
    /// which is the only thing that has a window to open one over — the same joining-at-the-window
    /// shape <c>PersonaSettling</c> and <c>AudioReloaded</c> already use.
    /// </summary>
    public Action? ShowChangelog { get; set; }

    /// <summary>Reopens the guided key setup (#50). Filled by <c>MainWindow</c>, for the same reason.</summary>
    public Func<Task>? SetUpKeys { get; set; }

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
    /// The last few thousand journal events, kept so the Transcript can show them
    /// (https://github.com/dseelinger/d47/issues/51). Fed from the spine's own poll below, and
    /// never by opening the file a second time - Elite holds the current journal open.
    /// </summary>
    public D47.Core.Journal.JournalLog JournalLog { get; private set; } = new();

    /// <summary>
    /// The ~4-10 Hz loop (architecture.md §4). Exposed because a surface that needs sampling
    /// rather than events — push-to-talk edge detection, the VR connection state machine —
    /// registers here instead of growing a timer of its own.
    /// </summary>
    public TickLoop Tick { get; }

    /// <summary>
    /// What the panel is showing. Owned here rather than by the window, because it is app
    /// state: the desktop window and the headset overlay each instantiate a view against it,
    /// and a model owned by one of them would make the other a guest (Phase 9).
    /// </summary>
    public Panel.PanelViewModel Panel { get; } = new();

    /// <summary>
    /// The headset path, once Avalonia has come up. Null before that and on a run where the
    /// framework never initialises — it needs a dispatcher and a widget tree, neither of which
    /// exists when this host is built.
    /// </summary>
    public Headset.VrHost? Vr { get; set; }

    /// <summary>
    /// The flat mini panel over the game, once Avalonia has come up (Phase 48). Null
    /// before that and on a run where the framework never initialises, for the same reason
    /// <see cref="Vr"/> is: it is a widget tree, and there is no dispatcher to build one on when
    /// this host is constructed.
    /// </summary>
    public Windowing.OverlayPanel? Overlay { get; set; }

    /// <summary>
    /// Whether Elite is running and in front. Exposed because the overlay asks it on every tick
    /// to decide whether to be on screen — the same question, and the same instance, the key
    /// injector asks before every scancode.
    /// </summary>
    public IEliteWindow Elite { get; private set; } = null!;

    /// <summary>
    /// What d47 says without being asked (Phase 8). Exposed because the panel drains it:
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
    /// The handle on the turn in recording. A surface must run its turns under
    /// <see cref="TurnCancellation.Begin"/>, or "cancel" has nothing to cancel and the model
    /// keeps generating — and billing — after the Commander has called it off.
    /// </summary>
    public TurnCancellation Cancellation { get; }

    /// <summary>
    /// <b>Cancel</b>: stop talking, and abandon the turn that is running
    /// (<a href="https://github.com/dseelinger/d47/issues/221">#221</a>). Returns whether there
    /// was a turn to abandon.
    /// <para>
    /// <b>Silence first, and the order is load-bearing</b> — the same order <c>cancel_turn</c>
    /// uses, for the same reason. Cancelling tears down the stream, but whatever already reached
    /// the audio queue would otherwise play on after the turn behind it is gone, which sounds
    /// exactly like the cancel not having worked.
    /// </para>
    /// <para>
    /// <b>One method, three callers</b>: the hotkey, the stick button, and the spoken phrases that
    /// reach <c>cancel_turn</c>. A control that only silenced would leave a web search running and
    /// still being paid for, which is the case the Commander asked this for.
    /// </para>
    /// </summary>
    public bool CancelNow()
    {
        Audio.Silence();
        return Cancellation.Cancel();
    }

    public UpdateChecker Updates { get; }

    /// <summary>
    /// Records what has been exercised by hand, when this process was asked to. Null — and
    /// therefore absent from the panel too — in every normal run.
    /// </summary>
    public D47.App.Coverage.CoverageRecorder? CoverageRecorder { get; private set; }

    /// <summary>
    /// Retains what crossed the audio boundary in both directions, when this process was asked
    /// to (<a href="https://github.com/dseelinger/d47/issues/164">#164</a>). Null — and therefore
    /// absent from the settings surface too — in every normal run.
    /// </summary>
    public Recording.AudioRecorder? AudioRecorder { get; private set; }

    /// <summary>Fetches and installs what <see cref="Updates"/> found.</summary>
    public UpdateInstaller Installer { get; }

    /// <summary>
    /// Gives up this process's claim on being the only d47, so the build replacing it can start
    /// before this one has finished exiting. Set by the composition root; null under a test.
    /// </summary>
    public Action? ReleaseSingleInstance { get; set; }

    /// <summary>One turn of conversation, whichever path answers it.</summary>
    public TurnLoop Turns { get; }

    /// <summary>Which Guardian core is aboard, and what it remembers (Phase 11).</summary>
    public PersonaHost Personas { get; }

    /// <summary>
    /// Which core flies which ship (Phase 35). Public because the gesture reaches it:
    /// a system-wide hotkey is bound in the window and has to be able to perform the act, on
    /// exactly the same footing as the panel button and the phrase.
    /// </summary>
    public ShipCoreService ShipCores { get; }

    /// <summary>
    /// The watch that compares a boarded ship against its plan (Phase 38). Held here for
    /// one reason: it keeps the last ship seen as a bare id, and a Commander switch has to reset
    /// it (Phase 44). Set after construction like the other services the tick registers.
    /// </summary>
    public ShipDriftWatch? Drift { get; set; }

    /// <summary>
    /// The session's opening line (Phase 31), held here so a Commander switch can make
    /// it due again (Phase 44, "Welcome back, Commander").
    /// </summary>
    public ContinuityCallout? Continuity { get; set; }

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
    /// When this process started, for the one question that needs it: what "this session" means as
    /// a span of the spend ledger (<a href="https://github.com/dseelinger/d47/issues/197">#197</a>).
    /// <para>
    /// The ledger records instants and not session ids, so the session can only be "everything
    /// since launch" — derivable, and not a concept the file holds. Stamped once here rather than
    /// asked of the process, so a replay and a real run answer the same way.
    /// </para>
    /// </summary>
    public DateTimeOffset LaunchedAt { get; } = SystemWallClock.Instance.UtcNow;

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
    /// clip it already holds and a reload can never cut a sentence (Phase 12).
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
    /// What removes d47's own voice from what the microphone hears (Phase 13). Exposed
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
    /// The Commander's checklist, and the proposals waiting on it (Phase 17). The panel
    /// writes through this like the macro editor does — and it is the surface that accepts a
    /// proposal, which is an act the model is not allowed to perform.
    /// </summary>
    public ChecklistService Checklists { get; private set; } = null!;

    /// <summary>The Commander's timers and alarms (Phase 24).</summary>
    public Timekeeper Timekeeper { get; private set; } = null!;

    /// <summary>The Commander's ship builds, joined to the fleet (Phase 26).</summary>
    public ShipPlanService Ships { get; private set; } = null!;

    /// <summary>Where the builds are kept, for the panel to follow and for a hand edit to reach.</summary>
    public ShipBuildStore ShipBuilds { get; private set; } = null!;

    /// <summary>
    /// The Commander's suit and weapon plans, joined to what they are wearing (Phase 27).
    /// The on-foot half of the same page.
    /// </summary>
    public D47.Core.Loadout.OnFootPlanService OnFootPlans { get; private set; } = null!;

    /// <summary>Where those are kept, for the panel to follow and for a hand edit to reach.</summary>
    public D47.Core.Loadout.OnFootBuildStore OnFootBuilds { get; private set; } = null!;

    /// <summary>
    /// Which engineer to go and get next, read across both plan stores (Phase 28). Owns
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
    /// What the settings surface needs to walk and assign a HOTAS switch (Phase 21).
    /// Null when nothing composed hardware, which is what the designer and a test that is not
    /// about switches get — the row's button is then absent rather than dead.
    /// </summary>
    public Settings.SwitchEditing? SwitchEditing { get; private set; }

    /// <summary>
    /// What the settings surface needs to show and write the Commander's own lore notes
    /// (Phase 23). Null under the designer, where the row shows a summary and no button.
    /// </summary>
    public Settings.LoreEditing? LoreEditing { get; private set; }

    /// <summary>
    /// What d47 remembers about the Commander, and the clock a fact typed on the panel is stamped
    /// with (Phase 31). Null under the designer, where the row shows a summary and no
    /// button.
    /// </summary>
    public (MemoryBook Book, Func<DateTimeOffset> Now)? Memories { get; private set; }

    /// <summary>
    /// The standing directions the debrief pass drafts and the Commander adopts, and the clock an
    /// adoption is stamped with (<a href="https://github.com/dseelinger/d47/issues/162">#162</a>).
    /// Null under the designer, where the row shows a summary and no button.
    /// </summary>
    public (DebriefBook Book, Func<DateTimeOffset> Now)? Debrief { get; private set; }

    /// <summary>
    /// What this session has sounded like, in memory and never on disk
    /// (<a href="https://github.com/dseelinger/d47/issues/162">#162</a>). Read once, at the end,
    /// by the pass; emptied at every session boundary.
    /// </summary>
    public DebriefSession Debriefing { get; } = new();

    /// <summary>
    /// The feedback nobody typed, collected across the session and turned into questions by the
    /// pass (<a href="https://github.com/dseelinger/d47/issues/162">#162</a>). A plain list behind
    /// its own lock, because it is written from the UI thread and from the tick and read once.
    /// </summary>
    private readonly List<DebriefSignal> _signals = [];

    private readonly Lock _signalGate = new();

    /// <summary>
    /// What the prompt carries for the length of this session
    /// (<a href="https://github.com/dseelinger/d47/issues/162">#162</a>). A latch rather than a
    /// convention: adopting a direction mid-flight writes a file and cannot move a byte above the
    /// cache breakpoint, which is what Phase 54's 23x makes non-negotiable.
    /// </summary>
    private readonly StandingDirectionsSession _directions = new();

    /// <summary>
    /// The Commander's log (Phase 33). Null under the designer, where the row reads a
    /// folder that does not exist and offers no way to spend anything.
    /// </summary>
    public D47.Core.Logbook.LogbookBook? Logbook { get; private set; }

    /// <summary>
    /// The Commander's long arcs (Phase 34). Null under the designer, where the checklist
    /// page draws no arc band and the row shows a summary and no button.
    /// </summary>
    public (D47.Core.Goals.GoalBook Book, Action? Backfill)? Goals { get; private set; }

    /// <summary>
    /// The stories the Commander flies, and the thing that writes one (Phase 47). Null
    /// under the designer, where the tab is simply not furnished.
    /// </summary>
    public (D47.Core.Adventures.AdventureBook Book, D47.Core.Adventures.AdventureGenerator Generator)? Adventures { get; private set; }

    /// <summary>The galaxy service, for the adventure editor to check a typed place against (Phase 47).</summary>
    public D47.Core.Knowledge.IGalaxyService? Galaxy { get; private set; }

    /// <summary>Where Elite writes its journals, for the adventure catch-up that walks them.</summary>
    public string? JournalDirectory { get; private set; }

    /// <summary>
    /// The stored loadouts, for the row that describes them and the press that rebuilds them
    /// (<a href="https://github.com/dseelinger/d47/issues/128">#128</a>).
    /// </summary>
    private LoadoutStore? _loadouts;

    /// <summary>
    /// What this Commander has met and what their transcriber gets wrong (#134), for the settings
    /// row that shows it, the pre-pass that applies it and the lookup that learns it.
    /// </summary>
    private HeardNamesStore? _heardNames;

    /// <summary>
    /// The one name a lookup is waiting to be corrected about. Per process and never written
    /// down: it is the state of one exchange, not of an installation.
    /// </summary>
    private readonly MishearingWatch _mishearings = new();

    /// <summary>The outstanding mishearing, for the capability that asks about it.</summary>
    internal MishearingWatch Mishearings => _mishearings;

    /// <summary>Whether a rescan is already running. One at a time, like the model download.</summary>
    private int _rescanning;

    /// <summary>
    /// The last plan each planner produced (Phase 37). Set during composition, like the
    /// three books above, because the file it reads lives beside the executable rather than
    /// anywhere Core can find on its own.
    /// <para>
    /// Shared deliberately: the model's path writes it through <c>RouteCapability</c> and the
    /// Routing tab reads and writes the same one, so a route plotted by voice and a route drawn
    /// on screen cannot be two different routes.
    /// </para>
    /// </summary>
    public D47.Core.Knowledge.RoutePlanBook? Plans { get; private set; }

    /// <summary>
    /// The last commodity answer (Phase 49), so the spoken one and the drawn one are one
    /// answer. In memory rather than on disk, unlike <see cref="Plans"/>: a price is the thing
    /// here that ages fastest, and a saved one would look current because it was saved.
    /// </summary>
    public D47.Core.Knowledge.CommodityBoard Commodities { get; private set; } = new();

    /// <summary>
    /// The last shopping list for a construction site (Phase 50), on the same terms as
    /// <see cref="Commodities"/> and for the same reason.
    /// </summary>
    public D47.Core.Knowledge.SourcingBoard Sourcing { get; private set; } = new();

    /// <summary>
    /// What the Commander has told d47 is on their fleet carrier. On disk, unlike the two boards
    /// above: it is a statement of theirs rather than a price, so it is worth keeping across a
    /// restart — and it is dated wherever it is used, because d47 has no way of checking it.
    /// </summary>
    public D47.Core.Knowledge.CarrierManifest? Carrier { get; private set; }

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
    /// Something the Commander said that no turn is going to write down (change-requests.md 31).
    /// <para>
    /// <b>The Technical page, not the conversation.</b> What was heard before routing is the
    /// working behind an answer rather than part of the exchange, and the conversation is the one
    /// page that has to stay readable. It is the same reasoning that puts in-game comms there.
    /// </para>
    /// <para>
    /// Raised only where it adds something: an utterance a chooser consumed, which nothing else
    /// records at all, and one whose words the wake policy changed on the way to the turn. When
    /// the heard words and the asked words agree — the ordinary case — the turn already carries
    /// them and this stays quiet rather than printing everything twice.
    /// </para>
    /// </summary>
    public event Action<string>? HeardText;

    private void HeardAside(string text, string why)
    {
        if (text is { Length: > 0 })
        {
            HeardText?.Invoke($"{why}: {text}");
        }
    }

    /// <summary>
    /// Raised true when a core has been chosen and has not yet worked out what to say, and
    /// false when it has (Phase 12, "Anything that might take a moment says it is
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
            // without a restart" rule everything else follows (Phase 4).
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

    /// <summary>The stick's half of push-to-talk (Phase 53).</summary>
    private readonly D47.Core.Hotas.BoundButton _pushToTalkButton;

    /// <summary>The two of them as one gate. Either opens it; the last release closes it.</summary>
    private readonly D47.Core.Hotas.PushToTalkSources _pushToTalkSources;

    /// <summary>
    /// The one thing that presses a key in the game, held only so that shutting down lets go of
    /// whatever it is holding (<a href="https://github.com/dseelinger/d47/issues/206">#206</a>).
    /// <para>
    /// Everything that <em>uses</em> it reaches it through the action surface; this field exists
    /// because it was a local in the composition root and nothing disposed it, so the release
    /// its own summary calls "the last chance to let go" had no caller.
    /// </para>
    /// </summary>
    private readonly ScancodeInjector _gameInput;

    /// <summary>
    /// Cancel's stick button (<a href="https://github.com/dseelinger/d47/issues/221">#221</a>).
    /// <para>
    /// Built here rather than taken as a constructor argument like push-to-talk's, because nothing
    /// outside this class ever needs to reach it: push-to-talk's is handed in so composition can
    /// wire it to the gate's two sources, and this one has a single subscriber which is a method
    /// on this class.
    /// </para>
    /// </summary>
    private readonly D47.Core.Hotas.BoundButton _cancelButton = new();

    /// <summary>
    /// The controllers, for the one question the push-to-talk button has to ask outside the tick:
    /// whether the device list has stopped changing. Null until composition assigns it.
    /// </summary>
    public D47.Core.Hotas.IHotasReader? Controllers { get; private set; }

    public string Version { get; }

    /// <summary>
    /// Whether GitHub calls this build's Release a pre-release
    /// (<a href="https://github.com/dseelinger/d47/issues/92">#92</a>).
    /// <para>
    /// <b>Held here rather than stamped into the binary</b>, because it is a property of the
    /// Release and not of the build: promoting a pre-release changes it while the executable and
    /// its checksum stay exactly what they were, and a published tag never moves — so a stamped
    /// build would keep calling itself a pre-release for ever after being promoted.
    /// </para>
    /// <para>
    /// <see cref="D47.Core.Updates.ReleaseChannel.Unknown"/> until the check comes back, and
    /// again whenever it cannot. That state shows no marker at all, which is what a final release
    /// looks like — the difference being that it claims nothing rather than claiming that.
    /// </para>
    /// </summary>
    public D47.Core.Updates.ReleaseChannel Channel { get; internal set; }
        = D47.Core.Updates.ReleaseChannel.Unknown;

    /// <summary>For surfaces that need a logger of their own — the theme manager, so far.</summary>
    public ILoggerFactory Loggers => _loggerFactory;

    /// <summary>
    /// Set when settings could not be loaded. Surfaced on the panel rather than swallowed:
    /// starting on defaults without saying so would discard the Commander's configuration
    /// silently, which is the failure mode the two-store split exists to prevent.
    /// </summary>
    public string? StartupError { get; }

    public static AppHost Start() => Start(startTicking: true);

    /// <summary>
    /// The whole of <see cref="Start"/> except the last line
    /// (<a href="https://github.com/dseelinger/d47/issues/79">#79</a>). Everything is built,
    /// every capability is registered, the settings surface is bound and every tick subscriber
    /// is wired — and then the loop that would drive them is not started, and the host is
    /// returned for the caller to dispose.
    /// <para>
    /// <b>This is what <c>--selftest</c> runs, and it is the real composition rather than a copy
    /// of it.</b> A copy is precisely what let
    /// <a href="https://github.com/dseelinger/d47/issues/78">#78</a> through: the test surfaces
    /// mirrored this, the mirror was missing the four About rows, and two releases shipped that
    /// died here before drawing a window.
    /// </para>
    /// <para>
    /// <b>Safe beside a Commander's running copy</b>, which is the promise <c>--selftest</c>
    /// already makes and the reason this seam is where it is. <see cref="AppHost"/> claims no
    /// mutex, registers no global hotkey, attaches to no headset and opens no window — those
    /// four live in <c>Program</c> and <c>MainWindow</c>, which is what makes the seam viable at
    /// all.
    /// </para>
    /// <para>
    /// <b>It is not inert, though, and the list matters more than the reassurance.</b> Measured
    /// by running it: composing opens the default audio output, <em>opens the microphone</em>
    /// when listening is configured to want one, tails the journal, enumerates controllers, and
    /// reaches the network for the voice lists. Audio and capture are shared-mode and a second
    /// client does not evict the first; the journal and the controllers are read-only; the voice
    /// fetch is the same egress the settings surface already discloses. So it is safe to run
    /// beside a live copy, but it is not silent, and anyone shortening this to "it does nothing"
    /// would be wrong about five things.
    /// </para>
    /// </summary>
    internal static AppHost Compose() => Start(startTicking: false);

    private static AppHost Start(bool startTicking)
    {
        var paths = AppPaths.ForRunningBuild();
        paths.EnsureCreated();

        // **The version, not the stamp** (<a href="https://github.com/dseelinger/d47/issues/92">#92</a>).
        // This read BuildInfo.Full, which is version *and* commit — so About's Version row and its
        // Build row printed the same forty characters, and the row a bug report quotes could not
        // tell two builds of one release apart any better than the row beside it could.
        //
        // AboutCapability.Create's own parameter documents what belongs here: "the version a
        // Commander would quote — BuildInfo.Semantic". The distinction was lost when About became a
        // settings area; the old AboutWindow had it right, and BuildInfo has carried both values
        // the whole time.
        var version = BuildInfo.Semantic;

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

        // Immediately after it, because the thing this catches makes every line below it a
        // description of a build that is not running (bugs.md, 2026-08-23).
        StaleBuildCheck.Report(logger, Environment.ProcessPath ?? string.Empty);

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
        // (Phase 18).
        var sampling = new SamplingStore(
            Path.Combine(paths.Data, "sampling.json"),
            loggerFactory.CreateLogger<SamplingStore>());

        sampling.Load();

        // Systems worth remarking on, in two files with two different characters (Phase 23).
        // The book is the Commander's own words, so it is polled for hand edits and
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

        // Lazy, and deliberately so: it reads back through older journal files, and the answer is
        // wanted once, the first time a Commander is seen. Nothing pays for it at startup, and a
        // session that never establishes an identity never scans a thing.
        var recoveredFleets = new Lazy<IReadOnlyDictionary<string, FleetRegistry>>(
            () => FleetBackfill.FromHistory(journalDirectory, loggerFactory.CreateLogger(nameof(FleetBackfill))));

        // What every ship the Commander has flown was last seen holding, kept between sessions
        // (#128). The memory itself shipped in v0.41.1 and was rebuilt from 25 journals at every
        // start, so a ship not flown inside that window was forgotten on the next launch and
        // re-forgotten on every launch after it. A cache rather than a source of truth: deleting
        // it costs a rebuild from the journals and nothing else.
        var loadouts = new LoadoutStore(
            Path.Combine(paths.Data, "loadouts.json"),
            loggerFactory.CreateLogger<LoadoutStore>());

        loadouts.Load();

        // Every place this Commander has met, and what their transcriber gets wrong about them
        // (#134). Proper nouns are where speech recognition fails hardest and most silently, and
        // this is the catalogue a misheard one is matched against — 400 billion systems exist and
        // d47 ships no list, but the few thousand a Commander has actually stood in are on disk.
        var heardNames = new HeardNamesStore(
            Path.Combine(paths.Data, "heard-names.json"),
            loggerFactory.CreateLogger<HeardNamesStore>());

        heardNames.Load();

        // The same deal for what is *in* those ships, and lazy for the same reason — seeded with
        // the file, so the window's job is catching up on the gap since d47 last ran rather than
        // being the whole memory. That seeding is also what makes a sale stick across a restart:
        // ShipyardSell and ShipyardNew are replayed through the same fold and take the ship out
        // of the long memory.
        var recoveredLoadouts = new Lazy<IReadOnlyDictionary<string, ShipLoadouts>>(
            () => LoadoutBackfill.FromHistory(
                journalDirectory,
                loggerFactory.CreateLogger(nameof(LoadoutBackfill)),
                loadouts.All,
                loadouts.FoldedThrough));

        // The same deal for the names, and lazy for the same reason. The first run reads
        // everything — a name met last summer is one the Commander may say tomorrow, and depth is
        // the whole point of the catalogue — and every run after it walks only the gap.
        var recoveredNames = new Lazy<IReadOnlyDictionary<string, SpokenNames>>(() =>
        {
            var found = SpokenNameMiner.FromHistory(
                journalDirectory,
                loggerFactory.CreateLogger(nameof(SpokenNameMiner)),
                heardNames.All.ToDictionary(
                    entry => entry.Key, entry => entry.Value.Names, StringComparer.Ordinal),
                heardNames.FoldedThrough);

            // Written straight back, so the expensive first walk happens once rather than at
            // every start until something else prompts a save.
            heardNames.RememberNames(found, DateTimeOffset.Now);

            return found;
        });

        var gameState = new GameStateStore
        {
            Restore = sampling.For,

            // The fleet cannot always be refolded from the newest journal: StoredShips is written
            // only on docking at a shipyard, and a session may contain no such docking — which is
            // how a Commander with eleven ships was shown the one they were sitting in.
            RestoreFleet = fid => recoveredFleets.Value.TryGetValue(fid, out var fleet) ? fleet : null,

            // And Loadout describes one ship, so without this every parked ship's slots read as
            // never seen the moment the Commander swapped out of it.
            RestoreLoadouts = fid => recoveredLoadouts.Value.TryGetValue(fid, out var seen) ? seen : null,

            // And the names, so a failing lookup has something to match against on the very first
            // question of the session rather than after a few jumps.
            RestoreNames = fid => recoveredNames.Value.TryGetValue(fid, out var names) ? names : null,
        };

        // The settings follow whoever the journal says is flying (Phase 44). Subscribed
        // before the priming tick, because the adoption happens inside it and the host that does
        // everything else on this signal does not exist yet — and this one does not wait for it:
        // a projection is a pure reading of the id and discards nothing, so it follows every
        // reassignment, replayed or live, and the priming flag is for the subscribers that do.
        gameState.CommanderChanged += change =>
            settings.UseCommander(change.Current.FrontierId, change.Current.Name);

        // The two state files Elite rewrites in place. Same folder as the journal, different
        // shape: a log is appended to and these are replaced, which is entirely inside the
        // readers.
        var status = new GameStatusReader(journalDirectory, loggerFactory.CreateLogger<GameStatusReader>());
        var route = new NavRouteReader(journalDirectory, loggerFactory.CreateLogger<NavRouteReader>());

        // A third of the same kind (Phase 38): what Elite says each module in the ship the
        // Commander is flying actually draws, engineering included. The measured half of the power
        // gauge, and the only place those figures exist — the journal's ModuleInfo event is a
        // marker carrying none of them.
        var modulePower = new ModulePowerReader(
            journalDirectory, loggerFactory.CreateLogger<ModulePowerReader>());

        // A third file of the same kind, and the markets read out of it (Phase 36). The
        // book is loaded here so a plan made in the first minute already knows the stations this
        // Commander has stood in; the reader files a new one whenever the game rewrites the file.
        var marketBook = new D47.Core.Knowledge.MarketBook(
            Path.Combine(paths.Data, "markets.json"),
            loggerFactory.CreateLogger<D47.Core.Knowledge.MarketBook>());

        marketBook.Load();

        // And a fourth (Phase 37). Loaded here for the same reason the market book is:
        // the Routing tab is drawn before anybody plots anything, and a tab that is empty until
        // the first plot of the session forgets what the Commander asked for last night.
        var planBook = new D47.Core.Knowledge.RoutePlanBook(
            Path.Combine(paths.Data, "route-plans.json"),
            loggerFactory.CreateLogger<D47.Core.Knowledge.RoutePlanBook>());

        planBook.Load();

        // In memory rather than loaded, unlike the plan book above: a commodity price is the
        // thing here that ages fastest, so one restored from disk would look current because it
        // was saved rather than because it is true (Phase 49).
        var commodityBoard = new D47.Core.Knowledge.CommodityBoard();
        var sourcingBoard = new D47.Core.Knowledge.SourcingBoard();

        // On disk, unlike the two boards: a carrier figure is the Commander's own statement rather
        // than a price, and it is dated wherever it is used (Phase 50).
        var carrierManifest = new D47.Core.Knowledge.CarrierManifest(
            Path.Combine(paths.Data, "carrier.json"),
            loggerFactory.CreateLogger<D47.Core.Knowledge.CarrierManifest>());

        var markets = new D47.Core.Knowledge.MarketReader(
            journalDirectory,
            marketBook,
            loggerFactory.CreateLogger<D47.Core.Knowledge.MarketReader>());

        // After the status reader, because the spine stamps a surface position onto events that
        // carry none — organic sampling is the whole reason (Phase 18).
        var journal = new JournalSpine(journalDirectory, gameState, loggerFactory, () => status.Current);

        // The Commander's checklist and the proposals waiting on it, in two files beside the
        // executable (Phase 17). Two files rather than one because the trust boundary is
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
            () => gameState.Active,

            // The chosen filter outlives the session, which is what was asked for on 2026-08-23.
            // In view state rather than settings: it has no default worth documenting and nothing
            // should fail loudly because it could not be read, which is the same argument the
            // window's own position is kept here by.
            view => viewState.Save(viewState.Load() with
            {
                ChecklistFilter = view.Filter,
                ChecklistPartialGrades = view.IncludePartialGrades,
            }));

        checklists.Restore(
            new ChecklistView(
                viewState.Load().ChecklistFilter ?? ChecklistService.Everything,
                viewState.Load().ChecklistPartialGrades));

        // What d47 remembers about the Commander (Phase 31). One file, per Commander with
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

        // The standing directions the debrief pass drafts and the Commander adopts (#162). One
        // file, per Commander with the key inside the document, exactly like the memory store
        // above — and the path is named by DebriefWriteFence rather than spelled here, because the
        // fence refuses anything else and two spellings of one name is how they disagree.
        var directions = new StandingDirectionsStore(
            Path.Combine(paths.Data, DebriefWriteFence.FileName),
            loggerFactory.CreateLogger<StandingDirectionsStore>());

        directions.Poll();

        var debriefBook = new DebriefBook(directions, () => gameState.Active?.Identity.FrontierId);

        // <b>Habits was withdrawn, and its file goes with it.</b> The Commander asked for the
        // feature removed "and any data associated with it as well from existing data files as
        // part of the update" (#84), and this is that: the store is gone, so anything left in
        // data/habits.json is read by nothing and is the Commander's own flying recorded by a
        // feature that no longer exists.
        //
        // <b>No repair flag, and that is a decision rather than an omission.</b> The precedent
        // for a one-time cleanup is PersonaSettings.VoicesRepaired, and it exists because a
        // repair that re-decides something the Commander may have decided differently must run
        // once and never again. Deleting a file is not that: it is idempotent, it costs one
        // existence check per launch, and a settings property added to remember having done it
        // would outlive the thing it was tracking — the file is append-only, so that property
        // could never be removed either.
        //
        // Logged when it actually removes something, because deleting a Commander's data
        // silently is the wrong way round even when they asked for it.
        var retiredHabits = Path.Combine(paths.Data, "habits.json");

        try
        {
            if (File.Exists(retiredHabits))
            {
                File.Delete(retiredHabits);
                logger.LogInformation(
                    "Habits was withdrawn; {Path} has been deleted", retiredHabits);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // A file that cannot be deleted is a file nothing reads. Worth a line and not worth
            // failing a launch over.
            logger.LogWarning(ex, "Could not delete the retired {Path}", retiredHabits);
        }

        // The Commander's long arcs (Phase 34). The third store keyed on the Frontier id
        // and the second walk over the same corpus. An arc carries a definition of done, a start
        // date and a person's decision to set it aside, which is why it is its own store.
        var goals = new D47.Core.Goals.GoalStore(
            Path.Combine(paths.Data, "goals.json"),
            loggerFactory.CreateLogger<D47.Core.Goals.GoalStore>());

        goals.Poll();

        // The stories the Commander flies (Phase 47). The same store shape again, keyed
        // per Commander; the book folds the journal after each story's acceptance and is the one
        // thing the tick, the prompt and the tab all read. Built before the callouts because it
        // has one, and before the turn loop because the prompt carries the story.
        var adventureStore = new D47.Core.Adventures.AdventureStore(
            Path.Combine(paths.Data, "adventures.json"),
            loggerFactory.CreateLogger<D47.Core.Adventures.AdventureStore>());

        adventureStore.Poll();

        var adventureBook = new D47.Core.Adventures.AdventureBook(
            adventureStore, loggerFactory.CreateLogger<D47.Core.Adventures.AdventureBook>());

        // A hand edit, a Begin or an Abandon all arrive here; the book keeps what it can and asks
        // for a walk over the files when a stamp moved, which the tick below grants.
        adventureStore.Changed += adventureBook.Reconcile;

        var goalMiner = new D47.Core.Goals.GoalMiner(
            loggerFactory.CreateLogger<D47.Core.Goals.GoalMiner>());

        var backfilling = 0;

        // Off the UI thread, because the pass is seconds long over hundreds of megabytes, and
        // guarded because the button is a press and a Commander who sees nothing happen presses it
        // again. Core stays synchronous and clock-free; the App decides where the work runs.
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
        // reordering, exactly as bindsRef above is one: the goal book has to be built before the
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
            loaded, loggerFactory, checklists, lore, loreVisits, memoryBook, adventureBook, viewState);

        // Acting on the game without being asked (Phase 10, item 2). Each member is off
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

        // Captured rather than reached through the host, for the same reason `arrived` is: this
        // closure is built before the instance exists (#51).
        var journalLog = new D47.Core.Journal.JournalLog();

        tick.Add("journal", context =>
        {
            // The first tick is the replay of the backlog, and the switch signal has to know that
            // (Phase 44): a Commander change met during the replay is history, not a login.
            var events = journal.Poll(priming: context.IsFirst);

            arrived = events;

            // Kept for the Journal page to read (#51). Every event, including the priming replay:
            // a Commander opening the page wants the session they have been flying rather than
            // only what has happened since they opened it. Noise is marked there and filtered by
            // the page, never dropped here - a read filter could not be switched back on.
            journalLog.Add(events);
            status.Poll();
            route.Poll();

            // Elite rewrites this one on outfitting and on a module being switched off, so it is
            // read on the same terms as the other two: only when its write time moves.
            modulePower.Poll();

            // The commodity board the Commander is standing in front of, if they have opened one
            // (Phase 36). Given the position from the journal, because Market.json does
            // not carry one and a market that cannot be placed cannot be routed to.
            markets.Poll(gameState.Active?.Location.StarPos);

            // A sold ship's list goes with the ship (change-requests.md 27). Before the poll, so
            // the lines are gone before anything evaluates them against a ship that is not there.
            //
            // Not on the priming tick: that replays every sale the journal has ever recorded, and
            // the ones from months ago were dealt with when they happened. Silent then rather than
            // announced-and-silent, because the deletion itself must not be replayed either.
            if (!context.IsFirst)
            {
                foreach (var journalEvent in events)
                {
                    if (journalEvent.Kind == "ShipyardSell"
                        && journalEvent.Int("SellShipID") is { } sold)
                    {
                        // What it cleared goes onto the list's own news queue, so the checklist
                        // callout speaks it and the Commander can switch that off like anything
                        // else it says.
                        checklists.ShipSold(sold);
                    }
                }
            }

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

            // The same cadence and the same reasoning for the ships (#128). Which events can
            // change the picture is asked of ShipLoadouts rather than restated here, so there is
            // one list — and it is not only Loadout: Elite writes none after engineering.
            if (events.LastOrDefault(ShipLoadouts.MayChange) is { } changed)
            {
                // Stamped with that event's own time rather than with the clock, so the
                // watermark the next catch-up walks back to means what it says even when
                // this tick is replaying a backlog from yesterday.
                loadouts.Save(gameState.All, changed.Timestamp);
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

        // A story under way is caught up before the priming tick replays the current session
        // (Phase 47): the walk is bounded to the files since the earliest acceptance, so
        // with nothing under way it reads nothing, and a beat that fired while d47 was closed is
        // in the standing before the first live event arrives.
        adventureBook.CatchUp(D47.Core.Adventures.AdventureBook.FilesToWalk(journalDirectory, adventureBook.EarliestAcceptance()));

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

        // Clocks, timers and alarms (Phase 24). Alarms are a file because they are a
        // promise about a wall-clock moment that outlives the process; timers live only in the
        // Timekeeper, because a forty-minute countdown through a crash is a question nobody can
        // answer.
        var alarms = new AlarmStore(
            Path.Combine(paths.Data, "alarms.json"),
            loggerFactory.CreateLogger<AlarmStore>());

        alarms.Poll();

        var timekeeper = new Timekeeper(alarms);

        // The Commander's ship builds (Phase 26). Its own store, because the plan owns
        // what and the checklist owns when - nothing crosses between them unasked.
        var shipBuilds = new ShipBuildStore(
            Path.Combine(paths.Data, "ships.json"),
            loggerFactory.CreateLogger<ShipBuildStore>());

        shipBuilds.Poll();

        // The fleet joined to the builds. It reads the checklist service to propose promotions
        // and never writes to it directly: the plan owns what, the checklist owns when.
        var shipPlans = new ShipPlanService(shipBuilds, checklists, () => gameState.Active);

        // And the one thing that watches the two for drifting apart (Phase 38). It asks
        // through the same proposal boundary the promote button uses and writes nothing itself.
        var drift = new ShipDriftWatch(shipPlans, checklists);

        // Which core flies which ship (Phase 35). Its own file rather than a column on the
        // one above: a build is a plan, so hanging a preference off one would create a plan as a
        // side effect of stating the preference, and lose the preference when the plan was deleted.
        var shipCoreStore = new ShipCoreStore(
            Path.Combine(paths.Data, "ship-cores.json"),
            loggerFactory.CreateLogger<ShipCoreStore>());

        shipCoreStore.Poll();

        var shipCores = new ShipCoreService(shipCoreStore, () => gameState.Active);

        // And the same arrangement on foot (Phase 27). Its own file rather than a second
        // array in the ship one, because the game separates ship and on-foot hard and a Commander
        // hand-editing a suit should not be reading past twenty hardpoints to find it.
        var onFootBuilds = new D47.Core.Loadout.OnFootBuildStore(
            Path.Combine(paths.Data, "on-foot.json"),
            loggerFactory.CreateLogger<D47.Core.Loadout.OnFootBuildStore>());

        onFootBuilds.Poll();

        var onFootPlans = new D47.Core.Loadout.OnFootPlanService(
            onFootBuilds, checklists, () => gameState.Active);

        // The engineer solver (Phase 28). It reads both stores rather than being handed
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

        // A session, written up (Phase 33). The one thing d47 produces that the Commander
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
            //
            // The conversation model, deliberately, and not Turns.BackgroundModel (Phase 54).
            // This one is quoted at a price the Commander agrees to before anything
            // is written, so the cheap model is not d47's to substitute — and the check above is
            // exactly what would refuse the write if it were. "It uses FlavourTurn" is the
            // reasoning that would otherwise move this by accident.
            () => new D47.Core.Logbook.LogbookContext
            {
                Provider = self?.Turns.Provider,
                Model = self?.Turns.Model,
                PersonalityEnabled = settings.Current.Llm.PersonalityEnabled,
                Persona = self?.Personas.RenderBlock(settings.Current.Llm.PersonalityEnabled),

                // Both halves. A log is written once and about the whole evening, so the
                // sheet-always-story-sometimes rule for flavour lines has nothing to save here.
                AboutMe = CommanderStory.Compose(
                    settings.Current.Llm.CharacterSheet, settings.Current.Llm.AboutMe, withStory: true),
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
        // by name (Phase 12, "Custom Sound Cues"). Nothing here holds the resulting
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

        // Off unless D47_RECORD_AUDIO=1 (#164). Created here, with the audio, because both of
        // its seams are here: the render reference tap for what was played, and — further down —
        // the buffer handed to the transcriber for what was heard. Before the registry, because
        // when it is on it adds a settings row, and which rows exist has to be settled before
        // registration; descriptors are registered once and never mutated.
        var recording = Recording.AudioRecorder.Create(
            paths,
            () => DateTimeOffset.Now,
            loggerFactory.CreateLogger<Recording.AudioRecorder>());

        if (recording is not null)
        {
            recording.Watch(audio, audioSink.ReferenceTap);

            // What each sentence was rendered by — the provider, the voice and, for the local
            // voice, the phonemes. The tap knows what came out of the speakers and cannot know
            // any of that; the pipeline knows all of it and never sees the sound.
            voice.Synthesised = recording.Noted;
        }

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
            // fully usable in text (Phase 3, "Capabilities as state, not guard").
            logger.LogError(ex, "No audio output could be opened; D47 will be silent");
        }

        // Listening. The microphone runs continuously into the gate and the gate decides which
        // part of that stream was addressed to d47 — push-to-talk is a policy over the stream,
        // not a reason to start and stop the device (Phase 6).
        var models = new HttpModelStore(paths, loggerFactory.CreateLogger<HttpModelStore>());
        var transcriber = new WhisperTranscriber(loggerFactory.CreateLogger<WhisperTranscriber>());
        var gate = new ListenGate(WasapiMicrophone.SampleRate, loggerFactory.CreateLogger<ListenGate>());

        // Between the microphone and the gate, consuming the arbiter's render reference tap
        // rather than a loopback capture (Phase 13, architecture.md D7). The tap has
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

        // The stick's half of push-to-talk, and the two of them as one gate (Phase 53).
        // Both are Core types: reading a controller is already a Core contract where reading a
        // key is a P/Invoke, and the asymmetry buys a path that is driveable with nothing
        // plugged in.
        var pushToTalkButton = new D47.Core.Hotas.BoundButton();
        var sources = new D47.Core.Hotas.PushToTalkSources();

        // The only thing that presses a key in the game (architecture.md D4). Built here so
        // there is exactly one, because release_all has to be able to let go of everything and
        // a second injector would hold keys the first one knows nothing about.
        var eliteWindow = new EliteWindow(loggerFactory.CreateLogger<EliteWindow>());

        // With the status alongside the window (#242): running and in front are not the same as
        // in the game, and the injector is the one place the difference is enforced.
        var gameInput = new ScancodeInjector(
            eliteWindow, loggerFactory.CreateLogger<ScancodeInjector>(), () => status.Current);

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

        // What d47 last offered to put on the clipboard. In memory and nowhere else: an offer is a
        // moment in a conversation, so there is no file and nothing to restore.
        var clipboardOffer = new D47.Core.Conversation.ClipboardOffer();

        // The Commander's HOTAS switches, in the same shape and beside the same executable
        // (Phase 21). The reader is the one hardware component here that stays
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

        // d47's own trade planner (Phase 36). It reaches the same host the two above do
        // and shares their one setting and their one disclosure, because it is the same decision a
        // Commander is making.
        var tradePlanner = new D47.Knowledge.SpanshTradePlanService(
            loggerFactory.CreateLogger<D47.Knowledge.SpanshTradePlanService>(),
            marketBook);

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

                    // The local voice, and what fetching it would cost (Phase 59). Read at draw
                    // time rather than captured, so the row changes the moment the download ends.
                    LocalVoiceState = () => self?.LocalVoiceState() ?? "Not available.",
                    DownloadLocalVoice = () => self is null ? null : self.DownloadLocalVoice,

                    // Which of the eight builds is actually on disk, and the swap onto another
                    // (#139). Both late-bound for the reason the download above records: rows are
                    // built before `self` exists, so anything asked at build time answers null and
                    // stays null.
                    InstalledLocalVoiceBuild = () =>
                        self is null
                            ? null
                            : D47.Core.Speech.KokoroAssets.InstalledBuild(self.KokoroFolder())?.Id,
                    SwitchLocalVoiceBuild = build => self is null
                        ? null
                        : (progress, cancellationToken) =>
                            self.SwitchLocalVoiceBuild(build, progress, cancellationToken),
                    Beds = () => [.. (self?.Cues ?? cues).BedNames],
                    BedLabel = name => (self?.Cues ?? cues).IsCustom(name) ? $"{name} (yours)" : name,
                    OutputDevices = () => [.. WasapiAudioSink.Devices().Select(device => device.Id)],
                    DeviceLabel = id => WasapiAudioSink.Devices()
                        .FirstOrDefault(device => device.Id == id).Name ?? id,

                    // Late-bound like the headset surface below, and for the same reason: the
                    // list is fetched from the provider over the network after this point.
                    Voices = group => self?.VoiceIds(group) ?? [],
                    VoiceLabel = (group, id) => self?.VoiceLabelFor(group, id) ?? id,
                    VoiceGender = (group, id) => self?.VoiceGenderFor(group, id),
                    WhyNoVoices = group => self?.WhyNoVoices(group),
                    SpeechSpend = () => self?.SpeechSpend,

                    // Asked of the slot's own provider, not the ship's. A carrier left on free
                    // Edge must not have its audition greyed out because ElevenLabs — which is
                    // speaking for the companion and for nobody else — has no key yet.
                    HasKey = group => self is not { } host
                                      || host.HasKeyFor(TtsProviderCatalog.Selected(
                                          VoiceGroups.ProviderFor(settings.Current.Speech, group))),
                    Audition = (voiceId, role, token) => self is { } host
                        ? host.AuditionVoiceAsync(voiceId, role, token)
                        : Task.CompletedTask,

                    // Late-bound like the two above, because the check is a network call made by
                    // a host that does not exist yet at this point in composition.
                    VerifyKey = (provider, token) => self is { } host
                        ? host.VerifySpeechKeyAsync(provider, token)
                        : Task.FromResult(SecretCheck.Unreachable("D47 is still starting up.")),
                },
                new ShipsCapability.ShipsSurface
                {
                    // Read at draw time, so the row says what is stored now rather than what was
                    // stored when the surface was assembled — including straight after a rescan.
                    Remembered = () => self?.RememberedShips() ?? "Nothing is remembered yet.",

                    // The delegate answers a press rather than being one, for the reason
                    // SpeechCapability.DownloadLocalVoice records: rows are built before `self`
                    // exists, so a press asked for here would be null and stay null.
                    Rescan = () => self is null ? null : self.RescanLoadoutsAsync,
                },

                // How a misheard proper noun is recovered (#134). The catalogue is read at call
                // time, because a system the Commander jumped into a minute ago is one they may be
                // about to ask about — and the watch is the App's, since it is the state of one
                // exchange rather than of an installation.
                new SpokenNamesSurface(
                    () => gameState.Active?.Names ?? SpokenNames.Empty,
                    self?.Mishearings ?? new MishearingWatch(),
                    (heard, meant) => self?.LearnCorrection(heard, meant)),
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

                    // Read at draw time, so the row shows what has been learned rather than what
                    // had been when the surface was assembled (#134).
                    Corrections = () => self?.LearnedCorrections() ?? "Nothing yet.",
                    ForgetCorrections = () => self?.ForgetCorrections(),

                    // What the gate policy is actually doing, which is the question a Commander
                    // running hands free is asking when they ask this one (Phase 13).
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
                    Nudge = (nudge, steps) =>
                        self?.Vr?.Nudge(nudge, steps) ?? Core.Vr.VrNudgeOutcome.NoHeadset,
                },
                actionSurface = new ActionSurface
                {
                    Binds = () => binds.Current,

                    Status = () => status.Current,
                    Input = gameInput,
                    Enabled = () => settings.Current.Actions.Keyboard,

                    // Not awaited (#158). Core says the line and goes on pressing keys; the
                    // arbiter puts it in front of the verdict the same pipeline queues seconds
                    // later. Awaiting a synthesis here would delay the launch key by the length
                    // of the sentence, which is the opposite of what the acknowledgement is for.
                    Acknowledge = said => _ = self?.SayAsync(
                        new Announcement("action.acknowledge", said)),
                },
                () => AutonomousCapability.Describe(autonomous),
                new NavigationSurface
                {
                    Clipboard = new DesktopClipboard(loggerFactory.CreateLogger<DesktopClipboard>()),
                    Actions = actionSurface,
                    AutoPlotEnabled = () => settings.Current.Actions.AutoPlot,
                    WatchRoute = () => new Input.RoutePlotWatch(route, loggerFactory.CreateLogger<Input.RoutePlotWatch>()),
                    AwaitGalaxyMap = (open, token) => AwaitGalaxyMap(status, open, logger, token),
                },
                macros,
                personas,
                checklists,
                () => (self?.Cues ?? cues).DescribeDrops(),
                coverage is null ? null : () => coverage.Report().Summary,

                // Constructed unconditionally and gated by its setting rather than by whether it
                // exists: the row that turns it on has to work without a restart, and a service
                // built only when the setting was already true could not (Phase 4).
                galaxy,
                routePlanner,
                tradePlanner,
                communityGoals,
                () => DateTimeOffset.Now,

                // Late-bound like the surfaces above: the check is a real network call and the
                // host that makes it does not exist yet at this point in composition.
                (provider, token) => self is { } host
                    ? host.VerifyLanguageModelKeyAsync(provider, token)
                    : Task.FromResult(SecretCheck.Unreachable("D47 is still starting up.")),

                // Where the Commander is standing, which only Status.json knows — ScanOrganic
                // carries no position at all (Phase 18).
                () => status.Current,

                // The same window object the injector asks about before every key. One thing
                // knows how to find Elite, and now it also knows how to raise it — a second
                // finder would be a second answer to "is that Elite" and they would disagree.
                // On a worker because Raise verifies its landing with short waits (#107), and
                // this delegate is invoked on the UI thread, which the VR host also posts to.
                () => Task.Run(eliteWindow.Raise),

                new SwitchSurface
                {
                    Mappings = () => switches.Switches,
                    States = () => reconciler.States,
                    Unavailable = () => controllers.Unavailable,
                    Problems = () => switches.Problems,
                },
                lore,

                // The Commander's timers and alarms (Phase 24). The store polls itself
                // from the tick below, so an alarm edited by hand is live without a restart.
                timekeeper,

                // How to present an instant locally. Asked each time rather than captured, so a
                // Commander whose machine changes zone mid-session sees it without restarting.
                () => TimeZoneInfo.Local,
                shipPlans,

                // And the suit and weapon plans beside them (Phase 27). Two stores rather
                // than one, because the game separates ship and on-foot hard.
                onFootPlans,

                // Which engineer to go and get next, read across both of them (Phase 28).
                unlocks,

                // The endpoint half of web search, for the egress row. Asked each time because
                // the Commander can retarget `llm.endpoint` without restarting, and the row is
                // computed at render time so it has to be able to change underneath.
                () => self?.SearchReachesTheWeb ?? true,

                // What the endpoint said it serves (Phase 29). Empty until the handshake
                // answers, which is the state the model picker was designed for from Phase 4 —
                // the row accepts free text, so an empty list has always been a supported answer
                // rather than a broken one.
                () => self?.EndpointModelIds ?? [],

                // What d47 remembers about the Commander (Phase 31). Read by two
                // capabilities — its own, and the privacy section, which is where emptying it lives
                // rather than in a second place to look.
                memoryBook,

                // Turning a session into something worth keeping (Phase 33).
                logbook,

                // The campaigns that outlive a checklist (Phase 34).
                goalBook,

                // What the "read my journals" button does for the arcs' ages.
                () => BackfillGoals,

                // Which core flies which ship (Phase 35). Read by the persona capability
                // for its two rows, its two protected tools, and the one sentence the model is
                // allowed to know about a binding.
                shipCores,

                // And where a plan goes once it is made (Phase 37), so the spoken route
                // and the drawn one are one answer rather than two.
                planBook,

                // What d47 last offered to copy (asked for 2026-08-21). Composed here rather than
                // inside a capability because two of them write it and the router reads it.
                clipboardOffer,

                // The three waits the compound ship commands need (Phase 52). Core owns
                // the sequences and none of the waiting, which is what lets the whole boost loop
                // run in a test in microseconds against a scripted status stream.
                new ShipCommandSurface
                {
                    Enabled = command => ShipCommands.IsEnabled(settings.Current, command),

                    AwaitLeftPanel = (open, token) => AwaitStatus(
                        status,
                        current => (current.GuiFocus == Core.Actions.Launch.Panel) == open,
                        TimeSpan.FromSeconds(3),
                        open ? "left panel open" : "left panel closed",
                        logger,
                        token),

                    // Longer than the others on purpose: the pad lift and the mail slot take real
                    // seconds, and a launch reported as failed because d47 stopped watching too
                    // early is the same lie as one reported as succeeded.
                    AwaitUndocked = token => AwaitStatus(
                        status,
                        current => !current.Has(Core.Journal.StatusFlags.Docked),
                        TimeSpan.FromSeconds(30),
                        "undocked",
                        logger,
                        token),

                    NextStatus = token => NextStatus(status, token),
                },

                // Where a commodity answer is posted on its way out (Phase 49), so the
                // Routing tab draws what was just said rather than asking again.
                commodityBoard,

                // What the Commander says is aboard their carrier, and where a build's shopping
                // list is posted on its way out (Phase 50).
                carrierManifest,
                sourcingBoard,

                // What this build is, for the About area (#50). Core knows the version and the
                // data folder by itself; the commit string is an assembly attribute, a Start Menu
                // shortcut is a shell object and a browser is a process, so those arrive here.
                new AboutSurface
                {
                    Build = BuildInfo.Full,

                    // Asked each time the row is drawn, not captured: the answer arrives over the
                    // network after this page exists, and it changes again if the release is
                    // promoted while d47 is running (#92).
                    Channel = () => self?.Channel ?? D47.Core.Updates.ReleaseChannel.Unknown,

                    // Late-bound through the host like the speech surface's three, because the
                    // two that open a window need an owner and nothing here has one yet. The
                    // window is MainWindow's business; the row is the capability's.
                    ShowChangelog = () => self?.ShowChangelog?.Invoke(),
                    ShowChangelogOnline = () => System.Diagnostics.Process.Start(
                        new System.Diagnostics.ProcessStartInfo(Controls.ChangelogWindow.OnlineUrl)
                        {
                            UseShellExecute = true,
                        }),

                    // Neither of these needs a window, so both are answered here.
                    AddToStartMenu = () =>
                    {
                        if (Environment.ProcessPath is { } executable)
                        {
                            StartMenuShortcut.TryCreate(StartMenuShortcut.DefaultPath, executable, logger);
                        }
                    },

                    StartMenuWanted = () => !StartMenuShortcut.Exists() && Environment.ProcessPath is not null,
                    SetUpKeys = () => _ = self?.SetUpKeys?.Invoke(),

                    ShowCommunity = () => System.Diagnostics.Process.Start(
                        new System.Diagnostics.ProcessStartInfo(Controls.ChangelogWindow.CommunityUrl)
                        {
                            UseShellExecute = true,
                        }),

                    // Moved off the foot of the Settings tab and onto the row that names the
                    // folder (2026-09-01). Same shell call it always made.
                    OpenDataFolder = () => System.Diagnostics.Process.Start(
                        new System.Diagnostics.ProcessStartInfo(paths.Data) { UseShellExecute = true }),
                },

                // What the audio recorder has kept (#164), so the privacy capability can
                // carry the row that empties it. Null on every ordinary run, and there is then
                // no row at all.
                recording?.Log,

                // **Withdrawal, and it now reaches the store rather than only this machine**
                // (#167). The press asks the endpoint to delete every donation made under this
                // installation's identifier, then forgets the identifier here and writes a record
                // of what went — a route out that is the same one button the consent was, and that
                // needs no public thread to post in.
                //
                // A refused erasure KEEPS the identifier, because it is the only handle anybody
                // has on what was sent; the sentence it answers with says so and the press can
                // simply be made again. See DonationDispatch.ForgetAsync.
                async (_, cancel) =>
                {
                    var forgotten = await Donation.DonationDispatch
                        .For(paths, static () => DonationSettings.Address, loggerFactory)
                        .ForgetAsync(cancel);

                    return forgotten.Receipt is { } receipt
                        ? $"{forgotten.Outcome.Said} A record of it is in {receipt}."
                        : forgotten.Outcome.Said;
                },

                // What the debrief drafted and what the Commander took (#162). The capability
                // advertises no tool at all — the pass is offline and the model cannot invoke it —
                // so this reaches one disclosure row and the pane behind it, and nothing else.
                debriefBook));

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

        // The router's dynamic vocabulary: the Commander's own macro names, and — while one is
        // standing — the phrases that take up a clipboard offer. Both are dynamic for the same
        // reason: the argument is not knowable when the descriptor is registered.
        var router = new KeywordRouter(
            capabilities,
            () => MacroCapability.Phrases(macros)
                .Concat(clipboardOffer.Phrases())

                // And "set course for my carrier", which is an instruction rather than a topic
                // and has to out-match the "my carrier" keyword that was answering it with a
                // position report (change-requests.md 31). Dynamic for the same reason as the two
                // above: the destination is not knowable when the descriptor is registered.
                .Concat(CarrierCourse.Phrases(() => gameState.Active?.Carrier))

                // And "what is Conductive Polymers for", one phrase per material something
                // planned actually wants (change-requests.md 37). Dynamic for the same reason
                // again: which materials are wanted changes with the plans, and a command is not
                // part of a tool's schema so this cannot move a byte of the cached prefix.
                .Concat(GapCapability.Phrases(shipPlans, onFootPlans, () => gameState.Active)));

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
            StandingSaid = checklists.SaidStanding,

            LiveGameState = () => Join(
                Situation.Describe(gameState.Active),
                Join(
                    ActionCapabilities.Describe(actionSurface),
                    Join(
                        MacroCapability.Live(macros),
                        Join(
                            ChecklistCapability.Live(checklists),

                            Join(
                                // The story under way, told from inside (Phase 47).
                                // Below the breakpoint beside the game state, so a beat firing
                                // costs the cached prefix nothing; and the turn and the ending
                                // are withheld until their beats, which is the block's own rule.
                                D47.Core.Adventures.AdventureContext.Describe(
                                    adventureBook.Standings(gameState.Active?.Identity.FrontierId),
                                    id => PersonaCatalog.Knows(id) ? PersonaCatalog.Resolve(id).Name : null,
                                    SystemWallClock.Instance.UtcNow),

                            Join(
                                // Both dates, already worked out, below the cache breakpoint
                                // where a per-turn value costs nothing (Phase 24). The
                                // model is never asked to add 1286 to anything.
                                UtilitiesCapability.Live(
                                    timekeeper, SystemWallClock.Instance.UtcNow, TimeZoneInfo.Local),

                                // Why d47 cannot look something up, when it cannot. Null when it
                                // can, so the Commander who has search on pays nothing for a
                                // sentence about it. Reached through `self` because the provider
                                // belongs to the turn loop this initialiser is still building.
                                ConversationCapability.LiveSearch(
                                    settings.Current.Llm.WebSearch,
                                    self?.SearchReachesTheWeb ?? true))))))),
        };

        // The catalogue a generated story may draw its stops from (Phase 47). A different
        // host from the galaxy search, behind its own row and its own disclosure.
        var notablePlaces = new D47.Knowledge.GecNotablePlacesService(
            loggerFactory.CreateLogger<D47.Knowledge.GecNotablePlacesService>());

        // What writes an adventure, once, for the Commander to agree to. Not a tool: it runs from
        // the panel with the flavour turn's bookkeeping, and the model it asks never sees an id.
        //
        // The conversation model, deliberately, and not turns.BackgroundModel (Phase 54).
        // Every reason the floor exists points the other way here: the Commander pressed a button
        // and is waiting, the output must name real systems exactly, it is validated and re-asked
        // on refusal, and it already asks Medium effort against token budgets an order of
        // magnitude above every other caller. Note for whoever greps: this reads the property
        // through a lowercase local, so a search for Turns.Model does not find it — which is
        // precisely the accident this comment exists to prevent.
        var adventureGenerator = new D47.Core.Adventures.AdventureGenerator(
            () => turns.Provider,
            () => turns.Model,
            () => personas.RenderBlock(settings.Current.Llm.PersonalityEnabled),
            () => settings.Current.Llm.PersonalityEnabled ? personas.Current.Id : null,
            () => CommanderStory.Compose(settings.Current.Llm.CharacterSheet, settings.Current.Llm.AboutMe, withStory: true),
            () => gameState.Active,
            () => settings.Current.Knowledge.GalaxySearch ? galaxy : null,
            () => settings.Current.Knowledge.NotablePlaces ? notablePlaces : null,
            spend,
            PriceTable.Default,
            loggerFactory.CreateLogger<D47.Core.Adventures.AdventureGenerator>());

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
            pushToTalkButton,
            sources,
            binds,
            gameInput,
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

        // The buffer the tick closure has been filling since before this instance existed (#51).
        host.JournalLog = journalLog;

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
        // than of any one capability (Phase 13). Set straight onto the view model from
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
        // restarted (Phase 4, "Apply every setting without a restart").
        settings.Changed += host.OnSettingsChanged;

        // The one instance, shared: the injector's foreground rule and the overlay's visibility
        // rule are the same question about the same window, and two readers would be two caches
        // of one handle.
        host.Elite = eliteWindow;

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

        // The Commander switch (Phase 44). Subscribed after the priming tick, which is
        // fine and also not the point: the signal carries whether it happened during priming, and
        // the handler honours that rather than this ordering. The settings followed the same
        // signal earlier, before the host existed.
        host.Drift = drift;
        host.Continuity = callouts.Callouts.OfType<ContinuityCallout>().Single();
        gameState.CommanderChanged += host.OnCommanderChanged;

        host.SwitchEditing = new Settings.SwitchEditing(
            switches,
            controllers,
            reconciler,
            () => DateTimeOffset.Now,
            Path.Combine(paths.Data, "switch-capture.txt"),
            () => host.PanelDestinations);
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

        // Same pairing, same reason (#162): an adoption is stamped with a real instant and Core
        // reads no clock of its own.
        host.Debrief = (debriefBook, () => DateTimeOffset.Now);

        // The session opens here, over what the file says right now. Everything the Commander
        // adopts from this point on is written to the file and reaches nothing until the next one.
        host.BeginDirections();

        // A callout switched off within seconds of it speaking (#162). A signal, collected; the
        // pass turns it into a question at the end of the session, and nothing adapts to it.
        callouts.Silenced += host.NoteSilenced;
        host.Logbook = logbook;
        host.Goals = (goalBook, BackfillGoals);
        host.Adventures = (adventureBook, adventureGenerator);
        host.Galaxy = galaxy;
        host.JournalDirectory = journalDirectory;
        host._loadouts = loadouts;
        host._heardNames = heardNames;
        host.Plans = planBook;
        host.Controllers = controllers;
        host.Commodities = commodityBoard;
        host.Sourcing = sourcingBoard;
        host.Carrier = carrierManifest;

        host.ReservedPhrases = PhrasesAlreadyTaken(capabilities);

        host.CoverageRecorder = coverage;
        coverage?.Follow(capabilities, settings);

        host.AudioRecorder = recording;

        // Captured audio becomes words on the thread pool, never on the audio thread that
        // produced it. Whisper on a CPU takes hundreds of milliseconds for a short clip; doing
        // that inline would stall capture and drop the next utterance.
        gate.Captured += host.TranscribeAsync;

        // The route reader lives in the tick closure, so the host reaches it through this
        // rather than owning it — proper-noun biasing wants the systems the Commander is about
        // to arrive in, and those are only in the route file.
        host._route = () => route.Current;
        host._modulePower = () => modulePower.Current;
        host._heardAt = heardAt;

        // Push-to-talk, sampled here rather than hooked. This is the whole reason the tick runs
        // at 10 Hz rather than 4: the period is the worst-case delay before a key-down is seen,
        // and the gate's pre-roll is what absorbs it. See PushToTalkKey for why polling one
        // virtual-key code beats the three alternatives.
        tick.Add("push-to-talk", context =>
        {
            pushToTalk.Poll();

            // And the stick, on the same tick (Phase 53). The polling rate is not a risk
            // here for the reason it is not one above: a button read on this tick is no less
            // responsive than the key it replaces.
            //
            // One reading for both buttons (#221). Polling the controllers twice on one tick
            // would be two different answers to one question, and the second binding to arrive
            // must not be able to change what the first one saw.
            var buttons = controllers.Poll();

            pushToTalkButton.Poll(buttons);
            host._cancelButton.Poll(buttons);

            // And then, and only then, whether the stick it is bound to turned up (#45). Asked
            // here because this is the only place that has looked; the button answers once per
            // binding, so this is a warning rather than ten of them a second.
            host.WarnIfTheStickIsMissing();

            // Whether the device is actually delivering audio, which only it knows and which is
            // half of what the panel's microphone indicator says. Sampled here rather than
            // raised, because a device disappearing does not always announce itself.
            gate.Capturing = microphone.IsCapturing;

            // Where the hands-free gate opens and closes. The detector runs on the audio thread
            // and records what it decided; this is the thread that acts on it, so a real-time
            // callback never plays a cue or hands an utterance to a transcriber (Phase 13,
            // architecture.md §4).
            gate.Poll(context.Now);
        });

        // Two sources, one gate (Phase 53). Either opens the microphone and the last
        // release closes it, so letting go of the key while the button is still held does not cut
        // the Commander off mid-sentence.
        pushToTalk.Pressed += sources.KeyPressed;
        pushToTalk.Released += sources.KeyReleased;
        pushToTalkButton.Pressed += sources.ButtonPressed;
        pushToTalkButton.Released += sources.ButtonReleased;

        sources.Pressed += () => gate.KeyDown(DateTimeOffset.Now);
        sources.Released += () => gate.KeyUp();

        // Cancel, on press and once (#221). Push-to-talk needs both edges because it is held;
        // this is a press, so the release edge is deliberately not subscribed — a Commander
        // holding the cancel button down has cancelled once, not twice.
        host._cancelButton.Pressed += () => host.CancelNow();

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
        // thread pool, for the same reason the autonomous drain does (Phase 21).
        tick.Add("switches", context =>
        {
            switches.Poll();

            // One snapshot for both fields, so the pages and the one showing were true together.
            var panel = host._panel;

            reconciler.Poll(
                new SwitchTick
                {
                    Now = context.Now,
                    Readings = controllers.Poll(),
                    Status = status.Current,
                    Binds = bindsRef!(),

                    // Gated by key injection as well as by its own row. A Commander who has not
                    // allowed d47 to press keys at all has not allowed it for switches either.
                    // A position that names a page of the panel is not behind either row —
                    // it presses nothing (Phase 46).
                    Enabled = settings.Current.Actions.Keyboard && settings.Current.Actions.Switches,
                    Destinations = panel.Destinations,
                    Showing = panel.Showing,
                },
                switches.Switches);

            // The annunciator, on whichever surfaces are up. Recomputed here rather than bound,
            // because it is a projection of two things that move independently — where the
            // switch is, and what the game says.
            host.ShowSwitches(SwitchCapability.Annunciator(reconciler.States));
        });

        // The first thing d47 does that nothing external triggers (Phase 24). Every other
        // subscriber here is reacting to something that arrived; this one asks whether time has
        // passed. The now comes from the tick's own context, which is what keeps the clock rule
        // and lets the replay harness run a day of alarms in a second.
        tick.Add("reminders", context =>
        {
            alarms.Poll();
            host.SoundReminders(timekeeper.Poll(context.Now));
        });

        // Ship builds are hand-editable, and buying a hull the Commander had planned for offers
        // to adopt the plan onto it rather than making them re-point it (Phase 26). Its
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
            // ShipyardNew written after it does (Phase 27).
            foreach (var adopted in shipPlans.Observe(arrived).Concat(onFootPlans.Observe(arrived)))
            {
                host.Panel.Append($"{adopted}{Environment.NewLine}");
                _ = host.Voice.AnnounceAsync(adopted);
            }

            // Boarding a ship whose build carries engineering the checklist has not got is the
            // moment to say so, once (Phase 38). Spoken, because the Commander is in the
            // cockpit at that moment and not looking at a window; the Ships tab carries the same
            // question as a banner for as long as it goes unanswered.
            if (drift.Observe(arrived) is { Length: > 0 } asked)
            {
                host.Panel.Append($"{asked}{Environment.NewLine}");
                _ = host.Voice.AnnounceAsync(asked);
            }
        });

        // A core per ship (Phase 35). The store is read first so a hand edit is live, then
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

        // What d47 remembers, on the tick like every other store (Phase 31). Four things,
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

        // The arcs, on the tick because goals.json is hand-editable, so a goal typed
        // into it is live without a restart. Nothing is walked here — that is a button.
        tick.Add("goals", _ => goals.Poll());

        // The adventures file is hand-editable and polled like the others; and when a stamp has
        // moved - Begin, Begin again, a hand edit - the walk the book asked for happens here, on
        // the tick, so the live fold cannot interleave with it. Bounded by date, so it is the
        // current file and the one before it in the ordinary case.
        tick.Add("adventures", _ =>
        {
            adventureStore.Poll();

            if (adventureBook.NeedsCatchUp)
            {
                adventureBook.CatchUp(D47.Core.Adventures.AdventureBook.FilesToWalk(journalDirectory, adventureBook.EarliestAcceptance()));
            }
        });

        tick.Add("callout-drain", _ => host.SpeakPendingCallouts());

        // Ambience follows the situation Status.json states, sampled on the tick rather than
        // hooked to a journal event: docked, supercruise and on foot are conditions rather than
        // things that happen, and the file is already being read here every tick.
        tick.Add("ambience", _ => host.FollowSituation(status.Current));

        // And the folder those tracks came from, which the Commander can add to while d47 is
        // running (Phase 12, "Pick up dropped-in audio without a restart"). The same
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
        //
        // Being last is also what makes <see cref="Compose"/> possible: not starting it is the
        // entire difference between composing the app and running it, so the seam costs one
        // branch rather than a restructuring of everything above (#79).
        if (startTicking)
        {
            host._ticking = new TickDriver(tick, loggerFactory.CreateLogger<TickDriver>()).Start();
        }

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
        D47.Core.Adventures.AdventureBook adventures,
        ViewStateStore viewState)
    {
        var engine = new CalloutEngine(loggers.CreateLogger<CalloutEngine>())
            .Add(new DangerCallout())

            // Above everything except danger itself (Phase 15). It is the only warning
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

            // Phase 40, and the same capacity for the opposite purpose: the milestone callout
            // needs it to work out how far along a stock is, and this one needs it to say nothing
            // about a stock that is finished.
            .Add(new EmissionCallout { Capacity = MaterialGrades.CapacityOf })

            // Phase 41. Its two thresholds are pushed by ApplyCalloutSettings with the rest,
            // which is what makes moving a slider take effect on the next docking rather than on
            // the next restart.
            .Add(new LimpetCallout())

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
            // is worse than silence (Phase 15).
            // The full explanation once per local day, across sessions and cores alike (asked
            // for 2026-08-31) — so the day lives in view-state.json, read-modify-write like the
            // introductions, and losing the file costs one repeated sentence.
            .Add(new RivalTerritoryCallout
            {
                LastExplainedDay = () => viewState.Load().RivalExplainedOn,
                RememberExplainedDay = day => viewState.Save(viewState.Load() with { RivalExplainedOn = day }),
            })

            // Phase 23. Below the warnings and above the ambient line: it is news, but it is news
            // about a place that will still be there in a minute.
            .Add(new LoreCallout(lore, loreVisits))

            // Phase 31, and the lowest thing here that is not the ambient line: it fires once, at
            // the start of a session, and it is about what was true before the Commander sat down.
            // Everything above it is about now. Since Phase 42 what it mostly says is the top of
            // the checklist, in the Commander's own order.
            .Add(new ContinuityCallout())

            // Getting into a game and leaving one (change-requests.md 29), which is a different
            // event from the line above: that one greets when d47 starts, and this one when the
            // game does. Below it, because on a launch where both fire the Commander has sat down
            // once and should hear one greeting rather than two.
            .Add(new SessionCallout())

            // A beat of the Commander's story, when they reach it (Phase 47). Also the one
            // path the live journal reaches the adventure book by.
            .Add(new D47.Core.Adventures.AdventureCallout(adventures))
            .Add(new AmbientCallout())

            // Invented chatter (#244): the marker only — the app composes the exchange, and
            // with no model the marker composes to nothing. See SpeakPendingCallouts.
            .Add(new NpcChatterCallout())
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

        // The clock the engine does not have, so it can tell a callout switched off seconds after
        // it spoke from one switched off an hour later (#162). It reads a transition rather than a
        // state, which is what makes this safe to call on every settings change as it always was.
        var now = DateTimeOffset.Now;

        engine.Enabled = callouts.Enabled;
        engine.SetEnabled("danger", callouts.Danger, now);
        engine.SetEnabled("fuel", callouts.Fuel, now);
        engine.SetEnabled("route", callouts.Route, now);
        engine.SetEnabled("long-jump", callouts.LongJump, now);
        engine.SetEnabled("arrival", callouts.Arrival, now);
        engine.SetEnabled("materials", callouts.Materials, now);
        engine.SetEnabled("emissions", callouts.Emissions, now);
        engine.SetEnabled("limpets", callouts.Limpets, now);
        engine.SetEnabled("announced-attack", callouts.AnnouncedAttack, now);
        engine.SetEnabled("rival-territory", callouts.RivalTerritory, now);
        engine.SetEnabled("sampling", callouts.Sampling, now);
        engine.SetEnabled("prospector", callouts.Prospector, now);
        engine.SetEnabled("core-asteroid", callouts.CoreAsteroid, now);
        engine.SetEnabled("checklist", callouts.Checklist, now);
        engine.SetEnabled("ambient", callouts.Ambient, now);
        engine.SetEnabled("continuity", callouts.Continuity, now);
        engine.SetEnabled("adventure", callouts.Adventure, now);

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

                case LimpetCallout limpets:
                    limpets.Floor = () => callouts.LimpetCargoFloor;
                    limpets.Percent = () => callouts.LimpetPercent;
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
                    //
                    // With no model it is silent too (#245), but that gate is not here: this
                    // method is static and the provider is runtime state, so the one place that
                    // holds "chatter is model-written or it is nothing" is the drain in
                    // SpeakPendingCallouts, which drops an ambient line the model did not write.
                    ambient.Enabled = () => settings.Callouts.Ambient && settings.Llm.PersonalityEnabled;
                    break;

                case NpcChatterCallout chatter:
                    chatter.Interval = TimeSpan.FromSeconds(callouts.NpcChatterSeconds);
                    chatter.Longest = TimeSpan.FromSeconds(callouts.NpcChatterMaxSeconds);

                    // The ambient pair of gates (#244): theatre is personality by any reading,
                    // and the no-model half lives at the compose step for the reason above.
                    chatter.Enabled = () => settings.Callouts.NpcChatter && settings.Llm.PersonalityEnabled;
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
            // factory says so (Phase 29).
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

        // Resolved once, here, rather than at each of the eight call sites (Phase 54).
        // Null means the Commander has not split the two, so the background calls take the
        // conversation model and nothing about them changes.
        Turns.BackgroundModel = current.Llm.BackgroundModel ?? current.Llm.Model;

        // What the Commander will pay for, kept apart from what the router thinks they asked
        // for. Both null is the router's own answer, unchanged.
        Turns.EffortFloor = current.Llm.EffortFloor;
        Turns.EffortCeiling = current.Llm.EffortCeiling;

        // What the transcriber gets wrong, put right before anything reads the sentence (#134).
        // Assigned here like every other per-session property, so a correction learned this
        // afternoon is applied to the next thing said without a restart.
        Turns.Heard = HeardAsMeant;

        // Position 4, both halves: the turn path is cached above the breakpoint, so the story's
        // thirteen hundred tokens are paid once per edit rather than per turn (Phase 43).
        Turns.AboutMe = CommanderStory.Compose(current.Llm.CharacterSheet, current.Llm.AboutMe, withStory: true);

        // Position 3 of the assembled prompt, and null when personality is off. Null rather
        // than a neutral block on purpose: "off" is position 3 being absent, and the guardrails
        // at position 2 are untouched either way, which is the property that whole arrangement
        // exists to guarantee (architecture.md §6).
        ApplyPersonaBlock();

        LlmAvailability.SetProviderConfigured(provider is not null, reason);
    }

    /// <summary>
    /// Puts the recall block into the prompt, and <b>only when it has actually changed</b> (Phase 31
    /// , "Recall arrives above the cache breakpoint").
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
    /// Says what an expiry took, when it took something worth saying (Phase 31, "Forgetting
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
    /// again on any change, so the two paths cannot drift (Phase 4, "Apply every
    /// setting without a restart").
    /// </summary>
    /// <summary>
    /// What the language-model endpoint said it serves (Phase 29). Empty until a
    /// handshake has answered, and empty again for a provider with no endpoint to ask.
    /// </summary>
    internal IReadOnlyList<string> EndpointModelIds => _endpointModels;

    /// <summary>
    /// Asks the endpoint what it serves, if it is the kind of thing that can be asked and has not
    /// been asked already (Phase 29).
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

                // Only if the address has not moved again while this was in recording. A slow
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

    /// <summary>The ids the voice picker offers for one slot.</summary>
    internal IReadOnlyList<string> VoiceIds(VoiceGroup group = VoiceGroup.Aboard) =>
        [.. VoicesFor(group).Voices.Select(voice => voice.Id)];

    /// <summary>
    /// How the picker labels one — "Ava — Female, en-US" rather than the raw id.
    /// </summary>
    /// <summary>
    /// The voice's name on its own — "George" — or null when the catalogue does not know it, which
    /// is the case for an id the Commander typed themselves and for every voice before the list
    /// has been fetched. Null rather than the id, so the caller decides what to show
    /// (remediation.md 10, item 9).
    /// <para>
    /// <b>Every slot's list, not the ship's alone</b>
    /// (<a href="https://github.com/dseelinger/d47/issues/149">#149</a>). This asked
    /// <see cref="AboardVoices"/> and nothing else, so a voice handed to an NPC out of
    /// ElevenLabs' pool was looked up in Kokoro's list and written into the log as a bare id. The
    /// rule is <see cref="VoiceGroups.NameFor"/>, in Core where it can be asserted; this supplies
    /// the one thing only the host knows, which is what each slot's provider actually answered.
    /// </para>
    /// <para>
    /// Through <see cref="VoicesFor"/> per slot rather than by walking
    /// <see cref="_voicesByProvider"/>, which is written to from the voice-loading path while
    /// this is read from the speaking one.
    /// </para>
    /// </summary>
    internal string? VoiceNameFor(string id) => VoiceGroups.NameFor(VoicesFor, id);

    /// <summary>
    /// How a voice is shown to the Commander, wherever one is shown — the row, its tooltip and
    /// the picker all read this. The rule itself is <see cref="VoiceCatalogue.LabelFor"/>; this
    /// supplies the two things it needs that only the host knows.
    /// </summary>
    internal string VoiceLabelFor(string id) => VoiceLabelFor(VoiceGroup.Aboard, id);

    /// <summary>
    /// What the provider tags one voice's gender as, or null where it says nothing
    /// (<a href="https://github.com/dseelinger/d47/issues/146">#146</a>).
    /// <para>
    /// Read out of the same catalogue <see cref="VoiceLabelFor(VoiceGroup, string)"/> and
    /// <c>VoicePool.Feminine</c> read, which is what makes the picker's gender filter and the
    /// casting rule agree about every voice by construction rather than by inspection.
    /// </para>
    /// </summary>
    internal string? VoiceGenderFor(VoiceGroup group, string id) =>
        VoicesFor(group).Voices
            .FirstOrDefault(voice => string.Equals(voice.Id, id, StringComparison.OrdinalIgnoreCase))
            ?.Gender;

    /// <inheritdoc cref="VoiceLabelFor(string)"/>
    internal string VoiceLabelFor(VoiceGroup group, string id) =>
        VoicesFor(group).LabelFor(
            id,
            TtsProviderCatalog.Selected(VoiceGroups.ProviderFor(Settings.Current.Speech, group)));

    /// <summary>
    /// Why the voice picker has nothing in it, when it has nothing in it (Phase 19;
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
    internal string? WhyNoVoices(VoiceGroup group = VoiceGroup.Aboard)
    {
        var provider = TtsProviderCatalog.Selected(VoiceGroups.ProviderFor(Settings.Current.Speech, group));

        return provider.Speaks ? VoicesOf(provider.Id).WhyEmpty(provider.Name) : null;
    }

    /// <summary>
    /// One voice per core, chosen once and written to settings (Phase 11, #33).
    /// <para>
    /// Guarded by a flag rather than by "are there pairings yet", so a Commander who cleared
    /// every pairing by hand does not have them silently regenerated on the next launch. Runs
    /// after the voice list arrives and never blocks anything: picking a character must not wait
    /// on a model being reachable.
    /// </para>
    /// </summary>
    /// <summary>
    /// A voice for the core aboard, chosen now if it has none (Phase 11, #33).
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

        if (AboardVoices.Count == 0 || Settings.Current.Persona.Voices.ContainsKey(persona.Id))
        {
            return;
        }

        try
        {
            var voice = await VoicePairing.ChooseOneAsync(
                persona,
                AboardVoices.Voices,
                Settings.Current.Persona.Voices.Values,
                Turns.Provider,
                Turns.BackgroundModel,
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
            VoicePairing.WithoutMiscastVoices(before, AboardVoices.Voices, _logger)).ConfigureAwait(false);

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
            AboardVoices.Voices,
            Turns.Provider,
            Turns.BackgroundModel,
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
            VoicePairing.WithNamedDefaultsRestored(before, AboardVoices.Voices, provider, _logger)).ConfigureAwait(false);

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
        if (AboardVoices.Count > 0)
        {
            await RepairMiscastVoicesAsync().ConfigureAwait(false);
            await RestoreNamedVoicesAsync().ConfigureAwait(false);
        }

        if (Settings.Current.Persona.VoicesPaired || AboardVoices.Count == 0)
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
                AboardVoices.Voices,
                Settings.Current.Persona.Voices,
                Turns.Provider,
                Turns.BackgroundModel,
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

    /// <summary>
    /// One provider's client, or null for a provider that does not speak. The only place a
    /// concrete synthesiser is named, which is what keeps the seam a seam (architecture.md §2).
    /// </summary>
    /// <summary>
    /// Where the local voice keeps its model, beside the speech-to-text models and for the same
    /// reason: it is a large downloaded thing that is not the Commander's data, and deleting it
    /// costs a download rather than anything they wrote.
    /// </summary>
    internal string KokoroFolder() => Path.Combine(Paths.Data, "models", "kokoro");

    /// <summary>Whether the local voice is here, and what it would cost if not.</summary>
    private string LocalVoiceState() =>
        D47.Core.Speech.KokoroAssets.IsInstalled(KokoroFolder())
            ? "Installed. Nothing D47 speaks through this provider leaves this machine."
            : $"Not downloaded. About {D47.Core.Speech.KokoroAssets.TotalMegabytes:0} MB, fetched "
              + "once from huggingface.co.";

    /// <summary>Whether a download is already running, atomic because the button is a press.</summary>
    private int _fetchingVoice;

    /// <summary>
    /// What the local voice says the moment it can say anything. A download that finished is a
    /// claim; a voice coming out of the speakers is the proof of it, and it is the only part of
    /// this a Commander can check without reading a log.
    /// </summary>
    private const string LocalVoiceProof =
        "Local voice installed. This is D47, speaking from your own machine. Nothing I say through "
        + "this provider leaves it.";

    /// <summary>
    /// Fetches the local voice, off the UI thread, saying how far it has got.
    /// <para>
    /// <b>Three of the four things this does were added after the first Commander pressed it</b>
    /// (2026-08-28). It downloaded 350 MB correctly and reported none of it: no bar, a button that
    /// stayed pressable, a row that went on saying <em>not downloaded</em>, and — the one that
    /// actually cost something — <b>a voice list that was never asked for again</b>. The picker had
    /// been told <em>not installed</em> at startup and nothing revisits that answer, so the
    /// Commander had a local voice on disk and no way to choose one.
    /// </para>
    /// <para>
    /// Guarded the same way every long press here is, and now doubly: the view shuts the button
    /// while this runs, and this refuses a second run regardless, because the view is not the only
    /// thing that could call it.
    /// </para>
    /// </summary>
    /// <summary>
    /// What is stored about the fleet, as a sentence for the settings row
    /// (<a href="https://github.com/dseelinger/d47/issues/128">#128</a>).
    /// <para>
    /// <b>The age of the oldest entry is the number worth showing.</b> A count on its own says
    /// nothing about whether it is right; "the oldest was last seen fourteen months ago" is what
    /// tells a Commander whether the answer they are looking at is worth acting on.
    /// </para>
    /// </summary>
    /// <summary>
    /// The Frontier id of whoever is flying, or empty before anybody has been identified
    /// (<a href="https://github.com/dseelinger/d47/issues/134">#134</a>). Everything the listening
    /// store holds is keyed on it, because two Commanders share one journal folder and neither
    /// may be handed the other's corrections.
    /// </summary>
    private string Flying => GameState.Active?.Identity.FrontierId ?? string.Empty;

    /// <summary>What d47 has learned this transcriber gets wrong, for the settings row (#134).</summary>
    internal string LearnedCorrections() =>
        Flying.Length == 0 || _heardNames is not { } store
            ? "Nothing yet. D47 learns one of these only when you correct a name it misheard."
            : store.AliasesFor(Flying).Summarise();

    /// <summary>Drops every learned correction for whoever is flying (#134).</summary>
    internal void ForgetCorrections()
    {
        if (Flying is { Length: > 0 } fid)
        {
            _heardNames?.ForgetCorrections(fid, DateTimeOffset.Now);
        }
    }

    /// <summary>
    /// The transcript pre-pass (<a href="https://github.com/dseelinger/d47/issues/134">#134</a>):
    /// what this Commander's transcriber reliably gets wrong, put right before anything reads the
    /// sentence.
    /// </summary>
    internal string HeardAsMeant(string spoken) =>
        Flying.Length == 0 || _heardNames is not { } store
            ? spoken
            : store.AliasesFor(Flying).Apply(spoken);

    /// <summary>
    /// Records a correction the Commander steered d47 to
    /// (<a href="https://github.com/dseelinger/d47/issues/134">#134</a>).
    /// <para>
    /// <b>Whether it is kept is the store's decision.</b> Everything that must not be aliased — a
    /// word that already names a place this Commander has met, a phrase the keyword router answers
    /// to, anything that is not a single word — is refused there, in one place, rather than by
    /// each caller remembering to ask.
    /// </para>
    /// </summary>
    internal void LearnCorrection(string heard, string meant)
    {
        if (Flying is { Length: > 0 } fid)
        {
            _heardNames?.Learn(
                fid,
                heard,
                meant,
                DateTimeOffset.Now,
                word => ReservedPhrases.Any(phrase =>
                    phrase.Contains(word, StringComparison.OrdinalIgnoreCase)));
        }
    }

    internal string RememberedShips()
    {
        if (GameState.Active?.Loadouts is not { IsKnown: true } ships)
        {
            return "No ship has been seen inside yet. Board one and D47 will remember it.";
        }

        var oldest = ships.Ships.Values.Min(ship => ship.SeenAt);
        var count = ships.Ships.Count;

        return count == 1
            ? $"One ship, last seen {TelemetryDelta.Spoken(DateTimeOffset.Now - oldest)} ago."
            : $"{count.ToString("N0", System.Globalization.CultureInfo.InvariantCulture)} ships, the "
              + $"oldest last seen {TelemetryDelta.Spoken(DateTimeOffset.Now - oldest)} ago.";
    }

    /// <summary>
    /// Reads every journal on disk again and rebuilds what each ship was last seen holding
    /// (<a href="https://github.com/dseelinger/d47/issues/128">#128</a>). <b>The Commander's own
    /// repair</b>, for when what is drawn does not look right.
    /// <para>
    /// <b>Off the UI thread, because it is disk-bound and long.</b> The whole of a 943-journal,
    /// 382 MB history reads and folds in about three seconds; the bar is what makes that legible
    /// rather than a freeze.
    /// </para>
    /// <para>
    /// <b>A rescan that read no journals changes nothing</b>, and that guard is the difference
    /// between a repair and a wipe: a folder that has moved, or a Commander pointed at the wrong
    /// one, answers exactly as a fleet that has genuinely been sold would.
    /// </para>
    /// </summary>
    internal async Task<string?> RescanLoadoutsAsync(
        IProgress<double> progress,
        CancellationToken cancellationToken)
    {
        if (JournalDirectory is not { Length: > 0 } directory || _loadouts is not { } store)
        {
            return "There is no journal folder to read.";
        }

        if (Interlocked.Exchange(ref _rescanning, 1) == 1)
        {
            return "A rescan is already running.";
        }

        try
        {
            var found = await Task.Run(
                () => LoadoutBackfill.Rescan(
                    directory,
                    _loggerFactory.CreateLogger(nameof(LoadoutBackfill)),
                    progress),
                cancellationToken).ConfigureAwait(false);

            if (found.Files == 0)
            {
                _logger.LogWarning("A rescan read no journals from {Directory}; nothing was changed", directory);

                return $"I could not read any journals in {directory}, so nothing was changed. "
                       + "Check the journal folder and try again.";
            }

            GameState.ReplaceLoadouts(found.ByCommander);

            // Dated off the newest thing the walk saw rather than off the clock, so the next
            // start's catch-up begins where this left off. Nothing seen at all is still a real
            // rescan — it means the journals hold no ships — and the stamp says so.
            var through = found.ByCommander.Values
                .SelectMany(ships => ships.Ships.Values)
                .Select(ship => ship.SeenAt)
                .DefaultIfEmpty(DateTimeOffset.Now)
                .Max();

            store.Save(GameState.All, through);

            _logger.LogInformation(
                "A rescan read {Files} journals and remembered {Ships} ship(s)", found.Files, found.Ships);

            return found.Ships == 0
                ? $"Read {found.Files} journals and found no ships in them. Nothing is remembered now."
                : $"Read {found.Files} journals. {found.Ships} ship(s) remembered.";
        }
        catch (OperationCanceledException)
        {
            return "The rescan was stopped. Nothing was changed.";
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _logger.LogWarning(ex, "A rescan of {Directory} could not be completed", directory);
            return "I could not read the journals through. Nothing was changed.";
        }
        finally
        {
            _ = Interlocked.Exchange(ref _rescanning, 0);
        }
    }

    private async Task<string?> DownloadLocalVoice(
        IProgress<double> progress,
        CancellationToken cancellationToken)
    {
        if (Interlocked.Exchange(ref _fetchingVoice, 1) == 1)
        {
            return "A download is already running.";
        }

        try
        {
            using var installer = new KokoroInstaller(
                KokoroFolder(), _loggerFactory.CreateLogger<KokoroInstaller>());

            var reported = new Progress<KokoroProgress>(step => progress.Report(step.Fraction));

            var result = await Task.Run(
                () => installer.InstallAsync(reported, cancellationToken),
                cancellationToken).ConfigureAwait(false);

            _logger.LogInformation("The local voice download ended as {Outcome}", result.Outcome);

            if (result.Outcome is not (KokoroInstall.Installed or KokoroInstall.AlreadyPresent))
            {
                return result.Detail ?? "The local voice could not be downloaded.";
            }

            // The picker's list, asked for again now that there is something to list. Without
            // this the files are on disk and the voice row is still empty, which is what shipped.
            await RefreshLocalVoicesAsync().ConfigureAwait(false);

            return await SpeakLocalVoiceProofAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is IOException or HttpRequestException)
        {
            _logger.LogWarning(ex, "The local voice could not be downloaded");
            return $"The local voice could not be downloaded: {ex.Message}";
        }
        finally
        {
            Interlocked.Exchange(ref _fetchingVoice, 0);
        }
    }

    /// <summary>
    /// Swaps the local voice onto a different one of Kokoro's eight builds (#139).
    /// <para>
    /// <b>The same guard as the download beside it, and it is the same guard rather than a second
    /// one</b>: both write <c>model.onnx</c>, so two of them running at once is two writers on one
    /// file. A switch while a first install is running is refused, and the other way round too.
    /// </para>
    /// <para>
    /// <b>The provider is rebuilt afterwards, not at the next restart.</b> A loaded
    /// <c>InferenceSession</c> holds the file it opened, so a Commander who changed the build and
    /// went on talking would keep hearing the old one — which is the "apply every setting without
    /// a restart" rule (Phase 4) and also the only observable difference this row makes.
    /// </para>
    /// </summary>
    internal async Task<string?> SwitchLocalVoiceBuild(
        string buildId,
        IProgress<double> progress,
        CancellationToken cancellationToken)
    {
        if (Interlocked.Exchange(ref _fetchingVoice, 1) == 1)
        {
            return "A download is already running.";
        }

        try
        {
            using var installer = new KokoroInstaller(
                KokoroFolder(), _loggerFactory.CreateLogger<KokoroInstaller>());

            var reported = new Progress<KokoroProgress>(step => progress.Report(step.Fraction));

            // Let go of the file before overwriting it. Windows will not replace a model an open
            // session is holding, and the failure it gives — "used by another process" — reads as
            // a download problem rather than as the one thing that has to happen first.
            DropLocalVoiceClient();

            var result = await Task.Run(
                () => installer.SwitchAsync(buildId, reported, cancellationToken),
                cancellationToken).ConfigureAwait(false);

            _logger.LogInformation(
                "The local voice build change to {Build} ended as {Outcome}", buildId, result.Outcome);

            if (result.Outcome is not (KokoroInstall.Installed or KokoroInstall.AlreadyPresent))
            {
                return result.Detail ?? $"The {buildId} build could not be downloaded.";
            }

            await RefreshLocalVoicesAsync().ConfigureAwait(false);

            return await SpeakLocalVoiceProofAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is IOException or HttpRequestException)
        {
            _logger.LogWarning(ex, "The local voice build could not be changed");
            return $"The {buildId} build could not be downloaded: {ex.Message}";
        }
        finally
        {
            Interlocked.Exchange(ref _fetchingVoice, 0);
        }
    }

    /// <summary>
    /// Closes and forgets the Kokoro client, so nothing is holding <c>model.onnx</c> open.
    /// <para>
    /// The next thing that needs the local voice builds a fresh client, which is what makes this
    /// safe to do mid-session: the provider is created on demand from the folder rather than kept
    /// as the only copy of anything.
    /// </para>
    /// </summary>
    private void DropLocalVoiceClient()
    {
        if (_clients.Remove(TtsProviderCatalog.KokoroId, out var client)
            && client is IDisposable disposable)
        {
            disposable.Dispose();
        }
    }

    /// <summary>
    /// Asks the local voice what it offers, now that it has something to offer.
    /// <para>
    /// Only where a client exists, which is where it matters: a client is built the moment the
    /// provider is selected, and a provider that is not selected asks for its list when it is.
    /// </para>
    /// </summary>
    private async Task RefreshLocalVoicesAsync()
    {
        if (_clients.GetValueOrDefault(TtsProviderCatalog.KokoroId) is { } client)
        {
            await LoadVoicesAsync(client).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Speaks one line in the voice that was just downloaded, through the one arbiter like
    /// everything else that makes a sound (architecture.md D7).
    /// <para>
    /// Through a client of its own where the provider is not the selected one, because proving the
    /// download worked must not require choosing it first — and that client is disposed here rather
    /// than kept, since the selected provider's client is the one the rest of d47 speaks through.
    /// </para>
    /// </summary>
    private async Task<string?> SpeakLocalVoiceProofAsync(CancellationToken cancellationToken)
    {
        var shared = _clients.GetValueOrDefault(TtsProviderCatalog.KokoroId);
        var own = shared is null
            ? new KokoroTtsProvider(
                KokoroFolder(),
                _loggerFactory.CreateLogger<KokoroTtsProvider>(),
                Paths.PronunciationsFile)
            : null;

        try
        {
            var clip = await (shared ?? own!).SynthesizeAsync(
                LocalVoiceProof,
                new VoiceSelection(
                    SpeechCapability.ShipVoiceFor(Settings.Current, Personas.Current.Id),
                    SpeechCapability.RateFor(Settings.Current, TtsProviderCatalog.KokoroId)),
                cancellationToken).ConfigureAwait(false);

            Audio.Enqueue(new AudioRequest
            {
                Channel = AudioChannel.Speech,
                Clip = clip,
                Group = AuditionGroup,
                Caption = clip.Name,
            });

            return null;
        }
        catch (Exception ex) when (ex is IOException or InvalidOperationException)
        {
            // The files are there and something else is wrong, which is worth saying on the row:
            // the state above it now reads "installed" and the Commander heard nothing.
            _logger.LogWarning(ex, "The local voice was downloaded but could not speak");
            return $"Downloaded, but the voice could not speak: {ex.Message}";
        }
        finally
        {
            own?.Dispose();
        }
    }

    private ITtsProvider? BuildSpeechClient(string providerId) => providerId switch
    {
        SpeechCapability.EdgeId =>
            new EdgeNeuralTtsProvider(_loggerFactory.CreateLogger<EdgeNeuralTtsProvider>()),

        SpeechCapability.ElevenLabsId => new ElevenLabsTtsProvider(
            () => Secrets.TryGet(ElevenLabsTtsProvider.KeySecretName, out var key) ? key : null,
            _loggerFactory.CreateLogger<ElevenLabsTtsProvider>()),

        TtsProviderCatalog.OpenAiId => new OpenAiTtsProvider(
            () => Secrets.TryGet(OpenAiTtsProvider.KeySecretName, out var key) ? key : null,
            _loggerFactory.CreateLogger<OpenAiTtsProvider>(),

            // How the core aboard should be performed, asked per sentence because a Commander
            // switches core while d47 is running (#49).
            //
            // ONE CLIENT SERVES ALL SIX SLOTS (Phase 57), so this reaches every slot on
            // OpenAI rather than the ship's AI alone - the Commander's call, 2026-08-26, taking
            // the small honest cost over a second client. A second client would be correct in
            // every configuration and would put two concurrency gates against one account, which
            // is the property ElevenLabsTtsProvider.MaxConcurrent's reasoning depends on.
            //
            // In practice it reaches the core and little else: Phase 57's own default puts every
            // slot carrying another player's words on Edge, and a Commander who moves one to
            // OpenAI has chosen that.
            direction: () => VoiceDirection.For(
                Settings.Current.Llm.PersonalityEnabled ? Personas.Current : null)),

        TtsProviderCatalog.CartesiaId => new CartesiaTtsProvider(
            () => Secrets.TryGet(CartesiaTtsProvider.KeySecretName, out var key) ? key : null,
            _loggerFactory.CreateLogger<CartesiaTtsProvider>()),

        // The local voice (Phase 59). No key, no endpoint and no network once the model is
        // on disk - the folder is the whole of its configuration. It is built whether or not
        // the files are there: a provider that cannot find them lists no voices and says why,
        // which is the state a Commander needs to see in order to fetch them.
        TtsProviderCatalog.KokoroId => new KokoroTtsProvider(
            KokoroFolder(),
            _loggerFactory.CreateLogger<KokoroTtsProvider>(),
            Paths.PronunciationsFile),

        _ => null,
    };

    private async Task LoadVoicesAsync(ITtsProvider provider)
    {
        try
        {
            var listed = await provider.ListVoicesAsync().ConfigureAwait(false);
            _voicesByProvider[provider.Id] = listed;

            _logger.LogInformation(
                "{Provider}'s voice list has {Count} voices ({Listing})",
                provider.Id,
                listed.Count,
                listed.Listing);

            // The pool a re-voiced sender is drawn from, on this provider's cast. Which voices
            // those are is decided in Core, where the distinction between a locale and an accent
            // label can be asserted — this used to read ElevenLabs' accent as a locale and
            // discard 472 of a 473-voice account, leaving every NPC in a system sharing one voice.
            var cast = Casting.Of(provider.Id);
            cast.Pool = VoicePool.From(listed.Voices);

            // And which of them are a woman's, so a sender whose name reads as one is given one.
            cast.Feminine = VoicePool.Feminine(listed.Voices);

            // Both numbers, because one of them alone is what hid that: "1 voice available" is
            // alarming beside "473 offered" and unremarkable on its own.
            _logger.LogInformation(
                "{Count} of {Offered} voices are available for re-voiced senders, {Feminine} of them women's",
                cast.Pool.Count,
                listed.Count,
                cast.Feminine.Count);

            // Pairing a voice to each core needs the list, so it starts once the list arrives
            // rather than at startup. Background and best-effort: picking a character must never
            // wait on it (Phase 11, #33).
            //
            // Only for the ship's own provider. A pairing is the companion's voice, and pairing
            // eleven cores against the list belonging to whoever speaks for local chat would
            // write ids the companion's provider has never heard of.
            if (string.Equals(
                    provider.Id,
                    VoiceGroups.ProviderFor(Settings.Current.Speech, VoiceGroup.Aboard),
                    StringComparison.OrdinalIgnoreCase))
            {
                _ = PairPersonaVoicesAsync();
            }
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
    /// situation and the library (Phase 12).
    /// </summary>
    private readonly Ambience _ambience = new();

    /// <summary>How often the drop-in folder is looked at. See <see cref="RescanAudio"/>.</summary>
    private static readonly TimeSpan AudioScanEvery = TimeSpan.FromSeconds(2);

    private TimeSpan _sinceAudioScan = TimeSpan.Zero;

    /// <summary>
    /// How often the memory store is checked for entries past their expiry (Phase 31).
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
    /// What kind of switch the settings write about to arrive is (Phase 35). A field
    /// rather than a parameter because the write goes through the settings row like every other
    /// caller — that is what keeps the ship-AI-name rule and the protected rule in one place —
    /// and the row cannot carry a reason. Set immediately before the write and cleared after it,
    /// which is safe because the change is raised synchronously from inside it.
    /// </summary>
    private PersonaSwitch _personaCause = PersonaSwitch.Selected;

    /// <summary>
    /// Puts the core the Commander bound to this ship aboard (Phase 35, "Switching ships
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
    /// What a new Commander logging in actually changes (Phase 44). The Commander's ruling,
    /// 2026-08-21: <i>"The old transcript goes away, a new one is created. New ship, new AI."</i>
    /// <para>
    /// <b>Adoption discards nothing.</b> Nobody to somebody is d47 learning who has been flying
    /// since before it started, and what was said before that belongs to no Commander rather
    /// than retroactively to this one — the same rule that keeps <c>MemoryStore.NoCommander</c> a
    /// real key nothing migrates out of. <b>A replayed switch discards nothing either</b>: the
    /// signal says whether it happened during priming, and this honours it rather than relying on
    /// any subscriber's own gate.
    /// </para>
    /// <para>
    /// The settings are not re-read here. They followed the signal before this host existed (see
    /// the subscription beside the <see cref="GameStateStore"/>), and they announce each Commander
    /// row that moved under its own key, so About Me reaches the prompt through the same fan-out
    /// an edit would use. That rebuild is the prompt cache dying, and it is meant to: About Me
    /// sits above the breakpoint, so a change of Commander invalidates the cached prefix by
    /// construction. Once per switch, and a switch is rare — recorded as a known cost and not
    /// optimised around.
    /// </para>
    /// </summary>
    public void OnCommanderChanged(CommanderSwitch change)
    {
        if (change.Priming || change.IsAdoption)
        {
            _logger.LogInformation(
                "Commander {Name} ({Fid}) is flying — {How}, nothing discarded",
                change.Current.Name,
                change.Current.FrontierId,
                change.Priming ? "met in the backlog" : "adopted");

            return;
        }

        _logger.LogInformation(
            "Commander {Previous} logged out and {Current} logged in: new transcript, core re-resolved, greeting due",
            change.Previous!.Name,
            change.Current.Name);

        // Every core's transcript, and the loop pointed at the fresh one — the handover is by
        // reference, so a discard the loop was not told about would leave it appending to the
        // old Commander's conversation.
        Personas.ForgetTranscripts();
        Turns.UseTranscript(Personas.Transcript);

        // The ship they are in is adopted afresh on the next tick, silently, as the ship d47 found
        // them in — which re-resolves the core through the store now keyed per Commander. Without
        // this, two Commanders both in ship 7 read as no change.
        ShipCores.Reset();
        Drift?.Reset();

        // Once per session rather than once per run: the greeting is the new ship AI's first words.
        Continuity?.Rearm();

        // The debrief too, and in this order for a reason: the session that just ended was the
        // previous Commander's, so it is filed under their id — named rather than asked for,
        // because the game state is already pointed at whoever logged in (#162). Then the record
        // is emptied, so nothing they said is attributed to the new Commander.
        RunDebrief(change.Previous.FrontierId);

        // And the directions are re-latched, because they are one person's and the person
        // changed. The other boundary is startup; there is no third.
        BeginDirections();

        // On the panel, so the transcript says why the next line starts from nothing.
        Noted?.Invoke($"Commander {change.Current.Name} logged in");
    }

    /// <summary>
    /// Writes down when the core aboard stopped being aboard, so a gap reaction can be about a
    /// month rather than about an evening (Phase 35). A read-modify-write, like every
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

        // A quiet arrival is a real switch with nothing to say (Phase 35). The voice, the
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
            // elsewhere (Phase 12).
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
                        Turns.BackgroundModel,
                        Personas.RenderBlock(Settings.Current.Llm.PersonalityEnabled),

                        // The sheet and not the story: a core reacting to lost time is speaking
                        // to somebody it knows by name, and this is a one-off with no index to
                        // choose a story call by.
                        CommanderStory.Compose(
                            Settings.Current.Llm.CharacterSheet, Settings.Current.Llm.AboutMe, withStory: false),
                        "You have just been switched back on after "
                        + $"{TelemetryDelta.Spoken(gap.Away)} of not running. Say one or two sentences "
                        + "reacting to the missing time, exactly as your character would. Do not greet "
                        + "the Commander formally and do not offer a list of what you can do.",
                        gap.TelemetryDelta,
                        Spend,
                        PriceTable.Default,
                        _logger).ConfigureAwait(false);

                    // Checked rather than merely non-null: a rewording brief answered with the
                    // model talking about itself is not a line this core said (GitHub issue 46).
                    // The instruction above already asks it not to offer a list of what it can
                    // do, which is the same failure anticipated and not guarded.
                    if (FlavourBriefs.MayBeSpoken(generated))
                    {
                        line = generated!;
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
                        Turns.BackgroundModel,
                        Personas.RenderBlock(brief.NeedsPersona),
                        StoryFor(brief),
                        brief.Instruction,
                        gameState: null,
                        Spend,
                        PriceTable.Default,
                        _logger).ConfigureAwait(false);

                    // Checked rather than merely non-null: a rewording brief answered with the
                    // model talking about itself is not a line this core said (GitHub issue 46).
                    // The instruction above already asks it not to offer a list of what it can
                    // do, which is the same failure anticipated and not guarded.
                    if (FlavourBriefs.MayBeSpoken(generated))
                    {
                        line = generated!;
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
    /// The ambience layer, following what the Commander is doing (Phase 12).
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

        // What to build, what to release, which slots moved and whose list to ask for again are
        // decided in Core, where a test can reach them; what to build and how to fetch it stay
        // here, where the loggers and the secret store are. Both of the faults an afternoon's
        // hand-testing found lived on the far side of that line (Phase 19), and Phase 57
        // widened the answer from one bool to six slots without moving the line.
        var plan = SpeechWiring.Plan(
            _speechWiring,
            VoiceGroups.Selected(speech),
            id => HasKeyFor(TtsProviderCatalog.Selected(id)));

        _speechWiring = plan.Next;

        // Released first, so a slot moving from ElevenLabs to Edge and another moving the other
        // way do not hold two of each at once.
        foreach (var released in plan.Dispose)
        {
            if (_clients.Remove(released, out var client))
            {
                // Through the interface, so this stays correct for a provider that needs no
                // disposal. ITtsProvider deliberately does not require IDisposable: it is a
                // text-to-audio seam, and whether an implementation holds an HTTP handle is its
                // own business.
                (client as IDisposable)?.Dispose();
            }

            _voicesByProvider.Remove(released);
            Casting.Forget(released);
        }

        foreach (var wanted in plan.Build)
        {
            if (BuildSpeechClient(wanted) is { } built)
            {
                _clients[wanted] = built;
            }
        }

        // One decorator per slot over the shared client, which is what lets the spend row answer
        // "which slot is costing money" without a second connection to the provider — the thing
        // ElevenLabsTtsProvider.MaxConcurrent's reasoning depends on (Phase 57).
        foreach (var moved in plan.Rewire)
        {
            _slots[moved] = _clients.GetValueOrDefault(VoiceGroups.ProviderFor(speech, moved)) is { } client
                ? new MeteredTtsProvider(client, SpeechSpend, moved)
                : null;
        }

        // Fetched in the background. The picker asks synchronously and the list comes over the
        // network, so it is cached rather than requested on open — and not awaited, because a
        // settings change must not wait on a provider being reachable.
        foreach (var asking in plan.RefetchVoices)
        {
            if (_clients.GetValueOrDefault(asking) is { } client)
            {
                _ = LoadVoicesAsync(client);
            }
        }

        Voice.Tts = Speaker(VoiceGroup.Aboard);
        Voice.SpeakerFor = Speaker;

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
        var aboard = VoiceGroups.ProviderFor(speech, VoiceGroup.Aboard);
        var carrier = VoiceGroups.ProviderFor(speech, VoiceGroup.Carrier);

        foreach (var providerId in VoiceGroups.ProvidersInUse(speech))
        {
            var cast = Casting.Of(providerId);

            // A rate is a property of the synthesiser rather than of the Commander's patience,
            // once two of them can be speaking at once: ElevenLabs *rejects* a speed outside its
            // range rather than clamping it, so a figure chosen for Edge and applied here would
            // not be a fast carrier but a silent one (Phase 57).
            cast.Rate = SpeechCapability.RateFor(Settings.Current, providerId);

            // The ship's voice belongs to the ship's provider and to nobody else's. Where a comms
            // slot happens to share that provider, this does double duty: VoiceCast.ForSender
            // steps past whatever is already aboard, so a pirate cannot be handed the
            // companion's voice.
            cast.DefaultVoice = string.Equals(providerId, aboard, StringComparison.OrdinalIgnoreCase)
                ? SpeechCapability.ShipVoiceFor(Settings.Current, Personas.Current.Id)
                : null;

            // Likewise the carrier's two, which are ids issued by whoever speaks for the carrier.
            var speaksForTheCarrier = string.Equals(providerId, carrier, StringComparison.OrdinalIgnoreCase);

            cast.Assign(VoiceRole.CarrierCaptain, speaksForTheCarrier ? speech.CarrierCaptainVoice : null);
            cast.Assign(VoiceRole.TowerControl, speaksForTheCarrier ? speech.TowerVoice : null);
        }

        Voice.Voice = Casting.Of(aboard).For(VoiceRole.ShipAi);
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
    /// <summary>
    /// Where the no-speech probe's refusal starts (#196). The measured populations
    /// (spike/NoSpeechProbe, unprompted tiny.en): real speech 0.017–0.26 against room tone
    /// 0.946–0.958 — 0.6 splits them with a wide margin on both sides, and errs toward keeping
    /// a real "Stop." over eating one.
    /// </summary>
    private const double NoSpeechFloor = 0.6;

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
                // they fly go in with every utterance (Phase 6).
                var nouns = ProperNouns.From(GameState.Active, _route?.Invoke());

                // The unprompted second opinion, beside the prompted pass rather than after it
                // (#196): tiny.en answers in ~350 ms while the main model is still working, so
                // the gate below costs nothing in latency.
                var probe = _transcriber.NoSpeechAsync(utterance);

                var transcription = await _transcriber
                    .TranscribeAsync(utterance, nouns)
                    .ConfigureAwait(false);

                // The exact buffer the transcriber was given, beside what it came back with
                // (#164). Here rather than anywhere upstream because this is the point the
                // ReadFully trap proved is the one that matters: the capture path invented 99%
                // silence for weeks and every layer above this reported it as a quiet Commander.
                //
                // Only the gated utterance is written down. What runs through the microphone
                // between utterances never reaches this line, so a recording is never a hot mic.
                AudioRecorder?.Heard(utterance, transcription);

                // **A word hallucinated from silence is refused here** (#196). The name-hint
                // prompt is what turns silence into a plausible word and what destroys the
                // prompted pass's own no-speech signal, so the ruling comes from the unprompted
                // probe. Refusal is the transcript becoming empty, so every downstream path —
                // the entry loop's heard-nothing case, the wake word, the turn — handles it as
                // the established nothing-heard shape rather than a new one. The flight
                // recorder above has already kept what Whisper actually said, which is how a
                // wrong refusal would be caught.
                if (transcription.Text.Length > 0
                    && await probe.ConfigureAwait(false) is { } noSpeech
                    && noSpeech >= NoSpeechFloor)
                {
                    _logger.LogInformation(
                        "Refused as no-speech: the unprompted probe read {Probability:0.###} against \"{Text}\"",
                        noSpeech,
                        transcription.Text);

                    transcription = transcription with { Text = string.Empty };
                }

                // A panel is asking for a value and this is the answer to it (Phase 25,
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
                    // Written down, because nothing after this point will. An utterance that
                    // answers a chooser never reaches the turn that would have recorded it, so
                    // before this it was a thing the Commander said that the page had no trace
                    // of at all (change-requests.md 31).
                    HeardAside(transcription.Text, "answering the question");

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

                // The wake word, applied to the words rather than to the audio (Phase 13).
                // Outside wake-word mode the policy holds no phrases and admits
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
                    // has its own indicator now (Phase 13).
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

                // What was heard, where it is not what gets asked. The wake policy strips its
                // own name off the front, so the turn is recorded under words the Commander did
                // not quite say — and the page that exists to show the working is the place that
                // difference belongs. Silent when the two agree, which is the ordinary case and
                // would otherwise print every utterance twice.
                if (!string.Equals(decision.Text, transcription.Text, StringComparison.Ordinal))
                {
                    HeardAside(transcription.Text, "heard");
                }

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
    /// The surfaces that may be waiting on a spoken value (Phase 25, "Say it, or type
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
    /// The navigators a spoken "show me the checklist" moves (Phase 25).
    /// <para>
    /// <b>Every surface, not one.</b> A phrase has no surface attached to it — the Commander said
    /// it once, into the room — and moving only the window would leave a Commander in a headset
    /// saying it twice with nothing happening either time. So both go, and the two surfaces agree
    /// about where they are for as long as neither is driven separately, which is the only reading
    /// of the phrase that is never surprising.
    /// </para>
    /// <para>
    /// For the transcript the agreement is unconditional (Phase 45): which of Conversation,
    /// Technical and the log file is being read is one choice across every surface, held by
    /// <see cref="_transcript"/>. Tabs and trails stay per surface, so this list is still walked.
    /// </para>
    /// </summary>
    private readonly List<Core.Interface.PanelNavigator> _navigators = [];

    /// <summary>
    /// The one mechanism that carries a transcript root from the surface that moved it to the rest
    /// (Phase 45). <see cref="Navigate"/> and <see cref="Show"/> initiate moves on every
    /// surface and never propagate them: the first navigator they reach raises <c>Changed</c>, the
    /// mirror moves the others, and the loop is declined by each of those as already there.
    /// </summary>
    private readonly Core.Interface.TranscriptMirror _transcript = new();

    /// <summary>
    /// How to reach each navigator from a thread that does not own it, in the order they were
    /// routed. A switch flip arrives on the tick thread and a navigator belongs to the thread
    /// that draws it (Phase 46).
    /// </summary>
    private readonly List<(Core.Interface.PanelNavigator Nav, Action<Action> Post)> _surfaces = [];

    /// <summary>
    /// The panel as the switch path sees it: every page any surface registered, and the one
    /// showing. Replaced whole on the thread that owns the navigators and read from the tick,
    /// never mutated — a navigator's dictionaries are not for reading off their thread.
    /// </summary>
    private volatile PanelSnapshot _panel = new([], null);

    private sealed record PanelSnapshot(IReadOnlyList<Core.Interface.PanelDestination> Destinations, string? Showing);

    /// <summary>
    /// Adds a surface's navigator to the ones a spoken phrase moves, with how to reach it from
    /// another thread. <paramref name="post"/> is called from the tick; it should carry a
    /// dispatcher the surface captured on its own thread rather than read one at call time.
    /// </summary>
    /// <param name="leads">
    /// Whether this surface's <em>tab</em> carries to the others (change-requests.md 34). True for
    /// the window and nothing else: the window leads, the mini panel follows and may be moved
    /// independently, and a follower's tab never drags the window's — which is what
    /// Phase 48 requires and why this is a flag rather than a mirror.
    /// </param>
    public void RouteNavigation(
        Core.Interface.PanelNavigator nav, Action<Action> post, bool leads = false)
    {
        _navigators.Add(nav);
        _surfaces.Add((nav, post));

        // Into the mirror before the snapshot is hooked, so a surface that arrives behind the
        // other is brought level and the first snapshot already reads two surfaces agreeing. The
        // mirror moves navigators in the handler rather than through `post`: every surface is on
        // the window's thread (architecture.md D1), and a posted move could cross another in
        // flight (Phase 45).
        if (leads)
        {
            _transcript.Lead(nav);
        }
        else
        {
            _transcript.Add(nav);
        }

        // Taken here and retaken every time a surface moves, on the thread that moved it. Every
        // tab is furnished before a surface routes here, so the roots are complete at the first
        // snapshot and never change after it.
        nav.Changed += (_, _) => SnapshotPanel();
        SnapshotPanel();
    }

    private void SnapshotPanel()
    {
        var destinations = _navigators
            .SelectMany(nav => nav.Destinations)
            .DistinctBy(page => page.Root.Key)
            .ToList();

        // What the panel is showing is what every surface agrees it is showing. Two surfaces in
        // different places is "cannot say" rather than either one's answer: a flip then asks
        // each of them, and each declines for itself if it is already there.
        var showing = _navigators.Select(nav => nav.Root.Key).Distinct().ToList();

        _panel = new PanelSnapshot(destinations, showing.Count == 1 ? showing[0] : null);
    }

    /// <summary>Every page any surface offers, for the switch editor's list (Phase 46).</summary>
    public IReadOnlyList<Core.Interface.PanelDestination> PanelDestinations => _panel.Destinations;

    /// <summary>
    /// Puts every surface on this page, each on its own thread — what a switch position that
    /// names a destination does (Phase 46). The same every-surface rule as
    /// <see cref="Navigate"/>, for the same reason: a switch has no surface attached to it
    /// either. A surface that does not offer the page declines it, which is
    /// <c>PanelView.Tab</c> declining a tab nobody furnished, inherited rather than re-stated.
    /// <para>
    /// For a transcript root the second surface is moved by <see cref="_transcript"/> before this
    /// loop reaches it, and declines the loop as already there; the loop still has to reach it,
    /// because arriving on the Transcript <em>tab</em> is per surface (Phase 45).
    /// </para>
    /// </summary>
    private void Show(string rootKey)
    {
        foreach (var (nav, post) in _surfaces)
        {
            post(() => nav.Show(rootKey));
        }
    }

    /// <summary>
    /// How each surface moves the page it is showing (#34). Registered rather than reached
    /// through <see cref="_navigators"/>, because a scroll position is the view's and a navigator
    /// has never held one.
    /// </summary>
    private readonly List<Func<Core.Interface.PanelScrollStep, bool>> _scrollers = [];

    /// <summary>
    /// Adds a surface to the ones a spoken scroll moves (#34).
    /// <para>
    /// No poster beside it, unlike <see cref="RouteNavigation"/>. A scroll arrives from the turn,
    /// which runs on the window's thread, and every surface is built on that thread
    /// (architecture.md D1) — where <see cref="Show"/> needs one is the switch path, which arrives
    /// from the tick.
    /// </para>
    /// </summary>
    public void RouteScrolling(Func<Core.Interface.PanelScrollStep, bool> scroll) =>
        _scrollers.Add(scroll);

    /// <summary>
    /// Moves the page on every surface, and says so — or null when the phrase was not a scroll,
    /// which is the common case and falls through to the turn (#34).
    /// <para>
    /// <b>All three, because a phrase has no surface attached to it</b> — the same reading that
    /// makes <see cref="Navigate"/> move every navigator. A Commander with a window, a headset and
    /// a strip said it once, into the room.
    /// </para>
    /// <para>
    /// It answers only where something actually moved. A surface already at that end scrolls
    /// nothing and says so, so "page down" at the bottom of the page falls through and is heard
    /// rather than swallowed into silence that looks like not being heard at all.
    /// </para>
    /// </summary>
    public string? Scroll(string spoken)
    {
        if (Core.Interface.PanelScroll.Match(spoken) is not { } step)
        {
            return null;
        }

        // Every one of them, and the answer is about the phrase rather than about any one
        // surface's share of it.
        var moved = _scrollers.Count(scroll => scroll(step));

        return moved > 0 ? Describe(step) : null;
    }

    private static string Describe(Core.Interface.PanelScrollStep step) => step switch
    {
        Core.Interface.PanelScrollStep.PageDown => "Page down.",
        Core.Interface.PanelScrollStep.PageUp => "Page up.",
        Core.Interface.PanelScrollStep.LineDown => "Scrolled down.",
        _ => "Scrolled up.",
    };

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
            //
            // A transcript mode — "technical" — is taken by the first surface at a root, and the
            // mirror has moved the rest before this loop reaches them (Phase 45). So a
            // headset three levels into a checklist, which answers nothing here, is reading
            // Technical when it comes back to the transcript.
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
    /// The plotted route, for anything that wants to draw it (Phase 37, "Progress").
    /// <para>
    /// The same reader the callout and the proper-noun biasing already use, rather than a second
    /// one: two readers of one file is two answers to "where am I going" waiting to disagree.
    /// <see cref="NavRoute.None"/> before composition has run, so a surface built early draws an
    /// empty route rather than throwing.
    /// </para>
    /// </summary>
    public NavRoute Route => _route?.Invoke() ?? NavRoute.None;

    /// <summary>Set during composition, like <see cref="_route"/> and for the same reason.</summary>
    private Func<ModulePower>? _modulePower;

    /// <summary>
    /// What Elite says each module in the ship being flown draws (Phase 38).
    /// <para>
    /// <see cref="ModulePower.None"/> before composition has run and until the file is first read,
    /// so a surface built early weighs the specification table rather than throwing — which is the
    /// same answer it gives for every ship the Commander is not sitting in.
    /// </para>
    /// </summary>
    public ModulePower ModulePower => _modulePower?.Invoke() ?? ModulePower.None;

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
    /// drift (Phase 4, "Apply every setting without a restart").
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
        _pushToTalkButton.ForceUp();

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

        var boundKey = _pushToTalk.Bind(listening.PushToTalkKey);

        // And the stick (Phase 53). Both stay live: a Commander who bound a key and later
        // bound a button has said two things rather than replaced one.
        var boundButton = _pushToTalkButton.Bind(
            D47.Core.Hotas.HotasButton.Parse(listening.PushToTalkButton));

        // Cancel's stick button, rebound on the same apply (#221). Its key half is registered by
        // the window, which is where a system-wide registration needs a handle to live in; only
        // the polled half belongs here.
        _cancelButton.Bind(
            D47.Core.Hotas.HotasButton.Parse(Settings.Current.Speech.CancelButton));

        // Whether that stick is actually here is asked from the tick, not from here (#45).
        // Nothing has polled yet at this point, so the only answer available on this line is
        // "not seen", which is the question rather than the answer to it.

        var bound = boundKey || boundButton;

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

        if (boundKey && Binds.Using(listening.PushToTalkKey!) is { Count: > 0 } collisions)
        {
            // Logged at startup as well as answered on request: the symptom of a double-bound
            // key is that nothing happens, which reads as d47 being broken.
            _logger.LogWarning(
                "Push-to-talk {Key} is also bound in Elite ({Preset}) to {Actions}; one of the two will not work",
                listening.PushToTalkKey,
                Binds.PresetName,
                string.Join(", ", collisions.Select(binding => binding.Action).Distinct()));
        }

        if (_pushToTalkButton.Bound is { } button
            && Binds.UsingJoystickButton(button.Button) is { Count: > 0 } sharing)
        {
            // Hedged, and the hedge is the honest part. Elite writes a joystick binding against
            // its own device hash, which is not the NonRoamableId d47 reads, so this cannot say
            // whether that Joy_N is on the same stick. A false warning costs a sentence; a missed
            // one costs an evening of a microphone that will not open.
            _logger.LogWarning(
                "Push-to-talk {Button} may collide: Elite ({Preset}) binds a button of that number to "
                + "{Actions}. D47 cannot tell whether that is the same controller.",
                button.Describe(),
                Binds.PresetName,
                string.Join(", ", sharing.Select(binding => binding.Action).Distinct()));
        }
    }

    /// <summary>
    /// The stick bound to push-to-talk is not here (Phase 53).
    /// <para>
    /// <b>Said, and the key carries on</b> (the Commander's call, 2026-08-25). A silent controller
    /// otherwise means no voice at all until they notice, and "d47 cannot hear me" with no reason
    /// attached has cost real evenings before. If a key is bound too, it is still live — which is
    /// the whole reason both stay bound rather than one replacing the other.
    /// </para>
    /// <para>
    /// <b>Called from the tick rather than from the settings apply</b>
    /// (<a href="https://github.com/dseelinger/d47/issues/45">#45</a>). It used to run on the line
    /// after the bind, where nothing had polled yet, so it warned on every single binding — the
    /// Commander's own log has it firing at 16:03:34 and the Commander speaking through that
    /// button at 16:03:42. The button now answers the question only once something has looked,
    /// and answers it once per binding rather than once per tick.
    /// </para>
    /// </summary>
    private void WarnIfTheStickIsMissing()
    {
        // Not while the readers are still enumerating: a single enumeration at startup reported
        // three of six devices on the bench, which is the whole of Phase 21's finding 1, and a
        // warning raised then would be wrong more often than right.
        //
        // Checked before the notice is taken rather than after, so an unsettled tick costs the
        // button nothing — the notice fires once per binding and must not be spent on a tick
        // that was never going to say anything.
        if (Controllers?.IsSettled != true)
        {
            return;
        }

        if (_pushToTalkButton.MissingDeviceNotice() is not { } button)
        {
            return;
        }

        _logger.LogWarning(
            "Push-to-talk is bound to {Button} on a controller that is not here",
            button.Describe());
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
    /// The switch annunciator, on both surfaces at once (Phase 21, item 6). Called every
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

        // The ones that move the panel rather than the ship go elsewhere: to each surface on its
        // own thread, and outside _acting, because a page move holds no keys for a honk to
        // collide with (Phase 46).
        foreach (var page in pending.Where(reconcile => reconcile.Destination is not null))
        {
            Show(page.Destination!);
        }

        pending = [.. pending.Where(reconcile => reconcile.Destination is null)];

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

        // Describing a key is the App's business — Core has no keyboard — so the renderer is
        // passed down and the sentence is chosen in Core, where a test reads what a Commander
        // reads. Which bindings exist is Core's answer, not this method's: reading the key alone
        // here told a Commander bound to a stick button that nothing opened the microphone,
        // while it was open (GitHub issue 44).
        var gesture = ListeningCapability.PushToTalkGesture(listening, Input.Gestures.Describe);

        Panel.MicrophoneDetail = MicrophoneNarration.For(
            state,
            listening.Mode,
            Wake.Phrases,
            gesture,
            listening.PreRollMilliseconds);

        // The same three facts, worded for a prompt that is waiting on one (remediation.md 10,
        // item 12). Set from here rather than from the prompt, so both surfaces are told once and
        // cannot disagree about one microphone.
        Panel.ListeningPrompt = MicrophoneNarration.Prompt(
            listening.Mode,
            Wake.Phrases,
            gesture);
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
    /// (Phase 5). Called after the panel is up so the same message is on screen.
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
    /// <summary>
    /// One client per provider, shared by every slot that named it (Phase 57).
    /// <para>
    /// <b>Never one per slot.</b> <c>ElevenLabsTtsProvider.MaxConcurrent</c> gates the account
    /// rather than the pipeline, and that reasoning only survives if two slots choosing one
    /// provider share one instance — six clients would each believe they owned the whole
    /// concurrency budget.
    /// </para>
    /// </summary>
    private readonly Dictionary<string, ITtsProvider> _clients = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// What each slot actually speaks through: a thin metering decorator over one of the shared
    /// clients above, or null for a slot on "none". The decorator is what lets the spend row
    /// break down per slot without <see cref="ITtsProvider"/> learning that slots exist.
    /// </summary>
    private readonly Dictionary<VoiceGroup, ITtsProvider?> _slots = new();

    /// <summary>Which client speaks for a slot. The one place the map is read.</summary>
    private ITtsProvider? Speaker(VoiceGroup group) => _slots.GetValueOrDefault(group);

    /// <summary>
    /// Which provider each slot is on, and whether it had its key last time speech settings were
    /// applied. Tracked rather than inferred, because "is it null" answered "does one need
    /// building" only while there was exactly one to build — and because a key arriving is an
    /// edge rather than a level.
    /// <para>
    /// Handed to <see cref="SpeechWiring.Plan"/> and replaced with what it answers. This field
    /// and that function are the whole of the state; nothing else here remembers the last apply.
    /// </para>
    /// </summary>
    private SpeechWiringState _speechWiring = SpeechWiringState.Nothing;

    /// <summary>
    /// Everyone d47 can speak as (Phase 11). Not a second audio path: it decides which
    /// voice a line is synthesised in, and the line still goes through the one arbiter, because
    /// separate paths per voice are how a line gets spoken in the wrong one (architecture.md D7).
    /// </summary>
    /// <summary>
    /// One cast per provider since Phase 57, because a voice id means nothing to a provider that
    /// did not issue it — and six slots can name up to three of them at once.
    /// </summary>
    public VoiceCasting Casting { get; } = new();

    /// <summary>
    /// The cast aboard the ship. Everything that was <c>Cast</c> before Phase 57 means this one,
    /// which is the provider the companion and the crew speak through.
    /// </summary>
    public VoiceCast Cast => Casting.Of(VoiceGroups.ProviderFor(Settings.Current.Speech, VoiceGroup.Aboard));

    /// <summary>
    /// What each provider in use offers, cached. Empty until the first fetch returns, which is
    /// the honest answer in the meantime: the picker allows a typed value, so an empty list is a
    /// smaller list rather than a dead end.
    /// <para>
    /// Per provider rather than per slot, for the reason the clients are: two slots on one
    /// service ask it one question.
    /// </para>
    /// </summary>
    private readonly Dictionary<string, VoiceCatalogue> _voicesByProvider = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>What one provider offers, or nothing if it has not answered yet.</summary>
    private VoiceCatalogue VoicesOf(string providerId) =>
        _voicesByProvider.GetValueOrDefault(providerId) ?? VoiceCatalogue.Silent;

    /// <summary>What one slot's provider offers.</summary>
    private VoiceCatalogue VoicesFor(VoiceGroup group) =>
        VoicesOf(VoiceGroups.ProviderFor(Settings.Current.Speech, group));

    /// <summary>
    /// The ship's own provider's list. Every voice the <em>companion</em> is chosen from comes
    /// from here — the per-core pairing, the miscast check, the named defaults — and all of that
    /// is about <see cref="VoiceGroup.Aboard"/> and always was.
    /// </summary>
    private VoiceCatalogue AboardVoices => VoicesFor(VoiceGroup.Aboard);

    /// <summary>What the language-model endpoint last said it serves (Phase 29).</summary>
    private volatile IReadOnlyList<string> _endpointModels = [];

    /// <summary>Which provider and address that list came from, so it is asked once each.</summary>
    private volatile string? _endpointModelsFor;

    /// <summary>
    /// What the voices have cost this session (Phase 19). Lives for the process, like
    /// <see cref="Spend"/>, and for the same reason: "what has this cost" is a question about a
    /// session rather than about a turn, and it must survive the provider being switched.
    /// </summary>
    public SpeechSpend SpeechSpend { get; } = new();

    /// <summary>
    /// Auditions already paid for, keyed by the provider that issued the voice, the role being
    /// cast and the voice itself (Phase 19).
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
    /// Speaks one voice so it can be judged before it is chosen (Phase 19, "Hear a voice
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
        // The slot the role belongs to, so the carrier's tower is auditioned through whoever
        // speaks for the carrier — and billed to that slot (Phase 57).
        var group = VoiceGroups.Of(role);

        if (Speaker(group) is not { } provider)
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
                new VoiceSelection(
                    voiceId,
                    SpeechCapability.RateFor(
                        Settings.Current,
                        VoiceGroups.ProviderFor(Settings.Current.Speech, group))),
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
                            // Through SayAsync rather than straight at the synthesiser
                            // (remediation.md 17, item 4). Going direct is why an action d47 took
                            // on its own was missing from the model's history *and* from the
                            // Commander's own conversation page: this path raised neither.
                            await SayAsync(new Announcement(
                                action.Id, $"I could not use {action.Label}. {result.Reason}")).ConfigureAwait(false);
                        }
                    }

                    if (action.Decision.Say is { } say)
                    {
                        await SayAsync(new Announcement(action.Id, say)).ConfigureAwait(false);
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
    /// Everything Phase 8 produces is the ship's AI, so this resolved to one voice until Phase 11
    /// . Now a re-voiced message carries a sender and a carrier line carries a role, and the
    /// lookup for both lives here — the callout knows whose line it is, the cast knows what
    /// that person sounds like, and neither has to know about the other.
    /// </para>
    /// </summary>
    /// <summary>
    /// What "it" currently means, for the lines that would otherwise say a procedural system name
    /// four times running (change-requests.md 30).
    /// </summary>
    private readonly D47.Core.Callouts.SpokenReferent _referent = new();

    /// <summary>
    /// The systems a line could be about: where the Commander is, and where they are going.
    /// <para>
    /// <b>Two candidates rather than one, so an ambiguous line is left alone.</b> A line naming
    /// both — <i>"Sol is 40 light years from Scorpii Sector BB-O a6-2"</i> — hands two names to
    /// the referent, which clears itself rather than choosing, because that is exactly the
    /// sentence where "it" stops being answerable.
    /// </para>
    /// <para>
    /// Filtered to what the line actually says, because a name d47 knows and did not mention is
    /// not a second subject.
    /// </para>
    /// </summary>
    private string[] SystemsIn(string text) =>
        [.. new[] { GameState.Active?.Location.StarSystem, Route.Hops.LastOrDefault()?.StarSystem }
            .Where(name => name is { Length: > 0 }
                && text.Contains(name, StringComparison.OrdinalIgnoreCase))
            .Select(name => name!)];

    private async Task SayAsync(Announcement announcement)
    {
        // The voice takes the pronoun; everything written below keeps the name, so a Commander
        // scrolling back can always see which system "it" was.
        announcement = announcement with
        {
            Text = _referent.Speak(announcement.Text, SystemsIn(announcement.Text), DateTimeOffset.Now),
        };

        // Drawn from the cast belonging to whoever speaks for this slot. A voice id means
        // nothing to a provider that did not issue it, so a stranger in local gets one of the
        // voices their own slot's provider offers and never one of the companion's
        // (Phase 57).
        var cast = Casting.Of(VoiceGroups.ProviderFor(
            Settings.Current.Speech,
            VoiceGroups.Of(announcement.Voice, announcement.CommsChannel)));

        var voice = announcement.Speaker is { Length: > 0 } speaker
            ? cast.ForSender(speaker, announcement.SpeakerIsPlayer, announcement.Voice)
            : cast.For(announcement.Voice);

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

            // **And into the conversation, not only onto the page** (remediation.md 17, item 4).
            // The two used to be the same call and they are not the same thing: `Said` reaches
            // the panel's transcript, which is a `StringBuilder` nobody sends anywhere. Reported
            // as *"I have no record of what I said before this"*, which was exactly true.
            //
            // Gated on `ConversationLine` and therefore on this being d47's own voice — a
            // re-voiced in-game message is somebody else's text and has no path into a prompt
            // (architecture.md §7).
            Turns.Said(spoken);
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
    /// the lines the checklist wants varied (Phase 11: "with varied LLM arrival and
    /// departure responses").
    /// <para>
    /// Only the carrier's own lines. A danger callout is never rewritten by a model: those fire
    /// on the event and say exactly what happened, and "shields are down" is not a line that
    /// benefits from personality (Phase 8).
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
            Turns.BackgroundModel,
            brief.NeedsPersona ? Personas.RenderBlock(personalityEnabled: true) : brief.Speaker,
            StoryFor(brief),
            brief.Instruction,
            brief.NeedsGameState ? Turns.LiveGameState?.Invoke() : null,
            Spend,
            PriceTable.Default,
            _logger,
            budget.Token).ConfigureAwait(false);

        // The authored line stands unless the rewrite is one that may be spoken. Null is a call
        // that failed; the other case is a call that succeeded and came back talking about the
        // model rather than about the callout, which is a non-empty string and used to win
        // (GitHub issue 46). Falling back costs a less varied line and nothing else.
        return FlavourBriefs.MayBeSpoken(line) ? announcement with { Text = line! } : announcement;
    }

    /// <summary>
    /// How long an exchange may spend being written. Looser than <see cref="FlavourBudget"/>
    /// because this composes a scene rather than rewording one line — and nothing waits behind
    /// it with an authored fallback, so running out costs silence rather than a worse line.
    /// </summary>
    private static readonly TimeSpan ChatterBudget = TimeSpan.FromSeconds(10);

    /// <summary>
    /// The invented exchange a chatter marker asked for (#244), as one announcement per parsed
    /// line. Comms role plus a speaker name is what buys the rest: a pooled per-system voice
    /// per invented name, no line in the conversation history, nothing on the comms record —
    /// heard once, and that is all it is for.
    /// <para>
    /// <b>Except the Commander's own carrier's two posts</b> (#249), which are people he has
    /// cast a voice for rather than invented nobodies: those lines come back from
    /// <see cref="NpcChatter.Parse"/> carrying a role, and a role is what
    /// <see cref="VoiceCast.ForSender"/> answers before it draws from the pool. Read fresh per
    /// exchange, because where the carrier is and whether it is going anywhere both change
    /// under this.
    /// </para>
    /// </summary>
    private async Task<IReadOnlyList<Announcement>> ComposeNpcChatterAsync(Announcement marker)
    {
        if (Turns.Provider is null || !Settings.Current.Llm.PersonalityEnabled)
        {
            return [];
        }

        var kind = NpcChatter.KindOf(marker.Key);
        var carrier = NpcChatterCarrier.Of(GameState.Active?.Carrier, GameState.Active?.Location);

        using var budget = new CancellationTokenSource(ChatterBudget);

        var script = await FlavourTurn.AskAsync(
            Turns.Provider,
            Turns.BackgroundModel,
            NpcChatter.Speaker,
            null,
            NpcChatter.Instruction(kind, carrier),
            Turns.LiveGameState?.Invoke(),
            Spend,
            PriceTable.Default,
            _logger,
            budget.Token).ConfigureAwait(false);

        return [.. NpcChatter.Parse(script, kind, carrier)
            .Select(line => new Announcement($"{NpcChatter.KeyPrefix}line", line.Text)
            {
                Urgency = CalloutUrgency.Routine,
                Voice = line.Role ?? D47.Core.Audio.VoiceRole.Comms,
                Speaker = line.Name,
                SpeakerIsPlayer = false,
                CommsChannel = "npc",
            })];
    }

    /// <summary>
    /// Position 4 for a flavour line, to the depth the brief asked for (Phase 43). The
    /// brief decides — in Core, where it can be asserted — and this only reads the two fields.
    /// </summary>
    private string? StoryFor(FlavourBrief brief) =>
        brief.NeedsAboutMe
            ? CommanderStory.Compose(
                Settings.Current.Llm.CharacterSheet, Settings.Current.Llm.AboutMe, withStory: brief.NeedsStory)
            : null;

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
    /// <para>
    /// <b>Flagged rather than fixed (Phase 54).</b> This asks about the conversation
    /// model while one of the two things it gates — the lore lookup — now runs on
    /// <see cref="TurnLoop.BackgroundModel"/>. It is correct today by accident: web search is
    /// endpoint-gated in all three providers, so the model named makes no difference to the
    /// answer. <see cref="D47.Core.Conversation.ILlmProvider"/> says the capability is
    /// model-gated in principle, so the day a provider disagrees this is where it will show.
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
            Turns.BackgroundModel,
            persona: null,
            aboutMe: null,
            LoreLookup.Instruction(systemName),
            gameState: null,
            Spend,
            PriceTable.Default,
            _logger,
            cancellationToken,
            webSearch: true,

            // Cold, and the reason lives beside the instruction in Core (#98).
            sampling: LoreLookup.Sampling);

    /// <summary>
    /// The second half of an arrival remark: a web search, and what it found (Phase 23,
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
                Turns.BackgroundModel,

                // No persona block. This is a report of what a search returned, and a core's
                // voice is for what d47 has to say rather than for what somebody else wrote —
                // which is the whole of the rule the attribution below carries.
                persona: null,
                aboutMe: null,
                LoreLookup.Instruction(name),
                gameState: null,
                Spend,
                PriceTable.Default,
                _logger,
                budget.Token,
                webSearch: true,

                // The same cold sampling the notes window asks for, from the same place.
                sampling: LoreLookup.Sampling).ConfigureAwait(false);

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
    public sealed class CrewTurn(
        AppHost host,
        CrewAddressed addressed,
        string? persona,
        VoiceSelection voice,
        string? captionSpeaker)
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
            host.Voice.CaptionSpeaker = captionSpeaker;
        }
    }

    /// <summary>
    /// Whether this input was addressed to somebody in the fighter bay rather than to the ship's
    /// AI, and if so, everything needed to answer as them (Phase 11, "Ship Crew").
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
        var captionSpeaker = Voice.CaptionSpeaker;

        _logger.LogInformation("Turn addressed to crew member {Name}", addressed.Member.Name);

        // Not a Guardian core. The crew are human pilots hired at a station, and handing one of
        // them a persona block would put a core in two places at once.
        Turns.Persona = CrewAddressing.Brief(addressed.Member, GameState.Active?.Ship.Name);
        Voice.Voice = Cast.ForSender(addressed.Member.Name, isPlayer: false, VoiceRole.Crew);

        // And the caption says who is answering (#201). Their own name rather than the role,
        // because the crew are people the Commander hired and addressed by name — "[Crew]" would
        // be less than the journal already told us.
        Voice.CaptionSpeaker = addressed.Member.Name;

        return new CrewTurn(this, addressed, persona, voice, captionSpeaker);
    }

    private string? _voiceScopeSystem;

    /// <summary>
    /// Drops the NPC voice assignments when the Commander arrives somewhere new. The cast turns
    /// over on a jump; a wingmate does not (Phase 11, "Voices stick").
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

        // And their own carrier, so its traffic comes in the tower's voice (#28). Read fresh each
        // time rather than captured, so a carrier renamed mid-session is matched on its new name.
        if (GameState.Active?.Carrier is { } carrier)
        {
            foreach (var callout in Callouts.Callouts.OfType<IncomingMessages>())
            {
                callout.CarrierName = carrier.Name;
                callout.CarrierCallSign = carrier.CallSign;

                // The third key, and the one that is known before the dock (#109). Read from the
                // same state and on the same pass as the other two, so nothing can be current while
                // another is stale.
                callout.CarrierDisplayName = carrier.DisplayName;

                // Whether the Commander shares a system with their own carrier (#248's second
                // half): the condition under which a System Authority vessel's canned line gets
                // the owner treatment. A delegate over live state rather than a captured value,
                // so a carrier jumping away mid-session is obeyed on the next message.
                callout.AuthorityNearOwnCarrier = () =>
                    GameState.Active is { } active
                    && active.Carrier.Owned
                    && active.Carrier.StarSystem is { Length: > 0 } parked
                    && string.Equals(parked, active.Location.StarSystem, StringComparison.OrdinalIgnoreCase);
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
            Casting.EnteredSystem();
        }

        _voiceScopeSystem = system;
    }

    /// <summary>
    /// Sounds what came due, and says which (Phase 24, "A timer says its own name").
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

                    // Captioned like the warnings are (#201). The issue called this one borderline
                    // and it is not: it is a discrete sound that means something, played ahead of
                    // the sentence that says which timer — exactly the shape the alert cues have.
                    Caption = Core.Audio.AlertCues.Caption(Core.Audio.AlertCue.TimerElapsed),
                });
            }

            var said = missed ? reminder.AnnounceMissed(zone) : reminder.Announce();

            _ = Voice.AnnounceAsync(said);

            Said?.Invoke(said);

            // A timer going off is d47 speaking unasked, like a callout (remediation.md 17,
            // item 4). "Why did you just say that?" has to be answerable about this too.
            Turns.Said(said);
        }
    }

    /// <summary>
    /// A line d47 says because the panel asked it to - a generator's reply, a refusal - spoken,
    /// shown, and recorded exactly as a timer going off is (Phase 47). "Why did you just
    /// say that?" has to be answerable about this too.
    /// </summary>
    public void SayAside(string line)
    {
        if (string.IsNullOrWhiteSpace(line))
        {
            return;
        }

        _ = Voice.AnnounceAsync(line);
        Said?.Invoke(line);
        Turns.Said(line);
    }

    private void SpeakPendingCallouts()
    {
        var pending = Callouts.Drain();

        if (pending.Count == 0)
        {
            return;
        }

        // Somebody else's words, written down in the session record and extracted from by nothing
        // (#162). IncomingMessages says of itself that there is no path from it into a prompt, and
        // there still is not: this record is read by the debrief pass, which reads
        // DebriefSpeaker.Commander and nothing else. What it buys is that the defence is
        // demonstrable rather than asserted — a message saying "from now on, always..." is in the
        // record, is visible, and produces no proposal.
        foreach (var message in pending.Where(announcement =>
                     announcement.Key.StartsWith("message.", StringComparison.Ordinal)))
        {
            NoteHeardFromOutside(message.Text);
        }

        _ = Task.Run(async () =>
        {
            // Varied before the lock is taken, never while holding it. This is a network round
            // trip, and the batch behind it is where a danger callout would be waiting — an
            // alert queued behind a carrier saying hello is an alert that arrives late.
            var lines = new List<Announcement>(pending.Count);

            foreach (var announcement in pending)
            {
                // Invented chatter is composed rather than varied (#244): the marker carries no
                // text of its own, and the exchange arrives back as one announcement per line,
                // each in an invented voice. With no model, or a reply that does not parse, it
                // composes to nothing — there is no authored fallback on purpose (#245).
                if (announcement.Key.StartsWith(NpcChatter.KeyPrefix, StringComparison.Ordinal))
                {
                    lines.AddRange(await ComposeNpcChatterAsync(announcement).ConfigureAwait(false));
                    continue;
                }

                var varied = await VaryAsync(announcement).ConfigureAwait(false);

                // An ambient remark the model did not write is not spoken (#245). VaryAsync
                // hands the same instance back on every road to "no model line" — provider
                // gone mid-recording, the three-second budget, a refusal, a line about itself —
                // and for chatter the authored text is a tone sample, not an understudy.
                // Ambient only: a fuel warning goes out authored, exactly as before.
                if (ReferenceEquals(varied, announcement)
                    && announcement.Key.StartsWith(AmbientCallout.KeyPrefix, StringComparison.Ordinal))
                {
                    continue;
                }

                lines.Add(varied);
            }

            // Whether the lore remarks in this batch are owed a second part, decided here rather
            // than inside the speaking loop: a Commander who asked for a lookup the endpoint
            // cannot run is told so in the first sentence, and the alternative is leaving them
            // waiting for something that was never coming (Phase 23).
            lines = [.. lines.Select(Owing)];

            // One at a time, and in order. A previous batch still being spoken holds this one
            // until it finishes rather than talking over it.
            await _speaking.WaitAsync().ConfigureAwait(false);

            try
            {
                foreach (var announcement in lines)
                {
                    await SayAsync(announcement).ConfigureAwait(false);

                    // What the Commander actually heard about a story, kept (asked for
                    // 2026-08-22). Here rather than where the announcement was made, because the
                    // wording is only final at this point: VaryAsync above has already replaced
                    // the authored line with the model's, and the authored one is what the
                    // adventure file already held.
                    RecordAdventure(announcement);

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

    /// <summary>
    /// A beat, as it was said, onto the story's own feed (asked for 2026-08-22).
    /// <para>
    /// The Adventures tab reads this rather than the authored lines: what was heard is the model's
    /// wording in the core's voice, and the definition holds the other one. It is also what stops
    /// the tab's <em>composing</em> animation — the wait ends when the words arrive, which is here
    /// and not where the beat fired.
    /// </para>
    /// <para>
    /// Silent about anything that is not one of these, which is nearly every announcement. The
    /// acknowledgement is deliberately not recorded: <c>AdventureAcks</c> is feedback rather than
    /// story, and a feed reading "That's it." between every beat would be a feed nobody reads.
    /// </para>
    /// </summary>
    private void RecordAdventure(Announcement announcement)
    {
        if (Adventures is not { } adventures
            || D47.Core.Adventures.AdventureCallout.Reached(announcement.Key) is not var (key, beat))
        {
            return;
        }

        var commander = GameState.Active?.Identity.FrontierId;
        var story = adventures.Book.Store.Find(commander, key);
        var reached = beat >= 0 ? story?.Beats.ElementAtOrDefault(beat) : null;

        adventures.Book.Told(commander, key, new D47.Core.Adventures.AdventureTold
        {
            Kind = D47.Core.Adventures.AdventureToldKind.Beat,
            Text = announcement.Text,
            At = DateTimeOffset.Now,
            Beat = beat,
            Title = reached?.Title ?? (beat < 0 ? "Opening" : null),

            // Stored rather than derived later: a story edited after a beat has fired would
            // otherwise re-describe what the Commander did with the trigger it has now.
            Trigger = reached?.Trigger.Describe(),
        });
    }

    /// <summary>
    /// One exchange, filed against any story it was about (asked for 2026-08-22).
    /// <para>
    /// The Commander chose the heuristic: a turn joins a story's feed when their words or the reply
    /// name the story, one of its beats, or a place a beat waits at — see
    /// <see cref="D47.Core.Adventures.AdventureMention"/>, which is where the whole-word rule and
    /// its reasons live. No classification turn: a round trip in front of every answer is the cost
    /// this change exists to remove elsewhere.
    /// </para>
    /// <para>
    /// Only stories under way. A draft is not something the Commander is flying, and a finished one
    /// is finished.
    /// </para>
    /// </summary>
    public void NoteTurn(string? asked, string? answered)
    {
        // The debrief's record, and it is written here rather than at the panel for one reason:
        // this is the single call site that has both halves of a turn with the speakers already
        // told apart. The panel flattens them onto a page, and a page is what an attack is made
        // of (#162, see DebriefSpeaker).
        if (Settings.Current.Debrief.Enabled)
        {
            var heardAt = DateTimeOffset.Now;

            Debriefing.Say(heardAt, DebriefSpeaker.Commander, asked ?? string.Empty);
            Debriefing.Say(heardAt, DebriefSpeaker.Ship, answered ?? string.Empty);
        }

        if (Adventures is not { } adventures
            || string.IsNullOrWhiteSpace(answered))
        {
            return;
        }

        var commander = GameState.Active?.Identity.FrontierId;

        foreach (var standing in adventures.Book.Active(commander))
        {
            if (!D47.Core.Adventures.AdventureMention.InExchange(standing.Adventure, asked, answered))
            {
                continue;
            }

            adventures.Book.Told(commander, standing.Adventure.Key, new D47.Core.Adventures.AdventureTold
            {
                Kind = D47.Core.Adventures.AdventureToldKind.Aside,
                Text = answered.Trim(),
                Asked = asked?.Trim(),
                At = DateTimeOffset.Now,
            });
        }
    }

    /// <summary>
    /// Writes down something that reached the Commander from outside the two of them — an in-game
    /// message read aloud, a quoted search result
    /// (<a href="https://github.com/dseelinger/d47/issues/162">#162</a>).
    /// <para>
    /// <b>Recorded on purpose, and extracted from by nothing.</b> A correction only makes sense
    /// next to what provoked it, so leaving these out would make the record uncheckable — and
    /// <see cref="DebriefExtractor"/> reads <see cref="DebriefSpeaker.Commander"/> and nothing
    /// else, so a hostile message saying <em>from now on, always…</em> is visible in the record
    /// and produces no proposal.
    /// </para>
    /// </summary>
    public void NoteHeardFromOutside(string text)
    {
        if (Settings.Current.Debrief.Enabled)
        {
            Debriefing.Say(DateTimeOffset.Now, DebriefSpeaker.Game, text);
        }
    }

    /// <summary>
    /// Records that d47 was stopped mid-sentence
    /// (<a href="https://github.com/dseelinger/d47/issues/162">#162</a>).
    /// <para>
    /// Feedback nobody typed, and deliberately ambiguous: it may mean d47 is too verbose, and it
    /// may mean something happened. So it is collected and, if it happens often enough in one
    /// session, becomes a question at the end of it. Nothing changes on its own.
    /// </para>
    /// </summary>
    public void NoteInterrupted() => NoteSignal(new DebriefSignal(
        DateTimeOffset.Now,
        DebriefSignalKind.SpeechCutOff,
        "you stopped me while I was talking"));

    /// <summary>
    /// Records that a callout was switched off within seconds of it speaking
    /// (<a href="https://github.com/dseelinger/d47/issues/162">#162</a>). Same terms as
    /// <see cref="NoteInterrupted"/>: a question at the end of the session, never an adjustment
    /// during it.
    /// </summary>
    public void NoteSilenced(CalloutSilenced silenced)
    {
        ArgumentNullException.ThrowIfNull(silenced);

        NoteSignal(new DebriefSignal(
            silenced.When,
            DebriefSignalKind.WarningDisabledSoonAfter,
            $"the {silenced.Id} callout"));
    }

    private void NoteSignal(DebriefSignal signal)
    {
        if (!Settings.Current.Debrief.Enabled)
        {
            return;
        }

        lock (_signalGate)
        {
            _signals.Add(signal);
        }
    }

    /// <summary>
    /// Opens a directions session over what the file says right now, and puts the block into the
    /// prompt (<a href="https://github.com/dseelinger/d47/issues/162">#162</a>).
    /// <para>
    /// <b>Called at startup and at a Commander change, and nowhere else.</b> Those are the two
    /// real boundaries — the second because the directions are one person's — and calling it
    /// anywhere else would be the per-turn churn Phase 54 measured at 23x, arriving by a
    /// different road.
    /// </para>
    /// </summary>
    public void BeginDirections()
    {
        if (Debrief is not { } debrief)
        {
            return;
        }

        debrief.Book.Store.Poll();
        _directions.Begin(debrief.Book.Adopted);

        Turns.Directions = _directions.Block();

        // Position 3 is rebuilt too, because a per-core direction rides in the persona block: the
        // overlay lives beside the Commander's other data and the pack is never touched (#162).
        ApplyPersonaBlock();

        _logger.LogInformation(
            "Standing directions latched: {Count} adopted, {Bytes} characters at position 6",
            _directions.Latched.Count,
            Turns.Directions?.Length ?? 0);
    }

    /// <summary>
    /// Position 3, with this core's overlay behind it
    /// (<a href="https://github.com/dseelinger/d47/issues/162">#162</a>).
    /// <para>
    /// <b>Appended here rather than merged into the pack.</b> Persona writing lives twice —
    /// <c>guardian-personas.md</c> ported into <see cref="PersonaCatalog"/> — so anything that
    /// edited either copy at runtime would drift them apart with nothing checking. What the model
    /// reads is the shipped block with a line of the Commander's own underneath it, and both
    /// copies of the pack stay exactly as they were written.
    /// </para>
    /// <para>
    /// The overlay comes from the <em>latched</em> set, so adopting one mid-session still reaches
    /// nothing until the next; switching core mid-session still changes position 3, which it
    /// always has.
    /// </para>
    /// </summary>
    private void ApplyPersonaBlock()
    {
        var block = Personas.RenderBlock(Settings.Current.Llm.PersonalityEnabled);

        if (block is not null && _directions.Overlay(Personas.Current.Id) is { } overlay)
        {
            block = block + "\n\n" + overlay;
        }

        Turns.Persona = block;
    }

    /// <summary>
    /// Runs the debrief over what this session sounded like, and files what it drafted
    /// (<a href="https://github.com/dseelinger/d47/issues/162">#162</a>).
    /// <para>
    /// <b>At the end of a session, over a record that was never on disk.</b> Phase 31 wrote down
    /// why a rolling transcript is not kept — a privacy liability, a context-window problem and a
    /// confabulation engine — and running the pass now rather than tomorrow is what buys the
    /// nightly cadence's convenience without buying all three back. What survives is a handful of
    /// proposals, each quoting one sentence the Commander said.
    /// </para>
    /// </summary>
    /// <param name="frontierId">
    /// Who the session belonged to. Named at a Commander change, where the game state has already
    /// moved on; null at exit, where asking is right.
    /// </param>
    public void RunDebrief(string? frontierId = null)
    {
        if (Debrief is not { } debrief || !Settings.Current.Debrief.Enabled)
        {
            return;
        }

        DebriefSignal[] signals;

        lock (_signalGate)
        {
            signals = [.. _signals];
            _signals.Clear();
        }

        try
        {
            var drafted = debrief.Book.Propose(
                Debriefing,
                signals,
                DateTimeOffset.Now,
                Personas.Current.Id,

                // What this installation answers to, so "hey Warden, stop calling it that" reads
                // as an instruction rather than as a sentence beginning with a name.
                [Personas.Current.Name, Settings.Current.Persona.ShipName ?? string.Empty],
                frontierId);

            _logger.LogInformation(
                "Debrief drafted {Count} proposals from {Lines} lines and {Signals} signals",
                drafted.Count,
                Debriefing.Lines.Count,
                signals.Length);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Losing a debrief costs a list nobody had agreed to. Losing the shutdown over it is
            // not a trade anybody would make.
            _logger.LogWarning(ex, "The debrief pass could not write its proposals");
        }
        finally
        {
            Debriefing.Empty();
        }
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
    /// Tries the stored language-model key for real (Phase 16, "a key is verified, not
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
        // spending a turn on it (Phase 29), and that is the same reasoning the speech key
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

            // Nothing said about sampling, on purpose (#98). This asks one token in order
            // to learn whether a key works, against a gateway that may validate fields d47
            // has never met — and a rejected field here reads as a rejected key, which sends
            // a Commander to their account page for another one that will fail the same way.
            Sampling = LlmSampling.Unstated,

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

            TtsProviderCatalog.OpenAiId => new OpenAiTtsProvider(
                () => key,
                _loggerFactory.CreateLogger<OpenAiTtsProvider>()),

            TtsProviderCatalog.CartesiaId => new CartesiaTtsProvider(
                () => key,
                _loggerFactory.CreateLogger<CartesiaTtsProvider>()),

            _ => null,
        };

        if (provider is null)
        {
            return SecretCheck.Unreachable($"D47 has no client for {selected.Name} yet.");
        }

        // A provider whose catalogue is static cannot be checked by listing it: the list is known
        // without a key, so it would answer "accepted the key" for a key that had never left this
        // machine. One character, synthesised and discarded, proves the call that actually has to
        // work — a fraction of a cent (Phase 58).
        if (selected.VoicesAreStatic)
        {
            return await ProveSpeechKeyAsync(provider, selected, budget.Token).ConfigureAwait(false);
        }

        try
        {
            var voices = await provider.ListVoicesAsync(budget.Token).ConfigureAwait(false);

            // Read from the listing rather than from the count, which is what this check was
            // quietly doing wrong: the provider answers an empty list rather than throwing, so a
            // rejected key arrived here as "accepted the key — 0 voices" (Phase 19).
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

    /// <summary>
    /// Proves a key by speaking one character and throwing the audio away (Phase 58).
    /// <para>
    /// For a provider whose voice list is static, where listing proves nothing. The distinction
    /// the answer has to keep is the one that makes a key check worth having: <b>"refused the
    /// key" and "could not be reached" are different answers with different remedies</b>, and one
    /// reported as the other sends the Commander to rotate a key that was fine
    /// (docs/spikes/elevenlabs-voice-sources.md §3).
    /// </para>
    /// </summary>
    private async Task<SecretCheck> ProveSpeechKeyAsync(
        ITtsProvider provider,
        TtsProviderInfo selected,
        CancellationToken cancellationToken)
    {
        try
        {
            _ = await provider
                .SynthesizeAsync(".", VoiceSelection.Default, cancellationToken)
                .ConfigureAwait(false);

            return SecretCheck.Works($"{selected.Name} accepted the key.");
        }
        catch (OperationCanceledException)
        {
            return SecretCheck.Unreachable(
                $"{selected.Name} did not answer within {KeyCheckBudget.TotalSeconds:0} seconds.");
        }
        catch (TtsException ex) when (ex.Fault == TtsFault.KeyRejected)
        {
            return SecretCheck.Rejected(ex.Message);
        }
        catch (Exception ex)
        {
            // Everything else is the network's problem rather than the key's, which is what the
            // Commander needs to know: there is nothing here for them to change.
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
    /// Waits for Status.json to report the galaxy map showing, or no longer showing. True when it
    /// did in time, false when it did not, null when the file was never readable at all. The
    /// route half of the same question is <see cref="Input.RoutePlotWatch"/>.
    /// <para>
    /// Logged either way, with the focus it saw, because this is the one step of the macro that
    /// can be read back at all — a report of "nothing happened" has to start from whether the
    /// map was even showing.
    /// </para>
    /// <para>
    /// Reads <see cref="GameStatusReader.Current"/> rather than polling the reader itself: the
    /// tick loop already re-reads Status.json ten times a second, and a second poller on another
    /// thread would race it over one stamp. Three seconds is several times what the map takes to
    /// open or close, so a miss here means the key did not reach the game rather than that the
    /// game was slow.
    /// </para>
    /// </summary>
    /// <summary>
    /// Waits for Status.json to say something, or gives up (Phase 52).
    /// <para>
    /// The same three answers <see cref="AwaitGalaxyMap"/> gives, and for the same reason: true
    /// means it happened, false means it did not, and <c>null</c> means d47 never got a readable
    /// status file and so cannot claim either. A macro that reports failure when it simply could
    /// not see is the failure mode this shape exists to avoid.
    /// </para>
    /// </summary>
    private static async Task<bool?> AwaitStatus(
        GameStatusReader status,
        Func<Core.Journal.GameStatus, bool> arrived,
        TimeSpan within,
        string what,
        Microsoft.Extensions.Logging.ILogger logger,
        CancellationToken cancellationToken)
    {
        var started = DateTimeOffset.Now;
        var deadline = started + within;
        var sawTheFile = false;

        while (DateTimeOffset.Now < deadline)
        {
            var current = status.Current;

            if (current.IsKnown)
            {
                sawTheFile = true;

                if (arrived(current))
                {
                    logger.LogInformation(
                        "Status reached {What} after {Elapsed:0.0}s",
                        what,
                        (DateTimeOffset.Now - started).TotalSeconds);
                    return true;
                }
            }

            try
            {
                await Task.Delay(TimeSpan.FromMilliseconds(100), cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                return null;
            }
        }

        logger.LogInformation(
            "Status never reached {What} within {Seconds:0}s; Status.json {Readable}",
            what,
            within.TotalSeconds,
            sawTheFile ? "readable" : "never readable");

        return sawTheFile ? false : null;
    }

    /// <summary>
    /// The next status sample, which is what the boost loop watches (Phase 52).
    /// <para>
    /// Elite rewrites Status.json several times a second, so this waits one polling interval and
    /// reads again rather than trying to detect a change: the loop only cares what the flag says
    /// now, and a sample identical to the last one is a perfectly good answer to that.
    /// </para>
    /// </summary>
    private static async Task<Core.Journal.GameStatus> NextStatus(
        GameStatusReader status,
        CancellationToken cancellationToken)
    {
        await Task.Delay(TimeSpan.FromMilliseconds(250), cancellationToken).ConfigureAwait(false);
        return status.Current;
    }

    private static async Task<bool?> AwaitGalaxyMap(
        GameStatusReader status,
        bool open,
        Microsoft.Extensions.Logging.ILogger logger,
        CancellationToken cancellationToken)
    {
        var started = DateTimeOffset.Now;
        var deadline = started + TimeSpan.FromSeconds(3);
        var sawTheFile = false;

        while (DateTimeOffset.Now < deadline)
        {
            var current = status.Current;

            if (current.IsKnown)
            {
                sawTheFile = true;

                if ((current.GuiFocus == Core.Journal.GuiFocus.GalaxyMap) == open)
                {
                    logger.LogInformation(
                        "Galaxy map {State} after {Elapsed:0.0}s (GuiFocus {Focus})",
                        open ? "open" : "closed",
                        (DateTimeOffset.Now - started).TotalSeconds,
                        current.GuiFocus);
                    return true;
                }
            }

            try
            {
                await Task.Delay(TimeSpan.FromMilliseconds(100), cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                return null;
            }
        }

        logger.LogInformation(
            "Galaxy map not {State} within 3s; Status.json {Readable}, GuiFocus {Focus}",
            open ? "open" : "closed",
            sawTheFile ? "readable" : "never readable",
            status.Current.GuiFocus);

        return sawTheFile ? false : null;
    }

    /// <summary>
    /// Every phrase d47 already answers to. A macro may not take one of these, because a macro
    /// called "gear down" would shadow a phrase that already means something and the Commander
    /// would have no way to tell which one ran.
    /// </summary>
    /// <summary>
    /// Every journal on the disk, oldest first, for the Commander's log (Phase 33).
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
                .. registry.All.SelectMany(c => c.Descriptor.Keywords).Select(keyword => keyword.Phrase),
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

        // The debrief, over what this session sounded like (#162). First, with the rest of the
        // shutdown still standing: the record is in memory and goes with the process, so a pass
        // that ran after the teardown would be a pass that ran over nothing. It writes proposals
        // and changes no prompt — what it drafts reaches the model only if the Commander takes it,
        // and then only at the start of a later session.
        RunDebrief();

        // When the core aboard stopped being aboard, which is now (Phase 35). Every other
        // core's stamp was written as it was switched away from; this is the one that never is,
        // and without it the core a Commander closes d47 on is the one core that could never earn
        // a gap reaction. A crash loses it, which costs one reaction and nothing else.
        RememberCoreAboard(Personas.Current.Id, DateTimeOffset.Now);

        Settings.Changed -= OnSettingsChanged;
        Personas.Changed -= OnPersonaChanged;
        GameState.CommanderChanged -= OnCommanderChanged;

        // The loop stops before anything it polls is torn down, so a tick cannot land on a
        // disposed sink or a closed file handle on the way out.
        _ticking?.Dispose();

        // And then let go of the game (#206). After the tick and not before it: the autonomous
        // drain and the switch reconciler both press keys on the tick, and a send landing on a
        // disposed injector is the same class of fault as a stranded key. In practice the
        // per-send finally has already released everything; this is the net for the send that
        // did not get that far.
        _gameInput.Dispose();

        // After the tick, so a serve cannot land on a destroyed overlay handle. A quad nobody
        // gave back stays floating in the cockpit after the app that put it there has gone.
        Vr?.Dispose();
        _speaking.Dispose();

        // After the tick has stopped, so a poll cannot land on a disposed capture device.
        _pushToTalk.ForceUp();
        _pushToTalkButton.ForceUp();
        _cancelButton.ForceUp();
        _microphone.Dispose();
        _transcriber.Dispose();
        (Models as IDisposable)?.Dispose();

        // Before the arbiter and the sink it is subscribed to, and before the last clip stops
        // being writable. It unhooks both seams and drains what it holds, so the last utterance
        // of a session — often the one being investigated — is on disk (#164).
        AudioRecorder?.Dispose();

        // Stop making noise before tearing anything down. Disposing the sink under a playing
        // clip is how an exit ends in a buzz rather than in silence.
        Audio.Silence();
        Audio.Dispose();
        _audioSink.Dispose();
        foreach (var client in _clients.Values)
        {
            (client as IDisposable)?.Dispose();
        }

        _clients.Clear();
        _slots.Clear();

        // Before the factory that owns the sink it writes to. Reaching this line is the whole of
        // the clean marker -- anything that threw above it leaves the "is stopping" line standing
        // on its own, which is what tells a reader the teardown is where to look.
        _logger.LogInformation("d47 stopped cleanly");

        _loggerFactory.Dispose();
        Log.CloseAndFlush();
    }
}
