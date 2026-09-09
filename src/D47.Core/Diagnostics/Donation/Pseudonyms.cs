namespace D47.Core.Diagnostics.Donation;

/// <summary>The consistent stand-in names one excerpt uses (#160).</summary>
public sealed class Pseudonyms
{
    /// <summary>
    /// The NATO alphabet, because it exists to be unambiguous when read aloud and a defect report is
    /// discussed as often as it is read.
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

    /// <summary>What a replaced value is replaced with — one counter and one shape each.</summary>
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

    /// <summary>Numeric stand-ins, kept apart from the rest because they are not text.</summary>
    private readonly Dictionary<long, long> _numbers = [];

    /// <summary>
    /// The stand-in this already gave a value, without allocating one for a value it has not seen.
    /// </summary>
    public bool Known(string value, out string standIn) =>
        _replacements.TryGetValue(value, out standIn!);

    /// <summary>Whether a value is something this already issued.</summary>
    public bool IsStandIn(string value) => _issuedValues.Contains(value);

    private readonly HashSet<string> _issuedValues = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>How many distinct values have been given a stand-in.</summary>
    public int Count => _replacements.Count + _numbers.Count;

    /// <summary>Every real value and what it became, longest real value first.</summary>
    public IReadOnlyList<KeyValuePair<string, string>> Replacements =>
        [.. _replacements.OrderByDescending(pair => pair.Key.Length)];

    /// <summary>A Commander, a crew mate, a wing mate or a message sender.</summary>
    public string Person(string name) => For(name, Kind.Person);

    /// <summary>A Frontier ID.</summary>
    public string FrontierId(string fid) => For(fid, Kind.FrontierId);

    /// <summary>A squadron's name.</summary>
    public string Squadron(string name) => For(name, Kind.Squadron);

    /// <summary>A ship's given name or its ident — the Commander named both, so both are theirs.</summary>
    public string Ship(string name) => For(name, Kind.Ship);

    /// <summary>A fleet carrier's given name.</summary>
    public string Carrier(string name) => For(name, Kind.Carrier);

    /// <summary>A carrier's callsign.</summary>
    public string Callsign(string callsign) => For(callsign, Kind.Callsign);

    /// <summary>A squadron's four-character tag, as another Commander's ship wears it.</summary>
    public string SquadronTag(string tag) => For(tag, Kind.SquadronTag);

    /// <summary>A squadron's numeric id.</summary>
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

    /// <summary>The stand-in for one value, allocating one on first sight.</summary>
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
