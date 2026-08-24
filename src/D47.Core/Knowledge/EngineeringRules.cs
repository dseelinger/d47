using D47.Core.Journal;

namespace D47.Core.Knowledge;

/// <summary>
/// What a material trader charges: pay <see cref="Paid"/> to receive <see cref="Received"/>.
/// <para>
/// In lowest terms, because the game trades in any whole multiple. The published rule quotes a
/// cross-line trade one grade down as 6 for 3, but 2 for 1 is the same exchange and is what a
/// Commander wanting one unit should actually hand over — quoting the unreduced form would send
/// them to gather three times what they need.
/// </para>
/// </summary>
public readonly record struct MaterialExchange(int Paid, int Received)
{
    /// <summary>How many to hand over for <paramref name="wanted"/> units, rounded up.</summary>
    public int CostOf(int wanted) => wanted <= 0 ? 0 : (int)Math.Ceiling((double)wanted / Received) * Paid;
}

/// <summary>
/// The handful of fixed numbers Elite applies to engineering, hand-written on purpose.
/// <para>
/// <b>These are rules, not data.</b> Everything else in this namespace is generated from a
/// community source and regenerated when Frontier ships a patch; this is five short tables that
/// change when the *game* changes, and putting them in a generated file would mean a routine
/// refresh could silently move them. Same split <see cref="MaterialGrades.CapacityOfGrade"/>
/// already makes — the grades are generated, the five capacities beside them are not.
/// </para>
/// <para>
/// <b>Sourced, and checked one table at a time — which is not the same as all of it.</b> The roll
/// table is Frontier's own, published in the 4.0.18.08 update notes, and it was confirmed against
/// <b>6,272 real engineering rolls</b> from a 912-journal corpus. The referral grade came from the
/// Fandom engineer page and was confirmed too, to the second, by a progress trace in
/// <c>docs/spikes/journal-corpus-engineering.md</c>.
/// <para>
/// <b>The reputation prices were neither, and this comment used to say otherwise.</b> It read
/// "all of it was then confirmed", which is the word that stopped anybody checking: joining
/// "credits of profit sold at a workshop" to a rank-up is not a question that corpus was ever
/// asked, and could not have been answered by it. The figures came from a wiki, were rendered as
/// though credits were the way a rank rises, and are gone (#26). The same spike disproves them in
/// passing — its trace has Selene Jean going rank 2 to rank 4 in thirty-eight seconds.
/// </para>
/// </para>
/// </summary>
public static class EngineeringRules
{
    /// <summary>
    /// The rolls a full grade takes, or null when the Commander's rank cannot reach it.
    /// <para>
    /// Frontier's published table, as progress gained per roll:
    /// </para>
    /// <code>
    ///            rank 1   rank 2   rank 3   rank 4   rank 5
    ///  grade 1     20%      25%      34%      50%     100%
    ///  grade 2      —       20%      25%      34%      50%
    ///  grade 3      —        —       20%      25%      34%
    ///  grade 4      —        —        —       20%      25%
    ///  grade 5      —        —        —        —       20%
    /// </code>
    /// <para>
    /// Rolls needed is 100 divided by the percentage, so the ladder is 5, 4, 3, 2, 1 indexed by
    /// <c>rank − grade</c>. Nothing else is involved: not luck, not the module, not the Commander.
    /// </para>
    /// <para>
    /// <b>A dash is a hard gate, not a slow path.</b> Grade <i>N</i> cannot be rolled below rank
    /// <i>N</i> at all, which is why this answers null rather than a large number — a shortfall is
    /// something gathering fixes and a gate is not. The corpus makes this the strongest single
    /// result it has: grade 5 shows exactly one step size across 1,428 deltas, which is what a hard
    /// gate predicts and what nothing else does.
    /// </para>
    /// </summary>
    public static int? RollsFor(int grade, int rank) =>
        grade is < 1 or > 5 || rank is < 1 or > 5 || rank < grade
            ? null
            : 5 - (rank - grade);

    /// <summary>
    /// The fill one roll adds, or null when the rank cannot reach the grade.
    /// <para>
    /// <b>Frontier's 34% is <c>1/3</c> in the game.</b> Observed deltas are <c>0.333</c> (428
    /// times) and <c>0.334</c> (190), never <c>0.34</c> — so three rolls sum to <c>0.99</c> rather
    /// than overshooting, and the completion check tolerates the shortfall its own rounding
    /// creates. Expressed as a reciprocal here so that stays true instead of being rounded back in.
    /// </para>
    /// </summary>
    public static double? ProgressPerRoll(int grade, int rank) =>
        RollsFor(grade, rank) is { } rolls ? 1.0 / rolls : null;

    /// <summary>
    /// The fill at or above which the game treats a grade as finished: <b>0.85, not 1.0</b>.
    /// <para>
    /// <b>This is the number that would have shipped a bug.</b> Of 994 grades in the corpus, 926
    /// reach exactly 1.0. Of the 68 that do not, the <b>45 the game let the Commander move on
    /// from</b> all sat at 0.85, 0.95 or 0.99, and the 23 genuinely abandoned all sat at 0.8 or
    /// below. Nothing falls between, so the boundary is clean — but 0.85 is the <b>lowest observed
    /// completion across that sample, not a proven threshold</b>, and a larger corpus could move
    /// it. Carried with its sample size for exactly that reason.
    /// </para>
    /// <para>
    /// Gating "done" on <c>Quality == 1.0</c> would have looked right in testing, because 93% of
    /// grades do reach 1.0. The bug lives in the last 7%, and it tells a Commander that a module
    /// they can see is finished is not.
    /// </para>
    /// </summary>
    public const double CompleteAt = 0.85;

    /// <summary>
    /// Rolls still to do on a part-finished grade. Zero once <see cref="CompleteAt"/> is reached.
    /// <para>
    /// Counted towards a full 1.0 rather than towards 0.85, deliberately. The band is where the
    /// game stops <em>insisting</em>, not a target to aim at — quoting the rolls to reach it would
    /// under-count for every Commander who intends to finish the grade properly. Null when the
    /// rank cannot reach the grade at all, which is a different answer from "none left".
    /// </para>
    /// <para>
    /// The last roll clamps — deltas of 0.15 and 0.1 appear only as the final step of a run — so
    /// this is a function of the remaining fill and not of the grade.
    /// </para>
    /// </summary>
    public static int? RollsRemaining(int grade, int rank, double quality)
    {
        if (ProgressPerRoll(grade, rank) is not { } step)
        {
            return null;
        }

        return quality >= CompleteAt ? 0 : (int)Math.Ceiling((1.0 - Math.Max(quality, 0.0)) / step);
    }

    /// <summary>
    /// What is true about getting a rank up, said in one sentence
    /// (<a href="https://github.com/dseelinger/d47/issues/26">#26</a>).
    /// <para>
    /// <b>This replaced a credits figure that was wrong to state as the route.</b> d47 used to say
    /// grade 5 "takes 16,000,000 credits of profit sold at that workshop", from a wiki, immediately
    /// after saying no amount of gathering would fix the gate — so a Commander was told credits
    /// were the only way up. They are not: <b>working with an engineer is what raises the rank</b>,
    /// which is how the report arrived, having reached grade 2 with Selene Jean by rolling.
    /// The corpus in this repository disproves the figures outright — one trace has Selene Jean
    /// going rank 2 to rank 4 in <b>thirty-eight seconds</b>, which nobody does by selling ten
    /// million credits of profit.
    /// </para>
    /// <para>
    /// What replaces it is Frontier's own published table rather than anybody's claim, and it is
    /// the encouraging half as well as the true one. Read <see cref="RollsFor"/> along a row: the
    /// further a rank is above the grade being rolled, the fewer rolls that grade takes — five at
    /// the bottom, one when the rank is four above. So the early work really is the slow part, and
    /// it really does pay for itself afterwards.
    /// </para>
    /// </summary>
    public static string RankRises =>
        "Rank rises by working with them, and it compounds: every rank you gain makes each later "
        + "craft count for more.";

    /// <summary>
    /// The grade a Commander needs with a referring engineer before that engineer will recommend
    /// the next one.
    /// <para>
    /// Both sources state it as grade 3 access <em>plus roughly half the bar towards grade 4</em>.
    /// Only the stated half is encoded; the rough half is not a number anybody published.
    /// </para>
    /// </summary>
    public const int ReferralGrade = 3;

    /// <summary>
    /// What a material trader charges to turn one material into another, or null for a trade the
    /// rules do not define.
    /// <para>
    /// One grade down returns 3 for 1, one grade up costs 6 for 1, and <b>a different line costs a
    /// further 6</b> — combinations multiply. Confirmed exactly across all 1,096 trades in the
    /// corpus, of which <b>560 were cross-line</b>: the majority, not an edge case.
    /// </para>
    /// <para>
    /// <b><paramref name="sameLine"/> is the trader's grid column, never the journal's
    /// <c>Category</c>.</b> That field names the Raw/Manufactured/Encoded type, which no trade
    /// ever crosses — so passing it here reads Iron for Vanadium as a same-line trade and prices
    /// the commonest trade there is at a sixth of what it costs. Use
    /// <see cref="MaterialEntry.Line"/>, and note that a material with no line cannot be traded at
    /// all.
    /// </para>
    /// <para>
    /// Null for a same-line trade at the same grade, which is not a trade: within a line a grade
    /// names exactly one material, so a same-grade exchange is always cross-line.
    /// </para>
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
    /// True when a trade the rules define is one nobody can actually make, because it wants more
    /// of the input than the Commander is allowed to hold.
    /// <para>
    /// Grade 1 to grade 5 within a line costs 1,296 units against a 300-unit cap, and grade 3 to
    /// grade 5 across lines costs 216 against a cap of 200. Both are arithmetic the rules permit
    /// and the game does not, so saying so is the answer — quoting the number and letting a
    /// Commander discover it is the same failure as inventing one.
    /// </para>
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
