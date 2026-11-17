using System.Runtime.InteropServices;

namespace D47.Vr;

/// <summary>Direct3D 11 device creation on a DXGI adapter, the COM table accessor and the texture description.</summary>
public static class D3D11Native
{
    /// <summary>Makes a device and its immediate context on a DXGI adapter. Throws when a DXGI or Direct3D call fails.</summary>
    public static void CreateDevice(int adapter, out nint device, out nint context)
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
                    out device,
                    out _,
                    out context));
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
    }

    /// <summary>The index of the DXGI adapter with this LUID, or null when none has it. Throws when DXGI cannot be asked.</summary>
    public static int? AdapterWithLuid(uint luidLow, int luidHigh)
    {
        var iid = DxgiFactory1;
        Marshal.ThrowExceptionForHR(CreateDXGIFactory1(in iid, out var factory));

        try
        {
            var description = new byte[AdapterDescSize];

            for (uint index = 0; ; index++)
            {
                var listed = Slot<EnumAdapters1Fn>(factory, FactoryEnumAdapters1)(factory, index, out var adapter);

                if (listed == DxgiErrorNotFound)
                {
                    return null;
                }

                Marshal.ThrowExceptionForHR(listed);

                try
                {
                    Marshal.ThrowExceptionForHR(Slot<GetDescFn>(adapter, AdapterGetDesc)(adapter, description));
                }
                finally
                {
                    Marshal.Release(adapter);
                }

                if (BitConverter.ToUInt32(description, AdapterDescLuidOffset) == luidLow
                    && BitConverter.ToInt32(description, AdapterDescLuidOffset + 4) == luidHigh)
                {
                    return (int)index;
                }
            }
        }
        finally
        {
            Marshal.Release(factory);
        }
    }

    /// <summary>The method at <paramref name="slot"/> in a COM object's table.</summary>
    public static T Slot<T>(nint com, int slot)
        where T : Delegate =>
        Marshal.GetDelegateForFunctionPointer<T>(Marshal.ReadIntPtr(Marshal.ReadIntPtr(com), slot * IntPtr.Size));

    [StructLayout(LayoutKind.Sequential)]
    public record struct TextureDesc(
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

    private const int FactoryEnumAdapters1 = 12;

    private delegate int GetDescFn(nint adapter, [Out] byte[] description);

    private const int AdapterGetDesc = 8;

    /// <summary>The size of DXGI_ADAPTER_DESC and the offset of its AdapterLuid.</summary>
    private const int AdapterDescSize = 304;
    private const int AdapterDescLuidOffset = 296;

    private const int DxgiErrorNotFound = unchecked((int)0x887A0002);

    /// <summary>Required with an explicit adapter.</summary>
    private const uint DriverTypeUnknown = 0;

    private const uint SdkVersion = 7;

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
