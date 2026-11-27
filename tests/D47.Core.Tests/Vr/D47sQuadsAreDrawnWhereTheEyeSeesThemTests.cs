using System.Numerics;
using D47.Core.Vr;
using Xunit;

namespace D47.Core.Tests.Vr;

/// <summary>A picture of the headset view has D47's quads in it where the eye sees them, as the runtime draws them.</summary>
public class D47sQuadsAreDrawnWhereTheEyeSeesThemTests
{
    private const int Size = 100;

    /// <summary>A quarter turn each way: tan 45° is 1, so a pixel's column maps straight to its ray's x.</summary>
    private static readonly EyeView Ahead = new(VrPose.Origin, -MathF.PI / 4, MathF.PI / 4, MathF.PI / 4, -MathF.PI / 4);

    [Fact]
    public void AQuadStraightAheadCoversTheMiddleAndNotTheEdges()
    {
        var eye = Black();

        EyeComposite.Draw(eye, Size, Size, Ahead, [Quad(At(0, 0, -1), Red())]);

        Assert.Equal(Red3, Pixel(eye, 50, 50));
        Assert.Equal(Black3, Pixel(eye, 30, 50));
        Assert.Equal(Black3, Pixel(eye, 70, 50));
        Assert.Equal(Black3, Pixel(eye, 50, 30));
    }

    [Fact]
    public void AQuadBehindTheEyeIsNotDrawn()
    {
        var eye = Black();

        EyeComposite.Draw(eye, Size, Size, Ahead, [Quad(At(0, 0, 1), Red())]);

        Assert.Equal(Black3, Pixel(eye, 50, 50));
    }

    [Fact]
    public void ThePicturesTopIsUpAndItsLeftIsLeft()
    {
        var eye = Black();
        var picture = new byte[4 * 4 * 4];

        for (var y = 0; y < 4; y++)
        {
            for (var x = 0; x < 4; x++)
            {
                var at = ((y * 4) + x) * 4;
                picture[at + (y < 2 ? 1 : 2)] = 255;
                picture[at] = x < 2 ? (byte)255 : (byte)0;
                picture[at + 3] = 255;
            }
        }

        EyeComposite.Draw(eye, Size, Size, Ahead, [new EyeQuad(At(0, 0, -1), 0.5f, 0.5f, 0f, picture, 4, 4)]);

        var upperLeft = Pixel(eye, 40, 40);
        var lowerRight = Pixel(eye, 60, 60);
        Assert.Equal((255, 255, 0), upperLeft);
        Assert.Equal((0, 0, 255), lowerRight);
    }

    [Fact]
    public void APremultipliedHalfShowsTheEyeThroughIt()
    {
        var eye = White();
        var half = Filled(128, 0, 0, 128);

        EyeComposite.Draw(eye, Size, Size, Ahead, [Quad(At(0, 0, -1), half)]);

        Assert.Equal((255, 127, 127), Pixel(eye, 50, 50));
    }

    [Fact]
    public void ALaterQuadCoversAnEarlierOne()
    {
        var eye = Black();

        EyeComposite.Draw(eye, Size, Size, Ahead, [Quad(At(0, 0, -1), Red()), Quad(At(0, 0, -0.9f), Filled(255, 0, 0, 255))]);

        Assert.Equal((255, 0, 0), Pixel(eye, 50, 50));
    }

    [Fact]
    public void ACurvedQuadReachesPastWhereTheFlatOneEnds()
    {
        var flat = Black();
        var curved = Black();
        var pose = At(0, 0, -1);

        EyeComposite.Draw(flat, Size, Size, Ahead, [new EyeQuad(pose, 1f, 1f, 0f, Red(), 4, 4)]);
        EyeComposite.Draw(curved, Size, Size, Ahead, [new EyeQuad(pose, 1f, 1f, 0.25f, Red(), 4, 4)]);

        Assert.Equal(Black3, Pixel(flat, 76, 50));
        Assert.Equal(Red3, Pixel(curved, 76, 50));
        Assert.Equal(Red3, Pixel(curved, 50, 50));
    }

    [Fact]
    public void ATurnedEyeSeesTheQuadItIsTurnedTowards()
    {
        var eye = Black();
        var quarter = Quaternion.CreateFromAxisAngle(Vector3.UnitY, MathF.PI / 2);
        var turned = Ahead with { Eye = new VrPose(Vector3.Zero, quarter) };

        EyeComposite.Draw(eye, Size, Size, turned, [Quad(new VrPose(new Vector3(-1, 0, 0), quarter), Red())]);

        Assert.Equal(Red3, Pixel(eye, 50, 50));
    }

    private static readonly (int, int, int) Red3 = (0, 0, 255);

    private static readonly (int, int, int) Black3 = (0, 0, 0);

    private static VrPose At(float x, float y, float z) => new(new Vector3(x, y, z), Quaternion.Identity);

    private static EyeQuad Quad(VrPose pose, byte[] picture) => new(pose, 0.5f, 0.5f, 0f, picture, 4, 4);

    private static byte[] Red() => Filled(0, 0, 255, 255);

    private static byte[] Filled(byte blue, byte green, byte red, byte alpha)
    {
        var picture = new byte[4 * 4 * 4];

        for (var at = 0; at < picture.Length; at += 4)
        {
            (picture[at], picture[at + 1], picture[at + 2], picture[at + 3]) = (blue, green, red, alpha);
        }

        return picture;
    }

    private static byte[] Black()
    {
        var eye = new byte[Size * Size * 4];

        for (var at = 3; at < eye.Length; at += 4)
        {
            eye[at] = 255;
        }

        return eye;
    }

    private static byte[] White()
    {
        var eye = new byte[Size * Size * 4];
        Array.Fill(eye, (byte)255);
        return eye;
    }

    /// <summary>Blue, green and red of the pixel at (<paramref name="column"/>, <paramref name="row"/>).</summary>
    private static (int, int, int) Pixel(byte[] eye, int column, int row)
    {
        var at = ((row * Size) + column) * 4;
        return (eye[at], eye[at + 1], eye[at + 2]);
    }
}
