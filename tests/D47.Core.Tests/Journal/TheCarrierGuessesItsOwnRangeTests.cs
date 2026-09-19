using D47.Core.Journal;
using Xunit;

namespace D47.Core.Tests.Journal;

/// <summary>
/// <see cref="CarrierFuel.RoughRange"/>, based on #25's measurement of spansh plotting Sol to
/// Colonia (22,000 ly) at 3,524 t for an empty carrier (#307).
/// </summary>
public class TheCarrierGuessesItsOwnRangeTests
{
    [Fact]
    public void SolToColoniaLandsWithinAQuarterOfThePlottedDistance()
    {
        var (rangeLy, jumps) = CarrierFuel.RoughRange(totalTritium: 3524, usedSpace: 0, jumpRange: null);

        Assert.True(jumps > 0);

        var error = Math.Abs(rangeLy - 22000) / 22000;

        Assert.True(error <= 0.25, $"{rangeLy} ly is more than 25% off 22,000 ly.");
    }

    [Fact]
    public void ZeroTritiumGivesZeroRangeAndZeroJumps()
    {
        var (rangeLy, jumps) = CarrierFuel.RoughRange(totalTritium: 0, usedSpace: 0, jumpRange: 500);

        Assert.Equal(0, jumps);
        Assert.Equal(0, rangeLy);
    }

    [Fact]
    public void LessThanOneJumpsWorthGivesZeroJumps()
    {
        var (rangeLy, jumps) = CarrierFuel.RoughRange(totalTritium: 10, usedSpace: 0, jumpRange: 500);

        Assert.Equal(0, jumps);
        Assert.Equal(0, rangeLy);
    }

    /// <summary>A carrier with no <c>JumpRangeCurr</c> yet is costed at the default 500 ly.</summary>
    [Fact]
    public void AnUnknownJumpRangeFallsBackToFiveHundred()
    {
        var known = CarrierFuel.RoughRange(totalTritium: 1000, usedSpace: 0, jumpRange: 500);
        var unknown = CarrierFuel.RoughRange(totalTritium: 1000, usedSpace: 0, jumpRange: null);

        Assert.Equal(known, unknown);
    }
}
