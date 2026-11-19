using D47.Core.Journal;
using D47.Core.Knowledge;
using D47.Core.Storage;
using D47.Core.Tests.Knowledge;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Journal;

/// <summary>
/// A Scan of a star or planet marks its kind found in the codex region of the last arrival's <c>StarPos</c>, keeping the
/// first scan of each (kind, region) pair; a scan before any position, or of another system, is in no region. A found
/// kind reads as logged on Discoveries.
/// </summary>
public class AScannedKindIsFoundInItsRegionTests
{
    private const string KGiant = "star.k_yellow_orange_giant_star.giant";

    private static JournalEvent Event(string json)
    {
        Assert.True(JournalEvent.TryParse(json, NullLogger.Instance, out var parsed));
        return parsed!;
    }

    private static string Arrive(string time, string kind = "FSDJump", string system = "Fixture Sol", long address = 1, string position = "0.0,0.0,0.0") =>
        "{\"timestamp\":\"" + time + "\",\"event\":\"" + kind + "\",\"StarSystem\":\"" + system + "\",\"SystemAddress\":" + address
        + ",\"StarPos\":[" + position + "]}";

    private static string Star(string time, string body, string type = "K_OrangeGiant", string luminosity = "III", string scanType = "AutoScan", long address = 1) =>
        "{\"timestamp\":\"" + time + "\",\"event\":\"Scan\",\"ScanType\":\"" + scanType + "\",\"BodyName\":\"" + body
        + "\",\"StarSystem\":\"Fixture Sol\",\"SystemAddress\":" + address + ",\"StarType\":\"" + type + "\",\"Luminosity\":\"" + luminosity + "\"}";

    private static CodexLedger Fold(params string[] lines) =>
        lines.Aggregate(CodexLedger.None, (ledger, line) => ledger.Apply(Event(line)));

    [Fact]
    public void AKGiantScannedAfterAJumpIsFoundInRegionEighteen()
    {
        var ledger = Fold(Arrive("2026-11-01T09:00:00Z"), Star("2026-11-01T09:01:00Z", "Fixture Sol A"));

        Assert.Equal(
            new CodexFoundKind(KGiant, 18, DateTimeOffset.Parse("2026-11-01T09:01:00Z", System.Globalization.CultureInfo.InvariantCulture), "Fixture Sol", 1, "Fixture Sol A"),
            ledger.Found(KGiant, 18));
        Assert.Null(ledger.Found("star.k_yellow_orange_star.dwarf", 18));
    }

    [Fact]
    public void ALaterScanOfTheSameKindInTheRegionLeavesTheFirst()
    {
        var ledger = Fold(
            Arrive("2026-11-01T09:00:00Z"),
            Star("2026-11-01T09:01:00Z", "Fixture Sol A"),
            Arrive("2026-11-01T10:00:00Z", system: "Fixture Sol Two", address: 2, position: "5.0,0.0,5.0"),
            Star("2026-11-01T10:01:00Z", "Fixture Sol Two A", address: 2));

        Assert.Equal("Fixture Sol A", ledger.Found(KGiant, 18)!.Body);
    }

    [Theory]
    [InlineData("Location")]
    [InlineData("CarrierJump")]
    public void ALocationOrCarrierJumpSetsThePositionToo(string kind)
    {
        var ledger = Fold(Arrive("2026-11-01T09:00:00Z", kind), Star("2026-11-01T09:01:00Z", "Fixture Sol A"));

        Assert.NotNull(ledger.Found(KGiant, 18));
    }

    [Fact]
    public void AScanBeforeAnyPositionIsInNoRegion()
    {
        var ledger = Fold(Star("2026-11-01T09:01:00Z", "Fixture Sol A"), Arrive("2026-11-01T09:02:00Z"));

        Assert.Null(ledger.Found(KGiant, 18));
    }

    [Fact]
    public void AScanOfAnotherSystemThanTheLastArrivalIsInNoRegion()
    {
        var ledger = Fold(Arrive("2026-11-01T09:00:00Z"), Star("2026-11-01T09:01:00Z", "Fixture Elsewhere A", address: 9));

        Assert.Null(ledger.Found(KGiant, 18));
    }

    [Theory]
    [InlineData("AutoScan")]
    [InlineData("Detailed")]
    [InlineData("NavBeaconDetail")]
    [InlineData("Basic")]
    public void EveryScanTypeCounts(string scanType)
    {
        var ledger = Fold(Arrive("2026-11-01T09:00:00Z"), Star("2026-11-01T09:01:00Z", "Fixture Sol A", scanType: scanType));

        Assert.NotNull(ledger.Found(KGiant, 18));
    }

    [Fact]
    public void AMergeKeepsTheWalksFirstScanAndAddsTheLiveJournalsNewKinds()
    {
        var walk = Fold(Arrive("2026-11-01T09:00:00Z"), Star("2026-11-01T09:01:00Z", "Fixture Sol A"));
        var live = Fold(
            Arrive("2026-11-02T09:00:00Z"),
            Star("2026-11-02T09:01:00Z", "Fixture Sol B"),
            Star("2026-11-02T09:02:00Z", "Fixture Sol C", type: "N", luminosity: "VII"));

        var merged = walk.With(live);

        Assert.Equal("Fixture Sol A", merged.Found(KGiant, 18)!.Body);
        Assert.Equal("Fixture Sol C", merged.Found("star.neutron_star", 18)!.Body);
    }

    [Fact]
    public void ANewCommanderForgetsTheFoundKinds()
    {
        var ledger = Fold(
            Arrive("2026-11-01T09:00:00Z"),
            Star("2026-11-01T09:01:00Z", "Fixture Sol A"),
            "{\"timestamp\":\"2026-11-01T09:02:00Z\",\"event\":\"NewCommander\",\"FID\":\"F1\",\"Name\":\"Again\"}");

        Assert.Null(ledger.Found(KGiant, 18));
    }

    [Fact]
    public void AFoundKindReadsAsLoggedOnDiscoveriesAndACanonnRowAsBefore()
    {
        var table = CodexTable.Parse("2\tBeta tree\tBiological and Geological\tOrganic structures\t18:30\n" + KindRows.RockyBodyAndNeutronStar());
        var ledger = Fold(Arrive("2026-11-01T09:00:00Z"), Star("2026-11-01T09:01:00Z", "Fixture Sol C", type: "N", luminosity: "VII"));

        var here = CodexDiscoveries.For(ledger, table, 18);
        var there = CodexDiscoveries.For(ledger, table, 34);

        var star = Assert.Single(here.Logged);
        Assert.Equal(("star.neutron_star", "Fixture Sol C"), (star.Entry.Key, star.Found!.Body));
        Assert.Null(star.Logged);
        Assert.Contains(here.Missing, item => item.Entry.Key == "2");
        Assert.Empty(there.Logged);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public void AKindFoundInAnOlderJournalIsInTheLedgerTheWalkRestores()
    {
        var path = Path.Combine(RepositoryFolder.Root(), "tests", "fixtures", "codex", "scan-kinds.log");
        var walked = CodexLedgerBackfill.FromHistory(new DiskFileSystem(), [path], NullLogger.Instance, TestContext.Current.CancellationToken)["F1"];

        var restored = walked.With(CodexLedger.None);

        Assert.Equal("Fixture Sol A", restored.Found(KGiant, 18)?.Body);
        Assert.Equal("Fixture Sol A 1", restored.Found("planet.rocky_body.terraformable.atmosphere.thin_carbon_dioxide", 18)?.Body);
    }
}
