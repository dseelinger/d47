namespace D47.Core.Knowledge;

/// <summary>The system names d47 holds, gathered from its sources each time they are asked for.</summary>
public sealed class SystemsInPlay
{
    private readonly Lock _gate = new();

    private readonly List<Func<IEnumerable<string?>>> _sources = [];

    public void Add(Func<IEnumerable<string?>> source)
    {
        lock (_gate)
        {
            _sources.Add(source);
        }
    }

    /// <summary>Every name the sources hold now, once each whatever its case, blanks dropped.</summary>
    public IReadOnlyCollection<string> Snapshot()
    {
        Func<IEnumerable<string?>>[] sources;

        lock (_gate)
        {
            sources = [.. _sources];
        }

        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var source in sources)
        {
            foreach (var name in source())
            {
                if (!string.IsNullOrWhiteSpace(name))
                {
                    names.Add(name.Trim());
                }
            }
        }

        return names;
    }
}
