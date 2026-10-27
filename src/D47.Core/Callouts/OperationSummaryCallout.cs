using D47.Core.Journal;

namespace D47.Core.Callouts;

/// <summary>One line about an Operation run, spoken when it ends and its merc coins are known (#1795).</summary>
public sealed class OperationSummaryCallout : ICallout
{
    private const int Listed = 3;

    private static readonly TimeSpan CoinsWait = TimeSpan.FromSeconds(60);

    private readonly HashSet<DateTimeOffset> _settled = [];

    public string Id => "operation-summary";

    public IEnumerable<Announcement> Examine(CalloutContext context)
    {
        if (context.State?.Operations.Last is not { ClosedAt: { } closedAt } run || _settled.Contains(run.OpenedAt))
        {
            yield break;
        }

        if (context.IsPriming)
        {
            _settled.Add(run.OpenedAt);
            yield break;
        }

        if (run.MercCoinsEarned is null && context.Now < closedAt + CoinsWait)
        {
            yield break;
        }

        _settled.Add(run.OpenedAt);
        yield return new Announcement($"operation-summary.{run.OpenedAt.Ticks}", Line(run));
    }

    private static string Line(OperationRun run)
    {
        var name = run.Name ?? "the Operation";
        var minutes = (int)(run.Duration ?? TimeSpan.Zero).TotalMinutes;
        var length = minutes == 0 ? "under a minute" : MiningSummaryCallout.Length(minutes);

        string head;

        if (run.Reward is not { } reward)
        {
            head = $"{Capitalised(name)} ended after {length}.";
        }
        else if (reward > 0)
        {
            var paid = new List<string> { Credits(reward) };

            if (run.MercCoinsEarned is > 0 and var coins)
            {
                paid.Add($"{Math.Round(coins, MidpointRounding.AwayFromZero):0} merc coins");
            }

            head = $"{Capitalised(name)} finished in {length}: {string.Join(" and ", paid)}.";
        }
        else
        {
            head = $"Left {name} after {length}, unfinished.";
        }

        var line = run.Kills > 0 ? $"{head} {MiningSummaryCallout.Count(run.Kills, "kill")}." : head;

        return run.Squad.Count == 0 ? line : $"{line} Flown with {Names(run.Squad.Take(Listed).ToList())}.";
    }

    private static string Credits(long amount) => $"{SpokenCredits.Band(amount)} credits";

    private static string Capitalised(string text) => char.ToUpperInvariant(text[0]) + text[1..];

    private static string Names(IReadOnlyList<string> names) =>
        names.Count == 1 ? names[0] : $"{string.Join(", ", names.Take(names.Count - 1))} and {names[^1]}";
}
