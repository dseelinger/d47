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
public sealed record ScrubbedLine(string? Json, int BodiesDropped);

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

    /// <summary>A message body: the words go, the field stays.</summary>
    Body,
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
/// reached by a different door.
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
    /// A boolean field on the event that has to be true, or the rule does not fire. It exists for
    /// the one question this table cannot answer from a field name — <b>is the person named here a
    /// person</b> — on the events where Elite answers it itself with <c>IsPlayer</c>. A condition
    /// read out of the event is still a field list; a condition inferred from the shape of a name
    /// would be the guesswork this class refuses.
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
        new("FID", Scrub.FrontierId),
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
        ["LoadGame"] = [new("Commander", Scrub.Person)],
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

        try
        {
            if (JsonNode.Parse(json) is not JsonObject root)
            {
                return new ScrubbedLine(null, 0);
            }

            foreach (var rule in Everywhere)
            {
                Anywhere(root, rule, names, ref bodies);
            }

            if (root["event"]?.GetValue<string>() is { } kind &&
                ByEvent.TryGetValue(kind, out var rules))
            {
                foreach (var rule in rules)
                {
                    Apply(root, rule, names, ref bodies);
                }
            }

            // One message rather than one field. ReceiveText carries the same sentence twice — raw
            // and localised — and counting fields would report two messages where one arrived.
            return new ScrubbedLine(root.ToJsonString(Flat), bodies > 0 ? 1 : 0);
        }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException or FormatException)
        {
            // Fail closed. A line this could not read is a line nobody has checked, and the whole
            // claim being made about the excerpt is that everything in it was checked.
            return new ScrubbedLine(null, 0);
        }
    }

    /// <summary>Walks the whole node applying one field rule wherever the field appears.</summary>
    private static void Anywhere(JsonNode? node, Rule rule, Pseudonyms names, ref int bodies)
    {
        switch (node)
        {
            case JsonObject json:
                foreach (var (key, value) in json.ToList())
                {
                    if (string.Equals(key, rule.Path, StringComparison.Ordinal))
                    {
                        Replace(json, key, rule.Scrub, names, ref bodies);
                        continue;
                    }

                    Anywhere(value, rule, names, ref bodies);
                }

                break;

            case JsonArray array:
                foreach (var item in array)
                {
                    Anywhere(item, rule, names, ref bodies);
                }

                break;
        }
    }

    /// <summary>Applies one event rule, resolving the three path shapes.</summary>
    private static void Apply(JsonObject root, Rule rule, Pseudonyms names, ref int bodies)
    {
        // A condition that is absent is a condition that is not met. Elite omits `IsPlayer` from
        // events it has nothing to say about, and a missing flag read as permission would make the
        // gate fire on exactly the events nobody has vouched for.
        if (rule.OnlyWhen is { } flag &&
            (root[flag] is not JsonValue gate
             || !gate.TryGetValue<bool>(out var allowed)
             || !allowed))
        {
            return;
        }

        var bracket = rule.Path.IndexOf("[]", StringComparison.Ordinal);

        if (bracket < 0)
        {
            Replace(root, rule.Path, rule.Scrub, names, ref bodies);
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
                if (Text(array[index]) is { } name && !IsSymbol(name))
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
                Replace(element, field, rule.Scrub, names, ref bodies);
            }
        }
    }

    /// <summary>Replaces one property's value in place, and does nothing where there is none.</summary>
    private static void Replace(JsonObject owner, string field, Scrub scrub, Pseudonyms names, ref int bodies)
    {
        if (!owner.ContainsKey(field) || Text(owner[field]) is not { } value)
        {
            return;
        }

        // A body goes whatever it looks like — a token in a Message is still somebody's line, and
        // the words are not what a replay needs. Everything else leaves a symbol where it found it,
        // **and leaves that symbol's translation with it**: `X` and `X_Localised` are one datum
        // rendered twice, so a `KillerName` of `$ShipName_Military_Federation;` makes its partner
        // "Federal Navy Ship" — a ship class, not a person, and replacing it read as absurd the
        // first time the corpus was swept for what these rules touch.
        if (scrub != Scrub.Body && (IsSymbol(value) || TranslatesASymbol(owner, field)))
        {
            return;
        }

        if (scrub == Scrub.Body)
        {
            bodies++;
        }

        owner[field] = JsonValue.Create(Stand(value, scrub, names));
    }

    /// <summary>
    /// Whether a value is one of Frontier's own <c>$symbol;</c> tokens rather than anything a
    /// person is called — <c>$ShipName_Military_Federation;</c> killed the Commander eleven times
    /// in the corpus, and <c>$npc_name_decorate:#name=...;</c> is how an NPC's name arrives.
    /// <para>
    /// <b>A symbol is left alone.</b> It is a game fact a replay may key on, replacing it would
    /// break a lookup, and no player is called one — Frontier's <c>$…;</c> namespace is
    /// localisation, and a Commander name never enters it. The one rule here that reads a value
    /// rather than a field name, and it reads its <em>shape</em> rather than guessing at its
    /// meaning.
    /// </para>
    /// </summary>
    private static bool IsSymbol(string value) =>
        value.StartsWith('$') && value.EndsWith(';');

    /// <summary>
    /// Whether this field is the English rendering of a sibling that is a symbol. Elite writes the
    /// pair everywhere — <c>Message</c> and <c>Message_Localised</c>, <c>Interdictor</c> and
    /// <c>Interdictor_Localised</c> — and the two always describe the same thing, so whatever is
    /// true of the one is true of the other.
    /// </summary>
    private static bool TranslatesASymbol(JsonObject owner, string field) =>
        field.EndsWith("_Localised", StringComparison.Ordinal)
        && Text(owner[field[..^"_Localised".Length]]) is { } original
        && IsSymbol(original);

    private static string Stand(string value, Scrub scrub, Pseudonyms names) => scrub switch
    {
        Scrub.Person => names.Person(value),
        Scrub.FrontierId => names.FrontierId(value),
        Scrub.Squadron => names.Squadron(value),
        Scrub.Ship => names.Ship(value),
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
