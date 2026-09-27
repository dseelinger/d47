using D47.Core.Journal;

namespace D47.Core.Callouts;

/// <summary>
/// A body just mapped with the DSS: its estimated value and the unsold total (#527). Spoken on the Detailed scan
/// that follows <c>SAAScanComplete</c>, since that scan is what gives the body's class and mass.
/// </summary>
public sealed class MappingCallout : ICallout
{
    public string Id => "mapping";

    /// <summary>Where a mapped body is priced and the unsold total kept; null says nothing.</summary>
    public CartographyLedger? Ledger { get; init; }

    private readonly Dictionary<(long System, int Body), JournalEvent> _awaitingScan = [];

    public IEnumerable<Announcement> Examine(CalloutContext context)
    {
        if (context.IsPriming || Ledger is null || context.State is not { } state)
        {
            _awaitingScan.Clear();
            yield break;
        }

        foreach (var journalEvent in context.Events)
        {
            if (journalEvent.Long("SystemAddress") is not { } system || journalEvent.Int("BodyID") is not { } body)
            {
                continue;
            }

            if (journalEvent.Kind == "SAAScanComplete")
            {
                _awaitingScan[(system, body)] = journalEvent;
                continue;
            }

            if (journalEvent.Kind != "Scan"
                || journalEvent.String("ScanType") != "Detailed"
                || !_awaitingScan.Remove((system, body), out var mapped)
                || Ledger.Price(mapped) is not { } map)
            {
                continue;
            }

            yield return new Announcement($"mapping.{mapped.Timestamp.Ticks}", Line(map, state));
        }
    }

    private string Line(HeldMap map, CommanderGameState state)
    {
        var line = $"{map.BodyName} mapped{(map.Efficient ? ", efficiently" : string.Empty)}.";

        line += map.Value is { } value
            ? $" About {BiologyCallout.Credits(value)}."
            : " I have no scan to value it from.";

        // Only once the older journals are folded, since before then the total leaves out what they hold.
        if (Ledger is not { HistoryFolded: true } ledger)
        {
            return line;
        }

        return line + $" About {BiologyCallout.Credits(ledger.Unsold(state.Identity.FrontierId).Total)} unsold.";
    }
}
