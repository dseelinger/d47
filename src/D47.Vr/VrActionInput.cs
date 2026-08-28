using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;
using Valve.VR;

namespace D47.Vr;

/// <summary>
/// The trigger, read through <c>IVRInput</c>.
/// <para>
/// <b>This replaced the overlay mouse channel, and had to.</b> The three calls that used to carry
/// controller input — <c>SetOverlayInputMethod(Mouse)</c>, the
/// <c>MakeOverlaysInteractiveIfVisible</c> flag and a mouse scale — opt an overlay in to SteamVR's
/// own <em>laser</em>, and SteamVR only runs that laser over its own dashboard. With Elite holding
/// the headset, <c>PollNextOverlayEvent</c> returns nothing, forever: no pointer, no grab, and no
/// error anywhere saying why. It works perfectly with the game closed, which is what made it look
/// like it worked at all.
/// </para>
/// <para>
/// Action input has no such dependency. Two earlier attempts at it, in two other projects,
/// concluded it was a dead end — the manifest loaded, the handles resolved, and nothing ever
/// fired. Both were missing the same step, and it is the one in <see cref="Register"/>.
/// </para>
/// <para>
/// Fail-soft throughout: a runtime that refuses any of this leaves the panel display-only, which
/// is exactly what it was before. It is never a reason not to start.
/// </para>
/// </summary>
public sealed class VrActionInput(ILogger logger)
{
    private ulong _set;
    private ulong _grab;
    private ulong _back;
    private bool _ready;

    /// <summary>Whether the back button was down last frame, so a hold is one press.</summary>
    private bool _backWasDown;

    private VRActiveActionSet_t[]? _active;

    /// <summary>The same set at priority zero: what <see cref="Release"/> hands over.</summary>
    private VRActiveActionSet_t[]? _released;

    /// <summary>The refusal a failing release was last logged with, so ten a second is said once.</summary>
    private EVRInputError? _releaseRefused;

    /// <summary>Whether the trigger can be read at all. False leaves the panel display-only.</summary>
    public bool Ready => _ready;

    /// <summary>Whether this frame is claiming the controllers, for the diagnostic line.</summary>
    public bool HoldingPriority { get; private set; }

    /// <summary>
    /// Registers with SteamVR and loads the manifest. Idempotent, and safe to call before the
    /// Commander has ever pointed at anything.
    /// <para>
    /// <b>The application registration is the load-bearing half.</b> SteamVR files bindings under
    /// an app key, and a process it does not recognise has none — so without
    /// <c>AddApplicationManifest</c> and <c>IdentifyApplication</c> there is nothing for a binding
    /// to attach to. The manifest still loads. The handles still resolve. Nothing ever fires, the
    /// app never appears under Manage Controller Bindings, and the only place that says so is
    /// <c>vrserver.txt</c>. That is the whole of why this was twice believed impossible.
    /// </para>
    /// </summary>
    public void Register(string actionFolder)
    {
        if (_ready)
        {
            return;
        }

        try
        {
            var actions = VrActionManifest.Write(actionFolder);
            var application = VrActionManifest.WriteAppManifest(actionFolder, actions);

            // Temporary, so it evaporates on a SteamVR restart rather than accumulating stale
            // entries in the Commander's application list.
            var added = OpenVR.Applications.AddApplicationManifest(application, true);

            if (added != EVRApplicationError.None)
            {
                logger.LogWarning("SteamVR would not take d47's application manifest: {Error}", added);
            }

            var identified = OpenVR.Applications.IdentifyApplication(
                (uint)Environment.ProcessId,
                VrActionManifest.AppKey);

            if (identified != EVRApplicationError.None)
            {
                logger.LogWarning("SteamVR would not identify d47 as {Key}: {Error}", VrActionManifest.AppKey, identified);
            }

            var loaded = OpenVR.Input.SetActionManifestPath(actions);

            if (loaded != EVRInputError.None)
            {
                logger.LogWarning("SteamVR would not load the action manifest at {Path}: {Error}", actions, loaded);
                return;
            }

            var set = 0ul;
            var grab = 0ul;
            var back = 0ul;

            if (OpenVR.Input.GetActionSetHandle(VrActionManifest.ActionSet, ref set) != EVRInputError.None
                || OpenVR.Input.GetActionHandle(VrActionManifest.GrabAction, ref grab) != EVRInputError.None
                || OpenVR.Input.GetActionHandle(VrActionManifest.BackAction, ref back) != EVRInputError.None)
            {
                logger.LogWarning("The action handles would not resolve; the panel stays display-only");
                return;
            }

            _set = set;
            _grab = grab;
            _back = back;
            _ready = true;

            logger.LogInformation("Controller input is on: the trigger carries the panel");
        }
        catch (Exception ex)
        {
            // No action input is a downgrade, not a failure. The panel still shows, still speaks,
            // and can still be placed by voice.
            logger.LogWarning(ex, "Controller input is unavailable; the panel stays display-only");
        }
    }

    /// <summary>
    /// Whether the trigger is held — and, as a side effect, whether the controllers are claimed
    /// this frame.
    /// <para>
    /// <paramref name="wanted"/> is the priority gate, and it is why this takes an argument at all.
    /// An action set activated at <c>k_nActionSetOverlayGlobalPriorityMin</c> takes the bound
    /// inputs from every other application — <em>if</em> SteamVR's "Enable global input from
    /// overlays" developer setting is on. It is off by default (<c>globalActionSetPriority</c> in
    /// SteamVR's <c>default.vrsettings</c>), and with it off the overlay range changes nothing:
    /// d47 receives its trigger at priority zero as well, alongside whatever else is bound to it,
    /// which is what Valve's description of the field and two shipping overlays both say. This
    /// class used to say the opposite — that at priority zero it received nothing — and that was
    /// reasoning, never a measurement. So the set is activated at overlay priority <em>only</em>
    /// while a ray is on the panel or a carry is already running, for the Commander who has the
    /// setting on.
    /// </para>
    /// <para>
    /// Elite does not bind motion controllers, so this is not about not disturbing the game. It is
    /// that Virtual Desktop and the SteamVR dashboard do want them, and holding global priority for
    /// a whole session would — with that setting on — take them hostage every moment the Commander
    /// is not pointing at the panel, which is nearly all of them.
    /// </para>
    /// <para>
    /// The claim outlives the frame: SteamVR holds an application's last active action-set list
    /// until the next <c>UpdateActionState</c> from that application, so every path that stops
    /// wanting the controllers has to say so through <see cref="Release"/>. This used to claim
    /// the opposite, and the Commander's controllers hung for it.
    /// </para>
    /// </summary>
    public bool TriggerHeld(bool wanted)
    {
        if (!_ready || !wanted)
        {
            Release();
            return false;
        }

        _active ??= [ClaimSet(_set)];

        if (!HoldingPriority)
        {
            // Once per claim rather than per frame, and at a level the installed log keeps: the
            // 2026-08-22 controller report was diagnosed with no line on this side saying when
            // the controllers had been taken, and vrserver.txt does not say either.
            logger.LogInformation("Claimed the controllers at overlay priority");
        }

        HoldingPriority = true;

        var updated = OpenVR.Input.UpdateActionState(
            _active,
            (uint)Marshal.SizeOf<VRActiveActionSet_t>());

        if (updated != EVRInputError.None)
        {
            return false;
        }

        var data = default(InputDigitalActionData_t);

        var read = OpenVR.Input.GetDigitalActionData(
            _grab,
            ref data,
            (uint)Marshal.SizeOf<InputDigitalActionData_t>(),
            OpenVR.k_ulInvalidInputValueHandle);

        // bActive as well as bState: an action bound to nothing, or on a controller that has gone
        // to sleep, reports a perfectly confident false rather than an error.
        return read == EVRInputError.None && data is { bActive: true, bState: true };
    }

    /// <summary>
    /// Gives the controllers back. Idempotent, and cheap when there is nothing to give back.
    /// <para>
    /// <b>Not calling <see cref="TriggerHeld"/> is not the release, and this class used to say
    /// it was.</b> SteamVR keeps an application's last active action-set list in force until
    /// that application calls <c>UpdateActionState</c> again, so a frame that claimed the
    /// controllers at overlay priority and was followed by frames that simply did not call — the
    /// ray left the panel, the panel stopped taking the pointer, the session ended — left the
    /// claim standing. The Commander's report (2026-08-21): "Motion Controller appears hung",
    /// and only restarting the headset freed it — which, it turned out on 2026-08-22, a standing
    /// claim could not have caused on his install, where the setting that gives the overlay range
    /// its teeth is off; that report is an open defect in bugs.md. The release is still an
    /// explicit call, made on every path out of a claim, because a Commander who turns the
    /// setting on is owed one.
    /// </para>
    /// <para>
    /// <b>And an empty set list is not the release either, which is how 0.48.6 shipped one that
    /// never released anything.</b> SteamVR refuses <c>UpdateActionState</c> with no sets in it
    /// — <c>NoActiveActionSet</c>, six times in thirty seconds in the installed log of
    /// 2026-08-22 — and leaves the last list it was given standing. What it takes is the same
    /// set at priority zero: below the overlay range, where a set takes inputs from no other
    /// application. And a refusal leaves the claim recorded as standing, so the next frame tries
    /// again — the old code forgot the claim on this side alone, which is a release that happens
    /// in the log and nowhere else.
    /// </para>
    /// </summary>
    public void Release()
    {
        if (!_ready || !HoldingPriority)
        {
            HoldingPriority = false;
            return;
        }

        _backWasDown = false;
        _released ??= [ReleaseSet(_set)];

        var released = OpenVR.Input.UpdateActionState(_released, (uint)Marshal.SizeOf<VRActiveActionSet_t>());

        if (released != EVRInputError.None)
        {
            if (_releaseRefused != released)
            {
                _releaseRefused = released;
                logger.LogWarning("Could not release the controllers: {Error}; the claim stands and the release is retried", released);
            }

            return;
        }

        _releaseRefused = null;
        HoldingPriority = false;

        logger.LogInformation("Gave the controllers back");
    }

    /// <summary>
    /// The set as a claim: overlay global priority, so the trigger and grip come here rather
    /// than to whatever else wants them. <see cref="TriggerHeld"/> is why that is only ever
    /// asked for while a ray is on the panel.
    /// </summary>
    public static VRActiveActionSet_t ClaimSet(ulong set) => new()
    {
        ulActionSet = set,
        ulRestrictedToDevice = OpenVR.k_ulInvalidInputValueHandle,
        nPriority = OpenVR.k_nActionSetOverlayGlobalPriorityMin,
    };

    /// <summary>
    /// The set as a release: the same set at priority zero. A set rather than none, because
    /// SteamVR refuses an empty list and keeps the claim; priority zero, because only the overlay
    /// range takes inputs from other applications. See <see cref="Release"/>.
    /// </summary>
    public static VRActiveActionSet_t ReleaseSet(ulong set) => new()
    {
        ulActionSet = set,
        ulRestrictedToDevice = OpenVR.k_ulInvalidInputValueHandle,
        nPriority = 0,
    };

    /// <summary>
    /// Whether the back button was <em>pressed</em> this frame (Phase 25, "Drill in, and
    /// find your way back").
    /// <para>
    /// The edge rather than the level, and that is the whole of it: this runs at frame rate and a
    /// held grip is one press, not ninety levels of going back.
    /// </para>
    /// <para>
    /// Read only while the action set is already active, which <see cref="TriggerHeld"/> decides
    /// for the frame - so the same priority argument holds, and a grip squeezed while the
    /// Commander is not pointing at the panel belongs to whatever else wants it.
    /// </para>
    /// </summary>
    public bool BackPressed()
    {
        if (!_ready || !HoldingPriority)
        {
            _backWasDown = false;
            return false;
        }

        var data = default(InputDigitalActionData_t);

        var read = OpenVR.Input.GetDigitalActionData(
            _back,
            ref data,
            (uint)Marshal.SizeOf<InputDigitalActionData_t>(),
            OpenVR.k_ulInvalidInputValueHandle);

        var down = read == EVRInputError.None && data is { bActive: true, bState: true };
        var pressed = down && !_backWasDown;

        _backWasDown = down;

        return pressed;
    }
}
