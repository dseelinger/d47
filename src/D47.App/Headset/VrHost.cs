using System.Numerics;
using Avalonia.Controls;
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
/// Wires the headset into the running app: the surfaces, the runtime, the state machine, and the one
/// tick that drives all three.
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
    private readonly D47.Core.AppPaths _paths;

    private int _pending;
    private bool _disposed;

    /// <summary>
    /// Where and when the trigger went down, while it is still undecided whether this is a press or a
    /// carry.
    /// </summary>
    private Press? _pressed;

    /// <summary>Whether the press that is running took hold of a scrollbar rather than the panel.</summary>
    private bool _scrolling;

    private readonly record struct Press(DateTimeOffset At, float U, float V);

    /// <summary>The frozen offset between the hand and the panel, while the panel is being carried.</summary>
    /// <param name="Carrying">
    /// The grab a hand is holding the panel by, and the device holding it, or null when nobody is
    /// (#30).
    /// </param>
    private sealed record AimGeometry(
        VrPose Resting,
        VrExtent Extent,
        float Curvature,
        (Matrix4x4 Grab, uint Device, VrSurface Surface)? Carrying = null);

    private AimGeometry? _aimGeometry;

    /// <summary>The last hands the aim loop read, for the serve to act on.</summary>
    private IReadOnlyList<VrHand> _aimHands = [];

    private Matrix4x4? _carrying;

    private uint _carryingHand;

    /// <summary>The last time a tick supplied.</summary>
    private DateTimeOffset _now = DateTimeOffset.MinValue;

    private VrHost(
        SettingsService settings,
        ViewStateStore viewState,
        VrPanelSurface panel,
        VrCaptionSurface captions,
        CaptionLayer layer,
        SteamVrRuntime runtime,
        VrLifecycle lifecycle,
        D47.Core.AppPaths paths,
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
        _paths = paths;
        _logger = logger;
    }

    public VrState State => _lifecycle.State;

    public string? Reason => _lifecycle.Reason;

    /// <summary>
    /// The headset panel's own prompts, so a spoken value can reach a chooser the Commander opened in
    /// the headset rather than only one they opened in the window (Phase 25).
    /// </summary>
    public Panel.PanelPrompts Prompts => _panel.Prompts;

    /// <summary>Where the headset panel is, so a spoken phrase can move it (Phase 25).</summary>
    public D47.Core.Interface.PanelNavigator Nav => _panel.Nav;

    /// <summary>How far down the headset panel is, so a spoken scroll can move it (#34).</summary>
    public D47.Core.Interface.PanelScrollOutcome Scroll(D47.Core.Interface.PanelScrollStep step) =>
        _panel.Scroll(step);

    /// <summary>Builds the headset path and subscribes it to the tick loop.</summary>
    public static VrHost Start(
        PanelViewModel model,
        AudioArbiter audio,
        SettingsService settings,
        ViewStateStore viewState,
        TickLoop tick,
        D47.Core.AppPaths paths,
        ILoggerFactory loggers,
        D47.Core.Interface.AvatarLibrary? avatars = null,
        string? dumpTo = null,
        Func<Control>? settingsPage = null,
        D47.Core.Checklists.ChecklistService? checklists = null,
        D47.Core.Utilities.Timekeeper? timekeeper = null,
        D47.Core.Utilities.AlarmStore? alarmStore = null,
        D47.Core.Ships.ShipPlanService? ships = null,
        Func<D47.Core.Journal.CommanderGameState?>? gameState = null,
        D47.Core.Loadout.OnFootPlanService? onFoot = null,
        D47.Core.Engineers.EngineerPlanService? unlocks = null,
        D47.Core.Goals.GoalBook? goals = null,
        Action? backfillGoals = null,
        Panel.AdventureSurface? adventures = null)
    {
        VrHost? self = null;

        var panel = new VrPanelSurface(
            model, settings, slot => self?.AnchorFor(slot), avatars, dumpTo, settingsPage,
            checklists, timekeeper, alarmStore, ships, gameState, onFoot, unlocks, goals,
            backfillGoals, adventures, viewState);
        var layer = new CaptionLayer { Settings = settings.Current.Vr.Captions };
        var captions = new VrCaptionSurface(layer);

        var runtime = new SteamVrRuntime([panel, captions], loggers.CreateLogger<SteamVrRuntime>());
        var lifecycle = new VrLifecycle(runtime, loggers.CreateLogger<VrLifecycle>());

        var host = self = new VrHost(
            settings, viewState, panel, captions, layer, runtime, lifecycle, paths,
            loggers.CreateLogger<VrHost>());

        host.Configure();
        settings.Changed += _ => Dispatcher.UIThread.Post(host.Configure);

        // Captions are driven by what is audible rather than by what was generated, which is what keeps them
        // in step with a reply that got interrupted, superseded or dropped: the arbiter is the one place that
        // knows what is actually coming out of the speaker.
        audio.ActivityChanged += activity => Dispatcher.UIThread.Post(() => host.Heard(activity));
        audio.Silenced += () => Dispatcher.UIThread.Post(layer.Silence);

        tick.Add("vr", host.OnTick);
        return host;
    }

    /// <summary>Moves the panel that is on screen one or more steps (#199).</summary>
    public VrNudgeOutcome Nudge(VrNudge nudge, int steps)
    {
        var slot = _panel.Mode == PanelMode.Mini ? VrCapability.MiniSlot : VrCapability.PanelSlot;
        var head = _runtime.Head is { IsFinite: true } seen ? seen : (VrPose?)null;

        var anchor = _panel.Placement.Lock == SurfaceLock.WorldLocked
                     && _anchors.TryGetValue(slot, out var existing)
            ? existing
            : null;

        var down = anchor is not null;

        if (anchor is null)
        {
            if (RestingNow() is not { } rest)
            {
                return VrNudgeOutcome.NoHeadset;
            }

            anchor = Anchor(rest.Where, rest.Head);
        }

        _anchors[slot] = new SurfaceAnchor
        {
            Placed = PoseSettings.From(VrNudges.Apply(anchor.Placed.ToPose(), nudge, steps)),

            // Against the head as it is now, when there is one.
            PlacedAgainst = head is { } now ? PoseSettings.From(now) : anchor.PlacedAgainst,
        };

        Remember();
        _panel.Invalidate();

        if (!down)
        {
            var locked = _settings.Apply(VrCapability.LockKey(slot), "world", SettingsCaller.Hotkey);

            if (locked.Status != SettingApplyStatus.Applied)
            {
                // Said out loud for the same reason the carry says it: from inside a headset the panel moves
                // once and then springs back to the head, with nothing anywhere saying the setting refused.
                _logger.LogWarning(
                    "The panel was nudged but {Key} would not go to world: {Status} — {Detail}",
                    VrCapability.LockKey(slot),
                    locked.Status,
                    locked.Message);
            }
        }

        _logger.LogInformation(
            "The {Slot} panel was nudged {Nudge} by {Steps} step(s){Rested}",
            slot,
            nudge,
            VrNudges.Steps(steps),
            down ? string.Empty : ", having first been put down in front of the Commander");

        return down ? VrNudgeOutcome.Moved : VrNudgeOutcome.PutDown;
    }

    /// <summary>Where a surface was put down, if it has been.</summary>
    public (VrPose Placed, VrPose Against)? AnchorFor(string slot) =>
        _anchors.TryGetValue(slot, out var anchor)
            ? (anchor.Placed.ToPose(), anchor.PlacedAgainst.ToPose())
            : null;

    public void Dispose()
    {
        _disposed = true;

        // Stopped and joined before the runtime goes, because this thread makes OpenVR calls and a session
        // pulled out from under one of them is the shape of fault that takes vrclient down rather than
        // throwing something catchable.
        _aimLoop?.Dispose();
        _aimLoop = null;

        _lifecycle.Stop();
        _runtime.Stop();
        _captions.Dispose();
        _panel.Dispose();
    }

    /// <summary>Runs only while there is a session to place a ray in.</summary>
    private VrAimLoop? _aimLoop;

    /// <summary>What is audible, turned into captions.</summary>
    private void Heard(AudioActivity activity)
    {
        if (activity.Caption is { Length: > 0 } caption)
        {
            // With the clip's identity, so the same one reported again — which happens on every change to
            // anything else audible — is one caption and not two.
            _layer.Say(caption, _now, activity.Utterance);
            return;
        }

        if (activity.Channel is null)
        {
            _layer.Quiet(_now);
        }
    }

    private void Configure()
    {
        // Before the surfaces, because it decides whether there is a pointer at all (#198).
        _runtime.Pointing = _settings.Current.Vr.Controllers;

        _captions.Configure(_settings.Current.Vr.Captions);
        _panel.Configure();
        _panel.ApplyMode();
    }

    private void OnTick(TickContext context)
    {
        // Coalesced.
        if (Interlocked.Exchange(ref _pending, 1) == 1)
        {
            return;
        }

        Dispatcher.UIThread.Post(() =>
        {
            Interlocked.Exchange(ref _pending, 0);

            // Before the serve, so a clock that moved is in the frame this tick draws rather than in the next
            // one.
            _panel.TickClocks();
            _panel.TickEngineers();

            // And one frame of the "d47 is composing" animation, which is the third reason a headset panel
            // changes with the Commander having done nothing (asked for 2026-08-22).
            _panel.TickAdventures();

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
                if (_runtime.Pointing)
                {
                    // Here rather than inside the runtime: this is the real application, and only the real
                    // application may claim the d47 application key with SteamVR.
                    _runtime.Actions.Register(_paths.VrActions);
                }

                RestIfNeverPlaced();
                Carry();

                // Started after the first Carry rather than before it, so the loop's first frame has a
                // published geometry to aim at instead of returning empty-handed.
                if (_runtime.Pointing)
                {
                    _aimLoop ??= new VrAimLoop(Aim, _logger).Start();
                }
                else if (_aimLoop is { } running)
                {
                    _aimLoop = null;
                    running.Dispose();
                }
            }
        }
        catch (Exception ex)
        {
            // The runtime going away underneath is a fact about the machine rather than a defect of ours, and
            // it must not take the desktop panel down with it.
            _logger.LogError(ex, "The headset path threw; the session will be rebuilt");
            _lifecycle.Stop();
        }
    }

    /// <summary>
    /// Puts a world-locked panel somewhere the first time it is ever shown
    /// (docs/plans/change-requests.md item 9).
    /// </summary>
    private void RestIfNeverPlaced()
    {
        var slot = _panel.Mode == PanelMode.Mini ? VrCapability.MiniSlot : VrCapability.PanelSlot;

        if (_carrying is not null
            || _anchors.ContainsKey(slot)
            || _panel.Placement.Lock != SurfaceLock.WorldLocked)
        {
            return;
        }

        if (RestingNow() is not { } rest)
        {
            return;
        }

        _anchors[slot] = Anchor(rest.Where, rest.Head);
        Remember();

        _logger.LogInformation(
            "The {Slot} panel had never been placed; resting it {Distance:0.00} m ahead with its top at {Top:0.00} m",
            slot,
            _panel.Placement.DistanceMetres,
            rest.Where.Position.Y + (rest.QuadHeight / 2f));
    }

    /// <summary>
    /// Where the panel on screen would come to rest right now, and the head it would rest against.
    /// </summary>
    private (VrPose Where, VrPose Head, float QuadHeight)? RestingNow()
    {
        if (_runtime.Head is not { IsFinite: true } head)
        {
            return null;
        }

        var placement = _panel.Placement;
        var (width, height) = _panel.Size;

        // The quad's height is not settable — it follows from the texture's aspect off the configured width —
        // so the height that puts the top edge anywhere has to be computed from both.
        var quadHeight = placement.WidthMetres * height / width;

        var resting = VrPlacementMath.Resting(
            head,
            placement.DistanceMetres,
            VrPlacementMath.KneeHeight(head.Position.Y),
            quadHeight);

        return (resting, head, quadHeight);
    }

    /// <summary>Grab-to-move, and the guidance that makes it possible to aim.</summary>
    private void Carry()
    {
        // The withdrawal (#198).
        if (!_runtime.Pointing)
        {
            Withdraw();
            return;
        }

        // Asked of the surface rather than assumed here.
        if (!_panel.TakesPointer || _runtime.Head is not { } head)
        {
            // Withdrawn before the return, so the aim loop stops placing a ray against geometry this serve
            // has just declined to stand behind.
            Volatile.Write(ref _aimGeometry, null);

            // A claim made on the last frame that got past this line would otherwise stand for as long as
            // this line keeps returning — see VrActionInput.Release.
            _runtime.Actions.Release();
            return;
        }

        var slot = _panel.Mode == PanelMode.Mini ? VrCapability.MiniSlot : VrCapability.PanelSlot;
        var placement = _panel.Placement;
        var resting = placement.Where(head);
        var (width, height) = _panel.Size;
        var extent = new VrExtent(placement.WidthMetres, (float)width / Math.Max(1, height));

        // Published for the aim loop, which places the ray against it at frame rate (#19) and the panel too
        // while a hand is carrying it (#30).
        Volatile.Write(
            ref _aimGeometry,
            new AimGeometry(
                resting,
                extent,
                placement.Curvature,
                _carrying is { } grab ? (grab, _carryingHand, _panel.Surface) : null));

        // What the aim loop last saw, rather than a read of our own: the pose read has one owner.
        var hands = Volatile.Read(ref _aimHands);
        var found = VrRay.PointingAt(hands, resting, extent, placement.Curvature);

        // Claimed only while a ray is on the panel or a carry is already running — the second because a hand
        // can swing the panel far enough that its own ray leaves it, and dropping the claim there would drop
        // the panel mid-move.
        var held = _runtime.Actions.TriggerHeld(found is not null || _carrying is not null);

        // Back, on the grip (Phase 25).
        if (_runtime.Actions.BackPressed() && _panel.Back())
        {
            _panel.Invalidate();
        }

        // What the ray is resting on, lit so the Commander can see they have found it.
        _panel.Aim(found?.Hit.U, found?.Hit.V);

        if (!held)
        {
            if (_scrolling)
            {
                _scrolling = false;
                _panel.ReleaseScroll();
                _pressed = null;
                return;
            }

            if (_pressed is { } tap && _carrying is null)
            {
                // Let go without ever having moved or dwelt: a press, not a carry.
                _pressed = null;

                try
                {
                    if (_panel.Press(tap.U, tap.V))
                    {
                        _logger.LogDebug("The panel was pressed at {U:0.00}, {V:0.00}", tap.U, tap.V);
                    }
                }
                catch (Exception ex)
                {
                    // A press must never be able to take the app down.
                    _logger.LogError(ex, "A press on the panel threw at {U:0.00}, {V:0.00}", tap.U, tap.V);
                }
            }

            _pressed = null;

            if (_carrying is not null)
            {
                // Written once, on release.
                _carrying = null;
                Remember();
                _logger.LogDebug("The panel was put down");
            }

            return;
        }

        if (_carrying is null)
        {
            if (found is not { } start)
            {
                return;
            }

            // One button does both, so the gesture has to say which.
            if (_pressed is not { } pressed)
            {
                _pressed = new Press(_now, start.Hit.U, start.Hit.V);

                // A hand that came down on a scrollbar is scrolling, not carrying — decided at the moment of
                // the press and not revisited, so a drag that wanders off the bar keeps scrolling rather than
                // suddenly picking the panel up.
                _scrolling = _panel.GrabsScroll(start.Hit.U, start.Hit.V);
                return;
            }

            if (_scrolling)
            {
                _panel.Scroll(start.Hit.U, start.Hit.V);
                return;
            }

            if (!VrPress.BecomesACarry(_now - pressed.At, pressed.U, pressed.V, start.Hit.U, start.Hit.V))
            {
                return;
            }

            _pressed = null;

            _carrying = VrPlacementMath.Grab(start.Hand.Aim, resting);
            _carryingHand = start.Hand.Device;

            // Picking it up means putting it somewhere, so it becomes world-locked.
            var locked = _settings.Apply(VrCapability.LockKey(slot), "world", SettingsCaller.Hotkey);

            if (locked.Status != SettingApplyStatus.Applied)
            {
                // Said out loud, because the failure is otherwise invisible from inside a headset: the panel
                // is carried perfectly and springs back to the head on release, and nothing anywhere says the
                // setting refused to move.
                _logger.LogWarning(
                    "The panel was picked up but {Key} would not go to world: {Status} — {Detail}",
                    VrCapability.LockKey(slot),
                    locked.Status,
                    locked.Message);
            }

            _logger.LogDebug("The panel was picked up by device {Device}", start.Hand.Device);
            return;
        }

        // The hand that took it, and only that one.
        if (Holding(hands) is not { } carrier)
        {
            return;
        }

        _anchors[slot] = Anchor(VrPlacementMath.Carried(_carrying.Value, carrier.Aim), head);
    }

    /// <summary>
    /// Lets go of everything a pointer was holding, because there is no longer a pointer (#198).
    /// </summary>
    private void Withdraw()
    {
        // Withdrawn first, so the aim loop — if this is the tick that raced its shutdown — stops placing a
        // ray against geometry this serve has declined to stand behind.
        Volatile.Write(ref _aimGeometry, null);

        // A claim standing from before the row moved would otherwise stand until the session ended.
        _runtime.Actions.Release();

        if (_scrolling)
        {
            _scrolling = false;
            _panel.ReleaseScroll();
        }

        _pressed = null;

        if (_carrying is not null)
        {
            // Put down where it had got to, and written, exactly as letting go of the trigger does.
            _carrying = null;
            Remember();
            _logger.LogInformation("The motion controllers were withdrawn mid-carry; the panel is down where it was");
        }

        _panel.Aim(null, null);
    }

    /// <summary>One frame of the aim ray, from <see cref="VrAimLoop"/> and never from the tick (#19).</summary>
    private void Aim()
    {
        if (Volatile.Read(ref _aimGeometry) is not { } geometry)
        {
            return;
        }

        var (hands, head) = _runtime.HandsAndHead();

        // Published before the ray is placed, so the next tick's decision sees what this frame saw rather
        // than what the frame before it did.
        Volatile.Write(ref _aimHands, hands);

        if (head is not { } where)
        {
            return;
        }

        // A panel being carried follows the hand at frame rate rather than at the tick's ten (#30).
        if (geometry.Carrying is { } carry)
        {
            foreach (var hand in hands)
            {
                if (hand.Device == carry.Device)
                {
                    _runtime.Reposition(carry.Surface, VrPlacementMath.Carried(carry.Grab, hand.Aim));
                    break;
                }
            }
        }

        // Read fresh in the same call as the hands.
        Guide(
            hands,
            VrRay.PointingAt(hands, geometry.Resting, geometry.Extent, geometry.Curvature),
            geometry.Resting,
            geometry.Extent,
            where);
    }

    /// <summary>
    /// The beam and the cursor: where the Commander is pointing, drawn, because SteamVR is no longer
    /// drawing it for them.
    /// </summary>
    private void Guide(
        IReadOnlyList<VrHand> hands,
        (VrHand Hand, VrHit Hit)? found,
        VrPose resting,
        VrExtent extent,
        VrPose head)
    {
        VrPose? aim = null;
        Vector3? point = null;
        var length = VrAim.BeamMissLengthMetres;

        if (found is { } on)
        {
            aim = on.Hand.Aim;
            length = on.Hit.DistanceMetres;

            // One value feeds both, so the beam stops at exactly the point the cursor sits on and the two
            // meet whether or not the curvature model is right about where that point is.
            point = VrAim.PointAlong(on.Hand.Aim, on.Hit.DistanceMetres);
        }
        else if (_carrying is not null && Holding(hands) is { } carrier)
        {
            // Kept through a carry that has swung the panel off its own ray.
            aim = carrier.Aim;
        }
        else
        {
            var guide = extent with { WidthMetres = extent.WidthMetres * VrAim.BeamGuideScale };

            if (VrRay.PointingAt(hands, resting, guide, 0f) is { } near)
            {
                aim = near.Hand.Aim;
            }
        }

        _runtime.AimBeam(aim, head, length);
        _runtime.ShowCursor(point, head);
    }

    /// <summary>The hand currently carrying the panel, if it is still being tracked.</summary>
    private VrHand? Holding(IReadOnlyList<VrHand> hands)
    {
        foreach (var hand in hands)
        {
            if (hand.Device == _carryingHand)
            {
                return hand;
            }
        }

        return null;
    }

    private static SurfaceAnchor Anchor(VrPose placed, VrPose head) => new()
    {
        Placed = PoseSettings.From(placed),
        PlacedAgainst = PoseSettings.From(head),
    };

    /// <summary>
    /// Read-modify-write against the file rather than against a snapshot, because the settings window
    /// writes card collapse state into the same store while this is running.
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
}
