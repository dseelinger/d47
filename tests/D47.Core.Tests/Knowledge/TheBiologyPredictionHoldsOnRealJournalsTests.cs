using D47.Core.Journal;
using D47.Core.Knowledge;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Knowledge;

/// <summary>
/// How often a genus <see cref="BiologyPotential"/> names for a body before it is surface-scanned is one
/// sampled there, and how often a sampled genus was named, on real journal lines in
/// <c>tests/fixtures/exobiology/sampled-bodies.log</c>.
/// </summary>
public class TheBiologyPredictionHoldsOnRealJournalsTests(ITestOutputHelper output)
{
    /// <summary>Measured on the fixture, as percentages.</summary>
    private const double NamedThatWereSampled = 42.0;

    private const double SampledThatWereNamed = 53.9;

    private const double Allowance = 5;

    [Fact]
    public void NeitherFigureFallsMoreThanFivePoints()
    {
        var measured = Assert.NotNull(Measure());

        output.WriteLine(
            $"{measured.Bodies} bodies: {measured.Precision:0.0}% of named genera sampled, "
            + $"{measured.Recall:0.0}% of sampled genera named.");

        Assert.True(measured.Precision >= NamedThatWereSampled - Allowance, $"Precision {measured.Precision:0.0}%");
        Assert.True(measured.Recall >= SampledThatWereNamed - Allowance, $"Recall {measured.Recall:0.0}%");
    }

    private static (int Bodies, double Precision, double Recall)? Measure()
    {
        var scans = BodyScans.Empty;
        var signals = BodySignals.Empty;
        var sampled = new Dictionary<(long, int), HashSet<string>>();

        foreach (var line in EmbeddedFixture.Lines("exobiology.sampled-bodies.log"))
        {
            if (!JournalEvent.TryParse(line, NullLogger.Instance, out var journalEvent) || journalEvent is null)
            {
                continue;
            }

            scans = scans.Apply(journalEvent);
            signals = signals.Apply(journalEvent);

            if (journalEvent.Kind == "ScanOrganic"
                && journalEvent.Long("SystemAddress") is { } system
                && journalEvent.Int("Body") is { } body
                && journalEvent.Named("Genus") is { } genus)
            {
                if (!sampled.TryGetValue((system, body), out var genera))
                {
                    sampled[(system, body)] = genera = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                }

                genera.Add(genus);
            }
        }

        int bodies = 0, named = 0, sampledCount = 0, hits = 0;

        foreach (var biology in signals.All)
        {
            if (biology is not { SystemAddress: { } system, BodyId: { } body }
                || !sampled.TryGetValue((system, body), out var genera)
                || scans.For(system, body) is not { } scan
                || BiologyPotential.For(scan, biology.BiologicalCount, []) is not { } estimate)
            {
                continue;
            }

            bodies++;
            named += estimate.Genera.Count;
            sampledCount += genera.Count;
            hits += estimate.Genera.Count(genera.Contains);
        }

        return bodies == 0 || named == 0 || sampledCount == 0
            ? null
            : (bodies, 100.0 * hits / named, 100.0 * hits / sampledCount);
    }

}
