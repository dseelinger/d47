using System.Reflection;
using D47.App.Input;
using D47.App.Logging;
using D47.App.Ticking;
using D47.App.Updates;
using D47.App.Voice;
using D47.Audio;
using D47.Core.Audio;
using D47.Core;
using D47.Core.Capabilities;
using D47.Core.Callouts;
using D47.Core.Capabilities.Builtin;
using D47.Core.Configuration;
using D47.Core.Conversation;
using D47.Core.Diagnostics;
using D47.Core.Input;
using D47.Core.Journal;
using D47.Core.Listening;
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
        TurnLoop turns,
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
        Turns = turns;
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

    /// <summary>One turn of conversation, whichever path answers it.</summary>
    public TurnLoop Turns { get; }

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

    /// <summary>
    /// Speech models on disk, and the consent-gated way to fetch one. Exposed because the
    /// settings surface is what asks the Commander, and only a surface can ask.
    /// </summary>
    public IModelStore Models { get; }

    /// <summary>Raised when an utterance has been turned into words, so a surface can run it.</summary>
    public event Action<string>? Heard;

    /// <summary>
    /// Raised when a selected speech model is not on disk. A surface answers it by asking the
    /// Commander and calling <see cref="InstallModelAsync"/>.
    /// <para>
    /// Raised from here rather than from the settings panel so it fires however the model came
    /// to be selected — the panel, the keyword router, or a hand-edited settings file. A
    /// consent prompt that only one surface knows to show is a surface that can be gone around.
    /// </para>
    /// </summary>
    public event Action<WhisperModel>? ModelNeeded;

    /// <summary>
    /// Downloads a model, having asked. Nothing is fetched unless <paramref name="consent"/>
    /// returns true, and the offer it is given carries the real size and host rather than an
    /// estimate.
    /// </summary>
    public async Task<ModelInstallResult> InstallModelAsync(
        WhisperModel model,
        Func<ModelOffer, Task<bool>> consent,
        IProgress<ModelProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var result = await Models
            .InstallAsync(model, consent, progress, cancellationToken)
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

        var version = Assembly.GetEntryAssembly()
                          ?.GetCustomAttribute<AssemblyInformationalVersionAttribute>()
                          ?.InformationalVersion
                      ?? "unknown";

        // Logging first, so everything below has somewhere to report a failure.
        var verbosity = new SerilogVerbosityControl();
        Log.Logger = LoggingSetup.Create(paths, verbosity);
        var loggerFactory = new SerilogLoggerFactory(Log.Logger);
        var logger = loggerFactory.CreateLogger<AppHost>();

        logger.LogInformation("d47 {Version} starting; data folder {Data}", version, paths.Data);

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
        var gameState = new GameStateStore();
        var journal = new JournalSpine(journalDirectory, gameState, loggerFactory);

        // The two state files Elite rewrites in place. Same folder as the journal, different
        // shape: a log is appended to and these are replaced, which is entirely inside the
        // readers.
        var status = new GameStatusReader(journalDirectory, loggerFactory.CreateLogger<GameStatusReader>());
        var route = new NavRouteReader(journalDirectory, loggerFactory.CreateLogger<NavRouteReader>());

        var callouts = BuildCallouts(loaded, loggerFactory);

        // The ~4-10 Hz loop from architecture.md §4. Registration order is load-bearing: the
        // journal and the two state files are read first, so the callouts examining them see
        // this tick's events rather than the last tick's.
        var tick = new TickLoop(loggerFactory.CreateLogger<TickLoop>());

        tick.Add("journal", context =>
        {
            var events = journal.Poll();
            status.Poll();
            route.Poll();

            callouts.Tick(new CalloutContext(
                context.Now,
                IsPriming: context.IsFirst,
                gameState.Active,
                status.Current,
                route.Current,
                events));
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

        try
        {
            audioSink.Open(loaded.Speech.OutputDevice);
        }
        catch (Exception ex)
        {
            // No audio output is a capability being off, not a startup failure. d47 stays
            // fully usable in text (list.md Phase 3, "Capabilities as state, not guard").
            logger.LogError(ex, "No audio output could be opened; d47 will be silent");
        }

        // Listening. The microphone runs continuously into the gate and the gate decides which
        // part of that stream was addressed to d47 — push-to-talk is a policy over the stream,
        // not a reason to start and stop the device (list.md Phase 6).
        var models = new HttpModelStore(paths, loggerFactory.CreateLogger<HttpModelStore>());
        var transcriber = new WhisperTranscriber(loggerFactory.CreateLogger<WhisperTranscriber>());
        var gate = new ListenGate(WasapiMicrophone.SampleRate, loggerFactory.CreateLogger<ListenGate>());
        var microphone = new WasapiMicrophone(gate, loggerFactory.CreateLogger<WasapiMicrophone>());
        var pushToTalk = new PushToTalkKey(loggerFactory.CreateLogger<PushToTalkKey>());

        // Read once at startup. The bindings file changes only when the Commander edits their
        // controls, which they cannot do while d47 is the foreground window, so re-reading it
        // ten times a second would be polling for an event that cannot happen.
        var binds = BindsResolver.Resolve(
            BindsResolver.DefaultBindingsDirectory(),
            EliteInstallations(),
            loggerFactory.CreateLogger<AppHost>());

        var cancellation = new TurnCancellation(loggerFactory.CreateLogger<TurnCancellation>());

        // The help capability answers from the registry it is itself registered in, so the
        // accessor is filled in immediately after Build. A Func rather than a mutable property
        // on the descriptor: descriptors are registered once and never mutated (architecture.md
        // D5), and that rule is what keeps tool schemas byte-identical across turns.
        CapabilityRegistry? built = null;

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
                },
                cancellation,
                callouts,
                () => built ?? throw new InvalidOperationException(
                    "Spoken help was asked what d47 can do before the registry finished building."),
                new ListeningCapability.ListeningSurface
                {
                    InputDevices = () => [.. WasapiMicrophone.Devices().Select(device => device.Id)],
                    DeviceLabel = id => WasapiMicrophone.Devices()
                        .FirstOrDefault(device => device.Id == id).Name ?? id,
                    CaptureState = () => (microphone.IsCapturing, microphone.Unavailable),

                    TranscriberState = () => (
                        transcriber.IsReady,
                        transcriber.Model,
                        transcriber.Unavailable ?? "No speech model is selected."),
                    Binds = () => binds,
                    InstalledModels = () => models.Installed(),
                }));

        built = capabilities;

        // The one late-bound edge in the composition: descriptors declare the settings rows and
        // some descriptors read settings, so the row table is supplied once the registry exists.
        settings.Bind(capabilities);

        logger.LogInformation(
            "Registered {Count} capabilities exposing {ToolCount} tools",
            capabilities.All.Count,
            capabilities.ToolNames.Count());

        var updates = new UpdateChecker(loggerFactory.CreateLogger<UpdateChecker>());

        var router = new KeywordRouter(capabilities);

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
            LiveGameState = () => Situation.Describe(gameState.Active),
        };

        var host = new AppHost(
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
            turns,
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

        host.ApplyLlmSettings();
        host.ApplySpeechSettings();
        host.ApplyListeningSettings();

        // From here on, a setting takes effect because it changed — not because something was
        // restarted (list.md Phase 4, "Apply every setting without a restart").
        settings.Changed += host.OnSettingsChanged;

        // Captured audio becomes words on the thread pool, never on the audio thread that
        // produced it. Whisper on a CPU takes hundreds of milliseconds for a short clip; doing
        // that inline would stall capture and drop the next utterance.
        gate.Captured += host.TranscribeAsync;

        // The route reader lives in the tick closure, so the host reaches it through this
        // rather than owning it — proper-noun biasing wants the systems the Commander is about
        // to arrive in, and those are only in the route file.
        host._route = () => route.Current;

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
        tick.Add("callout-drain", _ => host.SpeakPendingCallouts());

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
            .Add(new MaterialMilestoneCallout { Capacity = MaterialGrades.CapacityOf });

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
                reason = $"d47 has no client for {selected.Name} yet.";
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

        // The persona block itself arrives in Phase 11; this is the switch it will read.
        if (!current.Llm.PersonalityEnabled)
        {
            Turns.Persona = null;
        }

        LlmAvailability.SetProviderConfigured(provider is not null, reason);
    }

    /// <summary>
    /// Rebuilds everything downstream of the speech settings: the voice provider, the voice
    /// itself, the cues, the bed, the output device and the retry policy. Called at startup and
    /// again on any change, so the two paths cannot drift (list.md Phase 4, "Apply every
    /// setting without a restart").
    /// </summary>
    private void ApplySpeechSettings()
    {
        var speech = Settings.Current.Speech;

        if (speech.Provider == SpeechCapability.NoneId)
        {
            _tts?.Dispose();
            _tts = null;
        }
        else if (_tts is null)
        {
            _tts = new EdgeNeuralTtsProvider(_loggerFactory.CreateLogger<EdgeNeuralTtsProvider>());
        }

        Voice.Tts = _tts;
        Voice.Voice = new VoiceSelection(speech.Voice, speech.Rate);
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

            _ = Voice.AnnounceAsync("I heard you, but I have no speech model loaded to understand it.");
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
        else if (WhisperModels.Find(listening.Model) is { } wanted)
        {
            // Selected but not on disk. Asked for rather than fetched: the whole point of the
            // consent gate is that this moment is where the Commander is given the choice.
            _logger.LogInformation("{Model} is selected but not installed", wanted.Id);
            ModelNeeded?.Invoke(wanted);
        }
        else
        {
            _transcriber.Dispose();
        }

        var bound = _pushToTalk.Bind(listening.PushToTalkKey);

        if (!bound)
        {
            // No key, no microphone. d47 opening an input device it will never read from is
            // exactly the surprise the unset default exists to avoid.
            _microphone.Dispose();
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

    private EdgeNeuralTtsProvider? _tts;

    /// <summary>
    /// Takes whatever the callouts queued this tick and says it. Called from the tick thread and
    /// returns immediately — the speaking itself happens on the thread pool, because the tick
    /// must never block on synthesis.
    /// </summary>
    private void SpeakPendingCallouts()
    {
        var pending = Callouts.Drain();

        if (pending.Count == 0)
        {
            return;
        }

        _ = Task.Run(async () =>
        {
            // One at a time, and in order. A previous batch still being spoken holds this one
            // until it finishes rather than talking over it.
            await _speaking.WaitAsync().ConfigureAwait(false);

            try
            {
                foreach (var announcement in pending)
                {
                    await Voice.AnnounceAsync(announcement).ConfigureAwait(false);
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
        Settings.Changed -= OnSettingsChanged;

        // The loop stops before anything it polls is torn down, so a tick cannot land on a
        // disposed sink or a closed file handle on the way out.
        _ticking?.Dispose();
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
        _tts?.Dispose();

        _loggerFactory.Dispose();
        Log.CloseAndFlush();
    }
}
