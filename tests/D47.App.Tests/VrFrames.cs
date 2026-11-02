using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;
using D47.App.Headset;
using D47.App.Panel;
using D47.Vr;
using Xunit;

namespace D47.App.Tests;

/// <summary>Frame helpers for tests that drive a headset panel.</summary>
internal static class VrFrames
{
    /// <summary>Draws two frames into a buffer nobody reads, which lays the panel out.</summary>
    internal static void Serve(this VrPanelSurface panel)
    {
        Dispatcher.UIThread.RunJobs();

        var (width, height) = panel.Size;
        var buffer = new byte[width * height * 4];

        unsafe
        {
            fixed (byte* pixels = buffer)
            {
                panel.Draw((IntPtr)pixels, width * 4);
                panel.Draw((IntPtr)pixels, width * 4);
            }
        }

        Dispatcher.UIThread.RunJobs();
    }

    /// <summary>Where a control is on the quad's face, in the 0..1 a ray answers in.</summary>
    internal static (float U, float V) At(this VrPanelSurface panel, Control control, PanelView view)
    {
        var corner = control.TranslatePoint(new Point(0, 0), view);
        Assert.NotNull(corner);

        var centre = corner.Value + new Point(control.Bounds.Width / 2, control.Bounds.Height / 2);
        var (width, height) = panel.Size;

        return ((float)(centre.X / width), (float)(centre.Y / height));
    }

    /// <summary>How many colours the buffer has, counting stops once more than <paramref name="cap"/> are found.</summary>
    internal static int DistinctColours(VrPixels pixels, (int Width, int Height) size, int cap = 8)
    {
        var distinct = new HashSet<uint>();

        unsafe
        {
            var read = (uint*)pixels.Address;

            for (var index = 0; index < size.Width * size.Height; index++)
            {
                distinct.Add(read[index]);

                if (distinct.Count > cap)
                {
                    break;
                }
            }
        }

        return distinct.Count;
    }
}
