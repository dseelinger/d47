using D47.Core.Adventures;
using D47.Core.Engineers;
using D47.Core.Journal;
using D47.Core.Stories;

namespace D47.Core.Goals;

/// <summary>
/// What a Goals story chapter does for one arc: an objective it must hold, or the ladder whose rows its draw is
/// limited to. Exactly one of <see cref="Objective"/> and <see cref="Draw"/> is set.
/// </summary>
public sealed record GoalClimbStep(string Key, string Name)
{
    /// <summary>The objective the chapter must hold, or null for a limited draw.</summary>
    public AdventureTrigger? Objective { get; init; }

    /// <summary>The journal key of the ladder whose rows the draw is limited to, or null for an objective.</summary>
    public string? Draw { get; init; }

    /// <summary>Whether a drawn row works this step's ladder; false for an objective step.</summary>
    public bool Earns(DrawableActivity row) => Draw is { } ladder && GoalClimb.Earns(ladder, row);
}

/// <summary>Picks the unfinished Goals arc that is furthest behind, so a Goals story's chapters raise the arcs together.</summary>
public static class GoalClimb
{
    /// <summary>The share of a rank from which a chapter asks for the promotion rather than the activities that earn it.</summary>
    public const double PromotionNear = 0.75;

    private static readonly string[] CombatMissions = ["Mission_Massacre", "Mission_Assassinate"];

    private static readonly string[] SoldierMissions =
        ["Mission_OnFoot_Massacre", "Mission_OnFoot_Onslaught", "Mission_OnFoot_Assassination", "Mission_OnFoot_Defence"];

    /// <summary>
    /// The step for the unfinished arc in <see cref="GoalCatalogue.Every"/> with the lowest <see cref="GoalStanding.Fraction"/>,
    /// a null one counting as 0 and ties going in catalogue order, except that a Commander known to be unpledged is asked
    /// to pledge first; an arc with no step it can give now is passed over.
    /// Null once every arc is done or none can give a step. A career or navy gives a limited draw only when
    /// <paramref name="drawable"/> accepts one of its rows; without it, a row whose gates are held is drawable.
    /// </summary>
    public static GoalClimbStep? Next(CommanderGameState? state, GoalMine? mine, EngineerReport? engineers, Func<DrawableActivity, bool>? drawable = null) =>
        Next(Standings(state, mine), state, engineers, drawable);

    /// <summary>Every arc in <see cref="GoalCatalogue.Every"/>, evaluated, in catalogue order.</summary>
    public static IReadOnlyList<GoalStanding> Standings(CommanderGameState? state, GoalMine? mine) =>
        [.. GoalCatalogue.Every.Select(arc => GoalEvaluator.Evaluate(arc, state, mine))];

    /// <summary>The mean <see cref="GoalStanding.Fraction"/>, 0 to 1, a done arc counting as 1 and a null one as 0; 0 for no standings.</summary>
    public static double Overall(IReadOnlyList<GoalStanding> standings)
    {
        ArgumentNullException.ThrowIfNull(standings);

        return standings.Count == 0 ? 0 : standings.Average(standing => standing.IsDone ? 1 : standing.Fraction ?? 0);
    }

    /// <summary>Whether every standing is done; false for no standings.</summary>
    public static bool AllDone(IReadOnlyList<GoalStanding> standings)
    {
        ArgumentNullException.ThrowIfNull(standings);

        return standings.Count > 0 && standings.All(standing => standing.IsDone);
    }

    /// <summary>The same choice over <paramref name="standings"/>, which are in catalogue order.</summary>
    public static GoalClimbStep? Next(IReadOnlyList<GoalStanding> standings, CommanderGameState? state, EngineerReport? engineers, Func<DrawableActivity, bool>? drawable = null)
    {
        ArgumentNullException.ThrowIfNull(standings);

        var held = state is null ? ActivityGate.None : ChapterActivities.Held(state.Ranks, state.Ship, odyssey: true);
        drawable ??= row => ChapterActivities.Passes(row, held);
        var unpledged = state?.Pledge is { IsKnown: true, IsPledged: false };

        return standings
            .Where(standing => !standing.IsDone)
            .OrderBy(standing => unpledged && standing.Arc.Key == GoalCatalogue.Powerplay ? -1 : standing.Fraction ?? 0)
            .Select(standing => Step(standing, state, engineers, drawable))
            .FirstOrDefault(step => step is not null);
    }

    /// <summary>Whether a drawn row earns rank on the ladder with this journal key.</summary>
    public static bool Earns(string ladder, DrawableActivity row)
    {
        ArgumentNullException.ThrowIfNull(row);

        return ladder switch
        {
            "Combat" => row.Kind is TriggerKind.Bounty or TriggerKind.Bond or TriggerKind.Conflict || IsMission(row, CombatMissions),
            "Trade" => row.Kind == TriggerKind.Sell || row is { Kind: TriggerKind.Mission, Career: Career.Trading },
            "Explore" => row.Kind is TriggerKind.Discover or TriggerKind.Map or TriggerKind.Footfall || row is { Kind: TriggerKind.DataSale, Organic: false },
            "Exobiologist" => row.Kind == TriggerKind.Organic || row is { Kind: TriggerKind.DataSale, Organic: true },
            "Soldier" => IsMission(row, SoldierMissions),
            Careers.Empire or Careers.Federation => row.Kind == TriggerKind.Mission,
            _ => false,
        };
    }

    private static bool IsMission(DrawableActivity row, string[] families) =>
        row.Kind == TriggerKind.Mission && families.Contains(row.Family, StringComparer.Ordinal);

    private static GoalClimbStep? Step(GoalStanding standing, CommanderGameState? state, EngineerReport? engineers, Func<DrawableActivity, bool> drawable)
    {
        var arc = standing.Arc;
        var step = new GoalClimbStep(arc.Key, arc.Name);

        if ((GoalCatalogue.CareerOf(arc.Key) ?? GoalCatalogue.NavyOf(arc.Key)) is { } ladder)
        {
            if (standing is { Rung: >= PromotionNear, Have: { } rank })
            {
                return step with { Objective = new AdventureTrigger { Kind = TriggerKind.Rank, Career = ladder, Rank = (int)rank + 1 } };
            }

            return ChapterActivities.All.Any(row => Earns(ladder, row) && drawable(row))
                ? step with { Draw = ladder }
                : null;
        }

        switch (arc.Key)
        {
            case GoalCatalogue.Powerplay:
                if (state?.Pledge is not { IsKnown: true } pledge)
                {
                    return null;
                }

                return step with
                {
                    Objective = pledge.IsPledged
                        ? new AdventureTrigger { Kind = TriggerKind.PowerRank, Rank = pledge.Rank + 1 }
                        : new AdventureTrigger { Kind = TriggerKind.Pledge },
                };

            case GoalCatalogue.Engineers:
                if (engineers is null || GoalBook.NextEngineer(engineers) is not { } candidate)
                {
                    return null;
                }

                var stage = state?.Engineers.For(candidate.Engineer.Id) switch
                {
                    { IsUnlocked: true } => null,
                    { IsInvited: true } => "Unlocked",
                    _ => "Invited",
                };

                return stage is null
                    ? null
                    : step with { Objective = new AdventureTrigger { Kind = TriggerKind.Engineer, Engineer = candidate.Engineer.Name, Stage = stage } };

            case GoalCatalogue.Ships:
                return GoalBook.NextHull(state) is { } hull && ChapterFit.BoardWhy(hull.Symbol, owned: false, state?.Session.Balance) is null
                    ? step with { Objective = new AdventureTrigger { Kind = TriggerKind.Board, ShipType = hull.Symbol } }
                    : null;

            default:
                return null;
        }
    }
}
