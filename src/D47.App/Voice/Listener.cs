using System.Runtime.CompilerServices;
using D47.App.Diagnostics;
using D47.App.Input;
using D47.App.Panel;
using D47.App.Recording;
using D47.Audio;
using D47.Core.Audio;
using D47.Core.Capabilities.Builtin;
using D47.Core.Configuration;
using D47.Core.Hotas;
using D47.Core.Input;
using D47.Core.Listening;
using D47.Stt;
using Microsoft.Extensions.Logging;

namespace D47.App.Voice;

/// <summary>
/// Hearing: the microphone, the transcribers and the speech model, push-to-talk and cancel, the wake-word
/// gate, and turning a captured utterance into words.
/// </summary>
public sealed class Listener : IDisposable
{
    private readonly SettingsService _settings;
    private readonly SecretStore _secrets;
    private readonly ILoggerFactory _loggers;
    private readonly ILogger<Listener> _logger;
    private readonly VoicePipeline _voice;
    private readonly Action<string> _said;
    private readonly Func<Core.Interface.Heard, bool> _prompted;
    private readonly Func<IReadOnlyList<string>> _properNouns;
    private readonly Func<AudioRecorder?> _recorder;
    private readonly Func<string> _shipName;
    private readonly PanelViewModel _panel;
    private readonly ListenGate _gate;
    private readonly EchoCanceller _echo;
    private readonly WasapiMicrophone _microphone;
    private readonly DefaultDeviceFollowPolicy _inputFollow;

    /// <summary>Loads the local model, and runs the no-speech probe for every provider.</summary>
    private readonly WhisperTranscriber _transcriber;

    private readonly IModelStore _models;
    private readonly BindsWatch _binds;
    private readonly PushToTalkKey _pushToTalk;

    /// <summary>The stick's half of push-to-talk (Phase 53).</summary>
    private readonly BoundButton _pushToTalkButton;

    /// <summary>When the Commander was last heard and understood.</summary>
    private readonly StrongBox<DateTimeOffset?> _heardAt;

    /// <summary>The hearing provider as of the last apply.</summary>
    private volatile SttProviderInfo _hearing = SttProviderCatalog.Local;

    /// <summary>One per hosted provider, made on first use and kept until the listener is disposed.</summary>
    private readonly Dictionary<string, ISpeechTranscriber> _hosted = new(StringComparer.Ordinal);

    /// <summary>The model currently being fetched, or null.</summary>
    private string? _fetching;

    internal Listener(
        SettingsService settings,
        SecretStore secrets,
        ILoggerFactory loggers,
        VoicePipeline voice,
        Action<string> said,
        Func<Core.Interface.Heard, bool> prompted,
        Func<IReadOnlyList<string>> properNouns,
        Func<AudioRecorder?> recorder,
        Func<string> shipName,
        PanelViewModel panel,
        ListenGate gate,
        EchoCanceller echo,
        WasapiMicrophone microphone,
        WhisperTranscriber transcriber,
        IModelStore models,
        BindsWatch binds,
        PushToTalkKey pushToTalk,
        BoundButton pushToTalkButton,
        WakeWordGate wake,
        StrongBox<DateTimeOffset?> heardAt)
    {
        _settings = settings;
        _secrets = secrets;
        _loggers = loggers;
        _logger = loggers.CreateLogger<Listener>();
        _voice = voice;
        _said = said;
        _prompted = prompted;
        _properNouns = properNouns;
        _recorder = recorder;
        _shipName = shipName;
        _panel = panel;
        _gate = gate;
        _echo = echo;
        _microphone = microphone;
        _inputFollow = new DefaultDeviceFollowPolicy(microphone);
        _transcriber = transcriber;
        _models = models;
        _binds = binds;
        _pushToTalk = pushToTalk;
        _pushToTalkButton = pushToTalkButton;
        Wake = wake;
        _heardAt = heardAt;
    }

    /// <summary>Raised when an utterance has been turned into words, so a surface can run it.</summary>
    public event Action<string>? Heard;

    /// <summary>Something the Commander said that no turn is going to write down.</summary>
    public event Action<string>? HeardText;

    /// <summary>Whether an utterance was addressed to d47 at all, in wake-word mode.</summary>
    public WakeWordGate Wake { get; }

    /// <summary>Cancel's stick button (#221).</summary>
    public BoundButton CancelButton { get; } = new();

    private EliteBinds Binds => _binds.Current;

    private void HeardAside(string text, string why)
    {
        if (text is { Length: > 0 })
        {
            HeardText?.Invoke($"{why}: {text}");
        }
    }

    /// <summary>Says a hearing problem out loud and writes it into the transcript.</summary>
    private void Problem(string problem)
    {
        _ = _voice.AnnounceAsync(problem);
        _said(problem);
    }

    /// <summary>Downloads a model and loads it.</summary>
    public async Task<ModelInstallResult> InstallModelAsync(
        WhisperModel model,
        IProgress<ModelProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var result = await _models
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

    /// <summary>
    /// Follows the Windows Default Device for the microphone while it is left on "system default". Waits
    /// for the gate to go idle unless the open device has disappeared (#67).
    /// </summary>
    public void FollowInputDevice(DateTimeOffset now)
    {
        var listening = _settings.Current.Listening;

        var inputMove = _inputFollow.Poll(
            now,
            followingDefault: string.IsNullOrEmpty(listening.InputDevice),
            configuredDeviceId: listening.InputDevice,
            isBusy: () => _gate.IsListening,
            interrupt: _gate.Reset);

        if (inputMove is not { } input)
        {
            return;
        }

        if (input.Error is { } inputError)
        {
            _logger.LogError(inputError, "Could not follow the Default Device for the microphone");
            return;
        }

        _logger.LogInformation(
            "The Default Device moved input from {Old} to {New}",
            input.OldDeviceName ?? "(none)",
            input.NewDeviceName ?? "(none)");

        if (!input.Interrupted)
        {
            return;
        }

        // The gate was reset mid-utterance rather than left to time out silently.
        Problem("I did not catch that — my microphone just moved to a different device.");
    }

    /// <summary>The transcriber for a hosted provider.</summary>
    private ISpeechTranscriber HostedTranscriber(SttProviderInfo provider)
    {
        lock (_hosted)
        {
            if (!_hosted.TryGetValue(provider.Id, out var transcriber))
            {
                var secret = provider.KeySecretName!;

                transcriber = Hearing.Hosted(
                    provider,
                    () => _secrets.TryGet(secret, out var key) ? key : null,
                    _loggers);

                _hosted[provider.Id] = transcriber;
            }

            return transcriber;
        }
    }

    /// <summary>Turns one captured utterance into words and hands them on.</summary>
    public void TranscribeAsync(Utterance utterance)
    {
        // The microphone has closed and the words are being worked out.
        _voice.EnterState(LoopState.Transcribing);

        var provider = _hearing;

        // A hosted provider is never loading, so the local model's state does not gate it.
        var hosted = provider.Hosted ? HostedTranscriber(provider) : null;

        if (hosted is null && !_transcriber.IsReady && !_transcriber.IsLoading)
        {
            NoSpeechModel(utterance);
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

            Problem(
                $"I heard nothing at all — {device} is not sending any audio. "
                + "Check it is not muted, or pick a different microphone in Settings.");

            _voice.EnterState(LoopState.Idle, cue: false);
            return;
        }

        _ = Task.Run(async () =>
        {
            try
            {
                // A press made while the model was still loading waits for it here rather than being
                // discarded (#147).
                if (hosted is null)
                {
                    await _transcriber.Ready.ConfigureAwait(false);

                    if (!_transcriber.IsReady)
                    {
                        NoSpeechModel(utterance);
                        return;
                    }
                }

                // Journal-derived and network-free.
                var nouns = _properNouns();

                // The unprompted second opinion, beside the prompted pass rather than after it (#196):
                // tiny.en answers in ~350 ms while the main pass is still working, so the gate costs nothing
                // in latency. With a hosted provider no main model is loaded, so tiny.en is looked for in
                // the models folder.
                var outcome = await Hearing.TranscribeAsync(
                        hosted ?? _transcriber,
                        provider,
                        () => hosted is null
                            ? _transcriber.NoSpeechAsync(utterance)
                            : _transcriber.NoSpeechAsync(utterance, _models.Directory),
                        utterance,
                        nouns)
                    .ConfigureAwait(false);

                if (outcome.Problem is { } problem)
                {
                    TranscriptionUnavailable(problem);
                    return;
                }

                // The exact buffer the transcriber was given, beside what it came back with (#164).
                _recorder()?.Heard(utterance, outcome.Raw!);

                var transcription = outcome.Kept!;

                // **A word hallucinated from silence is refused here** (#196).
                if (outcome.RefusedAt is { } noSpeech)
                {
                    _logger.LogInformation(
                        "Refused as no-speech: the unprompted probe read {Probability:0.###} against \"{Text}\"",
                        noSpeech,
                        outcome.Raw!.Text);
                }

                // A panel is asking for a value and this is the answer to it (Phase 25, "Say it, or type
                // it").
                if (_prompted(new Core.Interface.Heard(
                        transcription.Text, transcription.Confidence, Final: true)))
                {
                    // Written down, because nothing after this point will.
                    HeardAside(transcription.Text, "answering the question");

                    _voice.EnterState(LoopState.Idle, cue: false);
                    return;
                }

                if (transcription.IsEmpty)
                {
                    // Distinguished from a failure: the model ran and heard nothing worth reporting, which a
                    // Commander who coughed should not be told is an error.
                    _logger.LogInformation("Nothing intelligible in {Seconds:0.#}s", utterance.Duration.TotalSeconds);

                    // Without a cue, like every other path here that has nothing to say (remediation.md 14,
                    // item 8).
                    _voice.EnterState(LoopState.Idle, cue: false);
                    return;
                }

                _heardAt.Value = DateTimeOffset.Now;

                // The wake word, applied to the words rather than to the audio (Phase 13).
                var decision = Wake.Admit(transcription.Text, DateTimeOffset.Now);

                if (decision.Outcome == WakeOutcome.Ignored)
                {
                    // Somebody in the room said something that was not to d47.
                    _logger.LogDebug("Not addressed to me: {Text}", transcription.Text);
                    _voice.EnterState(LoopState.Idle, cue: false);
                    return;
                }

                if (decision.Outcome == WakeOutcome.Woken)
                {
                    // The name and nothing after it.
                    _logger.LogInformation("Woken by name; listening for what follows");

                    // The cue on its own rather than the loop state behind it.
                    _voice.Cue(LoopState.Listening);

                    _voice.EnterState(LoopState.Idle, cue: false);
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
                _voice.EnterState(LoopState.Failed);
            }
        });
    }

    /// <summary>A hosted provider gave no words: said, and back to idle without a cue.</summary>
    private void TranscriptionUnavailable(string problem)
    {
        _logger.LogWarning("Hearing failed: {Problem}", problem);

        Problem(problem);

        _voice.EnterState(LoopState.Idle, cue: false);
    }

    /// <summary>An utterance arrived with nothing to transcribe it.</summary>
    private void NoSpeechModel(Utterance utterance)
    {
        _logger.LogInformation(
            "Heard {Seconds:0.#}s but no speech model is loaded", utterance.Duration.TotalSeconds);

        Problem("I heard you, but I have no speech model loaded to understand it.");

        // No cue: a sentence is about to be spoken saying the same thing, and a chime under it is d47
        // telling the Commander twice.
        _voice.EnterState(LoopState.Idle, cue: false);
    }

    /// <summary>
    /// Loads the speech model off the calling thread, timing the step and setting the indicator while it
    /// runs (#147).
    /// </summary>
    private async Task LoadModelAsync(string path, string modelId, bool useGpu)
    {
        // Set before the load is asked for, so the clear that follows the load cannot arrive first.
        _gate.ModelLoading = true;

        var timing = StartupTimer.Step("speech model");

        try
        {
            await _transcriber.LoadAsync(path, modelId, useGpu).ConfigureAwait(false);
        }
        finally
        {
            timing.Dispose();

            // From the transcriber rather than from here, because a later request may already have
            // superseded this one.
            _gate.ModelLoading = _transcriber.IsLoading;
        }
    }

    /// <summary>Downloads a selected model that is not on disk, then loads it.</summary>
    private async Task FetchModelAsync(WhisperModel model)
    {
        if (Interlocked.CompareExchange(ref _fetching, model.Id, null) is not null)
        {
            return;
        }

        try
        {
            var result = await _models.InstallAsync(model).ConfigureAwait(false);

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

    /// <summary>Closes the listening microphone for a take of the Commander's voice; true when it was open.</summary>
    public bool PauseListening()
    {
        var open = _microphone.IsCapturing;

        if (open)
        {
            _microphone.Close();
            _gate.Capturing = false;
        }

        return open;
    }

    public void ResumeListening()
    {
        _microphone.Open(_settings.Current.Listening.InputDevice);
        _gate.Capturing = _microphone.IsCapturing;
    }

    /// <summary>
    /// Rebuilds everything downstream of the listening settings: the device, the key, the gate policy
    /// and the pre-roll.
    /// </summary>
    public void ApplyListeningSettings()
    {
        var listening = _settings.Current.Listening;

        _gate.Mode = listening.Mode switch
        {
            ListeningCapability.ToggleMode => ListenMode.Toggle,
            ListeningCapability.ContinuousMode => ListenMode.VoiceActivity,
            ListeningCapability.WakeMode => ListenMode.WakeWord,
            _ => ListenMode.PushToTalk,
        };

        _gate.PreRoll = TimeSpan.FromMilliseconds(listening.PreRollMilliseconds);
        _gate.Voice.Sensitivity = listening.Sensitivity;
        _gate.Voice.Hangover = TimeSpan.FromMilliseconds(listening.SilenceMilliseconds);

        // Started before the microphone, so the first buffer off a freshly opened device is already going
        // through it.
        if (listening.EchoCancellation)
        {
            _echo.SuppressNoise = listening.NoiseSuppression;
            _echo.Start();
        }
        else
        {
            _echo.Stop();
        }

        // From the canceller's live state rather than from the row that asked for it.
        _gate.EchoCancelled = _echo.IsActive;

        ApplyWakeWords();

        // Rebinding while the key is held would leave the gate open with nothing able to close it — the
        // listening equivalent of a stranded key.
        _pushToTalk.ForceUp();
        _pushToTalkButton.ForceUp();

        _hearing = SttProviderCatalog.Selected(listening.Provider);

        // The model, before the key. A hosted provider unloads it.
        var model = ListeningWiring.PlanModel(listening, _models);

        // Cleared here and set again by the load itself, so the two writes cannot arrive out of order.
        _gate.ModelLoading = false;

        switch (model.Action)
        {
            case SpeechModelAction.Load:
                // Off the calling thread: this runs on the startup path and again on the UI thread for every
                // listening.* change, and a medium model takes over a second to load (#147).
                _ = LoadModelAsync(model.Path!, model.Model!.Id, model.UseGpu);
                break;

            case SpeechModelAction.Fetch:
                // Selected but not on disk, so fetch it.
                _logger.LogInformation("{Model} is selected but not installed; fetching it", model.Model!.Id);

                _transcriber.Unload();
                _ = FetchModelAsync(model.Model);
                break;

            default:
                // Unload, not Dispose: this runs on every listening.* change, and the listener keeps one
                // transcriber for the life of the process.
                _transcriber.Unload();
                break;
        }

        var boundKey = _pushToTalk.Bind(listening.PushToTalkKey);

        // And the stick (Phase 53).
        var boundButton = _pushToTalkButton.Bind(HotasButton.Parse(listening.PushToTalkButton));

        // Cancel's stick button, rebound on the same apply (#221).
        CancelButton.Bind(HotasButton.Parse(_settings.Current.Speech.CancelButton));

        // Whether that stick is actually here is asked from the tick, not from here (#45).

        var bound = boundKey || boundButton;

        if (!ListeningWiring.NeedsMicrophone(listening.Mode, bound))
        {
            // No key and nothing that opens the gate by itself, so no microphone. d47 opening an input device
            // it will never read from is exactly the surprise the unset default exists to avoid.
            _microphone.Close();
            _gate.Capturing = false;
            return;
        }

        _microphone.Open(listening.InputDevice);
        _gate.Capturing = _microphone.IsCapturing;

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

    /// <summary>
    /// Polls the stick and the buttons bound to it, on settled readings only (#146). An unsettled
    /// reader returns nothing by construction, and <see cref="BoundButton"/> counts every
    /// poll it is given toward the threshold its absence notice fires at.
    /// </summary>
    internal static void PollTheStick(
        IHotasReader? reader,
        BoundButton pushToTalkButton,
        BoundButton cancelButton,
        ILogger logger)
    {
        // A single enumeration at startup reported three of six devices on the bench, which is Phase 21's
        // finding 1.
        if (reader?.IsSettled != true)
        {
            return;
        }

        var buttons = reader.Poll();

        pushToTalkButton.Poll(buttons);
        cancelButton.Poll(buttons);

        // And then, and only then, whether the stick it is bound to turned up (#45).
        WarnIfTheStickIsMissing(pushToTalkButton, logger);
    }

    /// <summary>The stick bound to push-to-talk is not here (Phase 53). Asked on settled polls only.</summary>
    private static void WarnIfTheStickIsMissing(BoundButton pushToTalkButton, ILogger logger)
    {
        if (pushToTalkButton.MissingDeviceNotice() is not { } button)
        {
            return;
        }

        logger.LogWarning(
            "Push-to-talk is bound to {Button} on a controller that is not here",
            button.Describe());
    }

    /// <summary>Puts what the microphone is doing on the panel.</summary>
    public void ShowMicrophone(MicrophoneState state)
    {
        _panel.Microphone = state;

        var listening = _settings.Current.Listening;

        // Describing a key is the App's business — Core has no keyboard — so the renderer is passed down and
        // the sentence is chosen in Core, where a test reads what a Commander reads.
        var gesture = ListeningCapability.PushToTalkGesture(listening, Gestures.Describe);

        _panel.MicrophoneDetail = MicrophoneNarration.For(
            state,
            listening.Mode,
            Wake.Phrases,
            gesture,
            listening.PreRollMilliseconds);

        // The same three facts, worded for a prompt that is waiting on one (remediation.md 10, item 12).
        _panel.ListeningPrompt = MicrophoneNarration.Prompt(
            listening.Mode,
            Wake.Phrases,
            ListeningCapability.PushToTalkGesture(
                listening,
                Gestures.Describe,
                nameTheButton: false));
    }

    /// <summary>Points the wake-word policy at whatever d47 currently answers to.</summary>
    public void ApplyWakeWords()
    {
        var listening = _settings.Current.Listening;

        Wake.Window = TimeSpan.FromSeconds(listening.WakeWindowSeconds);

        Wake.Phrases = ListeningWiring.WakePhrases(listening.Mode, listening.WakeWords, _shipName());
    }

    /// <summary>Lets go of the keys, then releases the microphone and the transcribers.</summary>
    public void Dispose()
    {
        _pushToTalk.ForceUp();
        _pushToTalkButton.ForceUp();
        CancelButton.ForceUp();
        _microphone.Dispose();
        _transcriber.Dispose();

        lock (_hosted)
        {
            foreach (var hosted in _hosted.Values)
            {
                hosted.Dispose();
            }
        }
    }
}
