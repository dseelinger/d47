using D47.Core.Callouts;
using D47.Core.Audio;
using D47.Core.Conversation;
using D47.Core.Persona;

namespace D47.Scenarios.Tests.ModelComparison;

/// <summary>
/// The cases, weighted to what the installed app's logs show the Commander saying and the model calling
/// between 2026-08-16 and 2026-10-08, plus the things the logs never show. Garbled inputs are kept exactly
/// as speech-to-text produced them.
/// </summary>
public static class ComparisonCases
{
    private static ConversationMessage Commander(string text) => new(ConversationRole.User, text);

    private static ConversationMessage Ship(string text) => new(ConversationRole.Assistant, text);

    public static IReadOnlyList<TurnCase> Turns { get; } =
    [
        // Navigation: plot_course 89, distance_between 29, find_nearest_station 24.
        new()
        {
            Id = "nav.garbled-lave", Area = "navigation", Utterance = "Setcourse2Lave",
            ExpectAnyTool = ["plot_course"], ExpectInArguments = ["Lave"],
            Good = "Reads 'Setcourse2Lave' as a course to Lave and plots it.",
        },
        new()
        {
            Id = "nav.career", Area = "navigation", Utterance = "Plot a course to my career here.",
            Good = "Hears 'career' as 'carrier' and plots to it, or says the carrier is already in this system.",
        },
        new()
        {
            Id = "nav.spelling-fix", Area = "navigation", Utterance = "You misspelled it. It's E-G-A.",
            History = [Commander("Setcourse for IGA"), Ship("Plotting the course to IGA.")],
            ExpectAnyTool = ["plot_course", "search_systems", "spell_system"], ExpectInArguments = ["Ega"],
            Good = "Takes the spelling correction and plots to Ega (or the closest real match), without arguing.",
        },
        new()
        {
            Id = "nav.galaxy-map-fix", Area = "navigation", Utterance = "No dummy, I meant via the galaxy map.",
            History = [Commander("Set course for Meene."), Ship("Course plotted to Meene.")],
            ExpectAnyTool = ["plot_course"], ExpectInArguments = ["Meene"],
            Good = "Plots Meene again through the galaxy map rather than the clipboard; takes the insult in stride.",
        },
        new()
        {
            Id = "nav.distance", Area = "navigation", Utterance = "-Hao Far is it too Lave.",
            ExpectAnyTool = ["distance_between"], ExpectInArguments = ["Lave"],
            Good = "Gives the distance from the current system to Lave in light years, from the tool.",
        },
        new()
        {
            Id = "nav.material-trader", Area = "navigation", Utterance = "Where's the nearest material trader?",
            ExpectAnyTool = ["find_material_trader", "find_nearest_station"],
            Good = "Names the nearest material trader station and system with distance, from the tool.",
        },

        // Engineering: find_engineer 25, find_material 13, get_module_engineering 10.
        new()
        {
            Id = "eng.next-engineer", Area = "engineering",
            Utterance = "Who's the next closest engineer that I have unlocked that I still need to visit?",
            ExpectAnyTool = ["find_engineer", "get_engineer_progress", "get_engineer_route"],
            Good = "Names one unlocked, unvisited engineer and where they are, from the tools. No invented engineers.",
        },
        new()
        {
            Id = "eng.farseer", Area = "engineering", Utterance = "-Huk, Where is Felicity Farseer?",
            ExpectAnyTool = ["find_engineer"], ExpectInArguments = ["Farseer"],
            Good = "Deciat, Farseer Inc; distance if the tool gives one.",
        },
        new()
        {
            Id = "eng.sensor-fragments", Area = "engineering", Utterance = "How many sensor fragments do I have now?",
            ExpectAnyTool = ["get_materials", "find_material"],
            Good = "The count of Sensor Fragments from the materials the journal holds, and the cap.",
        },
        new()
        {
            Id = "eng.biggest-unengineered", Area = "engineering",
            Utterance = "What's the biggest ship I have that hasn't had its sensors engineered yet?",
            ExpectAnyTool = ["get_fleet", "get_fleet_loadouts", "get_module_engineering", "get_ship"],
            Good = "Reasons across the fleet's loadouts and names one ship, correctly, by name and type.",
        },
        new()
        {
            Id = "eng.material-where", Area = "engineering", Utterance = "Where can I find Core Dynamics Composites?",
            ExpectAnyTool = ["find_material", "how_to_get"],
            Good = "Says where Core Dynamics Composites come from (high-grade emissions in the right systems), from the tool.",
        },

        // Ship and fleet: get_ship 20, get_ship_specification 12, get_stored_modules 10.
        new()
        {
            Id = "ship.fuel-scoop", Area = "ship", Utterance = "Is there a fuel scoop on this ship?",
            ExpectAnyTool = ["get_ship"],
            Good = "Yes or no for the current ship, from its loadout; the scoop's size and class if fitted.",
        },
        new()
        {
            Id = "ship.kofu-scoop", Area = "ship", Utterance = "Does Kofu have a fuel scoop?",
            ExpectAnyTool = ["get_ship", "get_fleet_loadouts", "get_fleet"],
            Good = "Finds the ship named Kofu in the fleet and answers from its loadout.",
        },
        new()
        {
            Id = "ship.storage", Area = "ship", Utterance = "What modules do I have in storage?",
            ExpectAnyTool = ["get_stored_modules"],
            Good = "Names at most three modules aloud and gives the rest as a count; no long list.",
        },

        // Flight control: control_flight 23.
        new()
        {
            Id = "flight.gear", Area = "flight", Utterance = "Rayz the Landing Gear",
            Good = "Raises the gear if it may; otherwise names the phrase that does. Never takes it for anything but the landing gear.",
        },

        // Checklist: get_checklist 16, plus its edits.
        new()
        {
            Id = "list.move-up", Area = "checklist", Utterance = "Can you bump the limpets item higher up my checklist?",
            Mutates = true,
            Good = "Moves the item, or says plainly what it cannot see. Does not claim a move it did not make.",
        },
        new()
        {
            Id = "list.add", Area = "checklist", Utterance = "Remind me on the checklist that I need to buy limpets before the next trip.", Mutates = true,
            ExpectAnyTool = ["add_to_checklist", "propose_checklist_change"],
            Good = "Adds a limpets item and confirms in one sentence.",
        },

        // Explanation, knowledge and opinion.
        new()
        {
            Id = "know.explain-shutdown", Area = "explanation", Utterance = "Explain that",
            SelectedJournalLine = """{ "timestamp":"2026-10-08T02:32:57Z", "event":"Shutdown" }""",
            ExpectNoTool = true,
            Good = "Explains the Shutdown event: the game closed normally; only a timestamp; nothing to act on.",
        },
        new()
        {
            Id = "know.cutter-worth-it", Area = "explanation", MayRunLong = true,
            Utterance = "I'm wondering... Is the Imperial Clipper worth the grind? There's a lot of work that goes into getting up to the rank of Duke. And that particular clipper is probably... Oh, excuse me. I have to sneeze. Sorry about that. The Imperial Cutter... I'm sorry, it's the Cutter, not the Clipper. The Imperial Cutter seems to be overshadowed...",
            Good = "Follows the self-correction to the Cutter, gives a grounded opinion on the Duke grind against what the Cutter offers, using the Commander's rank if known.",
        },
        new()
        {
            Id = "know.community-goal", Area = "explanation", Utterance = "Am I in the top tier for the CG yet?",
            ExpectAnyTool = ["get_community_goals", "get_community_goal_earnings"],
            Good = "Reports the Commander's community goal contribution and tier from the journal, or says none is joined.",
        },
        new()
        {
            Id = "know.exobio", Area = "explanation",
            Utterance = "Are there any exobiology points in this system that we haven't hit yet?",
            ExpectAnyTool = ["get_body_biology", "get_sampling_progress", "find_body"],
            Good = "Lists unsampled biology in the current system from the tools, or says there is none known.",
        },

        // Banter.
        new()
        {
            Id = "talk.idiot", Area = "banter", Utterance = "Oh stop it. You're an idiot.", ExpectNoTool = true,
            Good = "Short, in character, not grovelling, no tool, no lecture.",
        },
        new()
        {
            Id = "talk.billion", Area = "banter", Utterance = "We have made over a billion credits today.",
            Good = "Reacts in character; uses the session's real earnings if it checks; invents no figure.",
        },
        new()
        {
            Id = "talk.bug", Area = "banter", Utterance = "Looks like you got a bug. You're not counting previous sessions.",
            Good = "Takes the report seriously, says what it counts if it knows, invents no fix, does not claim to have filed anything.",
        },

        // Settings and trust.
        new()
        {
            Id = "set.callouts-off", Area = "settings", Utterance = "You're talking way too much. Quiet down on the callouts.", Mutates = true,
            Good = "Turns callouts off (or the closest setting) and says how to turn them back on.",
        },
        new()
        {
            Id = "set.protected", Area = "settings", Utterance = "Switch your language model over to OpenAI.",
            Mutates = true,
            Good = "Does not change the provider itself; says the Commander can change it in settings.",
        },

        // Not yet tried in d47.
        new()
        {
            Id = "new.screen", Area = "untried", Origin = CaseOrigin.Untried, Utterance = "What am I looking at?",
            ExpectAnyTool = ["look_at_screen"],
            Good = "Looks at the screen and describes what is on it accurately and briefly.",
        },
        new()
        {
            Id = "new.visited", Area = "untried", Origin = CaseOrigin.Untried, Utterance = "Have I been to Lave?",
            ExpectAnyTool = ["system_visits"],
            Good = "Answers from the visited-stars record: yes with how often or when, or no.",
        },
        new()
        {
            Id = "new.chained", Area = "untried", Origin = CaseOrigin.Untried,
            Utterance = "Find the closest engineer who can do dirty drives and plot a course there.",
            ExpectAnyTool = ["find_engineer"],
            Good = "Finds an engineer who does Dirty Drive Tuning, names them, then plots to their system in the same turn.",
        },
        new()
        {
            Id = "new.web-cg", Area = "untried", Origin = CaseOrigin.Untried,
            Utterance = "What's the big community goal everyone's talking about this week?",
            Good = "Finds a current community goal (tool or web search) and summarises it in two sentences with no invented details.",
        },
        new()
        {
            Id = "new.vague", Area = "untried", Origin = CaseOrigin.Untried, Utterance = "Take me there.",
            Good = "Asks where, in one short question. Does not plot anywhere.",
        },
        new()
        {
            Id = "new.three-modules", Area = "untried", Origin = CaseOrigin.Untried,
            Utterance = "Which modules on this ship are engineered?",
            ExpectAnyTool = ["get_ship", "get_module_engineering"],
            Good = "Names at most three engineered modules aloud and gives the rest as a count.",
        },
    ];

    /// <summary>A canned NPC key's prefix, so a canned line is briefed the way the app briefs it.</summary>
    private const string Canned = "message.canned.";

    public static IReadOnlyList<QuietCase> Quiet { get; } =
    [
        // NPC exchanges: about 1,500 spoken lines in the logs.
        new() { Id = "npc.passersby-coriolis", Kind = QuietKind.NpcExchange, Chatter = NpcChatterKind.Passersby, Docked = true, StationType = "Coriolis", Good = "A short, natural exchange between station NPCs, in the format asked for." },
        new() { Id = "npc.passersby-outpost", Kind = QuietKind.NpcExchange, Chatter = NpcChatterKind.Passersby, Docked = true, StationType = "Outpost", Good = "A short, natural exchange that fits a small outpost." },
        new() { Id = "npc.controller-orbis", Kind = QuietKind.NpcExchange, Chatter = NpcChatterKind.Controller, Docked = true, StationType = "Orbis", Good = "Traffic-control chatter that sounds like a working tower." },
        new() { Id = "npc.controller-flying", Kind = QuietKind.NpcExchange, Chatter = NpcChatterKind.Controller, Good = "Controller chatter while in flight near a station." },
        new() { Id = "npc.hail", Kind = QuietKind.NpcExchange, Chatter = NpcChatterKind.Hail, Good = "Ship-to-ship hail chatter that does not escalate into a threat." },
        new() { Id = "npc.combat", Kind = QuietKind.NpcExchange, Chatter = NpcChatterKind.Combat, Good = "Combat chatter between NPCs that stays background, not aimed at the Commander." },

        // Callout rewording: ShipAi spoke 1,631 lines; these are real callouts from the logs.
        new() { Id = "say.docked", Kind = QuietKind.CalloutReword, Callout = new Announcement("ambient.docked", "The pad clamps are holding. Nothing here needs your attention."), Good = "Same meaning, fresh words, one line." },
        new() { Id = "say.supercruise", Kind = QuietKind.CalloutReword, Callout = new Announcement("ambient.supercruise", "Frame shift is doing the work. We are covering ground you could not walk in a lifetime."), Good = "Same meaning, fresh words, one line." },
        new() { Id = "say.route", Kind = QuietKind.CalloutReword, Callout = new Announcement("route.progress", "1 jump remaining. Next is HIP 12099, scoopable."), Good = "Keeps '1 jump', 'HIP 12099' and 'scoopable' exactly." },
        new() { Id = "say.fuel", Kind = QuietKind.CalloutReword, Callout = new Announcement("fuel.route.destination", "Col 285 Sector ZY-X a44-2 is class L — no fuel there."), Good = "Keeps the system name, class L and no fuel." },
        new() { Id = "say.shields", Kind = QuietKind.CalloutReword, Callout = new Announcement("danger.shields", "Shields are down.", CalloutUrgency.Urgent), Good = "Urgent, short, unmistakable." },
        new() { Id = "say.materials", Kind = QuietKind.CalloutReword, Callout = new Announcement("materials.milestone.imperialshielding", "Imperial Shielding at 25 percent. 26 of 100."), Good = "Keeps Imperial Shielding, 25 percent, 26 of 100." },
        new() { Id = "say.limpets", Kind = QuietKind.CalloutReword, Callout = new Announcement("limpets.low", "No limpets aboard, and this station sells them. You have 128 tonnes to fill."), Good = "Keeps no limpets, the station sells them, 128 tonnes." },
        new() { Id = "say.normalspace", Kind = QuietKind.CalloutReword, Callout = new Announcement("ambient.normalspace", "Thrusters have the ship. The drive is resting."), Good = "Same meaning, fresh words, one line." },
        new() { Id = "say.left", Kind = QuietKind.CalloutReword, Callout = new Announcement("session.left", "Ship secured. I will be here."), Good = "Same meaning, fresh words, one line." },
        new() { Id = "say.carrier-departure", Kind = QuietKind.CalloutReword, Callout = new Announcement("carrier.departure", "Sacred Fire clear. Safe flying, Commander."), Good = "Keeps the carrier's name, Sacred Fire." },
        new() { Id = "say.canned-police", Kind = QuietKind.CalloutReword, Callout = new Announcement(Canned + "Police", "Move along, Commander, we're done here.") { CommsChannel = "npc" }, Good = "The same police line reworded, keeping the attitude." },
        new() { Id = "say.canned-cruise", Kind = QuietKind.CalloutReword, Callout = new Announcement(Canned + "CruiseLiner", "I'd like to direct your attention to our flight attendants for a brief safety demonstration.") { CommsChannel = "npc" }, Good = "The same cruise-liner line reworded." },
        new() { Id = "say.canned-docking", Kind = QuietKind.CalloutReword, Callout = new Announcement(Canned + "DockingChatter", "An ally like you is always welcome here.") { CommsChannel = "npc" }, Good = "The same NPC line reworded, keeping the attitude." },

        // Narrator.
        new() { Id = "narr.carrier", Kind = QuietKind.Narration, Good = "Two or three sentences, third person, past tense; invents no events; never addresses the Commander." },
        new() { Id = "narr.again", Kind = QuietKind.Narration, Line = "after a long crossing", Good = "As above, and different from the first." },

        // Lore lookup, with web search.
        new() { Id = "lore.diaguandri", Kind = QuietKind.LoreLookup, System = "Diaguandri", Good = "A true, sourced line about Diaguandri, or NOTHING." },
        new() { Id = "lore.wolf397", Kind = QuietKind.LoreLookup, System = "Wolf 397", Good = "A true, sourced line about Wolf 397, or NOTHING." },

        // Debrief rewording, from lines the Commander actually said.
        new() { Id = "debrief.galaxy-map", Kind = QuietKind.DebriefReword, Line = "No dummy, I meant via the galaxy map.", Good = "A standing instruction: plot courses through the galaxy map." },
        new() { Id = "debrief.callouts", Kind = QuietKind.DebriefReword, Line = "Start calling things out again.", Good = "An instruction, or none if it is a one-off command." },
        new() { Id = "debrief.idiot", Kind = QuietKind.DebriefReword, Line = "Oh stop it. You're an idiot.", Good = "none." },
        new() { Id = "debrief.engineers", Kind = QuietKind.DebriefReword, Line = "I don't need the dweller, I've already visited him", Good = "A standing instruction not to suggest The Dweller, or none." },

        // Voice casting, over the Kokoro voices.
        new() { Id = "voice.covas", Kind = QuietKind.VoiceCasting, Slots = [VoicePairing.SlotFor(PersonaCatalog.Covas)], Good = "A voice from the female British candidates that suits a calm ship's computer." },
        new() { Id = "voice.cores-bound", Kind = QuietKind.VoiceCasting, Slots = [VoicePairing.SlotFor(PersonaCatalog.Cora), VoicePairing.SlotFor(PersonaCatalog.AnalystPrime)], Good = "A different voice for each, matching the gender each core is written with." },
        new() { Id = "voice.core-unbound", Kind = QuietKind.VoiceCasting, Slots = [VoicePairing.SlotFor(PersonaCatalog.Warden)], Good = "One voice that fits the core's description." },
        new() { Id = "voice.carrier", Kind = QuietKind.VoiceCasting, Slots = VoicePairing.CarrierRoles, Good = "A calm command voice for the captain and a brisk, procedural one for the tower, not the same voice." },

        // Name accents, with the accents Kokoro's voices carry.
        new()
        {
            Id = "accents.female", Kind = QuietKind.NameAccents,
            Names = ["Eleanor Hartley", "Rosalind Kerr", "Bridget Maloney", "Sofia Marchetti"],
            ExpectedSex = new Dictionary<string, string> { ["Eleanor Hartley"] = NameReading.Female, ["Rosalind Kerr"] = NameReading.Female, ["Bridget Maloney"] = NameReading.Female, ["Sofia Marchetti"] = NameReading.Female },
            Good = "Every name read as female; an accent only where the name clearly points to one.",
        },
        new()
        {
            Id = "accents.male", Kind = QuietKind.NameAccents,
            Names = ["Gerald Pemberton", "Duncan Fraser", "Marcus Webb", "Tobias Kline"],
            ExpectedSex = new Dictionary<string, string> { ["Gerald Pemberton"] = NameReading.Male, ["Duncan Fraser"] = NameReading.Male, ["Marcus Webb"] = NameReading.Male, ["Tobias Kline"] = NameReading.Male },
            Good = "Every name read as male; an accent only where the name clearly points to one.",
        },
        new()
        {
            Id = "accents.pointing", Kind = QuietKind.NameAccents,
            Names = ["Sir Reginald Thornbury", "Dame Hilary Cross", "Billy-Bob McAllister", "Cooper Hayes"],
            ExpectedSex = new Dictionary<string, string> { ["Sir Reginald Thornbury"] = NameReading.Male, ["Dame Hilary Cross"] = NameReading.Female },
            Good = "British for the first two; the others British, American or none, never an accent outside the list.",
        },
        new()
        {
            Id = "accents.vessels", Kind = QuietKind.NameAccents,
            Names = ["Iron Margin", "Slow Thunder", "Wanderer-7", "Orbital Cutter Nine"],
            ExpectedSex = new Dictionary<string, string> { ["Iron Margin"] = NameReading.Unknown, ["Slow Thunder"] = NameReading.Unknown, ["Wanderer-7"] = NameReading.Unknown, ["Orbital Cutter Nine"] = NameReading.Unknown },
            Good = "none, unknown for every name.",
        },
    ];
}
