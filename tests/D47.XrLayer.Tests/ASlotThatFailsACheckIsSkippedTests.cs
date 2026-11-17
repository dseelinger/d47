using D47.XrLayer;
using Xunit;

namespace D47.XrLayer.Tests;

public sealed class ASlotThatFailsACheckIsSkippedTests : IDisposable
{
    private readonly LayerRig rig = new();

    public static TheoryData<string> Faults =>
    [
        "position is not a number",
        "orientation is infinite",
        "orientation is not a unit quaternion",
        "position is out of reach",
        "width is not finite",
        "texture is wider than the largest side",
        "rectangle leaves the texture",
        "texture name has no terminator",
        "space is unknown",
        "texture is a different size",
        "texture is a different format",
        "texture has mip levels",
        "texture is not there",
    ];

    [Theory]
    [MemberData(nameof(Faults))]
    public void TheOtherSlotsAreStillDrawn(string fault)
    {
        rig.Show(0);
        rig.Show(1);
        rig.Show(2);
        Break(fault);
        rig.Publish();
        rig.Run();

        var quads = rig.EndFrame(2 * LayerRig.Millisecond).Calls.Single().Quads;

        Assert.Equal([0f, 2f], quads.Select(q => q.Pose.PositionX));
        Assert.DoesNotContain(rig.Device.Copies, copy => copy.Source == "d47.slot1.a");
    }

    private void Break(string fault)
    {
        ref var slot = ref rig.Slot(1);
        var texture = rig.Device.Textures["d47.slot1.a"];
        switch (fault)
        {
            case "position is not a number":
                slot.Pose.PositionY = float.NaN;
                break;
            case "orientation is infinite":
                slot.Pose.OrientationX = float.PositiveInfinity;
                break;
            case "orientation is not a unit quaternion":
                slot.Pose.OrientationW = 2f;
                break;
            case "position is out of reach":
                slot.Pose.PositionZ = -1000f;
                break;
            case "width is not finite":
                slot.WidthMetres = float.PositiveInfinity;
                break;
            case "texture is wider than the largest side":
                slot.TextureWidth = XrBlock.MaxTextureSide * 2;
                slot.RectWidth = (int)slot.TextureWidth;
                rig.Device.Textures["d47.slot1.a"] = LayerRig.Description(slot.TextureWidth, slot.TextureHeight);
                break;
            case "rectangle leaves the texture":
                slot.RectX = 1;
                break;
            case "texture name has no terminator":
                Span<char> name = slot.TextureName;
                name.Fill('x');
                break;
            case "space is unknown":
                slot.Space = (XrQuadSpace)7;
                break;
            case "texture is a different size":
                texture.Width = 1024;
                rig.Device.Textures["d47.slot1.a"] = texture;
                break;
            case "texture is a different format":
                texture.Format = 28;
                rig.Device.Textures["d47.slot1.a"] = texture;
                break;
            case "texture has mip levels":
                texture.MipLevels = 10;
                rig.Device.Textures["d47.slot1.a"] = texture;
                break;
            case "texture is not there":
                rig.Device.Textures.Remove("d47.slot1.a");
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(fault), fault, null);
        }
    }

    public void Dispose() => rig.Dispose();
}
