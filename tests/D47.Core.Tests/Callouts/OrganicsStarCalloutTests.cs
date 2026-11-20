using System.Text.Json;
using D47.Core.Callouts;
using D47.Core.Journal;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Callouts;

/// <summary>An undiscovered F or G arrival star is worth a full scan for organics.</summary>
public class OrganicsStarCalloutTests
{
    private static JournalEvent AutoScan(string starType, bool wasDiscovered = false, long address = 1234L)
    {
        var payload = new Dictionary<string, object?>
        {
            ["timestamp"] = "2026-09-13T17:00:00Z",
            ["event"] = "Scan",
            ["ScanType"] = "AutoScan",
            ["StarType"] = starType,
            ["DistanceFromArrivalLS"] = 0d,
            ["SystemAddress"] = address,
            ["WasDiscovered"] = wasDiscovered,
        };

        Assert.True(JournalEvent.TryParse(JsonSerializer.Serialize(payload), NullLogger.Instance, out var parsed));
        return parsed!;
    }

    private static CalloutContext Context(JournalEvent journalEvent, bool priming = false) =>
        new(DateTimeOffset.UnixEpoch, priming, null, GameStatus.Unknown, NavRoute.None, [journalEvent]);

    [Fact]
    public void AnUndiscoveredFStarIsWorthAFullScan()
    {
        var said = Assert.Single(new OrganicsStarCallout().Examine(Context(AutoScan("F"))));

        Assert.Equal("organicsStars.1234", said.Key);
        Assert.Equal("Class F star. Worth a full scan for organics.", said.Text);
    }

    [Fact]
    public void AnUndiscoveredGSuperGiantIsWorthAFullScan()
    {
        var said = Assert.Single(new OrganicsStarCallout().Examine(Context(AutoScan("G_WhiteSuperGiant"))));

        Assert.Equal("Class G star. Worth a full scan for organics.", said.Text);
    }

    [Fact]
    public void ADiscoveredGStarSaysNothing()
    {
        Assert.Empty(new OrganicsStarCallout().Examine(Context(AutoScan("G", wasDiscovered: true))));
    }

    [Theory]
    [InlineData("K")]
    [InlineData("A")]
    [InlineData("M")]
    [InlineData("D")]
    [InlineData("N")]
    public void AnUndiscoveredStarOfAnotherClassSaysNothing(string starType)
    {
        Assert.Empty(new OrganicsStarCallout().Examine(Context(AutoScan(starType))));
    }

    [Fact]
    public void TheSameStarScannedTwiceIsSaidOnce()
    {
        var callout = new OrganicsStarCallout();

        Assert.Single(callout.Examine(Context(AutoScan("F"))));
        Assert.Empty(callout.Examine(Context(AutoScan("F"))));
    }

    [Fact]
    public void PrimingSaysNothing()
    {
        Assert.Empty(new OrganicsStarCallout().Examine(Context(AutoScan("F"), priming: true)));
    }
}
