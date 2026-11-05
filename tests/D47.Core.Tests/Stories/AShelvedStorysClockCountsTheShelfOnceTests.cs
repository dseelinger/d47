using D47.Core.Persona;
using D47.Core.Stories;
using Xunit;

namespace D47.Core.Tests.Stories;

/// <summary>A shelved story's clock stops: its time since the beacon is the same when it comes back, and grows only while it runs.</summary>
public sealed class AShelvedStorysClockCountsTheShelfOnceTests
{
    private static readonly DateTimeOffset Scan = new(2026, 9, 1, 12, 0, 0, TimeSpan.Zero);

    private static Story Running() => new()
    {
        Id = "a-story",
        Title = "A Story",
        PublicLayer = "-",
        PickedAt = Scan.AddDays(-1),
        BeaconScanAt = Scan,
        PausedFor = TimeSpan.FromHours(5),
    };

    [Fact]
    public void AStoryPausedWhenShelvedComesBackWithTheSameClock()
    {
        var paused = Running().Paused(Scan.AddDays(2));
        var before = paused.SinceBeacon(Scan.AddDays(3));

        var shelved = paused.Shelved(Scan.AddDays(3));

        Assert.Equal(StoryState.Shelved, shelved.State);
        Assert.True(shelved.IsOff);
        Assert.Null(shelved.PausedAt);
        Assert.Equal(before, shelved.SinceBeacon(Scan.AddDays(30)));

        var back = shelved.Unshelved(Scan.AddDays(40));

        Assert.Equal(StoryState.Paused, back.State);
        Assert.False(back.IsOff);
        Assert.Equal(before, back.SinceBeacon(Scan.AddDays(41)));

        var resumed = back.Resumed(Scan.AddDays(42));

        Assert.Equal(before + TimeSpan.FromHours(6), resumed.SinceBeacon(Scan.AddDays(42).AddHours(6)));
    }

    [Fact]
    public void ARunningStoryShelvedAndBroughtBackLosesOnlyTheShelf()
    {
        var running = Running();
        var before = running.SinceBeacon(Scan.AddDays(3));

        var back = running.Shelved(Scan.AddDays(3)).Unshelved(Scan.AddDays(10));

        Assert.Equal(before, back.SinceBeacon(Scan.AddDays(12)));
        Assert.Equal(before + TimeSpan.FromDays(1), back.Resumed(Scan.AddDays(12)).SinceBeacon(Scan.AddDays(13)));
    }

    [Fact]
    public void AShelvedStoryIsNeitherCurrentNorHoldingCores()
    {
        var shelved = Running() with { BeaconScanAt = null };

        Assert.Equal(HeldCores.All, shelved.HeldCores);

        shelved = shelved.Shelved(Scan);

        Assert.False(shelved.IsCurrent);
        Assert.True(shelved.IsCurrentOrShelved);
        Assert.Equal(HeldCores.None, shelved.HeldCores);
    }
}
