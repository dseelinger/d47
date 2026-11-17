using D47.XrLayer;
using Xunit;

namespace D47.XrLayer.Tests;

/// <summary>
/// d47 and the layer in a running Elite may come from different releases. A change to the block's size or to any
/// field's offset needs a new <see cref="XrBlock.CurrentLayoutVersion"/> and a new row here.
/// </summary>
public sealed unsafe class TheSharedBlockLayoutIsPinnedTests
{
    private static readonly Dictionary<uint, (int Size, Dictionary<string, int> Offsets)> Layouts = new()
    {
        [1] = (1552, new()
        {
            ["Magic"] = 0,
            ["LayoutVersion"] = 4,
            ["D47.Sequence"] = 8,
            ["D47.Pointing"] = 12,
            ["D47.Heartbeat"] = 16,
            ["D47.Slots[0].Visible"] = 24,
            ["D47.Slots[0].Space"] = 28,
            ["D47.Slots[0].Pose"] = 32,
            ["D47.Slots[0].WidthMetres"] = 60,
            ["D47.Slots[0].TextureWidth"] = 64,
            ["D47.Slots[0].TextureHeight"] = 68,
            ["D47.Slots[0].TextureGeneration"] = 72,
            ["D47.Slots[0].RectX"] = 80,
            ["D47.Slots[0].RectY"] = 84,
            ["D47.Slots[0].RectWidth"] = 88,
            ["D47.Slots[0].RectHeight"] = 92,
            ["D47.Slots[0].TextureName"] = 96,
            ["D47.Slots[1].Visible"] = 224,
            ["D47.Slots[5].TextureName"] = 1096,
            ["Layer.Sequence"] = 1224,
            ["Layer.Attached"] = 1228,
            ["Layer.FrameCounter"] = 1232,
            ["Layer.Refusal"] = 1240,
            ["Layer.AdapterLuidLow"] = 1244,
            ["Layer.AdapterLuidHigh"] = 1248,
            ["Layer.HeadValid"] = 1252,
            ["Layer.Head"] = 1256,
            ["Layer.Hands[0].Aim"] = 1284,
            ["Layer.Hands[0].Grip"] = 1312,
            ["Layer.Hands[0].Valid"] = 1340,
            ["Layer.Hands[0].TriggerValue"] = 1344,
            ["Layer.Hands[0].GripValue"] = 1348,
            ["Layer.Hands[1].Aim"] = 1352,
            ["Layer.RuntimeName"] = 1420,
        }),
        [2] = (1552, new()
        {
            ["Magic"] = 0,
            ["LayoutVersion"] = 4,
            ["D47.Sequence"] = 8,
            ["D47.Pointing"] = 12,
            ["D47.Heartbeat"] = 16,
            ["D47.Slots[0].Visible"] = 24,
            ["D47.Slots[0].Space"] = 28,
            ["D47.Slots[0].Pose"] = 32,
            ["D47.Slots[0].WidthMetres"] = 60,
            ["D47.Slots[0].TextureWidth"] = 64,
            ["D47.Slots[0].TextureHeight"] = 68,
            ["D47.Slots[0].TextureGeneration"] = 72,
            ["D47.Slots[0].RectX"] = 80,
            ["D47.Slots[0].RectY"] = 84,
            ["D47.Slots[0].RectWidth"] = 88,
            ["D47.Slots[0].RectHeight"] = 92,
            ["D47.Slots[0].TextureName"] = 96,
            ["D47.Slots[1].Visible"] = 224,
            ["D47.Slots[5].TextureName"] = 1096,
            ["Layer.Sequence"] = 1224,
            ["Layer.Attached"] = 1228,
            ["Layer.FrameCounter"] = 1232,
            ["Layer.Refusal"] = 1240,
            ["Layer.ControllerRefusal"] = 1244,
            ["Layer.AdapterLuidLow"] = 1248,
            ["Layer.AdapterLuidHigh"] = 1252,
            ["Layer.HeadValid"] = 1256,
            ["Layer.Head"] = 1260,
            ["Layer.Hands[0].Aim"] = 1288,
            ["Layer.Hands[0].Grip"] = 1316,
            ["Layer.Hands[0].Valid"] = 1344,
            ["Layer.Hands[0].TriggerValue"] = 1348,
            ["Layer.Hands[0].GripValue"] = 1352,
            ["Layer.Hands[1].Aim"] = 1356,
            ["Layer.RuntimeName"] = 1424,
        }),
    };

    [Fact]
    public void TheSizeAndEveryOffsetMatchTheRowForTheCurrentVersion()
    {
        Assert.True(
            Layouts.TryGetValue(XrBlock.CurrentLayoutVersion, out var pinned),
            $"Layout version {XrBlock.CurrentLayoutVersion} has no row. Add its size and offsets.");

        Assert.Equal(pinned.Size, sizeof(XrBlock));
        Assert.Equal(pinned.Offsets, Offsets());
    }

    [Fact]
    public void ThePoseAndTheNamesHaveTheirWireSizes()
    {
        Assert.Equal(28, sizeof(XrBlockPose));
        Assert.Equal(200, sizeof(XrQuadSlot));
        Assert.Equal(68, sizeof(XrHand));
        Assert.Equal(XrBlock.TextureNameLength * sizeof(char), sizeof(XrTextureName));
        Assert.Equal(XrBlock.RuntimeNameLength, sizeof(XrRuntimeName));
    }

    [Fact]
    public void EachHalfStartsWithItsSequenceCounter()
    {
        var block = default(XrBlock);
        Assert.Equal(Offset(&block, &block.D47), Offset(&block, &block.D47.Sequence));
        Assert.Equal(Offset(&block, &block.Layer), Offset(&block, &block.Layer.Sequence));
    }

    private static Dictionary<string, int> Offsets()
    {
        var block = default(XrBlock);
        var b = &block;
        return new()
        {
            ["Magic"] = Offset(b, &b->Magic),
            ["LayoutVersion"] = Offset(b, &b->LayoutVersion),
            ["D47.Sequence"] = Offset(b, &b->D47.Sequence),
            ["D47.Pointing"] = Offset(b, &b->D47.Pointing),
            ["D47.Heartbeat"] = Offset(b, &b->D47.Heartbeat),
            ["D47.Slots[0].Visible"] = Offset(b, &b->D47.Slots[0].Visible),
            ["D47.Slots[0].Space"] = Offset(b, &b->D47.Slots[0].Space),
            ["D47.Slots[0].Pose"] = Offset(b, &b->D47.Slots[0].Pose),
            ["D47.Slots[0].WidthMetres"] = Offset(b, &b->D47.Slots[0].WidthMetres),
            ["D47.Slots[0].TextureWidth"] = Offset(b, &b->D47.Slots[0].TextureWidth),
            ["D47.Slots[0].TextureHeight"] = Offset(b, &b->D47.Slots[0].TextureHeight),
            ["D47.Slots[0].TextureGeneration"] = Offset(b, &b->D47.Slots[0].TextureGeneration),
            ["D47.Slots[0].RectX"] = Offset(b, &b->D47.Slots[0].RectX),
            ["D47.Slots[0].RectY"] = Offset(b, &b->D47.Slots[0].RectY),
            ["D47.Slots[0].RectWidth"] = Offset(b, &b->D47.Slots[0].RectWidth),
            ["D47.Slots[0].RectHeight"] = Offset(b, &b->D47.Slots[0].RectHeight),
            ["D47.Slots[0].TextureName"] = Offset(b, &b->D47.Slots[0].TextureName),
            ["D47.Slots[1].Visible"] = Offset(b, &b->D47.Slots[1].Visible),
            ["D47.Slots[5].TextureName"] = Offset(b, &b->D47.Slots[5].TextureName),
            ["Layer.Sequence"] = Offset(b, &b->Layer.Sequence),
            ["Layer.Attached"] = Offset(b, &b->Layer.Attached),
            ["Layer.FrameCounter"] = Offset(b, &b->Layer.FrameCounter),
            ["Layer.Refusal"] = Offset(b, &b->Layer.Refusal),
            ["Layer.ControllerRefusal"] = Offset(b, &b->Layer.ControllerRefusal),
            ["Layer.AdapterLuidLow"] = Offset(b, &b->Layer.AdapterLuidLow),
            ["Layer.AdapterLuidHigh"] = Offset(b, &b->Layer.AdapterLuidHigh),
            ["Layer.HeadValid"] = Offset(b, &b->Layer.HeadValid),
            ["Layer.Head"] = Offset(b, &b->Layer.Head),
            ["Layer.Hands[0].Aim"] = Offset(b, &b->Layer.Hands[0].Aim),
            ["Layer.Hands[0].Grip"] = Offset(b, &b->Layer.Hands[0].Grip),
            ["Layer.Hands[0].Valid"] = Offset(b, &b->Layer.Hands[0].Valid),
            ["Layer.Hands[0].TriggerValue"] = Offset(b, &b->Layer.Hands[0].TriggerValue),
            ["Layer.Hands[0].GripValue"] = Offset(b, &b->Layer.Hands[0].GripValue),
            ["Layer.Hands[1].Aim"] = Offset(b, &b->Layer.Hands[1].Aim),
            ["Layer.RuntimeName"] = Offset(b, &b->Layer.RuntimeName),
        };
    }

    private static int Offset(void* block, void* field) => (int)((byte*)field - (byte*)block);
}
