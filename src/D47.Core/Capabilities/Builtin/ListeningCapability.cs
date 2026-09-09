using System.Text;
using D47.Core.Configuration;
using D47.Core.Input;
using D47.Core.Listening;

namespace D47.Core.Capabilities.Builtin;

/// <summary>Hearing the Commander (Phase 6).</summary>
public static class ListeningCapability
{
    public const string Id = "listening";

    public const string DeviceKey = "listening.inputDevice";
    public const string PushToTalkKeyKey = "listening.pushToTalkKey";

    /// <summary>The stick button beside it (Phase 53).</summary>
    public const string PushToTalkButtonKey = "listening.pushToTalkButton";

    /// <summary>Cancel's key (#221).</summary>
    public const string CancelHotkeyKey = "listening.cancelHotkey";

    public const string CancelButtonKey = "listening.cancelButton";
    public const string ModeKey = "listening.mode";
    public const string PreRollKey = "listening.preRoll";
    public const string ModelKey = "listening.model";

    public const string GpuKey = "listening.useGpu";
    public const string EgressKey = "listening.egress";
    public const string EchoKey = "listening.echoCancellation";
    public const string NoiseKey = "listening.noiseSuppression";
    public const string SensitivityKey = "listening.sensitivity";
    public const string SilenceKey = "listening.silence";
    public const string WakeWordsKey = "listening.wakeWords";
    public const string WakeWindowKey = "listening.wakeWindow";

    public const string HoldMode = "hold";
    public const string ToggleMode = "toggle";

    /// <summary>Hands free: the gate opens when somebody talks (Phase 13).</summary>
    public const string ContinuousMode = "continuous";

    /// <summary>Hands free, and only when spoken to by name.</summary>
    public const string WakeMode = "wake";

    /// <summary>Whether a stored mode is one where d47 decides for itself when to listen.</summary>
    public static bool IsHandsFree(string? mode) => mode is ContinuousMode or WakeMode;

    /// <summary>
    /// What the app supplies from outside Core: the devices, and the answers only the live input path
    /// can give.
    /// </summary>
    public const string CorrectionsKey = "listening.corrections";

    public sealed record ListeningSurface
    {
        /// <summary>What d47 has learned this transcriber gets wrong, as a few lines to read (#134).</summary>
        public Func<string>? Corrections { get; init; }

        /// <summary>Forgets every learned correction.</summary>
        public Action? ForgetCorrections { get; init; }

        /// <summary>Input device ids.</summary>
        public required Func<IReadOnlyList<string>> InputDevices { get; init; }

        public required Func<string, string> DeviceLabel { get; init; }

        /// <summary>
        /// What "the system default" actually resolves to right now, or null when that cannot be
        /// determined.
        /// </summary>
        public Func<string?>? DefaultDeviceName { get; init; }

        /// <summary>How long ago the Commander was last heard and understood, or null if never.</summary>
        public Func<TimeSpan?>? SinceHeard { get; init; }

        /// <summary>Whether audio is actually flowing, and why not when it is not.</summary>
        public required Func<(bool Capturing, string? Unavailable)> CaptureState { get; init; }

        /// <summary>Whether a transcriber is loaded and ready to turn audio into words.</summary>
        public required Func<(bool Ready, string? Model, string? Reason)> TranscriberState { get; init; }

        /// <summary>What the microphone is doing right now, as the gate policy sees it.</summary>
        public Func<MicrophoneState>? Microphone { get; init; }

        /// <summary>Whether echo cancellation is actually running, and why not when it is not.</summary>
        public Func<(bool Active, string? Unavailable)>? EchoState { get; init; }

        /// <summary>What d47 currently answers to in wake-word mode.</summary>
        public Func<IReadOnlyList<string>>? WakeWords { get; init; }

        /// <summary>The Commander's Elite bindings, for the double-bind check.</summary>
        public required Func<EliteBinds> Binds { get; init; }

        /// <summary>Which speech models are already on disk, so the row can mark them.</summary>
        public required Func<IReadOnlyList<string>> InstalledModels { get; init; }

        /// <summary>A stored key as a Commander would write it: <c>[</c> rather than <c>Oem4</c>.</summary>
        public Func<string, string>? KeyLabel { get; init; }
    }

    public static CapabilityDescriptor Create(SettingsService settings, ListeningSurface surface) => new()
    {
        Id = Id,
        Group = "Voice",
        Name = "Listening",
        Summary = "Hear the Commander through the chosen microphone while the push-to-talk key is held.",
        Examples =
        [
            "can you hear me",
            "what microphone are you using",
            "is my push to talk key bound twice",
        ],
        Keywords =
        [
            "what microphone",
            "which microphone",
            "push to talk",
            "is my key bound twice",
        ],

        // Only when spoken.
        SpokenKeywords =
        [
            "can you hear me",
            "are you listening",
        ],
        Display = new CapabilityDisplay { PanelTitle = "Listening", Order = 3 },
        Tools =
        [
            new ToolDefinition
            {
                Name = "get_listening_status",
                Description =
                    "Report whether D47 can hear the Commander: the microphone in use, whether audio is "
                    + "flowing, the push-to-talk key, whether a transcription model is loaded, and whether "
                    + "that key collides with an Elite Dangerous binding.",
                Handler = (_, _) => Task.FromResult(ToolResult.Ok(Describe(settings.Current, surface))),
            },
        ],
        Settings =
        [
            new SettingRow
            {
                Key = DeviceKey,
                Label = "Microphone",
                Help =
                    "Which input D47 listens on. Leaving this unset uses the system default, which is the "
                    + "one setting whose failure looks like D47 simply not hearing you.",
                Kind = SettingKind.Choice,
                DefaultDisplay = "(the system default)",

                // Names the device the default actually resolves to.
                DefaultDisplaySource = _ => surface.DefaultDeviceName?.Invoke() is { Length: > 0 } resolved
                    ? $"(the system default — {resolved})"
                    : "(the system default)",
                AllowsFreeText = false,
                ChoiceSource = _ => surface.InputDevices(),
                ChoiceLabel = id => surface.DeviceLabel(id),
                DocsAnchor = "microphone",
                Binding = new SettingBinding
                {
                    Read = s => s.Listening.InputDevice,
                    Write = (s, v) => s with
                    {
                        Listening = s.Listening with
                        {
                            InputDevice = string.IsNullOrWhiteSpace(v) ? null : v,
                        },
                    },
                },
            },
            new SettingRow
            {
                Key = PushToTalkKeyKey,
                Label = "Push-to-talk",
                Help =
                    "Held, D47 listens — and pressing it shuts D47 up, whether or not you go on to say "
                    + "anything. Right shift out of the box, since that is what a Commander on a stick and "
                    + "throttle has spare. Bind a key, a stick button, or one of each — one at a time: press "
                    + "the control and give it a key, then press it again and give it a button. Giving it the "
                    + "same kind twice replaces that one. With both set, either opens the microphone. Unbind "
                    + "clears both, and with neither one D47 never opens the microphone.",
                Kind = SettingKind.Hotkey,
                DefaultDisplay = "RightShift",
                DocsAnchor = "push-to-talk-key",

                // One row, two properties (#217).
                AlsoBinds = PushToTalkButtonKey,

                // Protected: rebinding or clearing this is removing the Commander's way of speaking to d47,
                // and it is reachable from the panel and the router instead.
                Protected = true,
                Binding = new SettingBinding
                {
                    Read = s => s.Listening.PushToTalkKey,
                    Write = (s, v) => s with
                    {
                        Listening = s.Listening with
                        {
                            PushToTalkKey = string.IsNullOrWhiteSpace(v) ? null : v,
                        },
                    },
                },
            },
            new SettingRow
            {
                Key = PushToTalkButtonKey,
                Advanced = true,
                Label = "Push-to-talk button",
                Help =
                    "The same thing on your stick or throttle: press the button you want and D47 works out "
                    + "which one it was. It sits beside the key rather than replacing it — with both set, "
                    + "either one opens the microphone. Needs a button that springs back, not a switch that "
                    + "stays where you put it.",
                Kind = SettingKind.HotasButton,
                DocsAnchor = "push-to-talk-button",

                // Held by the key row's control rather than drawn on its own (#217).
                DrawnElsewhere = true,

                // Protected for the same reason the key is: rebinding or clearing this takes away the
                // Commander's way of speaking to d47.
                Protected = true,
                Binding = new SettingBinding
                {
                    Read = s => s.Listening.PushToTalkButton,
                    Write = (s, v) => s with
                    {
                        Listening = s.Listening with
                        {
                            PushToTalkButton = string.IsNullOrWhiteSpace(v) ? null : v,
                        },
                    },
                },
            },
            new SettingRow
            {
                Key = CancelHotkeyKey,
                Label = "Cancel",
                Help =
                    "Stops D47 talking and abandons the turn it is working on — including a long web " +
                    "search you have changed your mind about, which stops the spending rather than just " +
                    "the voice. Works from anywhere, including while Elite has the foreground. Bind a " +
                    "key, a stick button, or one of each; giving it the same kind twice replaces that one.",
                Kind = SettingKind.Hotkey,
                DefaultDisplay = "Ctrl+Alt+X",
                DocsAnchor = "cancel",

                // Claimed from the whole system, so a bare key is refused as it is bound.
                SystemWide = true,

                // One row over two properties (#217's arrangement, put to its second use by #221): the
                // control arms a keystroke listener and a controller walk at once.
                AlsoBinds = CancelButtonKey,

                // Protected for the same reason every hotkey row is: a model that can unbind the Commander's
                // stop button has removed the one control that outranks it.
                Protected = true,
                // Stored under Speech, and read here.
                Binding = new SettingBinding
                {
                    Read = s => s.Speech.ShutUpHotkey,
                    Write = (s, v) => s with { Speech = s.Speech with { ShutUpHotkey = v } },
                },
            },
            new SettingRow
            {
                Key = CancelButtonKey,
                Advanced = true,
                Label = "Cancel button",
                Help =
                    "The same thing on your stick or throttle: press the button you want and D47 works out "
                    + "which one it was. It sits beside the key rather than replacing it. Needs a button "
                    + "that springs back, not a switch that stays where you put it.",
                Kind = SettingKind.HotasButton,
                DocsAnchor = "cancel",

                // Held by the Cancel row's own control rather than drawn on its own (#217).
                DrawnElsewhere = true,

                Protected = true,
                Binding = new SettingBinding
                {
                    Read = s => s.Speech.CancelButton,
                    Write = (s, v) => s with
                    {
                        Speech = s.Speech with
                        {
                            CancelButton = string.IsNullOrWhiteSpace(v) ? null : v,
                        },
                    },
                },
            },
            new SettingRow
            {
                Key = ModeKey,
                Label = "How D47 decides you are talking to it",
                Help =
                    "Hold the key, press it to start and stop, or let D47 open the microphone itself — "
                    + "either whenever you speak, or only when you say its name. The key still works in "
                    + "all four.",
                Kind = SettingKind.Choice,
                Choices = [HoldMode, ToggleMode, ContinuousMode, WakeMode],

                // "Press to talk (PTT)" rather than "Hold to talk": PTT is what this is called everywhere
                // else a Commander has met it, and a name they already know beats a more literal one they
                // have to map onto it.
                ChoiceLabel = id => id switch
                {
                    ToggleMode => "Toggle on and off",
                    ContinuousMode => "Listen whenever I speak",
                    WakeMode => "Listen when I say its name",
                    _ => "Press to talk (PTT)",
                },
                DefaultDisplay = "hold",
                DocsAnchor = "mode",

                // Protected, and this is the row where that matters most.
                Protected = true,
                Commands =
                [
                    new SettingCommandPhrase("stop listening all the time", HoldMode),
                    new SettingCommandPhrase("only listen when I hold the key", HoldMode),
                    new SettingCommandPhrase("listen whenever I speak", ContinuousMode),
                    new SettingCommandPhrase("listen for your name", WakeMode),
                ],
                Binding = new SettingBinding
                {
                    Read = s => s.Listening.Mode,
                    Write = (s, v) => s with
                    {
                        Listening = s.Listening with
                        {
                            Mode = v is ToggleMode or ContinuousMode or WakeMode ? v : HoldMode,
                        },
                    },
                },
            },
            new SettingRow
            {
                Key = EchoKey,
                Advanced = true,
                Label = "Cancel D47's own voice out of the microphone",
                Help =
                    "Subtracts what D47 is playing from what it hears, so you can talk over it on "
                    + "speakers. Without this, hands-free listening goes deaf while D47 speaks rather "
                    + "than risk answering itself.",
                Kind = SettingKind.Toggle,
                DefaultDisplay = "on",
                DocsAnchor = "echo-cancellation",
                Binding = new SettingBinding
                {
                    Read = s => s.Listening.EchoCancellation ? "true" : "false",
                    Write = (s, v) => s with
                    {
                        Listening = s.Listening with { EchoCancellation = v is not "false" },
                    },
                },
            },
            new SettingRow
            {
                Key = NoiseKey,
                Advanced = true,
                Label = "Take the room out of what D47 hears",
                Help =
                    "Suppresses steady background noise — fans, a headset's own hiss — before the "
                    + "speech model sees it.",
                Kind = SettingKind.Toggle,
                DefaultDisplay = "on",
                DocsAnchor = "noise-suppression",
                Binding = new SettingBinding
                {
                    Read = s => s.Listening.NoiseSuppression ? "true" : "false",
                    Write = (s, v) => s with
                    {
                        Listening = s.Listening with { NoiseSuppression = v is not "false" },
                    },
                },
            },
            new SettingRow
            {
                Key = SensitivityKey,
                Label = "How much louder than the room speech has to be, in decibels",
                Help =
                    "Lower hears more and will open on a cough or a keyboard; higher waits until you "
                    + "are clearly talking. D47 measures the room continuously, so this is a margin "
                    + "above whatever your room happens to be, not a fixed loudness.",
                Kind = SettingKind.Number,
                Minimum = 3,
                Maximum = 30,
                DefaultDisplay = "9",
                DocsAnchor = "sensitivity",
                AppliesWhen = s => IsHandsFree(s.Listening.Mode),
                Binding = new SettingBinding
                {
                    Read = s => s.Listening.Sensitivity.ToString(),
                    Write = (s, v) => s with
                    {
                        Listening = s.Listening with
                        {
                            Sensitivity = int.TryParse(v, out var db) && db is >= 3 and <= 30
                                ? db
                                : s.Listening.Sensitivity,
                        },
                    },
                },
            },
            new SettingRow
            {
                Key = SilenceKey,
                Advanced = true,
                Label = "Quiet that ends a sentence, in milliseconds",
                Help =
                    "How long you have to stop talking before D47 decides you have finished. Short "
                    + "cuts you off mid-thought; long makes every answer wait for it.",
                Kind = SettingKind.Number,
                Step = 50,
                Minimum = 200,
                Maximum = 3000,
                DefaultDisplay = "700",
                DocsAnchor = "silence",
                AppliesWhen = s => IsHandsFree(s.Listening.Mode),
                Binding = new SettingBinding
                {
                    Read = s => s.Listening.SilenceMilliseconds.ToString(),
                    Write = (s, v) => s with
                    {
                        Listening = s.Listening with
                        {
                            SilenceMilliseconds = int.TryParse(v, out var ms) && ms is >= 200 and <= 3000
                                ? ms
                                : s.Listening.SilenceMilliseconds,
                        },
                    },
                },
            },
            new SettingRow
            {
                Key = WakeWordsKey,
                Advanced = true,
                Label = "What D47 answers to",
                Help =
                    "Comma-separated. Leave it unset and D47 answers to whatever you call your ship's "
                    + "AI, so renaming the core renames the wake word too. Add spellings if the speech "
                    + "model keeps hearing the name as something else.",
                Kind = SettingKind.Text,
                DefaultDisplay = "(the ship's AI name)",
                DefaultDisplaySource = _ => surface.WakeWords?.Invoke() is { Count: > 0 } names
                    ? $"({string.Join(", ", names)})"
                    : "(the ship's AI name)",
                DocsAnchor = "wake-words",
                AppliesWhen = s => s.Listening.Mode == WakeMode,
                Binding = new SettingBinding
                {
                    Read = s => s.Listening.WakeWords,
                    Write = (s, v) => s with
                    {
                        Listening = s.Listening with
                        {
                            WakeWords = string.IsNullOrWhiteSpace(v) ? null : v.Trim(),
                        },
                    },
                },
            },
            new SettingRow
            {
                Key = WakeWindowKey,
                Advanced = true,
                Label = "Seconds D47 keeps listening after you say its name",
                Help =
                    "Say the name alone, D47 answers, and the next thing you say is the request — the "
                    + "way you would address a person. Zero means the name and the request have to "
                    + "arrive in the same breath.",
                Kind = SettingKind.Number,
                Minimum = 0,
                Maximum = 60,
                DefaultDisplay = "12",
                DocsAnchor = "wake-window",
                AppliesWhen = s => s.Listening.Mode == WakeMode,
                Binding = new SettingBinding
                {
                    Read = s => s.Listening.WakeWindowSeconds.ToString(),
                    Write = (s, v) => s with
                    {
                        Listening = s.Listening with
                        {
                            WakeWindowSeconds = int.TryParse(v, out var seconds) && seconds is >= 0 and <= 60
                                ? seconds
                                : s.Listening.WakeWindowSeconds,
                        },
                    },
                },
            },
            new SettingRow
            {
                Key = ModelKey,
                Advanced = true,
                Label = "Speech model",
                Help =
                    "Which Whisper model turns your speech into words. Choosing one that is not yet on "
                    + "disk asks first, states the size and where it comes from, and downloads nothing "
                    + "until you agree.",
                Kind = SettingKind.Choice,
                Choices = WhisperModels.Ids,
                ChoiceLabel = id =>
                {
                    var label = WhisperModels.LabelOf(id);

                    if (id == WhisperModels.NoneId)
                    {
                        return label;
                    }

                    // Marked rather than hidden.
                    var installed = surface.InstalledModels().Contains(id);
                    var size = WhisperModels.Find(id)?.ApproximateMegabytes;

                    return installed
                        ? $"{label} — installed"
                        : $"{label} — about {size} MB to download";
                },
                DefaultDisplay = WhisperModels.DefaultId,
                DocsAnchor = "model",
                Binding = new SettingBinding
                {
                    // Adopted on the way out, so a file still naming a retired multilingual model shows the
                    // English twin it is actually running rather than a choice this build no longer offers
                    // (#187).
                    Read = s => WhisperModels.AdoptedId(s.Listening.Model) ?? WhisperModels.NoneId,
                    Write = (s, v) => s with
                    {
                        Listening = s.Listening with
                        {
                            Model = WhisperModels.AdoptedId(v) is { } wanted && WhisperModels.Find(wanted) is not null
                                ? wanted
                                : WhisperModels.NoneId,
                        },
                    },
                },
            },
            new SettingRow
            {
                Key = GpuKey,
                Advanced = true,
                Label = "Run the speech model on the GPU",

                // Both costs stated, because both are real and neither is guessable from the label.
                Help =
                    "Much faster — around five times — but it uses video memory and takes it from "
                    + "whatever else wants the GPU. In VR that is the game, where the cost shows up "
                    + "as dropped frames rather than as a speech problem. With no capable GPU, D47 "
                    + "runs on the CPU and says so rather than claiming otherwise.",
                Kind = SettingKind.Toggle,
                DefaultDisplay = "off",
                DocsAnchor = "gpu",
                AppliesWhen = s => s.Listening.Model != WhisperModels.NoneId,
                Binding = new SettingBinding
                {
                    Read = s => s.Listening.UseGpu ? "true" : "false",
                    Write = (s, v) => s with
                    {
                        Listening = s.Listening with { UseGpu = v is not "false" },
                    },
                },
            },
            new SettingRow
            {
                Key = PreRollKey,
                Advanced = true,
                Label = "Capture before the key, in milliseconds",
                Help =
                    "How much audio from just before the key was noticed is kept. The key is sampled ten "
                    + "times a second, so without this the first syllable is clipped.",
                Kind = SettingKind.Number,
                DefaultDisplay = "500",
                DocsAnchor = "pre-roll",

                // Applies in every mode.
                AppliesWhen = s => !NothingIsBound(s.Listening) || IsHandsFree(s.Listening.Mode),
                Binding = new SettingBinding
                {
                    Read = s => s.Listening.PreRollMilliseconds.ToString(),
                    Write = (s, v) => s with
                    {
                        Listening = s.Listening with
                        {
                            PreRollMilliseconds = int.TryParse(v, out var ms) && ms is >= 0 and <= 5000
                                ? ms
                                : s.Listening.PreRollMilliseconds,
                        },
                    },
                },
            },
            new SettingRow
            {
                Key = CorrectionsKey,
                Label = "Names it has learned to hear",
                Help =
                    "Proper nouns are where speech recognition fails hardest and least visibly: a "
                    + "misheard system name does not come back as an error, it comes back as a "
                    + "plausible English word and the answer is confidently about the wrong "
                    + "place.\n\n"
                    + "When a name D47 cannot find turns out to be one of these, it asks you which "
                    + "you meant, runs your question again, and remembers the word — so every "
                    + "later sentence containing it is put right too, not just the one you asked. "
                    + "It learns one only when you correct it, never on its own, and never for a "
                    + "word that already means something.\n\n"
                    + "Everything here stays on this machine and belongs to the Commander flying.",
                Kind = SettingKind.Info,
                DocsAnchor = "corrections",
                PressLabel = surface.ForgetCorrections is null ? null : "Forget them all",
                Press = surface.ForgetCorrections,
                Binding = new SettingBinding
                {
                    Read = _ => surface.Corrections?.Invoke()
                                ?? "Nothing yet. D47 learns one of these only when you correct a "
                                   + "name it misheard.",
                },
            },
        ],
    };

    /// <summary>Answers "can you hear me?" — as a question, not as a status page.</summary>
    public static string Describe(D47Settings settings, ListeningSurface surface)
    {
        var listening = settings.Listening;
        var (capturing, unavailable) = surface.CaptureState();
        var (ready, model, reason) = surface.TranscriberState();

        var faults = new StringBuilder();

        if (NothingIsBound(listening) && !IsHandsFree(listening.Mode))
        {
            faults.AppendLine(
                "No push-to-talk key or button is set, so I never open the microphone. "
                + "Set one in Settings and I will listen while you hold it.");
        }

        if (!capturing)
        {
            faults.AppendLine(
                $"The microphone is not capturing. {unavailable ?? "No reason was recorded."}");
        }

        if (!ready)
        {
            faults.AppendLine(
                $"I cannot turn audio into words. {reason ?? "No speech model is loaded."}");
        }

        // A collision has no symptom other than one of the two silently not working, so it is said whenever
        // it exists — even when everything else is healthy.
        if (listening.PushToTalkKey is { } bound
            && CollisionWarning(bound, surface.KeyLabel?.Invoke(bound) ?? bound, surface) is { } collision)
        {
            faults.AppendLine(collision);
        }

        if (ButtonCollisionWarning(listening, surface) is { } buttonCollision)
        {
            faults.AppendLine(buttonCollision);
        }

        if (faults.Length > 0)
        {
            return $"No — not properly.\n{faults.ToString().TrimEnd()}";
        }

        var device = DeviceName(listening.InputDevice, surface);

        // "Just now" is the strongest possible evidence and needs no qualification.
        if (surface.SinceHeard?.Invoke() is { } age && age < TimeSpan.FromMinutes(2))
        {
            return $"Yes — I just heard you, on {device}.";
        }

        return $"Yes — {device} is open and {model} is loaded. {HowToBeHeard(listening, surface)}";
    }

    /// <summary>
    /// The stick button bound to push-to-talk, printed as a Commander would say it, or null (GitHub
    /// issue 44).
    /// </summary>
    private static string? PrintedButton(ListeningSettings listening) =>
        Hotas.HotasButton.Parse(listening.PushToTalkButton)?.Describe();

    /// <summary>Whether nothing at all opens the microphone on purpose.</summary>
    private static bool NothingIsBound(ListeningSettings listening) =>
        listening.PushToTalkKey is null && PrintedButton(listening) is null;

    /// <summary>
    /// Everything the Commander can press to be heard, in one phrase, or null when there is nothing.
    /// </summary>
    /// <param name="nameTheButton">
    /// Whether a bound stick button is named by its number.
    /// </param>
    public static string? PushToTalkGesture(
        ListeningSettings listening,
        Func<string, string>? keyLabel,
        bool nameTheButton = true)
    {
        var printedKey = listening.PushToTalkKey is { } key
            ? keyLabel?.Invoke(key) ?? key
            : null;

        var printedButton = PrintedButton(listening) is { } button
            ? nameTheButton ? $"{button} on your stick" : "your push-to-talk button"
            : null;

        return (printedKey, printedButton) switch
        {
            ({ } bound, { } stick) => $"{bound} or {stick}",
            ({ } bound, null) => bound,
            (null, { } stick) => stick,
            _ => null,
        };
    }

    private static string? Gesture(ListeningSettings listening, ListeningSurface surface) =>
        PushToTalkGesture(listening, surface.KeyLabel);

    /// <summary>
    /// What the Commander should actually do to be heard, which is a different sentence in each of the
    /// four modes.
    /// </summary>
    private static string HowToBeHeard(ListeningSettings listening, ListeningSurface surface)
    {
        var gesture = Gesture(listening, surface);

        return listening.Mode switch
        {
            ContinuousMode => "Just talk — I open the microphone myself when I hear you start.",

            WakeMode when surface.WakeWords?.Invoke() is { Count: > 0 } names =>
                $"Say {names[0]}, and then whatever you want.",

            WakeMode => "Say my name, and then whatever you want.",

            ToggleMode when gesture is not null => $"Press {gesture} to start, and again to stop.",

            _ when gesture is not null => $"Hold {gesture} and say something.",

            // Hands free with nothing bound is a legitimate configuration and the branches above cover it;
            // this is the one that cannot happen, and says so rather than inventing a gesture.
            _ => "Nothing is bound, so nothing opens the microphone.",
        };
    }

    /// <summary>
    /// The unconditional inventory: every part of the listening path whether or not it is working.
    /// </summary>
    public static string DescribeInDetail(D47Settings settings, ListeningSurface surface)
    {
        var report = new StringBuilder();
        var listening = settings.Listening;

        var (capturing, unavailable) = surface.CaptureState();

        report.AppendLine(capturing
            ? $"Microphone: {DeviceName(listening.InputDevice, surface)}, capturing."
            : $"Microphone: not capturing. {unavailable ?? "No reason recorded."}");

        report.AppendLine($"Gate: {ModeName(listening.Mode)}.");

        if (surface.Microphone?.Invoke() is { } state)
        {
            report.AppendLine($"Right now: {StateName(state)}.");
        }

        // Both are reported, and both by name.
        if (Gesture(listening, surface) is { } gesture)
        {
            report.AppendLine(
                $"Push-to-talk: {gesture} ({(listening.Mode == ToggleMode ? "toggle" : "hold")}).");

            if (listening.PushToTalkKey is { } key)
            {
                foreach (var line in DescribeCollision(key, surface.KeyLabel?.Invoke(key) ?? key, surface))
                {
                    report.AppendLine(line);
                }
            }

            foreach (var line in DescribeButtonCollision(listening, surface))
            {
                report.AppendLine(line);
            }
        }
        else if (IsHandsFree(listening.Mode))
        {
            report.AppendLine("Push-to-talk: not set. I listen hands free instead.");
        }
        else
        {
            report.AppendLine("Push-to-talk: not set, so I never open the microphone.");
        }

        if (listening.Mode == WakeMode && surface.WakeWords?.Invoke() is { Count: > 0 } names)
        {
            report.AppendLine($"I answer to: {string.Join(", ", names)}.");
        }

        if (surface.EchoState?.Invoke() is { } echo)
        {
            report.AppendLine(echo.Active
                ? "Echo cancellation: running, so you can talk over me."
                : $"Echo cancellation: off. {echo.Unavailable ?? "Not enabled."}");
        }

        var (ready, model, reason) = surface.TranscriberState();

        report.Append(ready
            ? $"Transcription: {model} loaded."
            : $"Transcription: unavailable. {reason ?? "No model is loaded."}");

        return report.ToString();
    }

    /// <summary>The gate policy in the Commander's terms rather than in the stored spelling.</summary>
    public static string ModeName(string? mode) => mode switch
    {
        ToggleMode => "press once to start, again to stop",
        ContinuousMode => "hands free, opening whenever you speak",
        WakeMode => "hands free, opening when you say my name",
        _ => "hold the key to talk",
    };

    /// <summary>What the microphone is doing, said the way the panel's indicator says it.</summary>
    public static string StateName(MicrophoneState state) => state switch
    {
        MicrophoneState.Open => "the microphone is open and I am keeping what I hear",
        MicrophoneState.Armed => "the microphone is open and I am waiting to hear you start",
        MicrophoneState.Idle => "the microphone is open and nothing is being kept",
        _ => "the microphone is closed",
    };

    /// <summary>"Report a key that is bound twice" (Phase 6).</summary>
    private static IEnumerable<string> DescribeCollision(string key, string printed, ListeningSurface surface)
    {
        if (CollisionWarning(key, printed, surface) is { } warning)
        {
            yield return warning;
            yield break;
        }

        var binds = surface.Binds();

        // The all-clear belongs only to the detailed inventory.
        if (binds.IsKnown)
        {
            yield return $"No Elite binding uses {printed} in the {binds.PresetName} preset.";
        }
    }

    /// <summary>The collision, or null when there is not one.</summary>
    private static string? CollisionWarning(string key, string printed, ListeningSurface surface)
    {
        var binds = surface.Binds();

        if (!binds.IsKnown)
        {
            return null;
        }

        var collisions = binds.Using(key);

        if (collisions.Count == 0)
        {
            return null;
        }

        var actions = string.Join(", ", collisions.Select(binding => binding.Action).Distinct());

        return
            $"Warning: {printed} is also bound in Elite ({binds.PresetName}) to {actions}. "
            + "One of the two will not work, and neither will say so — pick another key for one of them.";
    }

    /// <summary>
    /// The same question for a stick button, and the same shape as the key's pair above: a warning
    /// worth interrupting for, separated from an all-clear that is not (#71).
    /// </summary>
    private static string? ButtonCollisionWarning(ListeningSettings listening, ListeningSurface surface)
    {
        if (Hotas.HotasButton.Parse(listening.PushToTalkButton) is not { } button)
        {
            return null;
        }

        var binds = surface.Binds();

        if (!binds.IsKnown)
        {
            return null;
        }

        var sharing = binds.UsingJoystickButton(button.Button);

        if (sharing.Count == 0)
        {
            return null;
        }

        var actions = string.Join(", ", sharing.Select(binding => binding.Action).Distinct());

        return
            $"Warning: {button.Describe()} may collide. Elite ({binds.PresetName}) binds a button of "
            + $"that number to {actions}, and I cannot tell whether that is the same controller. "
            + "If the microphone will not open, this is the first thing to check.";
    }

    /// <summary>The button collision or its all-clear, for the detailed inventory.</summary>
    private static IEnumerable<string> DescribeButtonCollision(
        ListeningSettings listening,
        ListeningSurface surface)
    {
        if (Hotas.HotasButton.Parse(listening.PushToTalkButton) is not { } button)
        {
            yield break;
        }

        if (ButtonCollisionWarning(listening, surface) is { } warning)
        {
            yield return warning;
            yield break;
        }

        var binds = surface.Binds();

        if (binds.IsKnown)
        {
            yield return
                $"No Elite binding uses a button of that number in the {binds.PresetName} preset.";
        }
    }

    /// <summary>The device in the Commander's terms.</summary>
    private static string DeviceName(string? id, ListeningSurface surface)
    {
        if (id is { Length: > 0 })
        {
            return surface.DeviceLabel(id);
        }

        return surface.DefaultDeviceName?.Invoke() is { Length: > 0 } resolved
            ? $"the system default ({resolved})"
            : "the system default";
    }
}
