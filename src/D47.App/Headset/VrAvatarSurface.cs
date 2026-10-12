using System.Runtime.InteropServices;
using Avalonia.Media.Imaging;
using D47.App.Media;
using D47.Core.Vr;
using D47.Vr;

namespace D47.App.Headset;

/// <summary>
/// The panel's avatar clip, played as its own quad over the panel's still frame of it, so the panel is
/// redrawn only when its content changes.
/// </summary>
public sealed class VrAvatarSurface(VrPanelSurface panel) : IVrSurfaceSource, IDisposable
{
    /// <summary>Frames decoded per advance: ahead of a 24 fps clip at the 10 Hz tick, at under a millisecond.</summary>
    private const int DecodesPerAdvance = 4;

    private readonly List<byte[]> _frames = [];

    private string? _path;
    private VideoFrames? _clip;
    private WriteableBitmap? _scratch;
    private bool _decoded;
    private double _framesPerSecond = 1;
    private DateTimeOffset? _started;
    private (int Width, int Height) _size = (1, 1);
    private int _showing = -1;
    private int _drawn = -1;

    public VrSurface Surface => VrSurface.Avatar;

    public bool Visible { get; private set; }

    public SurfacePlacement Placement =>
        panel.AvatarArea is { } area
            ? VrPlacementMath.Over(panel.Placement.Sane(), panel.DrawnSize, area)
            : panel.Placement;

    public bool TakesPointer => false;

    public (int Width, int Height) Size => _size;

    public bool IsDirty => Visible && _showing != _drawn;

    /// <summary>Through <c>SetOverlayRaw</c> the clip flickers.</summary>
    public bool UploadsAsTexture => true;

    public void Observe(VrPose head)
    {
    }

    /// <summary>Follows the panel's clip, decodes a few more frames, and picks the frame for this time.</summary>
    public void Advance(DateTimeOffset now)
    {
        Follow(panel.ShowingClip);
        Decode();

        if (_frames.Count > 0)
        {
            _started ??= now;
            _showing = FrameAt(now - _started.Value, _framesPerSecond, _frames.Count, _decoded);
        }

        Visible = Shows(panel.Visible, _frames.Count > 0, panel.AvatarArea is not null, panel.AvatarCovered);
    }

    public void Draw(IntPtr destination, int rowBytes)
    {
        var frame = _frames[_showing];
        var width = _size.Width * 4;

        for (var row = 0; row < _size.Height; row++)
        {
            Marshal.Copy(frame, row * width, destination + (row * rowBytes), width);
        }

        _drawn = _showing;
    }

    public void Dispose() => Close();

    /// <summary>Whether the quad goes up: the panel is, a clip is decoded, it was drawn, and nothing covers it.</summary>
    internal static bool Shows(bool panelVisible, bool hasFrames, bool drawnOnPanel, bool covered) =>
        panelVisible && hasFrames && drawnOnPanel && !covered;

    /// <summary>
    /// The frame for this far into the clip: the frames decoded so far in order, then forward and back as
    /// <see cref="Panel.AvatarView.Step"/> plays them, with neither end repeated.
    /// </summary>
    internal static int FrameAt(TimeSpan elapsed, double framesPerSecond, int count, bool decoded)
    {
        if (count < 2)
        {
            return 0;
        }

        var step = (long)(Math.Max(0, elapsed.TotalSeconds) * Math.Max(1, framesPerSecond));

        if (!decoded)
        {
            return (int)Math.Min(step, count - 1);
        }

        var period = 2L * (count - 1);
        var at = step % period;

        return (int)(at < count ? at : period - at);
    }

    private void Follow(string? path)
    {
        if (string.Equals(path, _path, StringComparison.Ordinal))
        {
            return;
        }

        Close();
        _path = path;

        if (path is null || VideoFrames.Open(path) is not { } clip)
        {
            return;
        }

        _clip = clip;
        _scratch = clip.Frame();
        _framesPerSecond = Math.Max(1, clip.FramesPerSecond);
        _size = (clip.Size.Width, clip.Size.Height);
    }

    private void Decode()
    {
        for (var i = 0; i < DecodesPerAdvance && _clip is not null && _scratch is not null; i++)
        {
            if (_clip.Next(_scratch))
            {
                _frames.Add(Opaque(_scratch));
                continue;
            }

            _decoded = true;
            _clip.Dispose();
            _clip = null;
        }
    }

    /// <summary>The frame's BGRA, with the fourth byte set: the clips leave it unused and the overlay is premultiplied.</summary>
    private static byte[] Opaque(WriteableBitmap bitmap)
    {
        using var locked = bitmap.Lock();

        var width = bitmap.PixelSize.Width * 4;
        var pixels = new byte[width * bitmap.PixelSize.Height];

        for (var row = 0; row < bitmap.PixelSize.Height; row++)
        {
            Marshal.Copy(locked.Address + (row * locked.RowBytes), pixels, row * width, width);
        }

        for (var i = 3; i < pixels.Length; i += 4)
        {
            pixels[i] = 0xFF;
        }

        return pixels;
    }

    private void Close()
    {
        _clip?.Dispose();
        _scratch?.Dispose();
        _clip = null;
        _scratch = null;
        _path = null;
        _frames.Clear();
        _decoded = false;
        _started = null;
        _showing = -1;
        _drawn = -1;
    }
}
