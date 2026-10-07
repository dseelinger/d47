using System.Runtime.InteropServices;
using System.Runtime.InteropServices.WindowsRuntime;
using D47.Vr;
using Microsoft.Extensions.Logging;
using Windows.Graphics.Imaging;

namespace D47.App.Diagnostics;

/// <summary>
/// The SteamVR compositor's left-eye image while d47 is attached to SteamVR, and Elite's window
/// otherwise (#601). A failed eye image is reported as the still's error, never replaced by the window.
/// </summary>
public sealed class HeadsetEyeCapture(
    Func<SteamVrRuntime?> headset,
    IWindowCapture window,
    int width,
    ILogger<HeadsetEyeCapture> logger) : IWindowCapture, IDisposable
{
    private readonly Lock _gate = new();

    private nint _device;
    private nint _context;
    private int _adapter = -1;

    private nint _staging;
    private TextureDesc _stagingDesc;

    /// <summary>The source the last still came from, so a change of source is logged once.</summary>
    private string? _source;

    public string? Capture(string path)
    {
        if (headset() is not { } runtime || runtime.HeadsetAdapter() is not { } adapter)
        {
            Source("Elite's window");
            return window.Capture(path);
        }

        try
        {
            lock (_gate)
            {
                return Eye(runtime, adapter, path);
            }
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "A still of the headset could not be taken");
            return ex.Message;
        }
    }

    private string? Eye(SteamVrRuntime runtime, int adapter, string path)
    {
        ObjectDisposedException.ThrowIf(_adapter == int.MinValue, this);

        Device(adapter);

        var source = default(TextureDesc);
        var refused = runtime.MirrorLeftEye(_device, view => source = Copy(view));

        if (refused is not null)
        {
            return refused;
        }

        if (!Readable(source.Format, out var swap))
        {
            return $"the eye image is DXGI format {source.Format}, which a still cannot read";
        }

        Source($"the headset's left eye, {source.Width}x{source.Height}");

        var pixels = Read(source.Width, source.Height, swap);

        using var bitmap = SoftwareBitmap.CreateCopyFromBuffer(
            pixels.AsBuffer(),
            BitmapPixelFormat.Bgra8,
            (int)source.Width,
            (int)source.Height,
            BitmapAlphaMode.Premultiplied);

        EliteWindowCapture.Save(bitmap, path, width);
        return null;
    }

    private void Source(string from)
    {
        if (!string.Equals(Interlocked.Exchange(ref _source, from), from, StringComparison.Ordinal))
        {
            logger.LogInformation("Input trace stills come from {Source}", from);
        }
    }

    /// <summary>
    /// Queues a copy of the view's texture into the staging texture and returns the source's description.
    /// Runs inside the SteamVR session, so it waits on nothing.
    /// </summary>
    private TextureDesc Copy(nint view)
    {
        Slot<GetResourceFn>(view, ViewGetResource)(view, out var resource);

        try
        {
            var iid = Texture2D;
            Marshal.ThrowExceptionForHR(Marshal.QueryInterface(resource, in iid, out var texture));

            try
            {
                Slot<GetDescFn>(texture, TextureGetDesc)(texture, out var desc);

                if (desc.SampleCount != 1)
                {
                    throw new InvalidOperationException($"the eye image is multisampled ({desc.SampleCount}x)");
                }

                Staging(desc);

                Slot<CopySubresourceRegionFn>(_context, ContextCopySubresourceRegion)(
                    _context, _staging, 0, 0, 0, 0, texture, 0, 0);

                return desc;
            }
            finally
            {
                Marshal.Release(texture);
            }
        }
        finally
        {
            Marshal.Release(resource);
        }
    }

    /// <summary>A CPU-readable texture the size and format of the eye image, kept while those hold.</summary>
    private void Staging(TextureDesc source)
    {
        if (_staging != 0
            && _stagingDesc.Width == source.Width
            && _stagingDesc.Height == source.Height
            && _stagingDesc.Format == source.Format)
        {
            return;
        }

        Release(ref _staging);

        var desc = source with
        {
            MipLevels = 1,
            ArraySize = 1,
            SampleCount = 1,
            SampleQuality = 0,
            Usage = UsageStaging,
            BindFlags = 0,
            CpuAccessFlags = CpuAccessRead,
            MiscFlags = 0,
        };

        Marshal.ThrowExceptionForHR(
            Slot<CreateTexture2DFn>(_device, DeviceCreateTexture2D)(_device, ref desc, 0, out _staging));

        _stagingDesc = desc;
    }

    /// <summary>The staging texture's pixels as opaque BGRA rows, waiting for the copy to land.</summary>
    private byte[] Read(uint columns, uint rows, bool swap)
    {
        Marshal.ThrowExceptionForHR(
            Slot<MapFn>(_context, ContextMap)(_context, _staging, 0, MapRead, 0, out var mapped));

        try
        {
            var stride = (int)columns * 4;
            var pixels = new byte[stride * (int)rows];

            for (var row = 0; row < rows; row++)
            {
                Marshal.Copy(mapped.Data + (row * (nint)mapped.RowPitch), pixels, row * stride, stride);
            }

            for (var at = 0; at < pixels.Length; at += 4)
            {
                if (swap)
                {
                    (pixels[at], pixels[at + 2]) = (pixels[at + 2], pixels[at]);
                }

                pixels[at + 3] = 0xFF;
            }

            return pixels;
        }
        finally
        {
            Slot<UnmapFn>(_context, ContextUnmap)(_context, _staging, 0);
        }
    }

    /// <summary>Whether a still can be read from <paramref name="format"/>, and whether red and blue swap.</summary>
    private static bool Readable(uint format, out bool swap)
    {
        swap = format is FormatRgbaTypeless or FormatRgba or FormatRgbaSrgb;
        return swap || format is FormatBgra or FormatBgraTypeless or FormatBgraSrgb;
    }

    /// <summary>
    /// A Direct3D device on the headset's adapter, which the compositor needs before it will share the
    /// eye image. Made once and kept while the adapter stays the same.
    /// </summary>
    private void Device(int adapter)
    {
        if (_device != 0 && _adapter == adapter)
        {
            return;
        }

        Teardown();

        var iid = DxgiFactory1;
        Marshal.ThrowExceptionForHR(CreateDXGIFactory1(in iid, out var factory));

        try
        {
            Marshal.ThrowExceptionForHR(
                Slot<EnumAdapters1Fn>(factory, FactoryEnumAdapters1)(factory, (uint)adapter, out var dxgiAdapter));

            try
            {
                Marshal.ThrowExceptionForHR(D3D11CreateDevice(
                    dxgiAdapter,
                    DriverTypeUnknown,
                    0,
                    0,
                    0,
                    0,
                    SdkVersion,
                    out _device,
                    out _,
                    out _context));
            }
            finally
            {
                Marshal.Release(dxgiAdapter);
            }
        }
        finally
        {
            Marshal.Release(factory);
        }

        _adapter = adapter;
    }

    private void Teardown()
    {
        Release(ref _staging);
        Release(ref _context);
        Release(ref _device);
        _adapter = -1;
    }

    private static void Release(ref nint com)
    {
        if (com != 0)
        {
            Marshal.Release(com);
            com = 0;
        }
    }

    public void Dispose()
    {
        lock (_gate)
        {
            Teardown();
            _adapter = int.MinValue;
        }

        (window as IDisposable)?.Dispose();
    }

    /// <summary>The method at <paramref name="slot"/> in a COM object's table.</summary>
    private static T Slot<T>(nint com, int slot)
        where T : Delegate =>
        Marshal.GetDelegateForFunctionPointer<T>(Marshal.ReadIntPtr(Marshal.ReadIntPtr(com), slot * IntPtr.Size));

    [StructLayout(LayoutKind.Sequential)]
    private record struct TextureDesc(
        uint Width,
        uint Height,
        uint MipLevels,
        uint ArraySize,
        uint Format,
        uint SampleCount,
        uint SampleQuality,
        uint Usage,
        uint BindFlags,
        uint CpuAccessFlags,
        uint MiscFlags);

    [StructLayout(LayoutKind.Sequential)]
    private readonly record struct MappedSubresource(nint Data, uint RowPitch, uint DepthPitch);

    private delegate int EnumAdapters1Fn(nint factory, uint index, out nint adapter);

    private delegate int CreateTexture2DFn(nint device, ref TextureDesc desc, nint initial, out nint texture);

    private delegate void GetResourceFn(nint view, out nint resource);

    private delegate void GetDescFn(nint texture, out TextureDesc desc);

    private delegate int MapFn(
        nint context,
        nint resource,
        uint subresource,
        uint mapType,
        uint flags,
        out MappedSubresource mapped);

    private delegate void UnmapFn(nint context, nint resource, uint subresource);

    private delegate void CopySubresourceRegionFn(
        nint context,
        nint destination,
        uint destinationSubresource,
        uint x,
        uint y,
        uint z,
        nint source,
        uint sourceSubresource,
        nint box);

    private const int FactoryEnumAdapters1 = 12;
    private const int DeviceCreateTexture2D = 5;
    private const int ViewGetResource = 7;
    private const int TextureGetDesc = 10;
    private const int ContextMap = 14;
    private const int ContextUnmap = 15;
    private const int ContextCopySubresourceRegion = 46;

    /// <summary>Required with an explicit adapter.</summary>
    private const uint DriverTypeUnknown = 0;

    private const uint SdkVersion = 7;
    private const uint UsageStaging = 3;
    private const uint CpuAccessRead = 0x20000;
    private const uint MapRead = 1;

    private const uint FormatRgbaTypeless = 27;
    private const uint FormatRgba = 28;
    private const uint FormatRgbaSrgb = 29;
    private const uint FormatBgra = 87;
    private const uint FormatBgraTypeless = 90;
    private const uint FormatBgraSrgb = 91;

    private static readonly Guid DxgiFactory1 = new("770aae78-f26f-4dba-a829-253c83d1b387");
    private static readonly Guid Texture2D = new("6f15aaf2-d208-4e89-9ab4-489535d34f9c");

    [DllImport("dxgi.dll", ExactSpelling = true)]
    private static extern int CreateDXGIFactory1(in Guid iid, out nint factory);

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
}
