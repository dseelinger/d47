using System.Numerics;
using Avalonia.Threading;
using D47.App.Panel;
using D47.Core.Audio;
using D47.Core.Capabilities.Builtin;
using D47.Core.Configuration;
using D47.Core.Ticking;
using D47.Core.Vr;
using D47.Vr;
using Microsoft.Extensions.Logging;

namespace D47.App.Headset;

/// <summary>
/// Wires the headset into the running app: the surfaces, the runtime, the state machine, and
/// the one tick that drives all three.
/// <para>
/// <b>Everything here happens on the UI thread</b>, and the tick loop only posts to it. A
/// <c>Visual</c> is thread-affine, so rasterising the panel is the dispatcher's work whatever
/// else happens; the alternative is the tick thread blocking on a dispatcher hop every frame,
/// which is a deadlock waiting for the day the UI thread waits on something the tick loop
/// owns. Posting also keeps the tick loop's contract — a subscriber must not block, and
/// <see cref="Dispatcher.Post"/> does not (architecture.md §4).
/// </para>
/// </summary>
public sealed class VrHost : IDisposable
{
    private readonly SettingsService _settings;
    private readonly ViewStateStore _viewState;
    private readonly Dictionary<string, SurfaceAnchor> _anchors;
    private readonly VrLifecycle _lifecycle;
    private readonly SteamVrRuntime _runtime;
    private readonly VrPanelSurface _panel;
    private readonly VrCaptionSurface _captions;
    private readonly CaptionLayer _layer;
    private readonly ILogger<VrHost> _logger;

    private int _pending;
    private bool _disposed;

    /// <summary>
    /// The frozen offset between the hand and the panel, while the panel is being carried.
    /// Null the rest of the time, which is nearly always.
    /// </summary>
    private Matrix4x4? _carrying;

    private uint _carryingHand;

    /// <summary>
    /// The last time a tick supplied. Captions arrive on the audio thread, which has no
    /// business reading the clock and no business being the one to decide what time it is —
    /// Core's rule about injected time does not stop applying because the caller is ours.
    /// </summary>
    private DateTimeOffset _now = DateTimeOffset.MinValue;

    private VrHost(
        SettingsService settings,
        ViewStateStore viewState,
        VrPanelSurface panel,
        VrCaptionSurface captions,
        CaptionLayer layer,
        SteamVrRuntime runtime,
        VrLifecycle lifecycle,
        ILogger<VrHost> logger)
    {
        _settings = settings;
        _viewState = viewState;
        _anchors = new Dictionary<string, SurfaceAnchor>(viewState.Load().VrAnchors, StringComparer.Ordinal);
        _panel = panel;
        _captions = captions;
        _layer = layer;
        _runtime = runtime;
        _lifecycle = lifecycle;
        _logger = logger;
    }

    public VrState State => _lifecycle.State;

    public string? Reason => _lifecycle.Reason;

    /// <summary>
    /// Builds the headset path and subscribes it to the tick loop.
    /// <para>
    /// Constructed unconditionally, on every machine, whether or not there is a headset. An
    /// absent headset is a first-class state and not a branch: the alternative is a code path
    /// that only runs for people who have one, which is the code path that breaks.
    /// </para>
    /// </summary>
    public static VrHost Start(
        PanelViewModel model,
        AudioArbiter audio,
        SettingsService settings,
        ViewStateStore viewState,
        TickLoop tick,
        ILoggerFactory loggers,
        D47.Core.Interface.AvatarLibrary? avatars = null,
        string? dumpTo = null)
    {
        VrHost? self = null;

        var panel = new VrPanelSurface(model, settings, slot => self?.AnchorFor(slot), avatars, dumpTo);
        var layer = new CaptionLayer { Settings = settings.Current.Vr.Captions };
        var captions = new VrCaptionSurface(layer);

        var runtime = new SteamVrRuntime([panel, captions], loggers.CreateLogger<SteamVrRuntime>());
        var lifecycle = new VrLifecycle(runtime, loggers.CreateLogger<VrLifecycle>());

        var host = self = new VrHost(
            settings, viewState, panel, captions, layer, runtime, lifecycle, loggers.CreateLogger<VrHost>());

        host.Configure();
        settings.Changed += _ => Dispatcher.UIThread.Post(host.Configure);

        // Captions are driven by what is audible rather than by what was generated, which is
        // what keeps them in step with a reply that got interrupted, superseded or dropped:
        // the arbiter is the one place that knows what is actually coming out of the speaker.
        audio.ActivityChanged += activity => Dispatcher.UIThread.Post(() => host.Heard(activity));
        audio.Silenced += () => Dispatcher.UIThread.Post(layer.Silence);

        tick.Add("vr", host.OnTick);
        return host;
    }

    /// <summary>
    /// Snaps every world-locked surface back to the current head pose, as a group.
    /// <para>
    /// As a group is the whole point. Elite's recenter turns the cockpit, so putting the
    /// panels back means turning them all by the same amount — a per-surface "put it back
    /// where it started" stacks them in front of the Commander, which is a different feature
    /// and not one anybody asked for (list.md Phase 9).
    /// </para>
    /// <para>
    /// Returns how many moved, so "there is nothing to re-anchor" is a real answer rather than
    /// silence that looks like a failure.
    /// </para>
    /// </summary>
    public int Reanchor()
    {
        if (_runtime.Head is not { } head)
        {
            return 0;
        }

        var moved = 0;

        foreach (var slot in _anchors.Keys.ToArray())
        {
            var anchor = _anchors[slot];

            _anchors[slot] = Anchor(
                VrPlacementMath.Reanchored(anchor.Placed.ToPose(), anchor.PlacedAgainst.ToPose(), head),
                head);

            moved++;
        }

        if (moved > 0)
        {
            Remember();
            _panel.Invalidate();
            _logger.LogInformation("Re-anchored {Count} world-locked surface(s)", moved);
        }

        return moved;
    }

    /// <summary>Where a surface was put down, if it has been. Read by the panel each serve.</summary>
    public (VrPose Placed, VrPose Against)? AnchorFor(string slot) =>
        _anchors.TryGetValue(slot, out var anchor)
            ? (anchor.Placed.ToPose(), anchor.PlacedAgainst.ToPose())
            : null;

    public void Dispose()
    {
        _disposed = true;
        _lifecycle.Stop();
        _runtime.Stop();
        _captions.Dispose();
        _panel.Dispose();
    }

    /// <summary>
    /// What is audible, turned into captions. A clip carrying a caption starts one; nothing
    /// audible ends the last one and starts its dwell.
    /// </summary>
    private void Heard(AudioActivity activity)
    {
        if (activity.Caption is { Length: > 0 } caption)
        {
            _layer.Say(caption, _now);
            return;
        }

        if (activity.Channel is null)
        {
            _layer.Quiet(_now);
        }
    }

    private void Configure()
    {
        _captions.Configure(_settings.Current.Vr.Captions);
        _panel.Configure();
        _panel.ApplyMode();
    }

    private void OnTick(TickContext context)
    {
        // Coalesced. At 10 Hz a dispatcher that fell behind would otherwise accumulate a queue
        // of frames it will draw late and then draw all at once.
        if (Interlocked.Exchange(ref _pending, 1) == 1)
        {
            return;
        }

        Dispatcher.UIThread.Post(() =>
        {
            Interlocked.Exchange(ref _pending, 0);
            Serve(context.Now);
        });
    }

    private void Serve(DateTimeOffset now)
    {
        if (_disposed)
        {
            return;
        }

        var wanted = _settings.Current.Vr.Enabled;

        if (!wanted)
        {
            if (_lifecycle.State == VrState.Active)
            {
                _logger.LogInformation("The headset overlays are switched off; giving the session back");
                _lifecycle.Stop();
            }

            return;
        }

        _now = now;
        _panel.Enabled = true;
        _captions.Enabled = _settings.Current.Vr.Captions.Enabled;
        _layer.Tick(now);

        try
        {
            _lifecycle.Tick(now);

            if (_lifecycle.State == VrState.Active)
            {
                Carry();
            }
        }
        catch (Exception ex)
        {
            // The runtime going away underneath is a fact about the machine rather than a
            // defect of ours, and it must not take the desktop panel down with it. The state
            // machine's own recovery is a rebuild, so the next tick starts clean either way.
            _logger.LogError(ex, "The headset path threw; the session will be rebuilt");
            _lifecycle.Stop();
        }
    }

    /// <summary>
    /// Grab-to-move. The pointer is the only controller input an overlay application gets —
    /// SteamVR takes the controllers to drive its own laser and hands back mouse events — so
    /// this is the trigger, arriving as a button, over a quad the ray is on.
    /// </summary>
    private void Carry()
    {
        var overlay = _runtime.OverlayFor(_panel.Surface);

        if (overlay is null || _runtime.Head is not { } head)
        {
            return;
        }

        var slot = _panel.Mode == PanelMode.Mini ? VrCapability.MiniSlot : VrCapability.PanelSlot;

        if (!overlay.Pointer.Held)
        {
            if (_carrying is not null)
            {
                // Written once, on release. A drag is thirty poses a second and the settings
                // store writes a whole file atomically; persisting each one would be a
                // hundred file writes to record one gesture.
                _carrying = null;
                Remember();
                _logger.LogDebug("The panel was put down");
            }

            return;
        }

        // Which hand is doing it is not on the event: trackedDeviceIndex on an overlay mouse
        // event has been measured as the invalid index against two tracked controllers. So it
        // is recovered by casting each hand's ray at the quad and taking the nearest hit,
        // which only has to tell one hand from the other.
        if (Pointing(overlay) is not { } pointing)
        {
            return;
        }

        if (_carrying is null)
        {
            _carrying = VrPlacementMath.Grab(pointing.Pose, _panel.Placement.Where(head));
            _carryingHand = pointing.Device;

            // Picking it up means putting it somewhere, so it becomes world-locked. The
            // setting follows the action rather than gating it: a Commander who has physically
            // carried the panel across the cockpit has said where they want it, and a
            // head-locked surface that sprang back would be d47 arguing with them.
            _settings.Apply(VrCapability.LockKey(slot), "world", SettingsCaller.Hotkey);
            return;
        }

        // A second hand pressing mid-carry does not steal the panel, and the carrying hand
        // losing tracking drops it where it was rather than following a pose nobody has.
        if (_carryingHand != pointing.Device)
        {
            return;
        }

        _anchors[slot] = Anchor(VrPlacementMath.Carried(_carrying.Value, pointing.Pose), head);
        _panel.Invalidate();
    }

    private static SurfaceAnchor Anchor(VrPose placed, VrPose head) => new()
    {
        Placed = PoseSettings.From(placed),
        PlacedAgainst = PoseSettings.From(head),
    };

    /// <summary>
    /// Read-modify-write against the file rather than against a snapshot, because the settings
    /// window writes card collapse state into the same store while this is running.
    /// </summary>
    private void Remember()
    {
        var state = _viewState.Load();

        foreach (var (slot, anchor) in _anchors)
        {
            state = state.With(slot, anchor);
        }

        _viewState.Save(state);
    }

    private (uint Device, VrPose Pose)? Pointing(VrOverlay overlay)
    {
        (uint Device, VrPose Pose)? nearest = null;
        var closest = float.MaxValue;

        foreach (var (device, pose) in _runtime.Controllers())
        {
            // A controller points along its own -Z, which is OpenVR's convention and not ours.
            var along = Vector3.Transform(-Vector3.UnitZ, pose.Facing);

            if (overlay.IntersectedBy(pose.Position, along) is { } distance && distance < closest)
            {
                closest = distance;
                nearest = (device, pose);
            }
        }

        return nearest;
    }
}
