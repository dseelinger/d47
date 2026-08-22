using System.Numerics;
using System.Runtime.InteropServices;
using D47.Core.Vr;
using Microsoft.Extensions.Logging;
using Valve.VR;

namespace D47.Vr;

/// <summary>
/// One surface's pixels, as the runtime needs to ask for them. Implemented in the app, where
/// the widget tree lives — this project stays free of Avalonia so that the graphics and the
/// runtime interop are the only things in it.
/// <para>
/// Everything here is called on the thread that drives <see cref="SteamVrRuntime.Serve"/>,
/// which the app arranges to be its UI thread: a <c>Visual</c> is thread-affine, so the
/// alternative is a blocking hop from the tick thread into the dispatcher on every frame.
/// </para>
/// </summary>
public interface IVrSurfaceSource
{
    VrSurface Surface { get; }

    /// <summary>Whether this surface should be on screen at all right now.</summary>
    bool Visible { get; }

    /// <summary>Where it goes and what it looks like.</summary>
    SurfacePlacement Placement { get; }

    /// <summary>
    /// Whether a controller ray is cast at this quad at all, and so whether it can be carried.
    /// Only a surface something can be done to wants this: the panel is grab-to-move, and captions
    /// are a label that would merely be in the way of everything behind them.
    /// <para>
    /// Asked of the surface rather than assumed, because it is the one half of the arrangement a
    /// test can reach without a headset — and it is the half that was missing twice. It first named
    /// two overlay flags that nothing ever set. Those flags turned out to be the wrong road
    /// entirely: they opt in to SteamVR's own laser, which only runs over its dashboard, so the
    /// events they were meant to unlock never arrive while a game holds the headset. What it gates
    /// now is d47's own ray cast, in <c>VrRay.PointingAt</c>, which does not depend on SteamVR
    /// pointing at anything.
    /// </para>
    /// </summary>
    bool TakesPointer { get; }

    /// <summary>
    /// The pixel size it wants. A change reallocates the buffer, which is why it is asked
    /// rather than fixed: mini is a smaller image, not the same image hung nearer.
    /// </summary>
    (int Width, int Height) Size { get; }

    /// <summary>
    /// Whether anything has changed since the last draw. D1's second Phase 9 instruction: the
    /// panel is view-model-driven, so the measured 4-10 Hz cost is a worst case rather than a
    /// target, and a surface with nothing new costs one boolean.
    /// </summary>
    bool IsDirty { get; }

    /// <summary>
    /// Rasterises straight into the buffer the runtime will hand OpenVR, with no intermediate
    /// copy. BGRA, which is what the rasteriser produces; the conversion to the RGBA the raw
    /// path reads happens on the way out, in <see cref="VrPixels.ToRgba"/>.
    /// </summary>
    void Draw(IntPtr destination, int rowBytes);

    /// <summary>Told where the head is, so a head-locked surface can work out where it goes.</summary>
    void Observe(VrPose head);
}

/// <summary>
/// The headset, for real. Everything in here needs SteamVR running and is therefore the one
/// part of the VR path that cannot be tested without hardware — which is exactly why so little
/// of the phase lives in it (architecture.md §8).
/// </summary>
public sealed class SteamVrRuntime(
    IReadOnlyList<IVrSurfaceSource> sources,
    ILogger<SteamVrRuntime> logger) : IVrRuntime
{
    /// <summary>
    /// Reverse-domain, one per quad. The compositor identifies an overlay by its key and two
    /// sharing one would be two overlays fighting.
    /// </summary>
    private static readonly IReadOnlyDictionary<VrSurface, (string Key, string Name)> Keys =
        new Dictionary<VrSurface, (string, string)>
        {
            [VrSurface.PanelFull] = ("com.dseelinger.D47.panel", "D47"),
            [VrSurface.PanelMini] = ("com.dseelinger.D47.panel", "D47"),
            [VrSurface.Captions] = ("com.dseelinger.D47.captions", "D47 captions"),
        };

    /// <summary>
    /// One process, one session. Nothing in OpenVR refuses a second <c>VR_Init</c> and a
    /// repeated one leaks, so the refusal has to live here — and process-wide is the right
    /// scope, because the leak is.
    /// </summary>
    private static int _sessionClaimed;

    private readonly Dictionary<string, VrOverlay> _overlays = new(StringComparer.Ordinal);

    /// <summary>Complaints already reported, so a refusal ten times a second is logged once.</summary>
    private readonly HashSet<string> _complaints = [];

    /// <summary>Surfaces that have been served at least once, so the report is not per frame.</summary>
    private readonly HashSet<VrSurface> _served = [];

    /// <summary>
    /// Each surface's last upload — whether the compositor refused it and whether that has been
    /// said. See <see cref="FrameDelivery"/>; this is only where the answer is kept.
    /// </summary>
    private readonly Dictionary<VrSurface, FrameHeld> _frames = [];

    /// <summary>
    /// The last thing each surface said about itself, and when it said it. A read-back identical
    /// to the one before it is not news, and printing it anyway is how the one that <em>is</em>
    /// news gets missed. When that amounts to a line in the log is
    /// <see cref="RuntimeReadback.Plan"/>'s decision; this is only where the answer is kept.
    /// </summary>
    private readonly Dictionary<VrSurface, SurfaceReport> _described = [];

    private DateTimeOffset _now;
    private readonly Dictionary<VrSurface, VrPixels> _buffers = [];

    /// <summary>
    /// The grip-to-tip correction per device, which is a property of the controller model rather
    /// than of the frame. See <see cref="GripToTip"/>.
    /// </summary>
    private readonly Dictionary<uint, Matrix4x4> _gripToTip = [];

    /// <summary>
    /// The trigger. Its own object because registering for it is a several-step transaction with
    /// SteamVR that has to happen once and can fail without being a reason not to start.
    /// <para>
    /// <b>Registering is the application's call, not this class's, and that is a correction.</b>
    /// <see cref="Start"/> used to do it, which meant anything that brought a session up did — and
    /// the test suite brings one up to check that attaching can be retried. So <c>dotnet test</c>
    /// registered the <c>com.dseelinger.d47</c> application key against a path under
    /// <c>tests/…/bin/Debug</c>, on the developer's own SteamVR, and SteamVR keeps the first
    /// manifest it is given for a key and skips every later one — logging
    /// "was already described in manifest … Skipping" and nothing else. The installed d47 then ran
    /// on bindings loaded out of a test's output folder, which works right up until that folder is
    /// cleaned or rebuilt and then stops, silently, with the grab dead again.
    /// </para>
    /// <para>
    /// Whoever owns the process decides. <c>D47.App</c> registers; a test does not.
    /// </para>
    /// </summary>
    public VrActionInput Actions { get; } = new(logger);

    /// <summary>
    /// The aim beam and the cursor: two more overlays, and they have to be overlays rather than
    /// pixels drawn into the panel. The beam moves with the hand at headset rate while the panel
    /// repaints a few times a second, so compositing it in would drag the panel's pixels along at
    /// 90 Hz — and the cursor has to be a real 3D object or the beam has nothing to stop on.
    /// </summary>
    private VrOverlay? _beam;

    private VrOverlay? _cursor;

    private float _beamLength = float.NaN;

    private CVRSystem? _system;
    private bool _claimed;

    /// <summary>The last head pose read. Null before the first serve.</summary>
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
            // The runtime path resolved but the library did not load or did not have what the
            // binding expects — a version disagreement between the vendored header and the
            // installed runtime. Absent rather than broken: there is nothing the Commander can
            // do about it from inside d47, and a crash here would take the desktop panel with it.
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

        foreach (var overlay in _overlays.Values)
        {
            overlay.Dispose();
        }

        _overlays.Clear();

        // A rebuilt session is a new session, and it has to be able to say so — otherwise the
        // one that recovered looks exactly like the one that never reported anything.
        _complaints.Clear();
        _served.Clear();
        _described.Clear();

        if (_system is not null)
        {
            // Before the session goes, so a claim standing at the moment the overlay was
            // switched off is given back rather than left for SteamVR to notice.
            Actions.Release();
            OpenVR.Shutdown();
            _system = null;
        }

        Release();
    }

    /// <summary>The aim beam, if the runtime allowed one. Null leaves the pointing unguided.</summary>
    public VrOverlay? Beam => _beam;

    /// <summary>The cursor sprite, if the runtime allowed one.</summary>
    public VrOverlay? Cursor => _cursor;

    /// <summary>
    /// Points the beam along a hand and stops it at <paramref name="lengthMetres"/>, or takes it
    /// off screen when nothing is being aimed at.
    /// <para>
    /// The width is what sets the length — the quad's height follows its width by the texture's
    /// aspect — so it is re-sent only when the length actually changes. The pixels are uploaded
    /// once, at creation, and never again: re-handing a texture to SteamVR at frame rate is what
    /// makes an overlay flicker, and a transform is cheap.
    /// </para>
    /// </summary>
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
    /// Every tracked controller that is genuinely reporting a pose, with the aim already corrected
    /// off the grip. Both flags are checked in <see cref="VrMatrix.Real"/>, and there is no later
    /// layer that would catch a slot that is merely zeroed.
    /// </summary>
    public IReadOnlyList<VrHand> Controllers()
    {
        if (_system is null)
        {
            return [];
        }

        var poses = new TrackedDevicePose_t[OpenVR.k_unMaxTrackedDeviceCount];

        // No prediction. A panel is furniture rather than a thing being aimed, and asking where a
        // device will be by the time photons land buys nothing but jitter when the answer decides
        // whether somebody grabbed something.
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

            if (VrMatrix.Real(poses[device]) is { } grip)
            {
                var aim = VrPose.FromMatrix(GripToTip(device) * grip.ToMatrix());
                found.Add(new VrHand(device, grip, aim));
            }
        }

        return found;
    }

    /// <summary>
    /// The correction from the grip pose OpenVR reports to the tip the Commander aims with.
    /// <para>
    /// The grip pose sits inside the handle and runs roughly along it; on Touch controllers that is
    /// off from the apparent pointing direction by a large angle — enough to put a ray visibly away
    /// from where the laser looks like it goes, so a grab misses a panel that is plainly being
    /// pointed at. Read out of the render model rather than hardcoded, so it is right for whichever
    /// controller is actually connected.
    /// </para>
    /// <para>
    /// <c>tip</c> before <c>openxr_aim</c>: aim is a near neighbour rather than the same pose and
    /// leaves a small residual offset. No render models, or no such component, degrades to
    /// grip-as-aim — wrong in the obvious way rather than a refusal to start.
    /// </para>
    /// <para>
    /// Cached per device. The component transform is a property of the model rather than of the
    /// frame, so this is a marshalled string property and two interop calls that would otherwise
    /// run for every controller on every tick.
    /// </para>
    /// </summary>
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

    private string? ModelName(uint device)
    {
        var error = ETrackedPropertyError.TrackedProp_Success;
        var text = new System.Text.StringBuilder((int)OpenVR.k_unMaxPropertyStringSize);

        _system!.GetStringTrackedDeviceProperty(
            device,
            ETrackedDeviceProperty.Prop_RenderModelName_String,
            text,
            OpenVR.k_unMaxPropertyStringSize,
            ref error);

        return error == ETrackedPropertyError.TrackedProp_Success && text.Length > 0
            ? text.ToString()
            : null;
    }

    private VrStart Bring()
    {
        // Asked before VR_Init, and this is the whole point of asking: VR_Init *starts SteamVR*
        // if it is not already running. An overlay companion attaching to a session the
        // Commander opened is one thing; one that launches SteamVR on a machine whose headset is
        // switched off is quite another - it takes over the desktop, fails to find a headset,
        // and on a retry loop does it again every few seconds until SteamVR gives up with a
        // critical error. Neither check touches the compositor and neither one starts anything.
        //
        // "Order agnostic" means d47 tolerates SteamVR arriving later, not that d47 is what
        // makes it arrive.
        // Released on the way out, like every other path that leaves without a session: Start
        // claims the one-session slot before calling this, and a return that skipped the release
        // would make the next retry report that a session is already running.
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
            // Not discriminated by code, deliberately. For an overlay application there is no
            // "give up" case — SteamVR not running is the normal startup state, and a headset
            // that is installed but asleep and one that is unplugged both mean "ask again".
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

        // The keys are claimed before anything expensive is built. Another copy of d47 owns
        // them for as long as it lives, so a retry that fails on a key fails identically every
        // five seconds — and doing it after creating a graphics device is a device created and
        // destroyed twice a minute to learn something we could have asked first.
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

        // Both fail soft. No beam and no cursor is a panel that can still be pointed at and
        // carried, just without anything on screen saying where — a downgrade, not a failure.
        _beam = Sprite("com.dseelinger.D47.beam", "D47 aim", VrSprites.Beam(),
            VrAim.BeamPixelsWide, VrAim.BeamPixelsTall, VrAim.BeamWidthFor(1f), sortOrder: 1);

        _cursor = Sprite("com.dseelinger.D47.cursor", "D47 cursor", VrSprites.Cursor(),
            VrSprites.CursorSize, VrSprites.CursorSize, VrAim.CursorSizeMetres, sortOrder: 2);

        logger.LogInformation("Headset overlays are up; {Count} quad(s) claimed", _overlays.Count);

        return VrStart.Started;
    }

    /// <summary>
    /// One of the two static quads: created, given its pixels once, sized, and left. Null if the
    /// runtime refuses any of it.
    /// </summary>
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
            // SetOverlayRaw copies before returning, which is what makes handing it a bare address
            // safe — but only for the duration of the call, hence the pin.
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

    /// <summary>
    /// Watches for SteamVR going away on purpose. Without this the first sign is a refused
    /// call somewhere else, which recovers the same way but a good deal less politely.
    /// </summary>
    private bool PumpSystem()
    {
        var next = new VREvent_t();
        var size = (uint)Marshal.SizeOf<VREvent_t>();

        while (_system!.PollNextEvent(ref next, size))
        {
            if ((EVREventType)next.eventType == EVREventType.VREvent_Quit)
            {
                // Acknowledged so SteamVR stops waiting on us before it exits. It is going
                // either way; the difference is whether it has to time us out first.
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

    /// <summary>
    /// Says what the runtime turned down, once per distinct complaint.
    /// <para>
    /// These calls happen ten times a second, so an unfiltered log would be the same line
    /// thousands of times and unreadable exactly when it is needed. Every one of them used to
    /// be discarded entirely, which is worse: an overlay refusing its texture and an overlay
    /// nobody asked to show look identical from inside a headset, and neither leaves a trace
    /// outside one.
    /// </para>
    /// </summary>
    private void Refused(string what)
    {
        if (_complaints.Add(what))
        {
            logger.LogWarning("SteamVR refused an overlay call. {What}", what);
        }
    }

    /// <summary>
    /// Says that a surface the compositor was turning down is going through again.
    /// <para>
    /// <b>The recovery is the half that was missing.</b> <see cref="_complaints"/> is add-only, so
    /// a refusal was said once for the life of the process and then never again — which meant a
    /// log could say frames were refused and never say whether that lasted a second or the whole
    /// session, and a second run an hour later left no trace at all. Forgetting the complaint here
    /// is what lets the next run be reported as its own event.
    /// </para>
    /// </summary>
    private void Recovered(VrSurface surface)
    {
        logger.LogInformation("{Surface}: SteamVR is taking frames again", surface);

        // Only the one that stalled. Every complaint carries the overlay key it came from, so
        // clearing by prefix cannot forget a different quad's unrelated grievance.
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

        // A held frame lives in the buffer it was drawn into, so a reallocation throws it away
        // along with the pixels. Nothing is lost: a surface whose size changed is a surface whose
        // mode changed, and every path that changes the mode marks it dirty.
        if (reallocated)
        {
            held = default;
        }

        // Asked only when something is actually waiting to go again. IsOverlayVisible is a call
        // into the runtime, and the ordinary frame — nothing held, nothing refused — has no use
        // for the answer.
        var drawnByTheRuntime = held.Pending && overlay.Visible;

        // Whether a refused frame is retried, and whether it is retried now, is decided in Core
        // where a test can drive a session's worth of them without a headset. See FrameDelivery
        // for why the retry re-sends rather than re-draws, and why it waits for the quad to be
        // visible (remediation.md 16, item 1).
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
                // Onto the next buffer of the ring, so the one the runtime was just handed is not
                // the one the next frame is drawn into. See VrPixels.InFlight for why that
                // matters — and note that a refused frame does not rotate, because the runtime
                // never took it and it is about to be sent again.
                pixels.Rotate();
            }

            held = outcome.Held;
        }

        _frames[source.Surface] = held;

        // Head-locked rides the headset; only something put down in the room needs a room
        // position. Splitting them is what keeps the tracking universe out of the common case.
        var where = placement.Where(Head ?? VrPose.Origin);

        if (placement.RidesTheHead)
        {
            overlay.PlaceOnHead(placement.AgainstTheHead());
        }
        else
        {
            overlay.PlaceAbsolute(where);
        }
        overlay.Look(placement.WidthMetres, placement.Curvature, placement.Opacity);
        overlay.Show(true);
        overlay.PumpEvents();

        // Read back from the runtime every few seconds while the session is up, rather than
        // once. A single sample said the quad was placed a metre in front of a tracking head,
        // which was true and did not help: the panel was still invisible, and three theories
        // reasoning from what d47 sent were all wrong. This is SteamVR's own account of what it
        // is holding, including whether it thinks the overlay is visible at all.
        // On change, and otherwise on a slow heartbeat. What is worth reading is the moment the
        // runtime starts saying something different - visible going false, a width the Commander
        // did not set, a transform that stopped tracking - and that is invisible in a wall of
        // identical lines. When that happens is decided in Core, where a test can drive a
        // session's worth of frames without a headset; asking the runtime and writing the line
        // stay here.
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

        // Once per surface per session. "The overlays are up" says the quads were created, not
        // that anything was ever put in one or that it went anywhere a Commander could look —
        // and when the answer is "I see nothing at all", those are the only two questions left.
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

    /// <summary>
    /// Whether a SteamVR session already exists to attach to. Asked of the process list rather
    /// than of OpenVR, because every OpenVR call that would answer it authoritatively is one
    /// that starts the thing being asked about.
    /// <para>
    /// <c>vrserver</c> rather than <c>vrmonitor</c>: the monitor window is the visible half and
    /// can be closed while the session lives, so it answers a slightly different question.
    /// </para>
    /// </summary>
    private static bool SteamVrIsRunning()
    {
        try
        {
            return System.Diagnostics.Process.GetProcessesByName("vrserver").Length > 0;
        }
        catch (Exception ex) when (ex is InvalidOperationException or SystemException)
        {
            // Cannot enumerate processes. Answering "no" keeps the safe behaviour - waiting -
            // rather than starting SteamVR on a guess.
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
