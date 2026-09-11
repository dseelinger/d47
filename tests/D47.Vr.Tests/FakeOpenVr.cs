using System.Numerics;
using System.Text;
using D47.Vr.Binding;
using Valve.VR;

namespace D47.Vr.Tests;

/// <summary>One call the runtime made, with the handle or device it named.</summary>
public readonly record struct VrCall(string Function, ulong Subject);

/// <summary>
/// A SteamVR that answers without a headset. Every call is recorded in <see cref="Calls"/>; the
/// answers are whatever the test set before it ran.
/// </summary>
public sealed class FakeOpenVr :
    IOpenVrSession,
    IOpenVrSystem,
    IOpenVrOverlay,
    IOpenVrInput,
    IOpenVrApplications,
    IOpenVrRenderModels
{
    private ulong _nextHandle = 1;
    private bool _started;

    public List<VrCall> Calls { get; } = [];

    /// <summary>Whether <c>openvr_api.dll</c> is on this imagined machine.</summary>
    public bool Installed { get; set; } = true;

    /// <summary>Whether there is a session to attach to.</summary>
    public bool Running { get; set; } = true;

    public bool HeadsetPresent { get; set; } = true;

    /// <summary>What <c>VR_Init</c> reports. Anything but None gives no session back.</summary>
    public EVRInitError InitError { get; set; } = EVRInitError.None;

    /// <summary>Overlay keys another process already owns.</summary>
    public HashSet<string> KeysTaken { get; } = new(StringComparer.Ordinal);

    /// <summary>The quads that exist right now, by handle.</summary>
    public Dictionary<ulong, string> Live { get; } = [];

    /// <summary>Every key that was ever created, in order.</summary>
    public List<string> Created { get; } = [];

    /// <summary>What each quad was last told to do about being on screen.</summary>
    public Dictionary<ulong, bool> Shown { get; } = [];

    /// <summary>Events waiting on each quad, drained by <c>PollNextOverlayEvent</c>.</summary>
    public Dictionary<ulong, Queue<VREvent_t>> OverlayEvents { get; } = [];

    /// <summary>Events waiting on the session, drained by <c>PollNextEvent</c>.</summary>
    public Queue<VREvent_t> SessionEvents { get; } = [];

    /// <summary>The poses the tracking universe reports, by device index.</summary>
    public Dictionary<uint, TrackedDevicePose_t> Poses { get; } = [];

    /// <summary>What each device is.</summary>
    public Dictionary<uint, ETrackedDeviceClass> Classes { get; } = [];

    /// <summary>The string properties every device answers with.</summary>
    public Dictionary<ETrackedDeviceProperty, string> Properties { get; } = new()
    {
        [ETrackedDeviceProperty.Prop_RenderModelName_String] = "fake_controller",
        [ETrackedDeviceProperty.Prop_SerialNumber_String] = "FAKE-1",
    };

    /// <summary>The components the render model has.</summary>
    public HashSet<string> Components { get; } = new(StringComparer.Ordinal) { OpenVR.k_pch_Controller_Component_Tip };

    /// <summary>Where the tip sits relative to the grip.</summary>
    public Matrix4x4 GripToTip { get; set; } = Matrix4x4.CreateTranslation(0f, 0f, -0.05f);

    /// <summary>How many times a function was called.</summary>
    public int Count(string function) => Calls.Count(call => call.Function == function);

    /// <summary>How many times a function was called about one handle or device.</summary>
    public int Count(string function, ulong subject) =>
        Calls.Count(call => call.Function == function && call.Subject == subject);

    /// <summary>A tracked pose that is actually being reported.</summary>
    public static TrackedDevicePose_t Tracked(Matrix4x4 where) => new()
    {
        mDeviceToAbsoluteTracking = VrMatrix.ToOpenVr(where),
        bPoseIsValid = true,
        bDeviceIsConnected = true,
    };

    /// <summary>
    /// Called on entry to every call, before it is recorded, so a test can hold one thread inside the
    /// runtime while another tears the session down. Read <see cref="Calls"/> only once both are done.
    /// </summary>
    public Action<string>? Entered { get; set; }

    private readonly Lock _recording = new();

    private void Record(string function, ulong subject = 0)
    {
        // Outside the lock, so a test that blocks here does not also block the other thread's recording.
        Entered?.Invoke(function);

        lock (_recording)
        {
            Calls.Add(new VrCall(function, subject));
        }
    }

    public bool Load()
    {
        Record(nameof(Load));
        return Installed;
    }

    public bool SessionIsRunning()
    {
        Record(nameof(SessionIsRunning));
        return Running;
    }

    public bool IsHmdPresent()
    {
        Record(nameof(IsHmdPresent));
        return HeadsetPresent;
    }

    public IOpenVrSystem? Init(ref EVRInitError error, EVRApplicationType kind)
    {
        Record(nameof(Init));
        error = InitError;

        if (InitError != EVRInitError.None)
        {
            return null;
        }

        _started = true;
        return this;
    }

    public void Shutdown()
    {
        Record(nameof(Shutdown));
        _started = false;
    }

    public string GetStringForHmdError(EVRInitError error) => error.ToString();

    public IOpenVrOverlay? Overlay => _started ? this : null;

    public IOpenVrInput? Input => _started ? this : null;

    public IOpenVrApplications? Applications => _started ? this : null;

    public IOpenVrRenderModels? RenderModels => _started ? this : null;

    public void GetDeviceToAbsoluteTrackingPose(
        ETrackingUniverseOrigin origin,
        float secondsAhead,
        TrackedDevicePose_t[] poses)
    {
        Record(nameof(GetDeviceToAbsoluteTrackingPose), (ulong)poses.Length);

        for (var device = 0u; device < poses.Length; device++)
        {
            poses[device] = Poses.TryGetValue(device, out var pose) ? pose : default;
        }
    }

    public ETrackedDeviceClass GetTrackedDeviceClass(uint device)
    {
        Record(nameof(GetTrackedDeviceClass), device);
        return Classes.TryGetValue(device, out var kind) ? kind : ETrackedDeviceClass.Invalid;
    }

    public EDeviceActivityLevel GetTrackedDeviceActivityLevel(uint device)
    {
        Record(nameof(GetTrackedDeviceActivityLevel), device);
        return EDeviceActivityLevel.k_EDeviceActivityLevel_UserInteraction;
    }

    public uint GetStringTrackedDeviceProperty(
        uint device,
        ETrackedDeviceProperty property,
        StringBuilder text,
        uint size,
        ref ETrackedPropertyError error)
    {
        Record(nameof(GetStringTrackedDeviceProperty), device);

        if (!Properties.TryGetValue(property, out var value))
        {
            error = ETrackedPropertyError.TrackedProp_UnknownProperty;
            return 0;
        }

        error = ETrackedPropertyError.TrackedProp_Success;
        text.Append(value);
        return (uint)value.Length + 1;
    }

    public bool PollNextEvent(ref VREvent_t next, uint size)
    {
        Record(nameof(PollNextEvent));

        if (SessionEvents.Count == 0)
        {
            return false;
        }

        next = SessionEvents.Dequeue();
        return true;
    }

    public void AcknowledgeQuit_Exiting() => Record(nameof(AcknowledgeQuit_Exiting));

    public EVROverlayError CreateOverlay(string key, string name, ref ulong handle)
    {
        Record(nameof(CreateOverlay));

        if (KeysTaken.Contains(key))
        {
            return EVROverlayError.KeyInUse;
        }

        handle = _nextHandle++;
        Live[handle] = key;
        Created.Add(key);
        return EVROverlayError.None;
    }

    public EVROverlayError DestroyOverlay(ulong handle)
    {
        Record(nameof(DestroyOverlay), handle);
        Live.Remove(handle);
        return EVROverlayError.None;
    }

    public EVROverlayError ShowOverlay(ulong handle)
    {
        Record(nameof(ShowOverlay), handle);
        Shown[handle] = true;
        return EVROverlayError.None;
    }

    public EVROverlayError HideOverlay(ulong handle)
    {
        Record(nameof(HideOverlay), handle);
        Shown[handle] = false;
        return EVROverlayError.None;
    }

    public bool IsOverlayVisible(ulong handle)
    {
        Record(nameof(IsOverlayVisible), handle);
        return Shown.TryGetValue(handle, out var shown) && shown;
    }

    /// <summary>Whether the compositor turns pixels down, as it does under load.</summary>
    public bool RawRefused { get; set; }

    public EVROverlayError SetOverlayRaw(ulong handle, IntPtr pixels, uint width, uint height, uint bytesPerPixel)
    {
        Record(nameof(SetOverlayRaw), handle);
        return RawRefused ? EVROverlayError.RequestFailed : EVROverlayError.None;
    }

    public EVROverlayError ClearOverlayTexture(ulong handle)
    {
        Record(nameof(ClearOverlayTexture), handle);
        return EVROverlayError.None;
    }

    public EVROverlayError SetOverlayFlag(ulong handle, VROverlayFlags flag, bool enabled)
    {
        Record(nameof(SetOverlayFlag), handle);
        return EVROverlayError.None;
    }

    public EVROverlayError SetOverlaySortOrder(ulong handle, uint sortOrder)
    {
        Record(nameof(SetOverlaySortOrder), handle);
        return EVROverlayError.None;
    }

    public EVROverlayError SetOverlayAlpha(ulong handle, float alpha)
    {
        Record(nameof(SetOverlayAlpha), handle);
        Alpha[handle] = alpha;
        return EVROverlayError.None;
    }

    public EVROverlayError GetOverlayAlpha(ulong handle, ref float alpha)
    {
        Record(nameof(GetOverlayAlpha), handle);
        alpha = Alpha.TryGetValue(handle, out var held) ? held : 0f;
        return EVROverlayError.None;
    }

    public EVROverlayError SetOverlayWidthInMeters(ulong handle, float widthMetres)
    {
        Record(nameof(SetOverlayWidthInMeters), handle);
        Width[handle] = widthMetres;
        return EVROverlayError.None;
    }

    public EVROverlayError GetOverlayWidthInMeters(ulong handle, ref float widthMetres)
    {
        Record(nameof(GetOverlayWidthInMeters), handle);
        widthMetres = Width.TryGetValue(handle, out var held) ? held : 0f;
        return EVROverlayError.None;
    }

    public EVROverlayError SetOverlayCurvature(ulong handle, float curvature)
    {
        Record(nameof(SetOverlayCurvature), handle);
        Curvature[handle] = curvature;
        return EVROverlayError.None;
    }

    /// <summary>What each quad was last told about its opacity, width and curvature.</summary>
    public Dictionary<ulong, float> Alpha { get; } = [];

    public Dictionary<ulong, float> Width { get; } = [];

    public Dictionary<ulong, float> Curvature { get; } = [];

    /// <summary>What each quad was last told about where it goes.</summary>
    public Dictionary<ulong, HmdMatrix34_t> Transform { get; } = [];

    public Dictionary<ulong, VROverlayTransformType> TransformKind { get; } = [];

    public EVROverlayError SetOverlayTransformAbsolute(
        ulong handle,
        ETrackingUniverseOrigin origin,
        ref HmdMatrix34_t transform)
    {
        Record(nameof(SetOverlayTransformAbsolute), handle);
        Transform[handle] = transform;
        TransformKind[handle] = VROverlayTransformType.VROverlayTransform_Absolute;
        return EVROverlayError.None;
    }

    public EVROverlayError GetOverlayTransformAbsolute(
        ulong handle,
        ref ETrackingUniverseOrigin origin,
        ref HmdMatrix34_t transform)
    {
        Record(nameof(GetOverlayTransformAbsolute), handle);
        transform = Transform.TryGetValue(handle, out var held) ? held : default;
        return EVROverlayError.None;
    }

    public EVROverlayError SetOverlayTransformTrackedDeviceRelative(
        ulong handle,
        uint device,
        ref HmdMatrix34_t transform)
    {
        Record(nameof(SetOverlayTransformTrackedDeviceRelative), handle);
        Transform[handle] = transform;
        TransformKind[handle] = VROverlayTransformType.VROverlayTransform_TrackedDeviceRelative;
        return EVROverlayError.None;
    }

    public EVROverlayError GetOverlayTransformTrackedDeviceRelative(
        ulong handle,
        ref uint device,
        ref HmdMatrix34_t transform)
    {
        Record(nameof(GetOverlayTransformTrackedDeviceRelative), handle);
        device = OpenVR.k_unTrackedDeviceIndex_Hmd;
        transform = Transform.TryGetValue(handle, out var held) ? held : default;
        return EVROverlayError.None;
    }

    public EVROverlayError GetOverlayTransformType(ulong handle, ref VROverlayTransformType kind)
    {
        Record(nameof(GetOverlayTransformType), handle);
        kind = TransformKind.TryGetValue(handle, out var held)
            ? held
            : VROverlayTransformType.VROverlayTransform_Absolute;
        return EVROverlayError.None;
    }

    public bool PollNextOverlayEvent(ulong handle, ref VREvent_t next, uint size)
    {
        Record(nameof(PollNextOverlayEvent), handle);

        if (!OverlayEvents.TryGetValue(handle, out var waiting) || waiting.Count == 0)
        {
            return false;
        }

        next = waiting.Dequeue();
        return true;
    }

    /// <summary>Puts one event on a quad's queue.</summary>
    public void Queue(ulong handle, EVREventType kind)
    {
        if (!OverlayEvents.TryGetValue(handle, out var waiting))
        {
            waiting = new Queue<VREvent_t>();
            OverlayEvents[handle] = waiting;
        }

        waiting.Enqueue(new VREvent_t { eventType = (uint)kind });
    }

    public EVRInputError SetActionManifestPath(string path)
    {
        Record(nameof(SetActionManifestPath));
        return EVRInputError.None;
    }

    public EVRInputError GetActionSetHandle(string name, ref ulong handle)
    {
        Record(nameof(GetActionSetHandle));
        handle = _nextHandle++;
        return EVRInputError.None;
    }

    public EVRInputError GetActionHandle(string name, ref ulong handle)
    {
        Record(nameof(GetActionHandle));
        handle = _nextHandle++;
        return EVRInputError.None;
    }

    public EVRInputError UpdateActionState(VRActiveActionSet_t[] sets, uint sizeOfOne)
    {
        Record(nameof(UpdateActionState), sets.Length == 0 ? 0 : (ulong)sets[0].nPriority);
        return EVRInputError.None;
    }

    /// <summary>Whether a digital action reads as held.</summary>
    public bool ActionHeld { get; set; }

    public EVRInputError GetDigitalActionData(
        ulong action,
        ref InputDigitalActionData_t data,
        uint size,
        ulong restrictToDevice)
    {
        Record(nameof(GetDigitalActionData), action);
        data = new InputDigitalActionData_t { bActive = true, bState = ActionHeld };
        return EVRInputError.None;
    }

    public EVRApplicationError AddApplicationManifest(string path, bool temporary)
    {
        Record(nameof(AddApplicationManifest));
        return EVRApplicationError.None;
    }

    public EVRApplicationError IdentifyApplication(uint processId, string appKey)
    {
        Record(nameof(IdentifyApplication));
        return EVRApplicationError.None;
    }

    public bool RenderModelHasComponent(string model, string component)
    {
        Record(nameof(RenderModelHasComponent));
        return Components.Contains(component);
    }

    public bool GetComponentState(
        string model,
        string component,
        ref VRControllerState_t buttons,
        ref RenderModel_ControllerMode_State_t mode,
        ref RenderModel_ComponentState_t state)
    {
        Record(nameof(GetComponentState));
        state.mTrackingToComponentLocal = VrMatrix.ToOpenVr(GripToTip);
        return true;
    }
}
