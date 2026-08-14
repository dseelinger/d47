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

    public const string HoldMode = "hold";
    public const string ToggleMode = "toggle";

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
        Display = new CapabilityDisplay { PanelTitle = "Listening", Order = 32 },
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
                Label = "Key behaviour",
                Help = "Press and hold to talk, or press once to start and again to stop.",
                Kind = SettingKind.Choice,
                Choices = [HoldMode, ToggleMode],

                // "Press to talk (PTT)" rather than "Hold to talk": PTT is what this is called
                // everywhere else a Commander has met it, and a name they already know beats a
                // more literal one they have to map onto it.
                ChoiceLabel = id => id == HoldMode ? "Press to talk (PTT)" : "Toggle on and off",
                DefaultDisplay = "hold",
                DocsAnchor = "mode",
                AppliesWhen = s => s.Listening.PushToTalkKey is not null,
                Binding = new SettingBinding
                {
                    Read = s => s.Listening.Mode,
                    Write = (s, v) => s with
                    {
                        Listening = s.Listening with { Mode = v == ToggleMode ? ToggleMode : HoldMode },
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
                AppliesWhen = s => s.Listening.PushToTalkKey is not null,
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

        if (listening.PushToTalkKey is null)
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

        var printedKey = listening.PushToTalkKey is { } key
            ? surface.KeyLabel?.Invoke(key) ?? key
            : null;

        return
            $"Yes — {device} is open and {model} is loaded. "
            + $"Hold {printedKey} and say something.";
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
        else
        {
            report.AppendLine("Push-to-talk: not set, so I never open the microphone.");
        }

        var (ready, model, reason) = surface.TranscriberState();

        report.Append(ready
            ? $"Transcription: {model} loaded."
            : $"Transcription: unavailable. {reason ?? "No model is loaded."}");

        return report.ToString();
    }

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
