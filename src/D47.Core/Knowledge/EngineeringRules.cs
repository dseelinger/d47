using D47.Core.Journal;

namespace D47.Core.Knowledge;

/// <summary>What a material trader charges: pay <see cref="Paid"/> to receive <see cref="Received"/>.</summary>
public readonly record struct MaterialExchange(int Paid, int Received)
{
    /// <summary>How many to hand over for <paramref name="wanted"/> units, rounded up.</summary>
    public int CostOf(int wanted) => wanted <= 0 ? 0 : (int)Math.Ceiling((double)wanted / Received) * Paid;
}

/// <summary>The handful of fixed numbers Elite applies to engineering, hand-written on purpose.</summary>
public static class EngineeringRules
{
    /// <summary>The rolls a full grade takes, or null when the Commander's rank cannot reach it.</summary>
    public static int? RollsFor(int grade, int rank) =>
        grade is < 1 or > 5 || rank is < 1 or > 5 || rank < grade
            ? null
            : 5 - (rank - grade);

    /// <summary>The fill one roll adds, or null when the rank cannot reach the grade.</summary>
    public static double? ProgressPerRoll(int grade, int rank) =>
        RollsFor(grade, rank) is { } rolls ? 1.0 / rolls : null;

    /// <summary>The fill at or above which the game treats a grade as finished: 0.85, not 1.0.</summary>
    public const double CompleteAt = 0.85;

    /// <summary>Rolls still to do on a part-finished grade.</summary>
    public static int? RollsRemaining(int grade, int rank, double quality)
    {
        if (ProgressPerRoll(grade, rank) is not { } step)
        {
            return null;
        }

        return quality >= CompleteAt ? 0 : (int)Math.Ceiling((1.0 - Math.Max(quality, 0.0)) / step);
    }

    /// <summary>What is true about getting a rank up, said in one sentence (#26).</summary>
    public static string RankRises =>
        "Rank rises by working with them, and it compounds: every rank you gain makes each later "
        + "craft count for more.";

    /// <summary>
    /// The grade a Commander needs with a referring engineer before that engineer will recommend the
    /// next one.
    /// </summary>
    public const int ReferralGrade = 3;

    /// <summary>
    /// What a material trader charges to turn one material into another, or null for a trade the rules
    /// do not define.
    /// </summary>
    public static MaterialExchange? TradeRate(int fromGrade, int toGrade, bool sameLine)
    {
        if (fromGrade is < 1 or > 5 || toGrade is < 1 or > 5 || (sameLine && fromGrade == toGrade))
        {
            return null;
        }

        var delta = toGrade - fromGrade;

        var paid = 1L;
        var received = 1L;

        if (delta < 0)
        {
            received = Power(3, -delta);
        }
        else
        {
            paid = Power(6, delta);
        }

        if (!sameLine)
        {
            paid *= 6;
        }

        var divisor = GreatestCommonDivisor(paid, received);

        return new MaterialExchange((int)(paid / divisor), (int)(received / divisor));
    }

    /// <summary>
    /// True when a trade the rules define is one nobody can actually make, because it wants more of the
    /// input than the Commander is allowed to hold.
    /// </summary>
    public static bool IsBeyondCapacity(int fromGrade, int toGrade, bool sameLine) =>
        TradeRate(fromGrade, toGrade, sameLine) is { } exchange
        && MaterialGrades.CapacityOfGrade(fromGrade) is { } capacity
        && exchange.Paid > capacity;

    private static long Power(int value, int exponent)
    {
        var total = 1L;

        for (var step = 0; step < exponent; step++)
        {
            total *= value;
        }

        return total;
    }

    private static long GreatestCommonDivisor(long left, long right)
    {
        while (right != 0)
        {
            (left, right) = (right, left % right);
        }

        return left;
    }
}
