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
    private sealed record Rule(string Path, Scrub Scrub);

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

        // Events that name somebody else outright.
        ["PVPKill"] = [new("Victim", Scrub.Person)],
        ["Interdicted"] = [new("Interdictor", Scrub.Person)],
        ["EscapeInterdiction"] = [new("Interdictor", Scrub.Person)],
        ["Interdiction"] = [new("Interdicted", Scrub.Person)],
        ["Died"] = [new("KillerName", Scrub.Person), new("Killers[].Name", Scrub.Person)],
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
                if (Text(array[index]) is { } name)
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

        if (scrub == Scrub.Body)
        {
            bodies++;
        }

        owner[field] = JsonValue.Create(Stand(value, scrub, names));
    }

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
