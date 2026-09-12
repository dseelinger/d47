using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;

namespace D47.Core.Configuration;

/// <summary>The settings store's whole shape.</summary>
public sealed record D47Settings
{
    /// <summary>Keys this build does not know, kept exactly as the file wrote them (#368).</summary>
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? Extra { get; init; }

    /// <summary>Settings as they are before anyone chooses anything.</summary>
    public static readonly D47Settings Defaults = new();

    public int SchemaVersion { get; init; } = 1;

    public LoggingSettings Logging { get; init; } = new();

    public LlmSettings Llm { get; init; } = new();

    public SpeechSettings Speech { get; init; } = new();

    /// <summary>Per-category level, mute and ducking (Phase 12, "#96 Ambient audio mixer").</summary>
    public Audio.AudioMix Audio { get; init; } = new();

    public UiSettings Ui { get; init; } = new();

    public HotkeySettings Hotkeys { get; init; } = new();

    public UpdateSettings Updates { get; init; } = new();

    /// <summary>Where a donated excerpt or journal history is sent, when one is (#175).</summary>
    public DonationSettings Donation { get; init; } = new();

    public CalloutSettings Callouts { get; init; } = new();

    public ListeningSettings Listening { get; init; } = new();

    public VrSettings Vr { get; init; } = new();

    public ActionSettings Actions { get; init; } = new();

    public PersonaSettings Persona { get; init; } = new();

    public KnowledgeSettings Knowledge { get; init; } = new();

    /// <summary>What d47 keeps about the Commander, and for how long (Phase 31).</summary>
    public MemorySettings Memory { get; init; } = new();

    /// <summary>Whether d47 debriefs itself after a session, and what it does with what it finds (#162).</summary>
    public DebriefSettings Debrief { get; init; } = new();

    /// <summary>How a Commander's log is written when one is asked for (Phase 33).</summary>
    public LogbookSettings Logbook { get; init; } = new();

    /// <summary>
    /// What each Commander has set of the rows that are theirs rather than the installation's (Phase
    /// 44).
    /// </summary>
    public IReadOnlyList<CommanderSettings> Commanders { get; init; } = [];
}

/// <summary>One Commander's overlay over the installation's settings (Phase 44).</summary>
public sealed record CommanderSettings
{
    /// <inheritdoc cref="D47Settings.Extra"/>
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? Extra { get; init; }

    public required string CommanderFid { get; init; }

    /// <summary>Who that is, for a person reading a file two Commanders share.</summary>
    public string? CommanderName { get; init; }

    /// <summary>This Commander's <see cref="LlmSettings.AboutMe"/>.</summary>
    public string? AboutMe { get; init; }

    /// <summary>This Commander's <see cref="LlmSettings.CharacterSheet"/>.</summary>
    public string? CharacterSheet { get; init; }

    /// <summary>
    /// This Commander's <see cref="PersonaSettings.ShipCoreShip"/> — a ship id, which only means
    /// something for the Commander whose fleet it counts.
    /// </summary>
    public int? ShipCoreShip { get; init; }
}

/// <summary>The Commander's log (Phase 33).</summary>
public sealed record LogbookSettings
{
    /// <inheritdoc cref="D47Settings.Extra"/>
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? Extra { get; init; }

    /// <summary>Whose voice writes it.</summary>
    public string Voice { get; init; } = "first-person";

    /// <summary>What span a log covers when nobody named one — a session, today, a week, a month.</summary>
    public string Range { get; init; } = "session";

    /// <summary>How long it runs to.</summary>
    public string Length { get; init; } = "standard";
}

/// <summary>The memory store's two settings (Phase 31, "It forgets, and can be read and emptied").</summary>
public sealed record MemorySettings
{
    /// <inheritdoc cref="D47Settings.Extra"/>
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? Extra { get; init; }

    /// <summary>Whether d47 remembers anything at all.</summary>
    public bool Enabled { get; init; } = true;

    /// <summary>
    /// How many days an entry lives before it is forgotten. 0 is "never", which is a real choice rather
    /// than an absence.
    /// </summary>
    public int ExpiryDays { get; init; } = 90;
}

/// <summary>The debrief pass (#162).</summary>
public sealed record DebriefSettings
{
    /// <inheritdoc cref="D47Settings.Extra"/>
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? Extra { get; init; }

    /// <summary>Whether the pass runs at the end of a session at all.</summary>
    public bool Enabled { get; init; } = true;
}

/// <summary>Looking things up outside this machine (Phase 14).</summary>
public sealed record KnowledgeSettings
{
    /// <inheritdoc cref="D47Settings.Extra"/>
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? Extra { get; init; }

    /// <summary>Whether d47 may reach the galaxy search.</summary>
    public bool GalaxySearch { get; init; }

    /// <summary>
    /// Whether a generated adventure may fetch the catalogue of notable places from edastro.com (Phase
    /// 47).
    /// </summary>
    public bool NotablePlaces { get; init; }
}

/// <summary>Which companion character is aboard (Phase 11).</summary>
public sealed record PersonaSettings
{
    /// <inheritdoc cref="D47Settings.Extra"/>
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? Extra { get; init; }

    /// <summary>A core id from <see cref="D47.Core.Persona.PersonaCatalog"/>.</summary>
    public string Id { get; init; } = D47.Core.Persona.PersonaCatalog.DefaultId;

    /// <summary>What the Commander calls the ship's AI.</summary>
    public string? ShipName { get; init; }

    /// <summary>Whether a name the Commander gave the ship's AI survives a change of core.</summary>
    public bool KeepShipName { get; init; } = true;

    /// <summary>
    /// Whether the cores are allowed an occasional light touch of wit (#243 — "it's so serious all the
    /// time").
    /// </summary>
    public bool Humor { get; init; }

    /// <summary>The voice paired to each core, keyed by persona id (Phase 11, #33).</summary>
    public IReadOnlyDictionary<string, string> Voices { get; init; } =
        new Dictionary<string, string>();

    /// <summary>Which ship the core-binding rows are pointed at, by its <c>ShipID</c>.</summary>
    public int ShipCoreShip { get; init; }

    /// <summary>Whether the background voice pairing has run.</summary>
    public bool VoicesPaired { get; init; }

    /// <summary>Whether the pairings have been checked against the gender each core is written with.</summary>
    public bool VoicesGenderChecked { get; init; }

    /// <summary>
    /// Superseded by <see cref="VoicesRepaired"/>, and kept because unknown keys are rejected on load:
    /// every file written between v0.6.2 and v0.6.4 carries this, and removing the property would
    /// refuse those files rather than ignore the value.
    /// </summary>
    public bool VoicesNamedChecked { get; init; }

    /// <summary>Which revision of the named-default repair this file has had.</summary>
    public int VoicesRepaired { get; init; }
}

/// <summary>Acting on the game (Phase 10).</summary>
public sealed record ActionSettings
{
    /// <inheritdoc cref="D47Settings.Extra"/>
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? Extra { get; init; }

    /// <summary>Whether spoken commands may send key bindings to Elite at all.</summary>
    public bool Keyboard { get; init; }

    /// <summary>Whether the discovery scanner fires by itself on arriving in a system.</summary>
    public bool HonkOnArrival { get; init; }

    /// <summary>Whether d47 may drive the galaxy map to plot a course.</summary>
    public bool AutoPlot { get; init; }

    /// <summary>Whether d47 may type into Elite's chat.</summary>
    public bool Chat { get; init; }

    /// <summary>Whether a mapped HOTAS switch may operate the ship (Phase 21).</summary>
    public bool Switches { get; init; }

    /// <summary>Whether "take us out" may walk the left panel to the launch button (Phase 52).</summary>
    public bool TakeUsOut { get; init; } = true;

    /// <summary>
    /// Whether "separate and engage" may go to full throttle and boost out of a mass lock (Phase 52).
    /// </summary>
    public bool SeparateAndEngage { get; init; } = true;

    /// <summary>The same, ending in supercruise.</summary>
    public bool SeparateAndSupercruise { get; init; } = true;

    /// <summary>Whether "request docking" may walk the left panel's contacts tab (#150).</summary>
    public bool RequestDocking { get; init; } = true;
}

/// <summary>The headset (Phase 9).</summary>
public sealed record VrSettings
{
    /// <inheritdoc cref="D47Settings.Extra"/>
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? Extra { get; init; }

    /// <summary>
    /// On by default, which costs nothing on a machine with no headset: the runtime is looked for, not
    /// found, and the state machine reports Unavailable.
    /// </summary>
    public bool Enabled { get; init; } = true;

    /// <summary>Which content set the panel is showing: "full" or "mini".</summary>
    public string Mode { get; init; } = "mini";

    /// <summary>
    /// Whether d47 touches the motion controllers at all — the action manifest, the trigger, the grip,
    /// and the ninety-times-a-second pose read behind the aim ray (#198).
    /// </summary>
    public bool Controllers { get; init; }

    /// <summary>Where the full panel sits and what it looks like.</summary>
    public VrSurfaceSettings Panel { get; init; } = new();

    /// <summary>And the mini one, separately.</summary>
    public VrSurfaceSettings Mini { get; init; } = VrSurfaceSettings.Mini();

    /// <summary>How solid the panel is, whichever of the two is on screen.</summary>
    public double Opacity { get; init; } = 0.95;

    /// <summary>
    /// Which revision of the shared-opacity repair this file has had — the same counter idiom as <see
    /// cref="PitchRepaired"/>, and for the same reason: a repair that ships wrong can only reach the
    /// files it already stamped if the stamp can be raised.
    /// </summary>
    public int OpacityShared { get; init; }

    /// <summary>The caption layer.</summary>
    public Vr.CaptionSettings Captions { get; init; } = new();

    /// <summary>Which revision of the panel-pitch repair this file has had.</summary>
    public int PitchRepaired { get; init; }
}

/// <summary>The microphone and the key that opens it (Phase 6).</summary>
public sealed record ListeningSettings
{
    /// <inheritdoc cref="D47Settings.Extra"/>
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? Extra { get; init; }

    /// <summary>The input device id, or null for the system default.</summary>
    public string? InputDevice { get; init; }

    /// <summary>Hold to talk.</summary>
    public string? PushToTalkKey { get; init; } = "RightShift";

    /// <summary>A stick button to talk with, as <c>NonRoamableId#index</c> (Phase 53).</summary>
    public string? PushToTalkButton { get; init; }

    /// <summary>"hold", "toggle", "continuous" or "wake" — the gate policy (Phase 6 and 13).</summary>
    public string Mode { get; init; } = "hold";

    /// <summary>How much audio from before the key was noticed is kept, in milliseconds.</summary>
    public int PreRollMilliseconds { get; init; } = 500;

    /// <summary>Which Whisper model transcribes.</summary>
    public string Model { get; init; } = Listening.WhisperModels.DefaultId;

    /// <summary>Run inference on the GPU.</summary>
    public bool UseGpu { get; init; }

    /// <summary>Subtract what d47 is playing from what the microphone hears (Phase 13).</summary>
    public bool EchoCancellation { get; init; } = true;

    /// <summary>Take the room out of the captured audio, which the same module does for free.</summary>
    public bool NoiseSuppression { get; init; } = true;

    /// <summary>
    /// How far above the room a sound has to be before continuous listening calls it speech, in
    /// decibels.
    /// </summary>
    public int Sensitivity { get; init; } = 9;

    /// <summary>
    /// How long the quiet after a sentence has to run before the utterance is finished, in
    /// milliseconds.
    /// </summary>
    public int SilenceMilliseconds { get; init; } = 700;

    /// <summary>What d47 answers to in wake-word mode, comma-separated.</summary>
    public string? WakeWords { get; init; }

    /// <summary>
    /// How long d47 goes on listening after answering to its name with nothing after it, in seconds.
    /// </summary>
    public int WakeWindowSeconds { get; init; } = 12;
}

/// <summary>What d47 says without being asked (Phase 8).</summary>
public sealed record CalloutSettings
{
    /// <inheritdoc cref="D47Settings.Extra"/>
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? Extra { get; init; }

    /// <summary>Off means d47 never speaks unprompted.</summary>
    public bool Enabled { get; init; } = true;

    /// <summary>Interdiction, shields, hull, heat, a full hold.</summary>
    public bool Danger { get; init; } = true;

    /// <summary>Low fuel, and the unscoopable-next-star case that strands a Commander.</summary>
    public bool Fuel { get; init; } = true;

    public bool Route { get; init; } = true;

    public bool LongJump { get; init; } = true;

    /// <summary>Home, carrier, stored ships, engineering.</summary>
    public bool Arrival { get; init; } = true;

    public bool Materials { get; init; } = true;

    /// <summary>
    /// Systems that might be running High Grade Emissions, and what would be in them (Phase 40).
    /// </summary>
    public bool Emissions { get; init; } = true;

    /// <summary>A reminder to buy limpets on docking somewhere that sells them (Phase 41).</summary>
    public bool Limpets { get; init; }

    /// <summary>The smallest cargo capacity worth reminding about, in tonnes.</summary>
    public int LimpetCargoFloor { get; init; } = 64;

    /// <summary>
    /// The limpet threshold, as a percentage of cargo capacity (the Commander's ruling, 2026-08-21).
    /// </summary>
    public int LimpetPercent { get; init; } = 5;

    /// <summary>An attack an NPC has announced but not yet made (Phase 15).</summary>
    public bool AnnouncedAttack { get; init; } = true;

    /// <summary>Flying in a rival Power's space (Phase 15).</summary>
    public bool RivalTerritory { get; init; } = true;

    /// <summary>
    /// A checklist item the journal has just changed its mind about, and the last unit a plan needed
    /// (Phase 17).
    /// </summary>
    public bool Checklist { get; init; } = true;

    /// <summary>What a prospector limpet found, spoken in the ring (Phase 18).</summary>
    public bool Prospector { get; init; } = true;

    /// <summary>A core asteroid (Phase 18).</summary>
    public bool CoreAsteroid { get; init; } = true;

    /// <summary>Organic sampling progress on the surface (Phase 18).</summary>
    public bool Sampling { get; init; } = true;

    /// <summary>How often route progress is reported, in jumps. 0 silences the progress line.</summary>
    public int RouteEveryNJumps { get; init; } = 3;

    /// <summary>
    /// How long a hyperspace jump has to run before it is worth remarking on, measured from entering
    /// hyperspace rather than from the jump being initiated.
    /// </summary>
    public double LongJumpSeconds { get; init; } = 30;

    /// <summary>The Commander's home system.</summary>
    public string? HomeSystem { get; init; }

    /// <summary>
    /// In-character remarks about where the Commander is, said because nothing has happened rather than
    /// because something has (Phase 11, "Ambient Voice") — drawn as "In Ship chatter" since 2026-09-02,
    /// when the pair took the name of who is speaking rather than of the occasion.
    /// </summary>
    public bool Ambient { get; init; } = true;

    /// <summary>
    /// The shortest gap between two ambient remarks, in seconds. 0 silences them, which is the same as
    /// turning <see cref="Ambient"/> off and is offered because a Commander reaching for "less" will
    /// reach for this row rather than the switch.
    /// </summary>
    public int AmbientSeconds { get; init; } = 300;

    /// <summary>
    /// And the longest, asked for 2026-09-01 (#258): the gap between remarks varies inside the range
    /// rather than ticking like a clock, the same spread <see cref="NpcChatterMaxSeconds"/> already
    /// has.
    /// </summary>
    public int AmbientMaxSeconds { get; init; } = 600;

    /// <summary>The running session total after every sale of the Community Goal commodity (#296).</summary>
    public bool CommunityGoalSales { get; init; } = true;

    /// <summary>The day the Elite week turns, UTC (#332).</summary>
    public DayOfWeek WeekBoundaryDay { get; init; } = DayOfWeek.Thursday;

    /// <summary>The hour, UTC, that day turns at.</summary>
    public int WeekBoundaryHourUtc { get; init; } = 7;

    /// <summary>
    /// Invented background chatter (#244), drawn as "NPC chatter": made-up exchanges between people who
    /// do not exist — passers-by, the dock, the occasional one-way hail.
    /// </summary>
    public bool NpcChatter { get; init; } = true;

    /// <summary>The shortest gap between two exchanges, in seconds. 0 silences them.</summary>
    public int NpcChatterSeconds { get; init; } = 300;

    /// <summary>
    /// And the longest, asked for 2026-08-31: the gap between exchanges varies inside the range rather
    /// than ticking like a clock, because a fixed cadence is the one thing overheard traffic must not
    /// have.
    /// </summary>
    public int NpcChatterMaxSeconds { get; init; } = 600;

    /// <summary>What this row held when it was in minutes.</summary>
    public int? AmbientMinutes { get; init; }

    /// <summary>What to say on arriving in a system d47 knows something about (Phase 23).</summary>
    public Callouts.LoreRemarks Lore { get; init; } = Callouts.LoreRemarks.Lookup;

    /// <summary>How long a system stays quiet after being remarked on (#101).</summary>
    public int LoreCooldownDays { get; init; } = 7;

    /// <summary>
    /// One line at the start of a session, picking up where the Commander left off (Phase 31, "Picking
    /// up where you left off").
    /// </summary>
    public bool Continuity { get; init; } = true;

    /// <summary>
    /// Retired with Habits itself in v0.83.0, and kept because unknown keys are rejected on load.
    /// </summary>
    public bool Habits { get; init; }

    /// <summary>A beat of the Commander's adventure, said when it is reached (Phase 47).</summary>
    public bool Adventure { get; init; } = true;
}

public sealed record LlmSettings
{
    /// <inheritdoc cref="D47Settings.Extra"/>
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? Extra { get; init; }

    /// <summary>
    /// Which provider to use. "none" is a real, supported choice — every input path stays answerable
    /// through the model-free keyword router (Phase 3).
    /// </summary>
    public string Provider { get; init; } = "anthropic";

    /// <summary>Null uses the provider's own default rather than pinning a model here.</summary>
    public string? Model { get; init; }

    /// <summary>Null uses the provider's published endpoint.</summary>
    public string? Endpoint { get; init; }

    /// <summary>False is "plain answers, no persona".</summary>
    public bool PersonalityEnabled { get; init; } = true;

    /// <summary>The Commander's story, in their own words, kept between sessions.</summary>
    public string? AboutMe { get; init; }

    /// <summary>
    /// The Commander's character sheet — name, origin, age, accent: the few lines that are true of them
    /// in any sentence (Phase 43).
    /// </summary>
    public string? CharacterSheet { get; init; }

    /// <summary>
    /// Whether the model may search the web when it decides a question needs current information.
    /// </summary>
    public bool WebSearch { get; init; }

    /// <summary>
    /// Which model answers the calls the Commander is not waiting on — ambient remarks, the opening
    /// brief, the gap reaction, the two lore lookups, and casting a voice (Phase 54).
    /// </summary>
    public string? BackgroundModel { get; init; }

    /// <summary>
    /// The least effort any conversation turn may run at, or null for the router's own answer (Phase
    /// 54).
    /// </summary>
    public Conversation.ThinkingEffort? EffortFloor { get; init; }

    /// <summary>
    /// The most effort any conversation turn may run at, or null for the router's own answer (Phase
    /// 54).
    /// </summary>
    public Conversation.ThinkingEffort? EffortCeiling { get; init; }
}

/// <summary>Everything audible.</summary>
public sealed record SpeechSettings
{
    /// <inheritdoc cref="D47Settings.Extra"/>
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? Extra { get; init; }

    /// <summary>
    /// Which voice provider, or "none". "none" is a real, supported choice: d47 stays fully usable in
    /// text with cues still audible, which is what keeps local-only operation reachable rather than
    /// theoretical (Phase 4).
    /// </summary>
    public string Provider { get; init; } = "edge";

    /// <summary>
    /// Which provider speaks for each of the other five slots, keyed by <see
    /// cref="Audio.VoiceGroupInfo.Id"/> (Phase 57).
    /// </summary>
    public IReadOnlyDictionary<string, string>? GroupProviders { get; init; }

    /// <summary>Null means the provider's own default voice rather than pinning one here.</summary>
    public string? Voice { get; init; }

    /// <summary>
    /// Which provider the stored voices were chosen from — this one's, the two named roles', and every
    /// persona pairing.
    /// </summary>
    public string? VoicesProvider { get; init; }

    /// <summary>
    /// The same fact for the carrier's two voices, which since Phase 57 can be on a different provider
    /// from the ship's.
    /// </summary>
    public string? CarrierVoicesProvider { get; init; }

    /// <summary>1.0 is the voice's natural pace.</summary>
    public double Rate { get; init; } = 1.0;

    /// <summary>Which of Kokoro's eight published ONNX builds the local voice runs on (#139).</summary>
    public string? LocalVoiceBuild { get; init; }

    /// <summary>
    /// Speaking rate per provider, keyed by provider id, overriding <see cref="Rate"/> where present
    /// (Phase 11: "Differences between providers, such as speed, is maintained on a per-provider
    /// basis").
    /// </summary>
    public IReadOnlyDictionary<string, double> ProviderRates { get; init; } =
        new Dictionary<string, double>();

    /// <summary>Which ElevenLabs model speaks, or null for the default (#291).</summary>
    public string? ElevenLabsModel { get; init; }

    /// <summary>
    /// The voices chosen while each other provider was selected, keyed by provider id (Phase 19,
    /// "Remember which voice you chose for each provider").
    /// </summary>
    public IReadOnlyDictionary<string, VoiceChoices> ProviderVoices { get; init; } =
        new Dictionary<string, VoiceChoices>(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// What a thousand characters costs, in US dollars, per provider (Phase 19, "What the voices cost,
    /// beside what the model costs").
    /// </summary>
    public IReadOnlyDictionary<string, double> CharacterPrices { get; init; } =
        new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// The same thing per minute of audio, for a provider whose bill is a function of that rather than
    /// of the characters handed over (#63).
    /// </summary>
    public IReadOnlyDictionary<string, double> MinutePrices { get; init; } =
        new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// The voice a fleet carrier answers in, or null for the ship AI's (Phase 11, "Carrier Captain").
    /// </summary>
    public string? CarrierCaptainVoice { get; init; }

    /// <summary>And its tower, separately, because they are two people.</summary>
    public string? TowerVoice { get; init; }

    /// <summary>
    /// Whether in-game messages are spoken aloud, re-voiced (Phase 11, "Speak incoming messages in
    /// another voice").
    /// </summary>
    public bool SpeakIncomingMessages { get; init; }

    /// <summary>Whether NPC chatter is included when messages are spoken.</summary>
    public bool SpeakNpcMessages { get; init; }

    /// <summary>Whether system chat (<c>starsystem</c>) is spoken (#299).</summary>
    public bool SpeakSystemChat { get; init; } = true;

    /// <summary>Whether local chat (<c>local</c>) is spoken (#299).</summary>
    public bool SpeakLocalChat { get; init; } = true;

    /// <summary>Whether wing chat (<c>wing</c>) is spoken (#299).</summary>
    public bool SpeakWingChat { get; init; } = true;

    /// <summary>
    /// Whether squadron chat is spoken (#299), covering both <c>squadron</c> and <c>squadleaders</c> —
    /// a Commander does not know Elite writes those as two channels, so there is one switch rather than
    /// two.
    /// </summary>
    public bool SpeakSquadronChat { get; init; } = true;

    /// <summary>Whether a direct message (<c>player</c>) is spoken (#299).</summary>
    public bool SpeakDirectMessages { get; init; } = true;

    /// <summary>The output device id, or null for the system default.</summary>
    public string? OutputDevice { get; init; }

    /// <summary>The loop-state cues (Phase 5, #20).</summary>
    public bool CuesEnabled { get; init; } = true;

    /// <summary>The bed under a working turn (#18).</summary>
    public bool ThinkingBedEnabled { get; init; } = true;

    /// <summary>Which bed.</summary>
    public string? ThinkingBed { get; init; }

    /// <summary>Cancel (Phase 5 as "Shut up"; widened by #221).</summary>
    public string? ShutUpHotkey { get; init; } = "Ctrl+Alt+X";

    /// <summary>
    /// The same act on a stick button (#221), beside the key rather than instead of it — the
    /// arrangement push-to-talk already has, for the reason it has it: a Commander who bound both said
    /// two things rather than replaced one.
    /// </summary>
    public string? CancelButton { get; init; }

    /// <summary>How many times a failing turn is tried in total. 1 disables retrying.</summary>
    public int RetryAttempts { get; init; } = 3;

    public double RetryWaitSeconds { get; init; } = 2;

    /// <summary>"sequential" or "logarithmic" (Phase 5).</summary>
    public string RetryBackoff { get; init; } = "sequential";

    /// <summary>How long one attempt may run before it counts as a failure worth reporting.</summary>
    public double TurnTimeoutSeconds { get; init; } = 45;
}

public sealed record LoggingSettings
{
    /// <inheritdoc cref="D47Settings.Extra"/>
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? Extra { get; init; }

    /// <summary>Applies to any subsystem with no explicit entry below.</summary>
    public LogLevel Default { get; init; } = LogLevel.Information;

    /// <summary>Per-subsystem overrides, keyed by <see cref="Diagnostics.Subsystems"/> name.</summary>
    public IReadOnlyDictionary<string, LogLevel> Subsystems { get; init; } =
        new Dictionary<string, LogLevel>();
}

public sealed record UiSettings
{
    /// <inheritdoc cref="D47Settings.Extra"/>
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? Extra { get; init; }

    /// <summary>A theme id from the shipped set.</summary>
    public string Theme { get; init; } = "elite";

    /// <summary>
    /// Whether the settings page shows every row, or only the ones most Commanders change (#60).
    /// </summary>
    public bool ShowEverySetting { get; init; }

    /// <summary>How large the panel is drawn, as a percentage (Phase 9, "Zoom the desktop window").</summary>
    public int ZoomPercent { get; init; } = Interface.ZoomLadder.Default;

    /// <summary>Which content set the desktop window is showing: "full" or "mini" (Phase 51).</summary>
    public string Mode { get; init; } = "full";

    /// <summary>The mini panel on a monitor, for a Commander with no headset (Phase 48).</summary>
    public OverlaySettings Overlay { get; init; } = new();

    /// <summary>
    /// Whether d47 fetches the large hull art — the 4K picture Ship Details shows and the turntable a
    /// card plays (#289).
    /// </summary>
    public bool HullArt { get; init; } = true;
}

/// <summary>
/// The flat mini panel: a chromeless, click-through, topmost strip pinned over the game (Phase 48).
/// </summary>
public sealed record OverlaySettings
{
    /// <inheritdoc cref="D47Settings.Extra"/>
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? Extra { get; init; }

    /// <summary>Off by default.</summary>
    public bool Enabled { get; init; }

    /// <summary>How large the strip is drawn, on <see cref="Interface.ZoomLadder"/>'s rungs.</summary>
    public int ScalePercent { get; init; } = Interface.ZoomLadder.Default;

    /// <summary>How solid it is.</summary>
    public double Opacity { get; init; } = 0.9;
}

/// <summary>Bound gestures, stored as the display form the binding UI produces ("Ctrl+Shift+S").</summary>
public sealed record HotkeySettings
{
    /// <inheritdoc cref="D47Settings.Extra"/>
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? Extra { get; init; }

    /// <summary>Ctrl+comma, which is what VS Code, Chrome, Slack and Discord all use for settings.</summary>
    public string? OpenSettings { get; init; } = "Ctrl+OemComma";

    public string? FocusAsk { get; init; } = "Ctrl+L";

    /// <summary>
    /// Snaps every world-locked headset surface back in front of the Commander (Phase 9, "Re-anchor the
    /// panels").
    /// </summary>
    public string? Reanchor { get; init; } = "Ctrl+Alt+R";

    /// <summary>
    /// Binds the core aboard to the ship the Commander is in, and unbinds it when it is already that
    /// core (Phase 35, "The binding is the Commander's, and unreachable from the model").
    /// </summary>
    public string? BindShipCore { get; init; } = "Ctrl+Alt+B";

    /// <summary>Shows and hides the flat mini panel (Phase 48).</summary>
    public string? ShowOverlay { get; init; } = "Ctrl+Alt+O";

    /// <summary>
    /// Puts the flat mini panel into place mode, where it briefly takes clicks so it can be dragged,
    /// and gives them back the moment it is done (Phase 48).
    /// </summary>
    public string? MoveOverlay { get; init; } = "Ctrl+Alt+M";

    /// <summary>Puts the desktop window into mini and back (Phase 51).</summary>
    public string? WindowMode { get; init; } = "Ctrl+M";
}

public sealed record UpdateSettings
{
    /// <inheritdoc cref="D47Settings.Extra"/>
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? Extra { get; init; }

    /// <summary>The startup check contacts GitHub, so it is egress and is disclosed as such.</summary>
    public bool CheckOnStartup { get; init; } = true;
}

/// <summary>Where donations go (#175).</summary>
public sealed record DonationSettings
{
    /// <inheritdoc cref="D47Settings.Extra"/>
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? Extra { get; init; }

    /// <summary>
    /// Where a donation is posted: Directive 47's own store, baked into the build (2026-08-31, on the
    /// Commander's instruction).
    /// </summary>
    public const string Address = "https://d47-donations.dseelinger.workers.dev";

    /// <summary>The address a Commander once pasted in, before the build carried its own.</summary>
    public string? Endpoint { get; init; }
}
