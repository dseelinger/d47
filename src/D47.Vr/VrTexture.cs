using Vortice.Direct3D;
using Vortice.Direct3D11;
using Vortice.DXGI;

namespace D47.Vr;

/// <summary>
/// The D3D11 device the overlays draw through, and one shared texture per surface.
/// <para>
/// <b>One device for the whole process, not one per overlay.</b> A previous implementation
/// gave each surface its own and it ran perfectly until the first moment two of them submitted
/// in the same session, then took the runtime down with an access violation inside
/// <c>vrclient</c>. OpenVR keeps its graphics state per process rather than per texture, so
/// the second device is not a saving — it is the bug.
/// </para>
/// </summary>
public sealed class VrDevice : IDisposable
{
    private VrDevice(ID3D11Device device, ID3D11DeviceContext context, string adapter, int adapterIndex)
    {
        Device = device;
        Context = context;
        AdapterName = adapter;
        AdapterIndex = adapterIndex;
    }

    public ID3D11Device Device { get; }

    public ID3D11DeviceContext Context { get; }

    public string AdapterName { get; }

    public int AdapterIndex { get; }

    /// <summary>
    /// Creates the device on the adapter SteamVR says it renders on.
    /// <para>
    /// <b>Named rather than defaulted</b>, which is the first of the two things D1 asks Phase 9
    /// to get right. <c>D3D11CreateDevice(null, ...)</c> takes the system default adapter; on
    /// the spike machine that happened to be the same one SteamVR reported, so it got the right
    /// answer by luck and said so. On a hybrid-graphics machine the default can be the iGPU,
    /// and a texture on the wrong adapter is a cross-adapter share that SteamVR will reject or
    /// slow-path — a failure that only ever happens on somebody else's computer.
    /// </para>
    /// </summary>
    public static VrDevice Create(int preferredAdapterIndex)
    {
        using var factory = DXGI.CreateDXGIFactory1<IDXGIFactory1>();

        IDXGIAdapter1? chosen = null;
        var index = -1;

        if (preferredAdapterIndex >= 0
            && factory.EnumAdapters1((uint)preferredAdapterIndex, out var wanted).Success)
        {
            chosen = wanted;
            index = preferredAdapterIndex;
        }

        try
        {
            // A null adapter with DriverType.Unknown is invalid, so the fallback has to switch
            // both together: named adapter with Unknown, or no adapter with Hardware.
            D3D11.D3D11CreateDevice(
                chosen,
                chosen is null ? DriverType.Hardware : DriverType.Unknown,
                DeviceCreationFlags.BgraSupport,
                [FeatureLevel.Level_11_1, FeatureLevel.Level_11_0],
                out var device,
                out _,
                out var context).CheckError();

            var name = chosen?.Description1.Description ?? DescribeDefault(device);
            return new VrDevice(device, context, name, index);
        }
        finally
        {
            chosen?.Dispose();
        }
    }

    public void Dispose()
    {
        Context.Dispose();
        Device.Dispose();
    }

    private static string DescribeDefault(ID3D11Device device)
    {
        using var dxgi = device.QueryInterface<IDXGIDevice>();
        using var adapter = dxgi.GetAdapter();
        return adapter.Description.Description;
    }
}

/// <summary>
/// One surface's pixels on their way to the compositor: a CPU-writable staging texture the
/// panel is rasterised into, copied to a shared texture the compositor reads.
/// <para>
/// The copy is a CPU roundtrip and that is fine — measured at 0.41 ms a frame at panel size,
/// under half a percent of one core at 10 Hz, of which the Skia rasterise is 72%
/// (docs/spikes/vr-texture.md). Zero-copy is not reachable through Avalonia's public API and
/// would be optimising the small end of an already-free budget.
/// </para>
/// </summary>
public sealed class VrTexture : IDisposable
{
    private readonly VrDevice _device;

    private ID3D11Texture2D _shared;
    private ID3D11Texture2D _staging;

    public VrTexture(VrDevice device, int width, int height)
    {
        _device = device;
        Width = width;
        Height = height;
        (_shared, _staging) = Allocate(device, width, height);
    }

    public int Width { get; private set; }

    public int Height { get; private set; }

    /// <summary>
    /// What <c>SetOverlayTexture</c> is given. The <c>ID3D11Texture2D*</c> itself, <b>not</b>
    /// the DXGI share handle — the share handle is what
    /// <see cref="Valve.VR.ETextureType.DXGISharedHandle"/> takes, and passing one with
    /// <see cref="Valve.VR.ETextureType.DirectX"/> is a plausible mistake with an unhelpful
    /// failure.
    /// </summary>
    public IntPtr NativePointer => _shared.NativePointer;

    public void Resize(int width, int height)
    {
        if (width == Width && height == Height)
        {
            return;
        }

        _staging.Dispose();
        _shared.Dispose();

        Width = width;
        Height = height;
        (_shared, _staging) = Allocate(_device, width, height);
    }

    /// <summary>
    /// Maps the staging texture so the rasteriser can write straight into it, removing one
    /// full-surface copy from the frame (the spike's variant B, 25% of the frame cost for
    /// about fifteen lines).
    /// </summary>
    public (IntPtr Address, int RowBytes) Map()
    {
        var mapped = _device.Context.Map(_staging, 0, MapMode.WriteDiscard);
        return (mapped.DataPointer, (int)mapped.RowPitch);
    }

    /// <summary>Unmaps and hands the pixels to the shared texture the compositor reads.</summary>
    public void Commit()
    {
        _device.Context.Unmap(_staging, 0);
        _device.Context.CopyResource(_shared, _staging);
        _device.Context.Flush();
    }

    public void Dispose()
    {
        _staging.Dispose();
        _shared.Dispose();
    }

    private static (ID3D11Texture2D Shared, ID3D11Texture2D Staging) Allocate(
        VrDevice device,
        int width,
        int height)
    {
        // SteamVR's compositor is another process, so the texture it reads has to be
        // shareable. The flag is not optional and its absence is not diagnosable.
        var shared = device.Device.CreateTexture2D(new Texture2DDescription
        {
            Width = (uint)width,
            Height = (uint)height,
            MipLevels = 1,
            ArraySize = 1,
            Format = Format.B8G8R8A8_UNorm,
            SampleDescription = new SampleDescription(1, 0),
            Usage = ResourceUsage.Default,
            BindFlags = BindFlags.ShaderResource | BindFlags.RenderTarget,
            CPUAccessFlags = CpuAccessFlags.None,
            MiscFlags = ResourceOptionFlags.Shared,
        });

        var staging = device.Device.CreateTexture2D(new Texture2DDescription
        {
            Width = (uint)width,
            Height = (uint)height,
            MipLevels = 1,
            ArraySize = 1,
            Format = Format.B8G8R8A8_UNorm,
            SampleDescription = new SampleDescription(1, 0),
            Usage = ResourceUsage.Dynamic,
            BindFlags = BindFlags.ShaderResource,
            CPUAccessFlags = CpuAccessFlags.Write,
            MiscFlags = ResourceOptionFlags.None,
        });

        return (shared, staging);
    }
}
