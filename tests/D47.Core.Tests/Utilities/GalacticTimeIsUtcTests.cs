using D47.Core.Utilities;
using Xunit;

namespace D47.Core.Tests.Utilities;

public class GalacticTimeIsUtcTests
{
    /// <summary>21:04 UTC on 17 August 2026.</summary>
    private static readonly DateTimeOffset Instant =
        new(2026, 8, 17, 21, 4, 0, TimeSpan.Zero);

    private static TimeZoneInfo Zone(double hours) =>
        TimeZoneInfo.CreateCustomTimeZone(
            $"t{hours:+0.#;-0.#}", TimeSpan.FromHours(hours), "t", "t");

    /// <summary>
    /// The same instant reads the same out there whoever is looking at it, which is what makes it agree
    /// with the station clock the Commander is docked at.
    /// </summary>
    [Theory]
    [InlineData(0)]
    [InlineData(3)]
    [InlineData(-5)]
    [InlineData(9.5)]
    public void EveryCommanderSeesTheSameGalacticTime(double offset)
    {
        Assert.Equal("21:04", GalacticTime.Read(Instant, Zone(offset)).GalacticTimeOfDay);
    }

    /// <summary>And the same galactic date, including across a local midnight.</summary>
    [Theory]
    [InlineData(0)]
    [InlineData(3)]
    [InlineData(-5)]
    public void AndTheSameGalacticDate(double offset)
    {
        Assert.Equal("17 August 3312", GalacticTime.Read(Instant, Zone(offset)).GalacticDate);
    }

    /// <summary>The Commander's own clock is still theirs, which is the half that was right.</summary>
    [Fact]
    public void TheLocalClockStillFollowsTheZone()
    {
        Assert.Equal("21:04", GalacticTime.Read(Instant, Zone(0)).LocalTimeOfDay);
        Assert.Equal("00:04", GalacticTime.Read(Instant, Zone(3)).LocalTimeOfDay);
        Assert.Equal("16:04", GalacticTime.Read(Instant, Zone(-5)).LocalTimeOfDay);
    }

    /// <summary>
    /// One instant presented twice, so the two sides cannot drift: whatever the zone, the galactic
    /// reading is exactly 1286 years after the UTC instant.
    /// </summary>
    [Fact]
    public void TheTwoSidesAreOneInstant()
    {
        var clocks = GalacticTime.Read(Instant, Zone(-5));

        Assert.Equal(Instant.UtcDateTime.AddYears(GalacticTime.YearsAhead), clocks.Galactic.UtcDateTime);
        Assert.Equal(Instant.UtcDateTime, clocks.Local.UtcDateTime);
    }

    /// <summary>
    /// A Commander three hours ahead sees two different clocks, which is the point of there being two
    /// of them.
    /// </summary>
    [Fact]
    public void TheClocksDifferWhereverTheCommanderIsNotOnUtc()
    {
        var ahead = GalacticTime.Read(Instant, Zone(3));

        Assert.NotEqual(ahead.LocalTimeOfDay, ahead.GalacticTimeOfDay);

        var greenwich = GalacticTime.Read(Instant, Zone(0));

        Assert.Equal(greenwich.LocalTimeOfDay, greenwich.GalacticTimeOfDay);
    }
}
