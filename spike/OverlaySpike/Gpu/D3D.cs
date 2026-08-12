using System;
using Vortice.Direct3D;
using Vortice.Direct3D11;
using Vortice.DXGI;

namespace OverlaySpike.Gpu;

/// D3D11 device + the one shared texture the overlay reads. Throwaway spike code.
sealed class D3D : IDisposable
{
    public ID3D11Device Device;
    public ID3D11DeviceContext Context;
    public ID3D11Texture2D Shared;      // MiscFlags.Shared - what SetOverlayTexture gets
    public ID3D11Texture2D Staging;     // CPU-write path
    public int Width, Height;
    public IntPtr LegacyShareHandle;
    public string AdapterName;
    public int AdapterIndex = -1;

    public static D3D Create(int w, int h, bool ownDevice = true, IntPtr existingDevice = default)
    {
        var d = new D3D { Width = w, Height = h };
        if (ownDevice)
        {
            var flags = DeviceCreationFlags.BgraSupport;
            D3D11.D3D11CreateDevice(null, DriverType.Hardware, flags,
                new[] { FeatureLevel.Level_11_1, FeatureLevel.Level_11_0 },
                out d.Device, out _, out d.Context).CheckError();
        }
        else
        {
            d.Device = new ID3D11Device(existingDevice);
            d.Device.AddRef();
            d.Context = d.Device.ImmediateContext;
        }

        // SteamVR's compositor is another process: the texture must be shareable.
        d.Shared = d.Device.CreateTexture2D(new Texture2DDescription
        {
            Width = (uint)w, Height = (uint)h, MipLevels = 1, ArraySize = 1,
            Format = Format.B8G8R8A8_UNorm,
            SampleDescription = new SampleDescription(1, 0),
            Usage = ResourceUsage.Default,
            BindFlags = BindFlags.ShaderResource | BindFlags.RenderTarget,
            CPUAccessFlags = CpuAccessFlags.None,
            MiscFlags = ResourceOptionFlags.Shared,
        });

        using (var res = d.Shared.QueryInterface<IDXGIResource>())
            d.LegacyShareHandle = res.SharedHandle;

        // Which adapter did we actually land on? SteamVR's compositor runs on the
        // adapter driving the HMD; a texture on a different one is a real hazard.
        using (var dxgi = d.Device.QueryInterface<IDXGIDevice>())
        using (var adapter = dxgi.GetAdapter())
        {
            d.AdapterName = adapter.Description.Description;
            var luid = adapter.Description.Luid;
            using var factory = DXGI.CreateDXGIFactory1<IDXGIFactory1>();
            for (uint i = 0; factory.EnumAdapters1(i, out var a).Success; i++)
            {
                if (a.Description1.Luid == luid) d.AdapterIndex = (int)i;
                a.Dispose();
            }
        }

        d.Staging = d.Device.CreateTexture2D(new Texture2DDescription
        {
            Width = (uint)w, Height = (uint)h, MipLevels = 1, ArraySize = 1,
            Format = Format.B8G8R8A8_UNorm,
            SampleDescription = new SampleDescription(1, 0),
            Usage = ResourceUsage.Dynamic,
            BindFlags = BindFlags.ShaderResource,
            CPUAccessFlags = CpuAccessFlags.Write,
            MiscFlags = ResourceOptionFlags.None,
        });
        return d;
    }

    /// CPU pixels -> dynamic texture -> GPU copy into the shared texture.
    public unsafe void UploadBgra(IntPtr src, int srcStride)
    {
        var map = Context.Map(Staging, 0, MapMode.WriteDiscard);
        var dst = (byte*)map.DataPointer;
        var s = (byte*)src;
        int rowBytes = Width * 4;
        if (map.RowPitch == (uint)srcStride && srcStride == rowBytes)
        {
            Buffer.MemoryCopy(s, dst, (long)map.RowPitch * Height, (long)rowBytes * Height);
        }
        else
        {
            for (int y = 0; y < Height; y++)
                Buffer.MemoryCopy(s + (long)y * srcStride, dst + (long)y * map.RowPitch, map.RowPitch, rowBytes);
        }
        Context.Unmap(Staging, 0);
        Context.CopyResource(Shared, Staging);
    }

    public void MapStaging(out IntPtr ptr, out int rowPitch)
    {
        var map = Context.Map(Staging, 0, MapMode.WriteDiscard);
        ptr = map.DataPointer; rowPitch = (int)map.RowPitch;
    }

    public void UnmapAndCopy()
    {
        Context.Unmap(Staging, 0);
        Context.CopyResource(Shared, Staging);
    }

    public void Flush() => Context.Flush();

    public void Dispose()
    {
        Staging?.Dispose(); Shared?.Dispose(); Context?.Dispose(); Device?.Dispose();
    }
}
