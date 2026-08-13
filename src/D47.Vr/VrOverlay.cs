using System.Runtime.InteropServices;
using D47.Core.Vr;
using Valve.VR;

namespace D47.Vr;

/// <summary>What the pointer did to a surface this tick.</summary>
public readonly record struct OverlayPointer(bool Pointing, bool Held);

/// <summary>
/// One <c>IVROverlay</c> quad: its texture, its transform, its look, and its pointer.
/// <para>
/// Nothing is pushed to the runtime unless it changed. A transform written every frame fights
/// the one the Commander is currently dragging it to, and a width re-sent to say it is the
/// width it already is is a runtime call per tick saying nothing.
/// </para>
/// </summary>
public sealed class VrOverlay : IDisposable
{
    /// <summary>
    /// The trigger, arriving as a mouse button because the overlay asked for
    /// <see cref="VROverlayInputMethod.Mouse"/>. A controller reports its other buttons —
    /// including the grip — through this same event, so treating them all as a press means the
    /// grip also clicks whatever the ray happened to be over.
    /// </summary>
    private const uint Trigger = (uint)EVRMouseButton.Left;

    /// <summary>
    /// High, so the panel sits above ordinary overlays and above SteamVR's own furniture.
    /// Not highest: it has no business claiming it outranks a system warning.
    /// </summary>
    private const uint SortOrder = 100;

    private readonly ulong _handle;
    private readonly Action<string>? _refused;

    private VrPose? _appliedPose;
    private float _appliedWidth = float.NaN;
    private float _appliedCurvature = float.NaN;
    private float _appliedAlpha = float.NaN;
    private (int Width, int Height) _appliedMouseScale = (-1, -1);
    private bool _shown;

    private bool _pointing;
    private bool _held;

    private VrOverlay(ulong handle, string key, Action<string>? refused)
    {
        _handle = handle;
        Key = key;
        _refused = refused;
    }

    /// <summary>
    /// Reports a call the runtime turned down, and answers whether it went through.
    /// <para>
    /// Every one of these used to be discarded. An overlay whose texture SteamVR refused is not
    /// a blank overlay, it is an overlay with nothing in it — indistinguishable, from inside a
    /// headset, from one that was never shown, and equally silent from outside one. Nothing on
    /// this path is loud enough to be found by looking at it.
    /// </para>
    /// </summary>
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

    public OverlayPointer Pointer => new(_pointing, _held);

    /// <summary>
    /// Creates the quad. <see cref="EVROverlayError.KeyInUse"/> is called out by name because
    /// the runtime's own word for it is not the useful half: a key is one application's
    /// identity for one quad, so the only way to be told it is in use is that something else
    /// already has it — and that something is almost always another copy of d47.
    /// </summary>
    public static VrOverlay? Create(string key, string name, out VrStart failure, Action<string>? refused = null)
    {
        ulong handle = 0;
        var created = OpenVR.Overlay.CreateOverlay(key, name, ref handle);

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

        var overlay = new VrOverlay(handle, key, refused);

        // The handle exists from the moment CreateOverlay succeeded, so a failure after this
        // point still has a quad to give back. Without this, a refused call would leave one in
        // the compositor with nothing holding it.
        try
        {
            OpenVR.Overlay.SetOverlaySortOrder(handle, SortOrder);

            // Off by default, and this is the reason: the flag sorts the quad with the
            // *non-scene* overlays, which is the class SteamVR's dashboard belongs to. It was
            // added to settle the panel against SteamVR's own menu, keyboard and notifications
            // — without it the panel is legible right up until anything of SteamVR's appears
            // and then it is behind it — but a panel that is only composited alongside the
            // dashboard is a panel that vanishes the moment the dashboard is dismissed, which
            // is worse than losing an argument about z-order.
            //
            // Set D47_VR_SORT=nonscene to put it back, so the two can be compared in one
            // session rather than one build each.
            if (Environment.GetEnvironmentVariable("D47_VR_SORT") == "nonscene")
            {
                OpenVR.Overlay.SetOverlayFlag(handle, VROverlayFlags.SortWithNonSceneOverlays, true);
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

    /// <summary>
    /// Asks SteamVR to point at this quad.
    /// <para>
    /// The flag is the load-bearing half and its absence is silent: without it the overlay is a
    /// picture, SteamVR draws no laser for it, and the ray passes straight through. The mouse
    /// scale is given in the quad's own metres so a reported position arrives in the units
    /// everything else uses, and it has to be re-sent on every size change or the quad answers
    /// a laser against the size it used to be.
    /// </para>
    /// </summary>
    public void TakePointer(int width, int height)
    {
        OpenVR.Overlay.SetOverlayInputMethod(_handle, VROverlayInputMethod.Mouse);
        OpenVR.Overlay.SetOverlayFlag(_handle, VROverlayFlags.MakeOverlaysInteractiveIfVisible, true);
        SetMouseScale(width, height);
    }

    public void SetMouseScale(int width, int height)
    {
        if (_appliedMouseScale == (width, height))
        {
            return;
        }

        _appliedMouseScale = (width, height);
        var scale = new HmdVector2_t { v0 = width, v1 = height };
        OpenVR.Overlay.SetOverlayMouseScale(_handle, ref scale);
    }

    public void Submit(IntPtr texture)
    {
        var handed = new Texture_t
        {
            handle = texture,
            eType = ETextureType.DirectX,
            eColorSpace = EColorSpace.Auto,
        };

        Went(OpenVR.Overlay.SetOverlayTexture(_handle, ref handed), "Setting the texture");
    }

    /// <summary>Places the quad in the tracking universe, if it is not already there.</summary>
    public void PlaceAbsolute(VrPose pose)
    {
        if (_appliedPose is { } applied && Close(applied, pose))
        {
            return;
        }

        var matrix = VrMatrix.ToOpenVr(pose);

        // Latched only once it went through, here and below. Recording a value the runtime
        // refused means never trying it again — the quad keeps whatever it had, and the
        // "nothing is pushed unless it changed" optimisation quietly becomes "nothing is
        // pushed".
        if (Went(
            OpenVR.Overlay.SetOverlayTransformAbsolute(
                _handle,
                ETrackingUniverseOrigin.TrackingUniverseSeated,
                ref matrix),
            "Placing the quad"))
        {
            _appliedPose = pose;
        }
    }

    public void Look(float widthMetres, float curvature, float opacity)
    {
        if (!Same(_appliedWidth, widthMetres)
            && Went(OpenVR.Overlay.SetOverlayWidthInMeters(_handle, widthMetres), "Setting the width"))
        {
            _appliedWidth = widthMetres;
        }

        if (!Same(_appliedCurvature, curvature)
            && Went(OpenVR.Overlay.SetOverlayCurvature(_handle, curvature), "Setting the curvature"))
        {
            _appliedCurvature = curvature;
        }

        if (!Same(_appliedAlpha, opacity)
            && Went(OpenVR.Overlay.SetOverlayAlpha(_handle, opacity), "Setting the opacity"))
        {
            _appliedAlpha = opacity;
        }
    }

    public void Show(bool shown)
    {
        if (_shown == shown)
        {
            return;
        }

        // Latched after the call, not before. Recording it as shown and then being refused left
        // an overlay that was never shown and would never be asked again, because every later
        // tick saw the state it wanted and returned.
        var went = shown
            ? Went(OpenVR.Overlay.ShowOverlay(_handle), "Showing the quad")
            : Went(OpenVR.Overlay.HideOverlay(_handle), "Hiding the quad");

        if (went)
        {
            _shown = shown;
        }
    }

    /// <summary>
    /// Drains the event queue and updates the latched pointer state.
    /// <para>
    /// Latched, because SteamVR reports transitions rather than state: a hand held still sends
    /// nothing at all, and treating no event as no pointer would make a grab let go the moment
    /// somebody stopped moving. Drained rather than sampled, because reading one event per
    /// frame puts the pointer behind the ray by however many were skipped.
    /// </para>
    /// </summary>
    public void PumpEvents()
    {
        var next = new VREvent_t();
        var size = (uint)Marshal.SizeOf<VREvent_t>();

        while (OpenVR.Overlay.PollNextOverlayEvent(_handle, ref next, size))
        {
            switch ((EVREventType)next.eventType)
            {
                case EVREventType.VREvent_MouseMove:
                    _pointing = true;
                    break;

                case EVREventType.VREvent_MouseButtonDown when next.data.mouse.button == Trigger:
                    _pointing = true;
                    _held = true;
                    break;

                case EVREventType.VREvent_MouseButtonUp when next.data.mouse.button == Trigger:
                    _held = false;
                    break;

                case EVREventType.VREvent_FocusLeave:
                    // The trigger is dropped with it. A Commander who aims away mid-drag gets
                    // no button-up on this overlay, and a hold nobody ever ends is one that
                    // carries the panel around for the rest of the session.
                    _pointing = false;
                    _held = false;
                    break;
            }
        }
    }

    /// <summary>Whether a ray from <paramref name="from"/> meets this quad, and how far along.</summary>
    public float? IntersectedBy(System.Numerics.Vector3 from, System.Numerics.Vector3 along)
    {
        var parameters = new VROverlayIntersectionParams_t
        {
            vSource = new HmdVector3_t { v0 = from.X, v1 = from.Y, v2 = from.Z },
            vDirection = new HmdVector3_t { v0 = along.X, v1 = along.Y, v2 = along.Z },
            eOrigin = ETrackingUniverseOrigin.TrackingUniverseSeated,
        };

        var results = new VROverlayIntersectionResults_t();

        return OpenVR.Overlay.ComputeOverlayIntersection(_handle, ref parameters, ref results)
            ? results.fDistance
            : null;
    }

    /// <summary>
    /// Gives the quad back. The texture is cleared first and the order is not bookkeeping: an
    /// overlay outliving the device that owns its image leaves SteamVR querying a destroyed
    /// one, which surfaces as a fault inside the runtime a long way from the line that caused
    /// it.
    /// </summary>
    public void Dispose()
    {
        OpenVR.Overlay?.ClearOverlayTexture(_handle);
        OpenVR.Overlay?.DestroyOverlay(_handle);
    }

    private static bool Same(float a, float b) => Math.Abs(a - b) < 0.0005f;

    private static bool Close(VrPose a, VrPose b) =>
        System.Numerics.Vector3.DistanceSquared(a.Position, b.Position) < 1e-8f
        && Math.Abs(System.Numerics.Quaternion.Dot(a.Facing, b.Facing)) > 0.999999f;
}
