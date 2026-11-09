using D47.Core.Adventures;
using D47.Core.Engineers;
using D47.Core.Journal;
using D47.Core.Knowledge;
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

/// <summary>Which engineers the engineers step may name.</summary>
public enum EngineerSteps
{
    /// <summary>Every engineer on the route.</summary>
    All,

    /// <summary>Not an engineer out at Colonia while the Commander is more than <see cref="GoalClimb.FarAway"/> light years from them.</summary>
    NotFarAway,

    /// <summary>None.</summary>
    None,
}

/// <summary>Picks the unfinished Goals arcs that are furthest behind, so a Goals story's chapters raise the arcs together.</summary>
public static class GoalClimb
{
    /// <summary>The share of a rank from which a chapter asks for the promotion rather than the activities that earn it.</summary>
    public const double PromotionNear = 0.75;

    /// <summary>The most arcs one chapter works.</summary>
    public const int MostSteps = 2;

    /// <summary>The light years from a far engineer beyond which a chapter does not carry their objective plainly.</summary>
    public const double FarAway = 1000;

    private static readonly string[] CombatMissions = ["Mission_Massacre", "Mission_Assassinate"];

    private static readonly string[] SoldierMissions =
        ["Mission_OnFoot_Massacre", "Mission_OnFoot_Onslaught", "Mission_OnFoot_Assassination", "Mission_OnFoot_Defence"];

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

    /// <summary>The standings whose arc keys are in <paramref name="arcs"/>, in their own order; all of them when it is null.</summary>
    public static IReadOnlyList<GoalStanding> Listed(IReadOnlyList<GoalStanding> standings, IReadOnlyList<string>? arcs)
    {
        ArgumentNullException.ThrowIfNull(standings);

        if (arcs is null)
        {
            return standings;
        }

        var keys = new HashSet<string>(arcs.Select(key => key.Trim()), StringComparer.OrdinalIgnoreCase);

        return [.. standings.Where(standing => keys.Contains(standing.Arc.Key))];
    }

    /// <summary>The steps for the first <see cref="MostSteps"/> arcs, each a different arc, in the order of their rank.</summary>
    public static IReadOnlyList<GoalClimbStep> Steps(
        IReadOnlyList<GoalStanding> standings, CommanderGameState? state, EngineerReport? engineers, Func<DrawableActivity, bool>? drawable = null, EngineerSteps offered = EngineerSteps.All) =>
        [.. Ranked(standings, state, engineers, drawable, offered).Take(MostSteps)];

    /// <summary>The engineer an engineers step in <paramref name="steps"/> names when they are out at Colonia and more than <see cref="FarAway"/> light years from <paramref name="at"/>; otherwise null.</summary>
    public static Engineer? FarEngineer(IReadOnlyList<GoalClimbStep> steps, EngineerReport? engineers, StarPosition? at)
    {
        ArgumentNullException.ThrowIfNull(steps);

        return engineers is not null
            && GoalBook.NextEngineer(engineers)?.Engineer is { } engineer
            && steps.Any(step => step.Objective is { Kind: TriggerKind.Engineer } objective && string.Equals(objective.Engineer, engineer.Name, StringComparison.Ordinal))
            && IsFarAway(engineer, at)
                ? engineer
                : null;
    }

    private static bool IsFarAway(Engineer engineer, StarPosition? at) =>
        engineer.IsFarFromTheBubble == true && engineer.DistanceFrom(at) > FarAway;

    /// <summary>How many activities a chapter with these steps draws: <see cref="ChapterDraw.Slots"/>, less one for each objective.</summary>
    public static int Slots(IReadOnlyList<GoalClimbStep> steps)
    {
        ArgumentNullException.ThrowIfNull(steps);

        return ChapterDraw.Slots - steps.Count(step => step.Objective is not null);
    }

    /// <summary>
    /// The limit of each drawn slot, by index, for <see cref="ChapterDraw.Draw"/>. A lone draw step limits every slot; with two
    /// steps the first limits two and the second one. An objective step limits none, and the slots past the list are unlimited.
    /// </summary>
    public static IReadOnlyList<Func<DrawableActivity, bool>> Limits(IReadOnlyList<GoalClimbStep> steps)
    {
        ArgumentNullException.ThrowIfNull(steps);

        return [.. steps.SelectMany((step, index) => Enumerable.Repeat<Func<DrawableActivity, bool>>(step.Earns, Share(steps, index)))];
    }

    /// <summary>
    /// The step each drawn slot is credited to, by index: the first step whose rows include it and whose share of the
    /// <see cref="Limits"/> is not used up, else the first step whose rows include it; null for a slot no step's rows include.
    /// </summary>
    public static IReadOnlyList<GoalClimbStep?> Credited(IReadOnlyList<GoalClimbStep> steps, IReadOnlyList<DrawnActivity>? slots)
    {
        ArgumentNullException.ThrowIfNull(steps);

        var left = steps.Select((_, index) => Share(steps, index)).ToArray();

        return [.. (slots ?? []).Select(slot =>
        {
            var row = ChapterActivities.Find(slot.Kind, slot.Family, slot.Organic);
            var index = row is null ? -1 : Enumerable.Range(0, steps.Count).FirstOrDefault(index => left[index] > 0 && steps[index].Earns(row), -1) is var shared and >= 0
                ? shared
                : Enumerable.Range(0, steps.Count).FirstOrDefault(index => steps[index].Earns(row), -1);

            if (index < 0)
            {
                return null;
            }

            left[index] = Math.Max(0, left[index] - 1);
            return steps[index];
        })];
    }

    /// <summary>The steps the drawn slots work: an objective step, or a draw step one of whose rows was drawn.</summary>
    public static IReadOnlyList<GoalClimbStep> Worked(IReadOnlyList<GoalClimbStep> steps, IReadOnlyList<DrawnActivity>? slots)
    {
        ArgumentNullException.ThrowIfNull(steps);

        return [.. steps.Where(step => step.Draw is null || slots?.Any(slot => ChapterActivities.Find(slot.Kind, slot.Family, slot.Organic) is { } row && step.Earns(row)) == true)];
    }

    private static int Share(IReadOnlyList<GoalClimbStep> steps, int index) =>
        steps[index].Draw is null ? 0 : steps.Count == 1 ? ChapterDraw.Slots : index == 0 ? ChapterDraw.Slots - 1 : 1;

    private static IEnumerable<GoalClimbStep> Ranked(IReadOnlyList<GoalStanding> standings, CommanderGameState? state, EngineerReport? engineers, Func<DrawableActivity, bool>? drawable, EngineerSteps offered)
    {
        ArgumentNullException.ThrowIfNull(standings);

        var held = state is null ? ActivityGate.None : ChapterActivities.Held(state.Ranks, state.Ship, odyssey: true);
        drawable ??= row => ChapterActivities.Passes(row, held);
        var unpledged = state?.Pledge is { IsKnown: true, IsPledged: false };

        return standings
            .Where(standing => !standing.IsDone)
            .OrderBy(standing => unpledged && standing.Arc.Key == GoalCatalogue.Powerplay ? -1 : standing.Fraction ?? 0)
            .Select(standing => Step(standing, state, engineers, drawable, offered))
            .OfType<GoalClimbStep>();
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
            "Soldier" => row.Kind == TriggerKind.OnFootBond || IsMission(row, SoldierMissions),
            Careers.Empire or Careers.Federation => row.Kind == TriggerKind.Mission,
            _ => false,
        };
    }

    private static bool IsMission(DrawableActivity row, string[] families) =>
        row.Kind == TriggerKind.Mission && families.Contains(row.Family, StringComparer.Ordinal);

    private static GoalClimbStep? Step(GoalStanding standing, CommanderGameState? state, EngineerReport? engineers, Func<DrawableActivity, bool> drawable, EngineerSteps offered)
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
                if (offered == EngineerSteps.None || engineers is null || GoalBook.NextEngineer(engineers) is not { } candidate
                    || (offered == EngineerSteps.NotFarAway && IsFarAway(candidate.Engineer, state?.Location.StarPos)))
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
