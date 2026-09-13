using System.Text.Json;
using D47.Core.Callouts;
using D47.Core.Journal;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Callouts;

/// <summary>The arrival star's autoscan, when nobody has sold data on it yet.</summary>
public class DiscoveryCalloutTests
{
    private static JournalEvent Event(string kind, params (string Key, object? Value)[] fields)
    {
        var payload = new Dictionary<string, object?>
        {
            ["timestamp"] = "2026-09-13T17:00:00Z",
            ["event"] = kind,
        };

        foreach (var (key, value) in fields)
        {
            payload[key] = value;
        }

        Assert.True(JournalEvent.TryParse(JsonSerializer.Serialize(payload), NullLogger.Instance, out var parsed));
        return parsed!;
    }

    private static JournalEvent AutoScan(bool wasDiscovered, double distance = 0, string starType = "K") =>
        Event(
            "Scan",
            ("ScanType", "AutoScan"),
            ("StarType", starType),
            ("DistanceFromArrivalLS", distance),
            ("SystemAddress", 1234L),
            ("WasDiscovered", wasDiscovered));

    private static CalloutContext Context(JournalEvent journalEvent, bool priming = false) =>
        new(DateTimeOffset.UnixEpoch, priming, null, GameStatus.Unknown, NavRoute.None, [journalEvent]);

    [Fact]
    public void AnUndiscoveredArrivalStarIsAnnounced()
    {
        var said = Assert.Single(new DiscoveryCallout().Examine(Context(AutoScan(wasDiscovered: false))));

        Assert.Equal("discovery.1234", said.Key);
        Assert.Contains("Undiscovered system", said.Text, StringComparison.Ordinal);
    }

    [Fact]
    public void ADiscoveredArrivalStarSaysNothing()
    {
        Assert.Empty(new DiscoveryCallout().Examine(Context(AutoScan(wasDiscovered: true))));
    }

    [Fact]
    public void ANavBeaconScanSaysNothing()
    {
        var scan = Event(
            "Scan",
            ("ScanType", "NavBeaconDetail"),
            ("StarType", "K"),
            ("DistanceFromArrivalLS", 0d),
            ("SystemAddress", 1234L),
            ("WasDiscovered", false));

        Assert.Empty(new DiscoveryCallout().Examine(Context(scan)));
    }

    [Fact]
    public void AScanAwayFromArrivalSaysNothing()
    {
        Assert.Empty(new DiscoveryCallout().Examine(Context(AutoScan(wasDiscovered: false, distance: 12.4))));
    }

    [Fact]
    public void PrimingSaysNothing()
    {
        Assert.Empty(new DiscoveryCallout().Examine(Context(AutoScan(wasDiscovered: false), priming: true)));
    }
}
