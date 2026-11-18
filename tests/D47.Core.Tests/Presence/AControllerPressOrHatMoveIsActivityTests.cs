using D47.Core.Hotas;
using D47.Core.Presence;
using Xunit;

namespace D47.Core.Tests.Presence;

/// <summary>
/// A rise in any button's press count, a changed hat value or a rise in the axis move count is activity; nothing
/// else on a controller is.
/// </summary>
public class AControllerPressOrHatMoveIsActivityTests
{
    [Fact]
    public void ADevicesFirstReadingIsItsBaseline()
    {
        var activity = new ControllerActivity();

        Assert.False(activity.Moved([Reading("stick", presses: [3, 7], hats: [2])]));
    }

    [Fact]
    public void ARiseInAnyPressCountIsActivity()
    {
        var activity = new ControllerActivity();
        activity.Moved([Reading("stick", presses: [3, 7], hats: [0])]);

        Assert.True(activity.Moved([Reading("stick", presses: [3, 8], hats: [0])]));
        Assert.False(activity.Moved([Reading("stick", presses: [3, 8], hats: [0])]));
    }

    [Fact]
    public void AChangedHatIsActivity()
    {
        var activity = new ControllerActivity();
        activity.Moved([Reading("stick", presses: [0], hats: [0, 0])]);

        Assert.True(activity.Moved([Reading("stick", presses: [0], hats: [0, 4])]));
        Assert.True(activity.Moved([Reading("stick", presses: [0], hats: [0, 0])]));
        Assert.False(activity.Moved([Reading("stick", presses: [0], hats: [0, 0])]));
    }

    [Fact]
    public void AButtonHeldWithoutANewPressIsNot()
    {
        var activity = new ControllerActivity();
        activity.Moved([Reading("stick", presses: [1], hats: [], held: [true])]);

        Assert.False(activity.Moved([Reading("stick", presses: [1], hats: [], held: [true])]));
    }

    [Fact]
    public void APressOnOneDeviceIsActivityWhateverTheOthersDo()
    {
        var activity = new ControllerActivity();
        activity.Moved([Reading("stick", presses: [0], hats: []), Reading("throttle", presses: [5], hats: [])]);

        Assert.True(activity.Moved([Reading("stick", presses: [0], hats: []), Reading("throttle", presses: [6], hats: [])]));
    }

    [Fact]
    public void AnEmptyReadingKeepsTheBaseline()
    {
        var activity = new ControllerActivity();
        activity.Moved([Reading("stick", presses: [2], hats: [0])]);

        Assert.False(activity.Moved([]));
        Assert.False(activity.Moved([Reading("stick", presses: [2], hats: [0])]));
        Assert.True(activity.Moved([Reading("stick", presses: [3], hats: [0])]));
    }

    [Fact]
    public void ARiseInTheAxisMoveCountIsActivity()
    {
        var activity = new ControllerActivity();
        activity.Moved([Reading("throttle", presses: [0], hats: [], axisMoves: 12)]);

        Assert.True(activity.Moved([Reading("throttle", presses: [0], hats: [], axisMoves: 13)]));
        Assert.False(activity.Moved([Reading("throttle", presses: [0], hats: [], axisMoves: 13)]));
    }

    [Fact]
    public void AnAxisMoveCountThatStartsAgainIsANewBaseline()
    {
        var activity = new ControllerActivity();
        activity.Moved([Reading("throttle", presses: [0], hats: [], axisMoves: 12)]);

        Assert.False(activity.Moved([Reading("throttle", presses: [0], hats: [], axisMoves: 0)]));
        Assert.True(activity.Moved([Reading("throttle", presses: [0], hats: [], axisMoves: 1)]));
    }

    private static HotasReading Reading(string id, int[] presses, int[] hats, bool[]? held = null, int axisMoves = 0) => new()
    {
        Id = id,
        Buttons = held ?? new bool[presses.Length],
        Presses = presses,
        Hats = hats,
        AxisMoves = axisMoves,
    };
}
