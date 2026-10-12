using System.Runtime.InteropServices;

namespace D47.Vr;

/// <summary>
/// A Direct3D 11 device holding the textures handed to <c>SetOverlayTexture</c>. Every call but
/// <see cref="Release"/> and <see cref="Close"/> is made from one thread.
/// </summary>
public interface IVrTextureDevice
{
    /// <summary>Makes the device and its immediate context on a DXGI adapter. Null when made, otherwise why not.</summary>
    string? Open(int adapter);

    /// <summary>A BGRA texture the compositor can show, or zero with <paramref name="refused"/> saying why not.</summary>
    IntPtr Create(int width, int height, out string? refused);

    /// <summary>Queues a copy of BGRA rows into <paramref name="texture"/>. Does not flush.</summary>
    void Write(IntPtr texture, IntPtr pixels, int rowBytes);

    /// <summary>Sends the queued writes to the GPU. Without it the compositor keeps showing an older frame.</summary>
    void Flush();

    void Release(IntPtr texture);

    /// <summary>Releases the context and the device. Issues no context call, so it is safe from another thread.</summary>
    void Close();
}

/// <summary>The real <see cref="IVrTextureDevice"/>, by P/Invoke into <c>d3d11.dll</c> and <c>dxgi.dll</c>.</summary>
public sealed class D3D11TextureDevice : IVrTextureDevice
{
    private nint _device;
    private nint _context;
    private UpdateSubresourceFn? _update;
    private FlushFn? _flush;

    public string? Open(int adapter)
    {
        Close();

        try
        {
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

            _update = Slot<UpdateSubresourceFn>(_context, ContextUpdateSubresource);
            _flush = Slot<FlushFn>(_context, ContextFlush);
            return null;
        }
        catch (Exception ex)
        {
            Close();
            return $"Direct3D 11 would not make a device on adapter {adapter}: {ex.Message}";
        }
    }

    public IntPtr Create(int width, int height, out string? refused)
    {
        var desc = new TextureDesc(
            Width: (uint)width,
            Height: (uint)height,
            MipLevels: 1,
            ArraySize: 1,
            Format: FormatBgra,
            SampleCount: 1,
            SampleQuality: 0,
            Usage: UsageDefault,
            BindFlags: BindShaderResource,
            CpuAccessFlags: 0,
            MiscFlags: 0);

        var made = Slot<CreateTexture2DFn>(_device, DeviceCreateTexture2D)(_device, ref desc, 0, out var texture);

        if (made < 0 || texture == 0)
        {
            refused = $"Direct3D 11 would not make a {width}x{height} texture: 0x{made:X8}";
            return 0;
        }

        refused = null;
        return texture;
    }

    public void Write(IntPtr texture, IntPtr pixels, int rowBytes) =>
        _update!(_context, texture, 0, 0, pixels, (uint)rowBytes, 0);

    public void Flush() => _flush!(_context);

    public void Release(IntPtr texture)
    {
        if (texture != 0)
        {
            Marshal.Release(texture);
        }
    }

    public void Close()
    {
        _update = null;
        _flush = null;
        Release(ref _context);
        Release(ref _device);
    }

    private static void Release(ref nint com)
    {
        if (com != 0)
        {
            Marshal.Release(com);
            com = 0;
        }
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

    private delegate int EnumAdapters1Fn(nint factory, uint index, out nint adapter);

    private delegate int CreateTexture2DFn(nint device, ref TextureDesc desc, nint initial, out nint texture);

    private delegate void FlushFn(nint context);

    private delegate void UpdateSubresourceFn(
        nint context,
        nint resource,
        uint subresource,
        nint box,
        nint data,
        uint rowPitch,
        uint depthPitch);

    private const int FactoryEnumAdapters1 = 12;
    private const int DeviceCreateTexture2D = 5;
    private const int ContextUpdateSubresource = 48;
    private const int ContextFlush = 111;

    /// <summary>Required with an explicit adapter.</summary>
    private const uint DriverTypeUnknown = 0;

    private const uint SdkVersion = 7;
    private const uint FormatBgra = 87;
    private const uint UsageDefault = 0;
    private const uint BindShaderResource = 0x8;

    private static readonly Guid DxgiFactory1 = new("770aae78-f26f-4dba-a829-253c83d1b387");

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
