namespace D47.Core.Help;

/// <summary>One entry in the spoken map: a category with children, or a leaf naming one capability.</summary>
public sealed record HelpNode(
    string Name,
    string Sentence,
    IReadOnlyList<HelpNode> Children,
    string? CapabilityId)
{
    /// <summary>A category: no capability of its own, at most <see cref="HelpTaxonomy.MostAtOnce"/> children.</summary>
    public static HelpNode Category(string name, string sentence, params HelpNode[] children) =>
        new(name, sentence, children, CapabilityId: null);

    /// <summary>A leaf: one capability, no children.</summary>
    public static HelpNode Leaf(string name, string sentence, string capabilityId) =>
        new(name, sentence, [], capabilityId);
}

/// <summary>
/// The spoken map of what D47 can do (#48, #166): a tree bounded to six things per level, over the
/// registry's capabilities, chosen for how they sound rather than for <see cref="D47.Core.Capabilities.CapabilityDescriptor.Group"/>.
/// Nothing speaks it yet — this is the tree and the tests that keep it honest.
/// </summary>
public static class HelpTaxonomy
{
    /// <summary>No level says more than this many things at once.</summary>
    public const int MostAtOnce = 6;

    /// <summary>
    /// Capability ids left out of the tree on purpose, starting with help itself. Learned phrases has no
    /// leaf of its own: it is reached through the "did you mean" offer that teaches it and the panel page
    /// that lists it, not through a spoken drill (#171).
    /// </summary>
    public static readonly IReadOnlyList<string> Unspoken = ["help", "learned-phrases"];

    public static readonly IReadOnlyList<HelpNode> Top =
    [
        HelpNode.Category(
            "Flying",
            "Everything about handling the ship in the moment.",
            HelpNode.Category(
                "Controls",
                "The switches and panels a Commander reaches for in flight.",
                HelpNode.Leaf(
                    "Flight and navigation",
                    "Operate the landing gear, lights, cargo scoop, hardpoints and the frame shift drive.",
                    "flight-controls"),
                HelpNode.Leaf(
                    "Ship systems",
                    "Move power between engines, weapons and systems, and reach silent running and heat sinks.",
                    "ship-systems"),
                HelpNode.Leaf(
                    "Panels and interface",
                    "Open the cockpit panels, move around them, and change fire group.",
                    "panels"),
                HelpNode.Leaf(
                    "SRV",
                    "Operate the SRV's turret, handbrake, drive assist and ship recall.",
                    "srv")),
            HelpNode.Category(
                "Getting there",
                "Pointing the ship somewhere and keeping it flying itself.",
                HelpNode.Leaf(
                    "Navigation",
                    "Put a system name on your clipboard, and try to plot a course to it.",
                    "navigation"),
                HelpNode.Leaf(
                    "Focus the game",
                    "Bring Elite Dangerous to the front, so flight commands can be sent again.",
                    "focus"),
                HelpNode.Leaf(
                    "Acting on its own",
                    "What D47 will do to your ship without being asked, all off until you turn it on.",
                    "autonomous-actions")),
            HelpNode.Category(
                "Automation and switches",
                "Ship actions run from a macro or a stick, and messages sent for you.",
                HelpNode.Leaf(
                    "Macros",
                    "Run one of your own named sequences of ship actions.",
                    "macros"),
                HelpNode.Leaf(
                    "HOTAS switches",
                    "Make a maintained switch on your stick mean a state, gear down means down, whatever the game was already doing.",
                    "switches"),
                HelpNode.Leaf(
                    "Comms",
                    "Send a message in Elite's chat, to local, system, wing or squadron.",
                    "comms"))),

        HelpNode.Category(
            "Trading and goals",
            "Finding what to do next, and what a place is worth.",
            HelpNode.Category(
                "Finding things",
                "Where to go, what to sell, and what a site still needs.",
                HelpNode.Leaf(
                    "Galaxy search",
                    "Look up star systems, and work out how far apart two of them are.",
                    "galaxy"),
                HelpNode.Leaf(
                    "Route planning",
                    "Plot a neutron route, a Road to Riches loop, or a trade run.",
                    "routes"),
                HelpNode.Leaf(
                    "Colonisation",
                    "Say what your construction sites still need, what you are already carrying towards them, and which nearby systems have the bodies your next colony wants.",
                    "colonisation"),
                HelpNode.Leaf(
                    "Community goals",
                    "What community goals are running, what tier they have reached, and how you are doing in them.",
                    "community-goals")),
            HelpNode.Category(
                "The galaxy itself",
                "What a place is, beyond what it can sell you.",
                HelpNode.Leaf(
                    "System names",
                    "Say what a system's own name reveals about it, from its sector, its boxel and the mass code that sizes it.",
                    "system-names"),
                HelpNode.Leaf(
                    "Lore",
                    "Say what is notable about a system on arrival, and remember what you tell me about one.",
                    "lore"),
                HelpNode.Leaf(
                    "Exobiology",
                    "Plot a circuit through known biology, and read back what your own surface scan found on the body you are at.",
                    "exobiology"),
                HelpNode.Leaf(
                    "Adventures",
                    "Follow stories you fly, written by you or by the ship's AI, and moved forward by your own journal.",
                    "adventures"))),

        HelpNode.Category(
            "Ship and engineering",
            "What your ships and your Commander can be fitted with, and what that costs.",
            HelpNode.Category(
                "Loadout and engineering",
                "What a hull, module or suit can do, and what it costs to improve one.",
                HelpNode.Leaf(
                    "Ship and module specifications",
                    "Say what a hull or a module can do, from a table built out of the community's own data.",
                    "specifications"),
                HelpNode.Leaf(
                    "Engineers",
                    "Say where each engineer is, what they grade, and how far along the Commander is with them.",
                    "engineers"),
                HelpNode.Leaf(
                    "Engineering",
                    "Say what a blueprint costs and changes, and how the craft on a fitted module went.",
                    "engineering"),
                HelpNode.Leaf(
                    "On foot",
                    "Say what the Commander is wearing and carrying, what an on-foot grade or modification costs, and where its materials come from.",
                    "on-foot")),
            HelpNode.Category(
                "Plans and fleet",
                "The builds you intend, and what they still need.",
                HelpNode.Leaf(
                    "Checklists",
                    "Keep one list of what you are working on, your own lines, your ship builds and your construction sites.",
                    "checklists"),
                HelpNode.Leaf(
                    "Ships",
                    "Track your fleet, the hulls you intend, and one build per ship.",
                    "ships"),
                HelpNode.Leaf(
                    "Carrier",
                    "Report the fleet carrier's fuel, cargo, balance, jump range, docking access and services.",
                    "carrier"),
                HelpNode.Leaf(
                    "The gap",
                    "Say what everything you have planned needs that you are not carrying, ledger by ledger.",
                    "gap"))),

        HelpNode.Category(
            "Talking and voices",
            "How D47 sounds, and what it keeps between sessions.",
            HelpNode.Category(
                "The voice",
                "How replies are spoken, and how D47 hears you back.",
                HelpNode.Leaf(
                    "Speech",
                    "Speak replies aloud, give each stage its own cue, and stop on command.",
                    "speech"),
                HelpNode.Leaf(
                    "Audio mixer",
                    "Say how loud each kind of sound is, and how far it drops while D47 is speaking.",
                    "audio"),
                HelpNode.Leaf(
                    "Listening",
                    "Hear the Commander through the chosen microphone while the push-to-talk key is held.",
                    "listening"),
                HelpNode.Leaf(
                    "Callouts",
                    "Speak up about danger, fuel, route progress and arrivals without waiting to be asked.",
                    "callouts")),
            HelpNode.Category(
                "What it remembers and says",
                "The conversation itself, and what carries forward from one session to the next.",
                HelpNode.Leaf(
                    "Language model",
                    "Report which language model is answering, whether it can be reached, and what this session has cost.",
                    "conversation"),
                HelpNode.Leaf(
                    "Persona",
                    "Say which Guardian core is aboard, what it is called, and how to change it.",
                    "persona"),
                HelpNode.Leaf(
                    "Memory",
                    "Keep facts about the Commander between sessions, and say where each one came from.",
                    "memory"),
                HelpNode.Leaf(
                    "Debrief",
                    "After a session, draft standing directions from what the Commander corrected, and change nothing until they take one.",
                    "debrief"))),

        HelpNode.Category(
            "Seeing what happened",
            "Where you are, what you have done, and what it adds up to.",
            HelpNode.Leaf(
                "Diagnostics",
                "Report where D47 keeps its files, and turn a subsystem's logging up or down without a restart.",
                "diagnostics"),
            HelpNode.Leaf(
                "Journal",
                "Report where the Commander is, what they are flying, what they own and what they have done this session, from the journal.",
                "journal"),
            HelpNode.Leaf(
                "Crew",
                "Report the pilots you have hired, who is on duty, and how to talk to them.",
                "crew"),
            HelpNode.Leaf(
                "Commander's log",
                "Turn a session, or a week, into a readable log, written from the journal and nothing else.",
                "logbook"),
            HelpNode.Leaf(
                "Goals",
                "Track the campaigns that take months, derive their progress from the journal, and say what to do about one today.",
                "goals")),

        HelpNode.Category(
            "Settings and safety",
            "What D47 shows, what it changes, and what it sends off this machine.",
            HelpNode.Leaf(
                "Privacy",
                "State exactly what D47 is sending off this machine right now, and to whom.",
                "privacy"),
            HelpNode.Leaf(
                "Settings",
                "List the settings D47 may change on your behalf, read one, or change one.",
                "settings"),
            HelpNode.Leaf(
                "About",
                "Say which build this is, where it keeps its files, and what changed.",
                "about"),
            HelpNode.Leaf(
                "Interface",
                "Choose D47's theme and the keys that reach it.",
                "interface"),
            HelpNode.Leaf(
                "Headset",
                "Show D47 in the headset as an overlay, over Elite, in your own cockpit.",
                "vr"),
            HelpNode.Category(
                "Clocks and timers",
                "The date in both worlds, and reminders that say their own name.",
                HelpNode.Leaf(
                    "Clock",
                    "Say the date and time in both worlds.",
                    "clock"),
                HelpNode.Leaf(
                    "Timers and alarms",
                    "Set timers and alarms that say their own name.",
                    "utilities"))),
    ];

    /// <summary>Every leaf in the tree, depth first.</summary>
    public static IEnumerable<HelpNode> Leaves(IEnumerable<HelpNode>? nodes = null) =>
        (nodes ?? Top).SelectMany(node =>
            node.CapabilityId is not null ? [node] : Leaves(node.Children));

    /// <summary>The category directly containing a leaf, or null when it is not in the tree (#172).</summary>
    public static HelpNode? CategoryOf(HelpNode leaf, IReadOnlyList<HelpNode>? level = null)
    {
        foreach (var node in level ?? Top)
        {
            if (node.Children.Contains(leaf))
            {
                return node;
            }

            if (CategoryOf(leaf, node.Children) is { } found)
            {
                return found;
            }
        }

        return null;
    }
}
