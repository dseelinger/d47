using D47.Core.Audio;
using D47.Core.Configuration;
using D47.Core.Conversation;

namespace D47.Core.Capabilities.Builtin;

/// <summary>
/// Everything audible: the voice, the device, the loop-state cues, the thinking bed, and the
/// one control that outranks all of them (Phase 5).
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
    public const string LocalVoiceKey = "speech.localVoice";
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
    public const string CharacterPriceKey = "speech.characterPrice";

    /// <summary>
    /// The same row for a provider billed by the length of the audio rather than by the
    /// characters handed over (<a href="https://github.com/dseelinger/d47/issues/63">#63</a>).
    /// <para>
    /// A second key rather than a unit that changes underneath the first: <c>settings.json</c> is
    /// append-only, so <see cref="CharacterPriceKey"/> means dollars per thousand characters for
    /// ever. Only one of the two is ever on screen, because each declares the kind of provider it
    /// applies to — a row that does not apply is absent rather than reinterpreted.
    /// </para>
    /// </summary>
    public const string MinutePriceKey = "speech.minutePrice";
    public const string SpentKey = "speech.spent";
    public const string SpeakNpcKey = "speech.speakNpcMessages";

    /// <summary>The secret row key for a voice provider's API key. One row per provider needing one.</summary>
    public static string KeyRowFor(TtsProviderInfo provider) => $"speech.{provider.Id}.apiKey";

    /// <summary>
    /// The row key for one slot's provider (Phase 57). Beneath
    /// <see cref="ProviderKey"/> rather than beside it, because that row <em>is</em>
    /// <see cref="VoiceGroup.Aboard"/>'s and these are the other five.
    /// </summary>
    public static string SlotProviderKey(VoiceGroupInfo slot) => $"speech.provider.{slot.Id}";

    /// <summary>What has been spoken this session, broken down by slot (Phase 57).</summary>
    public const string SpentBySlotKey = "speech.spentBySlot";

    /// <summary>
    /// The sentence the three voice rows show when their picker has nothing in it. One
    /// expression shared by all three, because a Commander opening the carrier tower's picker
    /// deserves the same explanation as one opening the ship AI's — and because three copies of
    /// this is three places for it to stop agreeing with the provider.
    /// </summary>
    private static Func<D47Settings, string?> WhyNoVoices(SpeechSurface surface, VoiceGroup group) => _ =>
        surface.WhyNoVoices?.Invoke(group);

    /// <summary>
    /// The audition the three voice rows offer, or null where nothing composed one — under the
    /// designer and in tests, where the button is then absent rather than dead.
    /// <para>
    /// One expression for all three, and the role is what tells them apart. A Commander casting
    /// the carrier tower's voice is listening for something different from one casting a
    /// companion, so the tower auditions as the tower rather than reciting the core's opening —
    /// which would be the same category error the item exists to fix, one level down.
    /// </para>
    /// </summary>
    private static SettingAudition? AuditionOf(SpeechSurface surface, VoiceRole role) =>
        surface.Audition is not { } play ? null : new SettingAudition
        {
            Play = (voiceId, token) => play(voiceId, role, token),

            // Priced and gated against the provider speaking for *this* row's slot, not the
            // ship's. A tower on free Edge beside a companion on a paid provider must not be
            // announced as costing a fraction of a cent a press (Phase 57).
            Cost = AuditionCost(VoiceGroups.Of(role)),
            Unavailable = AuditionUnavailable(surface, VoiceGroups.Of(role)),
        };

    /// <summary>
    /// What auditioning a voice costs. The whole point is that the price is in front of the
    /// Commander rather than discovered afterwards on the bill: each press is a synthesis request
    /// billed by the character, and the character count is known before it is sent.
    /// <para>
    /// A sentence about the list, not a caption. It was a button label until
    /// change-requests.md 18 made the control a play glyph on each row — the disclosure was the
    /// half of that button worth keeping, so it moved to the line above the list and to the
    /// pointer text on every glyph in it.
    /// </para>
    /// <para>
    /// Free reads as free. A provider that costs nothing must not be described with a figure,
    /// for the same reason the spend line does not print "$0.00" for Edge (Phase 19).
    /// </para>
    /// </summary>
    private static Func<D47Settings, string> AuditionCost(VoiceGroup group) => settings =>
    {
        var provider = TtsProviderCatalog.Selected(VoiceGroups.ProviderFor(settings.Speech, group));

        if (!provider.Billed)
        {
            return "Play a voice to hear it. This provider costs nothing.";
        }

        // Cached for the session, so this is what the *first* press of a given voice costs and
        // the ones after it are nothing. Said as an estimate because the line length varies with
        // the core aboard, and because the rate itself is a list price the Commander may have
        // corrected.
        return SpeechSpend.RateFor(settings, provider.Id) is { } rate
            ? $"Play a voice to hear it. Each one costs about {(rate * AuditionLine.TypicalCharacters / 1000m):C3}."
            : "Play a voice to hear it. This provider charges for each one.";
    };

    /// <summary>
    /// Why the button cannot be pressed. Two reasons, and both are things the Commander can act
    /// on from rows they can see — which is the difference between a shut button and a broken one.
    /// </summary>
    private static Func<D47Settings, string?> AuditionUnavailable(SpeechSurface surface, VoiceGroup group) =>
        settings =>
    {
        var provider = TtsProviderCatalog.Selected(VoiceGroups.ProviderFor(settings.Speech, group));

        if (!provider.Speaks)
        {
            return "No voice provider is selected, so there is nothing to hear it with.";
        }

        return provider.NeedsKey && surface.HasKey?.Invoke(group) == false
            ? $"{provider.Name} needs an API key before it will speak."
            : null;
    };

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
        /// <summary>
        /// What the local voice needs, said in one line: whether it is on this machine and what
        /// fetching it would cost (Phase 59). Null where nothing can answer, and the row then
        /// reads as unavailable rather than absent.
        /// </summary>
        public Func<string>? LocalVoiceState { get; init; }

        /// <summary>
        /// Fetches the local voice. A function returning an action rather than the action, for
        /// the reason every long-running press here uses: Core owns no thread, the download is
        /// hundreds of megabytes, and the App is what decides where that runs. A caller with
        /// nowhere to run it answers null and the row keeps its state and loses its button.
        /// </summary>
        public Func<Action?>? DownloadLocalVoice { get; init; }

        /// <summary>Stops everything audible, immediately. The whole point of the capability.</summary>
        public required Action Silence { get; init; }

        /// <summary>
        /// Voices the provider speaking for one slot offers, or empty when it cannot say. Takes
        /// the slot because since Phase 57 the ship's AI and the carrier's tower can be on two
        /// different providers, and offering one provider's ids in the other's picker is offering
        /// a list of values that will be refused.
        /// </summary>
        public Func<VoiceGroup, IReadOnlyList<string>>? Voices { get; init; }

        /// <summary>
        /// Why <see cref="Voices"/> came back empty, in a sentence, or null when it did not or
        /// when nothing better than the picker's own wording is known.
        /// <para>
        /// Separate from <see cref="Voices"/> because an empty list is four different situations
        /// and only the provider knows which — no key stored, a key it refused, no answer at all,
        /// or an account that genuinely holds none. Three of those look identical from here and
        /// two of them are the Commander's to fix (Phase 19).
        /// </para>
        /// </summary>
        public Func<VoiceGroup, string?>? WhyNoVoices { get; init; }

        /// <summary>
        /// What speech has cost this session (Phase 19). A function because the tracker
        /// is the app's and lives for the process, and Core holds no reference to it — the row
        /// asks at render time, which is also what keeps the figure current without anything
        /// having to push it.
        /// </summary>
        public Func<Audio.SpeechSpend?>? SpeechSpend { get; init; }

        /// <summary>
        /// Speaks one voice so it can be judged before it is chosen (Phase 19). Takes the
        /// voice id and the role it is being cast in; the app decides what it says, because the
        /// ship AI's line is the core's own and the core aboard is the app's to know.
        /// <para>
        /// Null where nothing can make a sound — under the designer, and in every test that is
        /// not about it — and the button is then absent rather than present and inert.
        /// </para>
        /// </summary>
        public Func<string, VoiceRole, CancellationToken, Task>? Audition { get; init; }

        /// <summary>
        /// Whether the selected provider has whatever credential it needs, or true where it needs
        /// none. The secret store is the app's and settings do not hold keys, so this is the only
        /// way a row can tell "no key yet" from "ready".
        /// </summary>
        public Func<VoiceGroup, bool>? HasKey { get; init; }

        /// <summary>
        /// Tries a provider's stored key against the real service (Phase 16). Takes the
        /// provider id, because the row that asks is the row for one provider and the surface has
        /// to know which.
        /// <para>
        /// Null where nothing can make the call, which is every test that does not care.
        /// </para>
        /// </summary>
        public Func<string, CancellationToken, Task<SecretCheck>>? VerifyKey { get; init; }

        /// <summary>Output devices, as id/label pairs the picker can render.</summary>
        public Func<IReadOnlyList<string>>? OutputDevices { get; init; }

        public Func<string, string>? DeviceLabel { get; init; }

        public Func<VoiceGroup, string, string>? VoiceLabel { get; init; }

        /// <summary>
        /// Bed names — shipped and dropped in. Read from the cue library, never a literal list.
        /// <para>
        /// A function rather than a list, because the library is rebuilt when the Commander drops
        /// a file into <c>data/audio/beds</c> and a list captured at startup would go on offering
        /// the set that existed then (Phase 12).
        /// </para>
        /// </summary>
        public required Func<IReadOnlyList<string>> Beds { get; init; }

        /// <summary>
        /// How a bed reads on the row. Marks a Commander's own as theirs, so a drop-in is
        /// distinguishable from what d47 ships with rather than merely present alongside it.
        /// </summary>
        public Func<string, string>? BedLabel { get; init; }
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
        Display = new CapabilityDisplay { PanelTitle = "Speech", Order = 2 },
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
                    + "switches voice. The list comes from the selected provider, and clearing "
                    + "the row has d47 choose for this core again.",
                Kind = SettingKind.Choice,

                // Two different answers, because clearing this row does two different things.
                // With a model, it is the way back to the voice d47 chose for this core: the
                // pairing is dropped and one is chosen again, here, now. With no model there is
                // nothing to choose with, so it really is the provider's own default — named as
                // far as it can be, since d47 does not know which voice Edge or ElevenLabs picks
                // when asked for nothing, and a row that says only "(the default)" is a row
                // answering a question with the question.
                DefaultDisplaySource = s => s.Llm.Provider == LlmProviderCatalog.NoneId
                    ? $"({TtsProviderCatalog.Selected(s.Speech.Provider).Name}'s own default voice)"
                    : "(the voice d47 picks for this core)",
                DefaultDisplay = "(the voice d47 picks for this core)",
                AllowsFreeText = true,
                ChoiceSource = _ => surface.Voices?.Invoke(VoiceGroup.Aboard) ?? [],
                ChoiceLabel = id => surface.VoiceLabel?.Invoke(VoiceGroup.Aboard, id) ?? id,
                WhyNoChoices = WhyNoVoices(surface, VoiceGroup.Aboard),
                Audition = AuditionOf(surface, VoiceRole.ShipAi),
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

                // Not offered where it would do nothing. Cartesia validates a speed precisely and
                // then ignores it, so a row here would be a control that appears to work — the
                // exact failure docs/capabilities/listening.md names (Phase 60). The
                // resolver refuses it too; this half only keeps it off the screen.
                AppliesWhen = s => TtsProviderCatalog.Selected(s.Speech.Provider)
                    is { Speaks: true, RateCanBeSet: true },
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
            // Below voice and rate rather than beside the provider: the block above is the
            // deliberate arrangement TheKeyRowSitsBesideItsProviderTests pins -- each key beside
            // the provider that needs it, then the voice, then the rate -- and this is setup
            // rather than a choice about how d47 sounds.
            new SettingRow
            {
                Key = LocalVoiceKey,
                Label = "Local voice",
                Help =
                    "Kokoro runs on this computer, so nothing D47 says leaves it — including re-voiced "
                    + "messages written by other players. The model is downloaded once from "
                    + "huggingface.co and after that this needs no network at all.",
                Kind = SettingKind.Info,
                DocsAnchor = "provider",

                // A press rather than a choice, and the button is absent rather than dead when
                // nothing can run the download — the same shape every long-running press here has.
                PressLabel = surface.DownloadLocalVoice?.Invoke() is null ? null : "Download it",
                Press = surface.DownloadLocalVoice?.Invoke(),
                Binding = new SettingBinding
                {
                    Read = _ => surface.LocalVoiceState?.Invoke() ?? "Not available.",
                },
            },
            new SettingRow
            {
                Key = CharacterPriceKey,
                Advanced = true,
                Label = "Price per 1,000 characters",
                Help =
                    "What this provider charges, in US dollars, so the session's speech can be "
                    + "priced beside the model's. The default is their published list price for "
                    + "the model D47 asks for — correct it if your subscription pays a different "
                    + "rate, because the API does not say which one you are on.",
                Kind = SettingKind.Number,

                // Tenths of a cent. The published rates run from $0.05 to $0.20 per thousand, so
                // a step of a cent would be a fifth of the range.
                Step = 0.001,
                Minimum = 0,
                Maximum = 10,

                // Only where money changes hands, and only where it changes hands per character.
                // Edge and "none" cost nothing, and a price row on a free provider invites a
                // figure that would then be reported as spend.
                AppliesWhen = s => TtsProviderCatalog.Selected(s.Speech.Provider) is
                    { Billed: true, BilledByMinute: false },
                DefaultDisplaySource = s =>
                    TtsProviderCatalog.Selected(s.Speech.Provider).ListDollarsPerThousandCharacters
                        is { } list
                        ? list.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture)
                        : "(not published — no price will be quoted)",
                Group = "What it costs",
                GroupHelp =
                    "D47 counts what it actually sent and how much audio came back, which are "
                    + "facts; turning either into money needs a rate, which is not. Providers "
                    + "disagree about which of the two they bill for, so this row asks about "
                    + "whichever one yours uses.",
                DocsAnchor = "voice-cost",
                Binding = new SettingBinding
                {
                    Read = s => SpeechSpend.RateFor(s, s.Speech.Provider)
                        ?.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture),
                    Write = WriteCharacterPrice,
                },
            },
            new SettingRow
            {
                Key = MinutePriceKey,
                Advanced = true,
                Label = "Price per minute of audio",
                Help =
                    "What this provider charges for a minute of speech, in US dollars. D47 measures "
                    + "each clip's length exactly — it has the audio — so the minutes are a fact. "
                    + "The rate is not: correct it if your account pays differently.",
                Kind = SettingKind.Number,

                // Tenths of a cent, matching the character row. A minute of OpenAI speech is
                // around a cent and a half, so this is the same order of precision.
                Step = 0.001,
                Minimum = 0,
                Maximum = 10,

                AppliesWhen = s => TtsProviderCatalog.Selected(s.Speech.Provider).BilledByMinute,
                DefaultDisplaySource = s =>
                    TtsProviderCatalog.Selected(s.Speech.Provider).ListDollarsPerMinute is { } list
                        ? list.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture)
                        : "(not published — no price will be quoted)",
                Group = "What it costs",
                DocsAnchor = "voice-cost",
                Binding = new SettingBinding
                {
                    Read = s => SpeechSpend.MinuteRateFor(s, s.Speech.Provider)
                        ?.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture),
                    Write = WriteMinutePrice,
                },
            },
            new SettingRow
            {
                Key = SpentKey,
                Advanced = true,
                Label = "Spoken this session",
                Help =
                    "Characters handed to the voice provider since D47 started, and what that "
                    + "comes to at the rate above. Counted on synthesis that succeeded, so a "
                    + "refused request costs nothing and a line cut off part-way still counts "
                    + "what was already sent. There is no caching: the same sentence twice is "
                    + "billed twice.",
                Kind = SettingKind.Info,
                Group = "What it costs",
                DocsAnchor = "voice-cost",
                Binding = new SettingBinding
                {
                    Read = s => surface.SpeechSpend?.Invoke()?.Describe(s) ?? "Nothing spoken yet.",
                },
            },
            new SettingRow
            {
                Key = SpentBySlotKey,
                Advanced = true,
                Label = "Spoken by each voice",
                Help =
                    "The same characters, split by who was speaking. Which slot is costing money "
                    + "is a question worth being able to ask, and until each of them could name "
                    + "its own provider there was only ever one answer.",
                Kind = SettingKind.Info,
                Group = "What it costs",
                DocsAnchor = "voice-cost",
                Binding = new SettingBinding
                {
                    Read = s => surface.SpeechSpend?.Invoke()?.DescribeSlots(s) ?? "Nothing spoken yet.",
                },
            },
            new SettingRow
            {
                Key = CarrierCaptainVoiceKey,
                Advanced = true,
                Label = "Carrier captain voice",
                Help = "Who answers for your fleet carrier. Empty uses the ship AI's voice.",
                Kind = SettingKind.Choice,
                DefaultDisplay = "(the ship AI's voice)",
                AllowsFreeText = true,
                ChoiceSource = _ => surface.Voices?.Invoke(VoiceGroup.Carrier) ?? [],
                ChoiceLabel = id => surface.VoiceLabel?.Invoke(VoiceGroup.Carrier, id) ?? id,
                WhyNoChoices = WhyNoVoices(surface, VoiceGroup.Carrier),
                Audition = AuditionOf(surface, VoiceRole.CarrierCaptain),

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
                Advanced = true,
                Label = "Carrier tower voice",
                Help = "Who handles arrivals and departures. A different person from the captain.",
                Kind = SettingKind.Choice,
                DefaultDisplay = "(the ship AI's voice)",
                AllowsFreeText = true,
                ChoiceSource = _ => surface.Voices?.Invoke(VoiceGroup.Carrier) ?? [],
                ChoiceLabel = id => surface.VoiceLabel?.Invoke(VoiceGroup.Carrier, id) ?? id,
                WhyNoChoices = WhyNoVoices(surface, VoiceGroup.Carrier),
                Audition = AuditionOf(surface, VoiceRole.TowerControl),
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
                Advanced = true,
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
                Advanced = true,
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
                Advanced = true,
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
                Advanced = true,
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
                Advanced = true,
                Label = "Thinking bed sound",
                Help = "Which loop plays while D47 works.",
                Kind = SettingKind.Choice,

                // Read from the library rather than listed here, which would be a second place
                // for a name to be wrong (Phase 5, #20) — and asked for each time it is
                // opened rather than captured, so a bed dropped into data/audio/beds is offered
                // without a restart (Phase 12).
                ChoiceSource = _ => surface.Beds(),
                ChoiceLabel = surface.BedLabel,
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
                Advanced = true,
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
                Advanced = true,
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
                Advanced = true,
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
                Advanced = true,
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
                Advanced = true,
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
                Advanced = true,
                Label = "What the voice provider receives",
                Kind = SettingKind.Info,

                // The selected provider's own words, not Edge's. This row used to state Edge's
                // disclosure unconditionally, which was true while Edge was the only provider
                // and becomes a false statement the moment a second one is selectable.
                Help = "Exactly what leaves this machine to be spoken, for the provider you have selected.",

                // On a tooltip rather than inline. It is five lines of prose sitting between
                // rows that are one line each, and it is consulted rather than read
                // (remediation.md, "What the voice provider receives").
                ValueAsHint = true,
                DocsAnchor = "egress",
                Binding = new SettingBinding
                {
                    Read = s => TtsProviderCatalog.Selected(s.Speech.Provider).Egress,
                },
            },
        };

        // One row per slot that is not the ship's, offering the same providers the row above
        // does. Placed between the ship's own voice and what it costs, because that is the order
        // the decision is actually made in: pick the companion's voice, say where everybody else
        // comes from, then look at the bill (Phase 57).
        rows.InsertRange(
            rows.FindIndex(row => row.Key == CharacterPriceKey),
            from slot in VoiceGroups.All
            where slot.Group != VoiceGroup.Aboard
            select new SettingRow
            {
                Key = SlotProviderKey(slot),
                Advanced = true,
                Label = $"{slot.Name} — provider",
                Help = SlotHelp(slot),
                Kind = SettingKind.Choice,

                // Not every provider, for a slot carrying other people's words: one that cannot
                // be told a language would read a French message in French, in the voice the
                // Commander chose for English. The list and the resolver read the same rule, so
                // the picker cannot offer something the app would then refuse to use.
                Choices = [.. TtsProviderCatalog.For(slot).Select(provider => provider.Id)],
                ChoiceLabel = id => TtsProviderCatalog.Selected(id).Label,

                // What an unwritten entry means, said in the row rather than left to be inferred
                // from a blank: absent follows the ship's provider, which is what a settings file
                // from before this phase does and what it sounded like.
                DefaultDisplay = "(the same as your ship's)",
                AppliesWhen = s => s.Speech.Provider != NoneId,
                Group = "Where each voice comes from",
                GroupHelp =
                    "Every voice that reaches you over a radio can come from somewhere different "
                    + "from the one in your cockpit. Anything carrying another Commander's words "
                    + "defaults to Edge, which is free — so nobody else can spend your money by "
                    + "typing.",
                DocsAnchor = "voice-slots",
                EgressId = EgressDisclosure.TextToSpeech,

                // This slot's own consequence, not whichever provider happens to be selected for
                // the ship. The key row learned this lesson first: a disclosure resolved against
                // the wrong subject is worse than none, because it is believed.
                EgressFor = s => EgressDisclosure.TextToSpeechForSlot(
                    slot,
                    TtsProviderCatalog.Selected(VoiceGroups.ProviderFor(s.Speech, slot.Group))),
                Binding = new SettingBinding
                {
                    Read = s => s.Speech.GroupProviders?.GetValueOrDefault(slot.Id),
                    Write = (s, v) => WriteSlotProvider(s, slot, v),
                },
            });

        // One key row per provider that needs one, rather than a single row whose secret name
        // shifts underneath it. Each declares when it applies, so only the selected provider's
        // key is on screen — the same shape the language-model capability uses.
        //
        // Directly beneath the provider row, not at the foot of the card. Selecting ElevenLabs is
        // what makes its key relevant, and the row that answers a choice belongs where the choice
        // was made: appended, it sat below sixteen rows about rates, cues and retries, so the
        // dropdown asked for a key and the box to put it in was off the bottom of the screen.
        // Index 1 rather than a sort, because "after the provider" is the whole requirement.
        rows.InsertRange(
            1,
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
                EgressId = EgressDisclosure.TextToSpeech,

                // This provider's own disclosure, not whichever one happens to be selected. The
                // key row is offered before the provider is chosen — that is what the first-run
                // window is for — so resolving by selection described the wrong service on the
                // one screen where a Commander is deciding whether to trust d47 with a key.
                EgressFor = _ => EgressDisclosure.TextToSpeechFor(provider),

                // Verified against the provider's own voice list, which is the real call this key
                // is for — and the one d47 makes anyway the moment the key lands.
                Verify = surface.VerifyKey is { } verify
                    ? token => verify(provider.Id, token)
                    : null,
                // On screen while *any* slot names this provider, not only while the ship does.
                // Phase 57 let the carrier sit on ElevenLabs while the companion stayed on Edge,
                // and this row went on asking about `speech.provider` alone — so the slot that
                // needed a key was configured and the box to put one in was not on the page.
                AppliesWhen = s => VoiceGroups.Selected(s.Speech).Values
                    .Any(id => string.Equals(id, provider.Id, StringComparison.OrdinalIgnoreCase)),
            });

        return rows;
    }

    /// <summary>
    /// What one slot's row says it is for, with the warning where the warning belongs.
    /// <para>
    /// <b>On the change rather than on the state.</b> A slot sitting on a paid provider is not a
    /// standing alarm to be lived with; choosing one for a slot that carries other people's words
    /// is a decision, and the sentence belongs at the moment it is made. The disclosure on the
    /// row carries the rest.
    /// </para>
    /// </summary>
    private static string SlotHelp(VoiceGroupInfo slot) =>
        slot.OtherPeoplesWords
            ? $"Who speaks for {slot.Covers}. A paid provider here bills you per character for "
              + "text somebody else wrote, and they can write as much of it as they like."
            : $"Who speaks for {slot.Covers}.";

    /// <summary>
    /// Writes one slot's provider, and clearing the row puts that slot back onto the ship's.
    /// <para>
    /// The map is created on the first write if it was never written, which is the same act as
    /// the migration: a file that reaches here has a Commander choosing per slot, so "before this
    /// phase" has stopped being true of it.
    /// </para>
    /// </summary>
    private static D47Settings WriteSlotProvider(D47Settings settings, VoiceGroupInfo slot, string? value)
    {
        var providers = new Dictionary<string, string>(
            settings.Speech.GroupProviders ?? new Dictionary<string, string>(),
            StringComparer.OrdinalIgnoreCase);

        if (string.IsNullOrWhiteSpace(value))
        {
            providers.Remove(slot.Id);
        }
        else
        {
            providers[slot.Id] = TtsProviderCatalog.Selected(value).Id;
        }

        return settings with { Speech = settings.Speech with { GroupProviders = providers } };
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
    /// They are no longer <em>lost</em>, which is the half that changed in Phase 19. What comes
    /// out of the live slots is filed under the provider it belonged to, in
    /// <see cref="SpeechSettings.ProviderVoices"/>, and whatever was filed under the provider
    /// being switched to is put back — so switching to ElevenLabs to hear what it sounds like
    /// and switching back no longer costs eleven paired cores and the ship's own voice. The
    /// clearing this method is named for still happens; only the discarding stopped.
    /// <see cref="VoiceMemory"/> owns it, because it is a pure function of settings and it is
    /// where a test can reach it.
    /// </para>
    /// </summary>
    public static D47Settings WithoutChosenVoices(D47Settings settings, string chosenFor) =>
        VoiceMemory.Switched(settings, settings.Speech.VoicesProvider, chosenFor);

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
    public static double RateFor(D47Settings settings) => RateFor(settings, settings.Speech.Provider);

    /// <summary>
    /// The same question asked of a named provider rather than of the selected one, which is what
    /// six slots made necessary (Phase 57).
    /// <para>
    /// <b>A rate is a property of the synthesiser, so it follows the slot's provider.</b> The
    /// ranges are not comparable — Edge takes a wide percentage offset and ElevenLabs a narrow
    /// multiplier it <em>rejects outright</em> rather than clamping — so a 1.5 chosen for Edge,
    /// applied to a carrier that has since moved to ElevenLabs, is not a fast carrier: it is a
    /// silent one. This is not the per-role rate that change-requests.md 43 asks for and Phase 57
    /// deliberately left out; it is the existing per-provider row, read for the provider actually
    /// doing the speaking.
    /// </para>
    /// </summary>
    public static double RateFor(D47Settings settings, string? providerId)
    {
        var provider = TtsProviderCatalog.Selected(providerId);

        // A provider that cannot be told a rate speaks at its own pace, whatever a settings file
        // says — the half of the rule the picker cannot enforce, because `settings.json` is a file
        // a Commander reads and edits (Phase 60). Natural pace rather than the stored
        // number, so the value the app uses and the value the provider honours are the same one.
        if (!provider.RateCanBeSet)
        {
            return 1.0;
        }

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

    /// <summary>
    /// Writes the price against the provider it was quoted for, never as a general one. A rate
    /// is a property of one account with one supplier, so there is no such thing as the price of
    /// speech in the abstract — the same reason the speaking rate is kept per provider.
    /// <para>
    /// Clearing the row removes the override and the published list price stands again, which is
    /// what makes "I do not know what I am paying" expressible.
    /// </para>
    /// </summary>
    private static D47Settings WriteCharacterPrice(D47Settings settings, string? value)
    {
        var provider = TtsProviderCatalog.Selected(settings.Speech.Provider);
        var prices = new Dictionary<string, double>(settings.Speech.CharacterPrices, StringComparer.OrdinalIgnoreCase);

        if (value is null)
        {
            prices.Remove(provider.Id);
        }
        else
        {
            prices[provider.Id] = ParseDouble(value, 0, 0, 10);
        }

        return settings with { Speech = settings.Speech with { CharacterPrices = prices } };
    }

    private static D47Settings WriteMinutePrice(D47Settings settings, string? value)
    {
        var provider = TtsProviderCatalog.Selected(settings.Speech.Provider);
        var prices = new Dictionary<string, double>(settings.Speech.MinutePrices, StringComparer.OrdinalIgnoreCase);

        if (value is null)
        {
            prices.Remove(provider.Id);
        }
        else
        {
            prices[provider.Id] = ParseDouble(value, 0, 0, 10);
        }

        return settings with { Speech = settings.Speech with { MinutePrices = prices } };
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
