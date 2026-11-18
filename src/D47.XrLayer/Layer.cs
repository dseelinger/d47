using System.Runtime.InteropServices;

namespace D47.XrLayer;

/// <summary>
/// The layer's entry points. Each hook calls the next function whatever its own work throws: once, or, when the
/// runtime rejects an xrEndFrame carrying d47's quads or an xrAttachSessionActionSets or xrSyncActions carrying the
/// layer's action set, a second time with Elite's call as Elite made it.
/// </summary>
internal static unsafe class Layer
{
    private const uint LoaderInterfaceVersion = 1;
    private const ulong ApiVersion = 1UL << 48;

    private static bool inElite;
    private static bool cylinders;
    private static delegate* unmanaged<ulong, byte*, nint*, int> nextGetInstanceProcAddr;
    private static delegate* unmanaged<ulong, void*, ulong*, int> nextCreateSession;
    private static delegate* unmanaged<ulong, void*, int> nextEndFrame;
    private static delegate* unmanaged<ulong, int> nextDestroySession;
    private static delegate* unmanaged<ulong, XrBaseHeader*, int> nextPollEvent;
    private static delegate* unmanaged<ulong, XrInteractionProfileSuggestedBinding*, int> nextSuggestBindings;
    private static delegate* unmanaged<ulong, XrSessionActionSetsAttachInfo*, int> nextAttachActionSets;
    private static delegate* unmanaged<ulong, XrActionsSyncInfo*, int> nextSyncActions;
    private static readonly SuggestedBindings Suggested = new();
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
        var result = CylinderExtension.Create(inElite, next, (XrInstanceCreateInfo*)info, &forwarded, instance, out var enabled);
        cylinders = enabled;
        return result;
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
        else if (requested.SequenceEqual("xrPollEvent"u8))
        {
            nextPollEvent = (delegate* unmanaged<ulong, XrBaseHeader*, int>)*function;
            *function = (nint)(delegate* unmanaged<ulong, XrBaseHeader*, int>)&PollEvent;
        }
        else if (requested.SequenceEqual("xrSuggestInteractionProfileBindings"u8))
        {
            nextSuggestBindings = (delegate* unmanaged<ulong, XrInteractionProfileSuggestedBinding*, int>)*function;
            *function = (nint)(delegate* unmanaged<ulong, XrInteractionProfileSuggestedBinding*, int>)&SuggestBindings;
        }
        else if (requested.SequenceEqual("xrAttachSessionActionSets"u8))
        {
            nextAttachActionSets = (delegate* unmanaged<ulong, XrSessionActionSetsAttachInfo*, int>)*function;
            *function = (nint)(delegate* unmanaged<ulong, XrSessionActionSetsAttachInfo*, int>)&AttachActionSets;
        }
        else if (requested.SequenceEqual("xrSyncActions"u8))
        {
            nextSyncActions = (delegate* unmanaged<ulong, XrActionsSyncInfo*, int>)*function;
            *function = (nint)(delegate* unmanaged<ulong, XrActionsSyncInfo*, int>)&SyncActions;
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

                    current = EliteSession.Attach(calls, block, Suggested, instance, (XrSessionCreateInfo*)createInfo, *session, cylinders);
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
    private static int PollEvent(ulong instance, XrBaseHeader* eventData)
    {
        var result = nextPollEvent(instance, eventData);
        try
        {
            var attached = current;
            if (result == XrResult.Success && attached is not null && eventData is not null
                && eventData->Type == XrType.EventDataSessionStateChanged)
            {
                var changed = (XrEventDataSessionStateChanged*)eventData;
                if (changed->Session == attached.Handle)
                {
                    attached.StateChanged(changed->State);
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
    private static int SuggestBindings(ulong instance, XrInteractionProfileSuggestedBinding* suggestedBindings)
    {
        var result = nextSuggestBindings(instance, suggestedBindings);
        try
        {
            if (result >= XrResult.Success)
            {
                Suggested.Record(instance, suggestedBindings);
            }
        }
        catch (Exception)
        {
            // Never reaches the loader.
        }

        return result;
    }

    [UnmanagedCallersOnly]
    private static int AttachActionSets(ulong session, XrSessionActionSetsAttachInfo* attachInfo)
    {
        var attached = current;
        return attached is not null && attached.Handle == session
            ? attached.AttachActionSets(attachInfo)
            : nextAttachActionSets(session, attachInfo);
    }

    [UnmanagedCallersOnly]
    private static int SyncActions(ulong session, XrActionsSyncInfo* syncInfo)
    {
        var attached = current;
        return attached is not null && attached.Handle == session
            ? attached.SyncActions(syncInfo)
            : nextSyncActions(session, syncInfo);
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
