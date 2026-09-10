using System.Text;
using Valve.VR;

namespace D47.Vr.Binding;

/// <summary>The seam over the vendored binding. Forwards and does nothing else.</summary>
public sealed class OpenVrBinding : IOpenVrSession
{
    /// <summary>The vendored binding is static, so one adapter over it is enough.</summary>
    public static OpenVrBinding Instance { get; } = new();

    private readonly SystemApi _system = new();
    private readonly OverlayApi _overlay = new();
    private readonly InputApi _input = new();
    private readonly ApplicationsApi _applications = new();
    private readonly RenderModelsApi _renderModels = new();

    public bool Load() => OpenVrLoader.Register();

    public bool SessionIsRunning() => SteamVrRuntime.SteamVrIsRunning();

    public bool IsHmdPresent() => OpenVR.IsHmdPresent();

    public IOpenVrSystem? Init(ref EVRInitError error, EVRApplicationType kind) =>
        OpenVR.Init(ref error, kind) is null ? null : _system;

    public void Shutdown() => OpenVR.Shutdown();

    public string GetStringForHmdError(EVRInitError error) => OpenVR.GetStringForHmdError(error);

    public IOpenVrOverlay? Overlay => OpenVR.Overlay is null ? null : _overlay;

    public IOpenVrInput? Input => OpenVR.Input is null ? null : _input;

    public IOpenVrApplications? Applications => OpenVR.Applications is null ? null : _applications;

    public IOpenVrRenderModels? RenderModels => OpenVR.RenderModels is null ? null : _renderModels;

    private sealed class SystemApi : IOpenVrSystem
    {
        public void GetDeviceToAbsoluteTrackingPose(
            ETrackingUniverseOrigin origin,
            float secondsAhead,
            TrackedDevicePose_t[] poses) =>
            OpenVR.System.GetDeviceToAbsoluteTrackingPose(origin, secondsAhead, poses);

        public ETrackedDeviceClass GetTrackedDeviceClass(uint device) =>
            OpenVR.System.GetTrackedDeviceClass(device);

        public EDeviceActivityLevel GetTrackedDeviceActivityLevel(uint device) =>
            OpenVR.System.GetTrackedDeviceActivityLevel(device);

        public uint GetStringTrackedDeviceProperty(
            uint device,
            ETrackedDeviceProperty property,
            StringBuilder text,
            uint size,
            ref ETrackedPropertyError error) =>
            OpenVR.System.GetStringTrackedDeviceProperty(device, property, text, size, ref error);

        public bool PollNextEvent(ref VREvent_t next, uint size) =>
            OpenVR.System.PollNextEvent(ref next, size);

        public void AcknowledgeQuit_Exiting() => OpenVR.System.AcknowledgeQuit_Exiting();
    }

    private sealed class OverlayApi : IOpenVrOverlay
    {
        public EVROverlayError CreateOverlay(string key, string name, ref ulong handle) =>
            OpenVR.Overlay.CreateOverlay(key, name, ref handle);

        public EVROverlayError DestroyOverlay(ulong handle) => OpenVR.Overlay.DestroyOverlay(handle);

        public EVROverlayError ShowOverlay(ulong handle) => OpenVR.Overlay.ShowOverlay(handle);

        public EVROverlayError HideOverlay(ulong handle) => OpenVR.Overlay.HideOverlay(handle);

        public bool IsOverlayVisible(ulong handle) => OpenVR.Overlay.IsOverlayVisible(handle);

        public EVROverlayError SetOverlayRaw(
            ulong handle,
            IntPtr pixels,
            uint width,
            uint height,
            uint bytesPerPixel) =>
            OpenVR.Overlay.SetOverlayRaw(handle, pixels, width, height, bytesPerPixel);

        public EVROverlayError ClearOverlayTexture(ulong handle) =>
            OpenVR.Overlay.ClearOverlayTexture(handle);

        public EVROverlayError SetOverlayFlag(ulong handle, VROverlayFlags flag, bool enabled) =>
            OpenVR.Overlay.SetOverlayFlag(handle, flag, enabled);

        public EVROverlayError SetOverlaySortOrder(ulong handle, uint sortOrder) =>
            OpenVR.Overlay.SetOverlaySortOrder(handle, sortOrder);

        public EVROverlayError SetOverlayAlpha(ulong handle, float alpha) =>
            OpenVR.Overlay.SetOverlayAlpha(handle, alpha);

        public EVROverlayError GetOverlayAlpha(ulong handle, ref float alpha) =>
            OpenVR.Overlay.GetOverlayAlpha(handle, ref alpha);

        public EVROverlayError SetOverlayWidthInMeters(ulong handle, float widthMetres) =>
            OpenVR.Overlay.SetOverlayWidthInMeters(handle, widthMetres);

        public EVROverlayError GetOverlayWidthInMeters(ulong handle, ref float widthMetres) =>
            OpenVR.Overlay.GetOverlayWidthInMeters(handle, ref widthMetres);

        public EVROverlayError SetOverlayCurvature(ulong handle, float curvature) =>
            OpenVR.Overlay.SetOverlayCurvature(handle, curvature);

        public EVROverlayError SetOverlayTransformAbsolute(
            ulong handle,
            ETrackingUniverseOrigin origin,
            ref HmdMatrix34_t transform) =>
            OpenVR.Overlay.SetOverlayTransformAbsolute(handle, origin, ref transform);

        public EVROverlayError GetOverlayTransformAbsolute(
            ulong handle,
            ref ETrackingUniverseOrigin origin,
            ref HmdMatrix34_t transform) =>
            OpenVR.Overlay.GetOverlayTransformAbsolute(handle, ref origin, ref transform);

        public EVROverlayError SetOverlayTransformTrackedDeviceRelative(
            ulong handle,
            uint device,
            ref HmdMatrix34_t transform) =>
            OpenVR.Overlay.SetOverlayTransformTrackedDeviceRelative(handle, device, ref transform);

        public EVROverlayError GetOverlayTransformTrackedDeviceRelative(
            ulong handle,
            ref uint device,
            ref HmdMatrix34_t transform) =>
            OpenVR.Overlay.GetOverlayTransformTrackedDeviceRelative(handle, ref device, ref transform);

        public EVROverlayError GetOverlayTransformType(ulong handle, ref VROverlayTransformType kind) =>
            OpenVR.Overlay.GetOverlayTransformType(handle, ref kind);

        public bool PollNextOverlayEvent(ulong handle, ref VREvent_t next, uint size) =>
            OpenVR.Overlay.PollNextOverlayEvent(handle, ref next, size);
    }

    private sealed class InputApi : IOpenVrInput
    {
        public EVRInputError SetActionManifestPath(string path) =>
            OpenVR.Input.SetActionManifestPath(path);

        public EVRInputError GetActionSetHandle(string name, ref ulong handle) =>
            OpenVR.Input.GetActionSetHandle(name, ref handle);

        public EVRInputError GetActionHandle(string name, ref ulong handle) =>
            OpenVR.Input.GetActionHandle(name, ref handle);

        public EVRInputError UpdateActionState(VRActiveActionSet_t[] sets, uint sizeOfOne) =>
            OpenVR.Input.UpdateActionState(sets, sizeOfOne);

        public EVRInputError GetDigitalActionData(
            ulong action,
            ref InputDigitalActionData_t data,
            uint size,
            ulong restrictToDevice) =>
            OpenVR.Input.GetDigitalActionData(action, ref data, size, restrictToDevice);
    }

    private sealed class ApplicationsApi : IOpenVrApplications
    {
        public EVRApplicationError AddApplicationManifest(string path, bool temporary) =>
            OpenVR.Applications.AddApplicationManifest(path, temporary);

        public EVRApplicationError IdentifyApplication(uint processId, string appKey) =>
            OpenVR.Applications.IdentifyApplication(processId, appKey);
    }

    private sealed class RenderModelsApi : IOpenVrRenderModels
    {
        public bool RenderModelHasComponent(string model, string component) =>
            OpenVR.RenderModels.RenderModelHasComponent(model, component);

        public bool GetComponentState(
            string model,
            string component,
            ref VRControllerState_t buttons,
            ref RenderModel_ControllerMode_State_t mode,
            ref RenderModel_ComponentState_t state) =>
            OpenVR.RenderModels.GetComponentState(model, component, ref buttons, ref mode, ref state);
    }
}
