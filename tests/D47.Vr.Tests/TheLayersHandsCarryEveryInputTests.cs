using System.Numerics;
using D47.Core.Vr;
using D47.XrLayer;
using Xunit;

namespace D47.Vr.Tests;

/// <summary>
/// Through the layer, each hand's record carries its own buttons and stick, the trigger and grip held by the
/// thresholds <see cref="OpenXrLayerRuntime.TriggerHeld"/> uses, and the bits sit where D47.Core's order puts them.
/// </summary>
public class TheLayersHandsCarryEveryInputTests
{
    [Fact]
    public void EachBlockBitIsTheBitOfTheCoreButtonOfTheSameName()
    {
        foreach (var bit in Enum.GetValues<XrButtons>().Where(b => b != XrButtons.None))
        {
            var button = Enum.Parse<VrButton>(bit.ToString());
            Assert.Equal(VrHand.Bit(button), (uint)bit);
        }

        Assert.Equal(
            [VrButton.Trigger, VrButton.Grip, VrButton.Primary, VrButton.Secondary, VrButton.Menu, VrButton.StickClick],
            Enum.GetValues<VrButton>());
    }

    [Fact]
    public void EachHandCarriesItsOwnButtonsAndStick()
    {
        using var block = new InMemoryXrBlock();
        var runtime = Started(block);

        try
        {
            block.Hand(0, trigger: 0f, grip: 0f, buttons: XrButtons.Primary | XrButtons.Menu, stickX: -0.5f, stickY: 0.25f);
            block.Hand(1, trigger: 0f, grip: 0f, buttons: XrButtons.StickClick, stickX: 0.75f, stickY: -1f);

            var hands = runtime.HandsAndHead().Hands;

            var left = Assert.Single(hands, hand => hand.Device == 0);
            Assert.Equal(VrHand.Bit(VrButton.Primary) | VrHand.Bit(VrButton.Menu), left.Held);
            Assert.Equal(new Vector2(-0.5f, 0.25f), left.Stick);

            var right = Assert.Single(hands, hand => hand.Device == 1);
            Assert.Equal(VrHand.Bit(VrButton.StickClick), right.Held);
            Assert.Equal(new Vector2(0.75f, -1f), right.Stick);
        }
        finally
        {
            runtime.Stop();
        }
    }

    [Fact]
    public void TheTriggerAndGripAreHeldPastPressedAtUntilBelowReleasedAtForEachHand()
    {
        using var block = new InMemoryXrBlock();
        var runtime = Started(block);

        try
        {
            block.Hand(0, trigger: 0.5f, grip: 0.5f);
            block.Hand(1, trigger: 1f, grip: 0f);
            Assert.Equal((0u, VrHand.Bit(VrButton.Trigger)), Held(runtime));

            block.Hand(0, trigger: 0.6f, grip: 0.6f);
            Assert.Equal((VrHand.Bit(VrButton.Trigger) | VrHand.Bit(VrButton.Grip), VrHand.Bit(VrButton.Trigger)), Held(runtime));

            block.Hand(0, trigger: 0.5f, grip: 0.4f);
            block.Hand(1, trigger: 0.4f, grip: 0f);
            Assert.Equal((VrHand.Bit(VrButton.Trigger), 0u), Held(runtime));
        }
        finally
        {
            runtime.Stop();
        }
    }

    [Fact]
    public void AStickThatIsNotANumberIsAtRest()
    {
        using var block = new InMemoryXrBlock();
        var runtime = Started(block);

        try
        {
            block.Hand(0, trigger: 0f, grip: 0f, stickX: float.NaN, stickY: 0.5f);

            Assert.Equal(Vector2.Zero, Assert.Single(runtime.HandsAndHead().Hands).Stick);
        }
        finally
        {
            runtime.Stop();
        }
    }

    private static OpenXrLayerRuntime Started(InMemoryXrBlock block)
    {
        var runtime = TheLayerDrawsIntoTheSpareTextureTests.Started(block, new FakeTextureDevice(), new FakeSurface());
        runtime.Pointing = true;
        return runtime;
    }

    private static (uint Left, uint Right) Held(OpenXrLayerRuntime runtime)
    {
        var hands = runtime.HandsAndHead().Hands;
        return (hands.Single(hand => hand.Device == 0).Held, hands.Single(hand => hand.Device == 1).Held);
    }
}
