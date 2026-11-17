using D47.Core.Vr;

namespace D47.Vr.Tests;

/// <summary>A surface that rasterises nothing and remembers what it was asked.</summary>
public sealed class FakeSurface : IVrSurfaceSource
{
    public VrSurface Surface { get; init; } = VrSurface.PanelFull;

    public bool Visible { get; set; } = true;

    public SurfacePlacement Placement { get; set; } = new();

    public (int Width, int Height) Size { get; set; } = (8, 8);

    public bool IsDirty { get; set; } = true;

    public bool UploadsAsTexture { get; init; }

    public (int Width, int Height) LargestSize { get; set; } = (8, 8);

    /// <summary>The byte every pixel channel is drawn as, or null to draw nothing.</summary>
    public byte? Paint { get; set; }

    /// <summary>How many times the runtime asked for pixels.</summary>
    public int Draws { get; private set; }

    /// <summary>The last head pose the runtime passed on.</summary>
    public VrPose? Seen { get; private set; }

    public unsafe void Draw(IntPtr destination, int rowBytes)
    {
        Draws++;

        if (Paint is { } paint)
        {
            new Span<byte>((void*)destination, rowBytes * Size.Height).Fill(paint);
        }
    }

    public void Observe(VrPose head) => Seen = head;
}
