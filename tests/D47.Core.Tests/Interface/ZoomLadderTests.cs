using D47.Core.Interface;
using Xunit;

namespace D47.Core.Tests.Interface;

public class ZoomLadderTests
{
    [Fact]
    public void StepsAreAscendingAndIncludeOneHundred()
    {
        Assert.Equal(ZoomLadder.Steps.OrderBy(step => step), ZoomLadder.Steps);
        Assert.Contains(ZoomLadder.Default, ZoomLadder.Steps);
    }

    [Theory]
    [InlineData(100, 110)]
    [InlineData(110, 125)]
    [InlineData(250, 300)]
    public void ZoomingInMovesOneRung(int from, int to) => Assert.Equal(to, ZoomLadder.In(from));

    [Theory]
    [InlineData(100, 90)]
    [InlineData(67, 50)]
    public void ZoomingOutMovesOneRung(int from, int to) => Assert.Equal(to, ZoomLadder.Out(from));

    [Fact]
    public void TheLadderHasEndsRatherThanWrappingRoundThem()
    {
        Assert.Equal(ZoomLadder.Maximum, ZoomLadder.In(ZoomLadder.Maximum));
        Assert.Equal(ZoomLadder.Minimum, ZoomLadder.Out(ZoomLadder.Minimum));
    }

    /// <summary>
    /// The settings file is hand-editable, so an off-ladder value is a case this has to answer
    /// for. Snapping keeps the gestures working from wherever it landed; rejecting would leave
    /// a level nothing can step off.
    /// </summary>
    [Theory]
    [InlineData(137, 125)]
    [InlineData(0, 50)]
    [InlineData(10_000, 300)]
    public void AnOffLadderValueSnapsToTheNearestRung(int given, int expected) =>
        Assert.Equal(expected, ZoomLadder.Snap(given));

    [Fact]
    public void SteppingFromBetweenRungsMovesTowardsTheDirectionOfTravel()
    {
        // 137 sits between 125 and 150. One step out is 125, not 110 — otherwise a stray value
        // costs the Commander a rung the first time they touch the gesture.
        Assert.Equal(125, ZoomLadder.Out(137));
        Assert.Equal(150, ZoomLadder.In(137));
    }

    [Fact]
    public void WalkingTheWholeLadderInBothDirectionsVisitsEveryRung()
    {
        var up = new List<int> { ZoomLadder.Minimum };
        while (up[^1] != ZoomLadder.Maximum)
        {
            up.Add(ZoomLadder.In(up[^1]));
        }

        Assert.Equal(ZoomLadder.Steps, up);

        var down = new List<int> { ZoomLadder.Maximum };
        while (down[^1] != ZoomLadder.Minimum)
        {
            down.Add(ZoomLadder.Out(down[^1]));
        }

        Assert.Equal(ZoomLadder.Steps.Reverse(), down);
    }

    [Fact]
    public void ScaleOfIsTheMultiplierATransformWants()
    {
        Assert.Equal(1.0, ZoomLadder.ScaleOf(100));
        Assert.Equal(1.5, ZoomLadder.ScaleOf(150));
    }
}
