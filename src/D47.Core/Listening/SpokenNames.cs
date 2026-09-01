using D47.Core.Journal;

namespace D47.Core.Listening;

/// <summary>
/// Every name this Commander has actually met — systems, stations and minor factions — as a
/// catalogue to match a misheard one against
/// (<a href="https://github.com/dseelinger/d47/issues/134">#134</a>).
/// <para>
/// <b>This is the catalogue the galaxy does not have.</b> There are 400 billion systems and d47
/// ships no list of them, which is why a misheard system name failed where a misheard module name
/// would not: <c>Catalogue.Near</c> had nothing to run against. Their own journals are the local
/// list, and a far better one than the galaxy would be — a name they have stood in is enormously
/// likelier to be the one they just said than a random system nobody has visited.
/// </para>
/// <para>
/// <b>Measured on the corpus this was built against:</b> 943 journals hold <b>15,216</b> distinct
/// names — 4,829 systems, 968 stations, 9,422 factions — in about 300 KB of text. And the reported
/// case is in there: <i>Eurybia</i> and <i>Eurybia Blue Mafia</i> both appear, so the mishearing
/// that started this was recoverable from the Commander's own history all along.
/// </para>
/// <para>
/// <b>It only ever grows, and that is what makes it cheap.</b> A ship can be sold and a loadout
/// can go stale, but a name the Commander has once met stays a name they might say — so there is
/// no forgetting, no reconciliation, and nothing that has to be got right about ordering.
/// </para>
/// <para>
/// <b>Names of places, never words other people wrote.</b> Nothing here comes from a chat message,
/// a mission description or another Commander — see <see cref="Apply"/>, which names each event it
/// reads and why. That is the untrusted-input rule reaching the listening surface.
/// </para>
/// </summary>
public sealed record SpokenNames
{
    /// <summary>
    /// The most names one Commander's catalogue may hold. Generous next to the 15,216 measured,
    /// and a bound all the same: this is matched against on a failed lookup, and an unbounded set
    /// fed by an unbounded history is a cost nobody chose.
    /// </summary>
    public const int Limit = 40_000;

    public static readonly SpokenNames Empty = new();

    /// <summary>
    /// Every name, in the order they were first met. <b>Order is kept</b> so that the file reads
    /// as a history and a truncation at <see cref="Limit"/> drops the oldest rather than an
    /// arbitrary one.
    /// </summary>
    public IReadOnlyList<string> Names { get; init; } = [];

    public bool IsKnown => Names.Count > 0;

    /// <summary>
    /// Whether this Commander has met this name. <b>The guard the alias store leans on</b>: a
    /// token that already names something is not a mishearing and must never be aliased.
    /// </summary>
    public bool Knows(string name) =>
        Knowledge.Catalogue.Match(Names, name ?? string.Empty) is not null;

    /// <summary>
    /// Names close enough to what was said to be worth offering back, sound-alikes included.
    /// </summary>
    public IReadOnlyList<string> Near(string spoken) =>
        Knowledge.Catalogue.NearSpoken(Names, spoken ?? string.Empty);

    /// <summary>
    /// Files whatever names this event carried.
    /// <para>
    /// <b>Each field is named rather than the event being scraped</b>, because the difference
    /// between a place and a person's words is the whole of the trust rule here. Systems, stations
    /// and the factions that hold them are facts about the galaxy that Elite wrote; a
    /// <c>ReceiveText</c> message, a mission's flavour text and another Commander's name are not,
    /// and none of them is read.
    /// </para>
    /// <para>
    /// <b>Bodies are deliberately left out.</b> They are 6,137 of the corpus's names and almost
    /// all of them are a system name with " A 1 a" on the end — noise in a list whose whole job is
    /// to be matched against, and every one of them already contributes its system.
    /// </para>
    /// </summary>
    public SpokenNames Apply(JournalEvent journalEvent)
    {
        ArgumentNullException.ThrowIfNull(journalEvent);

        var found = new List<string?>
        {
            // Where they are, where they are going, where they have been.
            journalEvent.String("StarSystem"),
            journalEvent.String("SystemName"),
            journalEvent.String("StationName"),

            // Who holds it. The two named factions Elite attaches to a place, and the plain one
            // that arrives on a mission's giving faction.
            journalEvent.Object("SystemFaction")?.String("Name"),
            journalEvent.Object("StationFaction")?.String("Name"),
            journalEvent.String("Faction"),
        };

        // The full presence list on an FSDJump or Location, which is where most of the 9,422 come
        // from — a system the Commander flew through carries every faction standing in it.
        found.AddRange(journalEvent.Items("Factions").Select(faction => faction.Named("Name")));

        return With(found);
    }

    /// <summary>Adds names, keeping the ones already held and their order.</summary>
    public SpokenNames With(IEnumerable<string?> names)
    {
        ArgumentNullException.ThrowIfNull(names);

        List<string>? grown = null;
        HashSet<string>? seen = null;

        foreach (var name in names)
        {
            if (!IsWorthKeeping(name))
            {
                continue;
            }

            seen ??= new HashSet<string>(Names, StringComparer.OrdinalIgnoreCase);

            if (!seen.Add(name!.Trim()))
            {
                continue;
            }

            grown ??= [.. Names];
            grown.Add(name.Trim());
        }

        if (grown is null)
        {
            return this;
        }

        // Oldest first out, which is what keeping the order buys: a Commander who has met forty
        // thousand names is likelier to say one of the recent ones.
        return this with
        {
            Names = grown.Count <= Limit ? grown : [.. grown.Skip(grown.Count - Limit)],
        };
    }

    /// <summary>
    /// <b>"unknown" is not a name</b>, and it is what several journal fields carry when Elite has
    /// none — so without this the catalogue would offer it back as a near miss. A single character
    /// is not worth matching against either.
    /// </summary>
    private static bool IsWorthKeeping(string? name) =>
        name is { Length: > 1 }
        && name.Trim() is { Length: > 1 } trimmed
        && !string.Equals(trimmed, "unknown", StringComparison.OrdinalIgnoreCase);
}
