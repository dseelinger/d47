namespace D47.Core.Journal;

/// <summary>The system Elite has targeted on its route, and how many jumps it says remain counting that one.</summary>
public sealed record RouteTarget(string StarSystem, string? StarClass, int JumpsRemaining);

/// <summary>
/// <c>NavRoute.json</c> checked against the route targets Elite writes to the journal. Elite can change a
/// route without rewriting the file; the targets then name systems the file does not.
/// </summary>
public sealed class LiveRoute
{
    private DateTimeOffset? _targetAt;

    /// <summary><c>NavRoute.json</c> as Elite wrote it.</summary>
    public NavRoute File { get; private set; } = NavRoute.None;

    /// <summary>The latest <c>FSDTarget</c> carrying <c>RemainingJumpsInRoute</c>, or null.</summary>
    public RouteTarget? Target { get; private set; }

    /// <summary>Whether the file no longer describes the route being flown.</summary>
    public bool Changed { get; private set; }

    /// <summary>
    /// Takes this tick's file and events and checks the target from <paramref name="location"/>, folded with the
    /// same events. Its jump destination counts only in hyperspace: a game quit mid-jump leaves it set.
    /// </summary>
    public void Fold(IReadOnlyList<JournalEvent> events, NavRoute file, JournalLocation? location) =>
        Fold(
            events,
            file,
            location?.StarSystem,
            location?.Mode == FlightMode.Hyperspace ? location.JumpDestination : null);

    /// <summary>
    /// Takes this tick's file and events, then checks the target against the file from where the ship is. Pass
    /// the location after the events have been folded into it; <paramref name="jumpDestination"/> is null
    /// outside a hyperspace jump.
    /// </summary>
    public void Fold(IReadOnlyList<JournalEvent> events, NavRoute file, string? currentSystem, string? jumpDestination)
    {
        ArgumentNullException.ThrowIfNull(events);
        ArgumentNullException.ThrowIfNull(file);

        if (!ReferenceEquals(file, File))
        {
            // Elite writes the NavRoute event and the first target in the same second as the file, and the file
            // can be read a tick after them; a target from that second describes the new file.
            if (_targetAt is not { } at || file.ReadAt is not { } written || at < Second(written))
            {
                Target = null;
                _targetAt = null;
            }

            File = file;
        }

        foreach (var journalEvent in events)
        {
            switch (journalEvent.Kind)
            {
                case "FSDTarget":
                    Target = journalEvent.String("Name") is { } name && journalEvent.Int("RemainingJumpsInRoute") is { } jumps
                        ? new RouteTarget(name, journalEvent.String("StarClass"), jumps)
                        : null;
                    _targetAt = Target is null ? null : journalEvent.Timestamp;
                    break;

                case "NavRoute" or "NavRouteClear":
                    Target = null;
                    _targetAt = null;
                    break;
            }
        }

        Changed = Diverges(currentSystem, jumpDestination);
    }

    private bool Diverges(string? currentSystem, string? jumpDestination)
    {
        if (Target is not { } target)
        {
            return false;
        }

        // In the tunnel Elite targets the hop after the destination; until it does, the target is the
        // destination itself and is checked from the system being left.
        var from = jumpDestination is not null && !Same(target.StarSystem, jumpDestination)
            ? jumpDestination
            : currentSystem;

        // A target already reached says nothing about what is ahead of it.
        if (Same(target.StarSystem, from))
        {
            return false;
        }

        var hops = File.Hops;
        var at = -1;

        for (var index = 0; index < hops.Count && from is not null; index++)
        {
            if (Same(hops[index].StarSystem, from))
            {
                at = index;
                break;
            }
        }

        // The same hops NavRoute.Ahead returns, read in place: this runs on every tick.
        var ahead = at < 0 ? 0 : hops.Count - at - 1;

        return ahead == 0 || ahead != target.JumpsRemaining || !Same(hops[at + 1].StarSystem, target.StarSystem);
    }

    private static bool Same(string? first, string? second) =>
        string.Equals(first, second, StringComparison.OrdinalIgnoreCase);

    private static DateTimeOffset Second(DateTimeOffset at) =>
        new(at.Ticks - (at.Ticks % TimeSpan.TicksPerSecond), at.Offset);
}
