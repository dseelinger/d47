using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Headless.XUnit;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using D47.App.Headset;
using D47.App.Panel;
using D47.Vr;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.App.Tests;

/// <summary>
/// What the headset is actually handed.
/// <para>
/// Everything else about the VR path is observable — the overlays log when they come up, the
/// session logs when it starts, a throwing serve logs and rebuilds. The one step that says
/// nothing either way is the rasterise: a detached <c>UserControl</c> measures 0x0 and draws an
/// empty quad with no exception and no warning, which is the single most expensive way this
/// path has failed before. The bytes submitted to SteamVR are the only place to catch it, and
/// they are not visible from any test that renders a control the ordinary way.
/// </para>
/// </summary>
public class VrPanelRasterTests
{
    /// <summary>
    /// Not blank. An overlay showing a fully transparent texture is indistinguishable, from
    /// inside a headset, from an overlay that was never shown at all.
    /// </summary>
    [AvaloniaFact]
    public void TheHeadsetIsHandedPixelsRatherThanAnEmptyQuad()
    {
        var (surface, width, height) = Rasterised(out var buffer);
        var opaque = 0;

        unsafe
        {
            var bytes = (byte*)buffer.Address;

            // Every fourth byte is alpha in Bgra8888.
            for (var i = 3; i < width * height * 4; i += 4)
            {
                if (bytes[i] != 0)
                {
                    opaque++;
                }
            }
        }

        Assert.Equal(width * height, opaque);
        Assert.Equal((1024, 640), surface.Size);
    }

    /// <summary>
    /// And the pixels are the panel rather than a filled rectangle. A control theme that failed
    /// to apply still paints a background, so "not transparent" alone would pass on exactly the
    /// blank quad this is here to catch — the text is what proves the tree was styled and laid
    /// out.
    /// </summary>
    [AvaloniaFact]
    public void WhatItDrawsIsThePanelAndNotJustItsBackground()
    {
        var (_, width, height) = Rasterised(out var buffer);
        var distinct = new HashSet<uint>();

        unsafe
        {
            var pixels = (uint*)buffer.Address;

            for (var i = 0; i < width * height; i++)
            {
                distinct.Add(pixels[i]);

                if (distinct.Count > 8)
                {
                    break;
                }
            }
        }

        // A background and nothing else is one or two colours. Text, the avatar ring, the
        // accent on the send button and the ask box border are many more.
        Assert.True(
            distinct.Count > 8,
            $"The submitted buffer has only {distinct.Count} distinct colours, which is a "
            + "blank quad rather than a rendered panel.");
    }

    /// <summary>
    /// The real panel, through the real buffer, arriving in the channel order the runtime
    /// reads — the whole path from a styled widget tree to the bytes <c>SetOverlayRaw</c>
    /// takes, with the conversion in the middle of it.
    /// <para>
    /// Asserted against the panel's own pixels rather than a synthetic pattern, because the
    /// failure being guarded is one where every call still succeeds. A panel handed over
    /// unconverted is a panel in the headset with the Elite theme's orange rendered blue,
    /// which is exactly the kind of wrong that gets remembered as "working".
    /// </para>
    /// </summary>
    [AvaloniaFact]
    public void WhatTheRuntimeIsHandedIsThePanelInRgba()
    {
        var (_, width, height) = Rasterised(out var buffer);

        var drawn = new byte[width * height * 4];
        Marshal.Copy(buffer.Address, drawn, 0, drawn.Length);

        buffer.ToRgba();

        var handed = new byte[drawn.Length];
        Marshal.Copy(buffer.Address, handed, 0, handed.Length);

        var accent = 0;

        for (var p = 0; p < width * height; p++)
        {
            var at = p * 4;

            Assert.Equal(drawn[at + 2], handed[at]);
            Assert.Equal(drawn[at + 1], handed[at + 1]);
            Assert.Equal(drawn[at], handed[at + 2]);
            Assert.Equal(drawn[at + 3], handed[at + 3]);

            // Red well clear of blue, in the first byte, where RGBA says red lives. The theme's
            // accent is orange; if the conversion were skipped this counts nothing, because
            // the first byte would still be the blue channel.
            if (handed[at] > 160 && handed[at + 2] < 80)
            {
                accent++;
            }
        }

        Assert.True(
            accent > 100,
            $"Only {accent} pixels of the submitted buffer read as the theme's orange accent, "
            + "which is what a panel handed over with red and blue swapped looks like.");
    }

    /// <summary>
    /// A surface starts dirty, or the first frame is shown before anything has ever been drawn
    /// into its texture: the runtime submits only when the source says it has changed.
    /// </summary>
    [AvaloniaFact]
    public void ASurfaceHasSomethingToDrawBeforeItIsEverAsked()
    {
        var (settings, _, _) = TestSurface.Create();

        Assert.True(new VrPanelSurface(new PanelViewModel(), settings, _ => null).IsDirty);
    }

    /// <summary>
    /// Rasterises through the same call the runtime makes, into the same buffer the runtime
    /// hands OpenVR, so nothing about the real path is stubbed out. Left as the rasteriser
    /// wrote it — BGRA, unconverted — because that is what the runtime has at this point.
    /// </summary>
    private static (VrPanelSurface Surface, int Width, int Height) Rasterised(out VrPixels buffer)
    {
        var (settings, _, _) = TestSurface.Create();

        new D47.App.Theming.ThemeManager(Application.Current!, NullLogger<D47.App.Theming.ThemeManager>.Instance)
            .Apply(D47.Core.Interface.ThemeCatalog.Elite);

        var model = new PanelViewModel();
        model.Append("Holding in normal space over HIP 12099 1 b.");
        model.TurnLine = "routed: model";

        var surface = new VrPanelSurface(model, settings, _ => null);
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        var (width, height) = surface.Size;

        buffer = new VrPixels(width, height);
        surface.Draw(buffer.Address, buffer.RowBytes);

        return (surface, width, height);
    }
}
