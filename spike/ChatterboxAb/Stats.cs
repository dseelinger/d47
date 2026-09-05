namespace ChatterboxAb;

/// <summary>
/// The stopping rule. Ties are not evidence either way, so they are dropped; what is left is a
/// coin-flip question — does the ear prefer one model more often than chance — and an exact
/// two-sided binomial test answers it without an approximation that a small n would break.
/// </summary>
internal static class Stats
{
    public const int MinDecisive = 20;
    public const int MaxTrials = 100;
    public const double Alpha = 0.05;

    /// <summary>Two-sided exact binomial p-value for <paramref name="k"/> successes in <paramref name="n"/> at p = 0.5.</summary>
    public static double BinomialP(int k, int n)
    {
        if (n == 0)
        {
            return 1;
        }

        var observed = Mass(k, n);
        var p = 0.0;

        for (var i = 0; i <= n; i++)
        {
            var mass = Mass(i, n);

            if (mass <= observed + 1e-12)
            {
                p += mass;
            }
        }

        return Math.Min(1, p);
    }

    /// <summary>Wilson 95% interval for the preference rate.</summary>
    public static (double Low, double High) Wilson(int k, int n)
    {
        if (n == 0)
        {
            return (0, 1);
        }

        const double z = 1.959964;
        var p = k / (double)n;
        var denominator = 1 + z * z / n;
        var centre = (p + z * z / (2 * n)) / denominator;
        var half = z * Math.Sqrt(p * (1 - p) / n + z * z / (4 * n * n)) / denominator;

        return (Math.Max(0, centre - half), Math.Min(1, centre + half));
    }

    private static double Mass(int k, int n)
    {
        // log C(n,k) - n log 2, to stay finite at n = 100.
        var log = 0.0;

        for (var i = 1; i <= k; i++)
        {
            log += Math.Log(n - k + i) - Math.Log(i);
        }

        return Math.Exp(log - n * Math.Log(2));
    }
}
