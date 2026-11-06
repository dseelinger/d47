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

    /// <summary>The turn's function at <paramref name="stage"/>.</summary>
    public static string TurnAt(StoryStage stage) => stage switch
    {
        StoryStage.Midpoint => Midpoint,
        StoryStage.AllIsLost => AllIsLost,
        _ => Turn,
    };
}
