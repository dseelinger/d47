using D47.Core.Journal;
using D47.Core.Knowledge;

namespace D47.Core.Goals;

/// <summary>
/// What every arc is worth right now (list.md Phase 34, "Progress is derived, never typed").
/// <para>
/// <b>Three sources and a fixed precedence, which is the whole of item 2.</b> Live journal state
/// answers what is true now and is preferred whenever it has anything to say. The mine answers the
/// two things live state structurally cannot — when an arc started, and what a whole history adds
/// up to. Where neither can say, <see cref="GoalSource.Unknown"/> is the answer and it is drawn as
/// one: <b>an arc d47 cannot currently evaluate reports as of when it last could, rather than
/// resetting to zero on the absence of evidence.</b>
/// </para>
/// <para>
/// Nothing here is stored. Every figure is recomputed from state and counts each time it is asked
/// for, which is the same rule <see cref="Checklists.ChecklistItem"/> states about numbers on a
/// list and for the same reason.
/// </para>
/// </summary>
public static class GoalEvaluator
{
    public static GoalStanding Evaluate(GoalArc arc, CommanderGameState? state, GoalMine? mine)
    {
        ArgumentNullException.ThrowIfNull(arc);

        if (arc.Kind == GoalKind.Authored)
        {
            return new GoalStanding
            {
                Arc = arc,
                Source = GoalSource.Commander,
                Started = arc.Written,
                AsOf = arc.FinishedAt,
                IsDone = arc.Finished,
                Note = arc.Finished ? "done, and you said so" : "yours to call done",
            };
        }

        var mark = mine?.For(arc.Key);

        return arc.Key switch
        {
            _ when GoalCatalogue.CareerOf(arc.Key) is { } career => Rank(arc, career, state, mark),
            GoalCatalogue.Engineers => Engineers(arc, state, mark),
            GoalCatalogue.Ships => Ships(arc, state, mark),
            GoalCatalogue.Systems => Milestone(arc, GoalCatalogue.SystemMilestones, mark),
            GoalCatalogue.Distance => Milestone(arc, GoalCatalogue.DistanceMilestones, mark),
            _ => new GoalStanding { Arc = arc, Source = GoalSource.Unknown, Started = mark?.Started },
        };
    }

    /// <summary>Every built-in arc at once, in catalogue order.</summary>
    public static IReadOnlyList<GoalStanding> All(CommanderGameState? state, GoalMine? mine) =>
        [.. GoalCatalogue.All.Select(arc => Evaluate(arc, state, mine))];

    /// <summary>
    /// A career ladder. <b>Counted, never named</b> — the journal gives an integer and a table of
    /// Frontier's rank words is not something this repo hand-writes. See
    /// <see cref="RankStanding"/>, which carries the whole argument.
    /// </summary>
    private static GoalStanding Rank(GoalArc arc, string career, CommanderGameState? state, GoalMark? mark)
    {
        if (state?.Ranks.For(career) is { } live && state.Ranks.IsKnown)
        {
            return new GoalStanding
            {
                Arc = arc,
                Have = live.Rank,
                Need = RankStanding.Elite,
                Source = GoalSource.Live,
                AsOf = state.Ranks.TakenAt,
                Started = mark?.Started,
                Note = live.Describe(),
                IsDone = live.IsElite,
            };
        }

        // The game has not said this session. The mined figure stands with its date, which is the
        // difference between "I have not looked" and "you are at nothing".
        return mark?.Have is { } mined
            ? new GoalStanding
            {
                Arc = arc,
                Have = mined,
                Need = RankStanding.Elite,
                Source = GoalSource.Mined,
                AsOf = mark.AsOf,
                Started = mark.Started,
                Note = new RankStanding(career, (int)mined).Describe(),
                IsDone = mined >= RankStanding.Elite,
            }
            : new GoalStanding { Arc = arc, Source = GoalSource.Unknown, Started = mark?.Started };
    }

    private static GoalStanding Engineers(GoalArc arc, CommanderGameState? state, GoalMark? mark)
    {
        var total = EngineerDirectory.All.Count;

        if (state?.Engineers is { IsKnown: true } live)
        {
            var unlocked = live.Unlocked.Count;

            return new GoalStanding
            {
                Arc = arc,
                Have = unlocked,
                Need = total,
                Source = GoalSource.Live,
                AsOf = live.TakenAt,
                Started = mark?.Started,
                Note = $"{unlocked} of {total} unlocked, {live.Invited.Count} invited",
                IsDone = unlocked >= total,
            };
        }

        return mark?.Have is { } mined
            ? new GoalStanding
            {
                Arc = arc,
                Have = mined,
                Need = total,
                Source = GoalSource.Mined,
                AsOf = mark.AsOf,
                Started = mark.Started,
                IsDone = mined >= total,
            }
            : new GoalStanding { Arc = arc, Need = total, Source = GoalSource.Unknown, Started = mark?.Started };
    }

    /// <summary>
    /// The collection. <b>Hulls owned now</b>, which is the fleet plus whatever is being flown —
    /// the fleet snapshot is taken at a shipyard and does not list the ship the Commander arrived
    /// in.
    /// </summary>
    private static GoalStanding Ships(GoalArc arc, CommanderGameState? state, GoalMark? mark)
    {
        var total = EliteSpecifications.Ships.Count;

        if (state?.Fleet is { IsKnown: true } fleet)
        {
            var hulls = new HashSet<string>(
                fleet.Ships.Select(ship => ship.Type).Where(type => type.Length > 0),
                StringComparer.OrdinalIgnoreCase);

            if (state.Ship.Type is { Length: > 0 } flying)
            {
                hulls.Add(flying);
            }

            return new GoalStanding
            {
                Arc = arc,
                Have = hulls.Count,
                Need = total,
                Source = GoalSource.Live,
                AsOf = fleet.TakenAt,
                Started = mark?.Started,
                Note = $"{hulls.Count} of {total} hulls owned",
                IsDone = hulls.Count >= total,
            };
        }

        // The mined figure counts every hull ever flown rather than every hull owned, so it is a
        // floor and is only ever reached for when the live fleet has said nothing at all.
        return mark?.Have is { } mined
            ? new GoalStanding
            {
                Arc = arc,
                Have = mined,
                Need = total,
                Source = GoalSource.Mined,
                AsOf = mark.AsOf,
                Started = mark.Started,
                Note = $"{mined} of {total} hulls flown, from your journals",
                IsDone = false,
            }
            : new GoalStanding { Arc = arc, Need = total, Source = GoalSource.Unknown, Started = mark?.Started };
    }

    /// <summary>
    /// A ladder arc. <b>Mined only, and that is structural rather than a shortfall</b>: how many
    /// systems a Commander has ever visited is a fact about every journal they have, and no live
    /// state holds it.
    /// </summary>
    private static GoalStanding Milestone(GoalArc arc, IReadOnlyList<long> ladder, GoalMark? mark)
    {
        if (mark?.Have is not { } have)
        {
            return new GoalStanding { Arc = arc, Source = GoalSource.Unknown, Started = mark?.Started };
        }

        var next = GoalCatalogue.NextMilestone(ladder, have);

        return new GoalStanding
        {
            Arc = arc,
            Have = have,
            Need = next,
            Source = GoalSource.Mined,
            AsOf = mark.AsOf,
            Started = mark.Started,
            Note = next is { } target
                ? $"{have:N0} of {target:N0} {arc.Unit}, next milestone"
                : $"{have:N0} {arc.Unit}, past every milestone",
            IsDone = next is null,
        };
    }
}
