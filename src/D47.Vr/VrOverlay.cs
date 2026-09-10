using System.Runtime.InteropServices;
using D47.Core.Vr;
using D47.Vr.Binding;
using Valve.VR;

namespace D47.Vr;

/// <summary>One <c>IVROverlay</c> quad: its texture, its transform, its look, and its pointer.</summary>
public sealed class VrOverlay : IDisposable
{
    /// <summary>High, so the panel sits above ordinary overlays and above SteamVR's own furniture.</summary>
    private const uint SortOrder = 100;

    private readonly ulong _handle;
    private readonly Action<string>? _refused;
    private readonly IOpenVrSession _session;

    /// <summary>
    /// The interface this quad was created through. Held rather than asked for per call, and given back
    /// through <see cref="_session"/> so a session that has already gone is not called into.
    /// </summary>
    private readonly IOpenVrOverlay _api;

    private VrPose? _appliedPose;
    private VrPose? _appliedHeadOffset;
    private float _appliedWidth = float.NaN;
    private float _appliedCurvature = float.NaN;
    private float _appliedAlpha = float.NaN;
    private bool _shown;

    private VrOverlay(
        IOpenVrSession session,
        IOpenVrOverlay api,
        ulong handle,
        string key,
        Action<string>? refused)
    {
        _session = session;
        _api = api;
        _handle = handle;
        Key = key;
        _refused = refused;
    }

    /// <summary>Reports a call the runtime turned down, and answers whether it went through.</summary>
    private bool Went(EVROverlayError error, string what)
    {
        if (error == EVROverlayError.None)
        {
            return true;
        }

        _refused?.Invoke($"{what} on '{Key}' was refused: {error}");
        return false;
    }

    public string Key { get; }

    /// <summary>Creates the quad.</summary>
    public static VrOverlay? Create(
        IOpenVrSession session,
        string key,
        string name,
        out VrStart failure,
        Action<string>? refused = null)
    {
        if (session.Overlay is not { } api)
        {
            failure = new VrStart(VrStartOutcome.Failed, "SteamVR has no overlay interface.");
            return null;
        }

        ulong handle = 0;
        var created = api.CreateOverlay(key, name, ref handle);

        if (created == EVROverlayError.KeyInUse)
        {
            failure = new VrStart(
                VrStartOutcome.AlreadyOwned,
                "Another copy of D47 already owns the headset overlays. Close it.");
            return null;
        }

        if (created != EVROverlayError.None)
        {
            failure = new VrStart(VrStartOutcome.Failed, $"SteamVR would not create an overlay: {created}.");
            return null;
        }

        var overlay = new VrOverlay(session, api, handle, key, refused);

        // The handle exists from the moment CreateOverlay succeeded, so a failure after this point still has
        // a quad to give back.
        try
        {
            api.SetOverlaySortOrder(handle, SortOrder);

            // The rasteriser hands over premultiplied alpha, so the compositor is told to expect it.
            api.SetOverlayFlag(handle, VROverlayFlags.IsPremultiplied, true);

            // Off by default, and this is the reason: the flag sorts the quad with the *non-scene* overlays,
            // which is the class SteamVR's dashboard belongs to.
            if (Environment.GetEnvironmentVariable("D47_VR_SORT") == "nonscene")
            {
                api.SetOverlayFlag(handle, VROverlayFlags.SortWithNonSceneOverlays, true);
            }
        }
        catch
        {
            overlay.Dispose();
            throw;
        }

        failure = VrStart.Started;
        return overlay;
    }

    /// <summary>Puts pixels on the quad, as raw RGBA the runtime uploads itself.</summary>
    /// <returns>Whether the runtime took it.</returns>
    public bool Submit(IntPtr pixels, int width, int height) =>
        Went(
            _api.SetOverlayRaw(_handle, pixels, (uint)width, (uint)height, 4),
            "Setting the pixels");

    /// <summary>Whether SteamVR is drawing this quad right now.</summary>
    public bool Visible => _api.IsOverlayVisible(_handle);

    /// <summary>Hangs the quad off the headset itself, which is what head-locked means to OpenVR.</summary>
    public void PlaceOnHead(VrPose offset)
    {
        if (_appliedHeadOffset is { } applied && Close(applied, offset))
        {
            return;
        }

        var matrix = VrMatrix.ToOpenVr(offset);

        if (Went(
            _api.SetOverlayTransformTrackedDeviceRelative(
                _handle,
                OpenVR.k_unTrackedDeviceIndex_Hmd,
                ref matrix),
            "Hanging the quad off the headset"))
        {
            _appliedHeadOffset = offset;
            _appliedPose = null;
        }
    }

    /// <summary>Places the quad in the tracking universe, if it is not already there.</summary>
    public void PlaceAbsolute(VrPose pose)
    {
        if (_appliedPose is { } applied && Close(applied, pose))
        {
            return;
        }

        var matrix = VrMatrix.ToOpenVr(pose);

        // Latched only once it went through, here and below.
        if (Went(
            _api.SetOverlayTransformAbsolute(
                _handle,
                ETrackingUniverseOrigin.TrackingUniverseSeated,
                ref matrix),
            "Placing the quad"))
        {
            _appliedPose = pose;
            _appliedHeadOffset = null;
        }
    }

    public void Look(float widthMetres, float curvature, float opacity)
    {
        if (!Same(_appliedWidth, widthMetres)
            && Went(_api.SetOverlayWidthInMeters(_handle, widthMetres), "Setting the width"))
        {
            _appliedWidth = widthMetres;
        }

        if (!Same(_appliedCurvature, curvature)
            && Went(_api.SetOverlayCurvature(_handle, curvature), "Setting the curvature"))
        {
            _appliedCurvature = curvature;
        }

        if (!Same(_appliedAlpha, opacity)
            && Went(_api.SetOverlayAlpha(_handle, opacity), "Setting the opacity"))
        {
            _appliedAlpha = opacity;
        }
    }

    /// <summary>Puts this quad above the panel.</summary>
    public void Above(uint sortOrder) => _api.SetOverlaySortOrder(_handle, SortOrder + sortOrder);

    public void Show(bool shown)
    {
        if (_shown == shown)
        {
            return;
        }

        // Latched after the call, not before.
        var went = shown
            ? Went(_api.ShowOverlay(_handle), "Showing the quad")
            : Went(_api.HideOverlay(_handle), "Hiding the quad");

        if (went)
        {
            _shown = shown;
        }
    }

    /// <summary>What SteamVR says about this quad, read back rather than remembered.</summary>
    public string Describe()
    {
        var visible = _api.IsOverlayVisible(_handle);

        var alpha = 0f;
        _api.GetOverlayAlpha(_handle, ref alpha);

        var width = 0f;
        _api.GetOverlayWidthInMeters(_handle, ref width);

        // Asked which kind first.
        var kind = VROverlayTransformType.VROverlayTransform_Absolute;
        _api.GetOverlayTransformType(_handle, ref kind);

        var held = new HmdMatrix34_t();
        var where = "";
        EVROverlayError got;

        if (kind == VROverlayTransformType.VROverlayTransform_TrackedDeviceRelative)
        {
            uint device = 0;
            got = _api.GetOverlayTransformTrackedDeviceRelative(_handle, ref device, ref held);
            where = $"riding device {device}";
        }
        else
        {
            var universe = ETrackingUniverseOrigin.TrackingUniverseSeated;
            got = _api.GetOverlayTransformAbsolute(_handle, ref universe, ref held);
            where = $"universe={universe}";
        }

        if (got != EVROverlayError.None)
        {
            return $"visible={visible} alpha={alpha:0.00} width={width:0.00} {kind} transform=<{got}>";
        }

        var pose = VrMatrix.ToPose(held);
        var forward = System.Numerics.Vector3.Transform(
            -System.Numerics.Vector3.UnitZ,
            pose.Orientation);

        return $"visible={visible} alpha={alpha:0.00} width={width:0.00}m {where} "
               + $"at ({pose.Position.X:0.00}, {pose.Position.Y:0.00}, {pose.Position.Z:0.00}) "
               + $"facing ({forward.X:0.00}, {forward.Y:0.00}, {forward.Z:0.00})";
    }

    /// <summary>Drains this overlay's event queue.</summary>
    public void PumpEvents()
    {
        var next = new VREvent_t();
        var size = (uint)Marshal.SizeOf<VREvent_t>();

        while (_api.PollNextOverlayEvent(_handle, ref next, size))
        {
        // Drained, not read.
        }
    }

    /// <summary>Gives the quad back.</summary>
    public void Dispose()
    {
        _session.Overlay?.ClearOverlayTexture(_handle);
        _session.Overlay?.DestroyOverlay(_handle);
    }

    private static bool Same(float a, float b) => Math.Abs(a - b) < 0.0005f;

    private static bool Close(VrPose a, VrPose b) =>
        System.Numerics.Vector3.DistanceSquared(a.Position, b.Position) < 1e-8f
        && Math.Abs(System.Numerics.Quaternion.Dot(a.Facing, b.Facing)) > 0.999999f;
}
