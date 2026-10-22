using D47.Core.Journal;
using Xunit;

namespace D47.Core.Tests.Journal;

public class ATravelPictureDrawsLegsBetweenJumpsTests
{
    private static readonly DateTimeOffset Start = new(2026, 10, 1, 0, 0, 0, TimeSpan.Zero);

    private static TravelStep Step(int day, long address, TravelArrival how, double x = 0) =>
        new(Start.AddDays(day), address, $"System {address}", new StarPosition(x, 0, 0), how);

    [Fact]
    public void JumpsJoinTheStepBeforeAndALocationStartsANewLine()
    {
        var picture = TravelPicture.Of(
        [
            Step(0, 1, TravelArrival.Location, 0),
            Step(1, 2, TravelArrival.Jump, 10),
            Step(2, 3, TravelArrival.CarrierJump, 20),
            Step(3, 4, TravelArrival.Location, 500),
            Step(4, 5, TravelArrival.Jump, 510),
        ], null);

        Assert.Equal(2, picture.Lines.Count);
        Assert.Equal([0.0, 10, 20], picture.Lines[0].Select(at => at.X));
        Assert.Equal([500.0, 510], picture.Lines[1].Select(at => at.X));
        Assert.Equal(3, picture.Jumps);
    }

    [Fact]
    public void EachSystemIsOneDotAtItsLatestVisit()
    {
        var picture = TravelPicture.Of(
        [
            Step(0, 1, TravelArrival.Location, 0),
            Step(1, 2, TravelArrival.Jump, 10),
            Step(2, 1, TravelArrival.Jump, 1),
        ], null);

        Assert.Equal(2, picture.Systems.Count);
        Assert.Contains(picture.Systems, at => at.X == 1);
        Assert.DoesNotContain(picture.Systems, at => at.X == 0);
    }

    [Fact]
    public void ARangeLeavesOutOlderStepsAndStartsItsFirstLineInsideIt()
    {
        var picture = TravelPicture.Of(
        [
            Step(0, 1, TravelArrival.Location, 0),
            Step(1, 2, TravelArrival.Jump, 10),
            Step(5, 3, TravelArrival.Jump, 20),
            Step(6, 4, TravelArrival.Jump, 30),
        ], Start.AddDays(5));

        var line = Assert.Single(picture.Lines);

        Assert.Equal([20.0, 30], line.Select(at => at.X));
        Assert.Equal(2, picture.Systems.Count);
        Assert.Equal(2, picture.Jumps);
    }

    [Fact]
    public void NoStepsDrawNothing()
    {
        var picture = TravelPicture.Of([], null);

        Assert.Empty(picture.Lines);
        Assert.Empty(picture.Systems);
        Assert.Equal(0, picture.Jumps);
    }
}
