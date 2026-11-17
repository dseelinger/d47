using System.Runtime.InteropServices;
using static D47.Vr.D3D11Native;

namespace D47.Vr;

/// <summary>
/// A Direct3D 11 device holding the textures handed to <c>SetOverlayTexture</c> or shared with the OpenXR
/// layer. Every call but <see cref="Release"/> and <see cref="Close"/> is made from one thread.
/// </summary>
public interface IVrTextureDevice
{
    /// <summary>Makes the device and its immediate context on a DXGI adapter. Null when made, otherwise why not.</summary>
    string? Open(int adapter);

    /// <summary>Makes the device on the DXGI adapter with this LUID. Null when made, otherwise why not.</summary>
    string? OpenOnLuid(uint luidLow, int luidHigh);

    /// <summary>A BGRA texture the compositor can show, or zero with <paramref name="refused"/> saying why not.</summary>
    IntPtr Create(int width, int height, out string? refused);

    /// <summary>
    /// A BGRA texture another process opens by <paramref name="name"/>, or zero with <paramref name="refused"/>
    /// saying why not. The name lasts until <see cref="Release"/>.
    /// </summary>
    IntPtr CreateShared(int width, int height, string name, out string? refused);

    /// <summary>Queues a copy of BGRA rows into <paramref name="texture"/>. Does not flush.</summary>
    void Write(IntPtr texture, IntPtr pixels, int rowBytes);

    /// <summary>
    /// Queues a copy of a <paramref name="width"/> by <paramref name="height"/> block of BGRA rows into the top
    /// left of <paramref name="texture"/>. Does not flush.
    /// </summary>
    void Write(IntPtr texture, IntPtr pixels, int rowBytes, int width, int height);

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

    /// <summary>The NT handle that names each shared texture, closed with it.</summary>
    private readonly Dictionary<nint, nint> _shared = [];
    private readonly Lock _sharedLock = new();

    public string? Open(int adapter)
    {
        Close();

        try
        {
            D3D11Native.CreateDevice(adapter, out _device, out _context);

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

    public string? OpenOnLuid(uint luidLow, int luidHigh)
    {
        int? adapter;

        try
        {
            adapter = D3D11Native.AdapterWithLuid(luidLow, luidHigh);
        }
        catch (Exception ex)
        {
            return $"DXGI would not list the adapters: {ex.Message}";
        }

        return adapter is { } index
            ? Open(index)
            : $"No DXGI adapter has the LUID {luidHigh:X8}:{luidLow:X8}";
    }

    public IntPtr CreateShared(int width, int height, string name, out string? refused)
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
            BindFlags: BindShaderResource | BindRenderTarget,
            CpuAccessFlags: 0,
            MiscFlags: MiscShared | MiscSharedNtHandle);

        var made = Slot<CreateTexture2DFn>(_device, DeviceCreateTexture2D)(_device, ref desc, 0, out var texture);

        if (made < 0 || texture == 0)
        {
            refused = $"Direct3D 11 would not make a shared {width}x{height} texture: 0x{made:X8}";
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

            lock (_sharedLock)
            {
                _shared[texture] = handle;
            }
        }
        finally
        {
            Marshal.Release(resource);
        }

        refused = null;
        return texture;
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

    public unsafe void Write(IntPtr texture, IntPtr pixels, int rowBytes, int width, int height)
    {
        var box = new Box(0, 0, 0, (uint)width, (uint)height, 1);
        _update!(_context, texture, 0, (nint)(&box), pixels, (uint)rowBytes, 0);
    }

    public void Flush() => _flush!(_context);

    public void Release(IntPtr texture)
    {
        if (texture == 0)
        {
            return;
        }

        nint handle;

        lock (_sharedLock)
        {
            _shared.Remove(texture, out handle);
        }

        if (handle != 0)
        {
            CloseHandle(handle);
        }

        Marshal.Release(texture);
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

    private delegate int CreateTexture2DFn(nint device, ref TextureDesc desc, nint initial, out nint texture);

    private delegate void FlushFn(nint context);

    private delegate int CreateSharedHandleFn(
        nint resource,
        nint attributes,
        uint access,
        [MarshalAs(UnmanagedType.LPWStr)] string name,
        out nint handle);

    [StructLayout(LayoutKind.Sequential)]
    private readonly record struct Box(uint Left, uint Top, uint Front, uint Right, uint Bottom, uint Back);

    [DllImport("kernel32.dll", ExactSpelling = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CloseHandle(nint handle);

    private delegate void UpdateSubresourceFn(
        nint context,
        nint resource,
        uint subresource,
        nint box,
        nint data,
        uint rowPitch,
        uint depthPitch);

    private const int DeviceCreateTexture2D = 5;
    private const int ContextUpdateSubresource = 48;
    private const int ContextFlush = 111;
    private const int ResourceCreateSharedHandle = 13;

    private const uint FormatBgra = 87;
    private const uint UsageDefault = 0;
    private const uint BindShaderResource = 0x8;
    private const uint BindRenderTarget = 0x20;
    private const uint MiscShared = 0x2;
    private const uint MiscSharedNtHandle = 0x800;
    private const uint SharedResourceRead = 0x80000000;
    private const uint SharedResourceWrite = 0x1;

    private static readonly Guid DxgiResource1 = new("30961379-4609-4a41-998e-54fe567ee0c1");
}
