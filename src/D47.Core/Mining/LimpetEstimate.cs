using System.Globalization;
using D47.Core.Journal;

namespace D47.Core.Mining;

/// <summary>How many limpets a haul takes, judged from the Commander's own finished runs (#610).</summary>
public static class LimpetEstimate
{
    /// <summary>The runs a material has to have led before its own ratio is used.</summary>
    public const int RunsForAMaterial = 3;

    /// <summary>
    /// The spoken estimate for <paramref name="tonnes"/>, of <paramref name="material"/> (a name from
    /// <see cref="MiningTarget.Materials"/>) when one is given, or null with no runs to estimate from.
    /// </summary>
    public static string? Describe(IReadOnlyList<MiningRun> runs, double tonnes, string? material)
    {
        ArgumentNullException.ThrowIfNull(runs);

        if (runs.Count == 0)
        {
            return null;
        }

        var symbol = material is null ? null : MiningTarget.SymbolOf(material);
        var led = symbol is null ? [] : runs.Where(run => MostRefined(run) == symbol).ToList();

        int refined;
        string from;
        string of;
        IReadOnlyList<MiningRun> used;

        if (led.Count >= RunsForAMaterial)
        {
            used = led;
            refined = led.Sum(run => run.Refined[symbol!].Tonnes);
            from = $"from your {led.Count} past {material} runs";
            of = $" of {material}";
        }
        else
        {
            used = runs;
            refined = runs.Sum(run => run.TonnesRefined);
            from = $"from your {Count(runs.Count)} of any material";
            of = string.Empty;
        }

        var collectors = used.Sum(run => run.CollectorsLaunched);
        var prospectors = used.Sum(run => run.ProspectorsLaunched);
        var collectorsNeeded = Needed(tonnes, collectors, refined);
        var prospectorsNeeded = Needed(tonnes, prospectors, refined);

        var lead = material is not null && of.Length == 0
            ? $"Fewer than {RunsForAMaterial} of your runs were mostly {material}, so this is across all of them. "
            : string.Empty;

        return $"{lead}About {collectorsNeeded} {Plural(collectorsNeeded, "collector")} "
            + $"and {prospectorsNeeded} {Plural(prospectorsNeeded, "prospector")} "
            + $"for {Number(tonnes)} {(tonnes == 1 ? "tonne" : "tonnes")}{of}, {from}: "
            + $"{Every("collector", refined, collectors)}, {Every("prospector", refined, prospectors)}.";
    }

    private static string? MostRefined(MiningRun run) =>
        run.Refined.Values
            .OrderByDescending(material => material.Tonnes)
            .ThenBy(material => material.Symbol, StringComparer.Ordinal)
            .FirstOrDefault()?.Symbol;

    /// <summary>Limpets for the haul at the runs' rate, rounded up to whole limpets.</summary>
    private static int Needed(double tonnes, int launched, int refined) =>
        (int)Math.Ceiling(Math.Round(tonnes * launched / refined, 9));

    private static string Every(string limpet, int refined, int launched) =>
        launched == 0
            ? $"no {limpet}s launched"
            : $"a {limpet} every {(refined / (double)launched).ToString("0.0", CultureInfo.InvariantCulture)} tonnes";

    private static string Count(int runs) => runs == 1 ? "1 past run" : $"{runs} past runs";

    private static string Plural(int count, string noun) => count == 1 ? noun : noun + "s";

    private static string Number(double value) => value.ToString("0.#", CultureInfo.InvariantCulture);
}
