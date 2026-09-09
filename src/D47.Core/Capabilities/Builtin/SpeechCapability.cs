using D47.Core.Audio;
using D47.Core.Configuration;
using D47.Core.Conversation;
using D47.Core.Speech;

namespace D47.Core.Capabilities.Builtin;

/// <summary>
/// Everything audible: the voice, the device, the loop-state cues, the thinking bed, and the one
/// control that outranks all of them (Phase 5).
/// </summary>
public static class SpeechCapability
{
    public const string Id = "speech";

    public const string ProviderKey = "speech.provider";
    public const string LocalVoiceKey = "speech.localVoice";
    public const string LocalVoiceBuildKey = "speech.localVoiceBuild";
    public const string VoiceKey = "speech.voice";
    public const string RateKey = "speech.rate";

    /// <summary>Which ElevenLabs model speaks (#291).</summary>
    public const string ElevenLabsModelKey = "speech.elevenlabs.model";
    public const string OutputDeviceKey = "speech.outputDevice";
    public const string CuesKey = "speech.cues";
    public const string BedEnabledKey = "speech.thinkingBed";
    public const string BedKey = "speech.thinkingBedSound";
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
    /// The same row for a provider billed by the length of the audio rather than by the characters
    /// handed over (#63).
    /// </summary>
    public const string MinutePriceKey = "speech.minutePrice";
    public const string SpentKey = "speech.spent";
    public const string SpeakNpcKey = "speech.speakNpcMessages";

    /// <summary>
    /// The five per-channel rows (#299), each named for the row rather than for the raw <c>Channel</c>
    /// value it gates — <see cref="SpeakSquadronKey"/> covers both <c>squadron</c> and
    /// <c>squadleaders</c>, which is the whole reason they are keys and not the channel strings
    /// themselves.
    /// </summary>
    public const string SpeakSystemChatKey = "speech.speakSystemChat";

    public const string SpeakLocalChatKey = "speech.speakLocalChat";
    public const string SpeakWingChatKey = "speech.speakWingChat";
    public const string SpeakSquadronKey = "speech.speakSquadronChat";
    public const string SpeakDirectMessagesKey = "speech.speakDirectMessages";

    /// <summary>The secret row key for a voice provider's API key.</summary>
    public static string KeyRowFor(TtsProviderInfo provider) => $"speech.{provider.Id}.apiKey";

    /// <summary>The row key for one slot's provider (Phase 57).</summary>
    public static string SlotProviderKey(VoiceGroupInfo slot) => $"speech.provider.{slot.Id}";

    /// <summary>What has been spoken this session, broken down by slot (Phase 57).</summary>
    public const string SpentBySlotKey = "speech.spentBySlot";

    /// <summary>The sentence the three voice rows show when their picker has nothing in it.</summary>
    private static Func<D47Settings, string?> WhyNoVoices(SpeechSurface surface, VoiceGroup group) => _ =>
        surface.WhyNoVoices?.Invoke(group);

    /// <summary>
    /// The gender filter the three voice rows offer, or null where this provider's voices carry no
    /// gender to filter on (#146).
    /// </summary>
    private static SettingFacet? GenderFacet(SpeechSurface surface, VoiceGroup group)
    {
        if (surface.Voices?.Invoke(group) is not { Count: > 0 } voices || surface.VoiceGender is not { } tag)
        {
            return null;
        }

        VoiceGender Of(string id) => VoicePool.GenderOf(tag(group, id));

        // Nothing to filter on.
        if (!voices.Any(id => Of(id) != VoiceGender.Unlabelled))
        {
            return null;
        }

        return new SettingFacet
        {
            Label = "Gender",
            Options =
            [
                // First, so it is what the picker opens on and nothing is hidden by default.
                new SettingFacetOption("All", null),
                new SettingFacetOption("Female", id => Of(id) == VoiceGender.Feminine),
                new SettingFacetOption("Male", id => Of(id) == VoiceGender.Masculine),
                new SettingFacetOption("Unlabelled", id => Of(id) == VoiceGender.Unlabelled),
            ],
        };
    }

    /// <summary>
    /// The audition the three voice rows offer, or null where nothing composed one — under the designer
    /// and in tests, where the button is then absent rather than dead.
    /// </summary>
    private static SettingAudition? AuditionOf(SpeechSurface surface, VoiceRole role) =>
        surface.Audition is not { } play ? null : new SettingAudition
        {
            Play = (voiceId, token) => play(voiceId, role, token),

            // Priced and gated against the provider speaking for *this* row's slot, not the ship's.
            Cost = AuditionCost(VoiceGroups.Of(role)),
            Unavailable = AuditionUnavailable(surface, VoiceGroups.Of(role)),
        };

    /// <summary>What auditioning a voice costs.</summary>
    private static Func<D47Settings, string> AuditionCost(VoiceGroup group) => settings =>
    {
        var provider = TtsProviderCatalog.Selected(VoiceGroups.ProviderFor(settings.Speech, group));

        if (!provider.Billed)
        {
            return "Play a voice to hear it. This provider costs nothing.";
        }

        // Cached for the session, so this is what the *first* press of a given voice costs and the ones after
        // it are nothing.
        return SpeechSpend.RateFor(settings, provider.Id) is { } rate
            ? $"Play a voice to hear it. Each one costs about {(rate * AuditionLine.TypicalCharacters / 1000m):C3}."
            : "Play a voice to hear it. This provider charges for each one.";
    };

    /// <summary>Why the button cannot be pressed.</summary>
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

    /// <summary>The provider ids the settings row offers. "none" is a first-class choice.</summary>
    public const string NoneId = TtsProviderCatalog.NoneId;
    public const string EdgeId = TtsProviderCatalog.EdgeId;
    public const string ElevenLabsId = TtsProviderCatalog.ElevenLabsId;

    /// <summary>Everything the arbiter needs from the outside world, supplied by the app.</summary>
    public sealed record SpeechSurface
    {
        /// <summary>
        /// What the local voice needs, said in one line: whether it is on this machine and what
        /// fetching it would cost (Phase 59).
        /// </summary>
        public Func<string>? LocalVoiceState { get; init; }

        /// <summary>Fetches the local voice.</summary>
        public Func<LongPress?>? DownloadLocalVoice { get; init; }

        /// <summary>Which of Kokoro's eight builds is on this machine, or null where none is (#139).</summary>
        public Func<string?>? InstalledLocalVoiceBuild { get; init; }

        /// <summary>Fetches a different build of the local voice model and swaps it in (#139).</summary>
        public Func<string, LongPress?>? SwitchLocalVoiceBuild { get; init; }

        /// <summary>Stops everything audible, immediately.</summary>
        public required Action Silence { get; init; }

        /// <summary>Voices the provider speaking for one slot offers, or empty when it cannot say.</summary>
        public Func<VoiceGroup, IReadOnlyList<string>>? Voices { get; init; }

        /// <summary>
        /// Why <see cref="Voices"/> came back empty, in a sentence, or null when it did not or when
        /// nothing better than the picker's own wording is known.
        /// </summary>
        public Func<VoiceGroup, string?>? WhyNoVoices { get; init; }

        /// <summary>What speech has cost this session (Phase 19).</summary>
        public Func<Audio.SpeechSpend?>? SpeechSpend { get; init; }

        /// <summary>Speaks one voice so it can be judged before it is chosen (Phase 19).</summary>
        public Func<string, VoiceRole, CancellationToken, Task>? Audition { get; init; }

        /// <summary>
        /// Whether the selected provider has whatever credential it needs, or true where it needs none.
        /// </summary>
        public Func<VoiceGroup, bool>? HasKey { get; init; }

        /// <summary>Tries a provider's stored key against the real service (Phase 16).</summary>
        public Func<string, CancellationToken, Task<SecretCheck>>? VerifyKey { get; init; }

        /// <summary>Output devices, as id/label pairs the picker can render.</summary>
        public Func<IReadOnlyList<string>>? OutputDevices { get; init; }

        public Func<string, string>? DeviceLabel { get; init; }

        public Func<VoiceGroup, string, string>? VoiceLabel { get; init; }

        /// <summary>
        /// What the provider tags one voice's gender as, unchanged and untranslated, or null where it
        /// says nothing (#146).
        /// </summary>
        public Func<VoiceGroup, string, string?>? VoiceGender { get; init; }

        /// <summary>Bed names — shipped and dropped in.</summary>
        public required Func<IReadOnlyList<string>> Beds { get; init; }

        /// <summary>How a bed reads on the row.</summary>
        public Func<string, string>? BedLabel { get; init; }
    }

    public static CapabilityDescriptor Create(SpeechSurface surface) => new()
    {
        Id = Id,
        Group = "Voice",
        Name = "Speech",
        Summary = "Speak replies aloud, mark each loop state with its own cue, and stop on command.",
        Examples = ["stop", "be quiet", "shut up"],

        // The fastest thing a Commander can say, and the reason InterruptKeywords exists as a separate list.
        InterruptKeywords = ["stop", "stop it", "enough", "quiet"],

        // Phrases that can only be a request for silence. "stop" alone is not one of them: it is the first
        // word of "stop the ship", "stop plotting", and a dozen other things — and neither is a bare
        // "silence", which turns any sentence mentioning it into a command.
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

                // The one tool that must answer while a turn is mid-sentence, since that is the only moment
                // it is ever wanted.
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
            // **First on the card** (the Commander's instruction, 2026-09-01, "for now — a consolidation is
            // coming").
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
                DefaultDisplaySource = s => s.Llm.Provider == LlmProviderCatalog.NoneId
                    ? $"({TtsProviderCatalog.Selected(s.Speech.Provider).Name}'s own default voice)"
                    : "(the voice d47 picks for this core)",
                DefaultDisplay = "(the voice d47 picks for this core)",
                AllowsFreeText = true,
                ChoiceSource = _ => surface.Voices?.Invoke(VoiceGroup.Aboard) ?? [],
                ChoiceLabel = id => surface.VoiceLabel?.Invoke(VoiceGroup.Aboard, id) ?? id,
                WhyNoChoices = WhyNoVoices(surface, VoiceGroup.Aboard),
                Facet = _ => GenderFacet(surface, VoiceGroup.Aboard),
                Audition = AuditionOf(surface, VoiceRole.ShipAi),
                AppliesWhen = s => s.Speech.Provider != NoneId,
                DocsAnchor = "voice",

                // Per core, not one voice for the app.
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

                // Fifths, because that is the unit the help text is written in.
                Step = 0.05,
                DefaultDisplay = "1.0",

                // Not offered where it would do nothing.
                AppliesWhen = RateCanBeSet,
                DocsAnchor = "rate",
                Binding = new SettingBinding
                {
                    // The same format the row's step derives, not a second one.
                    Read = s => RateFor(s).ToString("0.##", System.Globalization.CultureInfo.InvariantCulture),
                    Write = (s, v) => WriteRate(s, v),
                },
            },
            // Below voice and rate rather than beside the provider: the block above is the deliberate
            // arrangement TheKeyRowSitsBesideItsProviderTests pins -- each key beside the provider that needs
            // it, then the voice, then the rate -- and this is setup rather than a choice about how d47
            // sounds.
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

                // A press rather than a choice, and the button is absent rather than dead where nothing can
                // run the download. Asked at press time, never at build time, and that is the whole reason
                // the delegate returns a function. Rows are built once, before AppHost has finished
                // constructing itself — its `self` is still null while the capability list is assembled.
                PressLabel = surface.DownloadLocalVoice is null ? null : "Download it",
                PressAsync = surface.DownloadLocalVoice is null
                    ? null
                    : (progress, cancellationToken) =>
                        surface.DownloadLocalVoice.Invoke() is { } fetch
                            ? fetch(progress, cancellationToken)
                            : Task.FromResult<string?>(null),
                Binding = new SettingBinding
                {
                    Read = _ => surface.LocalVoiceState?.Invoke() ?? "Not available.",
                },
            },
            new SettingRow
            {
                Key = LocalVoiceBuildKey,
                Advanced = true,
                Label = "Local voice model build",
                Help =
                    "Kokoro publishes eight builds of the same model and they are not interchangeable "
                    + "on size alone — the smallest is the slowest, and one quantised build is nearly "
                    + "as large as the full one. Each choice states what it costs on disk and how long "
                    + "you wait before it starts speaking — for a typical spoken reply, timed on this "
                    + "machine, so treat the gap between them as the real figure rather than the "
                    + "seconds themselves. Choosing a different one downloads it, "
                    + "checks it, and replaces the one you have; a failed download leaves the build "
                    + "you were using in place. How they SOUND has not been ranked — fp32 is the "
                    + "reference and the default.",
                Kind = SettingKind.Choice,
                Choices = KokoroAssets.BuildIds,
                ChoiceLabel = id =>
                {
                    var build = KokoroAssets.BuildFor(id);

                    // Marked rather than hidden, the same way the speech model row marks its choices: a
                    // Commander comparing builds needs to know which one they are already running and which
                    // costs a download.
                    var installed = surface.InstalledLocalVoiceBuild?.Invoke();

                    return string.Equals(installed, build.Id, StringComparison.OrdinalIgnoreCase)
                        ? $"{build.Label} — installed"
                        : build.Label;
                },
                DefaultDisplay = KokoroAssets.DefaultBuildId,

                // The choice is the go-ahead: it stated its size and its speed in the list it was made from.
                FetchChoiceAsync = surface.SwitchLocalVoiceBuild is null
                    ? null
                    : (chosen, progress, cancellationToken) =>
                        surface.SwitchLocalVoiceBuild.Invoke(KokoroAssets.BuildFor(chosen).Id) is { } swap
                            ? swap(progress, cancellationToken)
                            : Task.FromResult<string?>("The local voice build cannot be changed here."),

                // Absent until the local voice is there at all.
                AppliesWhen = _ => surface.InstalledLocalVoiceBuild is not null
                                   && surface.InstalledLocalVoiceBuild.Invoke() is not null,
                DocsAnchor = "provider",
                Binding = new SettingBinding
                {
                    // What is on disk outranks what the file says. The byte count is a fact and the
                    // setting is a record of one, so a Commander who replaced model.onnx by hand reads the
                    // build they actually have rather than the one d47 last wrote.
                    Read = s => surface.InstalledLocalVoiceBuild?.Invoke()
                                ?? s.Speech.LocalVoiceBuild
                                ?? KokoroAssets.DefaultBuildId,
                    Write = (s, v) => s with
                    {
                        Speech = s.Speech with { LocalVoiceBuild = KokoroAssets.BuildFor(v).Id },
                    },
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

                // Tenths of a cent.
                Step = 0.001,
                Minimum = 0,
                Maximum = 10,

                // Only where money changes hands, and only where it changes hands per character.
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

                // Tenths of a cent, matching the character row.
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
                Facet = _ => GenderFacet(surface, VoiceGroup.Carrier),
                Audition = AuditionOf(surface, VoiceRole.CarrierCaptain),

                // Only on offer to a Commander who has one.
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
                Facet = _ => GenderFacet(surface, VoiceGroup.Carrier),
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

                // Only meaningful once messages are being spoken at all, so it is absent rather than greyed
                // out until then — a disabled control still asserts the setting exists.
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
                Key = SpeakSystemChatKey,
                Advanced = true,
                Label = "System chat",
                Help = "The comms panel's System tab (Elite calls it \"starsystem\" chat) — everyone currently in your system, not just those nearby.",
                Kind = SettingKind.Toggle,
                DefaultDisplay = "on",
                AppliesWhen = s => s.Speech.Provider != NoneId && s.Speech.SpeakIncomingMessages,
                Group = "Other voices",
                DocsAnchor = "incoming-messages",
                Binding = new SettingBinding
                {
                    Read = s => s.Speech.SpeakSystemChat ? "true" : "false",
                    Write = (s, v) => s with
                    {
                        Speech = s.Speech with { SpeakSystemChat = v is not "false" and not null },
                    },
                },
            },
            new SettingRow
            {
                Key = SpeakLocalChatKey,
                Advanced = true,
                Label = "Local chat",
                Help = "The comms panel's Local tab — anyone physically near you, regardless of system-wide chat.",
                Kind = SettingKind.Toggle,
                DefaultDisplay = "on",
                AppliesWhen = s => s.Speech.Provider != NoneId && s.Speech.SpeakIncomingMessages,
                Group = "Other voices",
                DocsAnchor = "incoming-messages",
                Binding = new SettingBinding
                {
                    Read = s => s.Speech.SpeakLocalChat ? "true" : "false",
                    Write = (s, v) => s with
                    {
                        Speech = s.Speech with { SpeakLocalChat = v is not "false" and not null },
                    },
                },
            },
            new SettingRow
            {
                Key = SpeakWingChatKey,
                Advanced = true,
                Label = "Wing",
                Help = "The comms panel's Wing tab — your wing's private channel.",
                Kind = SettingKind.Toggle,
                DefaultDisplay = "on",
                AppliesWhen = s => s.Speech.Provider != NoneId && s.Speech.SpeakIncomingMessages,
                Group = "Other voices",
                DocsAnchor = "incoming-messages",
                Binding = new SettingBinding
                {
                    Read = s => s.Speech.SpeakWingChat ? "true" : "false",
                    Write = (s, v) => s with
                    {
                        Speech = s.Speech with { SpeakWingChat = v is not "false" and not null },
                    },
                },
            },
            new SettingRow
            {
                Key = SpeakSquadronKey,
                Advanced = true,
                Label = "Squadron",
                Help = "The comms panel's Squadron tab — your squadron's channel, including squadron leadership broadcasts.",
                Kind = SettingKind.Toggle,
                DefaultDisplay = "on",
                AppliesWhen = s => s.Speech.Provider != NoneId && s.Speech.SpeakIncomingMessages,
                Group = "Other voices",
                DocsAnchor = "incoming-messages",
                Binding = new SettingBinding
                {
                    Read = s => s.Speech.SpeakSquadronChat ? "true" : "false",
                    Write = (s, v) => s with
                    {
                        Speech = s.Speech with { SpeakSquadronChat = v is not "false" and not null },
                    },
                },
            },
            new SettingRow
            {
                Key = SpeakDirectMessagesKey,
                Advanced = true,
                Label = "Direct messages",
                Help = "The comms panel's Direct Messages tab (Elite calls it \"player\" chat) — a message sent to you by name.",
                Kind = SettingKind.Toggle,
                DefaultDisplay = "on",
                AppliesWhen = s => s.Speech.Provider != NoneId && s.Speech.SpeakIncomingMessages,
                Group = "Other voices",
                DocsAnchor = "incoming-messages",
                Binding = new SettingBinding
                {
                    Read = s => s.Speech.SpeakDirectMessages ? "true" : "false",
                    Write = (s, v) => s with
                    {
                        Speech = s.Speech with { SpeakDirectMessages = v is not "false" and not null },
                    },
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

                // Read from the library rather than listed here, which would be a second place for a name to
                // be wrong (Phase 5, #20) — and asked for each time it is opened rather than captured, so a
                // bed dropped into data/audio/beds is offered without a restart (Phase 12).
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

                // Half-seconds.
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

                // The selected provider's own words, not Edge's.
                Help = "Exactly what leaves this machine to be spoken, for the provider you have selected.",

                // On a tooltip rather than inline.
                ValueAsHint = true,
                DocsAnchor = "egress",
                Binding = new SettingBinding
                {
                    Read = s => TtsProviderCatalog.Selected(s.Speech.Provider).Egress,
                },
            },
        };

        // One row per slot that is not the ship's, offering the same providers the row above does.
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

                // Not every provider, for a slot carrying other people's words: one that cannot be told a
                // language would read a French message in French, in the voice the Commander chose for
                // English.
                Choices = [.. TtsProviderCatalog.For(slot).Select(provider => provider.Id)],
                ChoiceLabel = id => TtsProviderCatalog.Selected(id).Label,

                // What an unwritten entry means, said in the row rather than left to be inferred from a
                // blank: absent follows the ship's provider, which is what a settings file from before this
                // phase does and what it sounded like.
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

                // This slot's own consequence, not whichever provider happens to be selected for the ship.
                EgressFor = s => EgressDisclosure.TextToSpeechForSlot(
                    slot,
                    TtsProviderCatalog.Selected(VoiceGroups.ProviderFor(s.Speech, slot.Group))),
                Binding = new SettingBinding
                {
                    Read = s => s.Speech.GroupProviders?.GetValueOrDefault(slot.Id),
                    Write = (s, v) => WriteSlotProvider(s, slot, v),
                },
            });

        // One key row per provider that needs one, rather than a single row whose secret name shifts
        // underneath it.
        rows.InsertRange(
            rows.FindIndex(row => row.Key == ProviderKey) + 1,
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

                // This provider's own disclosure, not whichever one happens to be selected.
                EgressFor = _ => EgressDisclosure.TextToSpeechFor(provider),

                // Verified against the provider's own voice list, which is the real call this key is for —
                // and the one d47 makes anyway the moment the key lands.
                Verify = surface.VerifyKey is { } verify
                    ? token => verify(provider.Id, token)
                    : null,
                // On screen while *any* slot names this provider, not only while the ship does.
                AppliesWhen = s => VoiceGroups.Selected(s.Speech).Values
                    .Any(id => string.Equals(id, provider.Id, StringComparison.OrdinalIgnoreCase)),
            });

        // Between the voice and the rate, not among the keys.
        rows.Insert(
            rows.FindIndex(row => row.Key == VoiceKey) + 1,
            new SettingRow
            {
                Key = ElevenLabsModelKey,
                Label = "ElevenLabs model",
                Help =
                    "v3 Conversational performs delivery direction such as a sigh or an alarmed "
                    + "line, and takes about two seconds a line. Flash 2.5 takes about a third of "
                    + "a second and reads direction out loud instead, so D47 does not send it any. "
                    + "Flash is also the only one of the two with a speaking rate.",
                Kind = SettingKind.Choice,
                Choices = [.. ElevenLabsModels.All.Select(model => model.Id)],
                ChoiceLabel = id => ElevenLabsModels.All.FirstOrDefault(model => model.Id == id).Label ?? id,
                DocsAnchor = "elevenlabs-model",

                // On screen while any slot speaks through ElevenLabs, the same rule as its key — the carrier
                // can be on ElevenLabs while the ship is on Edge, and the model is what that carrier will be
                // heard in.
                AppliesWhen = s => VoiceGroups.Selected(s.Speech).Values
                    .Any(id => string.Equals(
                        id, TtsProviderCatalog.ElevenLabsId, StringComparison.OrdinalIgnoreCase)),
                Binding = new SettingBinding
                {
                    // Resolved on read rather than echoed, so a file naming a model d47 no longer offers
                    // shows the one that will actually speak.
                    Read = s => ElevenLabsModels.Named(s.Speech.ElevenLabsModel),
                    Write = (s, v) => s with
                    {
                        Speech = s.Speech with { ElevenLabsModel = ElevenLabsModels.Named(v) },
                    },
                },
            });

        return rows;
    }

    /// <summary>What one slot's row says it is for, with the warning where the warning belongs.</summary>
    private static string SlotHelp(VoiceGroupInfo slot) =>
        slot.OtherPeoplesWords
            ? $"Who speaks for {slot.Covers}. A paid provider here bills you per character for "
              + "text somebody else wrote, and they can write as much of it as they like."
            : $"Who speaks for {slot.Covers}.";

    /// <summary>Writes one slot's provider, and clearing the row puts that slot back onto the ship's.</summary>
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
    /// disclosures live (<see cref="TtsProviderCatalog"/>) rather than asserting Edge's text as though
    /// it were every provider's.
    /// </summary>
    public static string EdgeEgress => TtsProviderCatalog.Edge.Egress;

    /// <summary>
    /// The settings-to-policy conversion, in one place so the panel, the file and the turn loop cannot
    /// end up with three readings of the same four rows.
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
    /// The same settings with every chosen voice dropped, for use when the speech provider changes.
    /// </summary>
    public static D47Settings WithoutChosenVoices(D47Settings settings, string chosenFor) =>
        VoiceMemory.Switched(settings, settings.Speech.VoicesProvider, chosenFor);

    /// <summary>The same settings with one voice id removed from every place that could hold it.</summary>
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

            // Left as it was.
            VoicesPaired = settings.Persona.VoicesPaired,
        },
    };

    private static string? Unless(string? held, string? unwanted) =>
        string.Equals(held, unwanted, StringComparison.Ordinal) ? null : held;

    /// <summary>The rate in force: this provider's own if it has one, otherwise the general one.</summary>
    public static double RateFor(D47Settings settings) => RateFor(settings, settings.Speech.Provider);

    /// <summary>
    /// The same question asked of a named provider rather than of the selected one, which is what six
    /// slots made necessary (Phase 57).
    /// </summary>
    public static bool RateCanBeSet(D47Settings settings) =>
        RateCanBeSet(settings, TtsProviderCatalog.Selected(settings.Speech.Provider));

    private static bool RateCanBeSet(D47Settings settings, TtsProviderInfo provider) =>
        provider is { Speaks: true, RateCanBeSet: true }
        && (provider.Id != TtsProviderCatalog.ElevenLabsId
            || ElevenLabsModels.ReadsRate(settings.Speech.ElevenLabsModel));

    public static double RateFor(D47Settings settings, string? providerId)
    {
        var provider = TtsProviderCatalog.Selected(providerId);

        // A provider that cannot be told a rate speaks at its own pace, whatever a settings file says — the
        // half of the rule the picker cannot enforce, because `settings.json` is a file a Commander reads and
        // edits (Phase 60).
        if (!RateCanBeSet(settings, provider))
        {
            return 1.0;
        }

        var rate = settings.Speech.ProviderRates.TryGetValue(provider.Id, out var own)
            ? own
            : settings.Speech.Rate;

        // Clamped to what the selected provider will actually accept, so a value carried over from a provider
        // with a wider range degrades to this one's fastest rather than being rejected as a request and
        // arriving as silence.
        return Math.Clamp(rate, provider.MinimumRate, provider.MaximumRate);
    }

    /// <summary>Writes the rate against the provider it was chosen for, never as the general one.</summary>
    public static string? ShipVoiceFor(D47Settings settings, string personaId) =>
        settings.Persona.Voices.GetValueOrDefault(personaId) ?? settings.Speech.Voice;

    /// <summary>
    /// Stores a chosen voice against the core aboard, and clears the one global choice that used to
    /// shadow every pairing.
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

    /// <summary>Writes the price against the provider it was quoted for, never as a general one.</summary>
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
