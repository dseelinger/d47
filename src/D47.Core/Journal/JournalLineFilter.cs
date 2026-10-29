using System.Collections.Frozen;

namespace D47.Core.Journal;

/// <summary>Whether a raw journal line names an event a reader parses, decided before any JSON is read.</summary>
internal sealed class JournalLineFilter
{
    private const string EventKey = "\"event\":\"";

    private readonly FrozenSet<string> _kinds;

    private readonly string[] _prefixes;

    private JournalLineFilter(FrozenSet<string> kinds, string[] prefixes)
    {
        _kinds = kinds;
        _prefixes = prefixes;
    }

    /// <summary>The event names the filter matches exactly.</summary>
    public IReadOnlyCollection<string> Kinds => _kinds;

    /// <summary>Lines whose event is one of <paramref name="kinds"/>.</summary>
    public static JournalLineFilter Events(params string[] kinds) =>
        new(kinds.ToFrozenSet(StringComparer.Ordinal), []);

    /// <summary>Lines whose event name starts with <paramref name="prefix"/> as well.</summary>
    public JournalLineFilter OrPrefix(string prefix) => new(_kinds, [.. _prefixes, prefix]);

    /// <summary>Tests the name after the first <c>"event":"</c> in the line.</summary>
    public bool Matches(string line)
    {
        var start = line.IndexOf(EventKey, StringComparison.Ordinal);

        if (start < 0)
        {
            return false;
        }

        start += EventKey.Length;
        var end = line.IndexOf('"', start);

        if (end <= start)
        {
            return false;
        }

        var name = line.AsSpan(start, end - start);

        if (_kinds.GetAlternateLookup<ReadOnlySpan<char>>().Contains(name))
        {
            return true;
        }

        foreach (var prefix in _prefixes)
        {
            if (name.StartsWith(prefix, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }
}
