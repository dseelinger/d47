using System.Numerics;
using System.Runtime.InteropServices;
using D47.Core.Storage;
using D47.Core.Vr;
using D47.Vr.Binding;
using Microsoft.Extensions.Logging;
using Valve.VR;

namespace D47.Vr;

/// <summary>The controllers' buttons and stick, read through <c>IVRInput</c>.</summary>
public sealed class VrActionInput(ILogger logger, IOpenVrSession session)
{
    private ulong _set;
    private ulong _grab;
    private ulong _back;
    private ulong _primary;
    private ulong _secondary;
    private ulong _menu;
    private ulong _stickClick;
    private ulong _stick;
    private readonly ulong[] _source = new ulong[2];
    private readonly uint[] _lastHeld = new uint[2];
    private readonly (int X, int Y)[] _lastStick = new (int, int)[2];
    private readonly Lock _gate = new();

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

    /// <summary>Whether this frame is claiming the controllers, for the diagnostic line.</summary>
    public bool HoldingPriority { get; private set; }

    /// <summary>
    /// Registers with SteamVR and loads the manifest. Called every tick until it succeeds: a refusal that
    /// persists writes the manifest files once and logs each distinct reason once.
    /// </summary>
    public void Register(IFileSystem files, string actionFolder)
    {
        lock (_gate)
        {
            RegisterLocked(files, actionFolder);
        }
    }

    private void RegisterLocked(IFileSystem files, string actionFolder)
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
                var written = VrActionManifest.Write(files, actionFolder);
                _manifest = (written, VrActionManifest.WriteAppManifest(files, actionFolder, written));
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
            var primary = 0ul;
            var secondary = 0ul;
            var menu = 0ul;
            var stickClick = 0ul;
            var stick = 0ul;
            var left = 0ul;
            var right = 0ul;

            if (input.GetActionSetHandle(VrActionManifest.ActionSet, ref set) != EVRInputError.None
                || input.GetActionHandle(VrActionManifest.GrabAction, ref grab) != EVRInputError.None
                || input.GetActionHandle(VrActionManifest.BackAction, ref back) != EVRInputError.None
                || input.GetActionHandle(VrActionManifest.PrimaryAction, ref primary) != EVRInputError.None
                || input.GetActionHandle(VrActionManifest.SecondaryAction, ref secondary) != EVRInputError.None
                || input.GetActionHandle(VrActionManifest.MenuAction, ref menu) != EVRInputError.None
                || input.GetActionHandle(VrActionManifest.StickClickAction, ref stickClick) != EVRInputError.None
                || input.GetActionHandle(VrActionManifest.StickAction, ref stick) != EVRInputError.None
                || input.GetInputSourceHandle("/user/hand/left", ref left) != EVRInputError.None
                || input.GetInputSourceHandle("/user/hand/right", ref right) != EVRInputError.None)
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
            _primary = primary;
            _secondary = secondary;
            _menu = menu;
            _stickClick = stickClick;
            _stick = stick;
            _source[LeftHand] = left;
            _source[RightHand] = right;
            _input = input;
            _registerRefused.Clear();

            logger.LogInformation("Controller input is on: the trigger carries the panel and every button is read");
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
        lock (_gate)
        {
            return TriggerHeldLocked(wanted);
        }
    }

    private bool TriggerHeldLocked(bool wanted)
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
        lock (_gate)
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
        lock (_gate)
        {
            return BackPressedLocked();
        }
    }

    private bool BackPressedLocked()
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

    public const int LeftHand = 0;
    public const int RightHand = 1;

    /// <summary>
    /// Refreshes the action state. A held claim stands; otherwise the set is updated at priority zero, which
    /// reads without taking an input from another application.
    /// </summary>
    public bool Refresh()
    {
        lock (_gate)
        {
            if (_input is not { } input)
            {
                return false;
            }

            var sets = HoldingPriority ? _active ??= [ClaimSet(_set)] : _released ??= [ReleaseSet(_set)];

            return input.UpdateActionState(sets, (uint)Marshal.SizeOf<VRActiveActionSet_t>()) == EVRInputError.None;
        }
    }

    /// <summary>One hand's buttons (a bit per <see cref="VrButton"/>) and stick, as of the last <see cref="Refresh"/>.</summary>
    public (uint Held, Vector2 Stick) ReadHand(int hand)
    {
        lock (_gate)
        {
            if (_input is not { } input)
            {
                return default;
            }

            var source = _source[hand];
            var held = 0u;

            (VrButton Button, ulong Action)[] buttons =
            [
                (VrButton.Trigger, _grab),
                (VrButton.Grip, _back),
                (VrButton.Primary, _primary),
                (VrButton.Secondary, _secondary),
                (VrButton.Menu, _menu),
                (VrButton.StickClick, _stickClick),
            ];

            foreach (var (button, action) in buttons)
            {
                var data = default(InputDigitalActionData_t);

                var read = input.GetDigitalActionData(
                    action,
                    ref data,
                    (uint)Marshal.SizeOf<InputDigitalActionData_t>(),
                    source);

                if (read == EVRInputError.None && data is { bActive: true, bState: true })
                {
                    held |= VrHand.Bit(button);
                }
            }

            var analog = default(InputAnalogActionData_t);

            var stick = input.GetAnalogActionData(
                _stick,
                ref analog,
                (uint)Marshal.SizeOf<InputAnalogActionData_t>(),
                source) == EVRInputError.None
                && analog.bActive
                && float.IsFinite(analog.x)
                && float.IsFinite(analog.y)
                    ? new Vector2(Math.Clamp(analog.x, -1f, 1f), Math.Clamp(analog.y, -1f, 1f))
                    : default;

            Say(hand, held, stick);

            return (held, stick);
        }
    }

    /// <summary>Logs each button edge, and the stick when it moves to another quarter-step.</summary>
    private void Say(int hand, uint held, Vector2 stick)
    {
        var name = hand == LeftHand ? "left" : "right";
        var changed = held ^ _lastHeld[hand];

        foreach (var button in Enum.GetValues<VrButton>())
        {
            if ((changed & VrHand.Bit(button)) != 0)
            {
                logger.LogInformation(
                    "The {Hand} {Button} was {State}",
                    name,
                    button,
                    (held & VrHand.Bit(button)) != 0 ? "pressed" : "released");
            }
        }

        _lastHeld[hand] = held;

        var step = ((int)MathF.Round(stick.X * 4f), (int)MathF.Round(stick.Y * 4f));

        if (step != _lastStick[hand])
        {
            _lastStick[hand] = step;
            logger.LogInformation("The {Hand} stick moved to {X:0.00}, {Y:0.00}", name, stick.X, stick.Y);
        }
    }
}
