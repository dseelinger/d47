using D47.Core.Knowledge;
using D47.Core.Storage;
using Microsoft.Extensions.Logging;

namespace D47.Core.Journal;

/// <summary>One signal the Commander scanned, placed by the system it was scanned in.</summary>
public sealed record SignalSighting(
    long SystemAddress,
    string SystemName,
    StarPosition Position,
    string SignalType,
    string SignalName,
    SignalKind? Kind,
    DateTimeOffset LastSeen);

/// <summary>
/// The FSS signals the Commander has scanned, for the machine rather than per Commander. Rebuilt from the
/// journals on every start and extended by live events; holds no file.
/// </summary>
public sealed class SignalSightings(IFileSystem fileSystem, ILogger logger)
{
    private readonly Lock _gate = new();

    private readonly Dictionary<long, (string Name, StarPosition Position)> _places = [];

    private readonly Dictionary<(long System, string Type, string Name), SignalSighting> _sightings = [];

    private readonly Dictionary<string, SignalSighting> _megaships = new(StringComparer.Ordinal);

    private bool _historyFolded;

    /// <summary>Whether the journals older than this session have been folded in.</summary>
    public bool HistoryFolded
    {
        get
        {
            lock (_gate)
            {
                return _historyFolded;
            }
        }
    }

    /// <summary>A copy of every sighting kept.</summary>
    public IReadOnlyList<SignalSighting> Snapshot()
    {
        lock (_gate)
        {
            return [.. _sightings.Values, .. _megaships.Values];
        }
    }

    /// <summary>Folds live events; a synchronous append with no I/O.</summary>
    public void Apply(IReadOnlyList<JournalEvent> events)
    {
        ArgumentNullException.ThrowIfNull(events);

        lock (_gate)
        {
            foreach (var journalEvent in events)
            {
                Fold(journalEvent);
            }
        }
    }

    /// <summary>Folds journal files oldest first, on the calling thread.</summary>
    public void FoldHistory(IReadOnlyList<string> files, CancellationToken cancellation = default)
    {
        ArgumentNullException.ThrowIfNull(files);

        foreach (var file in files)
        {
            cancellation.ThrowIfCancellationRequested();

            Stream? stream;

            try
            {
                stream = fileSystem.OpenRead(file);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                logger.LogWarning(ex, "Could not read {File} for signals", file);
                continue;
            }

            if (stream is null)
            {
                continue;
            }

            using (stream)
            {
                Fold(ReadLines(stream));
            }
        }

        lock (_gate)
        {
            _historyFolded = true;
        }
    }

    /// <summary>Folds journal lines in order.</summary>
    public void Fold(IEnumerable<string> lines)
    {
        ArgumentNullException.ThrowIfNull(lines);

        var events = new List<JournalEvent>();

        foreach (var line in lines)
        {
            if (Relevant(line) && JournalEvent.TryParse(line, logger, out var parsed) && parsed is not null)
            {
                events.Add(parsed);
            }
        }

        Apply(events);
    }

    private static IEnumerable<string> ReadLines(Stream stream)
    {
        using var reader = new StreamReader(stream);

        while (reader.ReadLine() is { } line)
        {
            yield return line;
        }
    }

    private static bool Relevant(string line) =>
        line.Contains("\"event\":\"FSSSignalDiscovered\"", StringComparison.Ordinal)
        || line.Contains("\"event\":\"Location\"", StringComparison.Ordinal)
        || line.Contains("\"event\":\"FSDJump\"", StringComparison.Ordinal)
        || line.Contains("\"event\":\"CarrierJump\"", StringComparison.Ordinal);

    private void Fold(JournalEvent journalEvent)
    {
        switch (journalEvent.Kind)
        {
            case "Location" or "FSDJump" or "CarrierJump":
                if (journalEvent.Long("SystemAddress") is { } address
                    && journalEvent.String("StarSystem") is { } system
                    && StarPosition.Read(journalEvent.Raw) is { } position)
                {
                    _places[address] = (system, position);
                }

                break;

            case "FSSSignalDiscovered":
                Sight(journalEvent);
                break;
        }
    }

    private void Sight(JournalEvent journalEvent)
    {
        if (journalEvent.Long("SystemAddress") is not { } address
            || !_places.TryGetValue(address, out var place)
            || journalEvent.String("SignalType") is not { } type
            || SignalKind.Describe(type, journalEvent.String("SignalName"), journalEvent.String("SignalName_Localised"))
                is not { } described)
        {
            return;
        }

        var (kind, name) = described;

        var sighting = new SignalSighting(address, place.Name, place.Position, type, name, kind, journalEvent.Timestamp);

        if (kind == SignalKind.Megaship)
        {
            if (!_megaships.TryGetValue(name, out var held) || held.LastSeen <= sighting.LastSeen)
            {
                _megaships[name] = sighting;
            }

            return;
        }

        var key = (address, type, name);

        if (!_sightings.TryGetValue(key, out var existing) || existing.LastSeen <= sighting.LastSeen)
        {
            _sightings[key] = sighting;
        }
    }
}
