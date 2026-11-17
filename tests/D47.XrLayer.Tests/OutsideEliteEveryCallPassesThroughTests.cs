using System.Runtime.InteropServices;
using D47.XrLayer;
using Xunit;

namespace D47.XrLayer.Tests;

/// <summary>
/// The test host is not Elite, so the layer here must negotiate and then hand every call to the next
/// layer untouched. A layer that declines would fail <c>xrCreateInstance</c> in the loader.
/// </summary>
public unsafe class OutsideEliteEveryCallPassesThroughTests
{
    private const nint NextFunction = 0x5EED;

    private static ApiLayerNextInfo* forwardedNextInfo;
    private static int nextCreateCalls;

    [Fact]
    public void TheLoaderInterfaceStructsHaveTheirNativeSizes()
    {
        Assert.Equal(40, sizeof(NegotiateLoaderInfo));
        Assert.Equal(48, sizeof(NegotiateApiLayerRequest));
        Assert.Equal(296, sizeof(ApiLayerNextInfo));
        Assert.Equal(544, sizeof(ApiLayerCreateInfo));
    }

    [Fact]
    public void NegotiationSucceedsAndCallsGoToTheNextLayer()
    {
        var request = Negotiate(minInterface: 1, maxInterface: 1, out var result);

        Assert.Equal(XrResult.Success, result);
        Assert.Equal(1u, request.LayerInterfaceVersion);
        Assert.True(request.GetInstanceProcAddr is not null);
        Assert.True(request.CreateApiLayerInstance is not null);

        nint before = 1;
        Assert.Equal(XrResult.ErrorFunctionUnsupported, GetProc(request, "xrInitializeLoaderKHR", &before));
        Assert.Equal(0, before);

        var second = new ApiLayerNextInfo { StructType = LoaderInterfaceStruct.ApiLayerNextInfo };
        var first = new ApiLayerNextInfo
        {
            StructType = LoaderInterfaceStruct.ApiLayerNextInfo,
            StructVersion = 1,
            StructSize = (nuint)sizeof(ApiLayerNextInfo),
            NextGetInstanceProcAddr = &FakeNextGetInstanceProcAddr,
            NextCreateApiLayerInstance = &FakeNextCreateApiLayerInstance,
            Next = &second,
        };
        var createInfo = new ApiLayerCreateInfo
        {
            StructType = LoaderInterfaceStruct.ApiLayerCreateInfo,
            StructVersion = 1,
            StructSize = (nuint)sizeof(ApiLayerCreateInfo),
            NextInfo = &first,
        };
        ulong instance = 0;
        nextCreateCalls = 0;

        Assert.Equal(XrResult.Success, request.CreateApiLayerInstance(null, &createInfo, &instance));
        Assert.Equal(1, nextCreateCalls);
        Assert.True(forwardedNextInfo == &second);
        Assert.Equal(42ul, instance);

        foreach (var name in new[]
        {
            "xrEndFrame", "xrCreateSession", "xrDestroySession", "xrGetInstanceProcAddr", "xrPollEvent",
            "xrSuggestInteractionProfileBindings", "xrAttachSessionActionSets", "xrSyncActions",
        })
        {
            nint function = 0;
            Assert.Equal(XrResult.Success, GetProc(request, name, &function));
            Assert.Equal(NextFunction, function);
        }
    }

    [Fact]
    public void ALoaderSpeakingAnotherInterfaceIsRefused()
    {
        Negotiate(minInterface: 2, maxInterface: 2, out var result);

        Assert.Equal(XrResult.ErrorInitializationFailed, result);
    }

    private static NegotiateApiLayerRequest Negotiate(uint minInterface, uint maxInterface, out int result)
    {
        var loaderInfo = new NegotiateLoaderInfo
        {
            StructType = LoaderInterfaceStruct.LoaderInfo,
            StructVersion = 1,
            StructSize = (nuint)sizeof(NegotiateLoaderInfo),
            MinInterfaceVersion = minInterface,
            MaxInterfaceVersion = maxInterface,
            MinApiVersion = 1UL << 48,
            MaxApiVersion = (1UL << 48) | (0x3FFUL << 32) | 0xFFF,
        };
        var request = new NegotiateApiLayerRequest
        {
            StructType = LoaderInterfaceStruct.ApiLayerRequest,
            StructVersion = 1,
            StructSize = (nuint)sizeof(NegotiateApiLayerRequest),
        };
        var name = "XR_APILAYER_D47_panel\0"u8;
        fixed (byte* layerName = name)
        {
            var negotiate = (delegate* unmanaged<NegotiateLoaderInfo*, byte*, NegotiateApiLayerRequest*, int>)
                &Layer.Negotiate;
            result = negotiate(&loaderInfo, layerName, &request);
        }

        return request;
    }

    private static int GetProc(NegotiateApiLayerRequest request, string name, nint* function)
    {
        var bytes = System.Text.Encoding.ASCII.GetBytes(name + "\0");
        fixed (byte* text = bytes)
        {
            return request.GetInstanceProcAddr(42, text, function);
        }
    }

    [UnmanagedCallersOnly]
    private static int FakeNextGetInstanceProcAddr(ulong instance, byte* name, nint* function)
    {
        *function = NextFunction;
        return XrResult.Success;
    }

    [UnmanagedCallersOnly]
    private static int FakeNextCreateApiLayerInstance(void* info, ApiLayerCreateInfo* layerInfo, ulong* instance)
    {
        nextCreateCalls++;
        forwardedNextInfo = layerInfo->NextInfo;
        *instance = 42;
        return XrResult.Success;
    }
}
