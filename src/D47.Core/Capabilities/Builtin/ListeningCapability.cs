using System.Text;
using D47.Core.Configuration;
using D47.Core.Input;
using D47.Core.Listening;

namespace D47.Core.Capabilities.Builtin;

/// <summary>
/// Hearing the Commander (list.md Phase 6).
/// <para>
/// The one tool is read-only, and the push-to-talk key row is protected. A model that can
/// rebind or unbind the microphone key has taken away how the Commander talks to it — and
/// anything the model can call, a hostile in-game message can attempt to invoke
/// (architecture.md §7).
/// </para>
/// </summary>
public static class ListeningCapability
{
    public const string Id = "listening";

    public const string DeviceKey = "listening.inputDevice";
    public const string PushToTalkKeyKey = "listening.pushToTalkKey";
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

    /// <summary>Hands free: the gate opens when somebody talks (list.md Phase 13).</summary>
    public const string ContinuousMode = "continuous";

    /// <summary>Hands free, and only when spoken to by name.</summary>
    public const string WakeMode = "wake";

    /// <summary>
    /// Whether a stored mode is one where d47 decides for itself when to listen. Answered here
    /// rather than at each of the four call sites that need it, because a fifth mode should mean
    /// editing one expression.
    /// </summary>
    public static bool IsHandsFree(string? mode) => mode is ContinuousMode or WakeMode;

    /// <summary>
    /// What the app supplies from outside Core: the devices, and the answers only the live
    /// input path can give.
    /// </summary>
    public sealed record ListeningSurface
    {
        /// <summary>Input device ids. Empty when the machine has none.</summary>
        public required Func<IReadOnlyList<string>> InputDevices { get; init; }

        public required Func<string, string> DeviceLabel { get; init; }

        /// <summary>
        /// What "the system default" actually resolves to right now, or null when that cannot
        /// be determined. Named everywhere the default is offered or reported, because the
        /// default is the one choice a Commander makes without seeing what they chose — and on
        /// a machine with VR or streaming software installed it is routinely a virtual endpoint
        /// that delivers silence. A picker that says only "System default" hides exactly the
        /// fact needed to spot that.
        /// </summary>
        public Func<string?>? DefaultDeviceName { get; init; }

        /// <summary>
        /// How long ago the Commander was last heard and understood, or null if never. Supplied
        /// as an age rather than a timestamp because Core reads no clock.
        /// </summary>
        public Func<TimeSpan?>? SinceHeard { get; init; }

        /// <summary>Whether audio is actually flowing, and why not when it is not.</summary>
        public required Func<(bool Capturing, string? Unavailable)> CaptureState { get; init; }

        /// <summary>Whether a transcriber is loaded and ready to turn audio into words.</summary>
        public required Func<(bool Ready, string? Model, string? Reason)> TranscriberState { get; init; }

        /// <summary>
        /// What the microphone is doing right now, as the gate policy sees it. The one question
        /// a Commander running hands free actually wants answered (list.md Phase 13).
        /// </summary>
        public Func<MicrophoneState>? Microphone { get; init; }

        /// <summary>
        /// Whether echo cancellation is actually running, and why not when it is not. Live state
        /// rather than the settings row: a canceller that was asked for and failed to start is
        /// exactly the case worth reporting, and the row would say it was on.
        /// </summary>
        public Func<(bool Active, string? Unavailable)>? EchoState { get; init; }

        /// <summary>
        /// What d47 currently answers to in wake-word mode. Supplied rather than read from
        /// settings, because an empty row means "the ship's AI name" and only the host knows
        /// what that has been set to.
        /// </summary>
        public Func<IReadOnlyList<string>>? WakeWords { get; init; }

        /// <summary>
        /// The Commander's Elite bindings, for the double-bind check. Read-only, and the same
        /// parse Phase 10's keyboard reachability will use rather than a second view of it.
        /// </summary>
        public required Func<EliteBinds> Binds { get; init; }

        /// <summary>Which speech models are already on disk, so the row can mark them.</summary>
        public required Func<IReadOnlyList<string>> InstalledModels { get; init; }

        /// <summary>
        /// A stored key as a Commander would write it: <c>[</c> rather than <c>Oem4</c>.
        /// <para>
        /// Injected because the printable form belongs to the input toolkit and Core does not
        /// reference one. The settings row has rendered it properly since the alias trap was
        /// found - several of those values carry two names and <c>ToString</c> picks whichever
        /// it finds first - and this is what stops the spoken and written status disagreeing
        /// with the panel about which key the Commander is holding.
        /// </para>
        /// </summary>
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

        // Only when spoken. Typed, these are conversation - a Commander who types "can you hear
        // me" is opening a conversation, and answering with a hardware summary answers about
        // the channel they did not use.
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

                // Names the device the default actually resolves to. Unset is the shipped
                // state, so this is what a Commander sees before they have chosen anything —
                // and Windows will hand that slot to a virtual endpoint from VR or streaming
                // software without mentioning it, which then presents as d47 not hearing them
                // at all. The placeholder is the only place that fact can be seen.
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
                Label = "Push-to-talk key",
                Help =
                    "Held, D47 listens. Right shift out of the box, since that is what a Commander on a "
                    + "stick and throttle has spare. Clear it and D47 never opens the microphone.",
                Kind = SettingKind.Hotkey,
                DefaultDisplay = "RightShift",
                DocsAnchor = "push-to-talk-key",

                // Protected: rebinding or clearing this is removing the Commander's way of
                // speaking to d47, and it is reachable from the panel and the router instead.
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
                Key = ModeKey,
                Label = "How D47 decides you are talking to it",
                Help =
                    "Hold the key, press it to start and stop, or let D47 open the microphone itself — "
                    + "either whenever you speak, or only when you say its name. The key still works in "
                    + "all four.",
                Kind = SettingKind.Choice,
                Choices = [HoldMode, ToggleMode, ContinuousMode, WakeMode],

                // "Press to talk (PTT)" rather than "Hold to talk": PTT is what this is called
                // everywhere else a Commander has met it, and a name they already know beats a
                // more literal one they have to map onto it.
                ChoiceLabel = id => id switch
                {
                    ToggleMode => "Toggle on and off",
                    ContinuousMode => "Listen whenever I speak",
                    WakeMode => "Listen when I say its name",
                    _ => "Press to talk (PTT)",
                },
                DefaultDisplay = "hold",
                DocsAnchor = "mode",

                // Protected, and this is the row where that matters most. The last two open the
                // microphone and keep it open, so a model that could set this could start
                // continuous capture on the Commander's machine — and anything the model can
                // call, a hostile in-game message can attempt to invoke (architecture.md §7).
                // It stays reachable from the panel and from the model-free router.
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

                    // Marked rather than hidden. A Commander comparing models needs to know
                    // which choices cost a download and which are already paid for.
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
                    Read = s => s.Listening.Model,
                    Write = (s, v) => s with
                    {
                        Listening = s.Listening with
                        {
                            Model = v is null || WhisperModels.Find(v) is null
                                ? WhisperModels.NoneId
                                : v,
                        },
                    },
                },
            },
            new SettingRow
            {
                Key = GpuKey,
                Label = "Run the speech model on the GPU",

                // The cost stated on the row, which the checklist asks for by name. A Commander
                // who turns this on in VR and then sees reprojection has no reason to connect
                // the two unless it was said here.
                Help =
                    "Faster, but in VR the GPU is already the scarce resource — a large model there "
                    + "shows up as dropped frames and reprojection rather than as a speech problem. "
                    + "Needs the CUDA runtime; D47 says so rather than quietly using the CPU.",
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
                Label = "Capture before the key, in milliseconds",
                Help =
                    "How much audio from just before the key was noticed is kept. The key is sampled ten "
                    + "times a second, so without this the first syllable is clipped.",
                Kind = SettingKind.Number,
                DefaultDisplay = "500",
                DocsAnchor = "pre-roll",

                // Applies in every mode. It was written for the polling delay on the key, and it
                // covers the detector's onset delay in the hands-free modes for the same reason
                // and by the same mechanism — the gate opens retroactively into the ring either
                // way (list.md Phase 13).
                AppliesWhen = s => s.Listening.PushToTalkKey is not null || IsHandsFree(s.Listening.Mode),
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
        ],
    };

    /// <summary>
    /// Answers "can you hear me?" — as a question, not as a status page.
    /// <para>
    /// The keyword that reaches this is a Commander asking something with a yes or a no in it,
    /// and when everything is working the honest answer is one word plus the evidence for it.
    /// Leading with an inventory of the microphone, the key, the mode and the binding table
    /// makes the reader do the diagnosis d47 has already done, and does it every time including
    /// the overwhelming majority of times nothing is wrong.
    /// </para>
    /// <para>
    /// <b>Asked by voice, the question answers itself.</b> The words only exist because they
    /// were heard, so the demonstration is better than any assertion about device state — which
    /// is why a recent transcription is reported first and the rest is dropped.
    /// </para>
    /// <para>
    /// The detail is not lost, it is conditional: every fault below is stated in full, with the
    /// thing to do about it, because <em>that</em> is when a Commander needs to know which of
    /// the five causes it is. <see cref="DescribeInDetail"/> keeps the unconditional inventory
    /// for a diagnostics surface, where a reader has asked for exactly that.
    /// </para>
    /// </summary>
    public static string Describe(D47Settings settings, ListeningSurface surface)
    {
        var listening = settings.Listening;
        var (capturing, unavailable) = surface.CaptureState();
        var (ready, model, reason) = surface.TranscriberState();

        var faults = new StringBuilder();

        if (listening.PushToTalkKey is null && !IsHandsFree(listening.Mode))
        {
            faults.AppendLine(
                "No push-to-talk key is set, so I never open the microphone. "
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

        // A collision has no symptom other than one of the two silently not working, so it is
        // said whenever it exists — even when everything else is healthy.
        if (listening.PushToTalkKey is { } bound
            && CollisionWarning(bound, surface.KeyLabel?.Invoke(bound) ?? bound, surface) is { } collision)
        {
            faults.AppendLine(collision);
        }

        if (faults.Length > 0)
        {
            return $"No — not properly.\n{faults.ToString().TrimEnd()}";
        }

        var device = DeviceName(listening.InputDevice, surface);

        // "Just now" is the strongest possible evidence and needs no qualification. The window
        // is generous because the answer is about whether hearing works at all, not about
        // precisely when it last did.
        if (surface.SinceHeard?.Invoke() is { } age && age < TimeSpan.FromMinutes(2))
        {
            return $"Yes — I just heard you, on {device}.";
        }

        return $"Yes — {device} is open and {model} is loaded. {HowToBeHeard(listening, surface)}";
    }

    /// <summary>
    /// What the Commander should actually do to be heard, which is a different sentence in each
    /// of the four modes. Stated rather than assumed, because "hold RightShift" is wrong advice
    /// in three of them and it is the last line of the answer.
    /// </summary>
    private static string HowToBeHeard(ListeningSettings listening, ListeningSurface surface)
    {
        var printedKey = listening.PushToTalkKey is { } key
            ? surface.KeyLabel?.Invoke(key) ?? key
            : null;

        return listening.Mode switch
        {
            ContinuousMode => "Just talk — I open the microphone myself when I hear you start.",

            WakeMode when surface.WakeWords?.Invoke() is { Count: > 0 } names =>
                $"Say {names[0]}, and then whatever you want.",

            WakeMode => "Say my name, and then whatever you want.",

            ToggleMode when printedKey is not null => $"Press {printedKey} to start, and again to stop.",

            _ when printedKey is not null => $"Hold {printedKey} and say something.",

            // Hands free with no key bound is a legitimate configuration and the branches above
            // cover it; this is the one that cannot happen, and says so rather than inventing a
            // gesture.
            _ => "No key is bound, so nothing opens the microphone.",
        };
    }

    /// <summary>
    /// The unconditional inventory: every part of the listening path whether or not it is
    /// working. For a diagnostics surface, where the whole point is to see the state of things
    /// that are fine. <see cref="Describe"/> is what answers a Commander who asked a question.
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

        if (listening.PushToTalkKey is { } key)
        {
            var printed = surface.KeyLabel?.Invoke(key) ?? key;

            report.AppendLine(
                $"Push-to-talk: {printed} ({(listening.Mode == ToggleMode ? "toggle" : "hold")}).");

            foreach (var line in DescribeCollision(key, printed, surface))
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

    /// <summary>
    /// What the microphone is doing, said the way the panel's indicator says it. One wording for
    /// the spoken answer and the drawn one, so they cannot disagree about what "armed" means.
    /// </summary>
    public static string StateName(MicrophoneState state) => state switch
    {
        MicrophoneState.Open => "the microphone is open and I am keeping what I hear",
        MicrophoneState.Armed => "the microphone is open and I am waiting to hear you start",
        MicrophoneState.Idle => "the microphone is open and nothing is being kept",
        _ => "the microphone is closed",
    };

    /// <summary>
    /// "Report a key that is bound twice" (list.md Phase 6). A double-bound push-to-talk key
    /// has no symptom other than not working — in one direction or the other, depending on
    /// which application sees the key first — so the collision is stated outright.
    /// </summary>
    /// <summary>
    /// The Elite collision, in the Commander's spelling. <paramref name="key"/> is the stored
    /// form and is what the binds are searched by; <paramref name="printed"/> is what is said
    /// back, because a Commander told their <c>Oem4</c> collides has to work out what that is.
    /// </summary>
    private static IEnumerable<string> DescribeCollision(string key, string printed, ListeningSurface surface)
    {
        if (CollisionWarning(key, printed, surface) is { } warning)
        {
            yield return warning;
            yield break;
        }

        var binds = surface.Binds();

        // The all-clear belongs only to the detailed inventory. Silence rather than a false
        // all-clear when the binds were never read: not having looked is not the same as
        // having looked and found nothing.
        if (binds.IsKnown)
        {
            yield return $"No Elite binding uses {printed} in the {binds.PresetName} preset.";
        }
    }

    /// <summary>
    /// The collision, or null when there is not one. Separated from the all-clear because a
    /// warning is worth interrupting a Commander for and a clean result is not.
    /// </summary>
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
    /// The device in the Commander's terms. An explicit choice is named; the default is named
    /// <em>and</em> resolved, so "the system default" never stands alone as the one answer that
    /// cannot be acted on.
    /// </summary>
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
