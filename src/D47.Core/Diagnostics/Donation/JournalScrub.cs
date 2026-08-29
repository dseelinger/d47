using System.Text.Json;
using System.Text.Json.Nodes;

namespace D47.Core.Diagnostics.Donation;

/// <summary>
/// One scrubbed journal line, and what it cost.
/// </summary>
/// <param name="Json">The line as it will travel, or null where it could not be read at all.</param>
/// <param name="BodiesDropped">
/// How many message bodies were blanked on the way through. Counted rather than inferred from the
/// result, because the report makes a claim about it — "no in-game message arrived in this window"
/// and a silence read the same, and only one of them is true.
/// </param>
/// <param name="FieldsDropped">
/// How many fields were removed outright. Counted for the same reason: a report that quietly takes
/// something out is a report making a claim it has not stated.
/// </param>
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

    /// <summary>
    /// A fleet carrier's identity, <b>and only where the value actually holds one</b>. The
    /// treatment carries its own guard because the fields it lives in hold ordinary station names,
    /// megaships and enum words the rest of the time — see <c>Carrier</c> below for the two shapes
    /// it recognises and the corpus measurement behind them.
    /// </summary>
    Callsign,

    /// <summary>A message body: the words go, the field stays.</summary>
    Body,

    /// <summary>
    /// The field goes entirely. <b>The only treatment that removes rather than replaces</b>, and it
    /// exists for a value that is not a name at all: a flag whose whole content is <em>this one is
    /// mine</em>. There is nothing to stand in for a true, and writing <c>false</c> would be a lie
    /// to whoever reads the report.
    /// </summary>
    Drop,

    /// <summary>
    /// A squadron's id, in either shape Elite writes it — the numeric one it keys by, and the
    /// four-character tag another Commander's ship wears. <b>The stand-in keeps the type</b>: a
    /// number stays a number, because a replay that parses this field would not survive being
    /// handed a string.
    /// </summary>
    SquadronId,
}

/// <summary>
/// The journal half's scrubber: <b>a field list, not guesswork</b>
/// (<a href="https://github.com/dseelinger/d47/issues/160">#160</a>).
/// <para>
/// The personal surface of a journal is short and enumerable, and it is enumerated below. Every
/// other field in a journal is a fact about the game — an <c>FSDJump</c> names a star system and
/// an economy, not a person — so nothing else is touched, and the excerpt stays a replay case
/// rather than becoming a redacted transcript.
/// </para>
/// <para>
/// <b>Bodies are blanked rather than removed.</b> The event shape is the half that makes an
/// excerpt worth having: <c>spike/CorpusReplay</c> drives these lines through the production
/// fold, and a <c>ReceiveText</c> with no <c>Message</c> takes a path no live event ever takes.
/// So the words are replaced by <see cref="Withheld"/> and the field stays where it was.
/// </para>
/// <para>
/// <b>It fails closed.</b> A line that will not parse, or that throws on the way through, is
/// withheld whole and counted — a scrubber that passes through what it could not read is not a
/// scrubber. See <see cref="ExcerptTally.JournalWithheld"/>, which the report prints.
/// </para>
/// <para>
/// <b>Two additions to the list the issue enumerates</b>, made on the reason the issue gives for
/// the list rather than on taste — <i>a donor cannot consent on another player's behalf</i>.
/// Ship names reach further than <c>SetUserShipName</c>: <c>Loadout</c> and <c>StoredShips</c>
/// carry the same name the Commander chose, and scrubbing only the event that sets it would leave
/// it in the one event every excerpt contains. And a handful of events name <em>other</em> players
/// outright — <c>PVPKill</c>, an interdiction, a death — which is the same personal surface
/// reached by a different door — as does the pilot of a ship the Commander targeted or shot,
/// which Elite writes as a Frontier symbol for an NPC and as <c>$cmdr_decorate:#name=…;</c> for a
/// person.
/// <para>
/// <b>A squadron is on the list twice</b>, name and id, on the Commander's ruling of 2026-08-29: a
/// squadron of one is a pseudonym for a person, and both halves resolve on INARA. A minor faction
/// is not — it is Frontier's, it belongs to the galaxy rather than to anybody, and it stays.
/// </para>
/// <para>
/// <b>Those combat events fire on a person and not on a pirate</b>, which is the Commander's ruling
/// of 2026-08-29: <i>scrub whenever it is a real player's name or Frontier ID</i>. Elite answers
/// that question itself on an interdiction, with <c>IsPlayer</c>, so the rule is conditioned on it
/// rather than on the shape of a name — a condition read out of the event is still a field list. A
/// <c>Died</c> carries no such flag and an NPC's generated name looks exactly like a Commander's,
/// so there "cannot tell" resolves to scrub. Measured over the 912-journal corpus, the difference
/// is 75 of 90 combat events now passing through untouched.
/// </para>
/// </summary>
public static class JournalScrub
{
    /// <summary>What a dropped message body is replaced by. Shaped like text, because it was.</summary>
    public const string Withheld = "[withheld]";

    /// <summary>
    /// One field, and what happens to it. <paramref name="Path"/> is a field name, an array of
    /// strings as <c>Others[]</c>, or a field inside an array of objects as <c>Killers[].Name</c>.
    /// </summary>
    /// <param name="OnlyWhen">
    /// A condition on the event that has to hold — <c>"Field"</c> for a boolean that must be true,
    /// <c>"Field=Value"</c> for a string that must match. It exists for the questions this table
    /// cannot answer from a field name: <b>is the person named here a person</b> (<c>IsPlayer</c>),
    /// <b>is this station a carrier</b> (<c>StationType</c>). A condition read out of the event is
    /// still a field list; a condition inferred from the shape of a name would be guesswork.
    /// </param>
    private sealed record Rule(string Path, Scrub Scrub, string? OnlyWhen = null);

    /// <summary>
    /// Fields whose name means the same thing wherever it appears, applied to every event at every
    /// depth. Only two qualify: <c>Name</c> means a dozen different things and is therefore an
    /// event-by-event decision below, which is exactly the difference between a field list and a
    /// guess.
    /// </summary>
    private static readonly Rule[] Everywhere =
    [
        new("SquadronName", Scrub.Squadron),
        new("SquadronID", Scrub.SquadronId),

        // **The flag that undoes the two rules above it.** An excerpt replaces GREYBEARD DELTA with
        // SQUADRON ALPHA and its id with a stand-in, and then a jump three lines later points at a
        // minor faction and says SquadronFaction: true — one hop on INARA from there to the
        // squadron and its member list. Minor factions stay, on the Commander's ruling; what goes
        // is only the sentence saying which one is theirs.
        //
        // Dropped rather than falsified, and the cost is nothing rather than a replay's game state:
        // grep the source and d47 reads neither this field, nor SquadronName, nor SquadronID, so
        // the production fold behaves identically without it. 275 events over the corpus, across
        // FSDJump, Location and CarrierJump — a global rule rather than three, because the field
        // name means one thing wherever it appears.
        new("SquadronFaction", Scrub.Drop),
        new("FID", Scrub.FrontierId),

        // The fields a fleet carrier's identity arrives in, across twenty-odd events — the
        // Commander's ruling of 2026-08-29 is that both its name and its callsign are PII, because
        // INARA and EDSM index carriers by the callsign. A field list rather than the per-event
        // conditions this started as, for a measured reason: five of those events — Shipyard,
        // Outfitting, StoredShips, StoredModules, FCMaterials — carry no StationType to condition
        // on at all, and they are the ones that list a whole fleet.
        //
        // `Type` and `SignalName` look loose in a global list and are not. Both hold enum words and
        // Frontier symbols most of the time, and Scrub.Callsign guards itself: a value that is
        // neither of the two carrier shapes is left exactly as it was.
        new("StationName", Scrub.Callsign),
        new("Callsign", Scrub.Callsign),
        new("SignalName", Scrub.Callsign),
        new("Type", Scrub.Callsign),
        new("CarrierName", Scrub.Callsign),

        // What you were closest to when you scanned something. Found by sweeping the corpus for
        // what still held a carrier after the rules above ran — 11 CodexEntry lines out of 179,378,
        // which is exactly the kind of residue a table written from the schema rather than from the
        // data would have kept.
        new("NearestDestination", Scrub.Callsign),
        new("NearestDestination_Localised", Scrub.Callsign),

        // A number on every event but one. FCMaterials writes the callsign into it as a string,
        // and Text() answers null for the numeric form, so this fires on that event alone.
        new("CarrierID", Scrub.Callsign),
    ];

    /// <summary>
    /// The list. One row per event that carries something personal; everything absent from this
    /// table travels untouched, which is most of a journal.
    /// </summary>
    private static readonly Dictionary<string, Rule[]> ByEvent = new(StringComparer.Ordinal)
    {
        // Identity. Two events say it, under different field names, and both are the front of
        // every journal file.
        ["Commander"] = [new("Name", Scrub.Person)],
        ["NewCommander"] = [new("Name", Scrub.Person)],

        // Chat. The body is another player's words on a ReceiveText and the Commander's own on a
        // SendText, and neither is the donor's to give — so both go, and the sender travels as a
        // stand-in so a conversation still reads as a conversation.
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

        // Crew, hired and multicrew alike. An NPC gunner's name is Frontier's rather than a
        // person's, and it is scrubbed anyway: telling the two apart from the event is a guess,
        // and a stand-in costs a replay nothing.
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

        // The ship's name and its ident: chosen by the Commander, and written by three events
        // rather than by the one that sets them.
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

        // A carrier's given name. Two events set or restate it, and CarrierStats restates it
        // constantly — 491 times over the corpus — which is what makes this reachable from any
        // incident window where SetUserShipName's equivalent would not be.
        // **A squadron's carrier is identified quite differently**, and the rules above miss all of
        // it: `Callsign` holds the four-character squadron tag rather than a XXX-XXX callsign,
        // `StationName` holds that same bare tag, and a scan reads "GBD FORMIDINE DREAMS | OV40".
        // Found by sweeping the corpus for names known to be real rather than by reading the
        // schema, which is the only way any of this was ever found.
        ["CarrierStats"] =
        [
            new("Name", Scrub.Carrier),
            new("Callsign", Scrub.SquadronId),
        ],
        ["CarrierNameChange"] = [new("Name", Scrub.Carrier)],

        // Who was flying the thing you targeted, or shot. **Overwhelmingly an NPC and sometimes
        // not**, and Elite marks the difference itself: an NPC arrives as a Frontier symbol, a
        // person arrives wrapped in `$cmdr_decorate:#name=…;`. The symbol exemption handles the
        // first and deliberately does not cover the second — see IsGameSymbol.
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

        // The bare tag, where the global StationName rule could not recognise it. Conditioned on
        // Elite's own word for what the station is, so an ordinary station keeps its name — and
        // harmless on a private carrier, whose StationName the global already turned into a
        // stand-in that Pseudonyms.IsStandIn declines to scrub a second time.
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

        // A private group's name, which people name after themselves: 78 of the corpus's LoadGame
        // events carry another Commander's name in this field.
        ["LoadGame"] = [new("Commander", Scrub.Person), new("Group", Scrub.Person)],

        // Who committed a crime against the Commander. A real person every time — an NPC does not
        // generate one of these.
        ["CrimeVictim"] = [new("Offender", Scrub.Person)],

        // Events that can name somebody else. **Only where that somebody is a real person**, which
        // is the Commander's ruling of 2026-08-29 and the reason three of these carry a condition:
        // an interdiction is overwhelmingly an NPC, Elite says which with `IsPlayer`, and replacing
        // Frontier's own generated pirates buys nothing and costs a replay the names it reasons
        // about. Measured over the 912-journal corpus: 67 interdictions and 23 deaths, not one of
        // them a player — because the Commander does not fly Open. Plenty of donors will.
        ["PVPKill"] = [new("Victim", Scrub.Person)],
        ["Interdicted"] =
        [
            new("Interdictor", Scrub.Person, OnlyWhen: "IsPlayer"),
            new("Interdictor_Localised", Scrub.Person, OnlyWhen: "IsPlayer"),
        ],
        ["EscapeInterdiction"] = [new("Interdictor", Scrub.Person, OnlyWhen: "IsPlayer")],
        ["Interdiction"] = [new("Interdicted", Scrub.Person, OnlyWhen: "IsPlayer")],

        // **The one that cannot be gated, and so is not.** A `Died` carries `KillerName`,
        // `KillerShip` and `KillerRank` and no player flag at all, and an NPC's generated name —
        // "Dominic Storin" — has the same shape as a Commander's. Where the event does not say,
        // "cannot tell" resolves to scrub: over-replacing a Frontier pirate costs a replay a name
        // nothing reasons about, and under-replacing hands over the one thing this class exists to
        // keep. `PVPKill` needs no condition for the opposite reason — its victim is a player by
        // definition.
        ["Died"] =
        [
            new("KillerName", Scrub.Person),
            new("KillerName_Localised", Scrub.Person),
            new("Killers[].Name", Scrub.Person),
        ],
    };

    private static readonly JsonSerializerOptions Flat = new() { WriteIndented = false };

    /// <summary>
    /// Scrubs one journal line, or withholds it. <see cref="ScrubbedLine.Json"/> is null where the
    /// line could not be read — the caller counts those rather than passing them on.
    /// </summary>
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

            // One message rather than one field. ReceiveText carries the same sentence twice — raw
            // and localised — and counting fields would report two messages where one arrived.
            return new ScrubbedLine(root.ToJsonString(Flat), bodies > 0 ? 1 : 0, dropped);
        }
        catch (Exception)
        {
            // **Fail closed, and the catch is deliberately wide.** A line this could not read is a
            // line nobody has checked, and the whole claim being made about the excerpt is that
            // everything in it was checked — so an unanticipated shape has to withhold rather than
            // escape. Anything thrown here reaches a Commander mid-donation as a crash.
            //
            // It was a list of three exception types until the corpus was swept for what these
            // rules touch. Elite writes **duplicate keys**: an assassination mission carries
            // `Target` twice, 11 lines over 912 journals, which JsonNode parses happily and then
            // throws ArgumentException on at the first enumeration. Guessing the next one is a
            // worse bet than holding everything.
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
        // A condition that is absent is a condition that is not met. Elite omits `IsPlayer` from
        // events it has nothing to say about, and a missing flag read as permission would make the
        // gate fire on exactly the events nobody has vouched for.
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

        // "Others[]" — an array of bare strings. Rewritten in place, because a wing's membership
        // is the fact and the names are not.
        if (rule.Path.Length == bracket + 2)
        {
            for (var index = 0; index < array.Count; index++)
            {
                // Through the same symbol check as every other value, though a wing mate is never
                // called one: two roads to the same decision are two roads that eventually differ.
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

    /// <summary>
    /// Whether a rule's condition holds on this event. Anything missing, or of the wrong type,
    /// answers no — a condition nobody stated is not a condition anybody met.
    /// </summary>
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

        // The one field that arrives as a number as often as a string. A stand-in of the wrong type
        // is not a redaction, it is a corrupt line: a replay reading SquadronID as an integer would
        // throw on a value that says "SQ01".
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

        // A body goes whatever it looks like — a token in a Message is still somebody's line, and
        // the words are not what a replay needs. Everything else leaves a game symbol where it
        // found it, **and leaves that symbol's translation with it**: `X` and `X_Localised` are one
        // datum rendered twice, so a `KillerName` of `$ShipName_Military_Federation;` makes its
        // partner "Federal Navy Ship" — a ship class, not a person, and replacing it read as absurd
        // the first time the corpus was swept for what these rules touch.
        if (scrub != Scrub.Body && (IsGameSymbol(value) || TranslatesASymbol(owner, field)))
        {
            return;
        }

        if (scrub == Scrub.Body)
        {
            bodies++;
        }

        // **A decorated name is spliced, not replaced.** The value around it is game state — which
        // role panel the pilot sat in, whether the ship was unmanned — and the pair has to agree:
        // `PilotName` carries the decoration and `PilotName_Localised` carries the same person in
        // prose, so the second reads the first's name rather than earning a stand-in of its own. A
        // person with two stand-ins in one excerpt is a person a reader cannot follow.
        if (scrub == Scrub.Person && DecoratedName(value) is { } inside)
        {
            owner[field] = JsonValue.Create(
                value.Replace(inside, names.Person(inside), StringComparison.Ordinal));

            return;
        }

        // The prose half of that pair, and it cannot splice the same way: the rules run in table
        // order, so by the time this is reached the decoration beside it already holds a stand-in
        // and the real name is only in the map. **This was a leak, found by running the rules over
        // a decorated pair rather than over a decorated field** — PilotName came out as CMDR ALPHA
        // and PilotName_Localised still said who they were.
        if (scrub == Scrub.Person && DecoratedName(Partner(owner, field)) is not null)
        {
            var prose = value;

            foreach (var (real, stand) in names.Replacements)
            {
                prose = prose.Replace(real, stand, StringComparison.OrdinalIgnoreCase);
            }

            // Whole-value if nothing matched, because a partner that says a person is here and a
            // value this could not place is the one combination that must not travel intact.
            owner[field] = JsonValue.Create(
                ReferenceEquals(prose, value) || prose == value ? names.Person(value) : prose);

            return;
        }

        owner[field] = JsonValue.Create(Stand(value, scrub, names));
    }

    /// <summary>The wrapper Frontier puts a <em>real Commander's</em> name inside.</summary>
    private const string Decoration = "$cmdr_decorate:#name=";

    /// <summary>
    /// Whether a value is one of Frontier's own <c>$symbol;</c> tokens rather than anything a
    /// person is called — <c>$ShipName_Military_Federation;</c> killed the Commander eleven times
    /// in the corpus, and <c>$npc_name_decorate:#name=…;</c> is how an NPC's name arrives.
    /// <para>
    /// <b>A game symbol is left alone.</b> It is a fact a replay may key on and replacing it would
    /// break a lookup. The one rule here that reads a value rather than a field name, and it reads
    /// its <em>shape</em> rather than guessing at its meaning.
    /// </para>
    /// <para>
    /// <b>Except the one that is not a fact at all.</b> <c>$cmdr_decorate:#name=CALVIN INSTI;</c> is
    /// a real player wearing a symbol's clothes, and it arrives in <c>ReceiveText.From</c>, in
    /// <c>ShipTargeted.PilotName</c> and in <c>Bounty.PilotName</c> — 15,970 values across the
    /// corpus. Exempting it was a hole this class opened for itself: <c>From</c> had been scrubbed
    /// since the first version, and the symbol rule quietly stopped it. Found by sweeping for what
    /// the rules actually touch rather than by reading them.
    /// </para>
    /// </summary>
    private static bool IsGameSymbol(string value) =>
        value.StartsWith('$')
        && value.EndsWith(';')
        && !value.Contains(Decoration, StringComparison.Ordinal);

    /// <summary>
    /// The Commander's name inside a decoration, or null where there is none. Elite concatenates
    /// tokens — <c>"$RolePanel2_unmanned; $cmdr_decorate:#name=BRADFYRD;"</c> — so this reads to the
    /// terminator rather than to the end of the value.
    /// </summary>
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
    /// A fleet carrier's identity in the two shapes Elite writes it, and the value untouched where
    /// it is neither. <b>Both shapes are measured over the 912-journal corpus rather than
    /// assumed</b>, because this is the one rule here that reads a value instead of a field name.
    /// <list type="bullet">
    /// <item>
    /// <b>The callsign alone</b> — <c>B0X-79X</c>. 24 of 968 distinct <c>StationName</c> values are
    /// shaped like this, and every one of the 24 was seen as a <c>FleetCarrier</c>.
    /// </item>
    /// <item>
    /// <b>A name with the callsign last</b> — <c>GDS PREDATOR B0X-79X</c>, <c>HMS BROTHEL
    /// X8H-B0Y</c>. 15,002 distinct values end in a callsign-shaped token and every one is a
    /// carrier. The whole value goes, because a name and its callsign are one identity — and
    /// somebody else's carrier is no more the donor's to give than their own.
    /// </item>
    /// </list>
    /// <para>
    /// <b>Position is what makes that safe, and it is the whole of the rule.</b> A megaship wears
    /// the same shape at the <em>front</em> — <c>MVU-891 Bellmarsh-class Reformatory</c>, 464
    /// distinct in the corpus — and a minor faction wears one in the middle, off the catalogue
    /// number of the star it is named for: <c>LP 466-235 Gold Boys</c>, 63 distinct. Both are game
    /// facts, both stay, and both would have gone under a rule that looked for the shape anywhere.
    /// </para>
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

        // "GBD FORMIDINE DREAMS | OV40" — a squadron's carrier, which wears its squadron's tag
        // where a private one wears a callsign. The pipe is Frontier's own separator and appears in
        // no other station name in the corpus.
        if (value.Contains(" | ", StringComparison.Ordinal))
        {
            return names.Carrier(value);
        }

        // And last, a value this excerpt has already ruled on. It is what covers a squadron
        // carrier's bare tag on the five events that say nothing about what kind of station they
        // are: an ordinary station is not in the map and comes back untouched.
        return names.Known(value, out var seen) ? seen : value;
    }

    private static bool IsCallsign(string value) =>
        value.Length == 7
        && value[3] == '-'
        && value.Where((c, at) => at != 3).All(char.IsAsciiLetterOrDigit)
        && value.ToUpperInvariant() == value;

    /// <summary>
    /// Whether this field is the English rendering of a sibling that is a symbol. Elite writes the
    /// pair everywhere — <c>Message</c> and <c>Message_Localised</c>, <c>Interdictor</c> and
    /// <c>Interdictor_Localised</c> — and the two always describe the same thing, so whatever is
    /// true of the one is true of the other.
    /// </summary>
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

    /// <summary>
    /// A node's text, or null where it is not text. Elite writes numbers into fields this table
    /// names — <c>UserShipId</c> is a string and <c>ShipID</c> is not — and a scrubber that
    /// stringified whatever it found would change a value's type under a replay.
    /// </summary>
    private static string? Text(JsonNode? node) =>
        node is JsonValue value && value.TryGetValue<string>(out var text) ? text : null;
}
