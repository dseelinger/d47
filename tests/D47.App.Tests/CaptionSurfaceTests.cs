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
