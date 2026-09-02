using System.Numerics;
using Avalonia;
using Avalonia.Headless.XUnit;
using Avalonia.Media.Imaging;
using D47.App.Headset;
using D47.App.Panel;
using D47.Core.Vr;
using Xunit;

namespace D47.App.Tests;

/// <summary>
/// The caption quad, rendered. The layout rules are asserted in Core against no pixels at all;
/// what these add is that the thing actually draws, that it is transparent where it should be,
/// and a capture a person can look at — because "is this legible over a starfield" is a
/// question only a face in a headset can answer, and giving it a picture to argue with is the
/// nearest a test gets.
/// </summary>
public class CaptionSurfaceTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 12, 21, 0, 0, TimeSpan.Zero);

    [AvaloniaFact]
    public void TheCaptionQuadIsTransparentEverywhereTheBoxIsNot()
    {
        var model = new CaptionViewModel();
        model.Show(["Fuel is at nineteen per cent."]);

        // The top-left corner is above the box, which sits at the bottom. Anything other than
        // a fully transparent pixel there is a dark slab across the Commander's view.
        Render(model, (frame, _) => Assert.Equal(0, AlphaAt(frame, 8, 8)));
    }

    [AvaloniaFact]
    public void ACaptionWithNothingToSayDrawsNothingAtAll()
    {
        var model = new CaptionViewModel();

        Render(model, (frame, size) =>
        {
            for (var x = 4; x < size.Width; x += 137)
            {
                for (var y = 4; y < size.Height; y += 71)
                {
                    Assert.Equal(0, AlphaAt(frame, x, y));
                }
            }
        });
    }

    [AvaloniaFact]
    public void TheBoxIsDrawnWhereTheTextIs()
    {
        var model = new CaptionViewModel();
        model.Show(["Docking permission granted, pad seven,", "and you have ten minutes."]);

        // Middle of the bottom, where the box lives.
        Render(model, (frame, size) => Assert.True(AlphaAt(frame, size.Width / 2, size.Height - 40) > 0));
    }

    /// <summary>
    /// Captures at each size for a human to look at, straight through the layer so what is
    /// rendered is what the standard's own wrapping produced rather than lines typed by hand.
    /// </summary>
    [AvaloniaFact]
    public void CaptionSizesRenderToCaptures()
    {
        var layer = new CaptionLayer();
        layer.Say("Interdiction detected.", Now);
        layer.Say("Submit or run, Commander, but decide now, because the tether is closing.", Now);

        foreach (var size in Enum.GetValues<CaptionSize>())
        {
            var settings = new CaptionSettings { Size = size };
            layer.Settings = settings;

            var model = new CaptionViewModel();
            model.Configure(settings);
            model.Show(layer.Lines);

            var name = size.ToString().ToLowerInvariant();

            Render(model, (frame, _) => frame.Save(
                Path.Combine(TestSurface.CaptureDirectory, $"vr-captions-{name}.png"),
                new PngBitmapEncoderOptions()));
        }

        // Two lines, which is the window: the long sentence wraps to two and the short one
        // ahead of it has rolled off.
        Assert.Equal(Caption.WindowLines, layer.Lines.Count);
    }

    /// <summary>
    /// Renders and hands the frame to the assertion while the surface is still alive. The
    /// bitmap belongs to the surface, so returning it and disposing on the way out hands back
    /// something already gone.
    /// </summary>
    private static void Render(CaptionViewModel model, Action<RenderTargetBitmap, PixelSize> assert)
    {
        var size = new PixelSize(1600, 340);

        var view = new CaptionView { DataContext = model };
        using var surface = new OffscreenSurface(view, size);

        assert(surface.Render(), size);
    }

    /// <summary>
    /// One pixel's alpha. The whole claim of this surface is that most of it is nothing, and
    /// alpha is the only channel that says so.
    /// </summary>
    private static byte AlphaAt(RenderTargetBitmap frame, int x, int y)
    {
        var stride = frame.PixelSize.Width * 4;
        var pixels = new byte[stride * frame.PixelSize.Height];

        unsafe
        {
            fixed (byte* buffer = pixels)
            {
                frame.CopyPixels(
                    new PixelRect(0, 0, frame.PixelSize.Width, frame.PixelSize.Height),
                    (IntPtr)buffer,
                    pixels.Length,
                    stride);
            }
        }

        return pixels[(y * stride) + (x * 4) + 3];
    }
}

/// <summary>
/// The caption quad across a whole utterance rather than one frame of it
/// (remediation.md, "Only the first caption arrives").
/// <para>
/// The reported symptom has two halves: the first caption is doubled, and every one after it
/// draws an empty box. The doubling is asserted in Core, where the identity that fixes it
/// lives. This is the other half — whether the surface actually redraws when the layer rolls,
/// which is a question about pixels.
/// </para>
/// </summary>
public class CaptionsKeepArrivingTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 12, 21, 0, 0, TimeSpan.Zero);

    /// <summary>
    /// Ink somewhere along the middle of the box. A box with no text in it still has alpha, so
    /// the box's own presence proves nothing — what is looked for is a pixel that is not the
    /// background and not fully transparent.
    /// </summary>
    private static int InkAcross(byte[] pixels, PixelSize size, int y)
    {
        var stride = size.Width * 4;
        var ink = 0;

        for (var x = 0; x < size.Width; x++)
        {
            var at = (y * stride) + (x * 4);

            // The text is #F2F2F2 on a black box. Anything bright is the text.
            if (pixels[at] > 200 && pixels[at + 1] > 200 && pixels[at + 2] > 200)
            {
                ink++;
            }
        }

        return ink;
    }

    private static byte[] Pixels(RenderTargetBitmap frame)
    {
        var stride = frame.PixelSize.Width * 4;
        var pixels = new byte[stride * frame.PixelSize.Height];

        unsafe
        {
            fixed (byte* buffer = pixels)
            {
                frame.CopyPixels(
                    new PixelRect(0, 0, frame.PixelSize.Width, frame.PixelSize.Height),
                    (IntPtr)buffer,
                    pixels.Length,
                    stride);
            }
        }

        return pixels;
    }

    /// <summary>How much text the surface would put on the quad as it stands.</summary>
    private static int Ink(VrCaptionSurface surface)
    {
        var (width, height) = surface.Size;
        var size = new PixelSize(width, height);

        var stride = size.Width * 4;
        var buffer = new byte[stride * size.Height];

        int ink;

        unsafe
        {
            fixed (byte* address = buffer)
            {
                surface.Draw((IntPtr)address, stride);
            }
        }

        ink = 0;

        for (var y = 0; y < size.Height; y += 2)
        {
            ink += InkAcross(buffer, size, y);
        }

        return ink;
    }

    [AvaloniaFact]
    public void EveryCaptionInATurnIsDrawnAndNotJustTheFirst()
    {
        var layer = new CaptionLayer();
        using var surface = new VrCaptionSurface(layer) { Enabled = true };

        layer.Say("Interdiction detected.", Now, utterance: 1);

        Assert.True(surface.IsDirty, "the first caption asks for a redraw");
        var first = Ink(surface);
        Assert.True(first > 0, "the first caption is drawn");

        // The second sentence of the same reply, which is where the report says it stops.
        layer.Say("Submit or run, Commander.", Now, utterance: 2);

        Assert.True(surface.IsDirty, "the second caption asks for a redraw");
        Assert.True(Ink(surface) > 0, "the second caption is drawn");

        // And a third, because "only the first" would also be satisfied by a surface that drew
        // two and then stopped.
        layer.Say("The tether is closing.", Now, utterance: 3);

        Assert.True(Ink(surface) > 0, "the third caption is drawn");
        Assert.True(surface.Visible, "and the quad is still asking to be shown");
    }
}

/// <summary>
/// Where the two caption bands go
/// (<a href="https://github.com/dseelinger/d47/issues/204">#204</a>).
/// <para>
/// The lock picks between two poses the surface works out for itself, so what is worth asserting
/// is that each one is where it claims to be and that neither is a placement: the world-locked
/// band is a constant in the seated universe, it does not move when the head does, and it is the
/// same apparent size as the band it replaces — which is what lets the three caption sizes carry
/// over instead of being re-measured.
/// </para>
/// </summary>
public class WhereTheCaptionBandGoesTests
{
    /// <summary>A head well away from the origin and rolled, so nothing can pass by accident.</summary>
    private static readonly VrPose Head = new(
        new Vector3(0.4f, 0.2f, -0.9f),
        Quaternion.CreateFromYawPitchRoll(0.7f, -0.3f, 0.25f));

    /// <summary>
    /// The word is what <c>settings.json</c> holds, so a hand-edited file can say anything. Anything
    /// that is not "world" is the band that is always in view — a caption layer has no reading of
    /// "somewhere else" — and nothing about it refuses to load, which a <c>SurfaceLock</c> on the
    /// settings record would have done to the very word the row and the docs both use.
    /// </summary>
    [AvaloniaTheory]
    [InlineData("head")]
    [InlineData("HEAD")]
    [InlineData("")]
    [InlineData("footwell")]
    public void AnythingButWorldIsTheBandInTheView(string said)
    {
        Assert.Equal(SurfaceLock.HeadLocked, Placement(said).Lock);
    }

    [AvaloniaTheory]
    [InlineData("world")]
    [InlineData("World")]
    public void TheWordTheRowWritesIsTheWordTheBandReads(string said)
    {
        Assert.Equal(SurfaceLock.WorldLocked, Placement(said).Lock);
    }

    [AvaloniaFact]
    public void ACaptionRidesTheViewUntilItIsToldNotTo()
    {
        var placement = Placement("head");

        Assert.Equal(SurfaceLock.HeadLocked, placement.Lock);
        Assert.True(placement.RidesTheHead);
        Assert.Null(placement.Placed);
    }

    /// <summary>
    /// <b>World-locked is world-locked from the first frame.</b> A surface whose lock says world
    /// and whose pose is null still rides the head, which is how a lock row comes to look like it
    /// did nothing — the trap the panel needs <c>RestIfNeverPlaced</c> to get out of. The caption
    /// band has no such gap because its pose is a constant, not a sample.
    /// </summary>
    [AvaloniaFact]
    public void TheWorldLockedBandIsPlacedBeforeAnyHeadHasBeenSeen()
    {
        var placement = Placement("world");

        Assert.Equal(SurfaceLock.WorldLocked, placement.Lock);
        Assert.NotNull(placement.Placed);
        Assert.False(placement.RidesTheHead);
    }

    [AvaloniaFact]
    public void TheWorldLockedBandDoesNotMoveWhenTheCommanderDoes()
    {
        var placement = Placement("world");

        Assert.Equal(placement.Where(VrPose.Origin), placement.Where(Head));
    }

    /// <summary>
    /// Between the console and the feet: ahead of the seated eye, below it, and low enough to be
    /// out of the way of flying without being on the floor. The angles are the claim — a Commander
    /// reads "between the console and my feet" as a direction to look, not as a coordinate.
    /// </summary>
    [AvaloniaFact]
    public void TheWorldLockedBandSitsLowInFrontOfTheSeatedCommander()
    {
        var placed = Placement("world").Placed!.Value;

        // Straight ahead of the seated origin, which is -Z, and squarely so.
        Assert.True(placed.Position.Z < -0.5f, $"the band is ahead of the seat: {placed.Position.Z}");
        Assert.True(MathF.Abs(placed.Position.X) < 0.01f, "and not off to one side");

        var below = Degrees(MathF.Atan2(-placed.Position.Y, -placed.Position.Z));

        // Below the console band and well above the feet. The head-locked band is at 16 degrees.
        Assert.InRange(below, 30f, 50f);

        // A seated eye is a little over a metre off the floor, and this is nowhere near it.
        Assert.True(placed.Position.Y > -0.9f, $"the band is not on the floor: {placed.Position.Y}");
    }

    /// <summary>
    /// Two things a band 40 degrees below the eye cannot do without: a face aimed at the eye,
    /// because a quad that far down and square to the world is read edge-on, and no roll, which
    /// is what makes this a way round the tilt in #189 for anyone who turns it on.
    /// </summary>
    [AvaloniaFact]
    public void TheWorldLockedBandFacesTheSeatedEyeAndIsLevel()
    {
        var placed = Placement("world").Placed!.Value;

        // An overlay's visible face looks along its own +Z.
        var face = Vector3.Normalize(Vector3.Transform(Vector3.UnitZ, placed.Facing));
        var toTheEye = Vector3.Normalize(-placed.Position);

        Assert.True(
            Vector3.Dot(face, toTheEye) > 0.999f,
            $"the face points at the seated eye: {Vector3.Dot(face, toTheEye)}");

        // Level: the quad's own lateral axis runs along the horizon.
        Assert.True(
            MathF.Abs(Vector3.Transform(Vector3.UnitX, placed.Facing).Y) < 1e-5f,
            "and the text runs along the horizon");
    }

    /// <summary>
    /// <b>The same angle of view, so the size steps carry over.</b> Apparent text size is the
    /// texture's pixel count and the quad's width in metres together, and the world-locked band
    /// is nearer — so holding the head-locked width would draw every caption half again as big.
    /// The width follows the distance instead, which is why <c>CaptionSize</c> needed no
    /// re-measuring.
    /// </summary>
    [AvaloniaFact]
    public void BothBandsSubtendTheSameAngle()
    {
        Assert.Equal(Subtense(Placement("head")), Subtense(Placement("world")), 3);
    }

    [AvaloniaFact]
    public void TheWorldLockedBandIsNearerAndSmallerThanTheOneItReplaces()
    {
        var inTheView = Placement("head");
        var inTheCockpit = Placement("world");

        Assert.True(EyeDistance(inTheCockpit) < EyeDistance(inTheView));
        Assert.True(inTheCockpit.WidthMetres < inTheView.WidthMetres);

        // Still never curved, for the reason it never was: two short lines have no far edges.
        Assert.Equal(0f, inTheCockpit.Curvature);
    }

    /// <summary>The band the surface draws when the settings row holds this word.</summary>
    private static SurfacePlacement Placement(string said)
    {
        var layer = new CaptionLayer { Settings = new CaptionSettings { Lock = said } };
        using var surface = new VrCaptionSurface(layer);

        return surface.Placement;
    }

    /// <summary>How far the band's centre is from the eye, which is the slant and not the reach.</summary>
    private static float EyeDistance(SurfacePlacement placement) => MathF.Sqrt(
        (placement.DistanceMetres * placement.DistanceMetres)
        + (placement.DropMetres * placement.DropMetres));

    /// <summary>How much of the view the band covers, side to side, in degrees.</summary>
    private static double Subtense(SurfacePlacement placement) =>
        2 * Degrees(MathF.Atan2(placement.WidthMetres / 2f, EyeDistance(placement)));

    private static float Degrees(float radians) => radians * 180f / MathF.PI;
}
