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
    private bool _ready;

    private VRActiveActionSet_t[]? _active;

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

            if (OpenVR.Input.GetActionSetHandle(VrActionManifest.ActionSet, ref set) != EVRInputError.None
                || OpenVR.Input.GetActionHandle(VrActionManifest.GrabAction, ref grab) != EVRInputError.None)
            {
                logger.LogWarning("The action handles would not resolve; the panel stays display-only");
                return;
            }

            _set = set;
            _grab = grab;
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
    /// An action set activated at <c>k_nActionSetOverlayGlobalPriorityMin</c> takes the controllers
    /// from whatever else wants them; at priority zero it loses to the running scene application
    /// and receives nothing at all. So the set is activated at overlay priority <em>only</em> while
    /// a ray is on the panel or a carry is already running.
    /// </para>
    /// <para>
    /// Elite does not bind motion controllers, so this is not about not disturbing the game. It is
    /// that Virtual Desktop and the SteamVR dashboard do want them, and holding global priority for
    /// a whole session would take them hostage every moment the Commander is not pointing at the
    /// panel — which is nearly all of them. The tell is physical: the trigger's own haptic tick
    /// stops while they are held.
    /// </para>
    /// <para>
    /// An action set is active only for the frame it is passed in, so <em>not</em> calling this is
    /// the release. There is no matching deactivate to forget.
    /// </para>
    /// </summary>
    public bool TriggerHeld(bool wanted)
    {
        if (!_ready || !wanted)
        {
            HoldingPriority = false;
            return false;
        }

        _active ??=
        [
            new VRActiveActionSet_t
            {
                ulActionSet = _set,
                ulRestrictedToDevice = OpenVR.k_ulInvalidInputValueHandle,
                nPriority = OpenVR.k_nActionSetOverlayGlobalPriorityMin,
            },
        ];

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
}
