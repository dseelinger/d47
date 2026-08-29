namespace D47.Core.Diagnostics.Donation;

/// <summary>
/// The consistent stand-in names one excerpt uses
/// (<a href="https://github.com/dseelinger/d47/issues/160">#160</a>).
/// <para>
/// <b>Consistent, and only within one excerpt.</b> A reader has to be able to follow one person
/// across a dozen events — a wing that forms, jumps and breaks up is three names and one story —
/// so the same input always gets the same stand-in here. It is deliberately <em>not</em> stable
/// across excerpts, and a hash or a saved table is exactly what would make it so.
/// </para>
/// <para>
/// <b>What that used to buy has been given up on purpose, and the sentence has to change with
/// it</b> (<a href="https://github.com/dseelinger/d47/issues/176">#176</a>). This said that two
/// donations from the same Commander could not be joined, and that was true while the envelope
/// carried nothing. It no longer is: donations from one installation now travel under a random
/// <see cref="DonorToken"/> so that a journal history can be added to, and anybody holding the
/// store can group them on it whatever this class does to the body. The weaker claim, which is
/// still worth stating: <b>donations from one installation accumulate under a random token that
/// identifies an install, not a person</b>, and the content stand-ins below remain per-donation.
/// The reversal is argued in #176 rather than left to be inferred from a bucket that groups
/// neatly, and a donor reads the weaker claim before their first donation rather than after.
/// </para>
/// <para>
/// <b>The map never leaves.</b> <see cref="Replacements"/> exists so the log half can be given the
/// same treatment as the journal half — a pseudonymised <c>LoadGame</c> is worth nothing if the
/// line below it says the real name — and nothing renders it. What the report shows is
/// <see cref="Count"/>, which says how much was replaced without saying what.
/// </para>
/// <para>
/// Allocation is by first sight, in the order the events are read, which is why an excerpt built
/// twice from the same window comes out byte-identical: the input order is the journal's own.
/// </para>
/// </summary>
public sealed class Pseudonyms
{
    /// <summary>
    /// The NATO alphabet, because it exists to be unambiguous when read aloud and a defect
    /// report is discussed as often as it is read. Past twenty-six it wraps with a suffix —
    /// <c>ALPHA-2</c> — rather than inventing more words.
    /// </summary>
    private static readonly string[] Words =
    [
        "ALPHA", "BRAVO", "CHARLIE", "DELTA", "ECHO", "FOXTROT", "GOLF", "HOTEL", "INDIA",
        "JULIETT", "KILO", "LIMA", "MIKE", "NOVEMBER", "OSCAR", "PAPA", "QUEBEC", "ROMEO",
        "SIERRA", "TANGO", "UNIFORM", "VICTOR", "WHISKEY", "XRAY", "YANKEE", "ZULU",
    ];

    private readonly Dictionary<string, string> _replacements =
        new(StringComparer.OrdinalIgnoreCase);

    private readonly Dictionary<Kind, int> _issued = [];

    /// <summary>What a replaced value is replaced <em>with</em> — one counter and one shape each.</summary>
    private enum Kind
    {
        Person,
        FrontierId,
        Squadron,
        Ship,
        Carrier,
        Callsign,
        SquadronTag,
    }

    /// <summary>
    /// Numeric stand-ins, kept apart from the rest because they are not text. One counter, because
    /// the only numeric identifier on the list is a squadron's.
    /// </summary>
    private readonly Dictionary<long, long> _numbers = [];

    /// <summary>
    /// The stand-in this already gave a value, without allocating one for a value it has not seen.
    /// <para>
    /// <b>It is how the events with nothing to condition on are covered.</b> <c>Shipyard</c>,
    /// <c>StoredShips</c>, <c>Outfitting</c>, <c>StoredModules</c> and <c>FCMaterials</c> hold a
    /// station's name and say nothing about what kind of station it is — so a squadron carrier's
    /// bare tag is indistinguishable there from an ordinary station's name, and the only safe way
    /// to recognise it is to have already been told, by an event in the same excerpt that did say.
    /// </para>
    /// </summary>
    public bool Known(string value, out string standIn) =>
        _replacements.TryGetValue(value, out standIn!);

    /// <summary>
    /// Whether a value is something this already issued. <b>A guard against scrubbing twice</b>:
    /// two rules can reach one field — a global on <c>StationName</c> and an event rule for the
    /// carriers the global cannot recognise — and the second must not give a stand-in a stand-in.
    /// </summary>
    public bool IsStandIn(string value) => _issuedValues.Contains(value);

    private readonly HashSet<string> _issuedValues = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>How many distinct values have been given a stand-in.</summary>
    public int Count => _replacements.Count + _numbers.Count;

    /// <summary>
    /// Every real value and what it became, longest real value first.
    /// <para>
    /// <b>Longest first is load-bearing</b>, and only for the log half, which is free text and
    /// therefore substituted rather than parsed. A Commander named <c>JOHN</c> flying a ship named
    /// <c>JOHN DEPARAGON'S FOLLY</c> has one name inside the other, and replacing the short one
    /// first leaves <c>CMDR ALPHA DEPARAGON'S FOLLY</c> — a value that is neither the real one nor
    /// the stand-in, and that no later pass can recognise.
    /// </para>
    /// </summary>
    public IReadOnlyList<KeyValuePair<string, string>> Replacements =>
        [.. _replacements.OrderByDescending(pair => pair.Key.Length)];

    /// <summary>A Commander, a crew mate, a wing mate or a message sender.</summary>
    public string Person(string name) => For(name, Kind.Person);

    /// <summary>A Frontier ID. Shaped like one, because a field that must parse still has to.</summary>
    public string FrontierId(string fid) => For(fid, Kind.FrontierId);

    /// <summary>A squadron's name.</summary>
    public string Squadron(string name) => For(name, Kind.Squadron);

    /// <summary>A ship's given name or its ident — the Commander named both, so both are theirs.</summary>
    public string Ship(string name) => For(name, Kind.Ship);

    /// <summary>A fleet carrier's given name.</summary>
    public string Carrier(string name) => For(name, Kind.Carrier);

    /// <summary>
    /// A carrier's callsign. <b>Frontier assigned it and it is still PII</b> — the Commander's
    /// ruling of 2026-08-29: a callsign is the key INARA and EDSM index carriers by, so it ties a
    /// carrier to an owner as surely as a name does, and more reliably.
    /// <para>
    /// The stand-in keeps the shape Frontier uses, because a replay reads it: a value that no
    /// longer parses as a callsign is a value the fold takes a different branch on.
    /// </para>
    /// </summary>
    public string Callsign(string callsign) => For(callsign, Kind.Callsign);

    /// <summary>
    /// A squadron's four-character tag, as another Commander's ship wears it. Shaped like a tag,
    /// because it is drawn as one.
    /// </summary>
    public string SquadronTag(string tag) => For(tag, Kind.SquadronTag);

    /// <summary>
    /// A squadron's numeric id. <b>Returns a number</b>: the field is an integer in every event but
    /// one, and a replay reading it would not survive a string.
    /// </summary>
    public long SquadronNumber(long id)
    {
        if (_numbers.TryGetValue(id, out var already))
        {
            return already;
        }

        var issued = 900_000L + _numbers.Count;

        _numbers[id] = issued;
        return issued;
    }

    /// <summary>
    /// The stand-in for one value, allocating one on first sight.
    /// <para>
    /// Blank in, blank out. Elite writes empty strings into name fields more often than it writes
    /// missing ones — an unnamed ship, a message from nobody — and issuing <c>CMDR ALPHA</c> for
    /// the absence of a person would invent a person.
    /// </para>
    /// </summary>
    private string For(string value, Kind kind)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return value;
        }

        if (_replacements.TryGetValue(value, out var already))
        {
            return already;
        }

        var ordinal = _issued.GetValueOrDefault(kind);
        _issued[kind] = ordinal + 1;

        var issued = kind switch
        {
            Kind.Person => $"CMDR {Word(ordinal)}",
            Kind.FrontierId => $"F{900_000 + ordinal}",
            Kind.Squadron => $"SQUADRON {Word(ordinal)}",
            Kind.Carrier => $"CARRIER {Word(ordinal)}",
            Kind.Callsign => $"ZZ0-{(ordinal + 1) % 1000:000}",
            Kind.SquadronTag => $"SQ{(ordinal + 1) % 100:00}",
            _ => $"SHIP {Word(ordinal)}",
        };

        _replacements[value] = issued;
        _issuedValues.Add(issued);
        return issued;
    }

    private static string Word(int ordinal) =>
        ordinal < Words.Length
            ? Words[ordinal]
            : $"{Words[ordinal % Words.Length]}-{(ordinal / Words.Length) + 1}";
}
