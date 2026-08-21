using System.Globalization;
using D47.Core.Journal;
using D47.Core.Knowledge;
using D47.Core.Ships;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace BuildGaugeProbe;

/// <summary>
/// Replays every <c>Loadout</c> in the corpus through the shipped gauge arithmetic and compares it
/// against the figures Frontier wrote (list.md Phase 38).
/// <para>
/// Two questions, and the corpus answers both: does the jump formula reproduce
/// <c>MaxJumpRange</c>, and does a blueprint's own grade row applied to a stock figure land on the
/// <c>Modifiers</c> a real roll produced. The second is the one with no ground truth for the thing
/// it is actually used for — a modelled figure can only be checked against a roll already done —
/// so what it proves is the arithmetic, not the prediction.
/// </para>
/// <para>
/// Findings are written up in <c>docs/spikes/build-gauges.md</c>. Exits non-zero if either
/// measurement falls below what that page records, so this is a gate and not just a report.
/// </para>
/// </summary>
internal static class Program
{
    /// <summary>
    /// Finished rolls the model does not reproduce, and the number is a ceiling rather than a
    /// tolerance.
    /// <para>
    /// <b>One, and it is named.</b> A grade 5 Lightweight Alloy on the Caspian Explorer: EDEngineer
    /// publishes the mass change as −56% and Elite applies −55%, so 15 tonnes lands at 6.75 where
    /// the table says 6.6. That is an upstream figure disagreeing with the game by one percentage
    /// point on one blueprint, not an error in the arithmetic — and it is left as a named exception
    /// so that a <em>second</em> one fails this run instead of hiding behind a percentage.
    /// </para>
    /// </summary>
    private const int KnownMisses = 1;

    private static int Main(string[] args)
    {
        var directory = args.Length > 0 ? args[0] : JournalFolder.DefaultPath();

        if (!Directory.Exists(directory))
        {
            Console.Error.WriteLine($"No such directory: {directory}");
            return 2;
        }

        var files = Directory.EnumerateFiles(directory, JournalFolder.FilePattern)
            .OrderBy(Path.GetFileName, StringComparer.Ordinal)
            .ToList();

        Console.WriteLine($"{files.Count} journals in {directory}");

        var logger = NullLogger.Instance;
        var jumps = new List<Divergence>();
        var rolls = new List<Divergence>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var noDrive = 0;
        var noGauge = 0;

        foreach (var file in files)
        {
            foreach (var line in ReadLines(file))
            {
                if (!line.Contains("\"event\":\"Loadout\"", StringComparison.Ordinal)
                    || !JournalEvent.TryParse(line, logger, out var journalEvent)
                    || journalEvent!.Kind != "Loadout")
                {
                    continue;
                }

                var loadout = ShipLoadout.Unknown.Apply(journalEvent);

                Jump(loadout, jumps, ref noDrive, ref noGauge);
                Rolls(loadout, rolls, seen);
            }
        }

        Console.WriteLine();
        Console.WriteLine("Jump range, best needle against Frontier's own MaxJumpRange");
        var jumpOk = Report(jumps, within: 0.5, atLeast: 99.0);
        Console.WriteLine($"  no drive found: {noDrive}, no gauge at all: {noGauge}");

        // Every miss should be the known Frontier-side inconsistency: their figure omits a fitted
        // Guardian booster's bonus. Asserted rather than tolerated — a *new* kind of miss is what
        // this run exists to notice.
        var strays = jumps
            .Where(divergence => divergence.Percent >= 0.5 && !divergence.Note.StartsWith("boost", StringComparison.Ordinal))
            .ToList();

        Console.WriteLine(
            strays.Count == 0
                ? "  every miss is the known booster disagreement"
                : $"  UNEXPLAINED misses: {strays.Count}");

        foreach (var stray in strays.Take(5))
        {
            Console.WriteLine($"    {stray}");
        }

        Console.WriteLine();
        Console.WriteLine("Modelled rolls against the Modifiers Elite wrote");
        var rollOk = Report(rolls, within: 0.05, atLeast: 97.0);

        var unfinished = rolls.Count(divergence => divergence.Percent >= 0.05 && divergence.Quality < 1);
        var finished = rolls.Count(divergence => divergence.Percent >= 0.05 && divergence.Quality >= 1);

        Console.WriteLine($"  misses on a part-rolled module: {unfinished}");
        Console.WriteLine($"  misses on a finished roll: {finished} (known: {KnownMisses})");

        foreach (var stray in rolls.Where(d => d.Percent >= 0.05 && d.Quality >= 1))
        {
            Console.WriteLine($"    {stray}");
        }

        return jumpOk && rollOk && strays.Count == 0 && finished <= KnownMisses ? 0 : 1;
    }

    /// <summary>
    /// The best needle for one reported loadout, against the figure the game put in the same event.
    /// </summary>
    private static void Jump(ShipLoadout loadout, List<Divergence> into, ref int noDrive, ref int noGauge)
    {
        if (loadout.MaxJumpRange is not { } reported || reported <= 0 || loadout.Type is not { } hull)
        {
            return;
        }

        var build = new ShipBuild(hull, hull, null);
        var gauges = ShipGauges.Read(build, loadout);

        if (gauges.Jump is not { } jump)
        {
            if (gauges.IsEmpty)
            {
                noGauge++;
            }
            else
            {
                noDrive++;
            }

            return;
        }

        var boosted = loadout.Modules.Any(module =>
            EliteSpecifications.Module(module.Item)?.JumpBoost is not null);

        into.Add(new Divergence(
            Math.Abs(jump.Best - reported) / reported * 100,
            1,
            boosted ? $"boosted {hull}" : hull,
            jump.Best,
            reported));
    }

    /// <summary>
    /// Every distinct engineered module, modelled from its own blueprint row and compared against
    /// what the roll actually produced. Distinct, because a fleet's worth of unchanged Loadouts
    /// would otherwise weight the result by how often the Commander re-docked.
    /// </summary>
    private static void Rolls(ShipLoadout loadout, List<Divergence> into, HashSet<string> seen)
    {
        foreach (var module in loadout.Modules)
        {
            if (module.Blueprint is not { Length: > 0 } blueprint
                || EliteSpecifications.Module(module.Item) is not { } spec)
            {
                continue;
            }

            var key = string.Join(
                '|',
                module.Item,
                blueprint,
                module.BlueprintLevel,
                module.Experimental,
                module.Quality?.ToString("0.####", CultureInfo.InvariantCulture));

            if (!seen.Add(key))
            {
                continue;
            }

            foreach (var modifier in module.Modifiers)
            {
                if (Attributes.GetValueOrDefault(modifier.Label) is not { } attribute
                    || modifier.OriginalValue is not { } stock
                    || stock == 0
                    || modifier.Value is not { } landed
                    || landed == 0)
                {
                    continue;
                }

                var modelled = RollModel.Apply(
                    stock,
                    attribute,
                    spec,
                    blueprint,
                    module.BlueprintLevel ?? 0,
                    module.Experimental);

                if (modelled is not { } value)
                {
                    continue;
                }

                into.Add(new Divergence(
                    Math.Abs(value - landed) / Math.Abs(landed) * 100,
                    module.Quality ?? 1,
                    $"{blueprint} g{module.BlueprintLevel} {modifier.Label} on {module.Item}",
                    value,
                    landed));
            }
        }
    }

    /// <summary>
    /// The <c>Modifiers</c> labels Elite writes, mapped to the blueprint table's own wording for
    /// the same attribute. Only the ones the gauges read: this is a check on the arithmetic, not
    /// an attempt to model every figure a weapon carries.
    /// </summary>
    private static readonly Dictionary<string, string> Attributes = new(StringComparer.OrdinalIgnoreCase)
    {
        ["PowerDraw"] = "Power Draw",
        ["PowerCapacity"] = "Power Generation",
        ["FSDOptimalMass"] = "Optimal Mass",
        ["Mass"] = "Mass",
        ["Integrity"] = "Integrity",
    };

    private static bool Report(List<Divergence> divergences, double within, double atLeast)
    {
        if (divergences.Count == 0)
        {
            Console.WriteLine("  nothing to compare");
            return false;
        }

        var sorted = divergences.Select(divergence => divergence.Percent).Order().ToList();
        var median = sorted[sorted.Count / 2];
        var p95 = sorted[(int)(sorted.Count * 0.95)];
        var share = 100.0 * sorted.Count(percent => percent < within) / sorted.Count;

        Console.WriteLine(
            $"  n={sorted.Count}  median {median:0.0000}%  p95 {p95:0.0000}%  "
            + $"within {within}% = {share:0.0}%");

        return median < 0.0001 && share >= atLeast;
    }

    /// <summary>One comparison: how far off, on what, and what the two figures were.</summary>
    private readonly record struct Divergence(
        double Percent, double Quality, string Note, double Modelled, double Reported)
    {
        public override string ToString() =>
            $"{Percent:0.00}% q={Quality:0.##} {Note}: modelled {Modelled:0.###} against {Reported:0.###}";
    }

    /// <summary>
    /// Reads a journal Elite may still be writing to, which is the same share mode the real reader
    /// uses.
    /// </summary>
    private static IEnumerable<string> ReadLines(string path)
    {
        using var stream = new FileStream(
            path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);

        using var reader = new StreamReader(stream);

        while (reader.ReadLine() is { } line)
        {
            yield return line;
        }
    }
}
