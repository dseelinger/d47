using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace D47.Core.Knowledge;

/// <summary>What the queue last did, for the panel.</summary>
public sealed record PlanWriteStatus(
    DateTimeOffset? LastSentAt,
    string? LastFailure,
    DateTimeOffset? LastFailureAt,
    bool KeyRefused);

/// <summary>
/// Sends <see cref="PlanWrite"/>s to the planner, one request in flight per project, through
/// <see cref="Dispatch"/>. <see cref="Add"/> and <see cref="Poll"/> are for the tick thread and never wait on
/// the writer. Nothing is written to disk.
/// </summary>
public sealed class PlanWriteQueue
{
    public static readonly TimeSpan HoldFor = TimeSpan.FromMinutes(10);

    private static readonly TimeSpan[] RetryAfter =
        [TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(15)];

    private sealed class Item(PlanWrite write, string commander, DateTimeOffset addedAt)
    {
        public PlanWrite Write { get; } = write;

        public string Commander { get; } = commander;

        public DateTimeOffset AddedAt { get; } = addedAt;

        public int Attempts { get; set; }

        public DateTimeOffset NotBefore { get; set; }

        public bool Matched { get; set; }

        public bool Sending { get; set; }

        public bool IsContribution => Write is ContributionWrite;
    }

    private sealed class Project
    {
        public List<Item> Items { get; } = [];

        public bool InFlight { get; set; }

        public IReadOnlyDictionary<string, int>? LastSent { get; set; }
    }

    private sealed record Finished(long MarketId, Item Item, PlanWriteResult Result);

    private readonly IColonisationPlanWriter _writer;
    private readonly ColonisationPlans _plans;
    private readonly ILogger<PlanWriteQueue> _logger;

    private readonly ConcurrentQueue<Finished> _finished = new();
    private readonly Dictionary<long, Project> _projects = [];
    private readonly HashSet<long> _stopped = [];
    private bool _keyRefused;
    private PlanWriteStatus _status = new(null, null, null, false);

    public PlanWriteQueue(IColonisationPlanWriter writer, ColonisationPlans plans, ILogger<PlanWriteQueue> logger)
    {
        _writer = writer;
        _plans = plans;
        _logger = logger;
    }

    /// <summary>Starts a send off the tick thread; must return without waiting for it.</summary>
    public Action<Func<Task>> Dispatch { get; set; } = work => _ = Task.Run(work);

    public PlanWriteStatus Status => _status;

    /// <summary>Queues writes. A snapshot or completion replaces the project's pending one.</summary>
    public void Add(IReadOnlyList<PlanWrite> writes, string commander, DateTimeOffset now)
    {
        if (_keyRefused)
        {
            return;
        }

        foreach (var write in writes)
        {
            if (_stopped.Contains(write.MarketId))
            {
                continue;
            }

            if (!_projects.TryGetValue(write.MarketId, out var project))
            {
                project = new Project();
                _projects[write.MarketId] = project;
            }

            var item = new Item(write, commander, now) { NotBefore = now };

            if (!item.IsContribution)
            {
                var replaced = project.Items.FindIndex(held => !held.IsContribution && !held.Sending);

                if (replaced >= 0)
                {
                    item.Attempts = project.Items[replaced].Attempts;
                    item.NotBefore = project.Items[replaced].NotBefore;
                    item.Matched = project.Items[replaced].Matched;
                    project.Items.RemoveAt(replaced);
                }
            }

            project.Items.Add(item);
        }

        Pump(now);
    }

    /// <summary>Starts the sends that are due and drains the finished ones. Never blocks.</summary>
    public void Poll(DateTimeOffset now)
    {
        while (_finished.TryDequeue(out var done))
        {
            Finish(done, now);
        }

        Pump(now);
    }

    /// <summary>Clears a key refusal and every dropped project, for a new key.</summary>
    public void Reset()
    {
        _keyRefused = false;
        _stopped.Clear();
        _status = _status with { KeyRefused = false };
    }

    private void Pump(DateTimeOffset now)
    {
        if (_keyRefused)
        {
            return;
        }

        IReadOnlyDictionary<string, SystemPlan>? plans = null;

        foreach (var (marketId, project) in _projects)
        {
            if (project.InFlight)
            {
                continue;
            }

            plans ??= _plans.Snapshot;

            for (var index = 0; index < project.Items.Count; index++)
            {
                var item = project.Items[index];
                var site = SiteFor(plans, item.Write);

                if (site is null)
                {
                    if (!item.Matched && now - item.AddedAt >= HoldFor)
                    {
                        _logger.LogDebug(
                            "Dropped a write for market {MarketId}: no planner project after {Held}",
                            marketId,
                            HoldFor);
                        project.Items.RemoveAt(index--);
                    }
                    else
                    {
                        _plans.Want(item.Write.StarSystem, now);
                    }

                    continue;
                }

                if (item.NotBefore > now)
                {
                    continue;
                }

                if (Redundant(project, item, site))
                {
                    project.Items.RemoveAt(index--);
                    continue;
                }

                item.Matched = true;
                item.Sending = true;
                project.InFlight = true;
                var buildId = site.BuildId!;
                Dispatch(() => SendAsync(marketId, item, buildId));
                break;
            }
        }
    }

    private static PlannedSite? SiteFor(IReadOnlyDictionary<string, SystemPlan> plans, PlanWrite write) =>
        plans.TryGetValue(write.StarSystem, out var plan)
            ? plan.Sites.FirstOrDefault(site => site.MarketId == write.MarketId && !string.IsNullOrEmpty(site.BuildId))
            : null;

    private static bool Redundant(Project project, Item item, PlannedSite site) => item.Write switch
    {
        NeedsWrite needs => SameNeeds(needs.Remaining, project.LastSent ?? site.Needed),
        CompleteWrite => site.Status == PlanStatus.Complete,
        _ => false,
    };

    private static bool SameNeeds(IReadOnlyDictionary<string, int> left, IReadOnlyDictionary<string, int> right)
    {
        var a = left.Where(pair => pair.Value > 0).ToDictionary(pair => pair.Key, pair => pair.Value);
        var b = right.Where(pair => pair.Value > 0).ToDictionary(pair => pair.Key, pair => pair.Value);
        return a.Count == b.Count && a.All(pair => b.TryGetValue(pair.Key, out var other) && other == pair.Value);
    }

    private async Task SendAsync(long marketId, Item item, string buildId)
    {
        PlanWriteResult result;

        try
        {
            result = item.Write switch
            {
                NeedsWrite needs => await _writer
                    .NeedsAsync(buildId, needs.Remaining, needs.MaxNeed, CancellationToken.None)
                    .ConfigureAwait(false),
                ContributionWrite contribution => await _writer
                    .ContributeAsync(buildId, item.Commander, contribution.Delivered, CancellationToken.None)
                    .ConfigureAwait(false),
                _ => await _writer.CompleteAsync(buildId, CancellationToken.None).ConfigureAwait(false),
            };
        }
        catch (Exception ex)
        {
            result = new PlanWriteResult(PlanWriteOutcome.Failed, ex.GetType().Name);
        }

        _finished.Enqueue(new Finished(marketId, item, result));
    }

    private void Finish(Finished done, DateTimeOffset now)
    {
        if (!_projects.TryGetValue(done.MarketId, out var project))
        {
            return;
        }

        project.InFlight = false;
        done.Item.Sending = false;
        project.Items.Remove(done.Item);

        switch (done.Result.Outcome)
        {
            case PlanWriteOutcome.Sent:
                if (done.Item.Write is NeedsWrite needs)
                {
                    project.LastSent = needs.Remaining;
                }

                _status = _status with { LastSentAt = now };
                break;

            case PlanWriteOutcome.NotOnPlanner:
                _stopped.Add(done.MarketId);
                project.Items.Clear();
                Drop(done, now, "the site is not on the planner");
                break;

            case PlanWriteOutcome.KeyRefused:
                _keyRefused = true;

                foreach (var held in _projects.Values)
                {
                    held.Items.Clear();
                }

                _status = _status with { KeyRefused = true };
                Drop(done, now, "the planner refused the key");
                break;

            default:
                Failed(done, project, now);
                break;
        }
    }

    private void Failed(Finished done, Project project, DateTimeOffset now)
    {
        var item = done.Item;
        var reason = done.Result.Message ?? "the write failed";

        if (item.IsContribution
            || project.Items.Any(held => !held.IsContribution)
            || item.Attempts >= RetryAfter.Length)
        {
            Drop(done, now, reason);
            return;
        }

        item.NotBefore = now + RetryAfter[item.Attempts++];
        project.Items.Add(item);
        _status = _status with { LastFailure = reason, LastFailureAt = now };
    }

    private void Drop(Finished done, DateTimeOffset now, string reason)
    {
        _logger.LogInformation("Planner write for market {MarketId} dropped: {Reason}", done.MarketId, reason);
        _status = _status with { LastFailure = reason, LastFailureAt = now };
    }
}
