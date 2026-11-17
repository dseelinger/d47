using System.Runtime.InteropServices;

namespace D47.XrLayer;

/// <summary>
/// The layer's entry points. Each hook calls the next function whatever its own work throws: once, or for an
/// xrEndFrame the runtime rejected with d47's quads, a second time without them.
/// </summary>
internal static unsafe class Layer
{
    private const uint LoaderInterfaceVersion = 1;
    private const ulong ApiVersion = 1UL << 48;

    private static bool inElite;
    private static delegate* unmanaged<ulong, byte*, nint*, int> nextGetInstanceProcAddr;
    private static delegate* unmanaged<ulong, void*, ulong*, int> nextCreateSession;
    private static delegate* unmanaged<ulong, void*, int> nextEndFrame;
    private static delegate* unmanaged<ulong, int> nextDestroySession;
    private static bool blockMapped;
    private static XrBlock* block;
    private static EliteSession? current;

    [UnmanagedCallersOnly(EntryPoint = "xrNegotiateLoaderApiLayerInterface")]
    public static int Negotiate(NegotiateLoaderInfo* loaderInfo, byte* layerName, NegotiateApiLayerRequest* request)
    {
        if (loaderInfo is null || request is null
            || loaderInfo->StructType != LoaderInterfaceStruct.LoaderInfo
            || loaderInfo->StructVersion != NegotiateLoaderInfo.Version
            || loaderInfo->StructSize != (nuint)sizeof(NegotiateLoaderInfo)
            || request->StructType != LoaderInterfaceStruct.ApiLayerRequest
            || request->StructVersion != NegotiateApiLayerRequest.Version
            || request->StructSize != (nuint)sizeof(NegotiateApiLayerRequest)
            || loaderInfo->MinInterfaceVersion > LoaderInterfaceVersion
            || loaderInfo->MaxInterfaceVersion < LoaderInterfaceVersion)
        {
            return XrResult.ErrorInitializationFailed;
        }

        try
        {
            inElite = LayerScope.AppliesTo(Environment.ProcessPath);
        }
        catch (Exception)
        {
            inElite = false;
        }

        request->LayerInterfaceVersion = LoaderInterfaceVersion;
        request->LayerApiVersion = ApiVersion;
        request->GetInstanceProcAddr = &GetInstanceProcAddr;
        request->CreateApiLayerInstance = &CreateApiLayerInstance;
        return XrResult.Success;
    }

    [UnmanagedCallersOnly]
    private static int CreateApiLayerInstance(void* info, ApiLayerCreateInfo* layerInfo, ulong* instance)
    {
        if (layerInfo is null || layerInfo->NextInfo is null
            || layerInfo->NextInfo->NextGetInstanceProcAddr is null
            || layerInfo->NextInfo->NextCreateApiLayerInstance is null)
        {
            return XrResult.ErrorInitializationFailed;
        }

        var next = layerInfo->NextInfo;
        var forwarded = *layerInfo;
        forwarded.NextInfo = next->Next;
        nextGetInstanceProcAddr = next->NextGetInstanceProcAddr;
        return next->NextCreateApiLayerInstance(info, &forwarded, instance);
    }

    [UnmanagedCallersOnly]
    private static int GetInstanceProcAddr(ulong instance, byte* name, nint* function)
    {
        var next = nextGetInstanceProcAddr;
        if (next is null)
        {
            if (function is not null)
            {
                *function = 0;
            }

            return XrResult.ErrorFunctionUnsupported;
        }

        var result = next(instance, name, function);
        if (!inElite || result < XrResult.Success || name is null || function is null || *function == 0)
        {
            return result;
        }

        var requested = MemoryMarshal.CreateReadOnlySpanFromNullTerminated(name);
        if (requested.SequenceEqual("xrGetInstanceProcAddr"u8))
        {
            *function = (nint)(delegate* unmanaged<ulong, byte*, nint*, int>)&GetInstanceProcAddr;
        }
        else if (requested.SequenceEqual("xrCreateSession"u8))
        {
            nextCreateSession = (delegate* unmanaged<ulong, void*, ulong*, int>)*function;
            *function = (nint)(delegate* unmanaged<ulong, void*, ulong*, int>)&CreateSession;
        }
        else if (requested.SequenceEqual("xrEndFrame"u8))
        {
            nextEndFrame = (delegate* unmanaged<ulong, void*, int>)*function;
            *function = (nint)(delegate* unmanaged<ulong, void*, int>)&EndFrame;
        }
        else if (requested.SequenceEqual("xrDestroySession"u8))
        {
            nextDestroySession = (delegate* unmanaged<ulong, int>)*function;
            *function = (nint)(delegate* unmanaged<ulong, int>)&DestroySession;
        }

        return result;
    }

    [UnmanagedCallersOnly]
    private static int CreateSession(ulong instance, void* createInfo, ulong* session)
    {
        var result = nextCreateSession(instance, createInfo, session);
        try
        {
            if (result >= XrResult.Success && session is not null)
            {
                current?.Destroy();
                current = null;
                var calls = NativeLayerCalls.Resolve(nextGetInstanceProcAddr, instance);
                if (calls is not null)
                {
                    if (!blockMapped)
                    {
                        blockMapped = true;
                        block = SharedBlock.Map();
                    }

                    current = EliteSession.Attach(calls, block, instance, (XrSessionCreateInfo*)createInfo, *session);
                }
            }
        }
        catch (Exception)
        {
            // Never reaches the loader.
        }

        return result;
    }

    [UnmanagedCallersOnly]
    private static int EndFrame(ulong session, void* frameEndInfo)
    {
        var attached = current;
        return attached is not null && attached.Handle == session
            ? attached.EndFrame((XrFrameEndInfo*)frameEndInfo)
            : nextEndFrame(session, frameEndInfo);
    }

    [UnmanagedCallersOnly]
    private static int DestroySession(ulong session)
    {
        try
        {
            var attached = current;
            if (attached is not null && attached.Handle == session)
            {
                current = null;
                attached.Destroy();
            }
        }
        catch (Exception)
        {
            // Never reaches the loader.
        }

        return nextDestroySession(session);
    }
}
