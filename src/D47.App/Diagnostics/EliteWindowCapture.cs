using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;
using Windows.Graphics.Capture;
using Windows.Graphics.DirectX;
using Windows.Graphics.DirectX.Direct3D11;
using Windows.Graphics.Imaging;
using Windows.Storage.Streams;

namespace D47.App.Diagnostics;

/// <summary>A still of the game window, for the input trace to put beside a step (#365).</summary>
public interface IWindowCapture
{
    /// <summary>Writes a still to <paramref name="path"/>.</summary>
    string? Capture(string path);
}

/// <summary>Elite's window, captured with Windows Graphics Capture (#365).</summary>
public sealed class EliteWindowCapture(
    Func<nint> window,
    int width,
    ILogger<EliteWindowCapture> logger) : IWindowCapture, IDisposable
{
    /// <summary>How long to wait for the compositor to hand over a frame.</summary>
    private static readonly TimeSpan Patience = TimeSpan.FromSeconds(2);

    private readonly Lock _gate = new();

    private IDirect3DDevice? _device;

    public string? Capture(string path)
    {
        try
        {
            if (!GraphicsCaptureSession.IsSupported())
            {
                return "Windows Graphics Capture is not available on this machine";
            }

            var handle = window();

            if (handle == 0)
            {
                return "Elite's window could not be found";
            }

            var item = ItemFor(handle);

            using var pool = Direct3D11CaptureFramePool.CreateFreeThreaded(
                Device(),
                DirectXPixelFormat.B8G8R8A8UIntNormalized,
                1,
                item.Size);

            using var session = pool.CreateCaptureSession(item);

            // Both are newer than the capture API itself and both are cosmetic, so a machine that does not
            // have them still gets its still.
            Quietly(() => session.IsBorderRequired = false);
            Quietly(() => session.IsCursorCaptureEnabled = false);

            session.StartCapture();

            var waited = Stopwatch.StartNew();
            Direct3D11CaptureFrame? frame = null;

            while (frame is null && waited.Elapsed < Patience)
            {
                frame = pool.TryGetNextFrame();

                if (frame is null)
                {
                    Thread.Sleep(15);
                }
            }

            if (frame is null)
            {
                return $"no frame arrived within {Patience.TotalSeconds:0.#}s";
            }

            using (frame)
            {
                Save(frame.Surface, path, width);
            }

            return null;
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "A still of Elite could not be taken");
            return ex.Message;
        }
    }

    private static void Quietly(Action set)
    {
        try
        {
            set();
        }
        catch (Exception)
        {
        // An older Windows without the property.
        }
    }

    /// <summary>The frame to a PNG, downscaled on the way out.</summary>
    private static void Save(IDirect3DSurface surface, string path, int width)
    {
        using var bitmap = SoftwareBitmap
            .CreateCopyFromSurfaceAsync(surface, BitmapAlphaMode.Premultiplied)
            .AsTask()
            .GetAwaiter()
            .GetResult();

        using var stream = new InMemoryRandomAccessStream();

        var encoder = BitmapEncoder
            .CreateAsync(BitmapEncoder.PngEncoderId, stream)
            .AsTask()
            .GetAwaiter()
            .GetResult();

        encoder.SetSoftwareBitmap(bitmap);

        if (bitmap.PixelWidth > width)
        {
            encoder.BitmapTransform.ScaledWidth = (uint)width;
            encoder.BitmapTransform.ScaledHeight =
                (uint)Math.Max(1, (long)bitmap.PixelHeight * width / bitmap.PixelWidth);
            encoder.BitmapTransform.InterpolationMode = BitmapInterpolationMode.Fant;
        }

        encoder.FlushAsync().AsTask().GetAwaiter().GetResult();

        var bytes = new byte[stream.Size];

        using var reader = new DataReader(stream.GetInputStreamAt(0));

        reader.LoadAsync((uint)stream.Size).AsTask().GetAwaiter().GetResult();
        reader.ReadBytes(bytes);

        File.WriteAllBytes(path, bytes);
    }

    /// <summary>
    /// The Direct3D device the frame pool draws into, made once and kept: creating one per still is the
    /// expensive half of a capture.
    /// </summary>
    private IDirect3DDevice Device()
    {
        lock (_gate)
        {
            if (_device is { } existing)
            {
                return existing;
            }

            Marshal.ThrowExceptionForHR(D3D11CreateDevice(
                0,
                DriverTypeHardware,
                0,
                BgraSupport,
                0,
                0,
                SdkVersion,
                out var d3d,
                out _,
                out var context));

            if (context != 0)
            {
                Marshal.Release(context);
            }

            try
            {
                var dxgiIid = DxgiDevice;

                Marshal.ThrowExceptionForHR(Marshal.QueryInterface(d3d, in dxgiIid, out var dxgi));

                try
                {
                    Marshal.ThrowExceptionForHR(
                        CreateDirect3D11DeviceFromDXGIDevice(dxgi, out var inspectable));

                    try
                    {
                        _device = WinRT.MarshalInspectable<IDirect3DDevice>.FromAbi(inspectable);
                    }
                    finally
                    {
                        Marshal.Release(inspectable);
                    }
                }
                finally
                {
                    Marshal.Release(dxgi);
                }
            }
            finally
            {
                Marshal.Release(d3d);
            }

            return _device!;
        }
    }

    /// <summary>A capture item for one window.</summary>
    private static GraphicsCaptureItem ItemFor(nint handle)
    {
        var iid = CaptureItem;
        var abi = Factory().CreateForWindow(handle, ref iid);

        try
        {
            return WinRT.MarshalInspectable<GraphicsCaptureItem>.FromAbi(abi);
        }
        finally
        {
            Marshal.Release(abi);
        }
    }

    private static IGraphicsCaptureItemInterop Factory()
    {
        const string className = "Windows.Graphics.Capture.GraphicsCaptureItem";

        Marshal.ThrowExceptionForHR(WindowsCreateString(className, className.Length, out var name));

        try
        {
            var iid = typeof(IGraphicsCaptureItemInterop).GUID;

            Marshal.ThrowExceptionForHR(RoGetActivationFactory(name, ref iid, out var factory));

            try
            {
                return (IGraphicsCaptureItemInterop)Marshal.GetObjectForIUnknown(factory);
            }
            finally
            {
                Marshal.Release(factory);
            }
        }
        finally
        {
            WindowsDeleteString(name);
        }
    }

    public void Dispose()
    {
        lock (_gate)
        {
            (_device as IDisposable)?.Dispose();
            _device = null;
        }
    }

    /// <summary>Hardware rather than WARP: the game is already on the adapter.</summary>
    private const uint DriverTypeHardware = 1;

    /// <summary>What the imaging stack needs of the device to hand a surface over as BGRA.</summary>
    private const uint BgraSupport = 0x20;

    private const uint SdkVersion = 7;

    private static readonly Guid DxgiDevice = new("54ec77fa-1377-44e6-8c32-88fd5f44c84c");

    /// <summary><c>IGraphicsCaptureItem</c>, which the factory is asked to produce.</summary>
    private static readonly Guid CaptureItem = new("79c3f95b-31f7-4ec2-a464-632ef5d30760");

    [ComImport]
    [Guid("3628E81B-3CAC-4C60-B7F4-23CE0E0C3356")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IGraphicsCaptureItemInterop
    {
        nint CreateForWindow(nint window, ref Guid iid);

        nint CreateForMonitor(nint monitor, ref Guid iid);
    }

    [DllImport("d3d11.dll", ExactSpelling = true)]
    private static extern int D3D11CreateDevice(
        nint adapter,
        uint driverType,
        nint software,
        uint flags,
        nint featureLevels,
        uint levels,
        uint sdkVersion,
        out nint device,
        out uint featureLevel,
        out nint context);

    [DllImport("d3d11.dll", ExactSpelling = true)]
    private static extern int CreateDirect3D11DeviceFromDXGIDevice(nint dxgiDevice, out nint graphicsDevice);

    [DllImport("combase.dll", ExactSpelling = true, CharSet = CharSet.Unicode)]
    private static extern int WindowsCreateString(string source, int length, out nint value);

    [DllImport("combase.dll", ExactSpelling = true)]
    private static extern int WindowsDeleteString(nint value);

    [DllImport("combase.dll", ExactSpelling = true)]
    private static extern int RoGetActivationFactory(nint className, ref Guid iid, out nint factory);
}
