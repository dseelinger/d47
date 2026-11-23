using D47.Core.Persona;
using D47.Core.Stories;
using Xunit;

namespace D47.Core.Tests.Stories;

/// <summary>A shelved story's clock keeps running: the days on the shelf count toward its time since the beacon.</summary>
public sealed class AShelvedStorysClockKeepsRunningTests
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
    public void AStoryPausedWhenShelvedComesBackWithTheShelfCounted()
    {
        var paused = Running().Paused(Scan.AddDays(2));

        var shelved = paused.Shelved(Scan.AddDays(3));

        Assert.Equal(StoryState.Shelved, shelved.State);
        Assert.True(shelved.IsOff);
        Assert.Null(shelved.PausedAt);
        Assert.Equal(TimeSpan.FromDays(30), shelved.SinceBeacon(Scan.AddDays(30)));

        var back = shelved.Unshelved(Scan.AddDays(40));

        Assert.Equal(StoryState.Paused, back.State);
        Assert.False(back.IsOff);
        Assert.Equal(TimeSpan.FromDays(41), back.SinceBeacon(Scan.AddDays(41)));

        var resumed = back.Resumed(Scan.AddDays(42));

        Assert.Equal(TimeSpan.FromDays(42), resumed.SinceBeacon(Scan.AddDays(42)));
    }

    [Fact]
    public void ARunningStoryShelvedAndBroughtBackKeepsTheShelfOnItsClock()
    {
        var back = Running().Shelved(Scan.AddDays(3)).Unshelved(Scan.AddDays(10));

        Assert.Equal(TimeSpan.FromDays(12), back.SinceBeacon(Scan.AddDays(12)));
        Assert.Equal(TimeSpan.FromDays(13), back.Resumed(Scan.AddDays(12)).SinceBeacon(Scan.AddDays(13)));
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
