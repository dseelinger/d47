using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using D47.Core.Vr;
using Microsoft.Extensions.Logging;

namespace D47.Vr;

/// <summary>
/// One decoded picture: <see cref="Height"/> packed rows of <see cref="Width"/> pixels, four bytes each in the order
/// blue, green, red and an unused byte.
/// </summary>
/// <param name="Key">The same object for as long as it is the same picture.</param>
public readonly record struct VrPicture(object Key, byte[] Pixels, int Width, int Height);

/// <summary>
/// The video quad. <see cref="Visible"/> and <see cref="Placement"/> are read on the serve; <see cref="Picture"/> is
/// read on the presenter thread.
/// </summary>
public interface IVrVideoSource
{
    bool Visible { get; }

    SurfacePlacement Placement { get; }

    /// <summary>The newest picture, or null. Safe from any thread, and does not block.</summary>
    VrPicture? Picture { get; }
}

/// <summary>Runs the video quad's presenter.</summary>
public interface IVrVideoClock
{
    /// <summary>
    /// Calls <paramref name="step"/> over and over, then <paramref name="finish"/> once, all on one thread of its own.
    /// Disposing the result stops the steps and waits for <paramref name="finish"/>.
    /// </summary>
    IDisposable Run(Action step, Action finish);
}

/// <summary>The real <see cref="IVrVideoClock"/>: a dedicated background thread stepping every <see cref="Period"/>.</summary>
public sealed class VrVideoThread(ILogger logger) : IVrVideoClock
{
    /// <summary>Well under a frame at 60 fps, so each new picture goes up within a timer tick of being published.</summary>
    public static readonly TimeSpan Period = TimeSpan.FromMilliseconds(5);

    public IDisposable Run(Action step, Action finish) => new Running(step, finish, logger);

    private sealed class Running : IDisposable
    {
        private readonly Action _step;
        private readonly Action _finish;
        private readonly ILogger _logger;
        private readonly CancellationTokenSource _stopping = new();
        private readonly Thread _thread;
        private bool _complained;

        public Running(Action step, Action finish, ILogger logger)
        {
            _step = step;
            _finish = finish;
            _logger = logger;
            _thread = new Thread(Run) { IsBackground = true, Name = "d47-vr-video" };
            _thread.Start();
        }

        public void Dispose()
        {
            _stopping.Cancel();

            if (_thread.IsAlive && !_thread.Join(TimeSpan.FromSeconds(2)))
            {
                _logger.LogWarning("The VR video presenter did not stop within two seconds");
            }

            _stopping.Dispose();
        }

        private void Run()
        {
            using var timer = new PeriodicTimer(Period);

            try
            {
                while (timer.WaitForNextTickAsync(_stopping.Token).AsTask().GetAwaiter().GetResult())
                {
                    try
                    {
                        _step();
                    }
                    catch (Exception ex)
                    {
                        if (!_complained)
                        {
                            _complained = true;
                            _logger.LogError(ex, "The VR video presenter threw; it carries on with the next picture");
                        }
                    }
                }
            }
            catch (OperationCanceledException)
            {
            // Stopped.
            }
            finally
            {
                try
                {
                    _finish();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "The VR video presenter could not give its textures back");
                }
            }
        }
    }
}

/// <summary>
/// The presenter's copy of the newest picture, with the unused byte set opaque and every channel multiplied by the
/// opacity, as premultiplied BGRA ready for a texture. Used from the presenter thread only.
/// </summary>
internal sealed class VrVideoCopy
{
    private object? _key;
    private float _opacity = float.NaN;
    private byte[] _pixels = [];

    public int Width { get; private set; }

    public int Height { get; private set; }

    public int RowBytes => Width * 4;

    /// <summary>Where the copy is. Pinned, so it stays put.</summary>
    public IntPtr Address { get; private set; }

    /// <summary>Copies the picture when it or the opacity changed since the last copy, and says whether it did.</summary>
    public bool Take(VrPicture? picture, float opacity)
    {
        if (picture is not { } taken
            || taken.Width <= 0
            || taken.Height <= 0
            || taken.Width > XrLayer.XrBlock.MaxTextureSide
            || taken.Height > XrLayer.XrBlock.MaxTextureSide
            || taken.Pixels.Length < taken.Width * taken.Height * 4)
        {
            return false;
        }

        if (ReferenceEquals(taken.Key, _key) && opacity.Equals(_opacity))
        {
            return false;
        }

        var length = taken.Width * taken.Height * 4;

        if (_pixels.Length != length)
        {
            _pixels = GC.AllocateArray<byte>(length, pinned: true);

            unsafe
            {
                Address = (IntPtr)Unsafe.AsPointer(ref MemoryMarshal.GetArrayDataReference(_pixels));
            }
        }

        Opaque(taken.Pixels.AsSpan(0, length), _pixels, opacity);

        Width = taken.Width;
        Height = taken.Height;
        _key = taken.Key;
        _opacity = opacity;
        return true;
    }

    /// <summary>Makes the next <see cref="Take"/> copy whatever it is given.</summary>
    public void Forget() => _key = null;

    /// <summary>The fourth byte set to 255, then every channel scaled by <paramref name="opacity"/>.</summary>
    internal static void Opaque(ReadOnlySpan<byte> from, Span<byte> to, float opacity)
    {
        var source = MemoryMarshal.Cast<byte, uint>(from);
        var target = MemoryMarshal.Cast<byte, uint>(to);

        if (opacity >= 1f)
        {
            for (var i = 0; i < source.Length; i++)
            {
                target[i] = source[i] | 0xFF000000u;
            }

            return;
        }

        var scale = (uint)Math.Round(Math.Clamp(opacity, 0f, 1f) * 256f);

        for (var i = 0; i < source.Length; i++)
        {
            var pixel = source[i] | 0xFF000000u;
            var blueRed = ((pixel & 0x00FF00FFu) * scale >> 8) & 0x00FF00FFu;
            var greenAlpha = ((pixel >> 8) & 0x00FF00FFu) * scale & 0xFF00FF00u;
            target[i] = blueRed | greenAlpha;
        }
    }
}
