namespace D47.Core.Configuration;

/// <summary>One key, or every bound row a family predicate matches. Exactly one of the two is set.</summary>
/// <param name="Under">Drawn indented beneath the entry before it in the same group.</param>
public sealed record SettingsEntry(string? Key, Func<string, bool>? Family = null, bool Under = false);

/// <summary>A titled run of entries under one place, with one sentence saying what its rows are for.</summary>
public sealed record SettingsPlaceGroup(string Title, string Help, IReadOnlyList<SettingsEntry> Entries);

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
    /// names only 8 of but the acceptance rule requires all of (11 entries, none Advanced, so 11 shown).
    /// </summary>
    public static readonly IReadOnlyList<string> ShownLimitExceptions = ["updates"];

    /// <summary>
    /// Place ids where <see cref="MostEntriesPerPlace"/> is exceeded today: <c>sounds</c> spells out
    /// Level/Mute/Duck for all five audio channels rather than collapsing them into one family entry (17
    /// entries, all Advanced, so 0 shown); <c>voice</c> spells out the eight Guardian voice toggles
    /// rather than collapsing them into one family entry (23 entries with the Test row, all Advanced,
    /// so 0 shown beyond what already applied) (#225, #226); <c>persona</c> spells out a humor level and
    /// frequency for each of three groups (17 entries, the six humor rows Advanced); <c>voice-input</c>
    /// holds the hearing provider beside the microphone, wake word and corrections rows (16 entries, the
    /// hosted providers' keys one family entry of which at most one row applies at a time, and every
    /// speech-recognition row Advanced).
    /// </summary>
    public static readonly IReadOnlyList<string> TotalLimitExceptions = ["sounds", "voice", "persona", "voice-input"];

    private static SettingsEntry E(string key, bool under = false) => new(key, Under: under);

    private static SettingsEntry F(Func<string, bool> family) => new(null, family);

    private static SettingsPlaceGroup G(string title, string help, IReadOnlyList<SettingsEntry> entries) =>
        new(title, help, entries);

    /// <summary>The documented family patterns, named so the resolver and the tests can each use them.</summary>
    public static bool IsSpeechProviderKeyFamily(string key) =>
        key.StartsWith("speech.", StringComparison.Ordinal) && key.EndsWith(".apiKey", StringComparison.Ordinal);

    public static bool IsVoiceProviderSlotFamily(string key) =>
        key.StartsWith("speech.provider.", StringComparison.Ordinal);

    public static bool IsHearingProviderKeyFamily(string key) =>
        key.StartsWith("listening.key.", StringComparison.Ordinal);

    public static bool IsLlmProviderKeyFamily(string key) =>
        key.StartsWith("llm.", StringComparison.Ordinal) && key.EndsWith(".apiKey", StringComparison.Ordinal);

    /// <summary>One surface's placement rows, for a slot <see cref="Capabilities.Builtin.VrCapability"/> names.</summary>
    public static Func<string, bool> IsVrPlacementFamily(string slot) =>
        key => key.StartsWith($"vr.{slot}.", StringComparison.Ordinal);

    public static bool IsVrHotkeyFamily(string key) =>
        key is Capabilities.Builtin.VrCapability.ZoomInHotkeyKey
            or Capabilities.Builtin.VrCapability.ZoomOutHotkeyKey
            or Capabilities.Builtin.VrCapability.ResetZoomHotkeyKey
            or Capabilities.Builtin.VrCapability.ResizeHotkeyKey;

    public static bool IsEgressFamily(string key) =>
        key.StartsWith("egress.", StringComparison.Ordinal);

    public static bool IsSubsystemLevelFamily(string key) =>
        key.StartsWith("logging.subsystems.", StringComparison.Ordinal);

    public static readonly IReadOnlyList<SettingsArea> Areas =
    [
        new SettingsArea(
            "voice",
            "Voice and hearing",
            "How D47 hears you, and how it sounds.",
            [
                new SettingsPlace(
                    "voice-input",
                    "Voice Input",
                    "The input device, how D47 decides you are talking to it, and which model turns speech into words.",
                    "listening",
                    ["mic", "ptt", "stt", "whisper"],
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
                                E("listening.echoCancellation"),
                                E("listening.noiseSuppression"),
                                E("listening.preRoll"),
                            ]),
                        G(
                            "Speech recognition",
                            "Who turns speech into words, and where it runs.",
                            [
                                E("listening.provider"),
                                F(IsHearingProviderKeyFamily),
                                E("listening.model"),
                                E("listening.useGpu"),
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
                    "voice",
                    "Its voice",
                    "Where spoken replies are synthesised, in which voice, and what it costs.",
                    "speech",
                    ["tts", "text to speech"],
                    false,
                    [
                        G(
                            "Ship's voice",
                            "Where D47 speaks from, and who voices it.",
                            [
                                E("speech.outputDevice"),
                                E("speech.provider"),
                                E("speech.voice"),
                                E("speech.rate"),
                                E("speech.localVoice"),
                                E("speech.localVoiceBuild"),
                                F(IsSpeechProviderKeyFamily),
                                E("speech.elevenlabs.model"),
                                E("speech.resetVoices"),
                            ]),
                        G(
                            "Where each voice comes from",
                            "Every voice that is not the ship's own can come from a different provider.",
                            [F(IsVoiceProviderSlotFamily)]),
                        G(
                            "Guardian voice",
                            "Optional treatments for the ship AI's voice, all off by default and global to every core.",
                            [
                                E("speech.guardianVoice.cylon"),
                                E("speech.guardianVoice.pitchDown"),
                                E("speech.guardianVoice.octaveDown"),
                                E("speech.guardianVoice.chorus"),
                                E("speech.guardianVoice.comb"),
                                E("speech.guardianVoice.ringMod"),
                                E("speech.guardianVoice.glitch"),
                                E("speech.guardianVoice.reverb"),
                                E("speech.guardianVoice.test"),
                            ]),
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
                    "sounds",
                    "Sounds and levels",
                    "The cues, the thinking bed, and the mixer for every channel.",
                    "audio",
                    [],
                    false,
                    [
                        G(
                            "Cues",
                            "Sounds D47 makes while it listens and thinks.",
                            [
                                E("speech.cues"),
                                E("speech.thinkingBed"),
                                E("speech.thinkingBedSound", under: true),
                            ]),
                        G("Levels", "Level, mute and duck, for every channel.", LevelEntries()),
                        G("Your own audio", "Sound files you've dropped in for D47 to use.", [E("audio.drops")]),
                    ]),
            ]),
        new SettingsArea(
            "ai",
            "The ship's AI",
            "Who answers you, how hard it thinks, and what it keeps between sessions.",
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
                                E("llm.backgroundModel"),
                                E("llm.effortFloor"),
                                E("llm.effortCeiling"),
                            ]),
                    ]),
                new SettingsPlace(
                    "look-up",
                    "What it can look up",
                    "What D47 may reach out to the web or the galaxy index for.",
                    "conversation",
                    [],
                    false,
                    [
                        G("Outside sources", "Where D47 may look for facts it does not already hold.", [E("llm.webSearch"), E("knowledge.galaxy")]),
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
                new SettingsPlace(
                    "persona",
                    "Persona",
                    "Which core is aboard, its character, and what it knows about you.",
                    "persona",
                    [],
                    false,
                    [
                        G(
                            "Core and character",
                            "Which core is aboard, how it behaves, and what it knows about you.",
                            [
                                E("persona.id"),
                                E("llm.personality"),
                                E("persona.introductions"),
                                E("persona.own"),
                                E("llm.characterSheet"),
                                E("llm.aboutMe"),
                            ]),
                        G(
                            "Humor",
                            "How funny each group may be, and on how many of its lines.",
                            [
                                E("persona.coreHumor"),
                                E("persona.coreHumorPercent", under: true),
                                E("persona.npcHumor"),
                                E("persona.npcHumorPercent", under: true),
                                E("persona.carrierHumor"),
                                E("persona.carrierHumorPercent", under: true),
                            ]),
                        G(
                            "Ship name",
                            "What you call your ship's AI, and whether the name follows it across a core switch.",
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
                    ]),
            ]),
        new SettingsArea(
            "speaking-up",
            "Speaking up",
            "What D47 says without being asked.",
            [
                new SettingsPlace(
                    "callouts",
                    "Callouts",
                    "The one switch every callout below depends on.",
                    "callouts",
                    [],
                    false,
                    [G("All callouts", "One switch for everything D47 says without being asked.", [E("callouts.enabled")])]),
                new SettingsPlace(
                    "in-flight",
                    "In flight",
                    "Danger, fuel, route progress and long jumps.",
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
                                E("callouts.routeProgress"),
                                E("callouts.routeEveryNJumps", under: true),
                                E("callouts.longJumpRemark"),
                                E("callouts.longJumpSeconds", under: true),
                                E("callouts.announcedAttack"),
                                E("callouts.rivalTerritory"),
                            ]),
                    ]),
                new SettingsPlace(
                    "exploring",
                    "Exploring",
                    "Arrivals, lore, undiscovered systems and biology.",
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
                                E("callouts.lore"),
                                E("callouts.loreCooldownDays", under: true),
                                E("callouts.discovery"),
                                E("callouts.footfall"),
                                E("callouts.biologyValue"),
                                E("callouts.surveyedBiology"),
                                E("callouts.biologyThreshold", under: true),
                                E("callouts.sampling"),
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
                            ]),
                    ]),
                new SettingsPlace(
                    "plans-and-stories",
                    "Plans and stories",
                    "Checklist changes, continuity, adventures, community goal sales and what to buy.",
                    "callouts",
                    [],
                    false,
                    [
                        G(
                            "Plan and story callouts",
                            "What D47 says about your checklist, adventures, community goals and trades.",
                            [
                                E("callouts.checklist"),
                                E("callouts.continuity"),
                                E("callouts.adventure"),
                                E("callouts.communityGoalSales"),
                                E("callouts.tradingMode"),
                                E("callouts.tradingModeMinHold", under: true),
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
                            "Invented radio traffic between other pilots, and how often it plays.",
                            [
                                E("callouts.ambient"),
                                E("callouts.ambientSeconds", under: true),
                                E("callouts.ambientMaxSeconds", under: true),
                                E("callouts.npcChatter"),
                                E("callouts.npcChatterSeconds", under: true),
                                E("callouts.npcChatterMaxSeconds", under: true),
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
                    [G("Your macros and switches", "Named key sequences you have made, and the HOTAS switches bound to them.", [E("switches.list"), E("macros.list")])]),
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
                                E("ui.bloom"),
                                E("ui.zoom"),
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
            "fleet-ships", "loadout.ships", true, "Fleet › Ships", [E("ships.remembered"), E("ships.art")]),
        new SettingsTabPlace(
            "routing-community-goal",
            "routing.communityGoal",
            true,
            "Routing › Community Goal",
            [
                E("knowledge.inaraKey"),
                E("callouts.weekBoundaryDay"),
                E("callouts.weekBoundaryHourUtc", under: true),
            ]),
        new SettingsTabPlace("adventures", "adventures", true, "Adventures", [E("knowledge.notablePlaces")]),
        new SettingsTabPlace(
            "fleet-carrier",
            "loadout.carrier",
            true,
            "Fleet › Carrier",
            [
                E("speech.carrierCaptainName"),
                E("speech.carrierCaptainVoice"),
                E("speech.towerName"),
                E("speech.towerVoice"),
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
