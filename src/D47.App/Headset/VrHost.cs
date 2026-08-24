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
    private readonly D47.Core.AppPaths _paths;

    private int _pending;
    private bool _disposed;

    /// <summary>
    /// Where and when the trigger went down, while it is still undecided whether this is a press
    /// or a carry. Null when nothing is held or the carry has already begun.
    /// </summary>
    private Press? _pressed;

    /// <summary>Whether the press that is running took hold of a scrollbar rather than the panel.</summary>
    private bool _scrolling;

    private readonly record struct Press(DateTimeOffset At, float U, float V);

    /// <summary>
    /// The frozen offset between the hand and the panel, while the panel is being carried.
    /// Null the rest of the time, which is nearly always.
    /// </summary>
    /// <summary>
    /// What the aim loop needs to place a ray, published by the 10 Hz serve
    /// (<a href="https://github.com/dseelinger/d47/issues/19">#19</a>).
    /// <para>
    /// The geometry is a function of the placement, the panel's pixel size and the head, all of
    /// which are read through <see cref="_panel"/> and settings. Publishing it rather than letting
    /// the aim thread go and get it is what keeps everything crossing threads to one immutable
    /// record — the alternative is a second thread reading settings and view state while the tick
    /// writes them.
    /// </para>
    /// <para>
    /// A geometry up to a tenth of a second old is the right trade: it moves when the panel is
    /// re-anchored or resized, which is rare, while the <em>hand</em> — the thing that made the ray
    /// look broken — is read fresh every frame.
    /// </para>
    /// </summary>
    /// <param name="Carrying">
    /// The grab a hand is holding the panel by, and the device holding it, or null when nobody is
    /// (<a href="https://github.com/dseelinger/d47/issues/30">#30</a>). Published so the loop can
    /// place a carried panel at frame rate — the tick still decides <em>whether</em> a carry is
    /// running, which hand owns it, and when it ends. Only the placing moved, which is the same
    /// rule the ray went by.
    /// </param>
    private sealed record AimGeometry(
        VrPose Resting,
        VrExtent Extent,
        float Curvature,
        (Matrix4x4 Grab, uint Device, VrSurface Surface)? Carrying = null);

    private AimGeometry? _aimGeometry;

    /// <summary>
    /// The last hands the aim loop read, for the serve to act on.
    /// <para>
    /// <b>The pose read has one owner.</b> <c>SteamVrRuntime.HandsAndHead</c> walks every device
    /// through <c>Note</c>, which keeps a plain dictionary of what each was last seen doing — two
    /// threads calling it is a corrupted dictionary and a race no test run would show. So the loop
    /// reads and the serve consumes what it published.
    /// </para>
    /// </summary>
    private IReadOnlyList<VrHand> _aimHands = [];

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
    /// The headset panel's own prompts, so a spoken value can reach a chooser the Commander
    /// opened in the headset rather than only one they opened in the window (list.md Phase 25).
    /// </summary>
    public Panel.PanelPrompts Prompts => _panel.Prompts;

    /// <summary>Where the headset panel is, so a spoken phrase can move it (list.md Phase 25).</summary>
    public D47.Core.Interface.PanelNavigator Nav => _panel.Nav;

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
            backfillGoals, adventures);
        var layer = new CaptionLayer { Settings = settings.Current.Vr.Captions };
        var captions = new VrCaptionSurface(layer);

        var runtime = new SteamVrRuntime([panel, captions], loggers.CreateLogger<SteamVrRuntime>());
        var lifecycle = new VrLifecycle(runtime, loggers.CreateLogger<VrLifecycle>());

        var host = self = new VrHost(
            settings, viewState, panel, captions, layer, runtime, lifecycle, paths,
            loggers.CreateLogger<VrHost>());

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

        // Stopped and joined before the runtime goes, because this thread makes OpenVR calls and a
        // session pulled out from under one of them is the shape of fault that takes vrclient down
        // rather than throwing something catchable.
        _aimLoop?.Dispose();
        _aimLoop = null;

        _lifecycle.Stop();
        _runtime.Stop();
        _captions.Dispose();
        _panel.Dispose();
    }

    /// <summary>
    /// Runs only while there is a session to place a ray in. Started on the first active tick and
    /// stopped with the host: there is nothing to point at when no headset is running, and a loop
    /// asking a dead runtime for poses ninety times a second is work nobody asked for.
    /// </summary>
    private VrAimLoop? _aimLoop;

    /// <summary>
    /// What is audible, turned into captions. A clip carrying a caption starts one; nothing
    /// audible ends the last one and starts its dwell.
    /// </summary>
    private void Heard(AudioActivity activity)
    {
        if (activity.Caption is { Length: > 0 } caption)
        {
            // With the clip's identity, so the same one reported again — which happens on every
            // change to anything else audible — is one caption and not two.
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

            // Before the serve, so a clock that moved is in the frame this tick draws rather than
            // in the next one. It marks the surface dirty only when Utilities is what is showing,
            // which is what keeps a ticking clock from re-rendering a transcript nobody moved
            // (list.md Phase 24).
            _panel.TickClocks();
            _panel.TickEngineers();

            // And one frame of the "d47 is composing" animation, which is the third reason a
            // headset panel changes with the Commander having done nothing (asked for
            // 2026-08-22). Same route, same 10 Hz, same rule about only marking dirty when
            // something moved.
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
                // Here rather than inside the runtime: this is the real application, and only the
                // real application may claim the d47 application key with SteamVR. Idempotent, so
                // every tick after the first costs a comparison, and a rebuilt session re-registers
                // itself without anything having to notice that it was rebuilt.
                _runtime.Actions.Register(_paths.VrActions);

                RestIfNeverPlaced();
                Carry();

                // Started after the first Carry rather than before it, so the loop's first frame
                // has a published geometry to aim at instead of returning empty-handed.
                _aimLoop ??= new VrAimLoop(Aim, _logger).Start();
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
    /// Puts a world-locked panel somewhere the first time it is ever shown
    /// (docs/plans/change-requests.md item 9).
    /// <para>
    /// <b>Without this the world-lock default is invisible.</b> <c>RidesTheHead</c> is true
    /// whenever <c>Placed</c> is null, whatever the lock says, and until now the only thing that
    /// ever set <c>Placed</c> was a completed carry — so a Commander who had never grabbed the
    /// panel would have seen a head-locked panel and a setting claiming otherwise.
    /// </para>
    /// <para>
    /// Once, and then it is theirs. It writes an anchor exactly as putting the panel down does,
    /// so the next launch finds one and this does nothing — and a Commander who moves it is
    /// never argued with.
    /// </para>
    /// <para>
    /// Needs a real head pose. An all-zero pose converts to a valid-looking unit quaternion at
    /// the origin, so a dropped tracking frame here would anchor the panel at the floor of the
    /// play space and persist it — which is why <c>Head</c> is null rather than defaulted when
    /// the pose is not valid, and why this waits rather than guessing.
    /// </para>
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

        if (_runtime.Head is not { IsFinite: true } head)
        {
            return;
        }

        var placement = _panel.Placement;
        var (width, height) = _panel.Size;

        // The quad's height is not settable — it follows from the texture's aspect off the
        // configured width — so the height that puts the top edge anywhere has to be computed
        // from both. Hand-tuning a drop would break the moment the width row was touched.
        var quadHeight = placement.WidthMetres * height / width;

        var resting = VrPlacementMath.Resting(
            head,
            placement.DistanceMetres,
            VrPlacementMath.KneeHeight(head.Position.Y),
            quadHeight);

        _anchors[slot] = Anchor(resting, head);
        Remember();

        _logger.LogInformation(
            "The {Slot} panel had never been placed; resting it {Distance:0.00} m ahead with its top at {Top:0.00} m",
            slot,
            placement.DistanceMetres,
            resting.Position.Y + (quadHeight / 2f));
    }

    /// <summary>
    /// Grab-to-move, and the guidance that makes it possible to aim.
    /// <para>
    /// <b>The ray is cast before the button is read, and that inversion is the whole fix.</b>
    /// Whether a ray is on the panel is what decides whether d47 claims the controllers at all —
    /// see <see cref="VrActionInput.TriggerHeld"/> — so position and claim come out of the same
    /// intersection, one frame, one source of truth. This used to be handed a <c>Held</c> flag
    /// decoded from overlay mouse events, which carry nothing while a game holds the headset.
    /// </para>
    /// <para>
    /// Which hand is holding it is resolved <em>only</em> at the moment of the press. Mid-carry the
    /// holder is whoever already holds it: re-deriving it every frame from the nearer ray would let
    /// a flicker in the curvature model hand the panel to the other hand, or drop it.
    /// </para>
    /// </summary>
    private void Carry()
    {
        // Asked of the surface rather than assumed here. Captions are read rather than touched,
        // and a quad that answers a ray in front of the cockpit is a beam that stops on a label.
        if (!_panel.TakesPointer || _runtime.Head is not { } head)
        {
            // Withdrawn before the return, so the aim loop stops placing a ray against geometry
            // this serve has just declined to stand behind.
            Volatile.Write(ref _aimGeometry, null);

            // A claim made on the last frame that got past this line would otherwise stand for
            // as long as this line keeps returning — see VrActionInput.Release.
            _runtime.Actions.Release();
            return;
        }

        var slot = _panel.Mode == PanelMode.Mini ? VrCapability.MiniSlot : VrCapability.PanelSlot;
        var placement = _panel.Placement;
        var resting = placement.Where(head);
        var (width, height) = _panel.Size;
        var extent = new VrExtent(placement.WidthMetres, (float)width / Math.Max(1, height));

        // Published for the aim loop, which places the ray against it at frame rate (#19) and the
        // panel too while a hand is carrying it (#30).
        Volatile.Write(
            ref _aimGeometry,
            new AimGeometry(
                resting,
                extent,
                placement.Curvature,
                _carrying is { } grab ? (grab, _carryingHand, _panel.Surface) : null));

        // What the aim loop last saw, rather than a read of our own: the pose read has one owner.
        // A sample up to one aim frame old is nothing beside the tick this decision runs on.
        var hands = Volatile.Read(ref _aimHands);
        var found = VrRay.PointingAt(hands, resting, extent, placement.Curvature);

        // Claimed only while a ray is on the panel or a carry is already running — the second
        // because a hand can swing the panel far enough that its own ray leaves it, and dropping
        // the claim there would drop the panel mid-move.
        var held = _runtime.Actions.TriggerHeld(found is not null || _carrying is not null);

        // Back, on the grip (list.md Phase 25). One of the three routes that must agree, and the
        // one a Commander with a controller in each hand reaches for without looking. Read after
        // the trigger because the action set's priority is claimed there, and only ever acted on
        // when it moves the panel somewhere — a grip squeezed at a root is a grip that meant
        // something else.
        if (_runtime.Actions.BackPressed() && _panel.Back())
        {
            _panel.Invalidate();
        }

        // What the ray is resting on, lit so the Commander can see they have found it. Null when
        // no ray is on the panel, which puts out whatever was lit.
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
                // Let go without ever having moved or dwelt: a press, not a carry. Delivered at
                // the point the finger went down rather than where it came up, which is what a
                // touch surface does and what makes a small target hittable at arm's length.
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
                    // A press must never be able to take the app down. The reported case — a
                    // combo box opening a popup on a window that is never shown — is handled
                    // where the press is decided rather than here, and this is the backstop for
                    // whatever control turns out to have the same shape next: the Commander
                    // loses one press and keeps their session.
                    _logger.LogError(ex, "A press on the panel threw at {U:0.00}, {V:0.00}", tap.U, tap.V);
                }
            }

            _pressed = null;

            if (_carrying is not null)
            {
                // Written once, on release. A drag is thirty poses a second and the settings store
                // writes a whole file atomically; persisting each one would be a hundred file
                // writes to record one gesture.
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

            // One button does both, so the gesture has to say which. A press that has neither
            // dwelt nor travelled is still undecided, and starting the carry immediately is what
            // made every attempt to press a control move the whole panel instead.
            if (_pressed is not { } pressed)
            {
                _pressed = new Press(_now, start.Hit.U, start.Hit.V);

                // A hand that came down on a scrollbar is scrolling, not carrying — decided at
                // the moment of the press and not revisited, so a drag that wanders off the bar
                // keeps scrolling rather than suddenly picking the panel up.
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

            // Picking it up means putting it somewhere, so it becomes world-locked. The setting
            // follows the action rather than gating it: a Commander who has physically carried the
            // panel across the cockpit has said where they want it, and a head-locked surface that
            // sprang back would be d47 arguing with them.
            var locked = _settings.Apply(VrCapability.LockKey(slot), "world", SettingsCaller.Hotkey);

            if (locked.Status != SettingApplyStatus.Applied)
            {
                // Said out loud, because the failure is otherwise invisible from inside a headset:
                // the panel is carried perfectly and springs back to the head on release, and
                // nothing anywhere says the setting refused to move.
                _logger.LogWarning(
                    "The panel was picked up but {Key} would not go to world: {Status} — {Detail}",
                    VrCapability.LockKey(slot),
                    locked.Status,
                    locked.Message);
            }

            _logger.LogDebug("The panel was picked up by device {Device}", start.Hand.Device);
            return;
        }

        // The hand that took it, and only that one. A second hand pressing does not steal the
        // panel, and the carrying hand losing tracking drops it where it was rather than following
        // a pose nobody has.
        if (Holding(hands) is not { } carrier)
        {
            return;
        }

        _anchors[slot] = Anchor(VrPlacementMath.Carried(_carrying.Value, carrier.Aim), head);
    }

    /// <summary>
    /// One frame of the aim ray, from <see cref="VrAimLoop"/> and never from the tick (#19).
    /// <para>
    /// This is the whole of what moved. It reads the poses, casts the ray at the geometry the last
    /// serve published, and places the beam and cursor quads — and it decides nothing. Whether a
    /// trigger was pulled, whether a carry starts, whether the panel goes back a level: all of that
    /// is still resolved on the tick, against its own sample, with its own state, on one thread.
    /// </para>
    /// <para>
    /// The cost of that split is that the drawn beam can be a few milliseconds ahead of the sample
    /// a click resolves against. At these rates it is invisible, and it buys single-threaded
    /// interaction state, which is worth far more.
    /// </para>
    /// </summary>
    private void Aim()
    {
        if (Volatile.Read(ref _aimGeometry) is not { } geometry)
        {
            return;
        }

        var (hands, head) = _runtime.HandsAndHead();

        // Published before the ray is placed, so the next tick's decision sees what this frame saw
        // rather than what the frame before it did.
        Volatile.Write(ref _aimHands, hands);

        if (head is not { } where)
        {
            return;
        }

        // A panel being carried follows the hand at frame rate rather than at the tick's ten
        // (#30). Nothing is decided here and nothing shared is written: the tick published that a
        // carry is running and which device holds it, and the tick still owns _anchors and the
        // one settings write that happens on release.
        //
        // The quad is placed directly rather than through _anchors, and that is the whole reason
        // this works. The serve is what turns an anchor into an overlay transform, and the serve
        // runs on the tick — so writing the anchor faster would have moved a number nothing reads
        // until the next tick anyway, while putting a plain Dictionary on two threads for nothing.
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

        // Read fresh in the same call as the hands. A head pose from the last serve is up to a
        // tenth of a second old, and a stale head while the head is turning misplaces the beam in
        // exactly the way this split exists to fix.
        Guide(
            hands,
            VrRay.PointingAt(hands, geometry.Resting, geometry.Extent, geometry.Curvature),
            geometry.Resting,
            geometry.Extent,
            where);
    }

    /// <summary>
    /// The beam and the cursor: where the Commander is pointing, drawn, because SteamVR is no
    /// longer drawing it for them.
    /// <para>
    /// <b>The beam's rule is "approaching", not "on".</b> A beam that only appeared once the ray
    /// was already on the panel would answer a question the cursor has already answered, and leave
    /// the real one — <em>where is the panel, and am I close?</em> — unanswered. So the test is a
    /// hit against a panel scaled up by <see cref="VrAim.BeamGuideScale"/>, which lights the beam
    /// as the hand comes near and keeps it off the rest of the time. An always-on laser in a
    /// cockpit is its own nuisance.
    /// </para>
    /// <para>
    /// The length says the same thing without a number. On the panel the beam stops exactly at the
    /// cursor, so it reads as touching; near but off, it is drawn a fixed short length and visibly
    /// falls short.
    /// </para>
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

            // One value feeds both, so the beam stops at exactly the point the cursor sits on and
            // the two meet whether or not the curvature model is right about where that point is.
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

    /// <summary>
    /// The hand currently carrying the panel, if it is still being tracked.
    /// <para>
    /// Nullable rather than <c>FirstOrDefault</c>: <see cref="VrHand"/> is a struct, so a missing
    /// hand comes back as a perfectly usable zero — device zero, at the origin, facing backwards —
    /// and following it would drag the panel to the Commander's feet the moment a controller went
    /// to sleep. That is the same shape as the empty-pose-slot trap one layer down.
    /// </para>
    /// </summary>
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
}
