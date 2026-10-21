namespace D47.Core.Journal;

/// <summary>The friends the journal has reported online this game launch. Holds no file.</summary>
public sealed class FriendRoster
{
    /// <summary>A name last reported longer ago than this and reported online again counts as arriving again.</summary>
    public static readonly TimeSpan SameVisit = TimeSpan.FromMinutes(30);

    private readonly Lock _gate = new();

    private readonly Dictionary<string, DateTimeOffset> _online = new(StringComparer.OrdinalIgnoreCase);

    private List<string> _arrivals = [];

    /// <summary>The names that came online in the last <see cref="Apply"/>.</summary>
    public IReadOnlyList<string> Arrivals
    {
        get
        {
            lock (_gate)
            {
                return _arrivals;
            }
        }
    }

    /// <summary>The names online now, each with the time it was last reported.</summary>
    public IReadOnlyDictionary<string, DateTimeOffset> Online
    {
        get
        {
            lock (_gate)
            {
                return new Dictionary<string, DateTimeOffset>(_online, StringComparer.OrdinalIgnoreCase);
            }
        }
    }

    public void Apply(IReadOnlyList<JournalEvent> events)
    {
        ArgumentNullException.ThrowIfNull(events);

        var arrivals = new List<string>();

        lock (_gate)
        {
            foreach (var journalEvent in events)
            {
                switch (journalEvent.Kind)
                {
                    case "Fileheader" when journalEvent.Int("part") == 1:
                    case "Shutdown":
                        _online.Clear();
                        break;

                    case "Friends" when journalEvent.String("Name") is { Length: > 0 } name:
                        switch (journalEvent.String("Status"))
                        {
                            case "Online":
                                var arrived = !_online.TryGetValue(name, out var last)
                                    || journalEvent.Timestamp - last > SameVisit;

                                _online[name] = journalEvent.Timestamp;

                                if (arrived && !arrivals.Contains(name, StringComparer.OrdinalIgnoreCase))
                                {
                                    arrivals.Add(name);
                                }

                                break;

                            case "Offline":
                                _online.Remove(name);
                                break;
                        }

                        break;
                }
            }

            _arrivals = arrivals;
        }
    }
}
