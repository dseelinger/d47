using System.Numerics;
using System.Runtime.InteropServices;
using D47.Core.Vr;
using Microsoft.Extensions.Logging;
using Valve.VR;

namespace D47.Vr;

/// <summary>One surface's pixels, as the runtime needs to ask for them.</summary>
public interface IVrSurfaceSource
{
    VrSurface Surface { get; }

    /// <summary>Whether this surface should be on screen at all right now.</summary>
    bool Visible { get; }

    /// <summary>Where it goes and what it looks like.</summary>
    SurfacePlacement Placement { get; }

    /// <summary>Whether a controller ray is cast at this quad at all, and so whether it can be carried.</summary>
    bool TakesPointer { get; }

    /// <summary>The pixel size it wants.</summary>
    (int Width, int Height) Size { get; }

    /// <summary>Whether anything has changed since the last draw.</summary>
    bool IsDirty { get; }

    /// <summary>
    /// Rasterises straight into the buffer the runtime will hand OpenVR, with no intermediate copy.
    /// </summary>
    void Draw(IntPtr destination, int rowBytes);

    /// <summary>Told where the head is, so a head-locked surface can work out where it goes.</summary>
    void Observe(VrPose head);
}

/// <summary>The headset, for real.</summary>
public sealed class SteamVrRuntime(
    IReadOnlyList<IVrSurfaceSource> sources,
    ILogger<SteamVrRuntime> logger) : IVrRuntime
{
    /// <summary>Reverse-domain, one per quad.</summary>
    private static readonly IReadOnlyDictionary<VrSurface, (string Key, string Name)> Keys =
        new Dictionary<VrSurface, (string, string)>
        {
            [VrSurface.PanelFull] = ("com.dseelinger.D47.panel", "D47"),
            [VrSurface.PanelMini] = ("com.dseelinger.D47.panel", "D47"),
            [VrSurface.Captions] = ("com.dseelinger.D47.captions", "D47 captions"),
        };

    /// <summary>One process, one session.</summary>
    private static int _sessionClaimed;

    private readonly Dictionary<string, VrOverlay> _overlays = new(StringComparer.Ordinal);

    /// <summary>Complaints already reported, so a refusal ten times a second is logged once.</summary>
    private readonly HashSet<string> _complaints = [];

    /// <summary>Surfaces that have been served at least once, so the report is not per frame.</summary>
    private readonly HashSet<VrSurface> _served = [];

    /// <summary>
    /// Each surface's last upload — whether the compositor refused it and whether that has been said.
    /// </summary>
    private readonly Dictionary<VrSurface, FrameHeld> _frames = [];

    /// <summary>The last thing each surface said about itself, and when it said it.</summary>
    private readonly Dictionary<VrSurface, SurfaceReport> _described = [];

    private DateTimeOffset _now;
    private readonly Dictionary<VrSurface, VrPixels> _buffers = [];

    /// <summary>
    /// The grip-to-tip correction per device, which is a property of the controller model rather than
    /// of the frame.
    /// </summary>
    private readonly Dictionary<uint, Matrix4x4> _gripToTip = [];

    /// <summary>
    /// The last thing each controller reported about itself — connected, tracking, and SteamVR's own
    /// activity level — so a change is logged once and a steady state not at all.
    /// </summary>
    private readonly Dictionary<uint, ControllerSeen> _controllersSeen = [];

    private readonly record struct ControllerSeen(bool Connected, bool Tracking, EDeviceActivityLevel Activity);

    /// <summary>The trigger.</summary>
    public VrActionInput Actions { get; } = new(logger);

    /// <summary>Whether d47 may touch the motion controllers at all (#198).</summary>
    public bool Pointing { get; set; }

    /// <summary>
    /// The aim beam and the cursor: two more overlays, and they have to be overlays rather than pixels
    /// drawn into the panel.
    /// </summary>
    private VrOverlay? _beam;

    private VrOverlay? _cursor;

    private float _beamLength = float.NaN;

    private CVRSystem? _system;
    private bool _claimed;

    /// <summary>The last head pose read.</summary>
    public VrPose? Head { get; private set; }

    public VrStart Start()
    {
        if (!OpenVrLoader.Register())
        {
            return new VrStart(
                VrStartOutcome.NoRuntime,
                "No SteamVR runtime is installed on this machine.");
        }

        if (Interlocked.CompareExchange(ref _sessionClaimed, 1, 0) != 0)
        {
            return new VrStart(VrStartOutcome.Failed, "A headset session is already running in this process.");
        }

        _claimed = true;

        try
        {
            return Bring();
        }
        catch (Exception ex) when (ex is DllNotFoundException or EntryPointNotFoundException or BadImageFormatException)
        {
            // The runtime path resolved but the library did not load or did not have what the binding expects
            // — a version disagreement between the vendored header and the installed runtime.
            Release();
            logger.LogWarning(ex, "The OpenVR runtime could not be loaded");
            return new VrStart(VrStartOutcome.NoRuntime, "The SteamVR runtime could not be loaded.");
        }
    }

    public bool Serve(DateTimeOffset now)
    {
        if (_system is null)
        {
            return false;
        }

        _now = now;

        if (!PumpSystem())
        {
            return false;
        }

        // Here as well as in Bring, so the motion-controller row is a live switch: see Guides.
        Guides();

        var head = ReadHead();
        if (head is { } pose)
        {
            Head = pose;
        }

        foreach (var source in sources)
        {
            if (head is { } seen)
            {
                source.Observe(seen);
            }

            if (!Serve(source))
            {
                return false;
            }
        }

        return true;
    }

    public void Stop()
    {
        _buffers.Clear();

        _beam?.Dispose();
        _cursor?.Dispose();
        _beam = null;
        _cursor = null;
        _beamLength = float.NaN;
        _guidesFor = null;

        foreach (var overlay in _overlays.Values)
        {
            overlay.Dispose();
        }

        _overlays.Clear();

        // A rebuilt session is a new session, and it has to be able to say so — otherwise the one that
        // recovered looks exactly like the one that never reported anything.
        _complaints.Clear();
        _served.Clear();
        _described.Clear();
        _controllersSeen.Clear();

        if (_system is not null)
        {
            // Before the session goes, so a claim standing at the moment the overlay was switched off is
            // given back rather than left for SteamVR to notice.
            Actions.Release();
            OpenVR.Shutdown();
            _system = null;
        }

        Release();
    }

    /// <summary>The aim beam, if the runtime allowed one.</summary>
    public VrOverlay? Beam => _beam;

    /// <summary>The cursor sprite, if the runtime allowed one.</summary>
    public VrOverlay? Cursor => _cursor;

    /// <summary>
    /// Points the beam along a hand and stops it at <paramref name="lengthMetres"/>, or takes it off
    /// screen when nothing is being aimed at.
    /// </summary>
    public void Reposition(VrSurface surface, VrPose where) => OverlayFor(surface)?.PlaceAbsolute(where);

    public void AimBeam(VrPose? along, VrPose head, float lengthMetres)
    {
        if (_beam is null)
        {
            return;
        }

        if (along is not { } aim)
        {
            _beam.Show(false);
            return;
        }

        if (!_beamLength.Equals(lengthMetres))
        {
            _beamLength = lengthMetres;
            _beam.Look(VrAim.BeamWidthFor(lengthMetres), 0f, 1f);
        }

        _beam.PlaceAbsolute(VrAim.BeamAlong(aim, head.Position, lengthMetres));
        _beam.Show(true);
    }

    /// <summary>Puts the cursor on a world point, or takes it off screen.</summary>
    public void ShowCursor(Vector3? at, VrPose head)
    {
        if (_cursor is null)
        {
            return;
        }

        if (at is not { } point)
        {
            _cursor.Show(false);
            return;
        }

        _cursor.PlaceAbsolute(VrAim.CursorAt(point, head.Position));
        _cursor.Show(true);
    }

    /// <summary>The overlay a surface is drawn on, for the placement code to point rays at.</summary>
    public VrOverlay? OverlayFor(VrSurface surface) =>
        Keys.TryGetValue(surface, out var key) && _overlays.TryGetValue(key.Key, out var overlay)
            ? overlay
            : null;

    /// <summary>
    /// The controllers and the head from one pose read, for a caller that needs both and is asking at
    /// frame rate (#19).
    /// </summary>
    public (IReadOnlyList<VrHand> Hands, VrPose? Head) HandsAndHead()
    {
        if (_system is null)
        {
            return ([], null);
        }

        // The withdrawal, at the one place a controller is actually read (#198).
        if (!Pointing)
        {
            return ([], ReadHead());
        }

        // Reused rather than allocated per call: sixty-four entries at frame rate is garbage this process
        // makes while sharing a GPU with Elite, and it is the same array every time.
        var poses = _poses ??= new TrackedDevicePose_t[OpenVR.k_unMaxTrackedDeviceCount];

        // No prediction.
        _system.GetDeviceToAbsoluteTrackingPose(
            ETrackingUniverseOrigin.TrackingUniverseSeated,
            0,
            poses);

        var found = new List<VrHand>(2);

        for (uint device = 0; device < poses.Length; device++)
        {
            if (_system.GetTrackedDeviceClass(device) != ETrackedDeviceClass.Controller)
            {
                continue;
            }

            Note(device, poses[device]);

            if (VrMatrix.Real(poses[device]) is { } grip)
            {
                var aim = VrPose.FromMatrix(GripToTip(device) * grip.ToMatrix());
                found.Add(new VrHand(device, grip, aim));
            }
        }

        return (found, VrMatrix.Real(poses[OpenVR.k_unTrackedDeviceIndex_Hmd]));
    }

    private TrackedDevicePose_t[]? _poses;

    /// <summary>The correction from the grip pose OpenVR reports to the tip the Commander aims with.</summary>
    private Matrix4x4 GripToTip(uint device)
    {
        if (_gripToTip.TryGetValue(device, out var cached))
        {
            return cached;
        }

        var correction = Matrix4x4.Identity;
        var models = OpenVR.RenderModels;

        if (models is not null && ModelName(device) is { } model)
        {
            foreach (var component in
                     new[] { OpenVR.k_pch_Controller_Component_Tip, OpenVR.k_pch_Controller_Component_OpenXR_Aim })
            {
                if (!models.RenderModelHasComponent(model, component))
                {
                    continue;
                }

                var buttons = default(VRControllerState_t);
                var mode = default(RenderModel_ControllerMode_State_t);
                var state = default(RenderModel_ComponentState_t);

                if (models.GetComponentState(model, component, ref buttons, ref mode, ref state))
                {
                    correction = VrMatrix.ToMatrix(state.mTrackingToComponentLocal);
                    break;
                }
            }
        }

        _gripToTip[device] = correction;

        logger.LogDebug(
            "Controller {Device} aims {Source}",
            device,
            correction == Matrix4x4.Identity ? "from the grip; no tip component was available" : "from its tip");

        return correction;
    }

    /// <summary>
    /// Says, once per change, what a controller is reporting — see <see cref="_controllersSeen"/>.
    /// </summary>
    private void Note(uint device, TrackedDevicePose_t pose)
    {
        var seen = new ControllerSeen(
            pose.bDeviceIsConnected,
            pose.bPoseIsValid,
            _system!.GetTrackedDeviceActivityLevel(device));

        if (_controllersSeen.TryGetValue(device, out var was) && was == seen)
        {
            return;
        }

        _controllersSeen[device] = seen;

        logger.LogInformation(
            "Controller {Device} ({Serial}) is {Connected}, {Tracking}, activity {Activity}",
            device,
            Property(device, ETrackedDeviceProperty.Prop_SerialNumber_String) ?? "no serial",
            seen.Connected ? "connected" : "disconnected",
            seen.Tracking ? "tracking" : "not tracking",
            seen.Activity);
    }

    private string? ModelName(uint device) =>
        Property(device, ETrackedDeviceProperty.Prop_RenderModelName_String);

    private string? Property(uint device, ETrackedDeviceProperty property)
    {
        var error = ETrackedPropertyError.TrackedProp_Success;
        var text = new System.Text.StringBuilder((int)OpenVR.k_unMaxPropertyStringSize);

        _system!.GetStringTrackedDeviceProperty(
            device,
            property,
            text,
            OpenVR.k_unMaxPropertyStringSize,
            ref error);

        return error == ETrackedPropertyError.TrackedProp_Success && text.Length > 0
            ? text.ToString()
            : null;
    }

    private VrStart Bring()
    {
        // Asked before VR_Init, and this is the reason for asking: VR_Init *starts SteamVR* if it is not
        // already running.
        if (!SteamVrIsRunning())
        {
            Release();
            return new VrStart(
                VrStartOutcome.NotReady,
                "SteamVR is not running. D47 will attach when you start it.");
        }

        if (!OpenVR.IsHmdPresent())
        {
            Release();
            return new VrStart(
                VrStartOutcome.NotReady,
                "No headset is switched on. D47 will attach when one appears.");
        }

        var error = EVRInitError.None;
        _system = OpenVR.Init(ref error, EVRApplicationType.VRApplication_Overlay);

        if (error != EVRInitError.None || _system is null)
        {
            // Not discriminated by code, deliberately.
            Release();
            return new VrStart(
                VrStartOutcome.NotReady,
                $"SteamVR is not ready: {OpenVR.GetStringForHmdError(error)}");
        }

        if (OpenVR.Overlay is null)
        {
            Release();
            return new VrStart(VrStartOutcome.Failed, "SteamVR started but has no overlay interface.");
        }

        // The keys are claimed before anything expensive is built.
        foreach (var source in sources)
        {
            var (key, name) = Keys[source.Surface];

            if (_overlays.ContainsKey(key))
            {
                continue;
            }

            var overlay = VrOverlay.Create(key, name, out var failure, Refused);

            if (overlay is null)
            {
                Stop();
                return failure;
            }

            _overlays[key] = overlay;
        }

        Guides();

        logger.LogInformation("Headset overlays are up; {Count} quad(s) claimed", _overlays.Count);

        return VrStart.Started;
    }

    /// <summary>Which state of <see cref="Pointing"/> the beam and cursor were last built for.</summary>
    private bool? _guidesFor;

    /// <summary>
    /// The beam and the cursor, built when there is something to guide and taken down when there is not
    /// (#198).
    /// </summary>
    private void Guides()
    {
        if (_guidesFor == Pointing)
        {
            return;
        }

        _guidesFor = Pointing;

        _beam?.Dispose();
        _cursor?.Dispose();
        _beam = null;
        _cursor = null;
        _beamLength = float.NaN;

        if (!Pointing)
        {
            return;
        }

        // Both fail soft.
        _beam = Sprite("com.dseelinger.D47.beam", "D47 aim", VrSprites.Beam(),
            VrAim.BeamPixelsWide, VrAim.BeamPixelsTall, VrAim.BeamWidthFor(1f), sortOrder: 1);

        _cursor = Sprite("com.dseelinger.D47.cursor", "D47 cursor", VrSprites.Cursor(),
            VrSprites.CursorSize, VrSprites.CursorSize, VrAim.CursorSizeMetres, sortOrder: 2);
    }

    /// <summary>One of the two static quads: created, given its pixels once, sized, and left.</summary>
    private VrOverlay? Sprite(
        string key,
        string name,
        byte[] pixels,
        int width,
        int height,
        float widthMetres,
        uint sortOrder)
    {
        var overlay = VrOverlay.Create(key, name, out _, Refused);

        if (overlay is null)
        {
            logger.LogWarning("SteamVR would not create the {Name} overlay; pointing goes unguided", name);
            return null;
        }

        var pinned = System.Runtime.InteropServices.GCHandle.Alloc(pixels, System.Runtime.InteropServices.GCHandleType.Pinned);

        try
        {
            // SetOverlayRaw copies before returning, which is what makes handing it a bare address safe — but
            // only for the duration of the call, hence the pin.
            overlay.Submit(pinned.AddrOfPinnedObject(), width, height);
        }
        finally
        {
            pinned.Free();
        }

        overlay.Look(widthMetres, 0f, 1f);
        overlay.Above(sortOrder);
        overlay.Show(false);
        return overlay;
    }

    /// <summary>Watches for SteamVR going away on purpose.</summary>
    private bool PumpSystem()
    {
        var next = new VREvent_t();
        var size = (uint)Marshal.SizeOf<VREvent_t>();

        while (_system!.PollNextEvent(ref next, size))
        {
            if ((EVREventType)next.eventType == EVREventType.VREvent_Quit)
            {
                // Acknowledged so SteamVR stops waiting on us before it exits.
                _system.AcknowledgeQuit_Exiting();
                logger.LogInformation("SteamVR is shutting down");
                return false;
            }
        }

        return true;
    }

    private VrPose? ReadHead()
    {
        var poses = new TrackedDevicePose_t[OpenVR.k_unTrackedDeviceIndex_Hmd + 1];
        _system!.GetDeviceToAbsoluteTrackingPose(
            ETrackingUniverseOrigin.TrackingUniverseSeated,
            0,
            poses);

        return VrMatrix.Real(poses[OpenVR.k_unTrackedDeviceIndex_Hmd]);
    }

    /// <summary>Says what the runtime turned down, once per distinct complaint.</summary>
    private void Refused(string what)
    {
        if (_complaints.Add(what))
        {
            logger.LogWarning("SteamVR refused an overlay call. {What}", what);
        }
    }

    /// <summary>Says that a surface the compositor was turning down is going through again.</summary>
    private void Recovered(VrSurface surface)
    {
        logger.LogInformation("{Surface}: SteamVR is taking frames again", surface);

        // Only the one that stalled.
        _complaints.RemoveWhere(complaint =>
            complaint.Contains($"'{Keys[surface].Key}'", StringComparison.Ordinal));
    }

    private bool Serve(IVrSurfaceSource source)
    {
        var overlay = OverlayFor(source.Surface);

        if (overlay is null)
        {
            return false;
        }

        if (!source.Visible)
        {
            overlay.Show(false);
            return true;
        }

        var placement = source.Placement.Sane();
        var (width, height) = source.Size;

        var reallocated = false;

        if (!_buffers.TryGetValue(source.Surface, out var pixels))
        {
            pixels = new VrPixels(width, height);
            _buffers[source.Surface] = pixels;
            reallocated = true;
        }
        else
        {
            reallocated = pixels.Width != width || pixels.Height != height;
            pixels.Resize(width, height);
        }

        var held = _frames.TryGetValue(source.Surface, out var carried) ? carried : default;

        // A held frame lives in the buffer it was drawn into, so a reallocation throws it away along with the
        // pixels.
        if (reallocated)
        {
            held = default;
        }

        // Asked only when something is actually waiting to go again.
        var drawnByTheRuntime = held.Pending && overlay.Visible;

        // Whether a refused frame is retried, and whether it is retried now, is decided in Core where a test
        // can drive a session's worth of them without a headset.
        var plan = FrameDelivery.Plan(held, source.IsDirty, drawnByTheRuntime);

        if (plan.Draw)
        {
            source.Draw(pixels.Address, pixels.RowBytes);
            pixels.ToRgba();
        }

        if (plan.Submit)
        {
            var outcome = FrameDelivery.Took(
                held,
                overlay.Submit(pixels.Address, pixels.Width, pixels.Height));

            if (outcome.Recovered)
            {
                Recovered(source.Surface);
            }

            if (outcome.Rotate)
            {
                // Onto the next buffer of the ring, so the one the runtime was just handed is not the one the
                // next frame is drawn into.
                pixels.Rotate();
            }

            held = outcome.Held;
        }

        _frames[source.Surface] = held;

        // Head-locked rides the headset; only something put down in the room needs a room position.
        var where = placement.Where(Head ?? VrPose.Origin);

        if (placement.RidesTheHead)
        {
            // The head goes in so the offset can cancel its roll (#189).
            overlay.PlaceOnHead(placement.AgainstTheHead(Head ?? VrPose.Origin));
        }
        else
        {
            overlay.PlaceAbsolute(where);
        }
        overlay.Look(placement.WidthMetres, placement.Curvature, placement.Opacity);
        overlay.Show(true);
        overlay.PumpEvents();

        // Read back from the runtime every few seconds while the session is up, rather than once.
        var described = overlay.Describe();

        var readback = RuntimeReadback.Plan(
            _described.TryGetValue(source.Surface, out var last) ? last : null,
            described,
            _now);

        _described[source.Surface] = readback.Held;

        if (readback.Write)
        {
            logger.LogInformation("{Surface}: {State}", source.Surface, described);
        }

        // Once per surface per session. "The overlays are up" says the quads were created, not that anything
        // was ever put in one or that it went anywhere a Commander could look — and when the answer is "I see
        // nothing at all", those are the only two questions left.
        if (_served.Add(source.Surface))
        {
            logger.LogInformation(
                "{Surface} is up: {Width}x{Height} at ({X:0.00}, {Y:0.00}, {Z:0.00}), "
                + "{Metres:0.00}m wide, opacity {Opacity:0.00}, head {Head}",
                source.Surface,
                width,
                height,
                where.Position.X,
                where.Position.Y,
                where.Position.Z,
                placement.WidthMetres,
                placement.Opacity,
                Head is null ? "not tracking" : "tracking");
        }

        return true;
    }

    /// <summary>Whether a SteamVR session already exists to attach to.</summary>
    public static bool SteamVrIsRunning()
    {
        try
        {
            return System.Diagnostics.Process.GetProcessesByName("vrserver").Length > 0;
        }
        catch (Exception ex) when (ex is InvalidOperationException or SystemException)
        {
            // Cannot enumerate processes.
            return false;
        }
    }

    private void Release()
    {
        if (_claimed)
        {
            _claimed = false;
            Interlocked.Exchange(ref _sessionClaimed, 0);
        }
    }
}
