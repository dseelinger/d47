using D47.Core.Vr;
using Xunit;

namespace D47.Vr.Tests;

/// <summary>
/// Through the layer, the hands come from the layer's half of the block, the trigger is read only while the
/// panel wants it, and a grip is one back press per hold.
/// </summary>
public class TheLayersControllersPressOncePerHoldTests
{
    [Fact]
    public void OnlyValidHandsAreReadAndOnlyWhilePointing()
    {
        using var block = new InMemoryXrBlock();
        var runtime = TheLayerDrawsIntoTheSpareTextureTests.Started(block, new FakeTextureDevice(), new FakeSurface());

        try
        {
            block.HeadAt(1.2f);
            block.Hand(0, 0f, 0f, valid: false);
            block.Hand(1, 0f, 0f);

            var (withdrawn, head) = runtime.HandsAndHead();
            Assert.Empty(withdrawn);
            Assert.Equal(1.2f, head!.Value.Position.Y);

            runtime.Pointing = true;
            var (hands, _) = runtime.HandsAndHead();
            Assert.Equal([1u], hands.Select(hand => hand.Device));
            Assert.Equal(1u, block.D47Half.Pointing);
        }
        finally
        {
            runtime.Stop();
        }
    }

    [Fact]
    public void TheTriggerIsHeldOnlyWhileWanted()
    {
        using var block = new InMemoryXrBlock();
        var runtime = TheLayerDrawsIntoTheSpareTextureTests.Started(block, new FakeTextureDevice(), new FakeSurface());

        try
        {
            block.Hand(0, trigger: 1f, grip: 0f);

            Assert.False(runtime.TriggerHeld(false));
            Assert.True(runtime.TriggerHeld(true));

            block.Hand(0, trigger: 0.5f, grip: 0f);
            Assert.True(runtime.TriggerHeld(true));

            block.Hand(0, trigger: 0.3f, grip: 0f);
            Assert.False(runtime.TriggerHeld(true));
        }
        finally
        {
            runtime.Stop();
        }
    }

    [Fact]
    public void AGripHeldDownIsOneBackPress()
    {
        using var block = new InMemoryXrBlock();
        var runtime = TheLayerDrawsIntoTheSpareTextureTests.Started(block, new FakeTextureDevice(), new FakeSurface());

        try
        {
            block.Hand(1, trigger: 0f, grip: 1f);

            // Not counted while the panel does not want the controllers.
            runtime.TriggerHeld(false);
            Assert.False(runtime.BackPressed());

            runtime.TriggerHeld(true);
            Assert.True(runtime.BackPressed());
            Assert.False(runtime.BackPressed());

            block.Hand(1, trigger: 0f, grip: 0f);
            Assert.False(runtime.BackPressed());

            block.Hand(1, trigger: 0f, grip: 1f);
            Assert.True(runtime.BackPressed());
        }
        finally
        {
            runtime.Stop();
        }
    }

    [Fact]
    public void TheBeamAndCursorAreSlotsOfTheirOwnWhilePointing()
    {
        using var block = new InMemoryXrBlock();
        var device = new FakeTextureDevice();
        var runtime = TheLayerDrawsIntoTheSpareTextureTests.Started(block, device, new FakeSurface());

        try
        {
            runtime.Pointing = true;
            Assert.True(runtime.Serve(DateTimeOffset.UnixEpoch));

            var head = new VrPose(new System.Numerics.Vector3(0f, 0.2f, 0.4f), System.Numerics.Quaternion.Identity);
            runtime.AimBeam(VrPose.Origin, head, 1f);
            runtime.ShowCursor(new System.Numerics.Vector3(0f, 0f, -1f), head, VrCursor.Ring);
            var ring = block.D47Half.Slots[OpenXrLayerRuntime.CursorSlot];

            runtime.ShowCursor(new System.Numerics.Vector3(0f, 0f, -1f), head, VrCursor.Horizontal);
            var arrow = block.D47Half.Slots[OpenXrLayerRuntime.CursorSlot];

            Assert.Equal(1u, block.D47Half.Slots[OpenXrLayerRuntime.BeamSlot].Visible);
            Assert.Equal(1u, ring.Visible);
            Assert.NotEqual(ring.TextureGeneration, arrow.TextureGeneration);
            Assert.NotEqual(InMemoryXrBlock.NameIn(ring), InMemoryXrBlock.NameIn(arrow));

            // A cursor kind is a texture of its own, so the aim thread writes no pixels.
            var writes = device.Writes.Count;
            runtime.ShowCursor(new System.Numerics.Vector3(0f, 0f, -1f), head, VrCursor.Ring);
            Assert.Equal(writes, device.Writes.Count);

            runtime.AimBeam(null, head, 1f);
            Assert.Equal(0u, block.D47Half.Slots[OpenXrLayerRuntime.BeamSlot].Visible);

            runtime.Pointing = false;
            block.Frame();
            Assert.True(runtime.Serve(DateTimeOffset.UnixEpoch + TimeSpan.FromMilliseconds(100)));
            Assert.Equal(0u, block.D47Half.Slots[OpenXrLayerRuntime.CursorSlot].Visible);
        }
        finally
        {
            runtime.Stop();
        }
    }
}
