using System.Runtime.InteropServices;
using static D47.Vr.D3D11Native;

namespace D47.Vr;

/// <summary>A Direct3D 11 device that makes the texture the OpenXR layer copies Elite's eye into, and reads it back.</summary>
public interface IVrEyeReader
{
    /// <summary>Makes the device on the DXGI adapter with this LUID, or keeps the one already there. Null when made, otherwise why not.</summary>
    string? OpenOnLuid(uint luidLow, int luidHigh);

    /// <summary>
    /// A texture of <paramref name="format"/> another process opens by <paramref name="name"/> to write, or zero with
    /// <paramref name="refused"/> saying why not.
    /// </summary>
    IntPtr CreateShared(int width, int height, uint format, string name, out string? refused);

    /// <summary>The texture's pixels as opaque BGRA rows, or null with <paramref name="refused"/> saying why not.</summary>
    byte[]? Read(IntPtr texture, out string? refused);

    void Release(IntPtr texture);

    void Close();
}

/// <summary>The real <see cref="IVrEyeReader"/>, by P/Invoke into <c>d3d11.dll</c> and <c>dxgi.dll</c>.</summary>
public sealed class D3D11EyeReader : IVrEyeReader
{
    private readonly Lock _gate = new();
    private readonly Dictionary<nint, nint> _shared = [];

    private nint _device;
    private nint _context;
    private (uint Low, int High)? _luid;

    public string? OpenOnLuid(uint luidLow, int luidHigh)
    {
        lock (_gate)
        {
            if (_device != 0 && _luid == (luidLow, luidHigh))
            {
                return null;
            }

            Teardown();

            try
            {
                if (AdapterWithLuid(luidLow, luidHigh) is not { } adapter)
                {
                    return $"No DXGI adapter has the LUID {luidHigh:X8}:{luidLow:X8}";
                }

                CreateDevice(adapter, out _device, out _context);
                _luid = (luidLow, luidHigh);
                return null;
            }
            catch (Exception ex)
            {
                Teardown();
                return $"Direct3D 11 would not make a device for the eye: {ex.Message}";
            }
        }
    }

    public IntPtr CreateShared(int width, int height, uint format, string name, out string? refused)
    {
        lock (_gate)
        {
            if (_device == 0)
            {
                refused = "the device for the eye is closed";
                return 0;
            }

            var desc = new TextureDesc(
                Width: (uint)width,
                Height: (uint)height,
                MipLevels: 1,
                ArraySize: 1,
                Format: format,
                SampleCount: 1,
                SampleQuality: 0,
                Usage: UsageDefault,
                BindFlags: BindShaderResource | BindRenderTarget,
                CpuAccessFlags: 0,
                MiscFlags: MiscShared | MiscSharedNtHandle);

            var made = Slot<CreateTexture2DFn>(_device, DeviceCreateTexture2D)(_device, ref desc, 0, out var texture);

            if (made < 0 || texture == 0)
            {
                refused = $"Direct3D 11 would not make a shared {width}x{height} texture of DXGI format {format}: 0x{made:X8}";
                return 0;
            }

            var iid = DxgiResource1;
            var queried = Marshal.QueryInterface(texture, in iid, out var resource);

            if (queried < 0)
            {
                Marshal.Release(texture);
                refused = $"The texture has no IDXGIResource1: 0x{queried:X8}";
                return 0;
            }

            try
            {
                var named = Slot<CreateSharedHandleFn>(resource, ResourceCreateSharedHandle)(
                    resource, 0, SharedResourceRead | SharedResourceWrite, name, out var handle);

                if (named < 0 || handle == 0)
                {
                    Marshal.Release(texture);
                    refused = $"DXGI would not share the texture as {name}: 0x{named:X8}";
                    return 0;
                }

                _shared[texture] = handle;
            }
            finally
            {
                Marshal.Release(resource);
            }

            refused = null;
            return texture;
        }
    }

    public byte[]? Read(IntPtr texture, out string? refused)
    {
        lock (_gate)
        {
            if (_device == 0)
            {
                refused = "the device for the eye is closed";
                return null;
            }

            nint staging = 0;

            try
            {
                Slot<GetDescFn>(texture, TextureGetDesc)(texture, out var source);

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
                    Slot<CreateTexture2DFn>(_device, DeviceCreateTexture2D)(_device, ref desc, 0, out staging));

                Slot<CopyResourceFn>(_context, ContextCopyResource)(_context, staging, texture);

                Marshal.ThrowExceptionForHR(
                    Slot<MapFn>(_context, ContextMap)(_context, staging, 0, MapRead, 0, out var mapped));

                try
                {
                    var stride = (int)desc.Width * 4;
                    var pixels = new byte[stride * (int)desc.Height];

                    for (var row = 0; row < desc.Height; row++)
                    {
                        Marshal.Copy(mapped.Data + (row * (nint)mapped.RowPitch), pixels, row * stride, stride);
                    }

                    EyeFormats.ToOpaqueBgra(pixels, desc.Format);
                    refused = null;
                    return pixels;
                }
                finally
                {
                    Slot<UnmapFn>(_context, ContextUnmap)(_context, staging, 0);
                }
            }
            catch (Exception ex)
            {
                refused = $"the eye image could not be read back: {ex.Message}";
                return null;
            }
            finally
            {
                if (staging != 0)
                {
                    Marshal.Release(staging);
                }
            }
        }
    }

    public void Release(IntPtr texture)
    {
        if (texture == 0)
        {
            return;
        }

        lock (_gate)
        {
            if (_shared.Remove(texture, out var handle))
            {
                CloseHandle(handle);
            }

            Marshal.Release(texture);
        }
    }

    public void Close()
    {
        lock (_gate)
        {
            Teardown();
        }
    }

    private void Teardown()
    {
        foreach (var handle in _shared.Values)
        {
            CloseHandle(handle);
        }

        _shared.Clear();

        if (_context != 0)
        {
            Marshal.Release(_context);
            _context = 0;
        }

        if (_device != 0)
        {
            Marshal.Release(_device);
            _device = 0;
        }

        _luid = null;
    }

    [StructLayout(LayoutKind.Sequential)]
    private readonly record struct MappedSubresource(nint Data, uint RowPitch, uint DepthPitch);

    private delegate int CreateTexture2DFn(nint device, ref TextureDesc desc, nint initial, out nint texture);

    private delegate void GetDescFn(nint texture, out TextureDesc desc);

    private delegate void CopyResourceFn(nint context, nint destination, nint source);

    private delegate int MapFn(nint context, nint resource, uint subresource, uint mapType, uint flags, out MappedSubresource mapped);

    private delegate void UnmapFn(nint context, nint resource, uint subresource);

    private delegate int CreateSharedHandleFn(
        nint resource,
        nint attributes,
        uint access,
        [MarshalAs(UnmanagedType.LPWStr)] string name,
        out nint handle);

    [DllImport("kernel32.dll", ExactSpelling = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CloseHandle(nint handle);

    private const int DeviceCreateTexture2D = 5;
    private const int TextureGetDesc = 10;
    private const int ContextMap = 14;
    private const int ContextUnmap = 15;
    private const int ContextCopyResource = 47;
    private const int ResourceCreateSharedHandle = 13;

    private const uint UsageDefault = 0;
    private const uint UsageStaging = 3;
    private const uint BindShaderResource = 0x8;
    private const uint BindRenderTarget = 0x20;
    private const uint CpuAccessRead = 0x20000;
    private const uint MiscShared = 0x2;
    private const uint MiscSharedNtHandle = 0x800;
    private const uint MapRead = 1;
    private const uint SharedResourceRead = 0x80000000;
    private const uint SharedResourceWrite = 0x1;

    private static readonly Guid DxgiResource1 = new("30961379-4609-4a41-998e-54fe567ee0c1");
}

/// <summary>The DXGI formats of an eye image a picture can read.</summary>
public static class EyeFormats
{
    private const uint RgbaTypeless = 27;
    private const uint Rgba = 28;
    private const uint RgbaSrgb = 29;
    private const uint Bgra = 87;
    private const uint BgraTypeless = 90;
    private const uint BgraSrgb = 91;

    /// <summary>Whether a picture can be read from an image of <paramref name="format"/>.</summary>
    public static bool Readable(uint format) =>
        format is RgbaTypeless or Rgba or RgbaSrgb or Bgra or BgraTypeless or BgraSrgb;

    /// <summary>Turns rows of <paramref name="format"/> into BGRA in place, with every alpha 255.</summary>
    public static void ToOpaqueBgra(byte[] pixels, uint format)
    {
        var swap = format is RgbaTypeless or Rgba or RgbaSrgb;

        for (var at = 0; at + 3 < pixels.Length; at += 4)
        {
            if (swap)
            {
                (pixels[at], pixels[at + 2]) = (pixels[at + 2], pixels[at]);
            }

            pixels[at + 3] = 0xFF;
        }
    }
}
