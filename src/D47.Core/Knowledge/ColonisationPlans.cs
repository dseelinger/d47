using D47.Core.Journal;
using Microsoft.Extensions.Logging;

namespace D47.Core.Knowledge;

/// <summary>
/// Holds the planner's plan for each colonisation system this session, apart from the journal state. Fetches go
/// through <see cref="Dispatch"/>; nothing here waits on the service from the tick, and nothing is written to disk.
/// </summary>
public sealed class ColonisationPlans
{
    /// <summary>How old a held plan may be before asking for it fetches again, and how long a failure waits.</summary>
    public static readonly TimeSpan RefreshAge = TimeSpan.FromMinutes(10);

    /// <summary>How long after a fetch a depot seen again does not fetch its system.</summary>
    public static readonly TimeSpan SeenAgainAge = TimeSpan.FromMinutes(2);

    private static readonly IReadOnlyDictionary<string, SystemPlan> None =
        new Dictionary<string, SystemPlan>(StringComparer.OrdinalIgnoreCase);

    private readonly IColonisationPlanService? _service;
    private readonly Func<bool> _enabled;
    private readonly Func<CommanderGameState?> _state;
    private readonly ILogger<ColonisationPlans> _logger;

    private readonly Lock _lock = new();
    private readonly Dictionary<string, SystemPlan> _held = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, Task<SystemPlan?>> _inFlight = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, DateTimeOffset> _attemptedAt = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, DateTimeOffset> _failedAt = new(StringComparer.OrdinalIgnoreCase);
    private int _generation;

    // Tick thread only.
    private readonly HashSet<string> _swept = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<long, DateTimeOffset> _seenAt = [];
    private string? _sweptFor;
    private bool _wasEnabled;

    public ColonisationPlans(
        IColonisationPlanService? service,
        Func<bool> enabled,
        Func<CommanderGameState?> state,
        ILogger<ColonisationPlans> logger)
    {
        _service = service;
        _enabled = enabled;
        _state = state;
        _logger = logger;
    }

    /// <summary>Starts a fetch off the calling thread; must return without waiting for it.</summary>
    public Action<Func<Task>> Dispatch { get; set; } = work => _ = Task.Run(work);

    /// <summary>The latest plan held for each system; never fetches.</summary>
    public IReadOnlyDictionary<string, SystemPlan> Snapshot
    {
        get
        {
            if (!On)
            {
                return None;
            }

            lock (_lock)
            {
                return new Dictionary<string, SystemPlan>(_held, StringComparer.OrdinalIgnoreCase);
            }
        }
    }

    private bool On => _service is not null && _enabled();

    /// <summary>Asks for a system's plan if what is held is older than the refresh age; returns at once.</summary>
    public void Want(string systemName, DateTimeOffset now)
    {
        if (On && !string.IsNullOrWhiteSpace(systemName))
        {
            _ = Start(systemName.Trim(), now, RefreshAge);
        }
    }

    /// <summary>For tool handlers on the pool: the held plan if fresh, otherwise a fetch.</summary>
    /// <remarks>A failed fetch answers with the plan held before it, or null.</remarks>
    public async Task<SystemPlan?> GetAsync(string systemName, DateTimeOffset now, CancellationToken cancellationToken)
    {
        if (!On || string.IsNullOrWhiteSpace(systemName))
        {
            return null;
        }

        var system = systemName.Trim();

        if (Start(system, now, RefreshAge) is { } fetch)
        {
            return await fetch.WaitAsync(cancellationToken).ConfigureAwait(false);
        }

        lock (_lock)
        {
            return _held.GetValueOrDefault(system);
        }
    }

    /// <summary>Starts the fetches that are due. Never blocks.</summary>
    public void Poll(DateTimeOffset now)
    {
        var enabled = On;

        if (enabled != _wasEnabled)
        {
            Reset();
        }

        _wasEnabled = enabled;

        if (!enabled || _state() is not { } state)
        {
            return;
        }

        if (state.Identity.FrontierId != _sweptFor)
        {
            _sweptFor = state.Identity.FrontierId;
            _swept.Clear();
            _seenAt.Clear();
        }

        var colonisation = state.Colonisation;

        var wanted = colonisation.Claims.Select(claim => claim.StarSystem)
            .Concat(colonisation.Active.Select(site => site.StarSystem))
            .OfType<string>()
            .Where(system => system.Length > 0);

        foreach (var system in wanted)
        {
            if (_swept.Add(system) || Failed(system))
            {
                _ = Start(system, now, TimeSpan.Zero);
            }
        }

        foreach (var site in colonisation.All)
        {
            var movedOn = _seenAt.TryGetValue(site.MarketId, out var seen) && site.SeenAt > seen;
            _seenAt[site.MarketId] = site.SeenAt;

            if (movedOn && site.StarSystem is { Length: > 0 } system)
            {
                _ = Start(system, now, SeenAgainAge);
            }
        }
    }

    /// <summary>Clears everything held when the switch changes, so turning it on fetches every system again.</summary>
    private void Reset()
    {
        _sweptFor = null;
        _swept.Clear();
        _seenAt.Clear();

        lock (_lock)
        {
            _generation++;
            _held.Clear();
            _inFlight.Clear();
            _attemptedAt.Clear();
            _failedAt.Clear();
        }
    }

    private bool Failed(string system)
    {
        lock (_lock)
        {
            return _failedAt.ContainsKey(system);
        }
    }

    /// <summary>
    /// The fetch for <paramref name="system"/>: the one already running, a new one when the last attempt is older than
    /// <paramref name="age"/> and no failure is within <see cref="RefreshAge"/>, or null when none is due.
    /// </summary>
    private Task<SystemPlan?>? Start(string system, DateTimeOffset now, TimeSpan age)
    {
        TaskCompletionSource<SystemPlan?> answer;
        int generation;

        lock (_lock)
        {
            if (_inFlight.TryGetValue(system, out var running))
            {
                return running;
            }

            if ((_attemptedAt.TryGetValue(system, out var attempted) && now - attempted < age)
                || (_failedAt.TryGetValue(system, out var failed) && now - failed < RefreshAge))
            {
                return null;
            }

            answer = new TaskCompletionSource<SystemPlan?>(TaskCreationOptions.RunContinuationsAsynchronously);
            generation = _generation;
            _attemptedAt[system] = now;
            _inFlight[system] = answer.Task;
        }

        Dispatch(() => Fetch(system, now, generation, answer));
        return answer.Task;
    }

    private async Task Fetch(string system, DateTimeOffset startedAt, int generation, TaskCompletionSource<SystemPlan?> answer)
    {
        SystemPlan? plan = null;
        var failed = false;

        try
        {
            plan = await _service!.SystemAsync(system, CancellationToken.None).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            failed = true;
            _logger.LogInformation(
                "Colonisation plan fetch for {System} failed: {Reason}",
                system,
                ex is ColonisationPlanUnavailableException ? ex.Message : ex.GetType().Name);
        }

        SystemPlan? result;

        lock (_lock)
        {
            if (generation != _generation)
            {
                result = null;
            }
            else
            {
                _inFlight.Remove(system);

                if (failed)
                {
                    _failedAt[system] = startedAt;
                }
                else
                {
                    _failedAt.Remove(system);

                    if (plan is null)
                    {
                        _held.Remove(system);
                    }
                    else
                    {
                        _held[system] = plan;
                    }
                }

                result = _held.GetValueOrDefault(system);
            }
        }

        answer.SetResult(result);
    }
}
