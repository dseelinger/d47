using D47.Core.Journal;

namespace D47.Core.Callouts;

/// <summary>A mining run's tonnes, rate and materials, spoken when it closes on docking (#609).</summary>
public sealed class MiningSummaryCallout : ICallout
{
    private const int Listed = 3;

    public string Id => "mining-summary";

    public IEnumerable<Announcement> Examine(CalloutContext context)
    {
        if (context.IsPriming || context.State?.Mining.Last is not { ClosedBy: "Docked" } run || run.TonnesRefined == 0)
        {
            yield break;
        }

        foreach (var journalEvent in context.Events)
        {
            if (journalEvent.Kind == "Docked" && journalEvent.Timestamp == run.ClosedAt)
            {
                yield return new Announcement($"mining-summary.{run.OpenedAt.Ticks}", Line(run));
            }
        }
    }

    private static string Line(MiningRun run)
    {
        var tonnes = run.TonnesRefined;
        var mined = (run.LastRefinedAt ?? run.OpenedAt) - run.OpenedAt;
        var minutes = (int)mined.TotalMinutes;

        var head = minutes == 0
            ? $"Mining run: {Tonnes(tonnes)} in under a minute."
            : $"Mining run: {Tonnes(tonnes)} in {Length(minutes)}, about {(int)Math.Round(tonnes / mined.TotalHours)} an hour.";

        var materials = run.Refined.Values
            .OrderByDescending(material => material.Tonnes)
            .ThenBy(material => material.Name, StringComparer.Ordinal)
            .ToList();

        var shown = materials.Take(Listed).Select(material => $"{material.Tonnes} {material.Name}").ToList();
        var more = materials.Count - shown.Count;
        var line = $"{head} {string.Join(", ", shown)}{(more > 0 ? $", and {more} more" : "")}.";

        var limpets = new List<string>();

        if (run.ProspectorsLaunched > 0)
        {
            limpets.Add(Count(run.ProspectorsLaunched, "prospector"));
        }

        if (run.CollectorsLaunched > 0)
        {
            limpets.Add(Count(run.CollectorsLaunched, "collector"));
        }

        if (limpets.Count > 0)
        {
            line += $" {string.Join(" and ", limpets)}.";
        }

        return run.CoresFound > 0 ? $"{line} {Count(run.CoresFound, "core")}." : line;
    }

    internal static string Tonnes(int count) => count == 1 ? "1 tonne" : $"{count} tonnes";

    private static string Count(int count, string noun) => count == 1 ? $"1 {noun}" : $"{count} {noun}s";

    private static string Length(int minutes)
    {
        var hours = minutes / 60;
        var rest = minutes % 60;

        return (hours, rest) switch
        {
            (0, _) => Count(rest, "minute"),
            (_, 0) => Count(hours, "hour"),
            _ => $"{Count(hours, "hour")} {Count(rest, "minute")}",
        };
    }
}
