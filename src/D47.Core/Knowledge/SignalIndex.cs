using System.Collections;
using System.Globalization;
using System.Text;
using D47.Core.Journal;

namespace D47.Core.Knowledge;

/// <summary>
/// The daily index of FSS signals Commanders uploaded to EDDN, as <c>tools/signal-index.py</c> writes it, held
/// column by column with names interned. Each sighting is built when it is read.
/// </summary>
public sealed class SignalIndex : IReadOnlyList<SignalSighting>
{
    public const string Header = "system_address\tsystem_name\tx\ty\tz\tsignal_type\tsignal_name\tlast_seen";

    /// <summary>The most rows read; a file with more is refused.</summary>
    public const int MaxRows = 5_000_000;

    /// <summary>The longest line read; a longer one is refused with the file.</summary>
    public const int MaxLineLength = 4_096;

    private const byte NoKind = byte.MaxValue;

    private static readonly Dictionary<SignalKind, byte> KindIds =
        SignalKind.All.Select((kind, id) => (kind, id)).ToDictionary(pair => pair.kind, pair => (byte)pair.id);

    private readonly List<string> _strings = [];

    private readonly List<SystemPlace> _systems = [];

    private readonly List<Row> _rows = [];

    private SignalIndex()
    {
    }

    /// <summary>The rows skipped because a column was missing or could not be read.</summary>
    public int Malformed { get; private set; }

    public int Count => _rows.Count;

    public SignalSighting this[int index]
    {
        get
        {
            var row = _rows[index];
            var system = _systems[row.System];

            return new SignalSighting(
                system.Address,
                _strings[system.Name],
                new StarPosition(system.X, system.Y, system.Z),
                _strings[row.Type],
                _strings[row.Name],
                row.Kind == NoKind ? null : SignalKind.All[row.Kind],
                new DateTimeOffset(DateOnly.FromDayNumber(row.Day), TimeOnly.MinValue, TimeSpan.Zero));
        }
    }

    /// <summary>Reads UTF-8 TSV text; see <see cref="Read(TextReader)"/>.</summary>
    public static SignalIndex Read(Stream tsv) => Read(new StreamReader(tsv));

    /// <summary>
    /// Reads the index. A malformed row is skipped and counted, and a row whose signal d47 does not show is left
    /// out. Throws <see cref="InvalidDataException"/> when the header is missing or the file is too large.
    /// </summary>
    public static SignalIndex Read(TextReader reader)
    {
        ArgumentNullException.ThrowIfNull(reader);

        if (ReadLine(reader, new StringBuilder()) != Header)
        {
            throw new InvalidDataException("Not a signal index: the header is missing.");
        }

        var index = new SignalIndex();
        var strings = new Dictionary<string, int>(StringComparer.Ordinal);
        var systems = new Dictionary<long, int>();
        var buffer = new StringBuilder();
        var read = 0;

        while (ReadLine(reader, buffer) is { } line)
        {
            if (line.Length == 0)
            {
                continue;
            }

            if (++read > MaxRows)
            {
                throw new InvalidDataException($"The signal index has more than {MaxRows} rows.");
            }

            if (!index.Add(line, strings, systems))
            {
                index.Malformed++;
            }
        }

        index._rows.TrimExcess();
        index._systems.TrimExcess();
        index._strings.TrimExcess();

        return index;
    }

    /// <summary>Where a system is, by name; null when no row is in it.</summary>
    public StarPosition? Place(string system)
    {
        foreach (var place in _systems)
        {
            if (_strings[place.Name].Equals(system, StringComparison.OrdinalIgnoreCase))
            {
                return new StarPosition(place.X, place.Y, place.Z);
            }
        }

        return null;
    }

    /// <summary>The sightings in systems within <paramref name="lightYears"/> of <paramref name="origin"/>.</summary>
    public IEnumerable<SignalSighting> Within(StarPosition origin, double lightYears)
    {
        for (var i = 0; i < _rows.Count; i++)
        {
            var system = _systems[_rows[i].System];

            if (origin.DistanceTo(new StarPosition(system.X, system.Y, system.Z)) <= lightYears)
            {
                yield return this[i];
            }
        }
    }

    public IEnumerator<SignalSighting> GetEnumerator()
    {
        for (var i = 0; i < _rows.Count; i++)
        {
            yield return this[i];
        }
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    /// <summary>One line without its terminator, or null at the end; a line over <see cref="MaxLineLength"/> refuses the file.</summary>
    private static string? ReadLine(TextReader reader, StringBuilder line)
    {
        line.Clear();

        while (true)
        {
            var next = reader.Read();

            if (next < 0)
            {
                return line.Length == 0 ? null : line.ToString();
            }

            if (next == '\n')
            {
                return line.ToString();
            }

            if (next == '\r')
            {
                continue;
            }

            if (line.Length == MaxLineLength)
            {
                throw new InvalidDataException($"The signal index has a line longer than {MaxLineLength} characters.");
            }

            line.Append((char)next);
        }
    }

    private bool Add(string line, Dictionary<string, int> strings, Dictionary<long, int> systems)
    {
        var cells = line.Split('\t');

        if (cells.Length != 8
            || !long.TryParse(cells[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out var address)
            || cells[1].Length == 0
            || !float.TryParse(cells[2], NumberStyles.Float, CultureInfo.InvariantCulture, out var x)
            || !float.TryParse(cells[3], NumberStyles.Float, CultureInfo.InvariantCulture, out var y)
            || !float.TryParse(cells[4], NumberStyles.Float, CultureInfo.InvariantCulture, out var z)
            || cells[5].Length == 0
            || cells[6].Length == 0
            || !DateOnly.TryParseExact(cells[7], "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var seen))
        {
            return false;
        }

        if (SignalKind.Describe(cells[5], cells[6], localised: null) is not { } described)
        {
            return true;
        }

        if (!systems.TryGetValue(address, out var system))
        {
            system = _systems.Count;
            systems[address] = system;
            _systems.Add(new SystemPlace(address, Intern(cells[1], strings), x, y, z));
        }

        var kind = described.Kind is { } found ? KindIds[found] : NoKind;

        _rows.Add(new Row(system, Intern(cells[5], strings), Intern(described.Name, strings), kind, seen.DayNumber));

        return true;
    }

    private int Intern(string text, Dictionary<string, int> strings)
    {
        if (!strings.TryGetValue(text, out var id))
        {
            id = _strings.Count;
            strings[text] = id;
            _strings.Add(text);
        }

        return id;
    }

    private readonly record struct SystemPlace(long Address, int Name, float X, float Y, float Z);

    private readonly record struct Row(int System, int Type, int Name, byte Kind, int Day);
}
