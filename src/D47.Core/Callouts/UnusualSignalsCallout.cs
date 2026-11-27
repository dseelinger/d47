using System.Globalization;
using D47.Core.Audio;
using D47.Core.Journal;

namespace D47.Core.Callouts;

/// <summary>
/// Notable stellar phenomena, once per system, and Guardian or Thargoid signals, once per body and kind, said
/// from whichever of <c>FSSBodySignals</c> or <c>SAASignalsFound</c> comes first.
/// </summary>
public sealed class UnusualSignalsCallout : ICallout
{
    private const string GuardianSymbol = "$SAA_SignalType_Guardian;";
    private const string ThargoidSymbol = "$SAA_SignalType_Thargoid;";

    private readonly HashSet<long> _phenomena = [];
    private readonly HashSet<(long SystemAddress, int BodyId, string Kind)> _bodies = [];

    public string Id => "unusual-signals";

    public IEnumerable<Announcement> Examine(CalloutContext context)
    {
        foreach (var journalEvent in context.Events)
        {
            switch (journalEvent.Kind)
            {
                case "FSSSignalDiscovered"
                    when journalEvent.String("SignalType") == "Codex"
                        && journalEvent.Long("SystemAddress") is { } address
                        && _phenomena.Add(address)
                        && !context.IsPriming:
                    yield return new Announcement($"unusual-signals.{address}", "Notable stellar phenomena in this system.");
                    break;

                case "FSSBodySignals" or "SAASignalsFound"
                    when journalEvent.Long("SystemAddress") is { } address
                        && journalEvent.Int("BodyID") is { } bodyId
                        && journalEvent.String("BodyName") is { } bodyName:
                    foreach (var announcement in Sites(context, journalEvent, address, bodyId, bodyName))
                    {
                        yield return announcement;
                    }

                    break;
            }
        }
    }

    private IEnumerable<Announcement> Sites(CalloutContext context, JournalEvent journalEvent, long address, int bodyId, string bodyName)
    {
        foreach (var row in journalEvent.Items("Signals"))
        {
            var kind = row.String("Type") switch
            {
                GuardianSymbol => "Guardian",
                ThargoidSymbol => "Thargoid",
                _ => null,
            };

            if (kind is null || !_bodies.Add((address, bodyId, kind)) || context.IsPriming)
            {
                continue;
            }

            var body = BiologyCallout.Short(bodyName, journalEvent.String("StarSystem") ?? context.State?.Location.StarSystem);
            var count = row.Int("Count") ?? 1;
            var signals = count > 1
                ? $"{SpokenNumbers.Expand(count.ToString(CultureInfo.InvariantCulture))} {kind} signals"
                : $"{kind} signals";

            yield return new Announcement($"unusual-signals.{address}.{bodyId}.{kind}", $"{body}: {signals}.");
        }
    }
}
