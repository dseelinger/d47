using D47.Core.Audio;
using D47.Core.Configuration;

namespace D47.Core.Capabilities.Builtin;

/// <summary>
/// Everything audible: the voice, the device, the loop-state cues, the thinking bed, and the
/// one control that outranks all of them (list.md Phase 5).
/// <para>
/// It registers exactly one tool, and that tool is <c>stop_speaking</c>. Nothing else here is
/// reachable by the model: a model that can change the output device or the voice mid-sentence
/// is a model that can make itself harder to hear, and there is no request that needs it.
/// Stopping, by contrast, is the one thing the Commander must always be able to ask for by
/// voice — so it is a tool, and it is also a keyword phrase and a hotkey, because a spoken
/// request that has to reach the model first is gated behind the very thing it is trying to
/// interrupt.
/// </para>
/// </summary>
public static class SpeechCapability
{
    public const string Id = "speech";

    public const string ProviderKey = "speech.provider";
    public const string VoiceKey = "speech.voice";
    public const string RateKey = "speech.rate";
    public const string OutputDeviceKey = "speech.outputDevice";
    public const string CuesKey = "speech.cues";
    public const string BedEnabledKey = "speech.thinkingBed";
    public const string BedKey = "speech.thinkingBedSound";
    public const string ShutUpHotkeyKey = "speech.shutUpHotkey";
    public const string RetryAttemptsKey = "speech.retryAttempts";
    public const string RetryWaitKey = "speech.retryWait";
    public const string RetryBackoffKey = "speech.retryBackoff";
    public const string TurnTimeoutKey = "speech.turnTimeout";
    public const string EgressKey = "speech.egress";

    /// <summary>The provider ids the settings row offers. "none" is a first-class choice.</summary>
    public const string NoneId = "none";
    public const string EdgeId = "edge";

    /// <summary>
    /// Everything the arbiter needs from the outside world, supplied by the app. Passed as
    /// callbacks rather than objects because Core must not hold the voice list's provider or
    /// the sound card — only a way to ask them what they have.
    /// </summary>
    public sealed record SpeechSurface
    {
        /// <summary>Stops everything audible, immediately. The whole point of the capability.</summary>
        public required Action Silence { get; init; }

        /// <summary>Voices the selected provider offers, or empty when it cannot say.</summary>
        public Func<IReadOnlyList<string>>? Voices { get; init; }

        /// <summary>Output devices, as id/label pairs the picker can render.</summary>
        public Func<IReadOnlyList<string>>? OutputDevices { get; init; }

        public Func<string, string>? DeviceLabel { get; init; }

        public Func<string, string>? VoiceLabel { get; init; }

        /// <summary>Bed names as shipped. Read from the cue library, never a literal list.</summary>
        public required IReadOnlyList<string> Beds { get; init; }
    }

    public static CapabilityDescriptor Create(SpeechSurface surface) => new()
    {
        Id = Id,
        Group = "Voice",
        Name = "Speech",
        Summary = "Speak replies aloud, mark each loop state with its own cue, and stop on command.",
        Examples = ["stop", "be quiet", "shut up"],

        // The fastest thing a Commander can say, and the reason InterruptKeywords exists as a
        // separate list. Bare "stop" is a common verb, and the general vocabulary refuses those
        // outright — a bare word hijacks any sentence containing it. Macros (Phase 10) are the
        // concrete claimant: the Commander names them, and the checklist says their vocabulary
        // cannot be closed in advance. Mid-sentence, though, "stop" has one meaning, and an
        // interrupt is judged on how quickly it can be said.
        InterruptKeywords = ["stop", "stop it", "enough", "quiet"],

        // Phrases that can only be a request for silence. "stop" alone is not one of them: it
        // is the first word of "stop the ship", "stop plotting", and a dozen other things —
        // and neither is a bare "silence", which turns any sentence mentioning it into a
        // command. Every phrase here needs at least two words to earn its place.
        Keywords =
        [
            "shut up",
            "be quiet",
            "stop talking",
            "stop speaking",
            "quiet please",
        ],
        Display = new CapabilityDisplay { PanelTitle = "Speech", Order = 30 },
        Tools =
        [
            new ToolDefinition
            {
                Name = "stop_speaking",
                Description =
                    "Immediately stop all speech and audio, discarding anything queued. " +
                    "Use when the Commander asks for silence.",

                // The one tool that must answer while a turn is mid-sentence, since that is
                // the only moment it is ever wanted.
                Interrupting = true,
                Handler = (_, _) =>
                {
                    surface.Silence();
                    return Task.FromResult(ToolResult.Ok("Stopped."));
                },
            },
        ],
        Settings =
        [
            new SettingRow
            {
                Key = ProviderKey,
                Label = "Voice provider",
                Help = "Where spoken replies are synthesised. \"None\" leaves d47 silent; cues still play.",
                Kind = SettingKind.Choice,
                Choices = [EdgeId, NoneId],
                ChoiceLabel = id => id == EdgeId ? "Edge Neural (free)" : "None — do not speak",
                DocsAnchor = "provider",
                Binding = new SettingBinding
                {
                    Read = s => s.Speech.Provider,
                    Write = (s, v) => s with { Speech = s.Speech with { Provider = v ?? EdgeId } },
                },
            },
            new SettingRow
            {
                Key = VoiceKey,
                Label = "Voice",
                Help = "Which voice speaks. The list comes from the selected provider.",
                Kind = SettingKind.Choice,
                DefaultDisplay = "(the provider's default)",
                AllowsFreeText = true,
                ChoiceSource = _ => surface.Voices?.Invoke() ?? [],
                ChoiceLabel = id => surface.VoiceLabel?.Invoke(id) ?? id,
                AppliesWhen = s => s.Speech.Provider != NoneId,
                DocsAnchor = "voice",
                Binding = new SettingBinding
                {
                    Read = s => s.Speech.Voice,
                    Write = (s, v) => s with { Speech = s.Speech with { Voice = v } },
                },
            },
            new SettingRow
            {
                Key = RateKey,
                Label = "Speaking rate",
                Help = "1.0 is the voice's natural pace. 1.2 is a fifth faster.",
                Kind = SettingKind.Number,
                DefaultDisplay = "1.0",
                AppliesWhen = s => s.Speech.Provider != NoneId,
                DocsAnchor = "rate",
                Binding = new SettingBinding
                {
                    Read = s => s.Speech.Rate.ToString("0.0#", System.Globalization.CultureInfo.InvariantCulture),
                    Write = (s, v) => s with
                    {
                        Speech = s.Speech with { Rate = ParseRate(v) },
                    },
                },
            },
            new SettingRow
            {
                Key = OutputDeviceKey,
                Label = "Output device",
                Help = "Where d47 speaks. Defaults to whatever Windows is using.",
                Kind = SettingKind.Choice,
                DefaultDisplay = "(the system default)",
                AllowsFreeText = true,
                ChoiceSource = _ => surface.OutputDevices?.Invoke() ?? [],
                ChoiceLabel = id => surface.DeviceLabel?.Invoke(id) ?? id,
                DocsAnchor = "output-device",
                Binding = new SettingBinding
                {
                    Read = s => s.Speech.OutputDevice,
                    Write = (s, v) => s with { Speech = s.Speech with { OutputDevice = v } },
                },
            },
            new SettingRow
            {
                Key = CuesKey,
                Label = "Loop-state cues",
                Help = "A short sound as d47 starts listening, starts thinking, and finishes.",
                Kind = SettingKind.Toggle,
                DefaultDisplay = "on",
                DocsAnchor = "cues",
                Binding = new SettingBinding
                {
                    Read = s => s.Speech.CuesEnabled ? "true" : "false",
                    Write = (s, v) => s with { Speech = s.Speech with { CuesEnabled = v != "false" } },
                },
            },
            new SettingRow
            {
                Key = BedEnabledKey,
                Label = "Thinking bed",
                Help = "A quiet loop while a turn runs, so a slow answer is not silence.",
                Kind = SettingKind.Toggle,
                DefaultDisplay = "on",
                Group = "While thinking",
                DocsAnchor = "thinking-bed",
                Binding = new SettingBinding
                {
                    Read = s => s.Speech.ThinkingBedEnabled ? "true" : "false",
                    Write = (s, v) => s with { Speech = s.Speech with { ThinkingBedEnabled = v != "false" } },
                },
            },
            new SettingRow
            {
                Key = BedKey,
                Label = "Thinking bed sound",
                Help = "Which loop plays while d47 works.",
                Kind = SettingKind.Choice,

                // The shipped set, read from the library. A literal list here would be a
                // second place for a name to be wrong (list.md Phase 5, #20).
                Choices = surface.Beds,
                DefaultDisplay = CueLibrary.DefaultBed,
                AppliesWhen = s => s.Speech.ThinkingBedEnabled,
                Group = "While thinking",
                DocsAnchor = "thinking-bed",
                Binding = new SettingBinding
                {
                    Read = s => s.Speech.ThinkingBed,
                    Write = (s, v) => s with { Speech = s.Speech with { ThinkingBed = v } },
                },
            },
            new SettingRow
            {
                Key = ShutUpHotkeyKey,
                Label = "Stop speaking",
                Help =
                    "Silences d47 instantly, from anywhere — including while Elite has the foreground. " +
                    "Press the key combination to bind it.",
                Kind = SettingKind.Hotkey,
                DefaultDisplay = "(unbound)",
                DocsAnchor = "shut-up",

                // Protected for the same reason every hotkey row is: a model that can unbind
                // the Commander's stop button has removed the one control that outranks it
                // (architecture.md §7).
                Protected = true,
                Binding = new SettingBinding
                {
                    Read = s => s.Speech.ShutUpHotkey,
                    Write = (s, v) => s with { Speech = s.Speech with { ShutUpHotkey = v } },
                },
            },
            new SettingRow
            {
                Key = RetryAttemptsKey,
                Label = "Attempts",
                Kind = SettingKind.Number,
                Help = "How many times a failing turn is tried in total. 1 means do not retry.",
                DefaultDisplay = "3",
                Group = "When a turn fails",
                GroupHelp =
                    "A turn that stalls is answered out loud rather than left as silence, which is " +
                    "otherwise indistinguishable from d47 having ignored you.",
                DocsAnchor = "retry",
                Binding = new SettingBinding
                {
                    Read = s => s.Speech.RetryAttempts.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    Write = (s, v) => s with
                    {
                        Speech = s.Speech with { RetryAttempts = ParseInt(v, 3, 1, 10) },
                    },
                },
            },
            new SettingRow
            {
                Key = RetryWaitKey,
                Label = "Wait between attempts",
                Kind = SettingKind.Number,
                Help = "Seconds before the first retry. Later waits grow according to the shape below.",
                DefaultDisplay = "2",
                Group = "When a turn fails",
                DocsAnchor = "retry",
                Binding = new SettingBinding
                {
                    Read = s => s.Speech.RetryWaitSeconds.ToString(
                        "0.#", System.Globalization.CultureInfo.InvariantCulture),
                    Write = (s, v) => s with
                    {
                        Speech = s.Speech with { RetryWaitSeconds = ParseDouble(v, 2, 0.1, 60) },
                    },
                },
            },
            new SettingRow
            {
                Key = RetryBackoffKey,
                Label = "Backoff",
                Kind = SettingKind.Choice,
                Help = "How the wait grows: sequential adds the base each time, logarithmic decelerates.",
                Choices = ["sequential", "logarithmic"],
                DefaultDisplay = "sequential",
                Group = "When a turn fails",
                DocsAnchor = "retry",
                Binding = new SettingBinding
                {
                    Read = s => s.Speech.RetryBackoff,
                    Write = (s, v) => s with
                    {
                        Speech = s.Speech with { RetryBackoff = v ?? "sequential" },
                    },
                },
            },
            new SettingRow
            {
                Key = TurnTimeoutKey,
                Label = "Give up after",
                Kind = SettingKind.Number,
                Help = "Seconds one attempt may run before it counts as failed.",
                DefaultDisplay = "45",
                Group = "When a turn fails",
                DocsAnchor = "retry",
                Binding = new SettingBinding
                {
                    Read = s => s.Speech.TurnTimeoutSeconds.ToString(
                        "0.#", System.Globalization.CultureInfo.InvariantCulture),
                    Write = (s, v) => s with
                    {
                        Speech = s.Speech with { TurnTimeoutSeconds = ParseDouble(v, 45, 5, 600) },
                    },
                },
            },
            new SettingRow
            {
                Key = EgressKey,
                Label = "What the voice provider receives",
                Kind = SettingKind.Info,
                Help = EdgeEgress,
                AppliesWhen = s => s.Speech.Provider != NoneId,
                DocsAnchor = "egress",
                Binding = new SettingBinding { Read = _ => EdgeEgress },
            },
        ],
    };

    /// <summary>
    /// Stated here rather than in the provider assembly so Core owns the disclosure and the
    /// documentation gate can read it without referencing a provider (list.md Phase 4).
    /// </summary>
    public const string EdgeEgress =
        "Edge Neural: the text of every reply d47 speaks is sent to Microsoft to be turned into " +
        "audio. No game state, no journal content and no keys are sent. Choosing \"None\" sends " +
        "nothing and leaves d47 silent.";

    /// <summary>
    /// The settings-to-policy conversion, in one place so the panel, the file and the turn loop
    /// cannot end up with three readings of the same four rows.
    /// </summary>
    public static Conversation.RetryPolicy RetryFrom(SpeechSettings speech) => new()
    {
        Attempts = speech.RetryAttempts,
        Wait = TimeSpan.FromSeconds(speech.RetryWaitSeconds),
        Backoff = speech.RetryBackoff.Equals("logarithmic", StringComparison.OrdinalIgnoreCase)
            ? Conversation.BackoffShape.Logarithmic
            : Conversation.BackoffShape.Sequential,
        AttemptTimeout = TimeSpan.FromSeconds(speech.TurnTimeoutSeconds),
    };

    private static double ParseRate(string? value) => ParseDouble(value, 1.0, 0.5, 2.0);

    private static double ParseDouble(string? value, double fallback, double min, double max) =>
        double.TryParse(value, System.Globalization.NumberStyles.Float,
            System.Globalization.CultureInfo.InvariantCulture, out var parsed)
            ? Math.Clamp(parsed, min, max)
            : fallback;

    private static int ParseInt(string? value, int fallback, int min, int max) =>
        int.TryParse(value, System.Globalization.NumberStyles.Integer,
            System.Globalization.CultureInfo.InvariantCulture, out var parsed)
            ? Math.Clamp(parsed, min, max)
            : fallback;
}
