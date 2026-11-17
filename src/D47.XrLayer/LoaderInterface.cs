using System.Runtime.InteropServices;

namespace D47.XrLayer;

// The OpenXR loader's layer interface, from openxr_loader_negotiation.h. Handles are 64-bit.

internal static class XrResult
{
    public const int Success = 0;
    public const int ErrorInitializationFailed = -6;
    public const int ErrorFunctionUnsupported = -7;
}

internal enum LoaderInterfaceStruct
{
    LoaderInfo = 1,
    ApiLayerRequest = 2,
    ApiLayerCreateInfo = 4,
    ApiLayerNextInfo = 5,
}

[StructLayout(LayoutKind.Sequential)]
internal struct NegotiateLoaderInfo
{
    public const uint Version = 1;

    public LoaderInterfaceStruct StructType;
    public uint StructVersion;
    public nuint StructSize;
    public uint MinInterfaceVersion;
    public uint MaxInterfaceVersion;
    public ulong MinApiVersion;
    public ulong MaxApiVersion;
}

[StructLayout(LayoutKind.Sequential)]
internal unsafe struct NegotiateApiLayerRequest
{
    public const uint Version = 1;

    public LoaderInterfaceStruct StructType;
    public uint StructVersion;
    public nuint StructSize;
    public uint LayerInterfaceVersion;
    public ulong LayerApiVersion;
    public delegate* unmanaged<ulong, byte*, nint*, int> GetInstanceProcAddr;
    public delegate* unmanaged<void*, ApiLayerCreateInfo*, ulong*, int> CreateApiLayerInstance;
}

[StructLayout(LayoutKind.Sequential)]
internal unsafe struct ApiLayerNextInfo
{
    public LoaderInterfaceStruct StructType;
    public uint StructVersion;
    public nuint StructSize;
    public fixed byte LayerName[256];
    public delegate* unmanaged<ulong, byte*, nint*, int> NextGetInstanceProcAddr;
    public delegate* unmanaged<void*, ApiLayerCreateInfo*, ulong*, int> NextCreateApiLayerInstance;
    public ApiLayerNextInfo* Next;
}

[StructLayout(LayoutKind.Sequential)]
internal unsafe struct ApiLayerCreateInfo
{
    public LoaderInterfaceStruct StructType;
    public uint StructVersion;
    public nuint StructSize;
    public void* LoaderInstance;
    public fixed byte SettingsFileLocation[512];
    public ApiLayerNextInfo* NextInfo;
}
