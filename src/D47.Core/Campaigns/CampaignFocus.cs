using D47.Core.Journal;

namespace D47.Core.Campaigns;

/// <summary>The front d47 proposes, held in memory and never written.</summary>
public sealed record FrontProposal(string FrontierId, string Faction, Front Front);

/// <summary>
/// Holds the Commander to one front. Proposes the top front while none is chosen, records the BGS ticks a chosen front
/// spans and, when fronts computed after it was chosen no longer hold it, writes its chronicle entry and clears the
/// choice. <see cref="Poll"/> runs on the tick thread after <see cref="CampaignFrontsWatch"/>; the actions run on any thread.
/// </summary>
public sealed class CampaignFocus(
    CampaignStore store,
    Func<CampaignFrontsSnapshot?> snapshot,
    Func<CommanderGameState?> gameState,
    Func<DateTimeOffset?> lastTick,
    Func<DateTimeOffset> clock)
{
    private readonly Lock _gate = new();

    private volatile FrontProposal? _proposal;

    private (FrontKind Kind, string System)? _offered;

    private DateTimeOffset? _tickSeen;

    /// <summary>The front proposed, or null while one is chosen or there is none to propose. Any thread.</summary>
    public FrontProposal? Proposal => _proposal;

    /// <summary>The front being worked, from the held fronts; null when none is chosen or it is not among them.</summary>
    public Front? Chosen()
    {
        var state = gameState();
        var held = snapshot();

        return state?.Identity.FrontierId is { Length: > 0 } id
            && store.For(id).Chosen is { } chosen
            && held is not null
            && string.Equals(held.FrontierId, id, StringComparison.Ordinal)
            ? Find(held.Fronts, chosen.Kind, chosen.System)
            : null;
    }

    /// <summary>The next action for the front being worked; null when none is chosen or it is not among the held fronts.</summary>
    public CampaignNextAction? NextAction()
    {
        var state = gameState();
        var held = snapshot();

        return state is not null && Chosen() is { } front && held is not null
            ? CampaignNextAction.For(front, held.Faction, state.Combat, state.BgsWork, clock())
            : null;
    }

    public void Poll()
    {
        lock (_gate)
        {
            var state = gameState();
            var held = snapshot();

            if (state?.Identity.FrontierId is not { Length: > 0 } id || held is null
                || !string.Equals(held.FrontierId, id, StringComparison.Ordinal))
            {
                _proposal = null;
                _offered = null;
                return;
            }

            if (store.For(id).Chosen is { } chosen)
            {
                _proposal = null;
                _offered = null;
                Follow(id, held, chosen, state);
                return;
            }

            Propose(id, held, forward: false);
        }
    }

    /// <summary>Takes the proposal. Null when none is held.</summary>
    public Front? Accept()
    {
        lock (_gate)
        {
            return _proposal is { } proposal && Choose(proposal.FrontierId, proposal.Front.Kind, proposal.Front.System)
                ? proposal.Front
                : null;
        }
    }

    /// <summary>Chooses the front of <paramref name="kind"/> (any kind when null) in <paramref name="system"/>. Null when the faction has none there.</summary>
    public Front? Take(FrontKind? kind, string system)
    {
        lock (_gate)
        {
            var state = gameState();
            var held = snapshot();

            if (state?.Identity.FrontierId is not { Length: > 0 } id || held is null
                || !string.Equals(held.FrontierId, id, StringComparison.Ordinal))
            {
                return null;
            }

            var front = held.Fronts.FirstOrDefault(candidate =>
                (kind is null || candidate.Kind == kind) && string.Equals(candidate.System, system.Trim(), StringComparison.OrdinalIgnoreCase));

            return front is not null && Choose(id, front.Kind, front.System) ? front : null;
        }
    }

    /// <summary>Proposes the front after the one proposed, wrapping to the top. Null when one is chosen or there are none.</summary>
    public FrontProposal? Next()
    {
        lock (_gate)
        {
            var state = gameState();
            var held = snapshot();

            if (state?.Identity.FrontierId is not { Length: > 0 } id || held is null
                || !string.Equals(held.FrontierId, id, StringComparison.Ordinal) || store.For(id).Chosen is not null)
            {
                return null;
            }

            Propose(id, held, forward: true);

            return _proposal;
        }
    }

    /// <summary>Drops the chosen front with no chronicle entry. False when none was chosen.</summary>
    public bool Drop()
    {
        lock (_gate)
        {
            return gameState()?.Identity.FrontierId is { Length: > 0 } id && store.Drop(id);
        }
    }

    private bool Choose(string id, FrontKind kind, string system)
    {
        if (!store.Choose(id, kind, system, clock()))
        {
            return false;
        }

        _proposal = null;
        _offered = null;

        return true;
    }

    private void Propose(string id, CampaignFrontsSnapshot held, bool forward)
    {
        var fronts = held.Fronts;

        if (fronts.Count == 0)
        {
            _proposal = null;
            _offered = null;
            return;
        }

        var at = _offered is { } offered ? IndexOf(fronts, offered.Kind, offered.System) : -1;
        var index = forward ? (at + 1) % fronts.Count : Math.Max(at, 0);

        _offered = (fronts[index].Kind, fronts[index].System);
        _proposal = new FrontProposal(id, held.Faction, fronts[index]);
    }

    private void Follow(string id, CampaignFrontsSnapshot held, ChosenFront chosen, CommanderGameState state)
    {
        if (held.Report is null || held.ComputedAt is not { } computed)
        {
            return;
        }

        if (lastTick() is { } tick && tick != _tickSeen)
        {
            _tickSeen = tick;

            if (!chosen.Ticks.Contains(tick))
            {
                store.NoteTick(id, chosen.Kind, chosen.System, tick);
            }
        }

        if (computed > chosen.Since && Resolved(held.Fronts, chosen))
        {
            store.Resolve(id, chosen.Kind, chosen.System, Entry(held.Faction, chosen, state));
        }
    }

    /// <summary>A front is resolved when it is gone from the fronts; a thin margin or coup threat hidden by a conflict is not.</summary>
    private static bool Resolved(IReadOnlyList<Front> fronts, ChosenFront chosen) =>
        Find(fronts, chosen.Kind, chosen.System) is null
        && (chosen.Kind switch
        {
            FrontKind.ThinMargin => Find(fronts, FrontKind.Conflict, chosen.System) is null && Find(fronts, FrontKind.Coup, chosen.System) is null,
            FrontKind.Coup => Find(fronts, FrontKind.Conflict, chosen.System) is null,
            _ => true,
        });

    private static ChronicleEntry Entry(string faction, ChosenFront chosen, CommanderGameState state)
    {
        var ended = chosen.Kind == FrontKind.Conflict && state.Standings.Latest(chosen.System) is { } reading
            ? reading.Conflicts.FirstOrDefault(conflict => conflict.Ended && Involves(conflict, faction))
            : null;

        var outcome = ended is null
            ? "Resolved"
            : ended.Winner is null ? "Ended" : string.Equals(ended.Winner, faction, StringComparison.OrdinalIgnoreCase) ? "Won" : "Lost";

        var opponent = ended is null
            ? null
            : string.Equals(ended.Faction1.Name, faction, StringComparison.OrdinalIgnoreCase) ? ended.Faction2.Name : ended.Faction1.Name;

        var work = state.BgsWork.Entries
            .Where(entry => entry.At >= chosen.Since
                && string.Equals(entry.Faction, faction, StringComparison.OrdinalIgnoreCase)
                && string.Equals(state.BgsWork.NameOf(entry.SystemAddress), chosen.System, StringComparison.OrdinalIgnoreCase))
            .GroupBy(entry => entry.Kind)
            .Select(group => new ChronicleWork(group.Key, group.Sum(entry => entry.Amount)))
            .OrderBy(entry => entry.Kind)
            .ToList();

        return new ChronicleEntry(CampaignFronts.Describe(chosen.Kind), chosen.System, opponent, outcome, chosen.Ticks, work);
    }

    private static bool Involves(SystemConflict conflict, string faction) =>
        string.Equals(conflict.Faction1.Name, faction, StringComparison.OrdinalIgnoreCase)
        || string.Equals(conflict.Faction2.Name, faction, StringComparison.OrdinalIgnoreCase);

    private static Front? Find(IReadOnlyList<Front> fronts, FrontKind kind, string system) =>
        fronts.FirstOrDefault(front => front.Kind == kind && string.Equals(front.System, system, StringComparison.OrdinalIgnoreCase));

    private static int IndexOf(IReadOnlyList<Front> fronts, FrontKind kind, string system)
    {
        for (var index = 0; index < fronts.Count; index++)
        {
            if (fronts[index].Kind == kind && string.Equals(fronts[index].System, system, StringComparison.OrdinalIgnoreCase))
            {
                return index;
            }
        }

        return -1;
    }
}
