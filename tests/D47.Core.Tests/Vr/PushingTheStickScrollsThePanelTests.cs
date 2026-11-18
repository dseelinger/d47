using System.Numerics;
using D47.Core.Interface;
using D47.Core.Vr;
using Xunit;

namespace D47.Core.Tests.Vr;

public class PushingTheStickScrollsThePanelTests
{
    private const uint Left = 1;
    private const uint Right = 2;
    private static readonly DateTimeOffset T0 = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
    private static readonly TimeSpan Every = VrStickScroll.RepeatEvery;

    private static VrHand Hand(uint device, float x, float y) =>
        new(device, default, default, 0, new Vector2(x, y));

    private static IReadOnlyList<VrHand> Hands(float leftY, float rightY = 0f, float rightX = 0f) =>
        [Hand(Left, 0f, leftY), Hand(Right, rightX, rightY)];

    [Fact]
    public void StickUpScrollsUpOnceThenRepeatsAtTheInterval()
    {
        var scroll = new VrStickScroll();

        Assert.Equal(PanelScrollStep.LineUp, scroll.Step(Hands(0f, 1f), Right, false, T0));
        Assert.Null(scroll.Step(Hands(0f, 1f), Right, false, T0 + (Every / 2)));
        Assert.Equal(PanelScrollStep.LineUp, scroll.Step(Hands(0f, 1f), Right, false, T0 + Every));
        Assert.Null(scroll.Step(Hands(0f, 1f), Right, false, T0 + Every));
    }

    [Fact]
    public void StickDownScrollsDown()
    {
        var scroll = new VrStickScroll();

        Assert.Equal(PanelScrollStep.LineDown, scroll.Step(Hands(0f, -1f), Right, false, T0));
    }

    [Fact]
    public void TheReleaseThresholdIsLowerThanThePressThreshold()
    {
        var scroll = new VrStickScroll();
        var between = (VrStickScroll.PressedAt + VrStickScroll.ReleasedAt) / 2f;

        Assert.Null(scroll.Step(Hands(0f, between), Right, false, T0));
        Assert.NotNull(scroll.Step(Hands(0f, 1f), Right, false, T0));
        Assert.NotNull(scroll.Step(Hands(0f, between), Right, false, T0 + Every));
        Assert.Null(scroll.Step(Hands(0f, VrStickScroll.ReleasedAt - 0.05f), Right, false, T0 + (Every * 2)));
        Assert.Null(scroll.Step(Hands(0f, between), Right, false, T0 + (Every * 3)));
    }

    [Fact]
    public void LeftAndRightDoNothing()
    {
        var scroll = new VrStickScroll();

        Assert.Null(scroll.Step(Hands(0f, 0f, 1f), Right, false, T0));
        Assert.Null(scroll.Step(Hands(0f, 0f, -1f), Right, false, T0));
    }

    [Fact]
    public void TheOtherHandsStickDoesNothing()
    {
        var scroll = new VrStickScroll();

        Assert.Null(scroll.Step(Hands(1f, 0f), Right, false, T0));
    }

    [Fact]
    public void ARayOffThePanelScrollsNothing()
    {
        var scroll = new VrStickScroll();

        Assert.Null(scroll.Step(Hands(0f, 1f), null, false, T0));
    }

    [Fact]
    public void ACarryOrAResizeScrollsNothing()
    {
        var scroll = new VrStickScroll();

        Assert.Null(scroll.Step(Hands(0f, 1f), Right, true, T0));
        Assert.NotNull(scroll.Step(Hands(0f, 1f), Right, false, T0));
    }
}
