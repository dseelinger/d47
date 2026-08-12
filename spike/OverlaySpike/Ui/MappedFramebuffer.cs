using System;
using Avalonia;
using Avalonia.Platform;

namespace OverlaySpike.Ui;

/// Lets Bitmap.CopyPixels write straight into a mapped D3D11 staging texture,
/// removing one full-surface copy from the CPU path.
sealed class MappedFramebuffer : ILockedFramebuffer
{
    public IntPtr Address { get; init; }
    public PixelSize Size { get; init; }
    public int RowBytes { get; init; }
    public Vector Dpi => new(96, 96);
    public PixelFormat Format => PixelFormat.Bgra8888;
    public AlphaFormat AlphaFormat => AlphaFormat.Premul;
    public void Dispose() { }
}
