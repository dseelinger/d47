namespace D47.Core.Configuration;

/// <summary>One key, or every bound row a family predicate matches. Exactly one of the two is set.</summary>
/// <param name="Under">Drawn indented beneath the entry before it in the same group.</param>
public sealed record SettingsEntry(string? Key, Func<string, bool>? Family = null, bool Under = false);

/// <summary>A titled run of entries under one place, with one sentence saying what its rows are for.</summary>
/// <param name="ToggleColumns">
/// How many across the group's toggles are drawn, as one grid of tiles; 0 draws them as rows.
/// </param>
/// <param name="Layout">Whether the group's rows are drawn one per setting or as a mixer table.</param>
public sealed record SettingsPlaceGroup(
    string Title,
    string Help,
    IReadOnlyList<SettingsEntry> Entries,
    int ToggleColumns = 2,
    SettingsGroupLayout Layout = SettingsGroupLayout.Rows);

/// <summary>How a group's rows are drawn.</summary>
public enum SettingsGroupLayout
{
    /// <summary>One row per setting.</summary>
    Rows,

    /// <summary>
    /// One table row per <see cref="Capabilities.SettingRow.Group"/>, with a column per setting label, in the
    /// order the rows resolve.
    /// </summary>
    Mixer,
}

/// <summary>One card on the settings page, gathering rows from any number of capabilities by key.</summary>
public sealed record SettingsPlace(
    string Id,
    string Title,
    string Sentence,
    string DocsCapabilityId,
    IReadOnlyList<string> Terms,
    bool StartCollapsed,
    IReadOnlyList<SettingsPlaceGroup> Groups);

/// <summary>A section of the page, holding up to <see cref="SettingsLayout.MostPlacesPerArea"/> places.</summary>
public sealed record SettingsArea(string Id, string Title, string Sentence, IReadOnlyList<SettingsPlace> Places);

/// <summary>A flat run of entries drawn on a panel tab rather than the settings page.</summary>
/// <param name="RootKey">
/// The App's breadcrumb root key for this tab, held as a literal string because Core cannot reference
/// D47.App.
/// </param>
/// <param name="Strip">Whether the tab draws these rows with its own chrome stripped to match the page.</param>
/// <param name="Title">How this tab and root read together, for a search match found elsewhere (#222).</param>
public sealed record SettingsTabPlace(
    string Id, string RootKey, bool Strip, string Title, IReadOnlyList<SettingsEntry> Entries);

/// <summary>
/// The hand-authored map of every settings row into areas and places (#217), the precedent being
/// <see cref="D47.Core.Help.HelpTaxonomy"/>. Rows are placed by key, independent of which capability
/// declares them. <see cref="SettingsService"/> resolves this against the bound registry.
/// </summary>
public static class SettingsLayout
{
    /// <summary>No area holds more places than this.</summary>
    public const int MostPlacesPerArea = 6;

    /// <summary>
    /// No place shows more entries than this on a fresh install, with one documented exception. An entry
    /// counts as shown when any row it resolves to has <c>Advanced == false</c> or <c>Kind == Secret</c>.
    /// </summary>
    public const int MostShownPerPlace = 8;

    /// <summary>No place holds more entries than this in all, with one documented exception.</summary>
    public const int MostEntriesPerPlace = 14;

    /// <summary>
    /// Place ids where <see cref="MostShownPerPlace"/> is exceeded today: <c>updates</c> holds every row
    /// <see cref="Capabilities.Builtin.AboutCapability"/> declares, which the issue's own arrangement table
    /// names only 8 of but the acceptance rule requires all of (11 entries, none Advanced, so 11 shown);
    /// <c>look-up</c> holds the eight outside-source rows and the Share with EDDN row beside them (9 shown).
    /// </summary>
    public static readonly IReadOnlyList<string> ShownLimitExceptions = ["updates", "look-up"];

    /// <summary>
    /// Place ids where <see cref="MostEntriesPerPlace"/> is exceeded today: <c>sounds</c> spells out
    /// Level/Mute/Duck for all five audio channels rather than collapsing them into one family entry, beside
    /// the output device (19 entries, the mixer rows Advanced); <c>callouts</c> holds the callouts switch and
    /// the plans and reminders toggles (18 entries, the week rows Advanced); <c>exploring</c> holds the
    /// exploration and exobiology callout toggles (17 entries); <c>chatter</c> holds the scene chatter row
    /// beside NPC chatter as well as the seven messages rows, the accent and NPC humor rows, the carrier
    /// captain and the fighter pilot (19 entries, the chatter rows Advanced).
    /// </summary>
    public static readonly IReadOnlyList<string> TotalLimitExceptions = ["sounds", "callouts", "exploring", "chatter"];

    private static SettingsEntry E(string key, bool under = false) => new(key, Under: under);

    private static SettingsEntry F(Func<string, bool> family) => new(null, family);

    private static SettingsPlaceGroup G(
        string title, string help, IReadOnlyList<SettingsEntry> entries, int toggleColumns = 2) =>
        new(title, help, entries, toggleColumns);

    /// <summary>The documented family patterns, named so the resolver and the tests can each use them.</summary>
    public static bool IsSpeechProviderKeyFamily(string key) =>
        key.StartsWith("speech.", StringComparison.Ordinal) && key.EndsWith(".apiKey", StringComparison.Ordinal);

    /// <summary>The download row of each voice that runs on this computer, each shown only while a slot uses it.</summary>
    public static bool IsLocalVoiceDownloadFamily(string key) =>
        key is Capabilities.Builtin.SpeechCapability.LocalVoiceKey or Capabilities.Builtin.SpeechCapability.ChatterboxVoiceKey;

    public static bool IsVoiceProviderSlotFamily(string key) =>
        key.StartsWith("speech.provider.", StringComparison.Ordinal);

    public static bool IsHearingProviderKeyFamily(string key) =>
        key.StartsWith("listening.key.", StringComparison.Ordinal);

    public static bool IsLlmProviderKeyFamily(string key) =>
        key.StartsWith("llm.", StringComparison.Ordinal) && key.EndsWith(".apiKey", StringComparison.Ordinal);

    /// <summary>One surface's placement rows, for a slot <see cref="Capabilities.Builtin.VrCapability"/> names.</summary>
    public static Func<string, bool> IsVrPlacementFamily(string slot) =>
        key => key.StartsWith($"vr.{slot}.", StringComparison.Ordinal);

    public static bool IsCareerRatingFamily(string key) =>
        key.StartsWith(Capabilities.Builtin.AdventureCapability.RatingKeyPrefix, StringComparison.Ordinal);

    public static bool IsVrHotkeyFamily(string key) =>
        key is Capabilities.Builtin.VrCapability.ZoomInHotkeyKey
            or Capabilities.Builtin.VrCapability.ZoomOutHotkeyKey
            or Capabilities.Builtin.VrCapability.ResetZoomHotkeyKey
            or Capabilities.Builtin.VrCapability.ResizeHotkeyKey;

    public static bool IsEgressFamily(string key) =>
        key.StartsWith("egress.", StringComparison.Ordinal);

    public static bool IsSubsystemLevelFamily(string key) =>
        key.StartsWith("logging.subsystems.", StringComparison.Ordinal);

    private const string OtherVoicesSwitchHelp =
        "These stop when Speak without being asked is off. That switch is under Your core › Callouts.";

    public static readonly IReadOnlyList<SettingsArea> Areas =
    [
        new SettingsArea(
            "core",
            "Your core",
            "Which core is aboard, how it sounds, and what it says without being asked.",
            [
                new SettingsPlace(
                    "persona",
                    "Core and character",
                    "Which core is aboard, its character, its humor and its name.",
                    "persona",
                    [],
                    false,
                    [
                        G(
                            "Core and character",
                            "Which core is aboard, and how it behaves.",
                            [
                                E("persona.id"),
                                E("llm.personality"),
                                E("persona.introductions"),
                                E("persona.own"),
                                E("persona.avatarClips"),
                            ]),
                        G(
                            "Humor",
                            "How funny the core may be, and on how many of its lines.",
                            [
                                E("persona.coreHumor"),
                                E("persona.coreHumorPercent", under: true),
                            ]),
                        G(
                            "Ship name",
                            "What you call your core, and whether the name follows it across a core switch.",
                            [E("persona.shipName"), E("persona.keepShipName")]),
                        G(
                            "A core per ship",
                            "Which core answers you, bound per ship.",
                            [
                                E("persona.shipCoreShip"),
                                E("persona.shipCore"),
                                E("persona.shipCores"),
                            ]),
                    ]),
                new SettingsPlace(
                    "voice",
                    "Its voice",
                    "How the core sounds, and the effects on its voice.",
                    "speech",
                    [],
                    false,
                    [
                        G(
                            "Voice",
                            "Who voices the core aboard, and how fast it speaks.",
                            [E("speech.voice"), E("speech.rate")]),
                        G(
                            "COVAS Voice",
                            "How the stock COVAS core sounds.",
                            [E("speech.covasVoice.reverb"), E("speech.covasVoice.test")]),
                        G(
                            "Guardian Voice Effects",
                            "Optional treatments for the core's voice, all off by default and global to every core.",
                            [E("speech.guardianVoice.preset")]),
                    ]),
                new SettingsPlace(
                    "callouts",
                    "Callouts",
                    "The one switch every callout depends on, and the plans and reminders the core raises.",
                    "callouts",
                    [],
                    false,
                    [
                        G("All callouts", "One switch for everything D47 says without being asked.", [E("callouts.enabled")]),
                        G(
                            "Plans and reminders",
                            "What the core says about your checklist and reminders, community goals, trades and what it pays attention to.",
                            [
                                E("callouts.checklist"),
                                E("callouts.continuity"),
                                E("callouts.recap"),
                                E("callouts.communityGoalSales"),
                                E("callouts.weekBoundaryDay", under: true),
                                E("callouts.weekBoundaryHourUtc", under: true),
                                E("callouts.communityGoalExpiry"),
                                E("callouts.colonisation"),
                                E("callouts.powerplayCycle"),
                                E("callouts.bgsTick"),
                                E("callouts.friendsOnline"),
                                E("callouts.galNet"),
                                E("callouts.missions"),
                                E("callouts.outstandingCrimes"),
                                E("callouts.reminders"),
                                E("callouts.sessionLength"),
                                E("callouts.sessionLengthHours", under: true),
                                E("callouts.domain"),
                                E("callouts.notableShips"),
                                E("callouts.shipModes"),
                                E("callouts.tradingMode"),
                                E("callouts.tradingModeMinHold", under: true),
                            ]),
                    ]),
                new SettingsPlace(
                    "in-flight",
                    "In flight",
                    "Danger, fuel, route progress and hyperspace tunnel delays.",
                    "callouts",
                    [],
                    false,
                    [
                        G(
                            "Flight callouts",
                            "What D47 warns you about and reports while you fly.",
                            [
                                E("callouts.danger"),
                                E("callouts.fuel"),
                                E("callouts.rebuy"),
                                E("callouts.carrierFuel"),
                                E("callouts.carrierUpkeep"),
                                E("callouts.routeProgress"),
                                E("callouts.routeEveryNJumps", under: true),
                                E("callouts.longJumpRemark"),
                                E("callouts.longJumpSeconds", under: true),
                                E("callouts.announcedAttack"),
                                E("callouts.kills"),
                                E("callouts.rivalTerritory"),
                                E("callouts.powerplayMerits"),
                                E("callouts.powerplaySalvage"),
                            ]),
                    ]),
                new SettingsPlace(
                    "exploring",
                    "Exploring",
                    "Arrivals, landing pads, lore, undiscovered systems, biology and unsold data at risk.",
                    "callouts",
                    [],
                    false,
                    [
                        G(
                            "Exploration callouts",
                            "What D47 points out when you arrive somewhere, scan and sample.",
                            [
                                E("callouts.arrival"),
                                E("callouts.homeSystem", under: true),
                                E("callouts.dockingPad"),
                                E("callouts.stationDamage"),
                                E("callouts.lore"),
                                E("callouts.loreCooldownDays", under: true),
                                E("callouts.discovery"),
                                E("callouts.footfall"),
                                E("callouts.footfallApproach"),
                                E("callouts.mapping"),
                                E("callouts.geology"),
                                E("callouts.regionFirst"),
                                E("callouts.bodyValue"),
                                E("callouts.bodyValueThreshold", under: true),
                                E("callouts.notableBodies"),
                                E("callouts.notableCollidingBinary", under: true),
                                E("callouts.notableShepherdMoon", under: true),
                                E("callouts.notableNestedMoon", under: true),
                                E("callouts.notableEccentricOrbit", under: true),
                                E("callouts.notableFastRotator", under: true),
                                E("callouts.notableWideRings", under: true),
                                E("callouts.notablePremiumFsdInjection", under: true),
                                E("callouts.notableLandableAtmosphere", under: true),
                                E("callouts.biologyValue"),
                                E("callouts.surveyedBiology"),
                                E("callouts.earlierFinds"),
                                E("callouts.biologyThreshold", under: true),
                                E("callouts.sampling"),
                                E("callouts.samplingRange"),
                                E("callouts.samplingAbandoned"),
                                E("callouts.unsoldDataAtRisk"),
                            ]),
                    ]),
                new SettingsPlace(
                    "mining",
                    "Mining and materials",
                    "Milestones, emissions and limpets.",
                    "callouts",
                    [],
                    false,
                    [
                        G(
                            "Mining callouts",
                            "What D47 reports while you prospect and mine.",
                            [
                                E("callouts.materials"),
                                E("callouts.emissions"),
                                E("callouts.limpets"),
                                E("callouts.limpetCargoFloor", under: true),
                                E("callouts.limpetPercent", under: true),
                                E("callouts.prospector"),
                                E("callouts.coreAsteroid"),
                                E("callouts.ringHotspots"),
                                E("callouts.miningSummary"),
                                E("callouts.holdFull"),
                            ]),
                    ]),
            ]),
        new SettingsArea(
            "other-voices",
            "Other voices",
            "Everyone aboard and on comms who is not your core.",
            [
                new SettingsPlace(
                    "narrator-and-stories",
                    "Narrator and stories",
                    "The Narrator telling your story, and when D47 offers a story.",
                    "callouts",
                    [],
                    false,
                    [
                        G(
                            "Narrator",
                            $"Who tells your story, and how often. {OtherVoicesSwitchHelp}",
                            [
                                E("callouts.narrator"),
                                E("callouts.narratorSeconds", under: true),
                                E("callouts.narratorMaxSeconds", under: true),
                                E("speech.narratorVoice"),
                            ]),
                        G(
                            "Stories",
                            $"Whether D47 offers a story, and nudges you about one. {OtherVoicesSwitchHelp}",
                            [
                                E("callouts.adventure"),
                                E("callouts.backstoryNudge", under: true),
                            ]),
                    ]),
                new SettingsPlace(
                    "crew-seats",
                    "Crew seats",
                    "Who speaks from each crew seat.",
                    "speech",
                    [],
                    false,
                    [
                        G(
                            "Seat voices",
                            "Who speaks from each seat, on every ship. A seat's unprompted remarks stop when Speak without being asked is off. That switch is under Your core › Callouts.",
                            [
                                .. Capabilities.Builtin.SpeechCapability.SeatVoiceRoles
                                    .Select(role => E(Capabilities.Builtin.SpeechCapability.SeatVoiceKey(role))),
                            ]),
                    ]),
                new SettingsPlace(
                    "chatter",
                    "Chatter and messages",
                    "Invented radio traffic, and in-game chat read aloud.",
                    "callouts",
                    [],
                    false,
                    [
                        G(
                            "Chatter",
                            $"Invented radio traffic between other pilots and at settlements, and how often it plays. {OtherVoicesSwitchHelp}",
                            [
                                E("callouts.ambient"),
                                E("callouts.ambientSeconds", under: true),
                                E("callouts.ambientMaxSeconds", under: true),
                                E("callouts.npcChatter"),
                                E("callouts.npcChatterSeconds", under: true),
                                E("callouts.npcChatterMaxSeconds", under: true),
                                E("callouts.scenes"),
                            ]),
                        G(
                            "Messages read aloud",
                            "Which comms tabs D47 reads out, once incoming messages are on.",
                            [
                                E("speech.speakIncomingMessages"),
                                E("speech.speakNpcMessages", under: true),
                                E("speech.speakSystemChat", under: true),
                                E("speech.speakLocalChat", under: true),
                                E("speech.speakWingChat", under: true),
                                E("speech.speakSquadronChat", under: true),
                                E("speech.speakDirectMessages", under: true),
                            ]),
                        G(
                            "How NPCs sound",
                            "How often the words written for an accented voice suit its accent, and how funny the other pilots may be.",
                            [
                                E("speech.accentPercent"),
                                E("persona.npcHumor"),
                                E("persona.npcHumorPercent", under: true),
                            ]),
                        G(
                            "Carrier captain",
                            $"Your carrier's captain mentioning the weekly payroll. {OtherVoicesSwitchHelp}",
                            [E("callouts.carrierPayroll")]),
                        G(
                            "Fighter pilot",
                            $"Your hired pilot speaking when the fighter launches, docks, is lost or is rebuilt. {OtherVoicesSwitchHelp}",
                            [E("callouts.fighter")]),
                    ]),
            ]),
        new SettingsArea(
            "voice",
            "Hearing and sound",
            "How D47 hears you, and the sounds it plays.",
            [
                new SettingsPlace(
                    "voice-input",
                    "Voice Input",
                    "The input device, and how D47 decides you are talking to it.",
                    "listening",
                    ["mic", "ptt"],
                    false,
                    [
                        G(
                            "Microphone",
                            "The input device, and how D47 decides you are talking to it.",
                            [
                                E("listening.inputDevice"),
                                E("listening.pushToTalkKey"),
                                E("listening.cancelHotkey"),
                                E("listening.mode"),
                                E("listening.sensitivity"),
                                E("listening.silence"),
                                E("listening.preRoll"),
                                E("listening.echoCancellation"),
                                E("listening.noiseSuppression"),
                            ]),
                        G(
                            "Wake word",
                            "What D47 answers to, hands free, and how long it keeps listening after.",
                            [E("listening.wakeWords"), E("listening.wakeWindow")]),
                        G(
                            "Corrections",
                            "Names D47 has learned to hear correctly.",
                            [E("listening.corrections")]),
                    ]),
                new SettingsPlace(
                    "sounds",
                    "Sounds and levels",
                    "The output device, the cues, the thinking bed, and the mixer for every channel.",
                    "audio",
                    [],
                    false,
                    [
                        G(
                            "Output",
                            "Where D47 speaks from.",
                            [E("speech.outputDevice")]),
                        G(
                            "Cues",
                            "Sounds D47 makes while it listens and thinks.",
                            [
                                E("speech.cues"),
                                E("speech.thinkingBed"),
                            ]),
                        G("Levels", "Level, mute and duck, for every channel.", LevelEntries(), toggleColumns: 0) with
                        {
                            Layout = SettingsGroupLayout.Mixer,
                        },
                        G("Your own audio", "Sound files you've dropped in for D47 to use.", [E("audio.drops")]),
                    ]),
            ]),
        new SettingsArea(
            "providers",
            "Providers",
            "Which services answer, hear and speak, what they cost, and how hard D47 retries them.",
            [
                new SettingsPlace(
                    "language-model",
                    "Language model",
                    "Which provider and model answer, and how hard they think.",
                    "conversation",
                    [],
                    false,
                    [
                        G(
                            "Provider and model",
                            "Who answers, with which model, and how hard it thinks.",
                            [
                                E("llm.provider"),
                                F(IsLlmProviderKeyFamily),
                                E("llm.endpoint"),
                                E("llm.model"),
                                E("llm.test"),
                                E("llm.backgroundModel"),
                                E("llm.effortFloor"),
                                E("llm.effortCeiling"),
                                E("models.refreshCatalog"),
                            ]),
                    ]),
                new SettingsPlace(
                    "speech-recognition",
                    "Speech recognition",
                    "Which model turns speech into words, and where it runs.",
                    "listening",
                    ["stt", "whisper"],
                    false,
                    [
                        G(
                            "Speech recognition",
                            "Who turns speech into words, and where it runs.",
                            [
                                E("listening.provider"),
                                F(IsHearingProviderKeyFamily),
                                E("listening.model"),
                                E("listening.useGpu"),
                            ]),
                    ]),
                new SettingsPlace(
                    "speech-synthesis",
                    "Speech synthesis",
                    "Where spoken replies are synthesised, your own voices, and what it costs.",
                    "speech",
                    ["tts", "text to speech", "record", "my voice", "own voice", "your voice", "import", "clone"],
                    false,
                    [
                        G(
                            "Provider",
                            "Where D47 synthesises speech, and what the provider needs.",
                            [
                                E("speech.provider"),
                                F(IsSpeechProviderKeyFamily),
                                E("speech.elevenlabs.model"),
                                F(IsLocalVoiceDownloadFamily),
                                E("speech.localVoiceBuild"),
                                E("speech.resetVoices"),
                            ]),
                        G(
                            "Where each voice comes from",
                            "Every voice that is not the ship's own can come from a different provider.",
                            [F(IsVoiceProviderSlotFamily)]),
                        G(
                            "Custom voices",
                            "Your voice and other voices you record or import, kept on this PC, for Chatterbox to speak in.",
                            [E("speech.customVoices")]),
                        G(
                            "What it costs",
                            "What this session has spent, and the rates it was priced at.",
                            [
                                E("speech.characterPrice"),
                                E("speech.minutePrice"),
                                E("speech.spent"),
                                E("speech.spentBySlot"),
                                E("speech.egress"),
                            ]),
                    ]),
                new SettingsPlace(
                    "look-up",
                    "Outside sources",
                    "What D47 may reach out to the web or the galaxy index for.",
                    "conversation",
                    [],
                    false,
                    [
                        G("Outside sources", "Where D47 may look for facts it does not already hold.", [E("llm.webSearch"), E("llm.lookAtScreen"), E("knowledge.galaxy"), E("knowledge.colonisationPlanner"), E("knowledge.bgsTick"), E("knowledge.galNet"), E("knowledge.signalIndex"), E("knowledge.communityBiology"), E("journal.codexPictures"), E("knowledge.inaraKey")]),
                        G("Sharing", "What D47 sends to community sites.", [E("contribution.eddn")]),
                    ]),
                new SettingsPlace(
                    "turn-fails",
                    "When a turn fails",
                    "How many times a failing turn is retried, and how long it waits.",
                    "speech",
                    [],
                    false,
                    [
                        G(
                            "Retries",
                            "How often a failed turn is tried again, and how long D47 waits for one.",
                            [
                                E("speech.retryAttempts"),
                                E("speech.retryWait"),
                                E("speech.retryBackoff"),
                                E("speech.turnTimeout"),
                            ]),
                    ]),
            ]),
        new SettingsArea(
            "you",
            "You and your records",
            "What D47 knows about you, and what it keeps between sessions.",
            [
                new SettingsPlace(
                    "about-you",
                    "About you",
                    "Your character sheet, what you have told D47 about yourself, and the scenario.",
                    "persona",
                    [],
                    false,
                    [
                        G(
                            "About you",
                            "What the core knows about you.",
                            [
                                E("llm.characterSheet"),
                                E("llm.aboutMe"),
                                E("llm.scenario"),
                                E("llm.scenarioAudience", under: true),
                            ]),
                    ]),
                new SettingsPlace(
                    "memory",
                    "Memory, notes and debrief",
                    "What D47 remembers between sessions, and what it drafts from a session's corrections.",
                    "memory",
                    [],
                    false,
                    [
                        G(
                            "Memory and debrief",
                            "What D47 keeps between sessions, and what it drafts after one.",
                            [
                                E("memory.enabled"),
                                E("memory.expiryDays"),
                                E("memory.store"),
                                E("lore.book"),
                                E("debrief.enabled"),
                                E("debrief.directions"),
                            ]),
                    ]),
                new SettingsPlace(
                    "logs-and-goals",
                    "Logs and goals",
                    "The Commander's log, and the long campaigns tracked alongside it.",
                    "logbook",
                    [],
                    false,
                    [
                        G(
                            "Log and goals",
                            "How the Commander's log is written, and where it and your goals are kept.",
                            [
                                E("logbook.voice"),
                                E("logbook.range"),
                                E("logbook.length"),
                                E("logbook.store"),
                                E("goals.store"),
                            ]),
                        G(
                            "Screenshots",
                            "Whether D47 saves a JPEG copy of each Elite screenshot, and where.",
                            [
                                E("screenshots.convert"),
                                E("screenshots.folder"),
                            ]),
                    ]),
            ]),
        new SettingsArea(
            "acting",
            "Acting on the game",
            "What D47 may do in Elite on your behalf. Everything here starts off.",
            [
                new SettingsPlace(
                    "may-do",
                    "What D47 may do",
                    "Keys, messages, courses, switches and the ship commands.",
                    "flight-controls",
                    [],
                    false,
                    [
                        G(
                            "Permissions",
                            "What D47 may do in the game for you. Each one starts off.",
                            [
                                E("actions.keyboard"),
                                E("actions.chat"),
                                E("actions.autoPlot"),
                                E("actions.switches"),
                                E("actions.headsetView"),
                                E("actions.switchChords"),
                                E("actions.honkOnArrival"),
                                E("actions.takeUsOut"),
                                E("actions.separateAndEngage"),
                                E("actions.separateAndSupercruise"),
                                E("actions.requestDocking"),
                            ]),
                    ]),
                new SettingsPlace(
                    "macros-and-switches",
                    "Macros and switches",
                    "The Commander's own named sequences, and the HOTAS switches bound to them.",
                    "macros",
                    [],
                    false,
                    [
                        G("Your macros and switches", "Named key sequences you have made, and the HOTAS switches bound to them.", [E("switches.list"), E("macros.list")]),
                        G("Binding profiles", "Named copies of Elite's bindings, saved and loaded by voice.", [E("binding-profiles.list")]),
                    ]),
            ]),
        new SettingsArea(
            "screens",
            "Screens",
            "The window, the overlay on your monitor, and the headset.",
            [
                new SettingsPlace(
                    "window",
                    "Window",
                    "The theme, the zoom, and the keys that open the window.",
                    "interface",
                    [],
                    false,
                    [
                        G(
                            "Look and keys",
                            "The window's theme and zoom, and the keys that bring it forward.",
                            [
                                E("ui.theme"),
                                E("ui.zoom"),
                                E("ui.tabs"),
                                E("hotkeys.openSettings"),
                                E("hotkeys.focusAsk"),
                            ]),
                    ]),
                new SettingsPlace(
                    "overlay",
                    "Overlay",
                    "The flat mini panel pinned over the game.",
                    "interface",
                    [],
                    false,
                    [
                        G(
                            "Overlay",
                            "Whether the mini panel shows over the game, how it looks, and where.",
                            [
                                E("ui.overlay.enabled"),
                                E("ui.overlay.scale", under: true),
                                E("ui.overlay.opacity", under: true),
                                E("ui.overlay.display"),
                                E("hotkeys.showOverlay"),
                                E("hotkeys.moveOverlay"),
                            ]),
                    ]),
                new SettingsPlace(
                    "headset",
                    "Headset",
                    "The SteamVR overlay, where it sits, and its captions.",
                    "vr",
                    [],
                    false,
                    [
                        G(
                            "Headset overlay",
                            "Whether the overlay shows in SteamVR, which panel it shows, and its keys.",
                            [
                                E("vr.enabled"),
                                E("vr.mode"),
                                E("vr.opacity", under: true),
                                E("vr.controllers", under: true),
                                E("vr.state"),
                                F(IsVrHotkeyFamily),
                            ]),
                        G(
                            "Panel you are looking at placement",
                            "Where the panel on screen right now sits in the headset.",
                            [F(IsVrPlacementFamily(Capabilities.Builtin.VrCapability.CurrentSlot))]),
                        G(
                            "Panel placement",
                            "Where the big panel sits in the headset.",
                            [F(IsVrPlacementFamily(Capabilities.Builtin.VrCapability.PanelSlot))]),
                        G(
                            "Mini panel placement",
                            "Where the mini panel sits in the headset.",
                            [F(IsVrPlacementFamily(Capabilities.Builtin.VrCapability.MiniSlot))]),
                        G(
                            "Captions",
                            "What D47 says, written under it, in the headset.",
                            [
                                E("vr.captions.enabled"),
                                E("vr.captions.lock"),
                                E("vr.captions.size"),
                                E("vr.captions.background"),
                                E("vr.captions.speed"),
                            ]),
                    ]),
            ]),
        new SettingsArea(
            "install",
            "Privacy and this install",
            "What leaves this machine, updates, and the tools for when something is wrong. Last.",
            [
                new SettingsPlace(
                    "privacy",
                    "Privacy and egress",
                    "What D47 forgets on request, and exactly what leaves this machine.",
                    "privacy",
                    ["telemetry", "data"],
                    false,
                    [
                        G(
                            "What D47 keeps",
                            "Memories, recordings and your donation identifier, each deleted with one press.",
                            [
                                E("privacy.memory"),
                                E("privacy.audioFlight"),
                                E("privacy.donor"),
                            ]),
                        G(
                            "What leaves this machine",
                            "Read-only, and computed from the settings as they stand right now.",
                            [F(IsEgressFamily)]),
                    ]),
                new SettingsPlace(
                    "updates",
                    "Updates and install",
                    "This build, what it changed, and every row About declares.",
                    "about",
                    [],
                    false,
                    [
                        G(
                            "This install",
                            "Update checks, this build, and the shortcuts to its files.",
                            [
                                E("updates.checkOnStartup"),
                                E("about.installUpdate"),
                                E("about.changelog"),
                                E("about.changelogOnline"),
                                E("about.setUpKeys"),
                                E("about.startMenu"),
                                E("about.dataFolder"),
                                E("about.community"),
                                E("about.version"),
                                E("about.build"),
                                E("about.attribution"),
                            ]),
                    ]),
                new SettingsPlace(
                    "diagnostics",
                    "Diagnostics",
                    "What is paused, and hand-testing coverage.",
                    "diagnostics",
                    [],
                    true,
                    [
                        G(
                            "Diagnostics",
                            "What is paused right now, and which features have been tested by hand.",
                            [
                                E("diagnostics.paused"),
                                E("diagnostics.coverage"),
                            ]),
                    ]),
            ]),
    ];

    public static readonly IReadOnlyList<SettingsTabPlace> Tabs =
    [
        new SettingsTabPlace(
            "fleet-ships", "loadout.ships", true, "Asset Mgmt › Ships", [E("ships.remembered"), E("ships.art"), E("ships.hullgpu")]),
        new SettingsTabPlace(
            "adventures", "adventures", true, "Stories", [E("knowledge.notablePlaces"), E("adventures.storyDownloads"), E("adventures.storyRatings"), E("adventures.castReactions"), F(IsCareerRatingFamily)]),
        new SettingsTabPlace(
            "fleet-carrier",
            "loadout.carrier",
            true,
            "Asset Mgmt › Carrier",
            [
                E("speech.carrierCaptainName"),
                E("speech.carrierCaptainVoice"),
                E("speech.towerName"),
                E("speech.towerVoice"),
                E("persona.carrierHumor"),
                E("persona.carrierHumorPercent", under: true),
            ]),
        new SettingsTabPlace(
            "checklist", "checklist", false, "Checklist",
            [E("checklists.summary"), E("checklists.removeFulfilled")]),
        new SettingsTabPlace(
            "log-levels", "transcript.log", true, "Transcript › Log File",
            [E(Capabilities.Builtin.DiagnosticsCapability.DefaultLevelKey), F(IsSubsystemLevelFamily)]),
    ];

    /// <summary>
    /// Level, mute and duck for every <see cref="Audio.AudioChannel"/>, spelled out rather than collapsed
    /// into a family — see <see cref="TotalLimitExceptions"/>.
    /// </summary>
    private static IReadOnlyList<SettingsEntry> LevelEntries()
    {
        var entries = new List<SettingsEntry>();

        foreach (var channel in Enum.GetValues<Audio.AudioChannel>())
        {
            entries.Add(E(Capabilities.Builtin.AudioCapability.LevelKey(channel)));
            entries.Add(E(Capabilities.Builtin.AudioCapability.MuteKey(channel)));

            if (Audio.AudioMix.Ducks(channel))
            {
                entries.Add(E(Capabilities.Builtin.AudioCapability.DuckKey(channel)));
            }
        }

        return entries;
    }
}
