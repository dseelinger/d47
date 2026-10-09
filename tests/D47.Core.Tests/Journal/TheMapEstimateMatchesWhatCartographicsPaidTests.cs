using D47.Core.Callouts;
using D47.Core.Capabilities;
using D47.Core.Capabilities.Builtin;
using D47.Core.Journal;
using D47.Core.Storage;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Journal;

/// <summary>
/// The Commander's sale of 2026-09-27T00:18:27Z, cut from their journals: every scan of a body in the systems it
/// lists since that system was last sold, the three DSS mappings among them, and the sale (#527).
/// </summary>
public class TheMapEstimateMatchesWhatCartographicsPaidTests
{
    private const double Tolerance = 0.02;

    private static readonly Lazy<IReadOnlyList<JournalEvent>> Fixture = new(() =>
    [
        .. EmbeddedFixture.Lines("cartography.Journal.2026-09-27T001827.01.log")
            .Select(line => JournalEvent.TryParse(line, NullLogger.Instance, out var parsed) ? parsed! : null)
            .OfType<JournalEvent>(),
    ]);

    [Fact]
    public void EveryBodyInTheSaleSumsToWithinTwoPercentOfWhatWasPaid()
    {
        var sale = Fixture.Value.Single(journalEvent => journalEvent.Kind == "MultiSellExplorationData");
        var systems = sale.Items("Discovered").Select(entry => entry.String("SystemName")).ToHashSet(StringComparer.Ordinal);

        var mapped = Fixture.Value
            .Where(journalEvent => journalEvent.Kind == "SAAScanComplete")
            .ToDictionary(
                journalEvent => (journalEvent.Long("SystemAddress"), journalEvent.Int("BodyID")),
                journalEvent => journalEvent);

        var estimate = 0L;

        foreach (var scans in Fixture.Value
                     .Where(journalEvent => journalEvent.Kind == "Scan" && systems.Contains(journalEvent.String("StarSystem")))
                     .GroupBy(journalEvent => (journalEvent.Long("SystemAddress"), journalEvent.Int("BodyID"))))
        {
            var mapping = mapped.GetValueOrDefault(scans.Key);
            var before = scans.Where(scan => mapping is null || scan.Timestamp <= mapping.Timestamp).ToList();
            var scan = scans.Last();

            var wasDiscovered = before.Any(earlier => Flag(earlier, "WasDiscovered"));
            var wasMapped = before.Any(earlier => Flag(earlier, "WasMapped"));

            if (scan.String("StarType") is { } starType)
            {
                estimate += CartographicValue.Star(starType, scan.Double("StellarMass") ?? 0, wasDiscovered);
            }
            else if (scan.String("PlanetClass") is { } planetClass)
            {
                estimate += CartographicValue.Planet(
                    planetClass,
                    scan.String("TerraformState"),
                    scan.Double("MassEM") ?? 0,
                    wasDiscovered,
                    wasMapped,
                    mapped: mapping is not null,
                    efficient: mapping?.Int("ProbesUsed") <= mapping?.Int("EfficiencyTarget"));
            }
        }

        var paid = (sale.Long("BaseValue") ?? 0) + (sale.Long("Bonus") ?? 0);

        Assert.True(
            Math.Abs(estimate - paid) <= paid * Tolerance,
            $"Estimated {estimate:N0} against {paid:N0} paid, {(estimate - paid) * 100.0 / paid:F2}%.");
    }

    [Fact]
    public void TheLedgerHoldsTheThreeMappedBodiesUntilTheSaleNamesTheirSystems()
    {
        var ledger = new CartographyLedger(null, new MemoryFileSystem(), NullLogger.Instance);
        ledger.FoldHistory([], TestContext.Current.CancellationToken);

        var beforeSale = Fixture.Value.TakeWhile(journalEvent => journalEvent.Kind != "MultiSellExplorationData").ToList();
        ledger.Apply(beforeSale, "F1");

        var held = ledger.Unsold("F1").Held;

        Assert.Equal(
            ["Praea Euq FV-Y c4 8", "Praea Euq FV-Y c4 9", "Praea Euq BF-A d95 5 e"],
            held.Select(map => map.BodyName));
        Assert.All(held, map => Assert.True(map.Efficient));

        // The water world's Detailed scan says WasMapped false; the AutoScan before it said true.
        Assert.InRange(held[0].Value!.Value, 1_600_000, 1_750_000);

        ledger.Apply([.. Fixture.Value.Skip(beforeSale.Count)]);

        Assert.Empty(ledger.Unsold("F1").Held);
    }

    /// <summary>The figures docs/capabilities/journal.md and callouts.md quote.</summary>
    [Fact]
    public async Task TheDocumentedAnswersAreTheOnesThisSaleGives()
    {
        var ledger = new CartographyLedger(null, new MemoryFileSystem(), NullLogger.Instance);
        ledger.FoldHistory([], TestContext.Current.CancellationToken);

        var gameState = new GameStateStore();
        gameState.Apply(Parse("""{"timestamp":"2026-09-26T18:00:00Z","event":"LoadGame","FID":"F1","Commander":"Fixture"}"""));

        var beforeSale = Fixture.Value.TakeWhile(journalEvent => journalEvent.Kind != "MultiSellExplorationData").ToList();
        ledger.Apply(beforeSale, "F1");

        var said = new MappingCallout { Ledger = ledger }.Examine(new CalloutContext(
            DateTimeOffset.UnixEpoch,
            IsPriming: false,
            State: gameState.Active,
            Status: GameStatus.Unknown,
            Route: NavRoute.None,
            Events: beforeSale));

        Assert.Equal(
            "Praea Euq BF-A d95 5 e mapped, efficiently. About 2,258 Cr. About 3.3 million unsold.",
            said.Last().Text);

        var answer = (await CapabilityRegistry
            .Build([JournalCapability.Create(gameState, cartography: ledger)])
            .InvokeAsync("get_unsold_exploration", ToolArguments.FromJson("{}"), TestContext.Current.CancellationToken)).Content;

        Assert.StartsWith(
            "About 3,277,078 credits of mapped bodies unsold, from 3 bodies. 3 of them were mapped efficiently.",
            answer,
            StringComparison.Ordinal);
    }

    private static JournalEvent Parse(string json)
    {
        Assert.True(JournalEvent.TryParse(json, NullLogger.Instance, out var parsed));
        return parsed!;
    }

    /// <summary>A missing flag reads as true, which claims no bonus, as the ledger reads it.</summary>
    private static bool Flag(JournalEvent scan, string property) =>
        !scan.Raw.TryGetProperty(property, out var flag) || flag.ValueKind != System.Text.Json.JsonValueKind.False;

}
