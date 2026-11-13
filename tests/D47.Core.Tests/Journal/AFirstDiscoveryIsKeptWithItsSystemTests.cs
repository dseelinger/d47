using D47.Core.Journal;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Journal;

public class AFirstDiscoveryIsKeptWithItsSystemTests
{
    private static TravelLog Log() => new(new MemoryInstall().Files, NullLogger.Instance);

    private static string Jump(string time, string system, long address) =>
        "{\"timestamp\":\"" + time + "\",\"event\":\"FSDJump\",\"StarSystem\":\"" + system + "\",\"SystemAddress\":" + address + ",\"StarPos\":[" + address + ",0,0]}";

    private static string Scan(string time, string system, long address, int body, bool discovered, string type = "Detailed") =>
        "{\"timestamp\":\"" + time + "\",\"event\":\"Scan\",\"ScanType\":\"" + type + "\",\"BodyName\":\"" + system + " " + body
        + "\",\"BodyID\":" + body + ",\"StarSystem\":\"" + system + "\",\"SystemAddress\":" + address
        + ",\"WasDiscovered\":" + (discovered ? "true" : "false") + ",\"WasMapped\":false}";

    private const string Load = "{\"timestamp\":\"2026-10-01T09:00:00Z\",\"event\":\"LoadGame\",\"FID\":\"F1\"}";

    [Fact]
    public void ScansInTwoSystemsPlaceOnlyTheSystemWithUndiscoveredBodiesAndCountItsBodies()
    {
        var log = Log();

        log.Fold(
        [
            Load,
            Jump("2026-10-01T09:05:00Z", "Alpha", 1),
            Scan("2026-10-01T09:06:00Z", "Alpha", 1, 0, discovered: false, "AutoScan"),
            Scan("2026-10-01T09:07:00Z", "Alpha", 1, 0, discovered: false),
            Scan("2026-10-01T09:08:00Z", "Alpha", 1, 3, discovered: false),
            Jump("2026-10-01T09:10:00Z", "Beta", 2),
            Scan("2026-10-01T09:11:00Z", "Beta", 2, 0, discovered: true, "AutoScan"),
            Scan("2026-10-01T09:12:00Z", "Beta", 2, 1, discovered: true),
        ]);

        var place = Assert.Single(log.FirstDiscoveries("F1"));
        Assert.Equal("Alpha", place.System);
        Assert.Equal(1, place.SystemAddress);
        Assert.Equal(new StarPosition(1, 0, 0), place.Position);
        Assert.Equal(2, place.Bodies);
        Assert.Equal(2, log.FirstDiscoveryCount("F1"));
    }

    [Fact]
    public void ANavBeaconScanIsNotAFirstDiscovery()
    {
        var log = Log();

        log.Fold([Load, Jump("2026-10-01T09:05:00Z", "Alpha", 1), Scan("2026-10-01T09:06:00Z", "Alpha", 1, 2, discovered: false, "NavBeaconDetail")]);

        Assert.Empty(log.FirstDiscoveries("F1"));
        Assert.Equal(0, log.FirstDiscoveryCount("F1"));
    }

    [Fact]
    public void ASystemWithNoArrivalIsNotPlaced()
    {
        var log = Log();

        log.Fold([Load, Jump("2026-10-01T09:05:00Z", "Alpha", 1), Scan("2026-10-01T09:06:00Z", "Elsewhere", 9, 0, discovered: false)]);

        Assert.Empty(log.FirstDiscoveries("F1"));
        Assert.Equal(1, log.FirstDiscoveryCount("F1"));
    }

    [Fact]
    public void ALiveScanIsAddedAndARepeatOfAFoldedOneIsNot()
    {
        var log = Log();
        var scan = Scan("2026-10-01T09:06:00Z", "Alpha", 1, 4, discovered: false);

        log.Fold([Load, Jump("2026-10-01T09:05:00Z", "Alpha", 1), scan]);
        log.Apply([Event(Load), Event(scan), Event(Scan("2026-10-01T09:07:00Z", "Alpha", 1, 5, discovered: false))]);

        Assert.Equal(2, Assert.Single(log.FirstDiscoveries("F1")).Bodies);
    }

    [Fact]
    public void ANewCommanderErasesTheDiscoveriesBeforeIt()
    {
        var log = Log();

        log.Fold(
        [
            Load,
            Jump("2026-10-01T09:05:00Z", "Alpha", 1),
            Scan("2026-10-01T09:06:00Z", "Alpha", 1, 0, discovered: false),
            "{\"timestamp\":\"2026-10-02T09:00:00Z\",\"event\":\"NewCommander\",\"FID\":\"F1\"}",
        ]);

        Assert.Equal(0, log.FirstDiscoveryCount("F1"));
    }

    private static JournalEvent Event(string line)
    {
        Assert.True(JournalEvent.TryParse(line, NullLogger.Instance, out var parsed));

        return parsed!;
    }
}
