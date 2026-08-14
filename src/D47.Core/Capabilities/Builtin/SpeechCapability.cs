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
    public const string CarrierCaptainVoiceKey = "speech.carrierCaptainVoice";
    public const string TowerVoiceKey = "speech.towerVoice";
    public const string SpeakIncomingKey = "speech.speakIncomingMessages";
    public const string SpeakNpcKey = "speech.speakNpcMessages";

    /// <summary>The secret row key for a voice provider's API key. One row per provider needing one.</summary>
    public static string KeyRowFor(TtsProviderInfo provider) => $"speech.{provider.Id}.apiKey";

    /// <summary>
    /// The provider ids the settings row offers. "none" is a first-class choice. Kept as
    /// constants because call sites compare against them, but the list itself now comes from
    /// <see cref="TtsProviderCatalog"/> so a provider cannot be offered by one row and unknown
    /// to another.
    /// </summary>
    public const string NoneId = TtsProviderCatalog.NoneId;
    public const string EdgeId = TtsProviderCatalog.EdgeId;
    public const string ElevenLabsId = TtsProviderCatalog.ElevenLabsId;

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
        Settings = Rows(surface),
    };

    private static IReadOnlyList<SettingRow> Rows(SpeechSurface surface)
    {
        var rows = new List<SettingRow>
        {
            new SettingRow
            {
                Key = ProviderKey,
                Label = "Voice provider",
                Help = "Where spoken replies are synthesised. \"None\" leaves D47 silent; cues still play.",
                Kind = SettingKind.Choice,
                Choices = [.. TtsProviderCatalog.All.Select(p => p.Id)],
                ChoiceLabel = id => TtsProviderCatalog.Selected(id).Label,
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
                Help =
                    "Which voice the core aboard speaks in. Kept per core, so switching persona "
                    + "switches voice. The list comes from the selected provider.",
                Kind = SettingKind.Choice,
                // Still the provider's default, which is what a core with no voice stored
                // actually gets. What is per core is the value, not the fallback.
                // Named as far as it can be. d47 does not know which voice ElevenLabs or Edge
                // picks when asked for nothing, so the honest limit is whose default it is —
                // which at least tells the Commander where to go and look. A row that says only
                // "(the default)" is a row answering a question with the question.
                DefaultDisplaySource = s =>
                    $"({TtsProviderCatalog.Selected(s.Speech.Provider).Name}'s own default voice)",
                DefaultDisplay = "(the provider's default)",
                AllowsFreeText = true,
                ChoiceSource = _ => surface.Voices?.Invoke() ?? [],
                ChoiceLabel = id => surface.VoiceLabel?.Invoke(id) ?? id,
                AppliesWhen = s => s.Speech.Provider != NoneId,
                DocsAnchor = "voice",

                // Per core, not one voice for the app. This row used to write a single value
                // that beat every pairing, so a Commander who chose a voice once heard it from
                // all eleven cores forever — the pairing was computed, stored, and never
                // reached. What they choose here is that core's voice, which is what
                // PersonaSettings.Voices has always said it holds: "written by the background
                // pairing at first startup and by the Commander choosing one by hand; nothing
                // distinguishes the two, on purpose".
                Binding = new SettingBinding
                {
                    Read = s => ShipVoiceFor(s, s.Persona.Id),
                    Write = WriteVoiceForCoreAboard,
                },
            },
            new SettingRow
            {
                Key = RateKey,
                Label = "Speaking rate",
                Help = "1.0 is the voice's natural pace. 1.2 is a fifth faster. Remembered per provider.",
                Kind = SettingKind.Number,

                // Fifths, because that is the unit the help text is written in. Without a step
                // this row rejected the exact value it offers as an example.
                Step = 0.05,
                DefaultDisplay = "1.0",
                AppliesWhen = s => s.Speech.Provider != NoneId,
                DocsAnchor = "rate",
                Binding = new SettingBinding
                {
                    // The same format the row's step derives, not a second one. A read format
                    // that disagrees with the written one makes every whole-number rate look
                    // like a change: "1" is written, "1.0" is read back, and the unchanged
                    // check never fires, so the settings file is rewritten on every apply.
                    Read = s => RateFor(s).ToString("0.##", System.Globalization.CultureInfo.InvariantCulture),
                    Write = (s, v) => WriteRate(s, v),
                },
            },
            new SettingRow
            {
                Key = CarrierCaptainVoiceKey,
                Label = "Carrier captain voice",
                Help = "Who answers for your fleet carrier. Empty uses the ship AI's voice.",
                Kind = SettingKind.Choice,
                DefaultDisplay = "(the ship AI's voice)",
                AllowsFreeText = true,
                ChoiceSource = _ => surface.Voices?.Invoke() ?? [],
                ChoiceLabel = id => surface.VoiceLabel?.Invoke(id) ?? id,

                // Only on offer to a Commander who has one. A row for a carrier you do not own
                // is a control that can only be got wrong, and the journal already knows.
                AppliesWhen = s => s.Speech.Provider != NoneId,
                Group = "Other voices",
                GroupHelp =
                    "Who else D47 speaks as. Each of these is a different person from your ship's AI, "
                    + "and they never borrow its voice unless you leave them empty.",
                DocsAnchor = "carrier-voices",
                Binding = new SettingBinding
                {
                    Read = s => s.Speech.CarrierCaptainVoice,
                    Write = (s, v) => s with { Speech = s.Speech with { CarrierCaptainVoice = v } },
                },
            },
            new SettingRow
            {
                Key = TowerVoiceKey,
                Label = "Carrier tower voice",
                Help = "Who handles arrivals and departures. A different person from the captain.",
                Kind = SettingKind.Choice,
                DefaultDisplay = "(the ship AI's voice)",
                AllowsFreeText = true,
                ChoiceSource = _ => surface.Voices?.Invoke() ?? [],
                ChoiceLabel = id => surface.VoiceLabel?.Invoke(id) ?? id,
                AppliesWhen = s => s.Speech.Provider != NoneId,
                Group = "Other voices",
                DocsAnchor = "carrier-voices",
                Binding = new SettingBinding
                {
                    Read = s => s.Speech.TowerVoice,
                    Write = (s, v) => s with { Speech = s.Speech with { TowerVoice = v } },
                },
            },
            new SettingRow
            {
                Key = SpeakIncomingKey,
                Label = "Speak incoming messages",
                Help = "Read in-game chat aloud, each sender in their own voice. Off by default.",
                Kind = SettingKind.Toggle,
                DefaultDisplay = "off",
                AppliesWhen = s => s.Speech.Provider != NoneId,
                Group = "Other voices",
                DocsAnchor = "incoming-messages",
                Binding = new SettingBinding
                {
                    Read = s => s.Speech.SpeakIncomingMessages ? "true" : "false",
                    Write = (s, v) => s with
                    {
                        Speech = s.Speech with { SpeakIncomingMessages = v is not "false" and not null },
                    },
                },
            },
            new SettingRow
            {
                Key = SpeakNpcKey,
                Label = "Include NPC chatter",
                Help = "Also speak messages from NPCs. A station approach produces a lot of these.",
                Kind = SettingKind.Toggle,
                DefaultDisplay = "off",

                // Only meaningful once messages are being spoken at all, so it is absent rather
                // than greyed out until then — a disabled control still asserts the setting exists.
                AppliesWhen = s => s.Speech.Provider != NoneId && s.Speech.SpeakIncomingMessages,
                Group = "Other voices",
                DocsAnchor = "incoming-messages",
                Binding = new SettingBinding
                {
                    Read = s => s.Speech.SpeakNpcMessages ? "true" : "false",
                    Write = (s, v) => s with
                    {
                        Speech = s.Speech with { SpeakNpcMessages = v is not "false" and not null },
                    },
                },
            },
            new SettingRow
            {
                Key = OutputDeviceKey,
                Label = "Output device",
                Help = "Where D47 speaks. Defaults to whatever Windows is using.",
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
                Help = "A short sound as D47 starts listening, starts thinking, and finishes.",
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
                Help = "Which loop plays while D47 works.",
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
                    "Silences D47 instantly, from anywhere — including while Elite has the foreground. " +
                    "Press the key combination to bind it.",
                Kind = SettingKind.Hotkey,
                DefaultDisplay = "(unbound)",
                DocsAnchor = "shut-up",

                // Claimed from the whole system, so a bare key is refused as it is bound.
                SystemWide = true,

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
                    "otherwise indistinguishable from D47 having ignored you.",
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

                // Half-seconds. The value has always been a double and the row has always read
                // it back to a tenth; without a step it could only ever hold whole ones.
                Step = 0.5,
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
                Step = 0.5,
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

                // The selected provider's own words, not Edge's. This row used to state Edge's
                // disclosure unconditionally, which was true while Edge was the only provider
                // and becomes a false statement the moment a second one is selectable.
                Help = "Exactly what leaves this machine to be spoken, for the provider you have selected.",
                DocsAnchor = "egress",
                Binding = new SettingBinding
                {
                    Read = s => TtsProviderCatalog.Selected(s.Speech.Provider).Egress,
                },
            },
        };

        // One key row per provider that needs one, rather than a single row whose secret name
        // shifts underneath it. Each declares when it applies, so only the selected provider's
        // key is on screen — the same shape the language-model capability uses.
        rows.AddRange(
            from provider in TtsProviderCatalog.All
            where provider.NeedsKey
            select new SettingRow
            {
                Key = KeyRowFor(provider),
                Label = $"{provider.Name} API key",
                Help = "Stored encrypted for this Windows account. Write-only: D47 will never show it back to you.",
                Kind = SettingKind.Secret,
                SecretName = provider.KeySecretName,
                DocsAnchor = "api-key",
                AppliesWhen = s => string.Equals(s.Speech.Provider, provider.Id, StringComparison.OrdinalIgnoreCase),
            });

        return rows;
    }

    /// <summary>
    /// Kept as the name the rest of the app already imports, now reading from the one place the
    /// disclosures live (<see cref="TtsProviderCatalog"/>) rather than asserting Edge's text as
    /// though it were every provider's.
    /// </summary>
    public static string EdgeEgress => TtsProviderCatalog.Edge.Egress;

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

    /// <summary>
    /// The same settings with every chosen voice dropped, for use when the speech provider
    /// changes.
    /// <para>
    /// A voice id is only meaningful to the provider that issued it. Carried across a switch
    /// they are not preferences that happen to be stale, they are identifiers for a service
    /// that has never heard of them — ElevenLabs answers <c>en-US-RogerNeural</c> with "an
    /// invalid ID has been received", and every sentence fails while the cues, which need no
    /// voice, keep playing. That reads as d47 choosing to be quiet.
    /// </para>
    /// <para>
    /// The persona pairings go with them, and <c>VoicesPaired</c> is cleared so they are chosen
    /// again from the new provider's list. Leaving that flag set was how eleven cores kept
    /// pointing at voices that had stopped existing.
    /// </para>
    /// <para>
    /// <paramref name="chosenFor"/> is the provider now selected, stamped on the way through so
    /// the file says which provider its voices belong to. Without it the agreement could only be
    /// checked by watching the switch happen, which is no help to a file that arrives already
    /// mismatched — see <see cref="SpeechSettings.VoicesProvider"/>.
    /// </para>
    /// <para>
    /// Dropped rather than remembered per provider, which is what <see cref="SpeechSettings
    /// .ProviderRates"/> does for the rate. Voices are a larger structure — the ship's, two
    /// named roles and one per core — and the fix for a Commander who cannot hear anything
    /// should not wait on a schema that can hold all of it twice.
    /// </para>
    /// </summary>
    public static D47Settings WithoutChosenVoices(D47Settings settings, string chosenFor) => settings with
    {
        Speech = settings.Speech with
        {
            Voice = null,
            CarrierCaptainVoice = null,
            TowerVoice = null,
            VoicesProvider = chosenFor,
        },
        Persona = settings.Persona with
        {
            Voices = new Dictionary<string, string>(StringComparer.Ordinal),
            VoicesPaired = false,
        },
    };

    /// <summary>
    /// The same settings with one voice id removed from every place that could hold it.
    /// <para>
    /// For a voice the provider itself refused. Unlike a provider switch this is not "these all
    /// belong to somebody else" — the rest of the choices are fine and only this one has stopped
    /// working, so only this one goes. It can be sitting in the ship AI's slot, either named
    /// role, or any number of persona pairings, and leaving a copy anywhere means the next turn
    /// that reaches that copy fails exactly as before.
    /// </para>
    /// </summary>
    public static D47Settings WithoutTheVoice(D47Settings settings, string voiceId) => settings with
    {
        Speech = settings.Speech with
        {
            Voice = Unless(settings.Speech.Voice, voiceId),
            CarrierCaptainVoice = Unless(settings.Speech.CarrierCaptainVoice, voiceId),
            TowerVoice = Unless(settings.Speech.TowerVoice, voiceId),
        },
        Persona = settings.Persona with
        {
            Voices = settings.Persona.Voices
                .Where(pair => !string.Equals(pair.Value, voiceId, StringComparison.Ordinal))
                .ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal),

            // Left as it was. The pairing ran and its result is still mostly good; re-running it
            // would replace every core's voice because one of them stopped resolving.
            VoicesPaired = settings.Persona.VoicesPaired,
        },
    };

    private static string? Unless(string? held, string? unwanted) =>
        string.Equals(held, unwanted, StringComparison.Ordinal) ? null : held;

    /// <summary>
    /// The rate in force: this provider's own if it has one, otherwise the general one. Read
    /// through here by the row and by the app, so the two cannot disagree about which value is
    /// actually being spoken at.
    /// </summary>
    public static double RateFor(D47Settings settings)
    {
        var provider = TtsProviderCatalog.Selected(settings.Speech.Provider);

        var rate = settings.Speech.ProviderRates.TryGetValue(provider.Id, out var own)
            ? own
            : settings.Speech.Rate;

        // Clamped to what the selected provider will actually accept, so a value carried over
        // from a provider with a wider range degrades to this one's fastest rather than being
        // rejected as a request and arriving as silence.
        return Math.Clamp(rate, provider.MinimumRate, provider.MaximumRate);
    }

    /// <summary>
    /// Writes the rate against the provider it was chosen for, never as the general one. The
    /// general value stays whatever a fresh install had, so clearing a provider's override
    /// falls back to something sensible rather than to the last provider's number.
    /// </summary>
    /// <summary>
    /// Which voice the ship's AI speaks in with a given core aboard: that core's, then the one
    /// value a settings file written before voices were kept per core still holds, then nothing
    /// — which the provider answers with its own default.
    /// <para>
    /// Read by the Voice row and by the app that does the speaking, so the row cannot show one
    /// voice while another is heard. That is not hypothetical: the row and the speaking path
    /// disagreed for the whole of Phase 11, and the pairing lost.
    /// </para>
    /// </summary>
    public static string? ShipVoiceFor(D47Settings settings, string personaId) =>
        settings.Persona.Voices.GetValueOrDefault(personaId) ?? settings.Speech.Voice;

    /// <summary>
    /// Stores a chosen voice against the core aboard, and clears the one global choice that used
    /// to shadow every pairing.
    /// <para>
    /// Cleared rather than left, because a value that is only read when the core aboard has no
    /// pairing is a value that reappears the moment one is removed — the Commander would clear
    /// this row to be offered a voice they picked under a different core, months ago.
    /// </para>
    /// <para>
    /// Clearing the row removes the pairing rather than writing an empty one, which is what
    /// makes "let d47 choose again" expressible: the next selection of this core has nothing
    /// stored and asks for one.
    /// </para>
    /// </summary>
    private static D47Settings WriteVoiceForCoreAboard(D47Settings settings, string? value)
    {
        var voices = new Dictionary<string, string>(settings.Persona.Voices, StringComparer.Ordinal);

        if (string.IsNullOrWhiteSpace(value))
        {
            voices.Remove(settings.Persona.Id);
        }
        else
        {
            voices[settings.Persona.Id] = value.Trim();
        }

        return settings with
        {
            Speech = settings.Speech with { Voice = null },
            Persona = settings.Persona with { Voices = voices },
        };
    }

    private static D47Settings WriteRate(D47Settings settings, string? value)
    {
        var provider = TtsProviderCatalog.Selected(settings.Speech.Provider);
        var rates = new Dictionary<string, double>(settings.Speech.ProviderRates, StringComparer.OrdinalIgnoreCase);

        if (value is null)
        {
            rates.Remove(provider.Id);
        }
        else
        {
            rates[provider.Id] = ParseDouble(value, settings.Speech.Rate, provider.MinimumRate, provider.MaximumRate);
        }

        return settings with { Speech = settings.Speech with { ProviderRates = rates } };
    }

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
