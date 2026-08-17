using System.Runtime.InteropServices;
using OpenCvSharp;
using Windows.Graphics.Capture;
using Windows.Graphics.DirectX;
using Windows.Graphics.DirectX.Direct3D11;
using Windows.Graphics.Imaging;
using Windows.Storage.Streams;
using WinRT;

namespace MirrorProbe;

/// <summary>
/// Windows Graphics Capture — the path d47 would actually use, so the frames this spike judges are
/// the frames the product would get. It is the only one of the three that is documented to work
/// against a window that has taken the display exclusively.
/// <para>
/// The readback deliberately goes out through <see cref="BitmapEncoder"/> and back in through
/// <see cref="Cv2.ImDecode(byte[], ImreadModes)"/> rather than mapping the texture. A spike takes a
/// handful of frames and does not care what that costs, and it buys the whole path in projected
/// WinRT calls with no COM marshalling to get wrong. A product that captured continuously would
/// map the staging texture instead; that is a performance decision and not this measurement.
/// </para>
/// </summary>
internal static class WgcCapture
{
    private const uint D3D11_SDK_VERSION = 7;
    private const uint D3D11_CREATE_DEVICE_BGRA_SUPPORT = 0x20;
    private const uint D3D_DRIVER_TYPE_HARDWARE = 1;

    private static readonly Guid IID_IDXGIDevice = new("54ec77fa-1377-44e6-8c32-88fd5f44c84c");

    [DllImport("d3d11.dll", ExactSpelling = true)]
    private static extern int D3D11CreateDevice(
        IntPtr adapter,
        uint driverType,
        IntPtr software,
        uint flags,
        IntPtr featureLevels,
        uint featureLevelCount,
        uint sdkVersion,
        out IntPtr device,
        out uint featureLevel,
        out IntPtr context);

    [DllImport("d3d11.dll", ExactSpelling = true)]
    private static extern int CreateDirect3D11DeviceFromDXGIDevice(IntPtr dxgiDevice, out IntPtr graphicsDevice);

    /// <summary>
    /// Captures one frame of the given window. Returns null with a reason rather than throwing:
    /// every way this fails is a finding.
    /// </summary>
    internal static Mat? Capture(IntPtr window, out string note)
    {
        note = string.Empty;

        if (!GraphicsCaptureSession.IsSupported())
        {
            note = "Windows Graphics Capture reports itself unsupported on this machine.";
            return null;
        }

        GraphicsCaptureItem? item;

        try
        {
            item = GraphicsCaptureItem.TryCreateFromWindowId(new Windows.UI.WindowId((ulong)window.ToInt64()));
        }
        catch (Exception error)
        {
            note = $"TryCreateFromWindowId threw: {error.Message}";
            return null;
        }

        if (item is null)
        {
            note = "TryCreateFromWindowId returned nothing for that window.";
            return null;
        }

        IDirect3DDevice device;

        try
        {
            device = CreateDevice();
        }
        catch (Exception error)
        {
            note = $"Could not create a Direct3D device: {error.Message}";
            return null;
        }

        using var framePool = Direct3D11CaptureFramePool.CreateFreeThreaded(
            device,
            DirectXPixelFormat.B8G8R8A8UIntNormalized,
            2,
            item.Size);

        using var session = framePool.CreateCaptureSession(item);

        // The yellow capture border is a Windows 11 courtesy and would land in the frame this
        // spike measures. Older builds do not expose the property at all, hence the guard.
        if (ApiInformation.IsCaptureBorderTogglePresent())
        {
            session.IsBorderRequired = false;
        }

        session.IsCursorCaptureEnabled = false;
        session.StartCapture();

        try
        {
            // The first frame a pool hands back is frequently the one that was already in flight.
            // Take several and keep the last, which is cheap and removes a whole class of
            // "the panel was not open yet" confusion from the report.
            Direct3D11CaptureFrame? latest = null;
            var deadline = DateTime.UtcNow.AddSeconds(3);
            var taken = 0;

            while (DateTime.UtcNow < deadline && taken < 5)
            {
                var frame = framePool.TryGetNextFrame();

                if (frame is null)
                {
                    Thread.Sleep(16);
                    continue;
                }

                latest?.Dispose();
                latest = frame;
                taken++;
            }

            if (latest is null)
            {
                note = "The frame pool produced no frame within three seconds.";
                return null;
            }

            using (latest)
            {
                return Decode(latest.Surface);
            }
        }
        finally
        {
            framePool.Dispose();
        }
    }

    private static IDirect3DDevice CreateDevice()
    {
        var hr = D3D11CreateDevice(
            IntPtr.Zero,
            D3D_DRIVER_TYPE_HARDWARE,
            IntPtr.Zero,
            D3D11_CREATE_DEVICE_BGRA_SUPPORT,
            IntPtr.Zero,
            0,
            D3D11_SDK_VERSION,
            out var d3dDevice,
            out _,
            out var context);

        Marshal.ThrowExceptionForHR(hr);

        try
        {
            var iid = IID_IDXGIDevice;
            Marshal.ThrowExceptionForHR(Marshal.QueryInterface(d3dDevice, in iid, out var dxgiDevice));

            try
            {
                Marshal.ThrowExceptionForHR(CreateDirect3D11DeviceFromDXGIDevice(dxgiDevice, out var inspectable));

                try
                {
                    return MarshalInspectable<IDirect3DDevice>.FromAbi(inspectable);
                }
                finally
                {
                    Marshal.Release(inspectable);
                }
            }
            finally
            {
                Marshal.Release(dxgiDevice);
            }
        }
        finally
        {
            if (context != IntPtr.Zero)
            {
                Marshal.Release(context);
            }

            Marshal.Release(d3dDevice);
        }
    }

    private static Mat Decode(IDirect3DSurface surface)
    {
        using var bitmap = SoftwareBitmap.CreateCopyFromSurfaceAsync(surface).GetAwaiter().GetResult();

        // The encoder will not take a premultiplied-alpha bitmap, which is what a capture surface
        // always is.
        using var straight = SoftwareBitmap.Convert(bitmap, BitmapPixelFormat.Bgra8, BitmapAlphaMode.Straight);

        using var stream = new InMemoryRandomAccessStream();

        var encoder = BitmapEncoder.CreateAsync(BitmapEncoder.PngEncoderId, stream).GetAwaiter().GetResult();
        encoder.SetSoftwareBitmap(straight);
        encoder.FlushAsync().GetAwaiter().GetResult();

        var bytes = new byte[stream.Size];

        using (var reader = new DataReader(stream.GetInputStreamAt(0)))
        {
            reader.LoadAsync((uint)stream.Size).GetAwaiter().GetResult();
            reader.ReadBytes(bytes);
        }

        return Cv2.ImDecode(bytes, ImreadModes.Color);
    }

    private static class ApiInformation
    {
        internal static bool IsCaptureBorderTogglePresent() =>
            Windows.Foundation.Metadata.ApiInformation.IsPropertyPresent(
                "Windows.Graphics.Capture.GraphicsCaptureSession",
                "IsBorderRequired");
    }
}
