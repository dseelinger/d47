using System.Runtime.InteropServices;
using D47.Vr.Binding;
using Microsoft.Extensions.Logging;
using Valve.VR;

namespace D47.Vr;

/// <summary>The trigger, read through <c>IVRInput</c>.</summary>
public sealed class VrActionInput(ILogger logger, IOpenVrSession session)
{
    private ulong _set;
    private ulong _grab;
    private ulong _back;

    /// <summary>The input interface, held from the moment the handles resolved. Null until then.</summary>
    private IOpenVrInput? _input;

    /// <summary>Whether the back button was down last frame, so a hold is one press.</summary>
    private bool _backWasDown;

    private VRActiveActionSet_t[]? _active;

    /// <summary>The same set at priority zero: what <see cref="Release"/> hands over.</summary>
    private VRActiveActionSet_t[]? _released;

    /// <summary>The refusal a failing release was last logged with, so ten a second is said once.</summary>
    private EVRInputError? _releaseRefused;

    /// <summary>The refusals a failing registration has already been logged with, so each is said once.</summary>
    private readonly HashSet<string> _registerRefused = new(StringComparer.Ordinal);

    /// <summary>The manifest paths, written on the first attempt and reused by every retry.</summary>
    private (string Actions, string Application)? _manifest;

    /// <summary>Whether the trigger can be read at all.</summary>
    public bool Ready => _input is not null;

    /// <summary>Whether this frame is claiming the controllers, for the diagnostic line.</summary>
    public bool HoldingPriority { get; private set; }

    /// <summary>
    /// Registers with SteamVR and loads the manifest. Called every tick until it succeeds: a refusal that
    /// persists writes the manifest files once and logs each distinct reason once.
    /// </summary>
    public void Register(string actionFolder)
    {
        if (_input is not null)
        {
            return;
        }

        try
        {
            if (session.Applications is not { } applications || session.Input is not { } input)
            {
                if (_registerRefused.Add("interface"))
                {
                    logger.LogWarning("SteamVR has no input interface; the panel stays display-only");
                }

                return;
            }

            if (_manifest is null)
            {
                var written = VrActionManifest.Write(actionFolder);
                _manifest = (written, VrActionManifest.WriteAppManifest(actionFolder, written));
            }

            var (actions, application) = _manifest.Value;

            // Temporary, so it evaporates on a SteamVR restart rather than accumulating stale entries in the
            // Commander's application list.
            var added = applications.AddApplicationManifest(application, true);

            if (added != EVRApplicationError.None && _registerRefused.Add($"application:{added}"))
            {
                logger.LogWarning("SteamVR would not take d47's application manifest: {Error}", added);
            }

            var identified = applications.IdentifyApplication(
                (uint)Environment.ProcessId,
                VrActionManifest.AppKey);

            if (identified != EVRApplicationError.None && _registerRefused.Add($"identify:{identified}"))
            {
                logger.LogWarning("SteamVR would not identify d47 as {Key}: {Error}", VrActionManifest.AppKey, identified);
            }

            var loaded = input.SetActionManifestPath(actions);

            if (loaded != EVRInputError.None)
            {
                if (_registerRefused.Add($"manifest:{loaded}"))
                {
                    logger.LogWarning("SteamVR would not load the action manifest at {Path}: {Error}", actions, loaded);
                }

                return;
            }

            var set = 0ul;
            var grab = 0ul;
            var back = 0ul;

            if (input.GetActionSetHandle(VrActionManifest.ActionSet, ref set) != EVRInputError.None
                || input.GetActionHandle(VrActionManifest.GrabAction, ref grab) != EVRInputError.None
                || input.GetActionHandle(VrActionManifest.BackAction, ref back) != EVRInputError.None)
            {
                if (_registerRefused.Add("handles"))
                {
                    logger.LogWarning("The action handles would not resolve; the panel stays display-only");
                }

                return;
            }

            _set = set;
            _grab = grab;
            _back = back;
            _input = input;
            _registerRefused.Clear();

            logger.LogInformation("Controller input is on: the trigger carries the panel");
        }
        catch (Exception ex)
        {
            // No action input is a downgrade, not a failure.
            if (_registerRefused.Add($"exception:{ex.GetType()}"))
            {
                logger.LogWarning(ex, "Controller input is unavailable; the panel stays display-only");
            }
        }
    }

    /// <summary>
    /// Whether the trigger is held — and, as a side effect, whether the controllers are claimed this
    /// frame.
    /// </summary>
    public bool TriggerHeld(bool wanted)
    {
        if (_input is not { } input || !wanted)
        {
            Release();
            return false;
        }

        _active ??= [ClaimSet(_set)];

        if (!HoldingPriority)
        {
            // Once per claim rather than per frame, and at a level the installed log keeps: the 2026-08-22
            // controller report was diagnosed with no line on this side saying when the controllers had been
            // taken, and vrserver.txt does not say either.
            logger.LogInformation("Claimed the controllers at overlay priority");
        }

        HoldingPriority = true;

        var updated = input.UpdateActionState(
            _active,
            (uint)Marshal.SizeOf<VRActiveActionSet_t>());

        if (updated != EVRInputError.None)
        {
            return false;
        }

        var data = default(InputDigitalActionData_t);

        var read = input.GetDigitalActionData(
            _grab,
            ref data,
            (uint)Marshal.SizeOf<InputDigitalActionData_t>(),
            OpenVR.k_ulInvalidInputValueHandle);

        // bActive as well as bState: an action bound to nothing, or on a controller that has gone to sleep,
        // reports a perfectly confident false rather than an error.
        return read == EVRInputError.None && data is { bActive: true, bState: true };
    }

    /// <summary>Gives the controllers back.</summary>
    public void Release()
    {
        if (_input is not { } input || !HoldingPriority)
        {
            HoldingPriority = false;
            return;
        }

        _backWasDown = false;
        _released ??= [ReleaseSet(_set)];

        var released = input.UpdateActionState(_released, (uint)Marshal.SizeOf<VRActiveActionSet_t>());

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
    /// The set as a claim: overlay global priority, so the trigger and grip come here rather than to
    /// whatever else wants them.
    /// </summary>
    public static VRActiveActionSet_t ClaimSet(ulong set) => new()
    {
        ulActionSet = set,
        ulRestrictedToDevice = OpenVR.k_ulInvalidInputValueHandle,
        nPriority = OpenVR.k_nActionSetOverlayGlobalPriorityMin,
    };

    /// <summary>The set as a release: the same set at priority zero.</summary>
    public static VRActiveActionSet_t ReleaseSet(ulong set) => new()
    {
        ulActionSet = set,
        ulRestrictedToDevice = OpenVR.k_ulInvalidInputValueHandle,
        nPriority = 0,
    };

    /// <summary>
    /// Whether the back button was pressed this frame (Phase 25, "Drill in, and find your way back").
    /// </summary>
    public bool BackPressed()
    {
        if (_input is not { } input || !HoldingPriority)
        {
            _backWasDown = false;
            return false;
        }

        var data = default(InputDigitalActionData_t);

        var read = input.GetDigitalActionData(
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
