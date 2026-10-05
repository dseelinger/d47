using D47.Core.Journal;
using D47.Core.Mining;

namespace D47.Core.Callouts;

/// <summary>A mapped ring's hotspots, most first, with the mining target leading when the ring has it (#608).</summary>
public sealed class RingHotspotsCallout : ICallout
{
    private const int Listed = 3;

    private readonly HashSet<string> _spoken = new(StringComparer.Ordinal);

    public string Id => "ring-hotspots";

    /// <summary>The Commander's mining target, or null for none.</summary>
    public Func<MiningTarget?> Target { get; set; } = () => null;

    public IEnumerable<Announcement> Examine(CalloutContext context)
    {
        if (context.IsPriming)
        {
            yield break;
        }

        foreach (var journalEvent in context.Events)
        {
            if (journalEvent.Kind != "SAASignalsFound"
                || journalEvent.String("BodyName") is not { } bodyName
                || !bodyName.EndsWith(" Ring", StringComparison.Ordinal)
                || !_spoken.Add(bodyName))
            {
                continue;
            }

            var hotspots = journalEvent
                .Items("Signals")
                .Select(row => new BodySignal(row.Spoken("Type") ?? "an unnamed signal", row.Int("Count") ?? 0))
                .Where(signal => signal.Count > 0)
                .OrderByDescending(signal => signal.Count)
                .ToList();

            if (hotspots.Count == 0)
            {
                continue;
            }

            yield return new Announcement(
                $"ring-hotspots.{journalEvent.Timestamp.Ticks}",
                $"{ShortName(bodyName)}: {Line(hotspots, Target())}");
        }
    }

    /// <summary>"Omicron Capricorni B B 1 A Ring" is "A Ring".</summary>
    private static string ShortName(string ringName)
    {
        var words = ringName.Split(' ', StringSplitOptions.RemoveEmptyEntries);

        return words.Length > 2 ? string.Join(' ', words[^2..]) : ringName;
    }

    private static string Line(List<BodySignal> hotspots, MiningTarget? target)
    {
        if (target is null)
        {
            return List(hotspots) + ".";
        }

        var wanted = hotspots.FirstOrDefault(signal => MiningTarget.SymbolOf(signal.Type) == target.Symbol);

        if (wanted is null)
        {
            return $"no {target.Material}. {List(hotspots)}.";
        }

        var line = Say(wanted) + ".";
        var others = hotspots.Where(signal => signal != wanted).ToList();

        return others.Count == 0 ? line : line + " Also " + List(others, Listed - 1) + ".";
    }

    private static string List(List<BodySignal> hotspots, int limit = Listed)
    {
        var shown = hotspots.Take(limit).Select(Say).ToList();
        var more = hotspots.Count - shown.Count;

        if (more > 0)
        {
            return string.Join(", ", shown) + $", and {more} more";
        }

        return shown.Count < 2
            ? shown[0]
            : string.Join(", ", shown[..^1]) + " and " + shown[^1];
    }

    private static string Say(BodySignal signal) => $"{signal.Count} {signal.Type}";
}
