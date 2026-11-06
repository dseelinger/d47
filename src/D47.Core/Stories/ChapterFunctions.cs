using D47.Core.Adventures;

namespace D47.Core.Stories;

/// <summary>The dramatic function of each objective in a planned chapter.</summary>
public static class ChapterFunctions
{
    public const string Setup = "setup";

    public const string Complication = "complication";

    public const string Turn = "turn";

    public const string Midpoint = "midpoint";

    public const string AllIsLost = "all is lost";

    public const string Setback = "setback";

    public const string Resolution = "resolution";

    /// <summary>
    /// The functions of <paramref name="count"/> objectives in order: setup first and resolution last; objective
    /// round(0.6 × (count − 1)) + 1 is the turn, named for <paramref name="stage"/>; the objective after it is setback unless
    /// it is the last; the rest are complication.
    /// </summary>
    public static IReadOnlyList<string> For(int count, StoryStage stage)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(count);

        var functions = Enumerable.Repeat(Complication, count).ToArray();

        if (count == 0)
        {
            return functions;
        }

        var turn = (int)Math.Round(0.6 * (count - 1), MidpointRounding.AwayFromZero);

        if (turn > 0 && turn < count - 1)
        {
            functions[turn] = TurnAt(stage);

            if (turn + 1 < count - 1)
            {
                functions[turn + 1] = Setback;
            }
        }

        functions[0] = Setup;
        functions[count - 1] = Resolution;

        return functions;
    }

    /// <summary>
    /// The functions of <paramref name="count"/> objectives that replace objectives labelled <paramref name="labels"/>: the labels
    /// themselves when the counts match; with more, complications before the last label; with fewer, the last label kept and the
    /// latest non-turning labels before it dropped first.
    /// </summary>
    public static IReadOnlyList<string> Relabel(IReadOnlyList<string> labels, int count)
    {
        ArgumentNullException.ThrowIfNull(labels);
        ArgumentOutOfRangeException.ThrowIfNegative(count);

        if (labels.Count == 0 || count == 0)
        {
            return Enumerable.Repeat(Complication, count).ToArray();
        }

        var kept = labels.ToList();

        while (kept.Count < count)
        {
            kept.Insert(kept.Count - 1, Complication);
        }

        while (kept.Count > count)
        {
            var drop = kept.FindLastIndex(kept.Count - 2, kept.Count - 1, label => !AdventureStanding.IsTurning(label));
            kept.RemoveAt(drop >= 0 ? drop : kept.Count - 2);
        }

        return kept;
    }

    /// <summary>The turn's function at <paramref name="stage"/>.</summary>
    public static string TurnAt(StoryStage stage) => stage switch
    {
        StoryStage.Midpoint => Midpoint,
        StoryStage.AllIsLost => AllIsLost,
        _ => Turn,
    };
}
