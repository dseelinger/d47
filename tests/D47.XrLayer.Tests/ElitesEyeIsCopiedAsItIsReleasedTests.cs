using System.Numerics;
using D47.XrLayer;
using Xunit;

namespace D47.XrLayer.Tests;

public sealed unsafe class ElitesEyeIsCopiedAsItIsReleasedTests
{
    private const uint Srgb = 29;

    private static readonly XrRect2Di Rect = new() { X = 100, Y = 50, Width = 1600, Height = 1500 };

    [Fact]
    public void TheEyesFormatAndSizeArePublishedEveryFrame()
    {
        using var rig = new LayerRig();
        rig.ShowEye(Rect);

        rig.Run();

        Assert.Equal(Srgb, rig.Published.EyeFormat);
        Assert.Equal(1600u, rig.Published.EyeWidth);
        Assert.Equal(1500u, rig.Published.EyeHeight);
    }

    [Fact]
    public void WithoutAProjectionViewNoEyeIsPublished()
    {
        using var rig = new LayerRig();

        rig.Run();

        Assert.Equal(0u, rig.Published.EyeFormat);
        Assert.Equal(0u, rig.Published.EyeWidth);
    }

    [Fact]
    public void ARequestIsCopiedAtTheReleaseAndAnsweredAtThatFramesEnd()
    {
        using var rig = Asked(EyeTexture());

        rig.EliteDrawsTheEye(2);

        var copy = Assert.Single(rig.Device.RegionCopies);
        Assert.Equal(FakeNext.SwapchainImage(LayerRig.EliteSwapchain, 2), copy.Source);
        Assert.Equal(0u, copy.Subresource);
        Assert.Equal(Rect, copy.Rect);
        Assert.Equal("d47.eye", copy.Destination);
        Assert.Equal(1, rig.Device.Flushes);
        Assert.Equal(0ul, rig.Published.CaptureServed);

        rig.EndFrame(3 * LayerRig.Millisecond);

        var published = rig.Published;
        Assert.Equal(1ul, published.CaptureServed);
        Assert.Equal(XrCaptureRefusal.None, published.CaptureRefusal);
        Assert.Equal(published.FrameCounter, published.CaptureFrame);
        Assert.Equal(-0.03f, published.CaptureEye.PositionX, 5);
        Assert.Equal(1.2f, published.CaptureEye.PositionY, 5);
        Assert.Equal(1f, published.CaptureEye.OrientationW, 5);
        Assert.Equal(-0.9f, published.CaptureFov.AngleLeft);
        Assert.Equal(-0.95f, published.CaptureFov.AngleDown);
        Assert.Equal(published.Head.PositionY, published.CaptureHead.PositionY);
        Assert.Contains("d47.eye", rig.Device.OpenedWritable);
        Assert.Contains("d47.eye", rig.Device.Released);
    }

    [Fact]
    public void ARequestWaitsForElitesRelease()
    {
        using var rig = Asked(EyeTexture());

        rig.EndFrame(3 * LayerRig.Millisecond);
        rig.EndFrame(4 * LayerRig.Millisecond);

        Assert.Empty(rig.Device.RegionCopies);
        Assert.Equal(0ul, rig.Published.CaptureServed);
    }

    [Fact]
    public void AReleaseOfAnotherSwapchainCopiesNothing()
    {
        using var rig = Asked(EyeTexture());

        rig.Session.Acquired(0x99, 0);
        rig.Session.BeforeRelease(0x99);

        Assert.Empty(rig.Device.RegionCopies);
    }

    [Fact]
    public void ARequestIsCopiedOnce()
    {
        using var rig = Asked(EyeTexture());
        rig.EliteDrawsTheEye(0);
        rig.EndFrame(3 * LayerRig.Millisecond);

        rig.EliteDrawsTheEye(1);
        rig.EndFrame(4 * LayerRig.Millisecond);

        Assert.Single(rig.Device.RegionCopies);
        Assert.Equal(1ul, rig.Published.CaptureServed);
    }

    [Fact]
    public void ATextureOfAnotherFormatIsRefusedAndNothingIsCopied()
    {
        using var rig = Asked(LayerRig.Description(1600, 1500));

        rig.EliteDrawsTheEye(0);

        Assert.Empty(rig.Device.RegionCopies);
        Assert.Equal(1ul, rig.Published.CaptureServed);
        Assert.Equal(XrCaptureRefusal.TextureMismatch, rig.Published.CaptureRefusal);
        Assert.Contains("d47.eye", rig.Device.Released);
    }

    [Fact]
    public void ATextureOfAnotherSizeIsRefused()
    {
        using var rig = Asked(LayerRig.Description(1600, 1400) with { Format = Srgb });

        Assert.Equal(XrCaptureRefusal.TextureMismatch, rig.Published.CaptureRefusal);
    }

    [Fact]
    public void AMultisampledEyeIsRefused()
    {
        using var rig = new LayerRig();
        rig.ShowEye(Rect, sampleCount: 4);
        rig.Run();
        rig.Ask(1, "d47.eye", EyeTexture());

        rig.EndFrame(2 * LayerRig.Millisecond);

        Assert.Equal(XrCaptureRefusal.Multisampled, rig.Published.CaptureRefusal);
        Assert.Equal(1ul, rig.Published.CaptureServed);
    }

    [Fact]
    public void WithNoEyeARequestIsRefused()
    {
        using var rig = new LayerRig();
        rig.Run();
        rig.Ask(1, "d47.eye", EyeTexture());

        rig.EndFrame(2 * LayerRig.Millisecond);

        Assert.Equal(XrCaptureRefusal.NoEyeImage, rig.Published.CaptureRefusal);
        Assert.Equal(1ul, rig.Published.CaptureServed);
    }

    [Fact]
    public void ATextureThatWillNotOpenIsRefused()
    {
        using var rig = new LayerRig();
        rig.ShowEye(Rect);
        rig.Run();
        rig.Ask(1, "d47.eye", EyeTexture());
        rig.Device.Textures.Remove("d47.eye");

        rig.EndFrame(2 * LayerRig.Millisecond);

        Assert.Equal(XrCaptureRefusal.TextureNotOpened, rig.Published.CaptureRefusal);
    }

    [Fact]
    public void AnUnlocatedSpaceIsRefused()
    {
        using var rig = Asked(EyeTexture());
        rig.Next.Unlocatable.Add(LayerRig.EliteSpace);

        rig.EliteDrawsTheEye(0);
        rig.EndFrame(3 * LayerRig.Millisecond);

        Assert.Equal(XrCaptureRefusal.NoPose, rig.Published.CaptureRefusal);
        Assert.Equal(1ul, rig.Published.CaptureServed);
    }

    [Fact]
    public void ARequestNoReleaseAnswersIsRefusedAfterItsFrames()
    {
        using var rig = Asked(EyeTexture());

        for (var frame = 0; frame <= EyeCaptureLimits.MostFramesWaited; frame++)
        {
            rig.EndFrame((3 + frame) * LayerRig.Millisecond);
        }

        Assert.Equal(XrCaptureRefusal.NotReleased, rig.Published.CaptureRefusal);
        Assert.Equal(1ul, rig.Published.CaptureServed);
    }

    [Fact]
    public void TheEyeIsPlacedThroughTheProjectionsSpace()
    {
        var quarter = Quaternion.CreateFromAxisAngle(Vector3.UnitY, MathF.PI / 2);
        var parent = new XrPosef
        {
            OrientationX = quarter.X,
            OrientationY = quarter.Y,
            OrientationZ = quarter.Z,
            OrientationW = quarter.W,
            PositionX = 1f,
            PositionY = 2f,
            PositionZ = 3f,
        };
        var child = new XrPosef { OrientationW = 1f, PositionZ = -1f };

        var placed = EyeCapture.Compose(parent, child);

        Assert.Equal(0f, placed.PositionX, 5);
        Assert.Equal(2f, placed.PositionY, 5);
        Assert.Equal(3f, placed.PositionZ, 5);
        Assert.Equal(quarter.Y, placed.OrientationY, 5);
    }

    private static TextureDescription EyeTexture() => LayerRig.Description(1600, 1500) with { Format = Srgb };

    /// <summary>A rig whose eye is published and whose request for <paramref name="texture"/> has been read.</summary>
    private static LayerRig Asked(TextureDescription texture)
    {
        var rig = new LayerRig();
        rig.ShowEye(Rect);
        rig.Run();
        rig.Ask(1, "d47.eye", texture);
        rig.EndFrame(2 * LayerRig.Millisecond);
        return rig;
    }
}
