using System.Text;
using Valve.VR;

namespace D47.Vr.Binding;

/// <summary>
/// The OpenVR functions d47 calls, as a seam. The vendored binding's classes are built over a
/// native function table through an internal constructor and cannot be substituted, so every call
/// below the runtime goes through this instead.
/// </summary>
public interface IOpenVrSession
{
    /// <summary>Whether <c>openvr_api.dll</c> was found and the binding taught where it is.</summary>
    bool Load();

    /// <summary>Whether a SteamVR session already exists to attach to.</summary>
    bool SessionIsRunning();

    bool IsHmdPresent();

    /// <summary>Starts or attaches. Null when <paramref name="error"/> says why not.</summary>
    IOpenVrSystem? Init(ref EVRInitError error, EVRApplicationType kind);

    void Shutdown();

    string GetStringForHmdError(EVRInitError error);

    /// <summary>Null until <see cref="Init"/> has succeeded, and again after <see cref="Shutdown"/>.</summary>
    IOpenVrOverlay? Overlay { get; }

    IOpenVrInput? Input { get; }

    IOpenVrApplications? Applications { get; }

    IOpenVrRenderModels? RenderModels { get; }
}

/// <summary><c>IVRSystem</c>: poses, device properties and the session's own event queue.</summary>
public interface IOpenVrSystem
{
    /// <summary>Fills <paramref name="poses"/>; the count is its length.</summary>
    void GetDeviceToAbsoluteTrackingPose(
        ETrackingUniverseOrigin origin,
        float secondsAhead,
        TrackedDevicePose_t[] poses);

    ETrackedDeviceClass GetTrackedDeviceClass(uint device);

    EDeviceActivityLevel GetTrackedDeviceActivityLevel(uint device);

    uint GetStringTrackedDeviceProperty(
        uint device,
        ETrackedDeviceProperty property,
        StringBuilder text,
        uint size,
        ref ETrackedPropertyError error);

    bool PollNextEvent(ref VREvent_t next, uint size);

    void AcknowledgeQuit_Exiting();
}

/// <summary><c>IVROverlay</c>: one quad's handle, texture, transform and look.</summary>
public interface IOpenVrOverlay
{
    EVROverlayError CreateOverlay(string key, string name, ref ulong handle);

    EVROverlayError DestroyOverlay(ulong handle);

    EVROverlayError ShowOverlay(ulong handle);

    EVROverlayError HideOverlay(ulong handle);

    bool IsOverlayVisible(ulong handle);

    EVROverlayError SetOverlayRaw(ulong handle, IntPtr pixels, uint width, uint height, uint bytesPerPixel);

    EVROverlayError ClearOverlayTexture(ulong handle);

    EVROverlayError SetOverlayFlag(ulong handle, VROverlayFlags flag, bool enabled);

    EVROverlayError SetOverlaySortOrder(ulong handle, uint sortOrder);

    EVROverlayError SetOverlayAlpha(ulong handle, float alpha);

    EVROverlayError GetOverlayAlpha(ulong handle, ref float alpha);

    EVROverlayError SetOverlayWidthInMeters(ulong handle, float widthMetres);

    EVROverlayError GetOverlayWidthInMeters(ulong handle, ref float widthMetres);

    EVROverlayError SetOverlayCurvature(ulong handle, float curvature);

    EVROverlayError SetOverlayTransformAbsolute(
        ulong handle,
        ETrackingUniverseOrigin origin,
        ref HmdMatrix34_t transform);

    EVROverlayError GetOverlayTransformAbsolute(
        ulong handle,
        ref ETrackingUniverseOrigin origin,
        ref HmdMatrix34_t transform);

    EVROverlayError SetOverlayTransformTrackedDeviceRelative(
        ulong handle,
        uint device,
        ref HmdMatrix34_t transform);

    EVROverlayError GetOverlayTransformTrackedDeviceRelative(
        ulong handle,
        ref uint device,
        ref HmdMatrix34_t transform);

    EVROverlayError GetOverlayTransformType(ulong handle, ref VROverlayTransformType kind);

    bool PollNextOverlayEvent(ulong handle, ref VREvent_t next, uint size);
}

/// <summary><c>IVRInput</c>: the action manifest, its handles and the digital actions.</summary>
public interface IOpenVrInput
{
    EVRInputError SetActionManifestPath(string path);

    EVRInputError GetActionSetHandle(string name, ref ulong handle);

    EVRInputError GetActionHandle(string name, ref ulong handle);

    EVRInputError UpdateActionState(VRActiveActionSet_t[] sets, uint sizeOfOne);

    EVRInputError GetDigitalActionData(
        ulong action,
        ref InputDigitalActionData_t data,
        uint size,
        ulong restrictToDevice);
}

/// <summary><c>IVRApplications</c>: enough of it to say who this process is.</summary>
public interface IOpenVrApplications
{
    EVRApplicationError AddApplicationManifest(string path, bool temporary);

    EVRApplicationError IdentifyApplication(uint processId, string appKey);
}

/// <summary><c>IVRRenderModels</c>: enough of it to find a controller's tip.</summary>
public interface IOpenVrRenderModels
{
    bool RenderModelHasComponent(string model, string component);

    bool GetComponentState(
        string model,
        string component,
        ref VRControllerState_t buttons,
        ref RenderModel_ControllerMode_State_t mode,
        ref RenderModel_ComponentState_t state);
}
