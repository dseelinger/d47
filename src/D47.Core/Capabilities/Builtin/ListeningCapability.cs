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
            "can you hear me",
            "are you listening",
            "what microphone",
            "which microphone",
            "push to talk",
            "is my key bound twice",
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
                    "Held, D47 listens. Unset means D47 never opens the microphone — which is the default, "
                    + "because a microphone that opens on a key nobody chose is a microphone opening by surprise.",
                Kind = SettingKind.Hotkey,
                DefaultDisplay = "(not set — D47 does not listen)",
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
                DefaultDisplay = "none",
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
    /// The whole listening picture in one answer, including the double-bind check. Reported
    /// together because "D47 cannot hear me" has five possible causes and the Commander should
    /// not have to guess which one it is.
    /// </summary>
    public static string Describe(D47Settings settings, ListeningSurface surface)
    {
        var report = new StringBuilder();
        var listening = settings.Listening;

        var (capturing, unavailable) = surface.CaptureState();

        report.AppendLine(capturing
            ? $"Microphone: {DeviceName(listening.InputDevice, surface)}, capturing."
            : $"Microphone: not capturing. {unavailable ?? "No reason recorded."}");

        if (listening.PushToTalkKey is { } key)
        {
            report.AppendLine(
                $"Push-to-talk: {key} ({(listening.Mode == ToggleMode ? "toggle" : "hold")}).");

            foreach (var line in DescribeCollision(key, surface))
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
    private static IEnumerable<string> DescribeCollision(string key, ListeningSurface surface)
    {
        var binds = surface.Binds();

        if (!binds.IsKnown)
        {
            // Silence rather than a false all-clear. Not having read the binds is not the same
            // as having read them and found nothing.
            yield break;
        }

        var collisions = binds.Using(key);

        if (collisions.Count == 0)
        {
            yield return $"No Elite binding uses {key} in the {binds.PresetName} preset.";
            yield break;
        }

        var actions = string.Join(", ", collisions.Select(binding => binding.Action).Distinct());

        yield return
            $"Warning: {key} is also bound in Elite ({binds.PresetName}) to {actions}. "
            + "One of the two will not work, and neither will say so — pick another key for one of them.";
    }

    private static string DeviceName(string? id, ListeningSurface surface) =>
        id is { Length: > 0 } ? surface.DeviceLabel(id) : "the system default";
}
