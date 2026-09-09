using System.Text.Json;
using System.Text.Json.Nodes;

namespace D47.Core.Diagnostics.Donation;

/// <summary>One scrubbed journal line, and what it cost.</summary>
/// <param name="Json">The line as it will travel, or null where it could not be read at all.</param>
/// <param name="BodiesDropped">
/// How many message bodies were blanked on the way through.
/// </param>
/// <param name="FieldsDropped">How many fields were removed outright.</param>
public sealed record ScrubbedLine(string? Json, int BodiesDropped, int FieldsDropped);

/// <summary>What one field is replaced with.</summary>
public enum Scrub
{
    /// <summary>A person's name — the Commander's, a crew mate's, a message sender's.</summary>
    Person,

    /// <summary>A Frontier ID.</summary>
    FrontierId,

    /// <summary>A squadron's name.</summary>
    Squadron,

    /// <summary>A ship's given name or ident, both of which the Commander chose.</summary>
    Ship,

    /// <summary>A fleet carrier's given name.</summary>
    Carrier,

    /// <summary>A fleet carrier's identity, and only where the value actually holds one.</summary>
    Callsign,

    /// <summary>A message body: the words go, the field stays.</summary>
    Body,

    /// <summary>The field goes entirely.</summary>
    Drop,

    /// <summary>
    /// A squadron's id, in either shape Elite writes it — the numeric one it keys by, and the
    /// four-character tag another Commander's ship wears.
    /// </summary>
    SquadronId,
}

/// <summary>The journal half's scrubber: a field list, not guesswork (#160).</summary>
public static class JournalScrub
{
    /// <summary>What a dropped message body is replaced by.</summary>
    public const string Withheld = "[withheld]";

    /// <summary>One field, and what happens to it.</summary>
    /// <paramref name="Path"/>
    /// is a field name, an array of strings as <c>Others[]</c>, or a field inside an array of objects
    /// as <c>Killers[].Name</c>.
    /// </paramref>
    private sealed record Rule(string Path, Scrub Scrub, string? OnlyWhen = null);

    /// <summary>
    /// Fields whose name means the same thing wherever it appears, applied to every event at every
    /// depth.
    /// </summary>
    private static readonly Rule[] Everywhere =
    [
        new("SquadronName", Scrub.Squadron),
        new("SquadronID", Scrub.SquadronId),

        // **The flag that undoes the two rules above it.** An excerpt replaces EXAMPLE SQUADRON with SQUADRON
        // ALPHA and its id with a stand-in, and then a jump three lines later points at a minor faction and
        // says SquadronFaction: true — one hop on INARA from there to the squadron and its member list.
        new("SquadronFaction", Scrub.Drop),
        new("FID", Scrub.FrontierId),

        // The fields a fleet carrier's identity arrives in, across twenty-odd events — the Commander's ruling
        // of 2026-08-29 is that both its name and its callsign are PII, because INARA and EDSM index carriers
        // by the callsign.
        new("StationName", Scrub.Callsign),
        new("Callsign", Scrub.Callsign),
        new("SignalName", Scrub.Callsign),
        new("Type", Scrub.Callsign),
        new("CarrierName", Scrub.Callsign),

        // What you were closest to when you scanned something.
        new("NearestDestination", Scrub.Callsign),
        new("NearestDestination_Localised", Scrub.Callsign),

        // A number on every event but one.
        new("CarrierID", Scrub.Callsign),
    ];

    /// <summary>The list.</summary>
    private static readonly Dictionary<string, Rule[]> ByEvent = new(StringComparer.Ordinal)
    {
        // Identity.
        ["Commander"] = [new("Name", Scrub.Person)],
        ["NewCommander"] = [new("Name", Scrub.Person)],

        // Chat.
        ["ReceiveText"] =
        [
            new("Message", Scrub.Body),
            new("Message_Localised", Scrub.Body),
            new("From", Scrub.Person),
            new("From_Localised", Scrub.Person),
        ],
        ["SendText"] =
        [
            new("Message", Scrub.Body),
            new("To", Scrub.Person),
            new("To_Localised", Scrub.Person),
        ],

        ["Friends"] = [new("Name", Scrub.Person)],

        // Crew, hired and multicrew alike.
        ["CrewMemberJoins"] = [new("Crew", Scrub.Person)],
        ["CrewMemberQuits"] = [new("Crew", Scrub.Person)],
        ["CrewMemberRoleChange"] = [new("Crew", Scrub.Person)],
        ["KickCrewMember"] = [new("Crew", Scrub.Person)],
        ["CrewLaunch"] = [new("Crew", Scrub.Person)],
        ["JoinACrew"] = [new("Captain", Scrub.Person)],
        ["QuitACrew"] = [new("Captain", Scrub.Person)],
        ["CrewHire"] = [new("Name", Scrub.Person)],
        ["CrewFire"] = [new("Name", Scrub.Person)],
        ["CrewAssign"] = [new("Name", Scrub.Person)],
        ["NpcCrewRank"] = [new("NpcCrewName", Scrub.Person)],
        ["NpcCrewPaidWage"] = [new("NpcCrewName", Scrub.Person)],

        ["WingAdd"] = [new("Name", Scrub.Person)],
        ["WingInvite"] = [new("Name", Scrub.Person)],
        ["WingJoin"] = [new("Others[]", Scrub.Person)],

        // The ship's name and its ident: chosen by the Commander, and written by three events rather than by
        // the one that sets them.
        ["SetUserShipName"] =
        [
            new("UserShipName", Scrub.Ship),
            new("UserShipId", Scrub.Ship),
        ],
        ["Loadout"] =
        [
            new("ShipName", Scrub.Ship),
            new("ShipIdent", Scrub.Ship),
        ],
        ["StoredShips"] =
        [
            new("ShipsHere[].Name", Scrub.Ship),
            new("ShipsRemote[].Name", Scrub.Ship),
        ],

        // A carrier's given name.
        ["CarrierStats"] =
        [
            new("Name", Scrub.Carrier),
            new("Callsign", Scrub.SquadronId),
        ],
        ["CarrierNameChange"] = [new("Name", Scrub.Carrier)],

        // Who was flying the thing you targeted, or shot. **Overwhelmingly an NPC and sometimes not**, and
        // Elite marks the difference itself: an NPC arrives as a Frontier symbol, a person arrives wrapped in
        // `$cmdr_decorate:#name=…;`.
        ["ShipTargeted"] =
        [
            new("PilotName", Scrub.Person),
            new("PilotName_Localised", Scrub.Person),
        ],
        ["Bounty"] =
        [
            new("PilotName", Scrub.Person),
            new("PilotName_Localised", Scrub.Person),
        ],

        // The bare tag, where the global StationName rule could not recognise it.
        ["Docked"] = [new("StationName", Scrub.SquadronId, OnlyWhen: "StationType=FleetCarrier")],
        ["Undocked"] = [new("StationName", Scrub.SquadronId, OnlyWhen: "StationType=FleetCarrier")],
        ["Location"] = [new("StationName", Scrub.SquadronId, OnlyWhen: "StationType=FleetCarrier")],
        ["CarrierJump"] = [new("StationName", Scrub.SquadronId, OnlyWhen: "StationType=FleetCarrier")],
        ["Market"] = [new("StationName", Scrub.SquadronId, OnlyWhen: "StationType=FleetCarrier")],
        ["DockingRequested"] = [new("StationName", Scrub.SquadronId, OnlyWhen: "StationType=FleetCarrier")],
        ["DockingGranted"] = [new("StationName", Scrub.SquadronId, OnlyWhen: "StationType=FleetCarrier")],
        ["DockingDenied"] = [new("StationName", Scrub.SquadronId, OnlyWhen: "StationType=FleetCarrier")],
        ["DockingCancelled"] = [new("StationName", Scrub.SquadronId, OnlyWhen: "StationType=FleetCarrier")],
        ["DockingTimeout"] = [new("StationName", Scrub.SquadronId, OnlyWhen: "StationType=FleetCarrier")],

        // A private group's name, which people name after themselves: 78 of the corpus's LoadGame events
        // carry another Commander's name in this field.
        ["LoadGame"] = [new("Commander", Scrub.Person), new("Group", Scrub.Person)],

        // Who committed a crime against the Commander.
        ["CrimeVictim"] = [new("Offender", Scrub.Person)],

        // Events that can name somebody else. **Only where that somebody is a real person**, which is the
        // Commander's ruling of 2026-08-29 and the reason three of these carry a condition: an interdiction
        // is overwhelmingly an NPC, Elite says which with `IsPlayer`, and replacing Frontier's own generated
        // pirates buys nothing and costs a replay the names it reasons about.
        ["PVPKill"] = [new("Victim", Scrub.Person)],
        ["Interdicted"] =
        [
            new("Interdictor", Scrub.Person, OnlyWhen: "IsPlayer"),
            new("Interdictor_Localised", Scrub.Person, OnlyWhen: "IsPlayer"),
        ],
        ["EscapeInterdiction"] = [new("Interdictor", Scrub.Person, OnlyWhen: "IsPlayer")],
        ["Interdiction"] = [new("Interdicted", Scrub.Person, OnlyWhen: "IsPlayer")],

        // **The one that cannot be gated, and so is not.** A `Died` carries `KillerName`, `KillerShip` and
        // `KillerRank` and no player flag at all, and an NPC's generated name — "Dominic Storin" — has the
        // same shape as a Commander's.
        ["Died"] =
        [
            new("KillerName", Scrub.Person),
            new("KillerName_Localised", Scrub.Person),
            new("Killers[].Name", Scrub.Person),
        ],
    };

    private static readonly JsonSerializerOptions Flat = new() { WriteIndented = false };

    /// <summary>Scrubs one journal line, or withholds it.</summary>
    public static ScrubbedLine Line(string json, Pseudonyms names)
    {
        var bodies = 0;
        var dropped = 0;

        try
        {
            if (JsonNode.Parse(json) is not JsonObject root)
            {
                return new ScrubbedLine(null, 0, 0);
            }

            foreach (var rule in Everywhere)
            {
                Anywhere(root, rule, names, ref bodies, ref dropped);
            }

            if (root["event"]?.GetValue<string>() is { } kind &&
                ByEvent.TryGetValue(kind, out var rules))
            {
                foreach (var rule in rules)
                {
                    Apply(root, rule, names, ref bodies, ref dropped);
                }
            }

            // One message rather than one field.
            return new ScrubbedLine(root.ToJsonString(Flat), bodies > 0 ? 1 : 0, dropped);
        }
        catch (Exception)
        {
            // **Fail closed, and the catch is deliberately wide.** A line this could not read is a line
            // nobody has checked, and the whole claim being made about the excerpt is that everything in it
            // was checked — so an unanticipated shape has to withhold rather than escape.
            return new ScrubbedLine(null, 0, 0);
        }
    }

    /// <summary>Walks the whole node applying one field rule wherever the field appears.</summary>
    private static void Anywhere(JsonNode? node, Rule rule, Pseudonyms names, ref int bodies, ref int dropped)
    {
        switch (node)
        {
            case JsonObject json:
                foreach (var (key, value) in json.ToList())
                {
                    if (string.Equals(key, rule.Path, StringComparison.Ordinal))
                    {
                        Replace(json, key, rule.Scrub, names, ref bodies, ref dropped);
                        continue;
                    }

                    Anywhere(value, rule, names, ref bodies, ref dropped);
                }

                break;

            case JsonArray array:
                foreach (var item in array)
                {
                    Anywhere(item, rule, names, ref bodies, ref dropped);
                }

                break;
        }
    }

    /// <summary>Applies one event rule, resolving the three path shapes.</summary>
    private static void Apply(JsonObject root, Rule rule, Pseudonyms names, ref int bodies, ref int dropped)
    {
        // A condition that is absent is a condition that is not met.
        if (rule.OnlyWhen is { } condition && !Holds(root, condition))
        {
            return;
        }

        var bracket = rule.Path.IndexOf("[]", StringComparison.Ordinal);

        if (bracket < 0)
        {
            Replace(root, rule.Path, rule.Scrub, names, ref bodies, ref dropped);
            return;
        }

        if (root[rule.Path[..bracket]] is not JsonArray array)
        {
            return;
        }

        // "Others[]" — an array of bare strings.
        if (rule.Path.Length == bracket + 2)
        {
            for (var index = 0; index < array.Count; index++)
            {
                // Through the same symbol check as every other value, though a wing mate is never called one:
                // two roads to the same decision are two roads that eventually differ.
                if (Text(array[index]) is { } name && !IsGameSymbol(name))
                {
                    array[index] = JsonValue.Create(Stand(name, rule.Scrub, names));
                }
            }

            return;
        }

        // "Killers[].Name" — a field inside each element.
        var field = rule.Path[(bracket + 3)..];

        foreach (var item in array)
        {
            if (item is JsonObject element)
            {
                Replace(element, field, rule.Scrub, names, ref bodies, ref dropped);
            }
        }
    }

    /// <summary>Whether a rule's condition holds on this event.</summary>
    private static bool Holds(JsonObject root, string condition)
    {
        var equals = condition.IndexOf('=', StringComparison.Ordinal);

        if (equals < 0)
        {
            return root[condition] is JsonValue flag && flag.TryGetValue<bool>(out var set) && set;
        }

        return string.Equals(
            Text(root[condition[..equals]]),
            condition[(equals + 1)..],
            StringComparison.Ordinal);
    }

    /// <summary>Replaces one property's value in place, and does nothing where there is none.</summary>
    private static void Replace(
        JsonObject owner, string field, Scrub scrub, Pseudonyms names, ref int bodies, ref int dropped)
    {
        if (!owner.ContainsKey(field))
        {
            return;
        }

        // Before the text check, because a flag is a boolean and Text() would answer null for it.
        if (scrub == Scrub.Drop)
        {
            owner.Remove(field);
            dropped++;
            return;
        }

        // The one field that arrives as a number as often as a string.
        if (scrub == Scrub.SquadronId
            && owner[field] is JsonValue number
            && number.TryGetValue<long>(out var id))
        {
            owner[field] = JsonValue.Create(names.SquadronNumber(id));
            return;
        }

        if (Text(owner[field]) is not { } value)
        {
            return;
        }

        // A body goes whatever it looks like — a token in a Message is still somebody's line, and the words
        // are not what a replay needs.
        if (scrub != Scrub.Body && (IsGameSymbol(value) || TranslatesASymbol(owner, field)))
        {
            return;
        }

        if (scrub == Scrub.Body)
        {
            bodies++;
        }

        // **A decorated name is spliced, not replaced.** The value around it is game state — which role panel
        // the pilot sat in, whether the ship was unmanned — and the pair has to agree: `PilotName` carries
        // the decoration and `PilotName_Localised` carries the same person in prose, so the second reads the
        // first's name rather than earning a stand-in of its own.
        if (scrub == Scrub.Person && DecoratedName(value) is { } inside)
        {
            owner[field] = JsonValue.Create(
                value.Replace(inside, names.Person(inside), StringComparison.Ordinal));

            return;
        }

        // The prose half of that pair, and it cannot splice the same way: the rules run in table order, so by
        // the time this is reached the decoration beside it already holds a stand-in and the real name is
        // only in the map. **This was a leak, found by running the rules over a decorated pair rather than
        // over a decorated field** — PilotName came out as CMDR ALPHA and PilotName_Localised still said who
        // they were.
        if (scrub == Scrub.Person && DecoratedName(Partner(owner, field)) is not null)
        {
            var prose = value;

            foreach (var (real, stand) in names.Replacements)
            {
                prose = prose.Replace(real, stand, StringComparison.OrdinalIgnoreCase);
            }

            // Whole-value if nothing matched, because a partner that says a person is here and a value this
            // could not place is the one combination that must not travel intact.
            owner[field] = JsonValue.Create(
                ReferenceEquals(prose, value) || prose == value ? names.Person(value) : prose);

            return;
        }

        owner[field] = JsonValue.Create(Stand(value, scrub, names));
    }

    /// <summary>The wrapper Frontier puts a real Commander's name inside.</summary>
    private const string Decoration = "$cmdr_decorate:#name=";

    /// <summary>
    /// Whether a value is one of Frontier's own <c>$symbol;</c> tokens rather than anything a person is
    /// called — <c>$ShipName_Military_Federation;</c> killed the Commander eleven times in the corpus,
    /// and <c>$npc_name_decorate:#name=…;</c> is how an NPC's name arrives.
    /// </summary>
    private static bool IsGameSymbol(string value) =>
        value.StartsWith('$')
        && value.EndsWith(';')
        && !value.Contains(Decoration, StringComparison.Ordinal);

    /// <summary>The Commander's name inside a decoration, or null where there is none.</summary>
    private static string? DecoratedName(string? value)
    {
        if (value is null)
        {
            return null;
        }

        var at = value.IndexOf(Decoration, StringComparison.Ordinal);

        if (at < 0)
        {
            return null;
        }

        var from = at + Decoration.Length;
        var end = value.IndexOf(';', from);

        return end > from ? value[from..end] : null;
    }

    /// <summary>
    /// A fleet carrier's identity in the two shapes Elite writes it, and the value untouched where it
    /// is neither.
    /// </summary>
    private static string Carrier(string value, Pseudonyms names)
    {
        if (names.IsStandIn(value))
        {
            return value;
        }

        if (IsCallsign(value))
        {
            return names.Callsign(value);
        }

        var space = value.LastIndexOf(' ');

        if (space > 0 && IsCallsign(value[(space + 1)..]))
        {
            return names.Carrier(value);
        }

        // "EXA EXAMPLE HORIZON | EX01" — a squadron's carrier, which wears its squadron's tag where a private
        // one wears a callsign.
        if (value.Contains(" | ", StringComparison.Ordinal))
        {
            return names.Carrier(value);
        }

        // And last, a value this excerpt has already ruled on.
        return names.Known(value, out var seen) ? seen : value;
    }

    private static bool IsCallsign(string value) =>
        value.Length == 7
        && value[3] == '-'
        && value.Where((c, at) => at != 3).All(char.IsAsciiLetterOrDigit)
        && value.ToUpperInvariant() == value;

    /// <summary>Whether this field is the English rendering of a sibling that is a symbol.</summary>
    private static bool TranslatesASymbol(JsonObject owner, string field) =>
        Partner(owner, field) is { } original && IsGameSymbol(original);

    /// <summary>The unlocalised half of an <c>X</c>/<c>X_Localised</c> pair, seen from the latter.</summary>
    private static string? Partner(JsonObject owner, string field) =>
        field.EndsWith("_Localised", StringComparison.Ordinal)
            ? Text(owner[field[..^"_Localised".Length]])
            : null;

    private static string Stand(string value, Scrub scrub, Pseudonyms names) => scrub switch
    {
        Scrub.Person => names.Person(value),
        Scrub.FrontierId => names.FrontierId(value),
        Scrub.Squadron => names.Squadron(value),
        Scrub.SquadronId => names.IsStandIn(value) ? value : names.SquadronTag(value),
        Scrub.Ship => names.Ship(value),
        Scrub.Carrier => names.Carrier(value),
        Scrub.Callsign => Carrier(value, names),
        _ => Withheld,
    };

    /// <summary>A node's text, or null where it is not text.</summary>
    private static string? Text(JsonNode? node) =>
        node is JsonValue value && value.TryGetValue<string>(out var text) ? text : null;
}
