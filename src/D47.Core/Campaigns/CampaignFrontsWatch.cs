using System.Collections.Concurrent;
using D47.Core.Journal;
using D47.Core.Knowledge;
using Microsoft.Extensions.Logging;

namespace D47.Core.Campaigns;

/// <summary>
/// The campaign faction's last galaxy report and the fronts computed from it, for one Commander. A failed fetch
/// keeps the report and fronts from before it.
/// </summary>
/// <param name="ReportFetchedAt">When the report was fetched, or null before the first fetch succeeds.</param>
/// <param name="ComputedAt">When <paramref name="Fronts"/> were last computed, or null when they never were.</param>
/// <param name="Failure">Why the last fetch failed, or null when it did not.</param>
public sealed record CampaignFrontsSnapshot(
    string FrontierId,
    string Faction,
    FactionReport? Report,
    DateTimeOffset? ReportFetchedAt,
    IReadOnlyList<Front> Fronts,
    DateTimeOffset? ComputedAt,
    string? Failure,
    DateTimeOffset? FailedAt);

/// <summary>
/// Keeps <see cref="Snapshot"/> current: fetches the campaign faction's report once its faction is known, again after
/// each new BGS tick, again when the faction or Commander changes, and again <see cref="RetryAfter"/> after a failure.
/// Recomputes the fronts when the Commander's standings or the campaign's intents change. The fetch runs on the pool; everything else
/// on the tick thread.
/// </summary>
public sealed class CampaignFrontsWatch
{
    public static readonly TimeSpan RetryAfter = TimeSpan.FromMinutes(30);

    private readonly Func<string, bool, CancellationToken, Task<FactionReport>> _fetch;
    private readonly Func<CommanderGameState?> _gameState;
    private readonly CampaignStore _campaigns;
    private readonly SquadronFactions? _squadronFactions;
    private readonly Func<DateTimeOffset?> _lastTick;
    private readonly FactionSystemsSeen? _seen;
    private readonly ILogger<CampaignFrontsWatch> _logger;

    private readonly ConcurrentQueue<Answer> _answers = new();

    private volatile CampaignFrontsSnapshot? _snapshot;

    private bool _fetching;
    private DateTimeOffset? _tickFetched;
    private DateTimeOffset? _failedAt;
    private SystemStandings? _standingsUsed;
    private System.Collections.Immutable.ImmutableDictionary<string, SystemIntent>? _intentsUsed;

    /// <param name="fetch">Fetches a faction's report by name; the flag asks for a fresh report rather than a held one.</param>
    public CampaignFrontsWatch(
        Func<string, bool, CancellationToken, Task<FactionReport>> fetch,
        Func<CommanderGameState?> gameState,
        CampaignStore campaigns,
        SquadronFactions? squadronFactions,
        Func<DateTimeOffset?> lastTick,
        FactionSystemsSeen? seen,
        ILogger<CampaignFrontsWatch> logger)
    {
        _fetch = fetch;
        _gameState = gameState;
        _campaigns = campaigns;
        _squadronFactions = squadronFactions;
        _lastTick = lastTick;
        _seen = seen;
        _logger = logger;
    }

    /// <summary>Starts the fetch off the tick thread; must return without waiting for it.</summary>
    public Action<Func<Task>> Dispatch { get; set; } = work => _ = Task.Run(work);

    /// <summary>The current Commander's campaign report and fronts, or null when no campaign faction is known. Any thread.</summary>
    public CampaignFrontsSnapshot? Snapshot => _snapshot;

    /// <summary>Takes any finished fetch, recomputes the fronts when their inputs changed, then starts a fetch when one is due.</summary>
    public void Poll(DateTimeOffset now)
    {
        var state = _gameState();
        var frontierId = state?.Identity.FrontierId;
        var campaign = _campaigns.For(frontierId);
        var faction = frontierId is { Length: > 0 } ? campaign.FactionOr(SquadronFactionResolver.Resolve(state, _squadronFactions)) : null;

        if (frontierId is not { Length: > 0 } || faction is null)
        {
            _snapshot = null;
            Drain();
            return;
        }

        var snapshot = _snapshot;

        if (snapshot is null
            || !string.Equals(snapshot.FrontierId, frontierId, StringComparison.Ordinal)
            || !string.Equals(snapshot.Faction, faction, StringComparison.OrdinalIgnoreCase))
        {
            snapshot = new CampaignFrontsSnapshot(frontierId, faction, null, null, [], null, null, null);
            _standingsUsed = null;
            _intentsUsed = null;
            _tickFetched = null;
            _failedAt = null;
        }

        while (_answers.TryDequeue(out var answer))
        {
            _fetching = false;

            if (!string.Equals(answer.FrontierId, frontierId, StringComparison.Ordinal)
                || !string.Equals(answer.Faction, faction, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (answer.Report is { } report)
            {
                _seen?.Remember(report);
                _failedAt = null;
                _standingsUsed = null;
                snapshot = snapshot with { Report = report, ReportFetchedAt = answer.At, Failure = null, FailedAt = null };
            }
            else
            {
                _failedAt = answer.At;
                snapshot = snapshot with { Failure = answer.Failure, FailedAt = answer.At };
            }
        }

        if (snapshot.Report is { } held
            && (!ReferenceEquals(_standingsUsed, state!.Standings) || !ReferenceEquals(_intentsUsed, campaign.Intents)))
        {
            _standingsUsed = state.Standings;
            _intentsUsed = campaign.Intents;
            snapshot = snapshot with { Fronts = CampaignFronts.Of(held, state.Standings, campaign, now), ComputedAt = now };
        }

        _snapshot = snapshot;

        if (_fetching)
        {
            return;
        }

        var tick = _lastTick();
        var first = snapshot.ReportFetchedAt is null && _failedAt is null;
        var ticked = !first && tick is not null && tick != _tickFetched;
        var retry = _failedAt is { } failed && now - failed >= RetryAfter;

        if (!first && !ticked && !retry)
        {
            return;
        }

        _fetching = true;
        _tickFetched = tick;
        _failedAt = null;
        Dispatch(() => Fetch(frontierId, faction, refresh: !first, now));
    }

    private void Drain()
    {
        while (_answers.TryDequeue(out _))
        {
            _fetching = false;
        }
    }

    private async Task Fetch(string frontierId, string faction, bool refresh, DateTimeOffset at)
    {
        FactionReport? report = null;
        string? failure = null;

        try
        {
            report = await _fetch(faction, refresh, CancellationToken.None).ConfigureAwait(false);

            if (report.Systems.Count == 0)
            {
                report = null;
                failure = $"Galaxy search found no systems for {faction}.";
            }
        }
        catch (GalaxyUnavailableException ex)
        {
            failure = ex.Message;
        }
        catch (Exception ex)
        {
            failure = "The faction report could not be fetched.";
            _logger.LogInformation("Campaign faction report fetch failed: {Reason}", ex.GetType().Name);
        }
        finally
        {
            _answers.Enqueue(new Answer(frontierId, faction, report, failure, at));
        }
    }

    private sealed record Answer(string FrontierId, string Faction, FactionReport? Report, string? Failure, DateTimeOffset At);
}
