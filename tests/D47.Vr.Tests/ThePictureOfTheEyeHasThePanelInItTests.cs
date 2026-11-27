using System.Numerics;
using D47.Core.Vr;
using D47.XrLayer;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Vr.Tests;

/// <summary>
/// Through the OpenXR layer, a picture of the headset view asks the layer for Elite's left eye in a texture made at
/// the eye's format and size, waits for its answer and two more frames, and draws D47's quads over what comes back.
/// </summary>
public class ThePictureOfTheEyeHasThePanelInItTests
{
    private const uint Srgb = 29;

    private static readonly DateTimeOffset Start = new(2026, 11, 27, 20, 0, 0, TimeSpan.Zero);

    [Fact]
    public void BeforeTheLayerHasSeenAnEyeThePictureIsRefused()
    {
        using var block = new InMemoryXrBlock();
        var reader = new FakeEyeReader();
        var runtime = Serving(block, reader);

        try
        {
            Assert.Equal(OpenXrLayerRuntime.NoEyeYet, runtime.BeginEye(out _));
            Assert.Empty(reader.Created);
        }
        finally
        {
            runtime.Stop();
        }
    }

    [Fact]
    public void AnEyeOfAFormatAPictureCannotReadIsRefusedByName()
    {
        using var block = new InMemoryXrBlock();
        var reader = new FakeEyeReader();
        var runtime = Serving(block, reader);
        block.Eye(10, 40, 40);

        try
        {
            Assert.Equal("Elite's eye image is DXGI format 10, which a picture cannot read.", runtime.BeginEye(out _));
        }
        finally
        {
            runtime.Stop();
        }
    }

    [Fact]
    public void TheRequestNamesATextureAtTheEyesFormatAndSize()
    {
        using var block = new InMemoryXrBlock();
        var reader = new FakeEyeReader();
        var runtime = Serving(block, reader);
        block.Eye(Srgb, 40, 30);

        try
        {
            Assert.Null(runtime.BeginEye(out var pending));

            var made = Assert.Single(reader.Created);
            Assert.Equal((40, 30, Srgb), (made.Width, made.Height, made.Format));
            Assert.Equal([(0x1234u, 0)], reader.Luids);
            var half = block.D47Half;
            Assert.Equal(pending!.Serial, half.CaptureRequest);
            Assert.Equal(made.Name, NameIn(half.CaptureTexture));
        }
        finally
        {
            runtime.Stop();
        }
    }

    [Fact]
    public void ThePictureWaitsForTheAnswerAndTwoFramesAfterIt()
    {
        using var block = new InMemoryXrBlock();
        var reader = new FakeEyeReader();
        var runtime = Serving(block, reader);
        block.Eye(Srgb, 40, 40);

        try
        {
            Assert.Null(runtime.BeginEye(out var pending));
            Assert.Null(runtime.PollEye(pending!));

            block.Answer(pending!.Serial);
            Assert.Null(runtime.PollEye(pending));

            block.Frame();
            Assert.Null(runtime.PollEye(pending));

            block.Frame();
            var picture = runtime.PollEye(pending);
            Assert.NotNull(picture);
            Assert.Null(picture!.Refusal);
            Assert.Equal((40, 40), (picture.Width, picture.Height));
        }
        finally
        {
            runtime.Stop();
        }
    }

    [Fact]
    public void ThePanelIsDrawnWhereTheEyeSawIt()
    {
        using var block = new InMemoryXrBlock();
        var reader = new FakeEyeReader();
        var runtime = Serving(block, reader);
        block.Eye(Srgb, 40, 40);

        try
        {
            Assert.Null(runtime.BeginEye(out var pending));

            // An eye a metre behind the panel, facing it.
            var panel = InLocal(block.D47Half.Slots[OpenXrLayerRuntime.PanelSlot], new VrPose(new Vector3(0f, 1.2f, 0f), Quaternion.Identity));
            var eye = panel.Position + Vector3.Transform(Vector3.UnitZ, panel.Facing);
            block.Answer(pending!.Serial, eye: Block(new VrPose(eye, panel.Facing)));
            block.Frame();
            block.Frame();

            var picture = runtime.PollEye(pending)!;

            Assert.Equal(200, picture.Pixels![((20 * 40) + 20) * 4]);
            Assert.Equal(0, picture.Pixels[0]);
        }
        finally
        {
            runtime.Stop();
        }
    }

    [Fact]
    public void ARefusedAnswerSaysWhy()
    {
        using var block = new InMemoryXrBlock();
        var reader = new FakeEyeReader();
        var runtime = Serving(block, reader);
        block.Eye(Srgb, 40, 40);

        try
        {
            Assert.Null(runtime.BeginEye(out var pending));
            block.Answer(pending!.Serial, XrCaptureRefusal.NotReleased);

            Assert.Equal(
                OpenXrLayerRuntime.Describe(XrCaptureRefusal.NotReleased),
                runtime.PollEye(pending)!.Refusal);
        }
        finally
        {
            runtime.Stop();
        }
    }

    [Fact]
    public void AnAnswerToAnotherRequestIsNotTaken()
    {
        using var block = new InMemoryXrBlock();
        var reader = new FakeEyeReader();
        var runtime = Serving(block, reader);
        block.Eye(Srgb, 40, 40);

        try
        {
            Assert.Null(runtime.BeginEye(out var pending));
            block.Answer(pending!.Serial - 1);
            block.Frame();
            block.Frame();

            Assert.Null(runtime.PollEye(pending));
        }
        finally
        {
            runtime.Stop();
        }
    }

    [Fact]
    public void AStoppedSessionEndsThePicture()
    {
        using var block = new InMemoryXrBlock();
        var reader = new FakeEyeReader();
        var runtime = Serving(block, reader);
        block.Eye(Srgb, 40, 40);

        Assert.Null(runtime.BeginEye(out var pending));
        runtime.Stop();

        Assert.Equal(OpenXrLayerRuntime.EyeSessionEnded, runtime.PollEye(pending!)!.Refusal);
    }

    [Fact]
    public void WithoutAReaderThereIsNoPicture()
    {
        using var block = new InMemoryXrBlock();
        block.Attach("VirtualDesktopXR");
        var runtime = new OpenXrLayerRuntime([], NullLogger<OpenXrLayerRuntime>.Instance, block, new FakeTextureDevice());

        Assert.Equal(OpenXrLayerRuntime.NoEyeReader, runtime.BeginEye(out _));
    }

    /// <summary>A layer runtime serving an opaque panel painted 200 in every channel, with the head located.</summary>
    private static OpenXrLayerRuntime Serving(InMemoryXrBlock block, FakeEyeReader reader)
    {
        var device = new FakeTextureDevice();
        block.Attach("VirtualDesktopXR");
        block.HeadAt(1.2f);
        var runtime = new OpenXrLayerRuntime(
            [new FakeSurface { Paint = 200, Placement = new SurfacePlacement { Opacity = 1f } }],
            NullLogger<OpenXrLayerRuntime>.Instance,
            block,
            device,
            eyes: reader);

        runtime.Start();
        block.Frame();
        Assert.Equal(VrStartOutcome.Started, runtime.Start().Outcome);
        Assert.True(runtime.Serve(Start));
        return runtime;
    }

    private static VrPose InLocal(XrQuadSlot slot, VrPose head)
    {
        var pose = new VrPose(
            new Vector3(slot.Pose.PositionX, slot.Pose.PositionY, slot.Pose.PositionZ),
            new Quaternion(slot.Pose.OrientationX, slot.Pose.OrientationY, slot.Pose.OrientationZ, slot.Pose.OrientationW));

        return slot.Space == XrQuadSpace.Head
            ? new VrPose(head.Position + Vector3.Transform(pose.Position, head.Facing), head.Facing * pose.Facing)
            : pose;
    }

    private static XrBlockPose Block(VrPose pose) => new()
    {
        OrientationX = pose.Facing.X,
        OrientationY = pose.Facing.Y,
        OrientationZ = pose.Facing.Z,
        OrientationW = pose.Facing.W,
        PositionX = pose.Position.X,
        PositionY = pose.Position.Y,
        PositionZ = pose.Position.Z,
    };

    private static string NameIn(XrTextureName name)
    {
        ReadOnlySpan<char> characters = name;
        var end = characters.IndexOf('\0');
        return new string(end < 0 ? characters : characters[..end]);
    }
}
