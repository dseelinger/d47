namespace D47.Core.Diagnostics.Donation;

/// <summary>
/// The consistent stand-in names one excerpt uses
/// (<a href="https://github.com/dseelinger/d47/issues/160">#160</a>).
/// <para>
/// <b>Consistent, and only within one excerpt.</b> A reader has to be able to follow one person
/// across a dozen events — a wing that forms, jumps and breaks up is three names and one story —
/// so the same input always gets the same stand-in here. It is deliberately <em>not</em> stable
/// across excerpts: two donations from the same Commander must not be joinable on
/// <c>CMDR ALPHA</c>, which is what a hash or a saved table would make them.
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
    }

    /// <summary>How many distinct values have been given a stand-in.</summary>
    public int Count => _replacements.Count;

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
            _ => $"SHIP {Word(ordinal)}",
        };

        _replacements[value] = issued;
        return issued;
    }

    private static string Word(int ordinal) =>
        ordinal < Words.Length
            ? Words[ordinal]
            : $"{Words[ordinal % Words.Length]}-{(ordinal / Words.Length) + 1}";
}
