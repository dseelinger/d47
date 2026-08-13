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
    /// The pixel size it wants. A change reallocates the textures, which is why it is asked
    /// rather than fixed: mini is a smaller image, not the same image hung nearer.
    /// </summary>
    (int Width, int Height) Size { get; }

    /// <summary>
    /// Whether anything has changed since the last draw. D1's second Phase 9 instruction: the
    /// panel is view-model-driven, so the measured 4-10 Hz cost is a worst case rather than a
    /// target, and a surface with nothing new costs one boolean.
    /// </summary>
    bool IsDirty { get; }

    /// <summary>Rasterises into the mapped staging texture, straight, with no intermediate copy.</summary>
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
            [VrSurface.PanelFull] = ("com.dseelinger.d47.panel", "d47"),
            [VrSurface.PanelMini] = ("com.dseelinger.d47.panel", "d47"),
            [VrSurface.Captions] = ("com.dseelinger.d47.captions", "d47 captions"),
        };

    /// <summary>
    /// One process, one session. Nothing in OpenVR refuses a second <c>VR_Init</c> and a
    /// repeated one leaks, so the refusal has to live here — and process-wide is the right
    /// scope, because the leak is.
    /// </summary>
    private static int _sessionClaimed;

    private readonly Dictionary<string, VrOverlay> _overlays = new(StringComparer.Ordinal);
    private readonly Dictionary<VrSurface, VrTexture> _textures = [];

    private CVRSystem? _system;
    private VrDevice? _device;
    private bool _claimed;

    /// <summary>The last head pose read. Null before the first serve.</summary>
    public VrPose? Head { get; private set; }

    /// <summary>Which adapter the device landed on, for the diagnostics page.</summary>
    public string? Adapter => _device?.AdapterName;

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
        if (_system is null || _device is null)
        {
            return false;
        }

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
        foreach (var texture in _textures.Values)
        {
            texture.Dispose();
        }

        _textures.Clear();

        foreach (var overlay in _overlays.Values)
        {
            overlay.Dispose();
        }

        _overlays.Clear();

        _device?.Dispose();
        _device = null;

        if (_system is not null)
        {
            OpenVR.Shutdown();
            _system = null;
        }

        Release();
    }

    /// <summary>The overlay a surface is drawn on, for the placement code to point rays at.</summary>
    public VrOverlay? OverlayFor(VrSurface surface) =>
        Keys.TryGetValue(surface, out var key) && _overlays.TryGetValue(key.Key, out var overlay)
            ? overlay
            : null;

    /// <summary>
    /// Every tracked controller that is genuinely reporting a pose. Both flags are checked in
    /// <see cref="VrMatrix.Real"/>, and there is no later layer that would catch a slot that
    /// is merely zeroed.
    /// </summary>
    public IReadOnlyList<(uint Device, VrPose Pose)> Controllers()
    {
        if (_system is null)
        {
            return [];
        }

        var poses = new TrackedDevicePose_t[OpenVR.k_unMaxTrackedDeviceCount];
        _system.GetDeviceToAbsoluteTrackingPose(
            ETrackingUniverseOrigin.TrackingUniverseSeated,
            0,
            poses);

        var found = new List<(uint, VrPose)>(2);

        for (uint device = 0; device < poses.Length; device++)
        {
            if (_system.GetTrackedDeviceClass(device) != ETrackedDeviceClass.Controller)
            {
                continue;
            }

            if (VrMatrix.Real(poses[device]) is { } pose)
            {
                found.Add((device, pose));
            }
        }

        return found;
    }

    private VrStart Bring()
    {
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

            var overlay = VrOverlay.Create(key, name, out var failure);

            if (overlay is null)
            {
                Stop();
                return failure;
            }

            _overlays[key] = overlay;
        }

        var wanted = -1;
        _system.GetDXGIOutputInfo(ref wanted);
        _device = VrDevice.Create(wanted);

        logger.LogInformation(
            "Headset overlays are up on {Adapter} (SteamVR asked for DXGI adapter {Wanted}, device landed on {Landed})",
            _device.AdapterName,
            wanted,
            _device.AdapterIndex);

        return VrStart.Started;
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

        if (!_textures.TryGetValue(source.Surface, out var texture))
        {
            texture = new VrTexture(_device!, width, height);
            _textures[source.Surface] = texture;
        }
        else
        {
            texture.Resize(width, height);
        }

        if (source.IsDirty)
        {
            var (address, rowBytes) = texture.Map();
            source.Draw(address, rowBytes);
            texture.Commit();
            overlay.Submit(texture.NativePointer);
        }

        overlay.PlaceAbsolute(placement.Where(Head ?? VrPose.Origin));
        overlay.Look(placement.WidthMetres, placement.Curvature, placement.Opacity);
        overlay.Show(true);
        overlay.PumpEvents();

        return true;
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
