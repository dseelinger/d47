using D47.Core.Conversation;

namespace D47.Core.Ships;

/// <summary>Where a proposed change stands.</summary>
public enum ChangeDecision
{
    Undecided,
    Accepted,
    Rejected,
}

/// <summary>What became of an accept.</summary>
public enum AcceptOutcome
{
    /// <summary>The slot's plan was written.</summary>
    Written,

    /// <summary>The slot's plan changed after the proposal was made; nothing was written.</summary>
    Stale,

    /// <summary>The change was already accepted or rejected.</summary>
    Decided,

    /// <summary>No such build, round or change.</summary>
    Missing,
}

/// <summary>
/// One remark about a build and what came back. <see cref="Advice"/> is null while the reply is pending;
/// <see cref="Decisions"/> runs parallel to the advice's changes.
/// </summary>
public sealed record TalkRound(
    int Id,
    string Remark,
    InputSource Source,
    DateTimeOffset At,
    BuildAdvice? Advice,
    DateTimeOffset? Answered,
    IReadOnlyList<ChangeDecision> Decisions)
{
    public bool IsWorking => Advice is null;

    public bool Failed => Advice is { Succeeded: false };
}

/// <summary>
/// The conversation about each build's plan, held in memory per build until cleared. A proposal reaches the plan
/// only through <see cref="Accept"/>, one slot at a time, and only while that slot's plan is still the one the
/// proposal was made against.
/// </summary>
public sealed class BuildTalk(
    ShipPlanService ships,
    Func<ShipBuild, IReadOnlyList<BuildRemark>, string, CancellationToken, Task<BuildAdvice>> advise,
    Func<DateTimeOffset> now)
{
    private readonly Lock _gate = new();
    private readonly Dictionary<string, List<TalkRound>> _rounds = new(StringComparer.Ordinal);
    private int _next;

    /// <summary>Raised on any thread whenever an exchange or a decision changes.</summary>
    public event Action? Changed;

    /// <summary>How long one remark may wait on the model before it fails.</summary>
    public TimeSpan Limit { get; init; } = TimeSpan.FromSeconds(120);

    /// <summary>The build whose page is open, or null. The spoken route talks about this one first.</summary>
    public string? Open { get; set; }

    /// <summary>How the current turn reached d47, read when a remark arrives through the tool.</summary>
    public Func<InputSource> TurnSource { get; init; } = () => InputSource.Spoken;

    /// <summary>The build a spoken remark is about: the one open on the page, else the one being flown.</summary>
    public ShipBuild? Target() =>
        Open is { } open && ships.Store.Find(open) is { } build
            ? build
            : ShipPlanService.Which(ships, named: null);

    /// <summary>The hull symbol of a build, or null where it is gone.</summary>
    public string? Hull(string buildId) => ships.Store.Find(buildId)?.Hull;

    public IReadOnlyList<TalkRound> Exchange(string buildId)
    {
        lock (_gate)
        {
            return _rounds.TryGetValue(buildId, out var rounds) ? [.. rounds] : [];
        }
    }

    public bool IsWorking(string buildId) => Exchange(buildId).Any(round => round.IsWorking);

    /// <summary>
    /// Asks about one build and keeps the round. Null when a reply on that build is already pending or the build
    /// is gone.
    /// </summary>
    public async Task<BuildAdvice?> AskAsync(
        string buildId,
        string remark,
        InputSource source,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(remark);

        TalkRound round;

        lock (_gate)
        {
            if (!_rounds.TryGetValue(buildId, out var rounds))
            {
                rounds = [];
                _rounds[buildId] = rounds;
            }

            if (rounds.Any(existing => existing.IsWorking))
            {
                return null;
            }

            round = new TalkRound(++_next, remark.Trim(), source, now(), null, null, []);
            rounds.Add(round);
        }

        Changed?.Invoke();

        return await RunAsync(buildId, round, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Asks a failed round's remark again, in its place.</summary>
    public async Task<BuildAdvice?> RetryAsync(string buildId, int roundId, CancellationToken cancellationToken)
    {
        TalkRound round;

        lock (_gate)
        {
            if (!_rounds.TryGetValue(buildId, out var rounds)
                || rounds.Any(existing => existing.IsWorking)
                || rounds.FindIndex(existing => existing.Id == roundId) is not (>= 0 and var at)
                || !rounds[at].Failed)
            {
                return null;
            }

            round = rounds[at] with { Advice = null, Answered = null, Decisions = [] };
            rounds[at] = round;
        }

        Changed?.Invoke();

        return await RunAsync(buildId, round, cancellationToken).ConfigureAwait(false);
    }

    private async Task<BuildAdvice?> RunAsync(string buildId, TalkRound round, CancellationToken cancellationToken)
    {
        if (ships.Store.Find(buildId) is not { } build)
        {
            Remove(buildId, round.Id);
            return null;
        }

        var before = Exchange(buildId)
            .TakeWhile(earlier => earlier.Id != round.Id)
            .Where(earlier => earlier.Advice is { Succeeded: true })
            .Select(earlier => new BuildRemark(earlier.Remark, earlier.Advice!.Reply))
            .ToList();

        BuildAdvice advice;
        using var limit = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        limit.CancelAfter(Limit);

        try
        {
            advice = await advise(build, before, round.Remark, limit.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            Remove(buildId, round.Id);
            throw;
        }
        catch (OperationCanceledException) when (limit.IsCancellationRequested)
        {
            advice = new BuildAdvice(BuildRemarkKind.Question, null, [], null, [], "The model took too long to answer.", "timeout");
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            advice = new BuildAdvice(
                BuildRemarkKind.Question,
                null,
                [],
                null,
                [],
                "The model did not answer.",
                exception.GetType().Name);
        }

        Replace(buildId, round.Id, current => current with
        {
            Advice = advice,
            Answered = now(),
            Decisions = [.. advice.Changes.Select(_ => ChangeDecision.Undecided)],
        });

        return advice;
    }

    /// <summary>Whether the slot's plan has moved since the proposal was made against it.</summary>
    public bool IsStale(string buildId, SlotChange change) =>
        ships.Store.Find(buildId) is not { } build || build.For(change.Slot) != change.Before;

    /// <summary>Writes one proposed change to the plan, if its slot still holds the plan it was proposed against.</summary>
    public AcceptOutcome Accept(string buildId, int roundId, int change)
    {
        var outcome = Decide(buildId, roundId, change, accept: true);

        if (outcome != AcceptOutcome.Missing)
        {
            Changed?.Invoke();
        }

        return outcome;
    }

    /// <summary>Rejects one proposed change. The plan is not touched.</summary>
    public bool Reject(string buildId, int roundId, int change)
    {
        var outcome = Decide(buildId, roundId, change, accept: false);

        if (outcome == AcceptOutcome.Written)
        {
            Changed?.Invoke();
        }

        return outcome == AcceptOutcome.Written;
    }

    /// <summary>Accepts every undecided change whose slot has not moved; how many were written.</summary>
    public int AcceptAll(string buildId, int roundId) => All(buildId, roundId, accept: true);

    /// <summary>Rejects every undecided change; how many there were.</summary>
    public int RejectAll(string buildId, int roundId) => All(buildId, roundId, accept: false);

    /// <summary>Forgets a build's exchange. Refused while a reply is pending.</summary>
    public bool Clear(string buildId)
    {
        lock (_gate)
        {
            if (!_rounds.TryGetValue(buildId, out var rounds) || rounds.Any(round => round.IsWorking))
            {
                return false;
            }

            _rounds.Remove(buildId);
        }

        Changed?.Invoke();
        return true;
    }

    /// <summary>What the Commander hears when the remark arrived by voice.</summary>
    public static string Spoken(BuildAdvice advice)
    {
        var reply = advice.Reply ?? "I have nothing to add about that build.";

        return advice.Changes.Count is > 0 and <= ShipPlanAdvisor.MostModulesNamed
            ? $"{reply} Accept or reject each change on the ship's page."
            : reply;
    }

    private int All(string buildId, int roundId, bool accept)
    {
        var count = Exchange(buildId).FirstOrDefault(round => round.Id == roundId)?.Decisions.Count ?? 0;
        var done = 0;

        for (var index = 0; index < count; index++)
        {
            if (Decide(buildId, roundId, index, accept) == AcceptOutcome.Written)
            {
                done++;
            }
        }

        if (done > 0)
        {
            Changed?.Invoke();
        }

        return done;
    }

    private AcceptOutcome Decide(string buildId, int roundId, int index, bool accept)
    {
        lock (_gate)
        {
            if (!_rounds.TryGetValue(buildId, out var rounds)
                || rounds.FindIndex(round => round.Id == roundId) is not (>= 0 and var at)
                || rounds[at].Advice is not { } advice
                || index < 0
                || index >= advice.Changes.Count)
            {
                return AcceptOutcome.Missing;
            }

            if (rounds[at].Decisions[index] != ChangeDecision.Undecided)
            {
                return AcceptOutcome.Decided;
            }

            var change = advice.Changes[index];

            if (accept)
            {
                if (IsStale(buildId, change))
                {
                    return AcceptOutcome.Stale;
                }

                if (!ships.Plan(buildId, change.After))
                {
                    return AcceptOutcome.Missing;
                }
            }

            var decisions = rounds[at].Decisions.ToArray();
            decisions[index] = accept ? ChangeDecision.Accepted : ChangeDecision.Rejected;
            rounds[at] = rounds[at] with { Decisions = decisions };

            return AcceptOutcome.Written;
        }
    }

    private void Remove(string buildId, int roundId)
    {
        lock (_gate)
        {
            if (_rounds.TryGetValue(buildId, out var rounds))
            {
                rounds.RemoveAll(round => round.Id == roundId);
            }
        }

        Changed?.Invoke();
    }

    private void Replace(string buildId, int roundId, Func<TalkRound, TalkRound> change)
    {
        lock (_gate)
        {
            if (_rounds.TryGetValue(buildId, out var rounds)
                && rounds.FindIndex(round => round.Id == roundId) is >= 0 and var at)
            {
                rounds[at] = change(rounds[at]);
            }
        }

        Changed?.Invoke();
    }
}
