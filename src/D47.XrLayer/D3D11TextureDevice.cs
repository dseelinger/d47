namespace D47.XrLayer;

/// <summary>Shared textures on an Elite session's Direct3D 11 device, through the COM tables.</summary>
internal sealed unsafe class D3D11TextureDevice : ITextureDevice
{
    // Slots from the Windows SDK's d3d11.h, d3d11_1.h and dxgi.h.
    private const int QueryInterfaceSlot = 0;
    private const int ReleaseSlot = 2;
    private const int DeviceGetImmediateContext = 40;
    private const int Device1OpenSharedResourceByName = 49;
    private const int ContextCopySubresourceRegion = 46;
    private const int ContextCopyResource = 47;
    private const int ContextFlush = 111;
    private const int Texture2DGetDesc = 10;
    private const int DxgiDeviceGetAdapter = 7;
    private const int DxgiAdapterGetDesc = 8;

    /// <summary>DXGI_SHARED_RESOURCE_READ.</summary>
    private const uint SharedResourceRead = 0x80000000;

    /// <summary>DXGI_SHARED_RESOURCE_WRITE.</summary>
    private const uint SharedResourceWrite = 0x1;

    /// <summary>The size of DXGI_ADAPTER_DESC and the offset of its AdapterLuid.</summary>
    private const int AdapterDescSize = 304;
    private const int AdapterDescLuidOffset = 296;

    private static readonly Guid Device1 = new("a04bfb29-08ef-43d6-a49c-a9bdbdcbe686");
    private static readonly Guid Texture2D = new("6f15aaf2-d208-4e89-9ab4-489535d34f9c");
    private static readonly Guid DxgiDevice = new("54ec77fa-1377-44e6-8c32-88fd5f44c84c");

    private nint device1;
    private nint context;

    private D3D11TextureDevice(nint device1, nint context, (uint, int) luid)
    {
        this.device1 = device1;
        this.context = context;
        AdapterLuid = luid;
    }

    public (uint Low, int High) AdapterLuid { get; }

    /// <summary>Takes references to the device's ID3D11Device1 and immediate context. Null when either is missing.</summary>
    public static D3D11TextureDevice? Open(nint device)
    {
        if (device == 0)
        {
            return null;
        }

        var device1 = QueryInterface(device, Device1);
        if (device1 == 0)
        {
            return null;
        }

        nint context = 0;
        ((delegate* unmanaged<nint, nint*, void>)Method(device1, DeviceGetImmediateContext))(device1, &context);
        if (context == 0)
        {
            ReleaseCom(device1);
            return null;
        }

        return new D3D11TextureDevice(device1, context, ReadAdapterLuid(device));
    }

    public nint OpenShared(char* name) => Open(name, SharedResourceRead);

    public nint OpenSharedWritable(char* name) => Open(name, SharedResourceRead | SharedResourceWrite);

    public TextureDescription Describe(nint texture)
    {
        TextureDescription description = default;
        ((delegate* unmanaged<nint, TextureDescription*, void>)Method(texture, Texture2DGetDesc))(texture, &description);
        return description;
    }

    public void Copy(nint source, nint destination) =>
        ((delegate* unmanaged<nint, nint, nint, void>)Method(context, ContextCopyResource))(context, destination, source);

    public void CopyRegion(nint source, uint subresource, XrRect2Di rect, nint destination)
    {
        var box = stackalloc uint[6];
        box[0] = (uint)rect.X;
        box[1] = (uint)rect.Y;
        box[2] = 0;
        box[3] = (uint)(rect.X + rect.Width);
        box[4] = (uint)(rect.Y + rect.Height);
        box[5] = 1;
        ((delegate* unmanaged<nint, nint, uint, uint, uint, uint, nint, uint, uint*, void>)Method(context, ContextCopySubresourceRegion))(
            context, destination, 0, 0, 0, 0, source, subresource, box);
    }

    public void Flush() => ((delegate* unmanaged<nint, void>)Method(context, ContextFlush))(context);

    public void Release(nint texture) => ReleaseCom(texture);

    public void Close()
    {
        if (context != 0)
        {
            ReleaseCom(context);
            context = 0;
        }

        if (device1 != 0)
        {
            ReleaseCom(device1);
            device1 = 0;
        }
    }

    private nint Open(char* name, uint access)
    {
        var iid = Texture2D;
        nint texture = 0;
        var result = ((delegate* unmanaged<nint, char*, uint, Guid*, nint*, int>)Method(device1, Device1OpenSharedResourceByName))(
            device1, name, access, &iid, &texture);
        return result < 0 ? 0 : texture;
    }

    private static (uint, int) ReadAdapterLuid(nint device)
    {
        var dxgiDevice = QueryInterface(device, DxgiDevice);
        if (dxgiDevice == 0)
        {
            return (0, 0);
        }

        try
        {
            nint adapter = 0;
            if (((delegate* unmanaged<nint, nint*, int>)Method(dxgiDevice, DxgiDeviceGetAdapter))(dxgiDevice, &adapter) < 0
                || adapter == 0)
            {
                return (0, 0);
            }

            try
            {
                var description = stackalloc byte[AdapterDescSize];
                if (((delegate* unmanaged<nint, byte*, int>)Method(adapter, DxgiAdapterGetDesc))(adapter, description) < 0)
                {
                    return (0, 0);
                }

                return (*(uint*)(description + AdapterDescLuidOffset), *(int*)(description + AdapterDescLuidOffset + 4));
            }
            finally
            {
                ReleaseCom(adapter);
            }
        }
        finally
        {
            ReleaseCom(dxgiDevice);
        }
    }

    private static nint QueryInterface(nint com, Guid iid)
    {
        nint result = 0;
        return ((delegate* unmanaged<nint, Guid*, nint*, int>)Method(com, QueryInterfaceSlot))(com, &iid, &result) < 0
            ? 0
            : result;
    }

    private static void ReleaseCom(nint com) => ((delegate* unmanaged<nint, uint>)Method(com, ReleaseSlot))(com);

    private static nint Method(nint com, int slot) => (*(nint**)com)[slot];
}
