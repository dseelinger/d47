using D47.Core.Journal;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Persona;

/// <summary><c>tests/fixtures/beacons</c>: a data point scanned elsewhere, then a jump to a beacon and a scan there.</summary>
internal static class BeaconFixture
{
    /// <summary>The events before the jump to the beacon system.</summary>
    public const int BeforeTheBeacon = 5;

    public static IReadOnlyList<JournalEvent> Events()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "d47.slnx")))
        {
            directory = directory.Parent;
        }

        Assert.NotNull(directory);

        return [.. File.ReadAllLines(
                Path.Combine(directory.FullName, "tests", "fixtures", "beacons", "Journal.2026-09-30T120000.01.log"))
            .Where(line => line.Length > 0)
            .Select(Event)];
    }

    public static JournalEvent Event(string json)
    {
        Assert.True(JournalEvent.TryParse(json, NullLogger.Instance, out var parsed));
        return parsed!;
    }

    public static JournalEvent JumpTo(string system, long address) => Event(
        $$"""{ "timestamp":"2026-09-30T13:00:00Z", "event":"FSDJump", "StarSystem":"{{system}}", "SystemAddress":{{address}} }""");

    public static JournalEvent DataPoint() => Event(
        """{ "timestamp":"2026-09-30T13:05:00Z", "event":"DataScanned", "Type":"$Datascan_DataPoint;" }""");
}
