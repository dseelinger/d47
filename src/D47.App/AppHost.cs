using System.Runtime.CompilerServices;
using System.Reflection;
using D47.App.Input;
using D47.App.Logging;
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
using D47.Core.Configuration;
using D47.Core.Conversation;
using D47.Core.Diagnostics;
using D47.Core.Input;
using D47.Core.Journal;
using D47.Core.Listening;
using D47.Core.Persona;
using D47.Core.Ticking;
using D47.Llm;
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
        LlmAvailabilityState llmAvailability,
        SpendTracker spend,
        WasapiAudioSink audioSink,
        AudioArbiter audio,
        CueLibrary cues,
        VoicePipeline voice,
        ListenGate gate,
        WasapiMicrophone microphone,
        PushToTalkKey pushToTalk,
        EliteBinds binds,
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
        LlmAvailability = llmAvailability;
        Spend = spend;
        _audioSink = audioSink;
        Audio = audio;
        Cues = cues;
        Voice = voice;
        Listening = gate;
        Binds = binds;
        _microphone = microphone;
        _pushToTalk = pushToTalk;
        Models = models;
        _transcriber = transcriber;
        Version = version;
        StartupError = startupError;
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
    /// The Commander's own per-state avatar frames, if they have supplied any. Null until
    /// startup has scanned for them, and empty for almost everyone — the panel draws its own.
    /// </summary>
    public D47.Core.Interface.AvatarLibrary? Avatars { get; private set; }

    /// <summary>Whether the model is usable right now, and why not when it isn't.</summary>
    public LlmAvailabilityState LlmAvailability { get; }

    /// <summary>Per-turn cost and the running total.</summary>
    public SpendTracker Spend { get; }

    /// <summary>
    /// The one queue every audible thing goes through (architecture.md D7). Exposed because
    /// the hotkey and the panel both need to silence it, and there is nowhere else to ask.
    /// </summary>
    public AudioArbiter Audio { get; }

    public CueLibrary Cues { get; }

    /// <summary>What a turn sounds like.</summary>
    public VoicePipeline Voice { get; }

    /// <summary>
    /// The gate the microphone feeds. Exposed because a surface subscribes to its utterances —
    /// the gate itself knows nothing about turns.
    /// </summary>
    public ListenGate Listening { get; }

    /// <summary>
    /// The Commander's Elite bindings, read once at startup. Read-only, and the same parse the
    /// double-bind check and Phase 10's reachability both use.
    /// </summary>
    public EliteBinds Binds { get; }

    /// <summary>The Commander's macros. The panel's editor writes through this, not past it.</summary>
    public MacroStore Macros { get; private set; } = null!;

    /// <summary>
    /// Every phrase d47 already answers to, so the macro editor can refuse one that would
    /// shadow a built-in command. Computed once: the registry is immutable.
    /// </summary>
    public IReadOnlyList<string> ReservedPhrases { get; private set; } = [];

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
        Log.Logger = LoggingSetup.Create(paths, verbosity);
        var loggerFactory = new SerilogLoggerFactory(Log.Logger);
        var logger = loggerFactory.CreateLogger<AppHost>();

        logger.LogInformation("D47 {Version} starting; data folder {Data}", version, paths.Data);

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

        var gameState = new GameStateStore();
        var journal = new JournalSpine(journalDirectory, gameState, loggerFactory);

        // The two state files Elite rewrites in place. Same folder as the journal, different
        // shape: a log is appended to and these are replaced, which is entirely inside the
        // readers.
        var status = new GameStatusReader(journalDirectory, loggerFactory.CreateLogger<GameStatusReader>());
        var route = new NavRouteReader(journalDirectory, loggerFactory.CreateLogger<NavRouteReader>());

        var callouts = BuildCallouts(loaded, loggerFactory);

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

        tick.Add("journal", context =>
        {
            var events = journal.Poll();
            status.Poll();
            route.Poll();

            var calloutContext = new CalloutContext(
                context.Now,
                IsPriming: context.IsFirst,
                gameState.Active,
                status.Current,
                route.Current,
                events);

            callouts.Tick(calloutContext);
            autonomous.Tick(calloutContext);
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
        var spend = new SpendTracker();

        // Audio comes up before the registry because the speech capability's settings rows read
        // the shipped bed names and the device list from it. The sink is opened here rather than
        // lazily so a machine with no working output says so once, at startup, instead of on the
        // first turn the Commander was hoping to hear.
        var cues = CueLibrary.Load();
        var audioSink = new WasapiAudioSink(loggerFactory.CreateLogger<WasapiAudioSink>());
        var audio = new AudioArbiter(audioSink, loggerFactory.CreateLogger<AudioArbiter>()).Start();
        var voice = new VoicePipeline(audio, cues, loggerFactory);

        // The loop settles back to idle when the arbiter goes quiet rather than when the turn
        // returns, because the turn returns while the reply is still being spoken. Wired here
        // because VoicePipeline has a primary constructor and cannot subscribe from one.
        audio.ActivityChanged += voice.Settle;

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
        var microphone = new WasapiMicrophone(gate, loggerFactory.CreateLogger<WasapiMicrophone>());
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

        // Read once at startup. The bindings file changes only when the Commander edits their
        // controls, which they cannot do while d47 is the foreground window, so re-reading it
        // ten times a second would be polling for an event that cannot happen.
        var binds = BindsResolver.Resolve(
            BindsResolver.DefaultBindingsDirectory(),
            EliteInstallations(),
            loggerFactory.CreateLogger<AppHost>());

        bindsRef = () => binds;

        // The Commander's own macros, beside the executable like everything else d47 writes.
        // Re-read on the tick, so a macro edited in a text editor is live without a restart.
        var macros = new MacroStore(
            Path.Combine(paths.Data, "macros.json"), loggerFactory.CreateLogger<MacroStore>());

        var cancellation = new TurnCancellation(loggerFactory.CreateLogger<TurnCancellation>());

        // Built before the registry, because the persona capability declares settings rows from
        // it and which rows exist has to be settled before registration — descriptors are
        // registered once and never mutated (architecture.md D5).
        var personas = new PersonaHost(PersonaCatalog.Resolve(settings.Current.Persona.Id));

        // The help capability answers from the registry it is itself registered in, so the
        // accessor is filled in immediately after Build. A Func rather than a mutable property
        // on the descriptor: descriptors are registered once and never mutated (architecture.md
        // D5), and that rule is what keeps tool schemas byte-identical across turns.
        CapabilityRegistry? built = null;

        // The same late-binding trick, for the same reason: the headset path is built after
        // Avalonia comes up, which is after this.
        AppHost? self = null;

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
                    Beds = [.. cues.BedNames],
                    OutputDevices = () => [.. WasapiAudioSink.Devices().Select(device => device.Id)],
                    DeviceLabel = id => WasapiAudioSink.Devices()
                        .FirstOrDefault(device => device.Id == id).Name ?? id,

                    // Late-bound like the headset surface below, and for the same reason: the
                    // list is fetched from the provider over the network after this point.
                    Voices = () => self?.VoiceIds() ?? [],
                    VoiceLabel = id => self?.VoiceLabelFor(id) ?? id,
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
                    Binds = () => binds,
                    InstalledModels = () => models.Installed(),

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
                    Binds = () => binds,

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
                coverage is null ? null : () => coverage.Report().Summary));

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

            LiveGameState = () => Join(
                Situation.Describe(gameState.Active),
                Join(ActionCapabilities.Describe(actionSurface), MacroCapability.Live(macros))),
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
            llmAvailability,
            spend,
            audioSink,
            audio,
            cues,
            voice,
            gate,
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

        // The avatar's own imagery, if the Commander has dropped any in. Scanned once at
        // startup; the drawn face is what every state falls back to, so an empty data/avatar is
        // the normal case rather than a missing asset.
        host.Avatars = D47.Core.Interface.AvatarLibrary.Load(paths);

        // The face follows the loop. Set straight onto the view model from whichever thread the
        // state arrived on: a view model is affine to nothing, and the view marshals — which is
        // the rule the transcript scroll already follows, so the avatar does not get a second
        // one of its own.
        voice.StateEntered += state => host.Panel.LoopState = state;

        // A voice the provider refuses is written out of settings rather than merely skipped for
        // the turn it broke. Subscribed before the first ApplySpeechSettings, because a stored
        // voice can be refused on the very first thing d47 says.
        voice.VoiceRejected += host.ForgetTheVoice;

        host.ApplyLlmSettings();
        host.ApplySpeechSettings();
        host.ApplyListeningSettings();

        // From here on, a setting takes effect because it changed — not because something was
        // restarted (list.md Phase 4, "Apply every setting without a restart").
        settings.Changed += host.OnSettingsChanged;

        host.Macros = macros;
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
            gate.Poll(context.Now);
        });

        pushToTalk.Pressed += () => gate.KeyDown(DateTimeOffset.Now);
        pushToTalk.Released += () => gate.KeyUp();

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

        tick.Add("callout-drain", _ => host.SpeakPendingCallouts());

        // NPC voices are scoped to the system, so something has to notice the system changing.
        // Sampled on the tick rather than hooked to a journal event, because the state store is
        // already folding those and a second reader would be a second thing to keep in step.
        tick.Add("voice-scope", _ => host.FollowSystemForVoices());

        // After the callouts, so a honk that reports why it did not fire is spoken in the same
        // order it was decided relative to everything else this tick.
        tick.Add("autonomous-drain", _ => host.CarryOutPendingActions(autonomous, gameInput));

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
    private static CalloutEngine BuildCallouts(D47Settings settings, ILoggerFactory loggers)
    {
        var engine = new CalloutEngine(loggers.CreateLogger<CalloutEngine>())
            .Add(new DangerCallout())
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
        engine.SetEnabled("ambient", callouts.Ambient);

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

                case AmbientCallout ambient:
                    ambient.Interval = TimeSpan.FromMinutes(callouts.AmbientMinutes);

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
        else if (ResolveKey(selected) is { } resolved)
        {
            provider = selected.Id switch
            {
                LlmProviderCatalog.AnthropicId => new AnthropicLlmProvider(resolved.Key, current.Llm.Endpoint),
                _ => null,
            };

            if (provider is null)
            {
                reason = $"D47 has no client for {selected.Name} yet.";
            }
            else
            {
                _logger.LogInformation(
                    "{Provider} configured from {Source}, endpoint {Endpoint}",
                    selected.Name,
                    resolved.Source,
                    current.Llm.Endpoint ?? selected.DefaultEndpoint ?? "(provider default)");
            }
        }
        else
        {
            reason = $"No {selected.Name} API key is stored. Add one in Settings.";
        }

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
    /// Rebuilds everything downstream of the speech settings: the voice provider, the voice
    /// itself, the cues, the bed, the output device and the retry policy. Called at startup and
    /// again on any change, so the two paths cannot drift (list.md Phase 4, "Apply every
    /// setting without a restart").
    /// </summary>
    /// <summary>The ids the voice picker offers.</summary>
    internal IReadOnlyList<string> VoiceIds() => [.. _voices.Select(voice => voice.Id)];

    /// <summary>
    /// How the picker labels one — "Ava — Female, en-US" rather than the raw id. Falls back to
    /// the id, so a voice the Commander typed themselves still shows as what they typed.
    /// </summary>
    internal string VoiceLabelFor(string id) =>
        _voices.FirstOrDefault(voice => string.Equals(voice.Id, id, StringComparison.OrdinalIgnoreCase))
            ?.Label ?? id;

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
                _voices,
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

    private async Task PairPersonaVoicesAsync()
    {
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
                _voices,
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
    /// that result.
    /// <para>
    /// Asked on every apply rather than only when the provider is seen to change, which is the
    /// difference that matters: the old check watched <c>_ttsProviderId</c>, and that is null on
    /// the first call of the process, so a settings file that was already mismatched was trusted
    /// on every launch and every sentence failed forever. The file now says which provider its
    /// voices came from, so the question can be asked of the file instead of of the process.
    /// </para>
    /// <para>
    /// A file with nothing recorded is stamped rather than cleared. It was written before d47
    /// recorded this, and its voices are as likely to be right as wrong - throwing them away on
    /// a guess would cost every Commander whose file was fine, while the one whose file is not
    /// is repaired at the seam the moment a voice is actually refused.
    /// </para>
    /// </summary>
    private SpeechSettings ReconcileVoicesWithProvider()
    {
        var speech = Settings.Current.Speech;
        var selected = TtsProviderCatalog.Selected(speech.Provider).Id;

        if (string.Equals(speech.VoicesProvider, selected, StringComparison.Ordinal))
        {
            return speech;
        }

        if (speech.VoicesProvider is { } chosenFor)
        {
            ForgetVoicesChosenFor(chosenFor, selected);
        }
        else
        {
            Settings.Replace(
                SpeechCapability.ProviderKey,
                current => current with { Speech = current.Speech with { VoicesProvider = selected } });
        }

        return Settings.Current.Speech;
    }

    /// <summary>Whether the selected provider has whatever credential it needs, if it needs one.</summary>
    private bool HasKeyFor(TtsProviderInfo provider) =>
        provider.KeySecretName is not { } secret || Secrets.Has(secret);

    /// <summary>
    /// Drops every voice chosen while <paramref name="previous"/> was selected.
    /// <para>
    /// A voice id is only meaningful to the provider that issued it, so these are not settings
    /// that survive a switch — they are settings that belong to a provider no longer in use.
    /// Clearing <c>VoicesPaired</c> as well is what lets the pairing run again against the new
    /// provider's list; leaving it set was how eleven cores kept pointing at voices that had
    /// stopped existing.
    /// </para>
    /// </summary>
    private void ForgetVoicesChosenFor(string previous, string now)
    {
        _logger.LogInformation(
            "The stored voices were chosen for {Previous} and {Now} is selected; clearing them",
            previous,
            now);

        Settings.Replace(
            SpeechCapability.ProviderKey,
            current => SpeechCapability.WithoutChosenVoices(current, now));
    }

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
            _logger.LogInformation("The voice list has {Count} voices", _voices.Count);

            // The pool a re-voiced sender is drawn from. English-locale voices only, where the
            // provider tags a locale at all: Edge offers several hundred across every language
            // it supports, and drawing a wingmate's voice from all of them means most Commanders
            // hear their wing in a language they do not speak. ElevenLabs tags an accent rather
            // than a locale, so nothing is filtered out there and the whole account is the pool.
            Cast.Pool =
            [
                .. _voices
                    .Where(voice => voice.Locale.Length == 0
                                    || voice.Locale.StartsWith("en", StringComparison.OrdinalIgnoreCase))
                    .Select(voice => voice.Id),
            ];

            _logger.LogInformation("{Count} voices are available for re-voiced senders", Cast.Pool.Count);

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
    private DateTimeOffset _personaSelectedAt = DateTimeOffset.Now;

    private readonly Dictionary<string, (DateTimeOffset At, SessionSummary Session)> _personaLastSeen =
        new(StringComparer.Ordinal);

    private void ApplyPersonaSettings()
    {
        var outgoing = Personas.Current;

        // Remembered before the switch, because after it there is nothing left to measure
        // against. Keyed by the core leaving, not the one arriving.
        _personaLastSeen[outgoing.Id] = (_personaSelectedAt, GameState.Active?.Session ?? SessionSummary.Empty);

        var incoming = PersonaCatalog.Resolve(Settings.Current.Persona.Id);
        var seen = _personaLastSeen.TryGetValue(incoming.Id, out var last) ? last : default;

        var away = seen.At == default ? (TimeSpan?)null : DateTimeOffset.Now - seen.At;
        var delta = seen.At == default
            ? null
            : TelemetryDelta.Between(seen.Session, GameState.Active?.Session, GameState.Active);

        if (!Personas.Apply(Settings.Current.Persona, away, delta))
        {
            // The name may still have changed underneath an unchanged core, and that is part of
            // the persona block, so the prompt is rebuilt either way.
            Turns.Persona = Personas.RenderBlock(Settings.Current.Llm.PersonalityEnabled);
            return;
        }

        _personaSelectedAt = DateTimeOffset.Now;

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

        _ = Task.Run(async () =>
        {
            // Before a word is spoken, because the first thing a core says is the thing most
            // worth hearing in its own voice.
            await EnsureVoiceForCurrentPersonaAsync().ConfigureAwait(false);

            var line = change.Current.Intro;

            if (change is { Arrival: PersonaArrival.Gap, Gap: { } gap })
            {
                // Authored fallback first, so there is always something to say; the model only
                // ever replaces it.
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

    private void ApplySpeechSettings()
    {
        var speech = ReconcileVoicesWithProvider();
        var provider = TtsProviderCatalog.Selected(speech.Provider);

        // Rebuilt when the provider changes, not merely when there is none yet. A voice id
        // belongs to the provider that issued it, so carrying a client — or a voice list, or a
        // table of sender assignments — across a switch keeps ids that no longer resolve.
        if (!string.Equals(_ttsProviderId, provider.Id, StringComparison.Ordinal))
        {
            // Through the interface, so this stays correct for a provider that needs no
            // disposal. ITtsProvider deliberately does not require IDisposable: it is a text-to-
            // audio seam, and whether an implementation holds an HTTP handle is its own business.
            (_tts as IDisposable)?.Dispose();
            _tts = null;
            _voices = [];
            Cast.Reset();
            _ttsProviderId = provider.Id;

            _tts = provider.Id switch
            {
                SpeechCapability.EdgeId =>
                    new EdgeNeuralTtsProvider(_loggerFactory.CreateLogger<EdgeNeuralTtsProvider>()),

                SpeechCapability.ElevenLabsId => new ElevenLabsTtsProvider(
                    () => Secrets.TryGet(ElevenLabsTtsProvider.KeySecretName, out var key) ? key : null,
                    _loggerFactory.CreateLogger<ElevenLabsTtsProvider>()),

                _ => null,
            };

            if (_tts is not null)
            {
                // Fetched once, in the background. The picker asks synchronously and the list
                // comes over the network, so it is cached rather than requested on open — and
                // not awaited, because a settings change must not wait on a provider being
                // reachable.
                _ = LoadVoicesAsync(_tts);
            }
        }
        else if (_tts is not null && HasKeyFor(provider) != _ttsKeyPresent)
        {
            // A key arriving is the other thing that changes what the provider can tell us, and
            // it does not change the provider. Selecting ElevenLabs before pasting the key
            // fetched an empty list and nothing refetched it, so the picker stayed empty until
            // the app was restarted — with the key sitting right there in the row above it.
            _ = LoadVoicesAsync(_tts);
        }

        _ttsKeyPresent = HasKeyFor(provider);

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
        if (!_transcriber.IsReady)
        {
            // Captured but not transcribable. Said once per utterance rather than silently
            // discarded — the Commander held a key and expects something to happen.
            _logger.LogInformation(
                "Heard {Seconds:0.#}s but no speech model is loaded", utterance.Duration.TotalSeconds);

            const string Cannot = "I heard you, but I have no speech model loaded to understand it.";

            _ = Voice.AnnounceAsync(Cannot);
            Said?.Invoke(Cannot);
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

                if (transcription.IsEmpty)
                {
                    // Distinguished from a failure: the model ran and heard nothing worth
                    // reporting, which a Commander who coughed should not be told is an error.
                    _logger.LogInformation("Nothing intelligible in {Seconds:0.#}s", utterance.Duration.TotalSeconds);
                    return;
                }

                if (_heardAt is { } clock)
                {
                    clock.Value = DateTimeOffset.Now;
                }

                _logger.LogInformation("Heard: {Text}", transcription.Text);
                Heard?.Invoke(transcription.Text);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Could not transcribe an utterance");
            }
        });
    }

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

        Listening.Mode = listening.Mode == ListeningCapability.ToggleMode
            ? ListenMode.Toggle
            : ListenMode.PushToTalk;

        Listening.PreRoll = TimeSpan.FromMilliseconds(listening.PreRollMilliseconds);

        // Rebinding while the key is held would leave the gate open with nothing able to close
        // it — the listening equivalent of a stranded key (architecture.md D4, rule 2).
        _pushToTalk.ForceUp();

        // The model, before the key. A Commander who binds a key and finds d47 captures but
        // cannot understand should see the reason in the status answer, not infer it.
        if (WhisperModels.Find(listening.Model) is { } model && Models.PathOf(model) is { } path)
        {
            _transcriber.Load(path, model.Id, listening.UseGpu);
        }
        else if (WhisperModels.AwaitingDownload(listening.Model, Models) is { } wanted)
        {
            // Selected but not on disk, so fetch it. The selection stays where it is while that
            // happens: it is what the Commander asked for, and a row that drops to none because
            // the file has not arrived yet describes the disk rather than the choice.
            //
            // Fetched rather than offered. The offer was the wrong shape for the one case that
            // matters — a fresh install, where the answer is always yes and the question is a
            // step between the Commander and a working microphone. Choosing a model on the
            // settings row has never asked either; this makes the two paths agree.
            _logger.LogInformation("{Model} is selected but not installed; fetching it", wanted.Id);

            _transcriber.Unload();
            _ = FetchModelAsync(wanted);
        }
        else
        {
            // Unload, not Dispose: this runs on every listening.* change, and the host keeps
            // one transcriber for the life of the process.
            _transcriber.Unload();
        }

        // Deferred to the end, because writing a setting raises Changed, which re-enters this
        // method: doing it above would run the microphone and key work twice on one apply.
        // Nothing deferred here any more. The selection used to be rewritten at the end of this
        // method — cleared to none so it could be re-offered — and a write raises Changed, which
        // re-enters here; the fetch above needs no such thing, because it leaves the setting
        // exactly where the Commander put it.

        var bound = _pushToTalk.Bind(listening.PushToTalkKey);

        if (!bound)
        {
            // No key, no microphone. d47 opening an input device it will never read from is
            // exactly the surprise the unset default exists to avoid. Closed rather than
            // disposed, for the same reason as the transcriber above — the Commander can bind a
            // key later, and that has to reopen the device rather than fail.
            _microphone.Close();
            return;
        }

        _microphone.Open(listening.InputDevice);

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
    /// Which provider <see cref="_tts"/> is. Tracked rather than inferred, because "is it null"
    /// answered "does one need building" only while there was exactly one to build.
    /// </summary>
    private string? _ttsProviderId;

    /// <summary>Whether the selected provider had its key last time speech settings were applied.</summary>
    private bool _ttsKeyPresent;

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
    private IReadOnlyList<VoiceInfo> _voices = [];

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
        if (Turns.Provider is null || !Settings.Current.Llm.PersonalityEnabled)
        {
            return announcement;
        }

        using var budget = new CancellationTokenSource(FlavourBudget);

        // An ambient remark is the core's own, so it gets the persona block and the live game
        // state. The carrier's two roles are other people entirely and get neither.
        var (persona, instruction, state) = announcement switch
        {
            { Key: var key } when key.StartsWith(AmbientCallout.KeyPrefix, StringComparison.Ordinal) =>
            (
                Personas.RenderBlock(personalityEnabled: true),
                "Make one short unprompted remark about where the Commander is right now — you are "
                + $"{AmbientLines.Describe(SituationOf(key))}. Nothing has happened; this is you "
                + "filling a quiet moment in character. One or two sentences. Do not ask a question, "
                + "do not offer help, and do not comment on the Commander's decisions.",
                Turns.LiveGameState?.Invoke()
            ),

            { Voice: VoiceRole.CarrierCaptain or VoiceRole.TowerControl } =>
            (
                // No persona block: this is not the ship's AI speaking. Handing a Guardian core
                // the carrier's lines would put one of them in two places at once, which is the
                // one thing the isolation model cannot survive.
                $"You are {(announcement.Voice == VoiceRole.CarrierCaptain
                    ? "the captain of the Commander's fleet carrier"
                    : "the tower controller aboard the Commander's fleet carrier")}. You are a "
                + "professional, not a character — brief, competent and human. One short sentence. "
                + "Never mention being an AI.",
                $"Say this in your own words, once: \"{announcement.Text}\"",
                (string?)null
            ),

            _ => (null, null, null),
        };

        if (instruction is null)
        {
            return announcement;
        }

        var line = await FlavourTurn.AskAsync(
            Turns.Provider,
            Turns.Model,
            persona,
            instruction,
            state,
            Spend,
            PriceTable.Default,
            _logger,
            budget.Token).ConfigureAwait(false);

        return line is null ? announcement : announcement with { Text = line };
    }

    /// <summary>
    /// Which situation an ambient announcement was about, from its key. Carried on the key
    /// rather than read back off the callout, because a batch may hold more than one and the
    /// callout only remembers the last.
    /// </summary>
    private static AmbientSituation SituationOf(string key) =>
        Enum.TryParse<AmbientSituation>(key[AmbientCallout.KeyPrefix.Length..], ignoreCase: true, out var situation)
            ? situation
            : AmbientSituation.None;

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

            // One at a time, and in order. A previous batch still being spoken holds this one
            // until it finishes rather than talking over it.
            await _speaking.WaitAsync().ConfigureAwait(false);

            try
            {
                foreach (var announcement in lines)
                {
                    await SayAsync(announcement).ConfigureAwait(false);
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
        if (change.Key.StartsWith("llm.", StringComparison.OrdinalIgnoreCase))
        {
            ApplyLlmSettings();
        }
        else if (change.Key.StartsWith("speech.", StringComparison.OrdinalIgnoreCase))
        {
            ApplySpeechSettings();
        }
        else if (change.Key.StartsWith("callouts.", StringComparison.OrdinalIgnoreCase))
        {
            ApplyCalloutSettings(Callouts, Settings.Current);
        }
        else if (change.Key.StartsWith("listening.", StringComparison.OrdinalIgnoreCase))
        {
            ApplyListeningSettings();
        }
        else if (change.Key.StartsWith("persona.", StringComparison.OrdinalIgnoreCase))
        {
            ApplyPersonaSettings();
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

    public void Dispose()
    {
        CoverageRecorder?.Save();

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

        _loggerFactory.Dispose();
        Log.CloseAndFlush();
    }
}
