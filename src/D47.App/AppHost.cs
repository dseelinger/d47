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

/// <summary>The composition root.</summary>
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

        // When each core was last aboard, from previous runs (Phase 35).
        foreach (var (core, at) in viewState.Load().CoresLastAboard)
        {
            _personaLastSeen[core] = (at, null);
        }
    }

    /// <summary>Shows the changelog that shipped inside this build (#50).</summary>
    public Action? ShowChangelog { get; set; }

    /// <summary>Reopens the guided key setup (#50).</summary>
    public Func<Task>? SetUpKeys { get; set; }

    public AppPaths Paths { get; }

    public SerilogVerbosityControl Verbosity { get; }

    /// <summary>The settings surface.</summary>
    public SettingsService Settings { get; }

    public SecretStore Secrets { get; }

    /// <summary>How the panel was left.</summary>
    public ViewStateStore ViewState { get; }

    public GameStateStore GameState { get; }

    public JournalSpine Journal { get; }

    /// <summary>
    /// The last few thousand journal events, kept so the Transcript can show them
    /// (https://github.com/dseelinger/d47/issues/51).
    /// </summary>
    public D47.Core.Journal.JournalLog JournalLog { get; private set; } = new();

    /// <summary>The ~4-10 Hz loop.</summary>
    public TickLoop Tick { get; }

    /// <summary>What the panel is showing.</summary>
    public Panel.PanelViewModel Panel { get; } = new();

    /// <summary>The headset path, once Avalonia has come up.</summary>
    public Headset.VrHost? Vr { get; set; }

    /// <summary>The flat mini panel over the game, once Avalonia has come up (Phase 48).</summary>
    public Windowing.OverlayPanel? Overlay { get; set; }

    /// <summary>Whether Elite is running and in front.</summary>
    public IEliteWindow Elite { get; private set; } = null!;

    /// <summary>What d47 says without being asked (Phase 8).</summary>
    public CalloutEngine Callouts { get; }

    public CapabilityRegistry Capabilities { get; }

    /// <summary>The model-free command path.</summary>
    public KeywordRouter Router { get; }

    /// <summary>The handle on the turn in recording.</summary>
    public TurnCancellation Cancellation { get; }

    /// <summary>Cancel: stop talking, and abandon the turn that is running (#221).</summary>
    public bool CancelNow()
    {
        Audio.Silence();
        return Cancellation.Cancel();
    }

    public UpdateChecker Updates { get; }

    /// <summary>Records what has been exercised by hand, when this process was asked to.</summary>
    public D47.App.Coverage.CoverageRecorder? CoverageRecorder { get; private set; }

    /// <summary>
    /// Retains what crossed the audio boundary in both directions, when this process was asked to
    /// (#164).
    /// </summary>
    public Recording.AudioRecorder? AudioRecorder { get; private set; }

    /// <summary>Writes what the injector sent, step by step, when this process was asked to (#365).</summary>
    public Diagnostics.InputTraceWriter? InputTrace { get; private set; }

    /// <summary>Fetches and installs what <see cref="Updates"/> found.</summary>
    public UpdateInstaller Installer { get; }

    /// <summary>
    /// Gives up this process's claim on being the only d47, so the build replacing it can start before
    /// this one has finished exiting.
    /// </summary>
    public Action? ReleaseSingleInstance { get; set; }

    /// <summary>One turn of conversation, whichever path answers it.</summary>
    public TurnLoop Turns { get; }

    /// <summary>Which Guardian core is aboard, and what it remembers (Phase 11).</summary>
    public PersonaHost Personas { get; }

    /// <summary>Which core flies which ship (Phase 35).</summary>
    public ShipCoreService ShipCores { get; }

    /// <summary>The watch that compares a boarded ship against its plan (Phase 38).</summary>
    public ShipDriftWatch? Drift { get; set; }

    /// <summary>
    /// The session's opening line (Phase 31), held here so a Commander switch can make it due again
    /// (Phase 44, "Welcome back, Commander").
    /// </summary>
    public ContinuityCallout? Continuity { get; set; }

    /// <summary>The Commander's own per-state avatar frames, if they have supplied any.</summary>
    public D47.Core.Interface.AvatarLibrary? Avatars { get; private set; }

    /// <summary>Whether the model is usable right now, and why not when it isn't.</summary>
    public LlmAvailabilityState LlmAvailability { get; }

    /// <summary>Per-turn cost and the running total.</summary>
    public SpendTracker Spend { get; }

    /// <summary>
    /// When this process started, for the one question that needs it: what "this session" means as a
    /// span of the spend ledger (#197).
    /// </summary>
    public DateTimeOffset LaunchedAt { get; } = SystemWallClock.Instance.UtcNow;

    /// <summary>Every charge, kept between runs.</summary>
    public SpendLedger SpendLedger { get; }

    /// <summary>The one queue every audible thing goes through.</summary>
    public AudioArbiter Audio { get; }

    /// <summary>
    /// The cues, beds and ambience currently loaded — the shipped set plus whatever is in
    /// <c>data/audio/</c>.
    /// </summary>
    public CueLibrary Cues { get; private set; }

    /// <summary>What a turn sounds like.</summary>
    public VoicePipeline Voice { get; }

    /// <summary>The gate the microphone feeds.</summary>
    public ListenGate Listening { get; }

    /// <summary>What removes d47's own voice from what the microphone hears (Phase 13).</summary>
    public EchoCanceller Echo { get; }

    /// <summary>Whether an utterance was addressed to d47 at all, in wake-word mode.</summary>
    public WakeWordGate Wake { get; } = new();

    /// <summary>The Commander's Elite bindings.</summary>
    public EliteBinds Binds => _binds.Current;

    /// <summary>The Commander's macros.</summary>
    public MacroStore Macros { get; private set; } = null!;

    /// <summary>The cores the Commander wrote themselves (remediation.md 11, item 9).</summary>
    public OwnPersonaStore OwnPersonas { get; private set; } = null!;

    /// <summary>The Commander's checklist, and the proposals waiting on it (Phase 17).</summary>
    public ChecklistService Checklists { get; private set; } = null!;

    /// <summary>The Commander's timers and alarms (Phase 24).</summary>
    public Timekeeper Timekeeper { get; private set; } = null!;

    /// <summary>The Commander's ship builds, joined to the fleet (Phase 26).</summary>
    public ShipPlanService Ships { get; private set; } = null!;

    /// <summary>Where the builds are kept, for the panel to follow and for a hand edit to reach.</summary>
    public ShipBuildStore ShipBuilds { get; private set; } = null!;

    /// <summary>The Commander's suit and weapon plans, joined to what they are wearing (Phase 27).</summary>
    public D47.Core.Loadout.OnFootPlanService OnFootPlans { get; private set; } = null!;

    /// <summary>Where those are kept, for the panel to follow and for a hand edit to reach.</summary>
    public D47.Core.Loadout.OnFootBuildStore OnFootBuilds { get; private set; } = null!;

    /// <summary>Which engineer to go and get next, read across both plan stores (Phase 28).</summary>
    public D47.Core.Engineers.EngineerPlanService Unlocks { get; private set; } = null!;

    /// <summary>Where the alarms are kept, for the panel to follow and for a hand edit to reach.</summary>
    public AlarmStore Alarms { get; private set; } = null!;

    /// <summary>
    /// Every phrase d47 already answers to, so the macro editor can refuse one that would shadow a
    /// built-in command.
    /// </summary>
    public IReadOnlyList<string> ReservedPhrases { get; private set; } = [];

    /// <summary>What the settings surface needs to walk and assign a HOTAS switch (Phase 21).</summary>
    public Settings.SwitchEditing? SwitchEditing { get; private set; }

    /// <summary>
    /// What the settings surface needs to show and write the Commander's own lore notes (Phase 23).
    /// </summary>
    public Settings.LoreEditing? LoreEditing { get; private set; }

    /// <summary>
    /// What d47 remembers about the Commander, and the clock a fact typed on the panel is stamped with
    /// (Phase 31).
    /// </summary>
    public (MemoryBook Book, Func<DateTimeOffset> Now)? Memories { get; private set; }

    /// <summary>
    /// The standing directions the debrief pass drafts and the Commander adopts, and the clock an
    /// adoption is stamped with (#162).
    /// </summary>
    public (DebriefBook Book, Func<DateTimeOffset> Now)? Debrief { get; private set; }

    /// <summary>What this session has sounded like, in memory and never on disk (#162).</summary>
    public DebriefSession Debriefing { get; } = new();

    /// <summary>
    /// The feedback nobody typed, collected across the session and turned into questions by the pass
    /// (#162).
    /// </summary>
    private readonly List<DebriefSignal> _signals = [];

    private readonly Lock _signalGate = new();

    /// <summary>What the prompt carries for the length of this session (#162).</summary>
    private readonly StandingDirectionsSession _directions = new();

    /// <summary>The Commander's log (Phase 33).</summary>
    public D47.Core.Logbook.LogbookBook? Logbook { get; private set; }

    /// <summary>The Commander's long arcs (Phase 34).</summary>
    public (D47.Core.Goals.GoalBook Book, Action? Backfill)? Goals { get; private set; }

    /// <summary>The stories the Commander flies, and the thing that writes one (Phase 47).</summary>
    public (D47.Core.Adventures.AdventureBook Book, D47.Core.Adventures.AdventureGenerator Generator)? Adventures { get; private set; }

    /// <summary>The galaxy service, for the adventure editor to check a typed place against (Phase 47).</summary>
    public D47.Core.Knowledge.IGalaxyService? Galaxy { get; private set; }

    /// <summary>Where Elite writes its journals, for the adventure catch-up that walks them.</summary>
    public string? JournalDirectory { get; private set; }

    /// <summary>
    /// The stored loadouts, for the row that describes them and the press that rebuilds them (#128).
    /// </summary>
    private LoadoutStore? _loadouts;

    /// <summary>
    /// What this Commander has met and what their transcriber gets wrong (#134), for the settings row
    /// that shows it, the pre-pass that applies it and the lookup that learns it.
    /// </summary>
    private HeardNamesStore? _heardNames;

    /// <summary>The one name a lookup is waiting to be corrected about.</summary>
    private readonly MishearingWatch _mishearings = new();

    /// <summary>The outstanding mishearing, for the capability that asks about it.</summary>
    internal MishearingWatch Mishearings => _mishearings;

    /// <summary>Whether a rescan is already running.</summary>
    private int _rescanning;

    /// <summary>The last plan each planner produced (Phase 37).</summary>
    public D47.Core.Knowledge.RoutePlanBook? Plans { get; private set; }

    /// <summary>
    /// The last commodity answer (Phase 49), so the spoken one and the drawn one are one answer.
    /// </summary>
    public D47.Core.Knowledge.CommodityBoard Commodities { get; private set; } = new();

    /// <summary>
    /// The Community Goal supply search, saved once and run by voice or from the Routing tab (#296).
    /// </summary>
    public D47.Core.Knowledge.CommunityGoalSearch CommunityGoalSearch { get; private set; } = new();

    /// <summary>
    /// What the Community Goal commodity has made or lost, per Commander, folded from the journals
    /// (#296).
    /// </summary>
    public D47.Core.Journal.CommodityLedger CommodityLedger { get; private set; } = new();

    /// <summary>
    /// The last shopping list for a construction site (Phase 50), on the same terms as <see
    /// cref="Commodities"/> and for the same reason.
    /// </summary>
    public D47.Core.Knowledge.SourcingBoard Sourcing { get; private set; } = new();

    /// <summary>What the Commander has told d47 is on their fleet carrier.</summary>
    public D47.Core.Knowledge.CarrierManifest? Carrier { get; private set; }

    /// <summary>Speech models on disk, and the way to fetch one.</summary>
    public IModelStore Models { get; }

    /// <summary>Raised when an utterance has been turned into words, so a surface can run it.</summary>
    public event Action<string>? Heard;

    /// <summary>
    /// Raised with something d47 is saying that no turn produced, so the transcript can carry it too.
    /// </summary>
    public event Action<string>? Said;

    /// <summary>
    /// Raised with something that happened to the conversation rather than something said in it — the
    /// core changing under it being the case this exists for.
    /// </summary>
    public event Action<string>? Noted;

    /// <summary>
    /// <summary> Something the Commander said that no turn is going to write down (change-requests.md
    /// 31).
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
    /// Raised true when a core has been chosen and has not yet worked out what to say, and false when
    /// it has (Phase 12, "Anything that might take a moment says it is working").
    /// </summary>
    public event Action<bool>? PersonaSettling;

    /// <summary>Raised when the Commander's audio folder was re-read and the library replaced.</summary>
    public event Action? AudioReloaded;

    /// <summary>Downloads a model and loads it.</summary>
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
            // Load it now rather than at the next restart — the same "apply every setting without a restart"
            // rule everything else follows (Phase 4).
            ApplyListeningSettings();
        }

        return result;
    }

    private readonly WhisperTranscriber _transcriber;

    private readonly BindsWatch _binds;

    private readonly WasapiMicrophone _microphone;

    /// <summary>When the Commander was last heard and understood.</summary>
    private StrongBox<DateTimeOffset?>? _heardAt;

    private readonly PushToTalkKey _pushToTalk;

    /// <summary>The stick's half of push-to-talk (Phase 53).</summary>
    private readonly D47.Core.Hotas.BoundButton _pushToTalkButton;

    /// <summary>The two of them as one gate.</summary>
    private readonly D47.Core.Hotas.PushToTalkSources _pushToTalkSources;

    /// <summary>
    /// The one thing that presses a key in the game, held only so that shutting down lets go of
    /// whatever it is holding (#206).
    /// </summary>
    private readonly ScancodeInjector _gameInput;

    /// <summary>Cancel's stick button (#221).</summary>
    private readonly D47.Core.Hotas.BoundButton _cancelButton = new();

    /// <summary>
    /// The controllers, for the one question the push-to-talk button has to ask outside the tick:
    /// whether the device list has stopped changing.
    /// </summary>
    public D47.Core.Hotas.IHotasReader? Controllers { get; private set; }

    public string Version { get; }

    /// <summary>Whether GitHub calls this build's Release a pre-release (#92).</summary>
    public D47.Core.Updates.ReleaseChannel Channel { get; internal set; }
        = D47.Core.Updates.ReleaseChannel.Unknown;

    /// <summary>For surfaces that need a logger of their own — the theme manager, so far.</summary>
    public ILoggerFactory Loggers => _loggerFactory;

    /// <summary>Where in-game comms are written down (#264).</summary>
    private Microsoft.Extensions.Logging.ILogger Comms => _comms ??= _loggerFactory.CreateLogger("D47.App.Voice.Comms");

    private Microsoft.Extensions.Logging.ILogger? _comms;

    /// <summary>Set when settings could not be loaded.</summary>
    public string? StartupError { get; }

    public static AppHost Start() => Start(startTicking: true);

    /// <summary>The whole of <see cref="Start"/> except the last line (#79).</summary>
    internal static AppHost Compose() => Start(startTicking: false);

    private static AppHost Start(bool startTicking)
    {
        var paths = AppPaths.ForRunningBuild();
        paths.EnsureCreated();

        // **The version, not the stamp** (#92).
        var version = BuildInfo.Semantic;

        // Logging first, so everything below has somewhere to report a failure.
        var verbosity = new SerilogVerbosityControl();

        Log.Logger = LoggingSetup.Create(paths, verbosity);
        var loggerFactory = new SerilogLoggerFactory(Log.Logger);
        var logger = loggerFactory.CreateLogger<AppHost>();

        // The earliest thing written, before settings, providers or the headset exist.
        logger.LogInformation("d47 {Version} is starting; data folder {Data}", version, paths.Data);

        // Immediately after it, because the thing this catches makes every line below it a description of a
        // build that is not running (bugs.md, 2026-08-23).
        StaleBuildCheck.Report(logger, Environment.ProcessPath ?? string.Empty);

        var store = new SettingsStore(paths, loggerFactory.CreateLogger<SettingsStore>());
        var loaded = new D47Settings();
        string? startupError = null;
        var settingsRefused = false;
        try
        {
            loaded = store.Load();
        }
        catch (SettingsLoadException ex)
        {
            startupError = ex.Message;
            settingsRefused = true;
            logger.LogCritical(ex, "Settings could not be loaded; continuing on defaults");
        }

        // A key this build does not know is kept rather than refused (#368), so the startup message is now
        // the only place a typo shows: without it, a mistyped row would silently do nothing and the
        // protection that unmapped keys used to give would be gone.
        if (store.UnknownKeys.Count > 0)
        {
            var kept = $"Settings at '{paths.SettingsFile}' hold "
                + string.Join(", ", store.UnknownKeys.Select(key => $"'{key}'"))
                + ", which this build does not know; kept as written.";

            startupError = startupError is null ? kept : $"{startupError} {kept}";
        }

        verbosity.Apply(loaded.Logging);

        var secrets = new SecretStore(
            paths,
            new DpapiSecretProtector(),
            loggerFactory.CreateLogger<SecretStore>());

        // The last of the three defences (#368), and the floor under the other two: while the file stands
        // refused, a change is applied in memory and never written, so one toggled row cannot replace a
        // configuration d47 could not read with the defaults it fell back to.
        var settings = new SettingsService(
            store, secrets, loaded, loggerFactory.CreateLogger<SettingsService>(), settingsRefused);

        // From here a level change is live wherever it came from — panel, tool or settings file.
        verbosity.FollowSettings(settings);

        var viewState = new ViewStateStore(paths, loggerFactory.CreateLogger<ViewStateStore>());

        var journalDirectory = ResolveJournalDirectory();
        // Assigned once the bindings have been resolved, below.
        Func<EliteBinds>? bindsRef = null;

        // Sampling history, which is the one derived state that has to outlive a session: the spine tails the
        // newest journal, so a run begun yesterday is otherwise simply gone (Phase 18).
        var sampling = new SamplingStore(
            Path.Combine(paths.Data, "sampling.json"),
            loggerFactory.CreateLogger<SamplingStore>());

        sampling.Load();

        // Systems worth remarking on, in two files with two different characters (Phase 23).
        var lore = new LoreBook(new LoreStore(
            Path.Combine(paths.Data, "lore.json"),
            loggerFactory.CreateLogger<LoreStore>()));

        var loreVisits = new LoreVisits(
            Path.Combine(paths.Data, "lore-visits.json"),
            loggerFactory.CreateLogger<LoreVisits>());

        lore.Store.Poll();
        loreVisits.Load();

        // Lazy, and deliberately so: it reads back through older journal files, and the answer is wanted
        // once, the first time a Commander is seen.
        var recoveredFleets = new Lazy<IReadOnlyDictionary<string, FleetRegistry>>(
            () => FleetBackfill.FromHistory(journalDirectory, loggerFactory.CreateLogger(nameof(FleetBackfill))));

        // And where the carrier is, on the same terms and for the same reason (#406).
        var recoveredCarriers = new Lazy<IReadOnlyDictionary<string, CarrierState>>(
            () => CarrierBackfill.FromHistory(
                journalDirectory, loggerFactory.CreateLogger(nameof(CarrierBackfill))));

        // What every ship the Commander has flown was last seen holding, kept between sessions (#128).
        var loadouts = new LoadoutStore(
            Path.Combine(paths.Data, "loadouts.json"),
            loggerFactory.CreateLogger<LoadoutStore>());

        loadouts.Load();

        // Every place this Commander has met, and what their transcriber gets wrong about them (#134).
        var heardNames = new HeardNamesStore(
            Path.Combine(paths.Data, "heard-names.json"),
            loggerFactory.CreateLogger<HeardNamesStore>());

        heardNames.Load();

        // The same deal for what is *in* those ships, and lazy for the same reason — seeded with the file, so
        // the window's job is catching up on the gap since d47 last ran rather than being the whole memory.
        var recoveredLoadouts = new Lazy<IReadOnlyDictionary<string, ShipLoadouts>>(
            () => LoadoutBackfill.FromHistory(
                journalDirectory,
                loggerFactory.CreateLogger(nameof(LoadoutBackfill)),
                loadouts.All,
                loadouts.FoldedThrough));

        // The same deal for the names, and lazy for the same reason.
        var recoveredNames = new Lazy<IReadOnlyDictionary<string, SpokenNames>>(() =>
        {
            var found = SpokenNameMiner.FromHistory(
                journalDirectory,
                loggerFactory.CreateLogger(nameof(SpokenNameMiner)),
                heardNames.All.ToDictionary(
                    entry => entry.Key, entry => entry.Value.Names, StringComparer.Ordinal),
                heardNames.FoldedThrough);

            // Written straight back, so the expensive first walk happens once rather than at every start
            // until something else prompts a save.
            heardNames.RememberNames(found, DateTimeOffset.Now);

            return found;
        });

        var gameState = new GameStateStore
        {
            Restore = sampling.For,

            // The fleet cannot always be refolded from the newest journal: StoredShips is written only on
            // docking at a shipyard, and a session may contain no such docking — which is how a Commander
            // with eleven ships was shown the one they were sitting in.
            RestoreFleet = fid => recoveredFleets.Value.TryGetValue(fid, out var fleet) ? fleet : null,

            // And Loadout describes one ship, so without this every parked ship's slots read as never seen
            // the moment the Commander swapped out of it.
            RestoreLoadouts = fid => recoveredLoadouts.Value.TryGetValue(fid, out var seen) ? seen : null,

            // And where the carrier was parked, so "where is my carrier" survives a restart.
            RestoreCarrier = fid => recoveredCarriers.Value.TryGetValue(fid, out var carrier) ? carrier : null,

            // And the names, so a failing lookup has something to match against on the very first question of
            // the session rather than after a few jumps.
            RestoreNames = fid => recoveredNames.Value.TryGetValue(fid, out var names) ? names : null,
        };

        // The settings follow whoever the journal says is flying (Phase 44).
        gameState.CommanderChanged += change =>
            settings.UseCommander(change.Current.FrontierId, change.Current.Name);

        // The two state files Elite rewrites in place.
        var status = new GameStatusReader(journalDirectory, loggerFactory.CreateLogger<GameStatusReader>());
        var route = new NavRouteReader(journalDirectory, loggerFactory.CreateLogger<NavRouteReader>());

        // A third of the same kind (Phase 38): what Elite says each module in the ship the Commander is
        // flying actually draws, engineering included.
        var modulePower = new ModulePowerReader(
            journalDirectory, loggerFactory.CreateLogger<ModulePowerReader>());

        // A third file of the same kind, and the markets read out of it (Phase 36).
        var marketBook = new D47.Core.Knowledge.MarketBook(
            Path.Combine(paths.Data, "markets.json"),
            loggerFactory.CreateLogger<D47.Core.Knowledge.MarketBook>());

        marketBook.Load();

        // And a fourth (Phase 37).
        var planBook = new D47.Core.Knowledge.RoutePlanBook(
            Path.Combine(paths.Data, "route-plans.json"),
            loggerFactory.CreateLogger<D47.Core.Knowledge.RoutePlanBook>());

        planBook.Load();

        // In memory rather than loaded, unlike the plan book above: a commodity price is the thing here that
        // ages fastest, so one restored from disk would look current because it was saved rather than because
        // it is true (Phase 49).
        var commodityBoard = new D47.Core.Knowledge.CommodityBoard();
        var sourcingBoard = new D47.Core.Knowledge.SourcingBoard();

        // The Community Goal supply search, saved once (#296), and the ledger of what its commodity has made
        // or lost.
        var communityGoalSearch = new D47.Core.Knowledge.CommunityGoalSearch
        {
            // Where the search measures from (#331): the live goal's own system, because a supply run is buy
            // near where you sell and that answer holds for the life of the goal.
            Board = () => gameState.Active?.CommunityGoals,
        };
        var commodityLedger = new D47.Core.Journal.CommodityLedger();

        // Where a nearest-first commodity search last sent the Commander (#325), so "set a course" and "set a
        // course and take us out" have something to act on without them repeating a system name they just
        // heard.
        var lastFoundSystem = new D47.Core.Conversation.LastFoundSystem();

        commodityLedger.FoldHistory(
            journalDirectory,
            DateTimeOffset.Now - D47.Core.Journal.CommodityLedger.Lookback,
            loggerFactory.CreateLogger<D47.Core.Journal.CommodityLedger>());

        // On disk, unlike the two boards: a carrier figure is the Commander's own statement rather than a
        // price, and it is dated wherever it is used (Phase 50).
        var carrierManifest = new D47.Core.Knowledge.CarrierManifest(
            Path.Combine(paths.Data, "carrier.json"),
            loggerFactory.CreateLogger<D47.Core.Knowledge.CarrierManifest>());

        var markets = new D47.Core.Knowledge.MarketReader(
            journalDirectory,
            marketBook,
            loggerFactory.CreateLogger<D47.Core.Knowledge.MarketReader>());

        // After the status reader, because the spine stamps a surface position onto events that carry none —
        // organic sampling is the whole reason (Phase 18).
        var journal = new JournalSpine(journalDirectory, gameState, loggerFactory, () => status.Current);

        // The Commander's checklist and the proposals waiting on it, in two files beside the executable
        // (Phase 17).
        var checklists = new ChecklistService(
            new ChecklistStore(
                Path.Combine(paths.Data, "checklist.json"),
                loggerFactory.CreateLogger<ChecklistStore>()),
            new ChecklistProposalStore(
                Path.Combine(paths.Data, "checklist-proposals.json"),
                loggerFactory.CreateLogger<ChecklistProposalStore>()),
            () => gameState.Active,

            // The chosen filter outlives the session, which is what was asked for on 2026-08-23.
            view => viewState.Save(viewState.Load() with
            {
                ChecklistFilter = view.Filter,
                ChecklistPartialGrades = view.IncludePartialGrades,
            }));

        // The engineer filter is a question about where the ship is, so the list is re-read when it moves
        // (#93).
        checklists.Follow(gameState);

        checklists.Restore(
            new ChecklistView(
                viewState.Load().ChecklistFilter ?? ChecklistService.Everything,
                viewState.Load().ChecklistPartialGrades));

        // What d47 remembers about the Commander (Phase 31).
        var memories = new MemoryStore(
            Path.Combine(paths.Data, "memories.json"),
            loggerFactory.CreateLogger<MemoryStore>());

        memories.Poll();

        var memoryBook = new MemoryBook(
            memories,
            () => gameState.Active?.Identity.FrontierId,
            () => MemorySituation.Of(gameState.Active, status.Current));

        // The only thing in the phase that writes a memory nobody asked for, and the only producer of the
        // observed tier — without it that tier would be an enum member reachable by nothing.
        var memoryObserver = new MemoryObserver(memoryBook);

        // The standing directions the debrief pass drafts and the Commander adopts (#162).
        var directions = new StandingDirectionsStore(
            Path.Combine(paths.Data, DebriefWriteFence.FileName),
            loggerFactory.CreateLogger<StandingDirectionsStore>());

        directions.Poll();

        var debriefBook = new DebriefBook(directions, () => gameState.Active?.Identity.FrontierId);

        // The withdrawn Habits feature's data file, deleted on every start with no repair flag (#84).
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
            // A file that cannot be deleted is a file nothing reads.
            logger.LogWarning(ex, "Could not delete the retired {Path}", retiredHabits);
        }

        // The Commander's long arcs (Phase 34).
        var goals = new D47.Core.Goals.GoalStore(
            Path.Combine(paths.Data, "goals.json"),
            loggerFactory.CreateLogger<D47.Core.Goals.GoalStore>());

        goals.Poll();

        // The stories the Commander flies (Phase 47).
        var adventureStore = new D47.Core.Adventures.AdventureStore(
            Path.Combine(paths.Data, "adventures.json"),
            loggerFactory.CreateLogger<D47.Core.Adventures.AdventureStore>());

        adventureStore.Poll();

        var adventureBook = new D47.Core.Adventures.AdventureBook(
            adventureStore, loggerFactory.CreateLogger<D47.Core.Adventures.AdventureBook>());

        // A hand edit, a Begin or an Abandon all arrive here; the book keeps what it can and asks for a walk
        // over the files when a stamp moved, which the tick below grants.
        adventureStore.Changed += adventureBook.Reconcile;

        var goalMiner = new D47.Core.Goals.GoalMiner(
            loggerFactory.CreateLogger<D47.Core.Goals.GoalMiner>());

        var backfilling = 0;

        // Off the UI thread, because the pass is seconds long over hundreds of megabytes, and guarded because
        // the button is a press and a Commander who sees nothing happen presses it again.
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

        // Assigned once the ship and on-foot plans exist, below.
        D47.Core.Engineers.EngineerPlanService? unlocksRef = null;

        // Reads the same holder the callouts do, because the engineers arc delegates its "what do I do about
        // this today" to the unlock solver rather than growing a worse one.
        var goalBook = new D47.Core.Goals.GoalBook(
            goals,
            () => gameState.Active?.Identity.FrontierId,
            () => gameState.Active,
            checklists,
            () => unlocksRef);

        var callouts = BuildCallouts(
            loaded,
            loggerFactory,
            checklists,
            lore,
            loreVisits,
            memoryBook,
            adventureBook,
            viewState,
            commodityLedger,
            communityGoalSearch,
            gameState);

        // Acting on the game without being asked (Phase 10, item 2).
        var autonomous = new AutonomousActionRunner(loggerFactory.CreateLogger<AutonomousActionRunner>())
            .Add(new HonkOnArrival(
                () => settings.Current.Actions.HonkOnArrival,
                () => bindsRef!()));

        // The ~4-10 Hz loop.
        var tick = new TickLoop(loggerFactory.CreateLogger<TickLoop>());

        // This tick's journal events, for the subscribers registered after the host exists and therefore too
        // late to be inside the closure below.
        IReadOnlyList<JournalEvent> arrived = [];

        // Captured rather than reached through the host, for the same reason `arrived` is: this closure is
        // built before the instance exists (#51).
        var journalLog = new D47.Core.Journal.JournalLog();

        // The journal's own witness that the galaxy map is showing (#365).
        var tracingInput = Diagnostics.InputTraceWriter.Enabled;
        string? musicTrack = null;

        tick.Add("journal", context =>
        {
            // The first tick is the replay of the backlog, and the switch signal has to know that (Phase 44):
            // a Commander change met during the replay is history, not a login.
            var events = journal.Poll(priming: context.IsFirst);

            arrived = events;

            if (tracingInput
                && events.LastOrDefault(journalEvent => journalEvent.Kind == "Music") is { } playing)
            {
                musicTrack = playing.String("MusicTrack");
            }

            // Kept for the Journal page to read (#51).
            journalLog.Add(events);
            status.Poll();
            route.Poll();

            // Elite rewrites this one on outfitting and on a module being switched off, so it is read on the
            // same terms as the other two: only when its write time moves.
            modulePower.Poll();

            // The commodity board the Commander is standing in front of, if they have opened one (Phase 36).
            markets.Poll(gameState.Active?.Location.StarPos);

            // A sold ship's list goes with the ship (change-requests.md 27).
            if (!context.IsFirst)
            {
                foreach (var journalEvent in events)
                {
                    if (journalEvent.Kind == "ShipyardSell"
                        && journalEvent.Int("SellShipID") is { } sold)
                    {
                        // What it cleared goes onto the list's own news queue, so the checklist callout
                        // speaks it and the Commander can switch that off like anything else it says.
                        checklists.ShipSold(sold);
                    }
                }
            }

            // Before the callouts and inside this subscriber, so a verdict recomputed from this tick's events
            // is announced on this tick rather than the next.
            checklists.Poll(announce: !context.IsFirst);

            // Before the callouts, so the sale callout reads a total that includes the sale it is announcing
            // (#296).
            commodityLedger.Apply(events);

            var calloutContext = new CalloutContext(
                context.Now,
                IsPriming: context.IsFirst,
                gameState.Active,
                status.Current,
                route.Current,
                events);

            callouts.Tick(calloutContext);
            autonomous.Tick(calloutContext);

            // Written only when a sample actually landed, rather than every tick: this runs ten times a
            // second and the file changes a few times an hour.
            if (events.Any(journalEvent => journalEvent.Kind == "ScanOrganic"))
            {
                sampling.Save(gameState.All);
            }

            // The same cadence and the same reasoning for the ships (#128).
            if (events.LastOrDefault(ShipLoadouts.MayChange) is { } changed)
            {
                // Stamped with that event's own time rather than with the clock, so the watermark the next
                // catch-up walks back to means what it says even when this tick is replaying a backlog from
                // yesterday.
                loadouts.Save(gameState.All, changed.Timestamp);
            }

            // The Commander's lore notes are hand-editable, so they are polled like the checklist is; the
            // remark stamps are written only when one was actually made, which is at most a handful of times
            // a day.
            lore.Store.Poll();

            if (loreVisits.Dirty)
            {
                loreVisits.Save();
            }
        });

        // A story under way is caught up before the priming tick replays the current session (Phase 47): the
        // walk is bounded to the files since the earliest acceptance, so with nothing under way it reads
        // nothing, and a beat that fired while d47 was closed is in the standing before the first live event
        // arrives.
        adventureBook.CatchUp(D47.Core.Adventures.AdventureBook.FilesToWalk(journalDirectory, adventureBook.EarliestAcceptance()));

        // Primed synchronously before anything reads game state, so a journal already on disk when d47 starts
        // is answered correctly, backlog and all — and so the panel's first status is not a race against the
        // first timer tick.
        tick.Tick(DateTimeOffset.Now);

        logger.LogInformation(
            "Journal folder {Directory}; tailing {File}",
            journalDirectory,
            journal.CurrentFile ?? "(none found)");

        // Availability and spend exist before the registry because capabilities report on them; the provider
        // itself is built afterwards, from settings, by ApplyLlmSettings.
        var llmAvailability = new LlmAvailabilityState(providerConfigured: false);

        // The history behind the running totals.
        var spendLedger = new SpendLedger(
            paths.SpendFile,
            SystemWallClock.Instance,
            loggerFactory.CreateLogger<SpendLedger>());

        var spend = new SpendTracker(spendLedger);

        // Clocks, timers and alarms (Phase 24).
        var alarms = new AlarmStore(
            Path.Combine(paths.Data, "alarms.json"),
            loggerFactory.CreateLogger<AlarmStore>());

        alarms.Poll();

        var timekeeper = new Timekeeper(alarms);

        // The Commander's ship builds (Phase 26).
        var shipBuilds = new ShipBuildStore(
            Path.Combine(paths.Data, "ships.json"),
            loggerFactory.CreateLogger<ShipBuildStore>());

        shipBuilds.Poll();

        // The fleet joined to the builds.
        var shipPlans = new ShipPlanService(shipBuilds, checklists, () => gameState.Active);

        // And the one thing that watches the two for drifting apart (Phase 38).
        var drift = new ShipDriftWatch(shipPlans, checklists);

        // Which core flies which ship (Phase 35).
        var shipCoreStore = new ShipCoreStore(
            Path.Combine(paths.Data, "ship-cores.json"),
            loggerFactory.CreateLogger<ShipCoreStore>());

        shipCoreStore.Poll();

        var shipCores = new ShipCoreService(shipCoreStore, () => gameState.Active);

        // And the same arrangement on foot (Phase 27).
        var onFootBuilds = new D47.Core.Loadout.OnFootBuildStore(
            Path.Combine(paths.Data, "on-foot.json"),
            loggerFactory.CreateLogger<D47.Core.Loadout.OnFootBuildStore>());

        onFootBuilds.Poll();

        var onFootPlans = new D47.Core.Loadout.OnFootPlanService(
            onFootBuilds, checklists, () => gameState.Active);

        // The engineer solver (Phase 28).
        var unlocks = new D47.Core.Engineers.EngineerPlanService(
            shipBuilds, onFootBuilds, checklists, () => gameState.Active);

        // The holder declared before the callouts, filled in now.
        unlocksRef = unlocks;

        // Late-bound, because several things built here have to read something that does not exist until the
        // host does — the voice list, the headset report, and now the cue library, which is replaced whenever
        // the Commander drops a file into data/audio.
        AppHost? self = null;

        // A session, written up (Phase 33).
        var logbook = new D47.Core.Logbook.LogbookBook(
            new D47.Core.Logbook.LogFolder(
                Path.Combine(paths.Data, D47.Core.Logbook.LogFolder.FolderName),
                loggerFactory.CreateLogger<D47.Core.Logbook.LogFolder>()),
            new D47.Core.Logbook.LogDigestBuilder(loggerFactory.CreateLogger<D47.Core.Logbook.LogDigestBuilder>()),
            new D47.Core.Logbook.LogWriter(loggerFactory.CreateLogger<D47.Core.Logbook.LogWriter>()),
            () => settings.Current.Logbook,

            // Read at the moment a log is asked for rather than captured now, because a Commander who has
            // been flying since launch has journals here that did not exist then.
            () => JournalsOnDisk(journalDirectory, logger),
            () => DateTimeOffset.Now,

            // The provider, the model and the persona as they are at this instant.
            () => new D47.Core.Logbook.LogbookContext
            {
                Provider = self?.Turns.Provider,
                Model = self?.Turns.Model,
                PersonalityEnabled = settings.Current.Llm.PersonalityEnabled,
                Persona = self?.Personas.RenderBlock(settings.Current.Llm.PersonalityEnabled),

                // Both halves.
                AboutMe = CommanderStory.Compose(
                    settings.Current.Llm.CharacterSheet, settings.Current.Llm.AboutMe, withStory: true),
                Ledger = spendLedger,
                Version = version,
            },
            loggerFactory.CreateLogger<D47.Core.Logbook.LogbookBook>());

        // Audio comes up before the registry because the speech capability's settings rows read the bed names
        // and the device list from it.
        var drops = new FolderAudioSource(paths.Audio, loggerFactory.CreateLogger<FolderAudioSource>());
        var cueLogger = loggerFactory.CreateLogger<CueLibrary>();
        var cues = CueLibrary.Load(cueLogger, new EmbeddedCueSource(typeof(CueLibrary).Assembly), drops);

        var audioSink = new WasapiAudioSink(loggerFactory.CreateLogger<WasapiAudioSink>());
        var audio = new AudioArbiter(audioSink, loggerFactory.CreateLogger<AudioArbiter>()).Start();
        var voice = new VoicePipeline(audio, () => self!.Cues, loggerFactory)
        {
            // What a voice is called, for the log line that says who spoke (remediation.md 10, item 9).
            VoiceName = id => id is { Length: > 0 } ? self?.VoiceNameFor(id) : null,
        };

        // The loop settles back to idle when the arbiter goes quiet rather than when the turn returns,
        // because the turn returns while the reply is still being spoken.
        audio.ActivityChanged += voice.Settle;

        // Off unless D47_RECORD_AUDIO=1 (#164).
        var recording = Recording.AudioRecorder.Create(
            paths,
            () => DateTimeOffset.Now,
            loggerFactory.CreateLogger<Recording.AudioRecorder>());

        if (recording is not null)
        {
            recording.Watch(audio, audioSink.ReferenceTap);

            // What each sentence was rendered by — the provider, the voice and, for the local voice, the
            // phonemes.
            voice.Synthesised = recording.Noted;
        }

        // A track ending is how the next one is asked for.
        audio.MusicFinished += () => self?.PlayNextTrack();

        try
        {
            audioSink.Open(loaded.Speech.OutputDevice);
        }
        catch (Exception ex)
        {
            // No audio output is a capability being off, not a startup failure. d47 stays fully usable in
            // text (Phase 3, "Capabilities as state, not guard").
            logger.LogError(ex, "No audio output could be opened; D47 will be silent");
        }

        // Listening.
        var models = new HttpModelStore(paths, loggerFactory.CreateLogger<HttpModelStore>());
        var transcriber = new WhisperTranscriber(loggerFactory.CreateLogger<WhisperTranscriber>());
        var gate = new ListenGate(WasapiMicrophone.SampleRate, loggerFactory.CreateLogger<ListenGate>());

        // Between the microphone and the gate, consuming the arbiter's render reference tap rather than a
        // loopback capture.
        var echo = new EchoCanceller(
            gate,
            audioSink.ReferenceTap,
            WasapiMicrophone.SampleRate,
            loggerFactory.CreateLogger<EchoCanceller>());

        // Whether d47 is currently audible, which the gate needs only when nothing is cancelling it:
        // uncancelled, a hands-free mode with speakers is a loop where d47 hears itself, transcribes itself
        // and answers itself.
        audio.ActivityChanged += activity => gate.FarEndActive = activity.Channel is not null;

        var microphone = new WasapiMicrophone(echo, loggerFactory.CreateLogger<WasapiMicrophone>());
        var pushToTalk = new PushToTalkKey(loggerFactory.CreateLogger<PushToTalkKey>());

        // The stick's half of push-to-talk, and the two of them as one gate (Phase 53).
        var pushToTalkButton = new D47.Core.Hotas.BoundButton();
        var sources = new D47.Core.Hotas.PushToTalkSources();

        // The only thing that presses a key in the game.
        var eliteWindow = new EliteWindow(loggerFactory.CreateLogger<EliteWindow>());

        // Off unless D47_TRACE_INPUT=1 or --trace-input (#365).
        var inputTrace = Diagnostics.InputTraceWriter.Create(
            paths,
            () => DateTimeOffset.Now,
            () => status.Current,
            () => musicTrack,
            () => new Diagnostics.EliteWindowCapture(
                () => eliteWindow.Handle,
                Diagnostics.InputTraceWriter.StillWidth,
                loggerFactory.CreateLogger<Diagnostics.EliteWindowCapture>()),
            loggerFactory.CreateLogger<Diagnostics.InputTraceWriter>());

        // With the status alongside the window (#242): running and in front are not the same as in the game,
        // and the injector is the one place the difference is enforced.
        var gameInput = new ScancodeInjector(
            eliteWindow, loggerFactory.CreateLogger<ScancodeInjector>(), () => status.Current, inputTrace);

        // Declared here and assigned inside the registry build below, so the capabilities and the prompt's
        // game-state block are looking at one surface rather than two that could disagree about what is
        // reachable.
        ActionSurface actionSurface;

        // Read at startup and re-read when Elite rewrites it.
        var binds = new BindsWatch(
            BindsResolver.DefaultBindingsDirectory(),
            EliteInstallations(),
            loggerFactory.CreateLogger<AppHost>());

        bindsRef = () => binds.Current;

        // The Commander's own macros, beside the executable like everything else d47 writes.
        var macros = new MacroStore(
            Path.Combine(paths.Data, "macros.json"), loggerFactory.CreateLogger<MacroStore>());

        // What d47 last offered to put on the clipboard.
        var clipboardOffer = new D47.Core.Conversation.ClipboardOffer();

        // The Commander's HOTAS switches, in the same shape and beside the same executable (Phase 21).
        var switches = new SwitchStore(
            Path.Combine(paths.Data, "switches.json"), loggerFactory.CreateLogger<SwitchStore>());

        var controllers = new HotasControllers(loggerFactory.CreateLogger<HotasControllers>());
        var reconciler = new SwitchReconciler(loggerFactory.CreateLogger<SwitchReconciler>());

        var cancellation = new TurnCancellation(loggerFactory.CreateLogger<TurnCancellation>());

        // The cores the Commander wrote, beside the executable and polled like the macros above
        // (remediation.md 11, item 9).
        var ownPersonas = new OwnPersonaStore(
            Path.Combine(paths.Data, "personas.json"),
            loggerFactory.CreateLogger<OwnPersonaStore>());

        ownPersonas.Poll();

        PersonaCatalog.Own = () => [.. ownPersonas.Cores.Select(core => core.AsPersona())];

        // Built before the registry, because the persona capability declares settings rows from it and which
        // rows exist has to be settled before registration — descriptors are registered once and never
        // mutated.
        var personas = new PersonaHost(
            PersonaCatalog.Resolve(settings.Current.Persona.Id),
            new ViewStateIntroductions(viewState));

        // The help capability answers from the registry it is itself registered in, so the accessor is filled
        // in immediately after Build.
        CapabilityRegistry? built = null;

        // When the Commander was last understood.
        var heardAt = new StrongBox<DateTimeOffset?>(null);

        // Off unless D47_COVERAGE=1.
        var coverage = D47.App.Coverage.CoverageRecorder.Create(
            paths,
            () => DateTimeOffset.Now,
            loggerFactory.CreateLogger<D47.App.Coverage.CoverageRecorder>());

        var galaxy = new D47.Knowledge.SpanshGalaxyService(
            loggerFactory.CreateLogger<D47.Knowledge.SpanshGalaxyService>());

        var routePlanner = new D47.Knowledge.SpanshRouteService(
            loggerFactory.CreateLogger<D47.Knowledge.SpanshRouteService>());

        // Where a named commodity's prices come from since #350: a live EDDN-fed index, applied as each
        // message arrives, rather than the station index's market figures — which ran hours and sometimes
        // weeks behind during a Community Goal rush and sent the Commander to a station that had already been
        // emptied.
        var commodities = new D47.Knowledge.ArdentCommodityService(
            loggerFactory.CreateLogger<D47.Knowledge.ArdentCommodityService>());

        // d47's own trade planner (Phase 36).
        var tradePlanner = new D47.Knowledge.SpanshTradePlanService(
            loggerFactory.CreateLogger<D47.Knowledge.SpanshTradePlanService>(),
            commodities,
            marketBook);

        // The key is read on every call rather than captured here, so pasting one in or clearing it takes
        // effect without a restart — the same rule the galaxy service's setting follows.
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

                    // The local voice, and what fetching it would cost (Phase 59).
                    LocalVoiceState = () => self?.LocalVoiceState() ?? "Not available.",
                    DownloadLocalVoice = () => self is null ? null : self.DownloadLocalVoice,

                    // Which of the eight builds is actually on disk, and the swap onto another (#139).
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

                    // Late-bound like the headset surface below, and for the same reason: the list is fetched
                    // from the provider over the network after this point.
                    Voices = group => self?.VoiceIds(group) ?? [],
                    VoiceLabel = (group, id) => self?.VoiceLabelFor(group, id) ?? id,
                    VoiceGender = (group, id) => self?.VoiceGenderFor(group, id),
                    WhyNoVoices = group => self?.WhyNoVoices(group),
                    SpeechSpend = () => self?.SpeechSpend,

                    // Asked of the slot's own provider, not the ship's.
                    HasKey = group => self is not { } host
                                      || host.HasKeyFor(TtsProviderCatalog.Selected(
                                          VoiceGroups.ProviderFor(settings.Current.Speech, group))),
                    Audition = (voiceId, role, token) => self is { } host
                        ? host.AuditionVoiceAsync(voiceId, role, token)
                        : Task.CompletedTask,

                    // Late-bound like the two above, because the check is a network call made by a host that
                    // does not exist yet at this point in composition.
                    VerifyKey = (provider, token) => self is { } host
                        ? host.VerifySpeechKeyAsync(provider, token)
                        : Task.FromResult(SecretCheck.Unreachable("D47 is still starting up.")),
                },
                new ShipsCapability.ShipsSurface
                {
                    // Read at draw time, so the row says what is stored now rather than what was stored when
                    // the surface was assembled — including straight after a rescan.
                    Remembered = () => self?.RememberedShips() ?? "Nothing is remembered yet.",

                    // The delegate answers a press rather than being one, for the reason
                    // SpeechCapability.DownloadLocalVoice records: rows are built before `self` exists, so a
                    // press asked for here would be null and stay null.
                    Rescan = () => self is null ? null : self.RescanLoadoutsAsync,
                },

                // How a misheard proper noun is recovered (#134).
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

                    // The demonstration beats any assertion about device state: if words arrived recently,
                    // hearing works, and that is the answer.
                    SinceHeard = () => heardAt.Value is { } heard ? DateTimeOffset.Now - heard : null,

                    TranscriberState = () => (
                        transcriber.IsReady,
                        transcriber.Model,
                        transcriber.Unavailable ?? "No speech model is selected."),
                    Binds = () => binds.Current,
                    InstalledModels = () => models.Installed(),

                    // Read at draw time, so the row shows what has been learned rather than what had been
                    // when the surface was assembled (#134).
                    Corrections = () => self?.LearnedCorrections() ?? "Nothing yet.",
                    ForgetCorrections = () => self?.ForgetCorrections(),

                    // What the gate policy is actually doing, which is the question a Commander running hands
                    // free is asking when they ask this one (Phase 13).
                    Microphone = () => gate.State,
                    EchoState = () => (echo.IsActive, echo.Unavailable),
                    WakeWords = () => self?.Wake.Phrases ?? [],

                    // So the status says "[" where the settings row already does.
                    KeyLabel = Input.Gestures.Describe,
                },
                // Late-bound for the same reason spoken help's registry accessor is: the headset path needs a
                // dispatcher and a widget tree, so it does not exist yet.
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

                    // The plotted route rather than Status.json's Destination (#344).
                    SystemTargeted = () =>
                        RouteProgress.For(route.Current, gameState.Active?.Location.StarSystem)
                            .JumpsRemaining > 0,

                    // Not awaited (#158).
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

                    // Where the attempt says how far it got, on every exit including a cancelled turn (#365).
                    Log = logger,
                },
                macros,
                personas,
                checklists,
                () => (self?.Cues ?? cues).DescribeDrops(),
                coverage is null ? null : () => coverage.Report().Summary,

                // Constructed unconditionally and gated by its setting rather than by whether it exists: the
                // row that turns it on has to work without a restart, and a service built only when the
                // setting was already true could not (Phase 4).
                galaxy,
                routePlanner,
                tradePlanner,
                communityGoals,
                () => DateTimeOffset.Now,

                // Late-bound like the surfaces above: the check is a real network call and the host that
                // makes it does not exist yet at this point in composition.
                (provider, token) => self is { } host
                    ? host.VerifyLanguageModelKeyAsync(provider, token)
                    : Task.FromResult(SecretCheck.Unreachable("D47 is still starting up.")),

                // Where the Commander is standing, which only Status.json knows — ScanOrganic carries no
                // position at all (Phase 18).
                () => status.Current,

                // The same window object the injector asks about before every key.
                () => Task.Run(eliteWindow.Raise),

                new SwitchSurface
                {
                    Mappings = () => switches.Switches,
                    States = () => reconciler.States,
                    Unavailable = () => controllers.Unavailable,
                    Problems = () => switches.Problems,
                },
                lore,

                // The Commander's timers and alarms (Phase 24).
                timekeeper,

                // How to present an instant locally.
                () => TimeZoneInfo.Local,
                shipPlans,

                // And the suit and weapon plans beside them (Phase 27).
                onFootPlans,

                // Which engineer to go and get next, read across both of them (Phase 28).
                unlocks,

                // The endpoint half of web search, for the egress row.
                () => self?.SearchReachesTheWeb ?? true,

                // What the endpoint said it serves (Phase 29).
                () => self?.EndpointModelIds ?? [],

                // What d47 remembers about the Commander (Phase 31).
                memoryBook,

                // Turning a session into something worth keeping (Phase 33).
                logbook,

                // The campaigns that outlive a checklist (Phase 34).
                goalBook,

                // What the "read my journals" button does for the arcs' ages.
                () => BackfillGoals,

                // Which core flies which ship (Phase 35).
                shipCores,

                // And where a plan goes once it is made (Phase 37), so the spoken route and the drawn one are
                // one answer rather than two.
                planBook,

                // What d47 last offered to copy (asked for 2026-08-21).
                clipboardOffer,

                // The three waits the compound ship commands need (Phase 52).
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

                    // Longer than the others on purpose: the pad lift and the mail slot take real seconds,
                    // and a launch reported as failed because d47 stopped watching too early is the same lie
                    // as one reported as succeeded.
                    AwaitUndocked = token => AwaitStatus(
                        status,
                        current => !current.Has(Core.Journal.StatusFlags.Docked),
                        TimeSpan.FromSeconds(30),
                        "undocked",
                        logger,
                        token),

                    NextStatus = token => NextStatus(status, token),

                    // The boost loop's pacing.
                    Now = () => DateTimeOffset.Now,
                },

                // Where a commodity answer is posted on its way out (Phase 49), so the Routing tab draws what
                // was just said rather than asking again.
                commodityBoard,

                // What the Commander says is aboard their carrier, and where a build's shopping list is
                // posted on its way out (Phase 50).
                carrierManifest,
                sourcingBoard,

                // What this build is, for the About area (#50).
                new AboutSurface
                {
                    Build = BuildInfo.Full,

                    // Asked each time the row is drawn, not captured: the answer arrives over the network
                    // after this page exists, and it changes again if the release is promoted while d47 is
                    // running (#92).
                    Channel = () => self?.Channel ?? D47.Core.Updates.ReleaseChannel.Unknown,

                    // Late-bound through the host like the speech surface's three, because the two that open
                    // a window need an owner and nothing here has one yet.
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

                    // Moved off the foot of the Settings tab and onto the row that names the folder
                    // (2026-09-01).
                    OpenDataFolder = () => System.Diagnostics.Process.Start(
                        new System.Diagnostics.ProcessStartInfo(paths.Data) { UseShellExecute = true }),
                },

                // What the audio recorder has kept (#164), so the privacy capability can carry the row that
                // empties it.
                recording?.Log,

                // **Withdrawal, and it now reaches the store rather than only this machine** (#167).
                async (_, cancel) =>
                {
                    var forgotten = await Donation.DonationDispatch
                        .For(paths, static () => DonationSettings.Address, loggerFactory)
                        .ForgetAsync(cancel);

                    return forgotten.Receipt is { } receipt
                        ? $"{forgotten.Outcome.Said} A record of it is in {receipt}."
                        : forgotten.Outcome.Said;
                },

                // What the debrief drafted and what the Commander took (#162).
                debriefBook,

                // What the Community Goal commodity has made or lost, and which commodity that is (#296), for
                // the earnings question on the community goals capability.
                commodityLedger,
                communityGoalSearch,

                // Where a nearest-first commodity search last sent the Commander (#325), threaded to the
                // galaxy search that writes it and the ship command that reads it.
                lastFoundSystem));

        built = capabilities;

        // The one late-bound edge in the composition: descriptors declare the settings rows and some
        // descriptors read settings, so the row table is supplied once the registry exists.
        settings.Bind(capabilities);

        logger.LogInformation(
            "Registered {Count} capabilities exposing {ToolCount} tools",
            capabilities.All.Count,
            capabilities.ToolNames.Count());

        var updates = new UpdateChecker(loggerFactory.CreateLogger<UpdateChecker>());
        var installer = new UpdateInstaller(paths, loggerFactory.CreateLogger<UpdateInstaller>());

        // The moment the retired build is no longer the running image is startup, so this is the first chance
        // to delete what a previous update left behind.
        if (Environment.ProcessPath is { } runningExecutable)
        {
            installer.CleanUpRetired(runningExecutable);
        }

        // The router's dynamic vocabulary: the Commander's own macro names, and — while one is standing — the
        // phrases that take up a clipboard offer.
        IEnumerable<DynamicCommand> OtherDynamicCommands() =>
            clipboardOffer.Phrases()

                // And "set course for my carrier", which is an instruction rather than a topic and has to
                // out-match the "my carrier" keyword that was answering it with a position report
                // (change-requests.md 31).
                .Concat(CarrierCourse.Phrases(() => gameState.Active?.Carrier))

                // And "what is Conductive Polymers for", one phrase per material something planned actually
                // wants (change-requests.md 37).
                .Concat(GapCapability.Phrases(shipPlans, onFootPlans, () => gameState.Active))

                // And "community goal search" (#296): the INARA query with its knobs baked, pointed at
                // find_nearest_station, plus "refresh" while the page that draws it is up.
                .Concat(communityGoalSearch.Phrases())

                // And "set a course" (#325): plot_course pointed at whatever a nearest-first commodity search
                // last found. "Set a course and take us out" is not here — it is a fixed phrase on
                // ship_command's own compound tool, since it runs two tools in order rather than baking one
                // argument.
                .Concat(CommunityGoalCourse.Phrases(lastFoundSystem));

        var router = new KeywordRouter(
            capabilities, () => MacroCapability.Phrases(macros).Concat(OtherDynamicCommands()));

        var turns = new TurnLoop(
            capabilities,
            router,
            llmAvailability,
            spend,
            PriceTable.Default,
            loggerFactory.CreateLogger<TurnLoop>(),
            settings: settings)
        {
            // Asked once per turn rather than assigned, so the state the model sees is the state as of the
            // moment the prompt was built — not as of whenever something last pushed it in.
            ToolContext = () => actionSurface.Context,
            ActionsEnabled = () => settings.Current.Actions.Keyboard,
            WebSearchEnabled = () => settings.Current.Llm.WebSearch,

            // A proposal the Commander has not answered, stated by the store rather than by the model
            // (remediation.md 10, item 10).
            Standing = checklists.Standing,
            StandingSaid = checklists.SaidStanding,

            LiveGameState = () => Join(
                // The live Status.json read alongside the folded journal (#360): read per turn like
                // everything else here, not captured once, so the balance the model sees is the balance the
                // game is showing.
                Situation.Describe(gameState.Active, status.Current, SystemWallClock.Instance.UtcNow),
                Join(
                    ActionCapabilities.Describe(actionSurface),
                    Join(
                        MacroCapability.Live(macros),
                        Join(
                            ChecklistCapability.Live(checklists),

                            Join(
                                // The story under way, told from inside (Phase 47).
                                D47.Core.Adventures.AdventureContext.Describe(
                                    adventureBook.Standings(gameState.Active?.Identity.FrontierId),
                                    id => PersonaCatalog.Knows(id) ? PersonaCatalog.Resolve(id).Name : null,
                                    SystemWallClock.Instance.UtcNow),

                            Join(
                                // Both dates, already worked out, below the cache breakpoint where a per-turn
                                // value costs nothing (Phase 24).
                                UtilitiesCapability.Live(
                                    timekeeper, SystemWallClock.Instance.UtcNow, TimeZoneInfo.Local),

                                // Why d47 cannot look something up, when it cannot.
                                ConversationCapability.LiveSearch(
                                    settings.Current.Llm.WebSearch,
                                    self?.SearchReachesTheWeb ?? true))))))),
        };

        // The catalogue a generated story may draw its stops from (Phase 47).
        var notablePlaces = new D47.Knowledge.GecNotablePlacesService(
            loggerFactory.CreateLogger<D47.Knowledge.GecNotablePlacesService>());

        // What writes an adventure, once, for the Commander to agree to.
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

        // Speech reaches the ledger too, or the running totals would look authoritative while covering only
        // what the model cost.
        host.SpeechSpend.LedgerTo(spendLedger, () => settings.Current);

        // The avatar's own imagery, if the Commander has dropped any in.
        host.Avatars = D47.Core.Interface.AvatarLibrary.Load(paths);

        // The buffer the tick closure has been filling since before this instance existed (#51).
        host.JournalLog = journalLog;

        // The face follows the loop.
        voice.StateEntered += state => host.Panel.LoopState = state;

        // That the microphone is open, on both surfaces, as a property of the gate policy rather than of any
        // one capability (Phase 13).
        gate.StateChanged += state => host.ShowMicrophone(state);

        // Stated once at startup as well as on every change, because the opening state is the one a Commander
        // sees for longest and nothing had raised an event yet.
        host.ShowMicrophone(gate.State);

        // A voice the provider refuses is written out of settings rather than merely skipped for the turn it
        // broke.
        voice.VoiceRejected += host.ForgetTheVoice;

        host.ApplyLlmSettings();
        host.ApplySpeechSettings();
        host.ApplyListeningSettings();

        // The mixer as the file left it, before anything is audible.
        audio.Mix = loaded.Audio;

        // From here on, a setting takes effect because it changed — not because something was restarted
        // (Phase 4, "Apply every setting without a restart").
        settings.Changed += host.OnSettingsChanged;

        // The one instance, shared: the injector's foreground rule and the overlay's visibility rule are the
        // same question about the same window, and two readers would be two caches of one handle.
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

        // The Commander switch (Phase 44).
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
        // Whether a lookup is possible is asked at the moment the window opens rather than captured now: the
        // Commander can change the setting or the provider between launching d47 and writing a note, and the
        // window's own first sentence depends on the answer.
        host.LoreEditing = new Settings.LoreEditing(
            lore,
            () => LoreCapability.PlaceOf(host.GameState.Active),
            () => host.CanSearch,
            host.SearchForAsync,
            () => DateTimeOffset.Now);

        // The store and the clock, together, because a fact typed here is stamped with a real instant and
        // Core reads no clock of its own.
        host.Memories = (memoryBook, () => DateTimeOffset.Now);

        // Same pairing, same reason (#162): an adoption is stamped with a real instant and Core reads no
        // clock of its own.
        host.Debrief = (debriefBook, () => DateTimeOffset.Now);

        // The session opens here, over what the file says right now.
        host.BeginDirections();

        // A callout switched off within seconds of it speaking (#162).
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
        host.CommunityGoalSearch = communityGoalSearch;
        host.CommodityLedger = commodityLedger;
        host.Sourcing = sourcingBoard;
        host.Carrier = carrierManifest;

        host.ReservedPhrases = PhrasesAlreadyTaken(capabilities, OtherDynamicCommands());

        host.CoverageRecorder = coverage;
        coverage?.Follow(capabilities, settings);

        host.AudioRecorder = recording;
        host.InputTrace = inputTrace;

        // Captured audio becomes words on the thread pool, never on the audio thread that produced it.
        gate.Captured += host.TranscribeAsync;

        // The route reader lives in the tick closure, so the host reaches it through this rather than owning
        // it — proper-noun biasing wants the systems the Commander is about to arrive in, and those are only
        // in the route file.
        host._route = () => route.Current;
        host._modulePower = () => modulePower.Current;
        host._heardAt = heardAt;

        // Push-to-talk, sampled here rather than hooked.
        tick.Add("push-to-talk", context =>
        {
            pushToTalk.Poll();

            // And the stick, on the same tick (Phase 53).
            var buttons = controllers.Poll();

            pushToTalkButton.Poll(buttons);
            host._cancelButton.Poll(buttons);

            // And then, and only then, whether the stick it is bound to turned up (#45).
            host.WarnIfTheStickIsMissing();

            // Whether the device is actually delivering audio, which only it knows and which is half of what
            // the panel's microphone indicator says.
            gate.Capturing = microphone.IsCapturing;

            // Where the hands-free gate opens and closes.
            gate.Poll(context.Now);
        });

        // Two sources, one gate (Phase 53).
        pushToTalk.Pressed += sources.KeyPressed;
        pushToTalk.Released += sources.KeyReleased;
        pushToTalkButton.Pressed += sources.ButtonPressed;
        pushToTalkButton.Released += sources.ButtonReleased;

        sources.Pressed += () => gate.KeyDown(DateTimeOffset.Now);
        sources.Released += () => gate.KeyUp();

        // Cancel, on press and once (#221).
        host._cancelButton.Pressed += () => host.CancelNow();

        // That d47 is listening, said both ways.
        gate.Started += () => host.Voice.EnterState(Core.Audio.LoopState.Listening);

        // Only the discarded case.
        gate.Ended += reason =>
        {
            if (reason == UtteranceEnd.TooShort)
            {
                host.Voice.EnterState(Core.Audio.LoopState.Idle);
            }
        };

        // The async half of a synchronous tick.
        tick.Add("macros", _ => macros.Poll(() => PhrasesAlreadyTaken(capabilities, OtherDynamicCommands())));

        // Same shape, same reason: a file Elite owns, re-read only when it moves.
        tick.Add("binds", _ => binds.Poll());

        // The switch path, in the tick's own shape: read the file if it changed, read the hardware, decide.
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

                    // Gated by key injection as well as by its own row.
                    Enabled = settings.Current.Actions.Keyboard && settings.Current.Actions.Switches,
                    Destinations = panel.Destinations,
                    Showing = panel.Showing,
                },
                switches.Switches);

            // The annunciator, on whichever surfaces are up.
            host.ShowSwitches(SwitchCapability.Annunciator(reconciler.States));
        });

        // The first thing d47 does that nothing external triggers (Phase 24).
        tick.Add("reminders", context =>
        {
            alarms.Poll();
            host.SoundReminders(timekeeper.Poll(context.Now));
        });

        // Ship builds are hand-editable, and buying a hull the Commander had planned for offers to adopt the
        // plan onto it rather than making them re-point it (Phase 26).
        tick.Add("ships", _unused =>
        {
            shipBuilds.Poll();
            onFootBuilds.Poll();

            // Both halves of the same offer.
            foreach (var adopted in shipPlans.Observe(arrived).Concat(onFootPlans.Observe(arrived)))
            {
                host.Panel.Append($"{adopted}{Environment.NewLine}");
                _ = host.Voice.AnnounceAsync(adopted);
            }

            // Boarding a ship whose build carries engineering the checklist has not got is the moment to say
            // so, once (Phase 38).
            if (drift.Observe(arrived) is { Length: > 0 } asked)
            {
                host.Panel.Append($"{asked}{Environment.NewLine}");
                _ = host.Voice.AnnounceAsync(asked);
            }
        });

        // The cores the Commander wrote, on the tick like every other store.
        tick.Add("own cores", _ => ownPersonas.Poll());

        // A core per ship (Phase 35).
        tick.Add("ship cores", context =>
        {
            shipCoreStore.Poll();

            if (shipCores.Observe(context.Since) is { } due)
            {
                host.PutCoreAboard(due);
            }
        });

        // What d47 remembers, on the tick like every other store (Phase 31).
        var expiredAt = DateTimeOffset.MinValue;

        tick.Add("memory", context =>
        {
            memories.Poll();

            if (!settings.Current.Memory.Enabled)
            {
                // Off means no new writes and nothing reaching the prompt.
                host.ApplyRecall(null);
                return;
            }

            memoryObserver.Observe(gameState.Active, context.Now);
            memoryObserver.Touch(gameState.Active, context.Now);

            // Once at startup and then rarely.
            if (context.Now - expiredAt >= ExpiryEvery)
            {
                expiredAt = context.Now;
                host.ReportExpiredMemories(
                    memoryBook.Expire(context.Now, MemoryCapability.ExpiryOf(settings.Current.Memory)),
                    context.IsFirst);
            }

            host.ApplyRecall(memoryBook.Recall());
        });

        // The arcs, on the tick because goals.json is hand-editable, so a goal typed into it is live without
        // a restart.
        tick.Add("goals", _ => goals.Poll());

        // The adventures file is hand-editable and polled like the others; and when a stamp has moved -
        // Begin, Begin again, a hand edit - the walk the book asked for happens here, on the tick, so the
        // live fold cannot interleave with it.
        tick.Add("adventures", _ =>
        {
            adventureStore.Poll();

            if (adventureBook.NeedsCatchUp)
            {
                adventureBook.CatchUp(D47.Core.Adventures.AdventureBook.FilesToWalk(journalDirectory, adventureBook.EarliestAcceptance()));
            }
        });

        tick.Add("callout-drain", _ => host.SpeakPendingCallouts());

        // Ambience follows the situation Status.json states, sampled on the tick rather than hooked to a
        // journal event: docked, supercruise and on foot are conditions rather than things that happen, and
        // the file is already being read here every tick.
        tick.Add("ambience", _ => host.FollowSituation(status.Current));

        // And the folder those tracks came from, which the Commander can add to while d47 is running (Phase
        // 12, "Pick up dropped-in audio without a restart").
        tick.Add("audio-folder", context => host.RescanAudio(context, drops, cueLogger));

        // NPC voices are scoped to the system, so something has to notice the system changing.
        tick.Add("voice-scope", _ => host.FollowSystemForVoices());

        // After the callouts, so a honk that reports why it did not fire is spoken in the same order it was
        // decided relative to everything else this tick.
        tick.Add("autonomous-drain", _ => host.CarryOutPendingActions(autonomous, gameInput));

        // After the autonomous drain and for the same reason it exists: the tick is synchronous and a key
        // press is not.
        tick.Add("switch-drain", _ => host.CarryOutReconciles(reconciler, gameInput));

        // Last, so every subscriber registered during composition is in place before the first timer-driven
        // tick — and so a failure above happens against a loop that never started rather than one already
        // running against half-built state.
        if (startTicking)
        {
            host._ticking = new TickDriver(tick, loggerFactory.CreateLogger<TickDriver>()).Start();
        }

        return host;
    }

    /// <summary>The callouts d47 ships with, in the order they are examined.</summary>
    private static CalloutEngine BuildCallouts(
        D47Settings settings,
        ILoggerFactory loggers,
        ChecklistService checklists,
        LoreBook lore,
        LoreVisits loreVisits,
        MemoryBook memories,
        D47.Core.Adventures.AdventureBook adventures,
        ViewStateStore viewState,
        D47.Core.Journal.CommodityLedger ledger,
        D47.Core.Knowledge.CommunityGoalSearch communityGoal,
        GameStateStore gameState)
    {
        var engine = new CalloutEngine(loggers.CreateLogger<CalloutEngine>())
            .Add(new DangerCallout())

            // Above everything except danger itself (Phase 15).
            .Add(new AnnouncedAttackCallout())
            .Add(new FuelCallout(loggers.CreateLogger<FuelCallout>()))
            .Add(new RouteCallout(loggers.CreateLogger<RouteCallout>()))
            .Add(new LongJumpCallout())
            .Add(new ArrivalCallout())

            // Capacity comes from the derived grade table.
            .Add(new MaterialMilestoneCallout { Capacity = MaterialGrades.CapacityOf })

            // Phase 40, and the same capacity for the opposite purpose: the milestone callout needs it to
            // work out how far along a stock is, and this one needs it to say nothing about a stock that is
            // finished.
            .Add(new EmissionCallout(loggers.CreateLogger<EmissionCallout>())
            {
                Capacity = MaterialGrades.CapacityOf,
            })

            // Phase 41.
            .Add(new LimpetCallout())

            // Phase 11.
            .Add(new CarrierCallout())

            // Phase 17.
            .Add(new SamplingCallout())
            .Add(new ProspectorCallout())
            .Add(new CoreAsteroidCallout())
            .Add(new ChecklistCallout(checklists))

            // Low on purpose.
            .Add(new RivalTerritoryCallout
            {
                LastExplainedDay = () => viewState.Load().RivalExplainedOn,
                RememberExplainedDay = day => viewState.Save(viewState.Load() with { RivalExplainedOn = day }),
            })

            // Phase 23.
            .Add(new LoreCallout(lore, loreVisits))

            // Phase 31, and the lowest thing here that is not the ambient line: it fires once, at the start
            // of a session, and it is about what was true before the Commander sat down.
            .Add(new ContinuityCallout())

            // Getting into a game and leaving one (change-requests.md 29), which is a different event from
            // the line above: that one greets when d47 starts, and this one when the game does.
            .Add(new SessionCallout())

            // Where a sale of the Community Goal commodity leaves the session, net of cost (#296).
            .Add(new CommunityGoalSaleCallout(ledger, communityGoal))

            // A beat of the Commander's story, when they reach it (Phase 47).
            .Add(new D47.Core.Adventures.AdventureCallout(adventures))

            // Invented chatter (#244): the marker only — the app composes the exchange, and with no model the
            // marker composes to nothing.
            .Add(new NpcChatterCallout())
            .Add(new AmbientCallout())
            .Add(new IncomingMessages
            {
                Enabled = () => settings.Speech.SpeakIncomingMessages,
                IncludeNpcs = () => settings.Speech.SpeakNpcMessages,

                // squadleaders follows the Squadron row (#299) — a Commander does not know Elite writes those
                // as two channels, so there is one switch, not two.
                ChannelEnabled = channel => channel switch
                {
                    "starsystem" => settings.Speech.SpeakSystemChat,
                    "local" => settings.Speech.SpeakLocalChat,
                    "wing" => settings.Speech.SpeakWingChat,
                    "squadron" or "squadleaders" => settings.Speech.SpeakSquadronChat,
                    "player" => settings.Speech.SpeakDirectMessages,
                    _ => true,
                },

                // Reads live state directly rather than waiting on the voice-scope follow to install it
                // (#102): a line judged before that follow has ever run still takes the authority road
                // when the state already says it should.
                AuthorityNearOwnCarrier = () =>
                    gameState.Active is { } active
                    && active.Carrier.Owned
                    && active.Carrier.StarSystem is { Length: > 0 } parked
                    && string.Equals(parked, active.Location.StarSystem, StringComparison.OrdinalIgnoreCase),
            });

        // Elite echoes what you send back to you on the channel it went out on.

        ApplyCalloutSettings(engine, settings);
        return engine;
    }

    /// <summary>
    /// Pushes the callout settings into the engine and into the individual callouts that carry a
    /// tunable.
    /// </summary>
    private static void ApplyCalloutSettings(CalloutEngine engine, D47Settings settings)
    {
        var callouts = settings.Callouts;

        // The clock the engine does not have, so it can tell a callout switched off seconds after it spoke
        // from one switched off an hour later (#162).
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
        engine.SetEnabled("community-goal-sales", callouts.CommunityGoalSales, now);

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
                    // Read through the settings the switch was handed rather than captured once, so a
                    // Commander who turns the lookup off is obeyed on the next arrival rather than on the
                    // next launch — the same shape the ambient row below has.
                    lore.Remarks = () => callouts.Lore;
                    break;

                case AmbientCallout ambient:
                    ambient.Interval = TimeSpan.FromSeconds(callouts.AmbientSeconds);
                    ambient.Longest = TimeSpan.FromSeconds(callouts.AmbientMaxSeconds);

                    // Silent while personality is off.
                    ambient.Enabled = () => settings.Callouts.Ambient && settings.Llm.PersonalityEnabled;
                    break;

                case NpcChatterCallout chatter:
                    chatter.Interval = TimeSpan.FromSeconds(callouts.NpcChatterSeconds);
                    chatter.Longest = TimeSpan.FromSeconds(callouts.NpcChatterMaxSeconds);

                    // The ambient pair of gates (#244): theatre is personality by any reading, and the
                    // no-model half lives at the compose step for the reason above.
                    chatter.Enabled = () => settings.Callouts.NpcChatter && settings.Llm.PersonalityEnabled;
                    break;
            }
        }
    }

    /// <summary>Where the Anthropic key lives in the secret store.</summary>
    public const string AnthropicApiKeySecret = "anthropic.apiKey";

    /// <summary>
    /// Rebuilds everything downstream of the language model settings: the provider itself, the pinned
    /// model, the standing About Me text, and whether the model capability is on at all.
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
            // The key may legitimately be absent.
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
        Turns.BackgroundModel = current.Llm.BackgroundModel ?? current.Llm.Model;

        // What the Commander will pay for, kept apart from what the router thinks they asked for.
        Turns.EffortFloor = current.Llm.EffortFloor;
        Turns.EffortCeiling = current.Llm.EffortCeiling;

        // What the transcriber gets wrong, put right before anything reads the sentence (#134).
        Turns.Heard = HeardAsMeant;

        // Position 3.5, and asked of the client that will speak rather than of the settings, so the prompt
        // describes the voice a Commander will actually hear.
        Turns.CanBeDirected = () => DirectableIn(VoiceGroup.Aboard);

        // Position 4, both halves: the turn path is cached above the breakpoint, so the story's thirteen
        // hundred tokens are paid once per edit rather than per turn (Phase 43).
        Turns.AboutMe = CommanderStory.Compose(current.Llm.CharacterSheet, current.Llm.AboutMe, withStory: true);

        // Position 3 of the assembled prompt, and null when personality is off.
        ApplyPersonaBlock();

        LlmAvailability.SetProviderConfigured(provider is not null, reason);
    }

    /// <summary>
    /// Puts the recall block into the prompt, and only when it has actually changed (Phase 31 , "Recall
    /// arrives above the cache breakpoint").
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
    /// Says what an expiry took, when it took something worth saying (Phase 31, "Forgetting is said out
    /// loud when it matters").
    /// </summary>
    /// <param name="priming">True on the startup tick.</param>
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
    /// Rebuilds everything downstream of the speech settings: the voice provider, the voice itself, the
    /// cues, the bed, the output device and the retry policy.
    /// </summary>
    internal IReadOnlyList<string> EndpointModelIds => _endpointModels;

    /// <summary>
    /// Asks the endpoint what it serves, if it is the kind of thing that can be asked and has not been
    /// asked already (Phase 29).
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

                // Only if the address has not moved again while this was in recording.
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
                // No list is a capability being partly off rather than a failure: the model row still accepts
                // a name typed in, which is what it did before there was anybody to ask.
                _logger.LogWarning(ex, "Could not ask the endpoint which models it serves");
            }
        });
    }

    /// <summary>The ids the voice picker offers for one slot.</summary>
    internal IReadOnlyList<string> VoiceIds(VoiceGroup group = VoiceGroup.Aboard) =>
        [.. VoicesFor(group).Voices.Select(voice => voice.Id)];

    /// <summary>How the picker labels one — "Ava — Female, en-US" rather than the raw id.</summary>
    internal string? VoiceNameFor(string id) => VoiceGroups.NameFor(VoicesFor, id);

    /// <summary>
    /// How a voice is shown to the Commander, wherever one is shown — the row, its tooltip and the
    /// picker all read this.
    /// </summary>
    internal string VoiceLabelFor(string id) => VoiceLabelFor(VoiceGroup.Aboard, id);

    /// <summary>What the provider tags one voice's gender as, or null where it says nothing (#146).</summary>
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
    /// </summary>
    internal string? WhyNoVoices(VoiceGroup group = VoiceGroup.Aboard)
    {
        var provider = TtsProviderCatalog.Selected(VoiceGroups.ProviderFor(Settings.Current.Speech, group));

        return provider.Speaks ? VoicesOf(provider.Id).WhyEmpty(provider.Name) : null;
    }

    /// <summary>One voice per core, chosen once and written to settings (Phase 11, #33).</summary>
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

            // Nothing else will notice: the pairing is not a settings row, and the core aboard has just
            // acquired the voice it is about to speak in.
            ApplySpeechSettings();
        }
        catch (Exception ex)
        {
            // A convenience, exactly like the pass at startup.
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

    /// <summary>One repair's result, with a voice chosen for every core the repair took one off.</summary>
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

    /// <summary>Puts every named default back where the table says it goes, once.</summary>
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
            // The pass has run, but it may have run in a session with no model configured and left the core
            // aboard with nothing.
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

            // Flagged as run even when no model was configured and only the named defaults were written.
            Settings.Replace("persona.voices", current => current with
            {
                Persona = current.Persona with { Voices = paired, VoicesPaired = true },
            });

            // The core aboard may have just acquired a voice, and nothing else will notice.
            ApplySpeechSettings();
        }
        catch (Exception ex)
        {
            // Pairing is a convenience.
            _logger.LogWarning(ex, "Could not pair voices to personas");
        }
    }

    /// <summary>
    /// Makes the stored voices and the selected provider agree, and answers the speech settings that
    /// result.
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

        // The decision itself is a pure function of settings and lives where a test can reach it.
        Settings.Replace(SpeechCapability.ProviderKey, VoiceMemory.Reconciled);

        return Settings.Current.Speech;
    }

    /// <summary>Whether the selected provider has whatever credential it needs, if it needs one.</summary>
    private bool HasKeyFor(TtsProviderInfo provider) =>
        provider.KeySecretName is not { } secret || Secrets.Has(secret);

    /// <summary>Drops one voice the provider refused, everywhere it is written down.</summary>
    private void ForgetTheVoice(string voiceId)
    {
        _logger.LogInformation("{Voice} was refused by the provider; removing it", voiceId);

        Settings.Replace(
            SpeechCapability.ProviderKey,
            current => SpeechCapability.WithoutTheVoice(current, voiceId));

        ApplySpeechSettings();
    }

    /// <summary>One provider's client, or null for a provider that does not speak.</summary>
    internal string KokoroFolder() => Path.Combine(Paths.Data, "models", "kokoro");

    /// <summary>Whether the local voice is here, and what it would cost if not.</summary>
    private string LocalVoiceState() =>
        D47.Core.Speech.KokoroAssets.IsInstalled(KokoroFolder())
            ? "Installed. Nothing D47 speaks through this provider leaves this machine."
            : $"Not downloaded. About {D47.Core.Speech.KokoroAssets.TotalMegabytes:0} MB, fetched "
              + "once from huggingface.co.";

    /// <summary>Whether a download is already running, atomic because the button is a press.</summary>
    private int _fetchingVoice;

    /// <summary>What the local voice says the moment it can say anything.</summary>
    private const string LocalVoiceProof =
        "Local voice installed. This is D47, speaking from your own machine. Nothing I say through "
        + "this provider leaves it.";

    /// <summary>Fetches the local voice, off the UI thread, saying how far it has got.</summary>
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
    /// The transcript pre-pass (#134): what this Commander's transcriber reliably gets wrong, put right
    /// before anything reads the sentence.
    /// </summary>
    internal string HeardAsMeant(string spoken) =>
        Flying.Length == 0 || _heardNames is not { } store
            ? spoken
            : store.AliasesFor(Flying).Apply(spoken);

    /// <summary>Records a correction the Commander steered d47 to (#134).</summary>
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
    /// Reads every journal on disk again and rebuilds what each ship was last seen holding (#128).
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

            // Dated off the newest thing the walk saw rather than off the clock, so the next start's catch-up
            // begins where this left off.
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

            // The picker's list, asked for again now that there is something to list.
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

    /// <summary>Swaps the local voice onto a different one of Kokoro's eight builds (#139).</summary>
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

            // Let go of the file before overwriting it.
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

    /// <summary>Closes and forgets the Kokoro client, so nothing is holding <c>model.onnx</c> open.</summary>
    private void DropLocalVoiceClient()
    {
        if (_clients.Remove(TtsProviderCatalog.KokoroId, out var client)
            && client is IDisposable disposable)
        {
            disposable.Dispose();
        }
    }

    /// <summary>Asks the local voice what it offers, now that it has something to offer.</summary>
    private async Task RefreshLocalVoicesAsync()
    {
        if (_clients.GetValueOrDefault(TtsProviderCatalog.KokoroId) is { } client)
        {
            await LoadVoicesAsync(client).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Speaks one line in the voice that was just downloaded, through the one arbiter like everything
    /// else that makes a sound.
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
            // The files are there and something else is wrong, which is worth saying on the row: the state
            // above it now reads "installed" and the Commander heard nothing.
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
            _loggerFactory.CreateLogger<ElevenLabsTtsProvider>(),

            // Asked per line rather than captured, the same as the key, so switching model applies to the
            // next thing said rather than to the next session (#291).
            model: () => Settings.Current.Speech.ElevenLabsModel),

        TtsProviderCatalog.OpenAiId => new OpenAiTtsProvider(
            () => Secrets.TryGet(OpenAiTtsProvider.KeySecretName, out var key) ? key : null,
            _loggerFactory.CreateLogger<OpenAiTtsProvider>(),

            // How the core aboard should be performed, asked per sentence because a Commander switches core
            // while d47 is running (#49).
            direction: () => VoiceDirection.For(
                Settings.Current.Llm.PersonalityEnabled ? Personas.Current : null)),

        TtsProviderCatalog.CartesiaId => new CartesiaTtsProvider(
            () => Secrets.TryGet(CartesiaTtsProvider.KeySecretName, out var key) ? key : null,
            _loggerFactory.CreateLogger<CartesiaTtsProvider>()),

        // The local voice (Phase 59).
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

            // The pool a re-voiced sender is drawn from, on this provider's cast.
            var cast = Casting.Of(provider.Id);
            cast.Pool = VoicePool.From(listed.Voices);

            // And which of them are a woman's, so a sender whose name reads as one is given one.
            cast.Feminine = VoicePool.Feminine(listed.Voices);

            // Both numbers, because one of them alone is what hid that: "1 voice available" is alarming
            // beside "473 offered" and unremarkable on its own.
            _logger.LogInformation(
                "{Count} of {Offered} voices are available for re-voiced senders, {Feminine} of them women's",
                cast.Pool.Count,
                listed.Count,
                cast.Feminine.Count);

            // Pairing a voice to each core needs the list, so it starts once the list arrives rather than at
            // startup.
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
            // No list is a capability being partly off, not a failure: the row still accepts a voice name
            // typed in, and speaking still works with the provider's default.
            _logger.LogWarning(ex, "Could not fetch the list of voices");
        }
    }

    /// <summary>
    /// When the core currently aboard became the core currently aboard, and what the ship's ledger
    /// looked like then.
    /// </summary>
    private readonly Ambience _ambience = new();

    /// <summary>How often the drop-in folder is looked at.</summary>
    private static readonly TimeSpan AudioScanEvery = TimeSpan.FromSeconds(2);

    private TimeSpan _sinceAudioScan = TimeSpan.Zero;

    /// <summary>How often the memory store is checked for entries past their expiry (Phase 31).</summary>
    private static readonly TimeSpan ExpiryEvery = TimeSpan.FromMinutes(10);

    private DateTimeOffset _personaSelectedAt = DateTimeOffset.Now;

    /// <summary>When each core was last aboard, and what the ship's ledger looked like then.</summary>
    private readonly Dictionary<string, (DateTimeOffset At, SessionSummary? Session)> _personaLastSeen =
        new(StringComparer.Ordinal);

    /// <summary>What kind of switch the settings write about to arrive is (Phase 35).</summary>
    private PersonaSwitch _personaCause = PersonaSwitch.Selected;

    /// <summary>
    /// Puts the core the Commander bound to this ship aboard (Phase 35, "Switching ships switches the
    /// core").
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

    /// <summary>What a new Commander logging in actually changes (Phase 44).</summary>
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

        // Every core's transcript, and the loop pointed at the fresh one — the handover is by reference, so a
        // discard the loop was not told about would leave it appending to the old Commander's conversation.
        Personas.ForgetTranscripts();
        Turns.UseTranscript(Personas.Transcript);

        // The ship they are in is adopted afresh on the next tick, silently, as the ship d47 found them in —
        // which re-resolves the core through the store now keyed per Commander.
        ShipCores.Reset();
        Drift?.Reset();

        // Once per session rather than once per run: the greeting is the new ship AI's first words.
        Continuity?.Rearm();

        // The debrief too, and in this order for a reason: the session that just ended was the previous
        // Commander's, so it is filed under their id — named rather than asked for, because the game state is
        // already pointed at whoever logged in (#162).
        RunDebrief(change.Previous.FrontierId);

        // And the directions are re-latched, because they are one person's and the person changed.
        BeginDirections();

        // On the panel, so the transcript says why the next line starts from nothing.
        Noted?.Invoke($"Commander {change.Current.Name} logged in");
    }

    /// <summary>
    /// Writes down when the core aboard stopped being aboard, so a gap reaction can be about a month
    /// rather than about an evening (Phase 35).
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
            // Losing this costs one core one gap reaction it will not give.
            _logger.LogDebug(ex, "Could not record when {Core} was last aboard", id);
        }
    }

    private void ApplyPersonaSettings()
    {
        var outgoing = Personas.Current;

        // Remembered before the switch, because after it there is nothing left to measure against.
        _personaLastSeen[outgoing.Id] = (_personaSelectedAt, GameState.Active?.Session ?? SessionSummary.Empty);

        var incoming = PersonaCatalog.Resolve(Settings.Current.Persona.Id);
        var seen = _personaLastSeen.TryGetValue(incoming.Id, out var last) ? last : default;

        var away = seen.At == default ? (TimeSpan?)null : DateTimeOffset.Now - seen.At;
        var delta = seen.At != default && seen.Session is { } session
            ? TelemetryDelta.Between(session, GameState.Active?.Session, GameState.Active)
            : null;

        if (!Personas.Apply(Settings.Current.Persona, away, delta, _personaCause))
        {
            // The name may still have changed underneath an unchanged core, and that is part of the persona
            // block, so the prompt is rebuilt either way.
            Turns.Persona = Personas.RenderBlock(Settings.Current.Llm.PersonalityEnabled);
            return;
        }

        _personaSelectedAt = DateTimeOffset.Now;

        // The core that just left, written where the next session can read it.
        RememberCoreAboard(outgoing.Id, _personaLastSeen[outgoing.Id].At);

        // Each core owns its transcript, handed over by reference so the turns land in it directly.
        Turns.UseTranscript(Personas.Transcript);
        Turns.Persona = Personas.RenderBlock(Settings.Current.Llm.PersonalityEnabled);
    }

    /// <summary>The new core, saying it is here.</summary>
    private void OnPersonaChanged(PersonaChanged change)
    {
        // The ship's voice is the core aboard's, so it has to be re-read when the core changes.
        ApplySpeechSettings();

        // And what d47 answers to, for the same reason and with the same failure if it is skipped: the wake
        // word defaults to the ship's AI name, so a core switch that did not re-read it would leave a
        // Commander calling the new core by the old one's name.
        ApplyWakeWords();

        _logger.LogInformation(
            "Persona changed from {Previous} to {Current} ({Arrival})",
            change.Previous?.Name ?? "(none)",
            change.Current.Name,
            change.Arrival);

        // Ahead of the line the new core is about to say, and outside the task below, so the transcript reads
        // in the order it happened: the switch, then the first thing said after it.
        Noted?.Invoke($"Switched to {change.Current.Name}");

        // A quiet arrival is a real switch with nothing to say (Phase 35).
        if (change.Arrival == PersonaArrival.Quiet)
        {
            return;
        }

        _ = Task.Run(async () =>
        {
            // The Commander chose a core on a settings row and nothing has happened yet: the voice has to be
            // fetched and, on a gap, a model has to be asked for a line.
            PersonaSettling?.Invoke(true);

            string? line = change.Current.Intro;

            // What the ship can prove about itself, for the guard on both branches below (#338).
            var facts = ShipFacts.Of(GameState.Active);

            try
            {
                // Before a word is spoken, because the first thing a core says is the thing most worth
                // hearing in its own voice.
                await EnsureVoiceForCurrentPersonaAsync().ConfigureAwait(false);

                if (change is { Arrival: PersonaArrival.Gap, Gap: { } gap })
                {
                    var instruction =
                        "You have just been switched back on after "
                        + $"{TelemetryDelta.Spoken(gap.Away)} of not running. Say one or two sentences "
                        + "reacting to the missing time, exactly as your character would. Do not greet "
                        + "the Commander formally and do not offer a list of what you can do.";

                    // Named because #338's retry asks the same question again with the contradiction
                    // appended, and only the instruction differs.
                    Task<string?> AskAsync(string ask) => FlavourTurn.AskAsync(
                        Turns.Provider,
                        Turns.BackgroundModel,
                        Personas.RenderBlock(Settings.Current.Llm.PersonalityEnabled),

                        // The sheet and not the story: a core reacting to lost time is speaking to somebody
                        // it knows by name, and this is a one-off with no index to choose a story call by.
                        CommanderStory.Compose(
                            Settings.Current.Llm.CharacterSheet, Settings.Current.Llm.AboutMe, withStory: false),
                        ask,
                        gap.TelemetryDelta,
                        Spend,
                        PriceTable.Default,
                        _logger);

                    var generated = await AskAsync(instruction).ConfigureAwait(false);

                    // Checked rather than merely non-null: a rewording brief answered with the model talking
                    // about itself is not a line this core said (GitHub issue 46).
                    line = await ContradictedClaims.SayableAsync(
                               generated,
                               facts,
                               contradiction => AskAsync($"{instruction} {contradiction.Correction}"),
                               _logger,
                               "persona.return").ConfigureAwait(false)
                           ?? ContradictedClaims.Sayable(
                               change.Current.Return, facts, _logger, "persona.return");
                }
                else if (FlavourBriefs.Introducing(
                             change.Current.Intro,
                             Settings.Current.Llm.PersonalityEnabled) is { } brief)
                {
                    // The authored intro is a sample of how this core sounds rather than the script it reads,
                    // so it goes through the model like everything else d47 says in character.
                    Task<string?> AskAsync(string ask) => FlavourTurn.AskAsync(
                        Turns.Provider,
                        Turns.BackgroundModel,
                        Personas.RenderBlock(brief.NeedsPersona),
                        StoryFor(brief),
                        ask,
                        gameState: null,
                        Spend,
                        PriceTable.Default,
                        _logger);

                    var generated = await AskAsync(brief.Instruction).ConfigureAwait(false);

                    // Checked rather than merely non-null: a rewording brief answered with the model talking
                    // about itself is not a line this core said (GitHub issue 46).
                    line = await ContradictedClaims.SayableAsync(
                               generated,
                               facts,
                               contradiction => AskAsync($"{brief.Instruction} {contradiction.Correction}"),
                               _logger,
                               "persona.intro").ConfigureAwait(false)
                           ?? ContradictedClaims.Sayable(
                               change.Current.Intro, facts, _logger, "persona.intro");
                }
            }
            finally
            {
                // Cleared before the line is said rather than after it has been spoken aloud: what the row
                // was waiting for is d47 having something to say, and a row still marked busy while the core
                // is talking is a row describing the wrong thing.
                PersonaSettling?.Invoke(false);
            }

            // Both the model's line and the authored one contradicted the ship, and there is nothing true
            // left to say (#338).
            if (line is null)
            {
                return;
            }

            // Anything d47 says without a turn behind it still belongs in the transcript, so that what was
            // heard and what can be read back are the same set.
            Said?.Invoke(line);

            await Voice.AcknowledgePersonaAsync(line).ConfigureAwait(false);
        });
    }

    /// <summary>Re-reads <c>data/audio/</c> when something in it has changed.</summary>
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

        // The rows that read the library — the bed picker's choices and the row saying what was found — have
        // no other way to know.
        AudioReloaded?.Invoke();
    }

    /// <summary>The ambience layer, following what the Commander is doing (Phase 12).</summary>
    private void FollowSituation(D47.Core.Journal.GameStatus status)
    {
        if (!_ambience.Enter(Situations.For(status)))
        {
            return;
        }

        // The old situation's track does not play out over the new one.
        Audio.StopMusic();
        PlayNextTrack();
    }

    /// <summary>Starts the next ambience track, or leaves it quiet.</summary>
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

        // What to build, what to release, which slots moved and whose list to ask for again are decided in
        // Core, where a test can reach them; what to build and how to fetch it stay here, where the loggers
        // and the secret store are.
        var plan = SpeechWiring.Plan(
            _speechWiring,
            VoiceGroups.Selected(speech),
            id => HasKeyFor(TtsProviderCatalog.Selected(id)));

        _speechWiring = plan.Next;

        // Released first, so a slot moving from ElevenLabs to Edge and another moving the other way do not
        // hold two of each at once.
        foreach (var released in plan.Dispose)
        {
            if (_clients.Remove(released, out var client))
            {
                // Through the interface, so this stays correct for a provider that needs no disposal.
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

        // One decorator per slot over the shared client, which is what lets the spend row answer "which slot
        // is costing money" without a second connection to the provider — the thing
        // ElevenLabsTtsProvider.MaxConcurrent's reasoning depends on (Phase 57).
        foreach (var moved in plan.Rewire)
        {
            _slots[moved] = _clients.GetValueOrDefault(VoiceGroups.ProviderFor(speech, moved)) is { } client
                ? new MeteredTtsProvider(client, SpeechSpend, moved)
                : null;
        }

        // Fetched in the background.
        foreach (var asking in plan.RefetchVoices)
        {
            if (_clients.GetValueOrDefault(asking) is { } client)
            {
                _ = LoadVoicesAsync(client);
            }
        }

        Voice.Tts = Speaker(VoiceGroup.Aboard);
        Voice.SpeakerFor = Speaker;

        // Everyone d47 can speak as, filled in from settings.
        var aboard = VoiceGroups.ProviderFor(speech, VoiceGroup.Aboard);
        var carrier = VoiceGroups.ProviderFor(speech, VoiceGroup.Carrier);

        foreach (var providerId in VoiceGroups.ProvidersInUse(speech))
        {
            var cast = Casting.Of(providerId);

            // A rate is a property of the synthesiser rather than of the Commander's patience, once two of
            // them can be speaking at once: ElevenLabs *rejects* a speed outside its range rather than
            // clamping it, so a figure chosen for Edge and applied here would not be a fast carrier but a
            // silent one (Phase 57).
            cast.Rate = SpeechCapability.RateFor(Settings.Current, providerId);

            // The ship's voice belongs to the ship's provider and to nobody else's.
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
                // A device that has gone away between being chosen and being opened.
                _logger.LogError(ex, "Could not move audio output to {Device}", speech.OutputDevice);
            }
        }
    }

    /// <summary>Turns one captured utterance into words and hands them on.</summary>
    private const double NoSpeechFloor = 0.6;

    private void TranscribeAsync(Utterance utterance)
    {
        // The microphone has closed and the words are being worked out.
        Voice.EnterState(Core.Audio.LoopState.Transcribing);

        if (!_transcriber.IsReady)
        {
            // Captured but not transcribable.
            _logger.LogInformation(
                "Heard {Seconds:0.#}s but no speech model is loaded", utterance.Duration.TotalSeconds);

            const string Cannot = "I heard you, but I have no speech model loaded to understand it.";

            _ = Voice.AnnounceAsync(Cannot);
            Said?.Invoke(Cannot);

            // No cue: a sentence is about to be spoken saying the same thing, and a chime under it is d47
            // telling the Commander twice.
            Voice.EnterState(Core.Audio.LoopState.Idle, cue: false);
            return;
        }

        if (utterance.IsSilent)
        {
            // Not "nothing intelligible" — nothing at all arrived.
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
                // Journal-derived and network-free.
                var nouns = ProperNouns.From(GameState.Active, _route?.Invoke());

                // The unprompted second opinion, beside the prompted pass rather than after it (#196):
                // tiny.en answers in ~350 ms while the main model is still working, so the gate below costs
                // nothing in latency.
                var probe = _transcriber.NoSpeechAsync(utterance);

                var transcription = await _transcriber
                    .TranscribeAsync(utterance, nouns)
                    .ConfigureAwait(false);

                // The exact buffer the transcriber was given, beside what it came back with (#164).
                AudioRecorder?.Heard(utterance, transcription);

                // **A word hallucinated from silence is refused here** (#196).
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

                // A panel is asking for a value and this is the answer to it (Phase 25, "Say it, or type
                // it").
                if (Prompted(new Core.Interface.Heard(
                        transcription.Text, transcription.Confidence, Final: true)))
                {
                    // Written down, because nothing after this point will.
                    HeardAside(transcription.Text, "answering the question");

                    Voice.EnterState(Core.Audio.LoopState.Idle, cue: false);
                    return;
                }

                if (transcription.IsEmpty)
                {
                    // Distinguished from a failure: the model ran and heard nothing worth reporting, which a
                    // Commander who coughed should not be told is an error.
                    _logger.LogInformation("Nothing intelligible in {Seconds:0.#}s", utterance.Duration.TotalSeconds);

                    // Without a cue, like every other path here that has nothing to say (remediation.md 14,
                    // item 8).
                    Voice.EnterState(Core.Audio.LoopState.Idle, cue: false);
                    return;
                }

                if (_heardAt is { } clock)
                {
                    clock.Value = DateTimeOffset.Now;
                }

                // The wake word, applied to the words rather than to the audio (Phase 13).
                var decision = Wake.Admit(transcription.Text, DateTimeOffset.Now);

                if (decision.Outcome == WakeOutcome.Ignored)
                {
                    // Somebody in the room said something that was not to d47.
                    _logger.LogDebug("Not addressed to me: {Text}", transcription.Text);
                    Voice.EnterState(Core.Audio.LoopState.Idle, cue: false);
                    return;
                }

                if (decision.Outcome == WakeOutcome.Woken)
                {
                    // The name and nothing after it.
                    _logger.LogInformation("Woken by name; listening for what follows");

                    // The cue on its own rather than the loop state behind it.
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

                // What was heard, where it is not what gets asked.
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

    /// <summary>The surfaces that may be waiting on a spoken value (Phase 25, "Say it, or type it").</summary>
    private readonly List<Func<Core.Interface.Heard, bool>> _prompts = [];

    /// <summary>Adds a surface to the list of places a spoken value may be destined for.</summary>
    public void RoutePrompts(Func<Core.Interface.Heard, bool> surface) => _prompts.Add(surface);

    /// <summary>The navigators a spoken "show me the checklist" moves (Phase 25).</summary>
    private readonly List<Core.Interface.PanelNavigator> _navigators = [];

    /// <summary>
    /// The one mechanism that carries a transcript root from the surface that moved it to the rest
    /// (Phase 45).
    /// </summary>
    private readonly Core.Interface.TranscriptMirror _transcript = new();

    /// <summary>
    /// How to reach each navigator from a thread that does not own it, in the order they were routed.
    /// </summary>
    private readonly List<(Core.Interface.PanelNavigator Nav, Action<Action> Post)> _surfaces = [];

    /// <summary>
    /// The panel as the switch path sees it: every page any surface registered, and the one showing.
    /// </summary>
    private volatile PanelSnapshot _panel = new([], null);

    private sealed record PanelSnapshot(IReadOnlyList<Core.Interface.PanelDestination> Destinations, string? Showing);

    /// <summary>
    /// Adds a surface's navigator to the ones a spoken phrase moves, with how to reach it from another
    /// thread.
    /// </summary>
    /// <paramref name="post"/>
    /// is called from the tick; it should carry a dispatcher the surface captured on its own thread
    /// rather than read one at call time.
    /// </paramref>
    public void RouteNavigation(
        Core.Interface.PanelNavigator nav, Action<Action> post, bool leads = false)
    {
        _navigators.Add(nav);
        _surfaces.Add((nav, post));

        // Into the mirror before the snapshot is hooked, so a surface that arrives behind the other is
        // brought level and the first snapshot already reads two surfaces agreeing.
        if (leads)
        {
            _transcript.Lead(nav);
        }
        else
        {
            _transcript.Add(nav);
        }

        // Taken here and retaken every time a surface moves, on the thread that moved it.
        nav.Changed += (_, _) => SnapshotPanel();
        SnapshotPanel();
    }

    private void SnapshotPanel()
    {
        var destinations = _navigators
            .SelectMany(nav => nav.Destinations)
            .DistinctBy(page => page.Root.Key)
            .ToList();

        // What the panel is showing is what every surface agrees it is showing.
        var showing = _navigators.Select(nav => nav.Root.Key).Distinct().ToList();

        _panel = new PanelSnapshot(destinations, showing.Count == 1 ? showing[0] : null);
    }

    /// <summary>Every page any surface offers, for the switch editor's list (Phase 46).</summary>
    public IReadOnlyList<Core.Interface.PanelDestination> PanelDestinations => _panel.Destinations;

    /// <summary>
    /// Puts every surface on this page, each on its own thread — what a switch position that names a
    /// destination does (Phase 46).
    /// </summary>
    private void Show(string rootKey)
    {
        foreach (var (nav, post) in _surfaces)
        {
            post(() => nav.Show(rootKey));
        }
    }

    /// <summary>How each surface moves the page it is showing (#34).</summary>
    private readonly List<Func<Core.Interface.PanelScrollStep, Core.Interface.PanelScrollOutcome>> _scrollers = [];

    /// <summary>Adds a surface to the ones a spoken scroll moves (#34).</summary>
    public void RouteScrolling(Func<Core.Interface.PanelScrollStep, Core.Interface.PanelScrollOutcome> scroll) =>
        _scrollers.Add(scroll);

    /// <summary>
    /// Moves the page on every surface, and says so — or null when the phrase was not a scroll, which
    /// is the common case and falls through to the turn (#34).
    /// </summary>
    public string? Scroll(string spoken)
    {
        if (Core.Interface.PanelScroll.Match(spoken) is not { } step)
        {
            return null;
        }

        // Every one of them, and then what that means out loud is asked of the vocabulary rather than decided
        // here: the wording belongs beside the phrases it answers, where it can be asserted without an
        // AppHost.
        return Core.Interface.PanelScroll.Answer(
            step,
            _scrollers.Select(scroll => scroll(step)));
    }

    /// <summary>
    /// Moves every surface the phrase named somewhere, and says what happened — or null when it named
    /// nowhere, which is the common case and falls through to the turn.
    /// </summary>
    public string? Navigate(string spoken)
    {
        string? said = null;

        foreach (var nav in _navigators)
        {
            // Every one of them, and the first answer is the one said out loud.
            var moved = Core.Interface.PanelPhrases.Apply(spoken, nav);

            said ??= moved;
        }

        return said;
    }

    /// <summary>Offers what was heard to each surface in turn, and says whether one took it.</summary>
    private bool Prompted(Core.Interface.Heard heard) =>
        _prompts.Any(surface => surface(heard));

    /// <summary>The plotted route, for proper-noun biasing.</summary>
    private Func<NavRoute>? _route;

    /// <summary>The plotted route, for anything that wants to draw it (Phase 37, "Progress").</summary>
    public NavRoute Route => _route?.Invoke() ?? NavRoute.None;

    /// <summary>Set during composition, like <see cref="_route"/> and for the same reason.</summary>
    private Func<ModulePower>? _modulePower;

    /// <summary>What Elite says each module in the ship being flown draws (Phase 38).</summary>
    public ModulePower ModulePower => _modulePower?.Invoke() ?? ModulePower.None;

    /// <summary>The model currently being fetched, or null.</summary>
    private string? _fetching;

    /// <summary>Downloads a selected model that is not on disk, then loads it.</summary>
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

                // Re-applied rather than loaded directly, so a file arriving goes through the one path that
                // knows what loading a model entails.
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
    /// Rebuilds everything downstream of the listening settings: the device, the key, the gate policy
    /// and the pre-roll.
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

        // Started before the microphone, so the first buffer off a freshly opened device is already going
        // through it.
        if (listening.EchoCancellation)
        {
            Echo.SuppressNoise = listening.NoiseSuppression;
            Echo.Start();
        }
        else
        {
            Echo.Stop();
        }

        // From the canceller's live state rather than from the row that asked for it.
        Listening.EchoCancelled = Echo.IsActive;

        ApplyWakeWords();

        // Rebinding while the key is held would leave the gate open with nothing able to close it — the
        // listening equivalent of a stranded key.
        _pushToTalk.ForceUp();
        _pushToTalkButton.ForceUp();

        // The model, before the key.
        var model = ListeningWiring.PlanModel(listening, Models);

        switch (model.Action)
        {
            case SpeechModelAction.Load:
                _transcriber.Load(model.Path!, model.Model!.Id, model.UseGpu);
                break;

            case SpeechModelAction.Fetch:
                // Selected but not on disk, so fetch it.
                _logger.LogInformation("{Model} is selected but not installed; fetching it", model.Model!.Id);

                _transcriber.Unload();
                _ = FetchModelAsync(model.Model);
                break;

            default:
                // Unload, not Dispose: this runs on every listening.* change, and the host keeps one
                // transcriber for the life of the process.
                _transcriber.Unload();
                break;
        }

        // Deferred to the end, because writing a setting raises Changed, which re-enters this method: doing
        // it above would run the microphone and key work twice on one apply.

        var boundKey = _pushToTalk.Bind(listening.PushToTalkKey);

        // And the stick (Phase 53).
        var boundButton = _pushToTalkButton.Bind(
            D47.Core.Hotas.HotasButton.Parse(listening.PushToTalkButton));

        // Cancel's stick button, rebound on the same apply (#221).
        _cancelButton.Bind(
            D47.Core.Hotas.HotasButton.Parse(Settings.Current.Speech.CancelButton));

        // Whether that stick is actually here is asked from the tick, not from here (#45).

        var bound = boundKey || boundButton;

        if (!ListeningWiring.NeedsMicrophone(listening.Mode, bound))
        {
            // No key and nothing that opens the gate by itself, so no microphone. d47 opening an input device
            // it will never read from is exactly the surprise the unset default exists to avoid.
            _microphone.Close();
            Listening.Capturing = false;
            return;
        }

        _microphone.Open(listening.InputDevice);
        Listening.Capturing = _microphone.IsCapturing;

        if (!bound)
        {
            // Hands free with no key bound is a legitimate configuration, and the collision check below has
            // nothing to check.
            return;
        }

        if (boundKey && Binds.Using(listening.PushToTalkKey!) is { Count: > 0 } collisions)
        {
            // Logged at startup as well as answered on request: the symptom of a double-bound key is that
            // nothing happens, which reads as d47 being broken.
            _logger.LogWarning(
                "Push-to-talk {Key} is also bound in Elite ({Preset}) to {Actions}; one of the two will not work",
                listening.PushToTalkKey,
                Binds.PresetName,
                string.Join(", ", collisions.Select(binding => binding.Action).Distinct()));
        }

        if (_pushToTalkButton.Bound is { } button
            && Binds.UsingJoystickButton(button.Button) is { Count: > 0 } sharing)
        {
            // Hedged, and the hedge is the accurate part.
            _logger.LogWarning(
                "Push-to-talk {Button} may collide: Elite ({Preset}) binds a button of that number to "
                + "{Actions}. D47 cannot tell whether that is the same controller.",
                button.Describe(),
                Binds.PresetName,
                string.Join(", ", sharing.Select(binding => binding.Action).Distinct()));
        }
    }

    /// <summary>The stick bound to push-to-talk is not here (Phase 53).</summary>
    private void WarnIfTheStickIsMissing()
    {
        // Not while the readers are still enumerating: a single enumeration at startup reported three of six
        // devices on the bench, which is the whole of Phase 21's finding 1, and a warning raised then would
        // be wrong more often than right.
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

    /// <summary>Puts what the microphone is doing in front of the Commander, on both surfaces.</summary>
    private void ShowSwitches(string? against) => Panel.SwitchesText = against;

    /// <summary>Carries out whatever the reconciler decided this tick.</summary>
    private void CarryOutReconciles(SwitchReconciler reconciler, IGameInput input)
    {
        var pending = reconciler.Drain();

        if (pending.Count == 0)
        {
            return;
        }

        // The ones that move the panel rather than the ship go elsewhere: to each surface on its own thread,
        // and outside _acting, because a page move holds no keys for a honk to collide with (Phase 46).
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

                        // The Commander flipped a switch and is watching for the thing to happen, so a
                        // refusal that stayed in the log would look like the feature not working.
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
                // Unconditional, like everywhere else that presses a key.
                input.ReleaseAll();
                _acting.Release();
            }
        });
    }

    private void ShowMicrophone(MicrophoneState state)
    {
        Panel.Microphone = state;

        var listening = Settings.Current.Listening;

        // Describing a key is the App's business — Core has no keyboard — so the renderer is passed down and
        // the sentence is chosen in Core, where a test reads what a Commander reads.
        var gesture = ListeningCapability.PushToTalkGesture(listening, Input.Gestures.Describe);

        Panel.MicrophoneDetail = MicrophoneNarration.For(
            state,
            listening.Mode,
            Wake.Phrases,
            gesture,
            listening.PreRollMilliseconds);

        // The same three facts, worded for a prompt that is waiting on one (remediation.md 10, item 12).
        Panel.ListeningPrompt = MicrophoneNarration.Prompt(
            listening.Mode,
            Wake.Phrases,
            ListeningCapability.PushToTalkGesture(
                listening,
                Input.Gestures.Describe,
                nameTheButton: false));
    }

    /// <summary>Points the wake-word policy at whatever d47 currently answers to.</summary>
    private void ApplyWakeWords()
    {
        var listening = Settings.Current.Listening;

        Wake.Window = TimeSpan.FromSeconds(listening.WakeWindowSeconds);

        Wake.Phrases = ListeningWiring.WakePhrases(listening.Mode, listening.WakeWords, Personas.ShipName);
    }

    /// <summary>Says out loud that the model is not usable, if there is a voice to say it with.</summary>
    public async Task AnnounceStartupProblemsAsync()
    {
        if (StartupError is { } settingsError)
        {
            Voice.EnterState(Core.Audio.LoopState.Failed);
            // "Did not load cleanly" rather than "could not be loaded": since #368 this carries a kept
            // unknown key as well as a refusal, and the file it names may have loaded fine.
            await Voice.AnnounceAsync($"My settings did not load cleanly. {settingsError}")
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

    /// <summary>Guards the callout speaker.</summary>
    private readonly SemaphoreSlim _speaking = new(1, 1);

    /// <summary>The voice provider in use.</summary>
    private readonly Dictionary<string, ITtsProvider> _clients = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// What each slot actually speaks through: a thin metering decorator over one of the shared clients
    /// above, or null for a slot on "none".
    /// </summary>
    private readonly Dictionary<VoiceGroup, ITtsProvider?> _slots = new();

    /// <summary>Which client speaks for a slot.</summary>
    private ITtsProvider? Speaker(VoiceGroup group) => _slots.GetValueOrDefault(group);

    /// <summary>
    /// Whether a line written for this slot may carry delivery direction — asked of the client that
    /// will speak it, never of the settings (#291).
    /// </summary>
    private bool DirectableIn(VoiceGroup group) => Speaker(group)?.ReadsAudioTags == true;

    /// <summary>
    /// Which provider each slot is on, and whether it had its key last time speech settings were
    /// applied.
    /// </summary>
    private SpeechWiringState _speechWiring = SpeechWiringState.Nothing;

    /// <summary>Everyone d47 can speak as (Phase 11).</summary>
    public VoiceCasting Casting { get; } = new();

    /// <summary>The cast aboard the ship.</summary>
    public VoiceCast Cast => Casting.Of(VoiceGroups.ProviderFor(Settings.Current.Speech, VoiceGroup.Aboard));

    /// <summary>What each provider in use offers, cached.</summary>
    private readonly Dictionary<string, VoiceCatalogue> _voicesByProvider = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>What one provider offers, or nothing if it has not answered yet.</summary>
    private VoiceCatalogue VoicesOf(string providerId) =>
        _voicesByProvider.GetValueOrDefault(providerId) ?? VoiceCatalogue.Silent;

    /// <summary>What one slot's provider offers.</summary>
    private VoiceCatalogue VoicesFor(VoiceGroup group) =>
        VoicesOf(VoiceGroups.ProviderFor(Settings.Current.Speech, group));

    /// <summary>The ship's own provider's list.</summary>
    private VoiceCatalogue AboardVoices => VoicesFor(VoiceGroup.Aboard);

    /// <summary>What the language-model endpoint last said it serves (Phase 29).</summary>
    private volatile IReadOnlyList<string> _endpointModels = [];

    /// <summary>Which provider and address that list came from, so it is asked once each.</summary>
    private volatile string? _endpointModelsFor;

    /// <summary>What the voices have cost this session (Phase 19).</summary>
    public SpeechSpend SpeechSpend { get; } = new();

    /// <summary>
    /// Auditions already paid for, keyed by the provider that issued the voice, the role being cast and
    /// the voice itself (Phase 19).
    /// </summary>
    private readonly Dictionary<(string Provider, string Voice), AudioClip> _auditions = new();

    /// <summary>The group auditions play in, so a second one drops the first mid-word.</summary>
    private const string AuditionGroup = "voice-audition";

    /// <summary>
    /// Speaks one voice so it can be judged before it is chosen (Phase 19, "Hear a voice before you
    /// choose it").
    /// </summary>
    internal async Task AuditionVoiceAsync(string voiceId, VoiceRole role, CancellationToken cancellationToken)
    {
        // The slot the role belongs to, so the carrier's tower is auditioned through whoever speaks for the
        // carrier — and billed to that slot (Phase 57).
        var group = VoiceGroups.Of(role);

        if (Speaker(group) is not { } provider)
        {
            throw new InvalidOperationException("No voice provider is selected.");
        }

        // Before the synthesis rather than after it, so pressing the button twice in a row silences the first
        // attempt while the second is still being fetched — which on a paid provider is most of the wait.
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

            // Cached after the await, so a cancelled or failed synthesis caches nothing and the next press
            // tries again.
            _auditions[key] = clip;
        }

        cancellationToken.ThrowIfCancellationRequested();

        Audio.Enqueue(new AudioRequest
        {
            Channel = AudioChannel.Speech,
            Clip = clip,
            Group = AuditionGroup,
            // The clip's name is the text it was synthesised from, which is what the caption layer wants — so
            // an audition is captioned in the headset like any other speech.
            Caption = clip.Name,
        });
    }

    /// <summary>One autonomous action at a time.</summary>
    private readonly SemaphoreSlim _acting = new(1, 1);

    /// <summary>Carries out whatever the autonomous actions decided this tick.</summary>
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

                        // Nobody asked for this, so nobody is watching for it to fail.
                        if (!result.Sent)
                        {
                            // Through SayAsync rather than straight at the synthesiser (remediation.md 17,
                            // item 4).
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
                // Unconditional, like everywhere else that presses a key.
                input.ReleaseAll();
                _acting.Release();
            }
        });
    }

    /// <summary>Takes whatever the callouts queued this tick and says it.</summary>
    private readonly D47.Core.Callouts.SpokenReferent _referent = new();

    /// <summary>Whether the next carrier exchange may make his owning it the subject (#88).</summary>
    private readonly NpcChatterOwnershipSpotlight _carrierSpotlight = new();

    /// <summary>The systems a line could be about: where the Commander is, and where they are going.</summary>
    private string[] SystemsIn(string text) =>
        [.. new[] { GameState.Active?.Location.StarSystem, Route.Hops.LastOrDefault()?.StarSystem }
            .Where(name => name is { Length: > 0 }
                && text.Contains(name, StringComparison.OrdinalIgnoreCase))
            .Select(name => name!)];

    private async Task SayAsync(Announcement announcement)
    {
        // The voice takes the pronoun; everything written below keeps the name, so a Commander scrolling back
        // can always see which system "it" was.
        announcement = announcement with
        {
            Text = _referent.Speak(announcement.Text, SystemsIn(announcement.Text), DateTimeOffset.Now),
        };

        // Drawn from the cast belonging to whoever speaks for this slot.
        var cast = Casting.Of(VoiceGroups.ProviderFor(
            Settings.Current.Speech,
            VoiceGroups.Of(announcement.Voice, announcement.CommsChannel)));

        var voice = announcement.Speaker is { Length: > 0 } speaker
            ? cast.ForSender(speaker, announcement.SpeakerIsPlayer, announcement.Voice)
            : cast.For(announcement.Voice);

        // Written before it is spoken, and whether or not the speaking works: a message that could not be
        // synthesised is still a message that arrived. **Into the log, on the Commander's instruction**
        // (#264): "In-game comms should appear in the Log File - voice related stuff." It was an event onto
        // the Technical reading until #260 deleted that page, and putting it in the conversation was tried
        // and drew badly - that page is bubbles, so a station's line arrived in d47's own voice and merged
        // into whatever it had just said.
        if (announcement.Transcript is { Length: > 0 } line)
        {
            Comms.LogInformation("{Message}", line.TrimEnd());
        }
        else if (announcement.ConversationLine is { Length: > 0 } spoken)
        {
            // The ship's AI, saying something no turn produced — which is exactly what Said is for, and what
            // callouts were never routed through.
            Said?.Invoke(spoken);

            // **And into the conversation, not only onto the page** (remediation.md 17, item 4).
            Turns.Said(spoken);
        }

        await Voice.AnnounceAsync(announcement, voice).ConfigureAwait(false);
    }

    /// <summary>How long a carrier line may spend being written before the authored one is used instead.</summary>
    private static readonly TimeSpan FlavourBudget = TimeSpan.FromSeconds(3);

    /// <summary>
    /// The same announcement, said in character, when there is a model to ask and it is one of the
    /// lines the checklist wants varied (Phase 11: "with varied LLM arrival and departure responses").
    /// </summary>
    private async Task<Announcement?> VaryAsync(Announcement announcement)
    {
        // Which lines are eligible and what each is asked lives in Core, where the one property that matters
        // — that a danger callout is never rewritten — can be asserted.
        if (Turns.Provider is null
            || FlavourBriefs.For(announcement, Settings.Current.Llm.PersonalityEnabled) is not { } brief)
        {
            return announcement;
        }

        using var budget = new CancellationTokenSource(FlavourBudget);

        // What the ship can prove about itself, read once and used for both the model's line and the authored
        // fallback below (#338).
        var facts = ShipFacts.Of(GameState.Active);

        // Named because #338's retry asks the same question again with the contradiction appended, and only
        // the instruction differs.
        Task<string?> AskAsync(string ask) => FlavourTurn.AskAsync(
            Turns.Provider,
            Turns.BackgroundModel,
            brief.NeedsPersona ? Personas.RenderBlock(personalityEnabled: true) : brief.Speaker,
            StoryFor(brief),
            ask,
            brief.NeedsGameState ? Turns.LiveGameState?.Invoke() : null,
            Spend,
            PriceTable.Default,
            _logger,
            budget.Token,

            // Against the slot this line will be spoken in, not the ship's.
            canBeDirected: DirectableIn(VoiceGroups.Of(announcement.Voice, announcement.CommsChannel)));

        var line = await AskAsync(brief.Instruction).ConfigureAwait(false);

        // The authored line stands unless the rewrite is one that may be spoken.
        var said = await ContradictedClaims.SayableAsync(
            line,
            facts,
            contradiction => AskAsync($"{brief.Instruction} {contradiction.Correction}"),
            _logger,
            announcement.Key).ConfigureAwait(false);

        if (said is not null)
        {
            return announcement with { Text = said };
        }

        // The authored line is the fallback and is checked too, because an authored line that contradicts the
        // ship is the incident this guard was reported for.
        return ContradictedClaims.Sayable(announcement.Text, facts, _logger, announcement.Key) is null
            ? null
            : announcement;
    }

    /// <summary>How long an exchange may spend being written.</summary>
    private static readonly TimeSpan ChatterBudget = TimeSpan.FromSeconds(10);

    /// <summary>
    /// The invented exchange a chatter marker asked for (#244), as one announcement per parsed line.
    /// </summary>
    private async Task<IReadOnlyList<Announcement>> ComposeNpcChatterAsync(Announcement marker)
    {
        if (Turns.Provider is null || !Settings.Current.Llm.PersonalityEnabled)
        {
            return [];
        }

        var kind = NpcChatter.KindOf(marker.Key);
        var location = GameState.Active?.Location;
        var docked = location?.Docked ?? false;

        // The kind was picked from the Docked flag when the marker was made; the exchange is composed
        // later. A controller needs a dock to be at — one lifted off in between is worse than silence
        // (#43).
        if (kind == NpcChatterKind.Controller && !docked)
        {
            return [];
        }

        var carrier = NpcChatterCarrier.Of(GameState.Active?.Carrier, location);
        var spotlight = _carrierSpotlight.Claim(carrier.Present);

        using var budget = new CancellationTokenSource(ChatterBudget);

        var script = await FlavourTurn.AskAsync(
            Turns.Provider,
            Turns.BackgroundModel,
            NpcChatter.Speaker,
            null,
            NpcChatter.Instruction(kind, carrier, docked, spotlight, marker.Variant ?? 0),
            Turns.LiveGameState?.Invoke(),
            Spend,
            PriceTable.Default,
            _logger,
            budget.Token,
            canBeDirected: DirectableIn(VoiceGroup.Npcs)).ConfigureAwait(false);

        var facts = ShipFacts.Of(GameState.Active);
        var heard = new List<Announcement>();

        foreach (var line in NpcChatter.Parse(script, kind, carrier))
        {
            // **Per line rather than per exchange** (#338).
            var said = ContradictedClaims.AboutTheCommandersShip(line.Text)
                ? await ContradictedClaims.SayableAsync(
                    line.Text,
                    facts,
                    async contradiction => NpcChatter.Rewritten(
                        await FlavourTurn.AskAsync(
                            Turns.Provider,
                            Turns.BackgroundModel,
                            NpcChatter.Speaker,
                            null,
                            ContradictedClaims.Rewrite(line.Text, contradiction),
                            Turns.LiveGameState?.Invoke(),
                            Spend,
                            PriceTable.Default,
                            _logger,
                            budget.Token,
                            canBeDirected: DirectableIn(VoiceGroup.Npcs)).ConfigureAwait(false),
                        line.Role,
                        carrier),
                    _logger,
                    NpcChatter.LineKey).ConfigureAwait(false)
                : line.Text;

            if (said is null)
            {
                continue;
            }

            heard.Add(new Announcement(NpcChatter.LineKey, said)
            {
                Urgency = CalloutUrgency.Routine,
                Voice = line.Role ?? D47.Core.Audio.VoiceRole.Comms,
                Speaker = line.Name,
                SpeakerIsPlayer = false,
                CommsChannel = "npc",
            });
        }

        return heard;
    }

    /// <summary>Position 4 for a flavour line, to the depth the brief asked for (Phase 43).</summary>
    private string? StoryFor(FlavourBrief brief) =>
        brief.NeedsAboutMe
            ? CommanderStory.Compose(
                Settings.Current.Llm.CharacterSheet, Settings.Current.Llm.AboutMe, withStory: brief.NeedsStory)
            : null;

    /// <summary>Whether a web lookup could actually be run right now.</summary>
    private bool CanSearch =>
        Settings.Current.Llm.WebSearch && Turns.Provider is not null && SearchReachesTheWeb;

    /// <summary>
    /// The endpoint half on its own — whether the provider and model in use offer a server-side search
    /// at all, ignoring whether the Commander has asked for one.
    /// </summary>
    private bool SearchReachesTheWeb =>
        Turns.Provider is not { } provider
        || provider.CapabilitiesFor(Turns.Model ?? provider.DefaultModel).SupportsWebSearch;

    /// <summary>The same lore remark, told that nothing further is coming when nothing further can.</summary>
    private Announcement Owing(Announcement announcement) =>
        LoreCallout.AddressOf(announcement.Key) is not null
        && Settings.Current.Callouts.Lore == LoreRemarks.Lookup
        && !CanSearch
            ? announcement with { Text = $"{announcement.Text} {LoreLookup.CannotSearch}" }
            : announcement;

    /// <summary>
    /// One web search about a system, for the notes window — the same call the arrival lookup makes, so
    /// a note is corroborated by exactly what a Commander would have heard.
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
    /// The second half of an arrival remark: a web search, and what it found (Phase 23, "Look it up,
    /// and say where the answer came from").
    /// </summary>
    private void LookUpLore(Announcement announcement)
    {
        if (LoreCallout.AddressOf(announcement.Key) is not { } address
            || Settings.Current.Callouts.Lore != LoreRemarks.Lookup
            || !CanSearch)
        {
            return;
        }

        // The name as the journal spelled it, taken now rather than when the answer lands: by then the
        // Commander may be somewhere else, and this is the system being asked about.
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

                // No persona block.
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

            // Dropped rather than spoken when the Commander has moved on.
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

    /// <summary>A turn the Commander addressed to a crew member.</summary>
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
    /// Whether this input was addressed to somebody in the fighter bay rather than to the ship's AI,
    /// and if so, everything needed to answer as them (Phase 11, "Ship Crew").
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

        // Not a Guardian core.
        Turns.Persona = CrewAddressing.Brief(addressed.Member, GameState.Active?.Ship.Name);
        Voice.Voice = Cast.ForSender(addressed.Member.Name, isPlayer: false, VoiceRole.Crew);

        // And the caption says who is answering (#201).
        Voice.CaptionSpeaker = addressed.Member.Name;

        return new CrewTurn(this, addressed, persona, voice, captionSpeaker);
    }

    private string? _voiceScopeSystem;

    /// <summary>Drops the NPC voice assignments when the Commander arrives somewhere new.</summary>
    private void FollowSystemForVoices()
    {
        // The Commander's own name, so their own messages are not read back to them.
        if (GameState.Active?.Identity.Name is { Length: > 0 } commander)
        {
            foreach (var callout in Callouts.Callouts.OfType<IncomingMessages>())
            {
                callout.CommanderName = commander;
            }
        }

        // And their own carrier, so its traffic comes in the tower's voice (#28).
        if (GameState.Active?.Carrier is { } carrier)
        {
            foreach (var callout in Callouts.Callouts.OfType<IncomingMessages>())
            {
                callout.CarrierName = carrier.Name;
                callout.CarrierCallSign = carrier.CallSign;

                // The third key, and the one that is known before the dock (#109).
                callout.CarrierDisplayName = carrier.DisplayName;
            }
        }

        var system = GameState.Active?.Location.StarSystem;

        if (system is null || string.Equals(system, _voiceScopeSystem, StringComparison.Ordinal))
        {
            return;
        }

        // Not on the first sample.
        if (_voiceScopeSystem is not null)
        {
            Casting.EnteredSystem();
        }

        _voiceScopeSystem = system;
    }

    /// <summary>Sounds what came due, and says which (Phase 24, "A timer says its own name").</summary>
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

                    // Captioned like the warnings are (#201).
                    Caption = Core.Audio.AlertCues.Caption(Core.Audio.AlertCue.TimerElapsed),
                });
            }

            var said = missed ? reminder.AnnounceMissed(zone) : reminder.Announce();

            _ = Voice.AnnounceAsync(said);

            Said?.Invoke(said);

            // A timer going off is d47 speaking unasked, like a callout (remediation.md 17, item 4). "Why did
            // you just say that?" has to be answerable about this too.
            Turns.Said(said);
        }
    }

    /// <summary>
    /// A line d47 says because the panel asked it to - a generator's reply, a refusal - spoken, shown,
    /// and recorded exactly as a timer going off is (Phase 47). "Why did you just say that?" has to be
    /// answerable about this too.
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

        // Somebody else's words, written down in the session record and extracted from by nothing (#162).
        foreach (var message in pending.Where(announcement =>
                     announcement.Key.StartsWith("message.", StringComparison.Ordinal)))
        {
            NoteHeardFromOutside(message.Text);
        }

        _ = Task.Run(async () =>
        {
            // Varied before the lock is taken, never while holding it.
            var lines = new List<Announcement>(pending.Count);

            foreach (var announcement in pending)
            {
                // Invented chatter is composed rather than varied (#244): the marker carries no text of its
                // own, and the exchange arrives back as one announcement per line, each in an invented voice.
                if (announcement.Key.StartsWith(NpcChatter.KeyPrefix, StringComparison.Ordinal))
                {
                    lines.AddRange(await ComposeNpcChatterAsync(announcement).ConfigureAwait(false));
                    continue;
                }

                var varied = await VaryAsync(announcement).ConfigureAwait(false);

                // Nothing true left to say (#338): the model's line and the authored one both contradicted
                // what the ship knows about itself, and both were logged on the way out.
                if (varied is null)
                {
                    continue;
                }

                // An ambient remark the model did not write is not spoken (#245).
                if (ReferenceEquals(varied, announcement)
                    && announcement.Key.StartsWith(AmbientCallout.KeyPrefix, StringComparison.Ordinal))
                {
                    continue;
                }

                lines.Add(varied);
            }

            // Whether the lore remarks in this batch are owed a second part, decided here rather than inside
            // the speaking loop: a Commander who asked for a lookup the endpoint cannot run is told so in the
            // first sentence, and the alternative is leaving them waiting for something that was never coming
            // (Phase 23).
            lines = [.. lines.Select(Owing)];

            // One at a time, and in order.
            await _speaking.WaitAsync().ConfigureAwait(false);

            try
            {
                var beat = 0;

                foreach (var announcement in lines)
                {
                    // The arbiter's drop only reaches what is already queued, and the rest of an exchange
                    // is synthesised after it, so it is abandoned here instead (#61).
                    if (announcement.Key == NpcChatter.LineKey && Voice.Engaged)
                    {
                        continue;
                    }

                    // Air between the lines of an exchange (#259), reported as two people never once leaving
                    // a gap.
                    if (announcement.Key == NpcChatter.LineKey)
                    {
                        await HoldTheBeatAsync(NpcChatter.Beat(beat++)).ConfigureAwait(false);
                    }
                    else
                    {
                        beat = 0;
                    }

                    await SayAsync(announcement).ConfigureAwait(false);

                    // What the Commander actually heard about a story, kept (asked for 2026-08-22).
                    RecordAdventure(announcement);

                    // After the fact has been spoken, and not awaited: the search is a round trip through
                    // somebody else's index, and the rest of this batch is where a danger callout would be
                    // waiting.
                    LookUpLore(announcement);
                }
            }
            catch (Exception ex)
            {
                // A callout that cannot be synthesised is a callout the Commander does not hear.
                _logger.LogError(ex, "A callout could not be spoken");
            }
            finally
            {
                _speaking.Release();
            }
        });
    }

    /// <summary>
    /// The pause in front of a line of an invented exchange (#259), taken in slices so it can be
    /// abandoned.
    /// </summary>
    private async Task HoldTheBeatAsync(TimeSpan beat)
    {
        var slice = TimeSpan.FromMilliseconds(100);

        for (var held = TimeSpan.Zero; held < beat; held += slice)
        {
            if (Callouts.AnythingUrgentWaiting || Voice.Engaged)
            {
                return;
            }

            await Task.Delay(slice < beat - held ? slice : beat - held).ConfigureAwait(false);
        }
    }

    /// <summary>A beat, as it was said, onto the story's own feed (asked for 2026-08-22).</summary>
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

            // Stored rather than derived later: a story edited after a beat has fired would otherwise
            // re-describe what the Commander did with the trigger it has now.
            Trigger = reached?.Trigger.Describe(),
        });
    }

    /// <summary>One exchange, filed against any story it was about (asked for 2026-08-22).</summary>
    public void NoteTurn(string? asked, string? answered)
    {
        // The debrief's record, and it is written here rather than at the panel for one reason: this is the
        // single call site that has both halves of a turn with the speakers already told apart.
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
    /// message read aloud, a quoted search result (#162).
    /// </summary>
    public void NoteHeardFromOutside(string text)
    {
        if (Settings.Current.Debrief.Enabled)
        {
            Debriefing.Say(DateTimeOffset.Now, DebriefSpeaker.Game, text);
        }
    }

    /// <summary>Records that d47 was stopped mid-sentence (#162).</summary>
    public void NoteInterrupted() => NoteSignal(new DebriefSignal(
        DateTimeOffset.Now,
        DebriefSignalKind.SpeechCutOff,
        "you stopped me while I was talking"));

    /// <summary>Records that a callout was switched off within seconds of it speaking (#162).</summary>
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
    /// Opens a directions session over what the file says right now, and puts the block into the prompt
    /// (#162).
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

        // Position 3 is rebuilt too, because a per-core direction rides in the persona block: the overlay
        // lives beside the Commander's other data and the pack is never touched (#162).
        ApplyPersonaBlock();

        _logger.LogInformation(
            "Standing directions latched: {Count} adopted, {Bytes} characters at position 6",
            _directions.Latched.Count,
            Turns.Directions?.Length ?? 0);
    }

    /// <summary>Position 3, with this core's overlay behind it (#162).</summary>
    private void ApplyPersonaBlock()
    {
        var block = Personas.RenderBlock(Settings.Current.Llm.PersonalityEnabled);

        if (block is not null && _directions.Overlay(Personas.Current.Id) is { } overlay)
        {
            block = block + "\n\n" + overlay;
        }

        Turns.Persona = block;
    }

    /// <summary>Runs the debrief over what this session sounded like, and files what it drafted (#162).</summary>
    /// <param name="frontierId">Who the session belonged to.</param>
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

                // What this installation answers to, so "hey Warden, stop calling it that" reads as an
                // instruction rather than as a sentence beginning with a name.
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
            // Losing a debrief costs a list nobody had agreed to.
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
        // Which subsystem a key reaches is decided in Core, where every row can be asserted against the one
        // it is supposed to re-apply.
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
                // Straight onto the arbiter, which re-levels whatever is already playing.
                Audio.Mix = Settings.Current.Audio;

                // Muting the ambience stops it rather than playing it at nothing, and unmuting it starts a
                // track rather than waiting for the next time the Commander docks — which could be an hour,
                // and reads as a switch that did not work.
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

        // After the apply, as it was when this was an if/else chain.
        if (fanout.ChooseVoiceForCoreAboard)
        {
            _ = EnsureVoiceForCurrentPersonaAsync();
        }
    }

    /// <summary>The secret store is the real home for a key.</summary>
    private static readonly TimeSpan KeyCheckBudget = TimeSpan.FromSeconds(20);

    /// <summary>
    /// Tries the stored language-model key for real (Phase 16, "a key is verified, not merely stored").
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

        // An OpenAI-shaped endpoint is checked by asking it what it serves rather than by spending a turn on
        // it (Phase 29), and that is the same reasoning the speech key check follows: it proves the exact
        // call d47 makes anyway rather than a proxy for it.
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
                // Reached and refused.
                EndpointReach.Refused => SecretCheck.Rejected(asked.Detail ?? $"{selected.Name} refused the request."),

                EndpointReach.Answered when asked.Ids.Count > 0 => SecretCheck.Works(
                    $"{selected.Name} answered — {asked.Ids.Count} models."),

                // Answered with an empty catalogue, which is a gateway's prerogative and not a fault.
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

            // Nothing said about sampling, on purpose (#98).
            Sampling = LlmSampling.Unstated,

            // Enough room to say one word, rather than exactly one token.
            MaxOutputTokens = 64,
        };

        try
        {
            await foreach (var step in provider.StreamAsync(request, budget.Token).ConfigureAwait(false))
            {
                // A failure the provider itself classified.
                if (step is LlmStreamEvent.Failed failure)
                {
                    return failure.Transient
                        ? SecretCheck.Unreachable(failure.Message)
                        : SecretCheck.Rejected(failure.Message);
                }
            }

            // Reaching the end of the stream without a failure is the provider having accepted the key.
            return SecretCheck.Works($"{selected.Name} accepted the key.");
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return SecretCheck.Unreachable($"{selected.Name} did not answer within {KeyCheckBudget.TotalSeconds:0} seconds.");
        }
        catch (Exception ex)
        {
            // Never the key, at any level — the message is the exception's and the exception never held it.
            _logger.LogWarning(ex, "The {Provider} key check could not be completed", selected.Name);
            return SecretCheck.Unreachable(ex.Message);
        }
    }

    /// <summary>
    /// Tries the stored speech key for real, against the provider's own voice list — which is the call
    /// d47 makes anyway the moment a key lands, so this proves the exact thing that has to work rather
    /// than a proxy for it.
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

        // Its own instance rather than the live one, so a refusal surfaces here as a verdict instead of being
        // swallowed by the background refresh's catch.
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

        // A provider whose catalogue is static cannot be checked by listing it: the list is known without a
        // key, so it would answer "accepted the key" for a key that had never left this machine.
        if (selected.VoicesAreStatic)
        {
            return await ProveSpeechKeyAsync(provider, selected, budget.Token).ConfigureAwait(false);
        }

        try
        {
            var voices = await provider.ListVoicesAsync(budget.Token).ConfigureAwait(false);

            // Read from the listing rather than from the count, which is what this check was getting wrong
            // without saying so: the provider answers an empty list rather than throwing, so a rejected key
            // arrived here as "accepted the key — 0 voices" (Phase 19).
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

    /// <summary>Proves a key by speaking one character and throwing the audio away (Phase 58).</summary>
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
            // Everything else is the network's problem rather than the key's, which is what the Commander
            // needs to know: there is nothing here for them to change.
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

    /// <summary>Where Elite might be installed, for the shipped control presets.</summary>
    private static string? Join(string? situation, string? actions) =>
        (situation, actions) switch
        {
            (null, null) => null,
            (null, var only) => only,
            (var only, null) => only,
            var (both, and) => both + Environment.NewLine + Environment.NewLine + and,
        };

    /// <summary>Waits for Status.json to report the galaxy map showing, or no longer showing.</summary>
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

    /// <summary>The next status sample, which is what the boost loop watches (Phase 52).</summary>
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

    /// <summary>Every phrase d47 already answers to.</summary>
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

    /// <summary>
    /// <param name="dynamicCommands"> The phrases already claimed by dynamic commands — loaded macros,
    /// a standing clipboard offer, the carrier course, the gap questions and the Community Goal search
    /// — so a new macro sharing one is refused at load time like every other phrase collision already
    /// is (#319).
    /// </summary>
    /// <param name="dynamicCommands">
    /// The phrases already claimed by dynamic commands — loaded macros, a standing clipboard offer, the
    /// carrier course, the gap questions and the Community Goal search — so a new macro sharing one is
    /// refused at load time like every other phrase collision already is (#319).
    /// </param>
    private static IReadOnlyList<string> PhrasesAlreadyTaken(
        CapabilityRegistry? registry, IEnumerable<DynamicCommand> dynamicCommands) =>
        registry is null
            ? []
            : [
                .. registry.All.SelectMany(c => c.Descriptor.Keywords).Select(keyword => keyword.Phrase),
                .. registry.All.SelectMany(c => c.Descriptor.InterruptKeywords),
                .. registry.All.SelectMany(c => c.Descriptor.Tools).SelectMany(t => t.Commands)
                    .Select(command => command.Phrase),
                .. registry.All.SelectMany(c => c.Descriptor.Settings).SelectMany(row => row.Commands)
                    .Select(command => command.Phrase),
                .. dynamicCommands.Select(command => command.Phrase),
                .. D47.Core.Conversation.ClipboardOffer.EveryPhrase,
                D47.Core.Capabilities.Builtin.CommunityGoalCourse.SetCourse,
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
    /// The real Elite Dangerous journal folder, unless overridden — useful for developing and testing
    /// d47 without needing a live game session.
    /// </summary>
    private static string ResolveJournalDirectory()
    {
        var overridePath = Environment.GetEnvironmentVariable("D47_JOURNAL_DIR");
        return string.IsNullOrWhiteSpace(overridePath) ? JournalFolder.DefaultPath() : overridePath;
    }

    /// <summary>Why d47 is stopping, for the shutdown line (remediation.md 10, item 7).</summary>
    public string StoppingBecause { get; set; } = "the process is ending";

    /// <summary>
    /// What this build is, what it is pointed at, and what came up — written once, at the moment
    /// everything that can answer has (remediation.md 10, item 7).
    /// </summary>
    public void RecordStartup()
    {
        var current = Settings.Current;

        _logger.LogInformation(
            "d47 {Version} started. Model: {Provider}/{Model}. Speech: {Speech}. "
            + "Hearing: {Whisper}, {Listening}. Headset: {Vr}. Data: {Data}",
            Version,
            LlmProviderCatalog.Selected(current.Llm.Provider)?.Name ?? current.Llm.Provider,

            // A provider with no model chosen is the state on a fresh install, and "Anthropic/null" is a line
            // that reads as a fault rather than as a setting nobody has set yet.
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
        // First, so the reason survives whatever the teardown below does.
        _logger.LogInformation("d47 {Version} is stopping: {Why}", Version, StoppingBecause);

        CoverageRecorder?.Save();

        // The debrief, over what this session sounded like (#162).
        RunDebrief();

        // When the core aboard stopped being aboard, which is now (Phase 35).
        RememberCoreAboard(Personas.Current.Id, DateTimeOffset.Now);

        Settings.Changed -= OnSettingsChanged;
        Personas.Changed -= OnPersonaChanged;
        GameState.CommanderChanged -= OnCommanderChanged;

        // The loop stops before anything it polls is torn down, so a tick cannot land on a disposed sink or a
        // closed file handle on the way out.
        _ticking?.Dispose();

        // And then let go of the game (#206).
        _gameInput.Dispose();

        // After the tick, so a serve cannot land on a destroyed overlay handle.
        Vr?.Dispose();
        _speaking.Dispose();

        // After the tick has stopped, so a poll cannot land on a disposed capture device.
        _pushToTalk.ForceUp();
        _pushToTalkButton.ForceUp();
        _cancelButton.ForceUp();
        _microphone.Dispose();
        _transcriber.Dispose();
        (Models as IDisposable)?.Dispose();

        // Before the arbiter and the sink it is subscribed to, and before the last clip stops being writable.
        AudioRecorder?.Dispose();

        // The same reasoning one line up: the last lines of a trace are the ones an attempt that ended badly
        // left behind, so the queue is drained rather than dropped (#365).
        InputTrace?.Dispose();

        // Stop making noise before tearing anything down.
        Audio.Silence();
        Audio.Dispose();
        _audioSink.Dispose();
        foreach (var client in _clients.Values)
        {
            (client as IDisposable)?.Dispose();
        }

        _clients.Clear();
        _slots.Clear();

        // Before the factory that owns the sink it writes to.
        _logger.LogInformation("d47 stopped cleanly");

        _loggerFactory.Dispose();
        Log.CloseAndFlush();
    }
}
