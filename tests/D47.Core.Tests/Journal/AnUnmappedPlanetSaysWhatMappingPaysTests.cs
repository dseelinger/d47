using D47.Core.Journal;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Journal;

/// <summary>What a scanned planet would pay if the Commander mapped it efficiently, for On this body (#555).</summary>
public class AnUnmappedPlanetSaysWhatMappingPaysTests
{
    private const long SystemAddress = 3274702866819;

    private static JournalEvent Parse(string json)
    {
        Assert.True(JournalEvent.TryParse(json, NullLogger.Instance, out var parsed));
        return parsed!;
    }

    private static string Scan(bool wasDiscovered, bool wasMapped) =>
        $$"""
          {"timestamp":"2026-10-04T16:00:00Z","event":"Scan","ScanType":"Detailed","BodyName":"Praea Euq BF-A d95 1",
           "BodyID":1,"StarSystem":"Praea Euq BF-A d95","SystemAddress":{{SystemAddress}},
           "PlanetClass":"High metal content body","TerraformState":"","MassEM":1.738816,
           "WasDiscovered":{{(wasDiscovered ? "true" : "false")}},"WasMapped":{{(wasMapped ? "true" : "false")}}}
          """;

    private static CartographyLedger Ledger(params string[] lines)
    {
        var ledger = new CartographyLedger(null, NullLogger.Instance);
        ledger.Apply([.. lines.Select(Parse)]);
        return ledger;
    }

    [Fact]
    public void AnUndiscoveredPlanetIsPricedWithBothFirstBonuses()
    {
        var ledger = Ledger(Scan(wasDiscovered: false, wasMapped: false));

        Assert.Equal(
            CartographicValue.Planet("High metal content body", "", 1.738816, wasDiscovered: false, wasMapped: false, mapped: true, efficient: true),
            ledger.IfMapped(SystemAddress, 1));
    }

    [Fact]
    public void APlanetSomeoneMappedIsPricedWithoutTheMappersBonus()
    {
        var ledger = Ledger(Scan(wasDiscovered: true, wasMapped: true));

        Assert.Equal(
            CartographicValue.Planet("High metal content body", "", 1.738816, wasDiscovered: true, wasMapped: true, mapped: true, efficient: true),
            ledger.IfMapped(SystemAddress, 1));
    }

    [Fact]
    public void ABodyWithNoScanHasNoFigure() => Assert.Null(Ledger().IfMapped(SystemAddress, 1));
}
